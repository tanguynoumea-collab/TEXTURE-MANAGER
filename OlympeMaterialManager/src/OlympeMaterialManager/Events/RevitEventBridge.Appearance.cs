using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Partie Apparence (DR2-1) : lecture best-effort de la COULEUR D'APPARENCE
/// des matériaux — la couleur diffuse/albedo de l'asset d'apparence, celle que
/// la vue 3D « Réaliste » de Revit affiche pour un matériau sans texture.
/// Remplace l'ancienne résolution de textures bitmap (supprimée après
/// diagnostic terrain : zéro texture résolvable sur les données réelles).
/// Lecture mémoire pure sur le thread Revit dans les handlers — aucune I/O
/// disque. Introuvable → null, JAMAIS d'exception : le fallback couleur
/// graphique côté UI est le chemin nominal.
/// </summary>
public partial class RevitEventBridge
{
    /// <summary>
    /// Issue de la résolution de la couleur d'apparence d'un matériau, pour la
    /// ligne de synthèse de HandleGetAllMaterials (pattern DR1-3).
    /// </summary>
    private enum AppearanceResolution { Resolved, NoAsset, NoColor }

    /// <summary>
    /// Cache par session du bridge : AppearanceAssetId → (couleur ARGB ou null,
    /// statut de résolution). Les matériaux partageant le même asset (fréquent)
    /// ne coûtent qu'une lecture. Accès uniquement depuis le thread Revit
    /// (Execute est séquentiel) — pas de synchronisation nécessaire. Un asset
    /// édité en cours de session peut rester en cache : accepté (coût/bénéfice,
    /// cache d'aperçu).
    /// </summary>
    private static readonly Dictionary<long, (int? Argb, AppearanceResolution Status)>
        _appearanceColorByAssetId = new();

    /// <summary>
    /// Ids des matériaux SANS asset d'apparence déjà tracés dans olympe.log :
    /// le cas n'entre pas dans le cache par asset (pas d'asset), ce set évite
    /// de répéter la ligne de diagnostic à chaque rafraîchissement.
    /// </summary>
    private static readonly HashSet<long> _noAssetLoggedMaterialIds = new();

    /// <summary>
    /// FIA2-02 (conservé) : clé (PathName, ou titre si non enregistré) du
    /// document ayant rempli le cache. Un changement de document actif vide le
    /// cache — sans cette dimension, une valeur d'ElementId identique dans un
    /// autre document servirait la couleur d'apparence de l'ancien document.
    /// </summary>
    private static string? _appearanceCacheDocKey;

    /// <summary>
    /// Retourne la couleur d'apparence ARGB d'un matériau (alpha 255), ou null
    /// (pas d'asset d'apparence, asset sans propriété couleur exploitable).
    /// </summary>
    private static int? GetMaterialAppearanceColorArgb(Document doc, Material material)
        => GetMaterialAppearanceColorArgb(doc, material, out _);

    /// <summary>
    /// Variante avec statut de résolution (synthèse de HandleGetAllMaterials).
    /// Diagnostic de terrain : UNE ligne LogService.Info par matériau à la
    /// première résolution de son asset (les matériaux partageant un asset déjà
    /// résolu passent par le cache, sans nouvelle ligne) — jamais conditionnée
    /// au verbose.
    /// </summary>
    private static int? GetMaterialAppearanceColorArgb(Document doc, Material material,
        out AppearanceResolution status)
    {
        try
        {
            // FIA2-02 : invalider le cache si le document a changé.
            var docKey = string.IsNullOrEmpty(doc.PathName) ? doc.Title : doc.PathName;
            if (!string.Equals(_appearanceCacheDocKey, docKey, StringComparison.Ordinal))
            {
                _appearanceColorByAssetId.Clear();
                _noAssetLoggedMaterialIds.Clear();
                _appearanceCacheDocKey = docKey;
            }

            var assetId = material.AppearanceAssetId;
            if (assetId == ElementId.InvalidElementId)
            {
                status = AppearanceResolution.NoAsset;
                // Tracé une seule fois par matériau (set dédié : le cas n'entre
                // pas dans le cache par asset).
                if (_noAssetLoggedMaterialIds.Add(ElementIdHelper.GetValue(material.Id)))
                    LogService.Info($"Apparence '{material.Name}': pas d'asset d'apparence");
                return null;
            }

            long cacheKey = ElementIdHelper.GetValue(assetId);
            if (_appearanceColorByAssetId.TryGetValue(cacheKey, out var cached))
            {
                status = cached.Status;
                return cached.Argb;
            }

            int? argb = null;
            if (doc.GetElement(assetId) is AppearanceAssetElement assetElem)
            {
                argb = FindAppearanceColorArgb(assetElem.GetRenderingAsset());
            }

            if (argb != null)
            {
                status = AppearanceResolution.Resolved;
                LogService.Info(
                    $"Apparence '{material.Name}': OK #{argb.Value & 0xFFFFFF:X6}");
            }
            else
            {
                status = AppearanceResolution.NoColor;
                LogService.Info($"Apparence '{material.Name}': asset sans couleur");
            }

            _appearanceColorByAssetId[cacheKey] = (argb, status);
            return argb;
        }
        catch (Exception ex)
        {
            // Best-effort : un asset exotique ne doit jamais faire échouer la requête.
            LogService.Error(
                $"Lecture de la couleur d'apparence impossible pour \"{material.Name}\"", ex);
            status = AppearanceResolution.NoColor;
            return null;
        }
    }

    /// <summary>
    /// Cherche la couleur diffuse/albedo d'un asset d'apparence.
    /// Schéma générique d'abord (constante typée Generic.GenericDiffuse, prouvée
    /// par compilation sur les deux cibles), puis balayage de PREMIER NIVEAU
    /// pour les autres schémas (PBR…) : première AssetPropertyDoubleArray4d dont
    /// le nom évoque une couleur de base. Pas de descente dans les assets
    /// connectés : la couleur de base des schémas connus est une DoubleArray4d
    /// de premier niveau — aucune récursion, donc aucun risque de cycle.
    /// </summary>
    private static int? FindAppearanceColorArgb(Asset? asset)
    {
        if (asset == null) return null;

        // Schéma générique (la grande majorité des matériaux Revit).
        if (asset.FindByName(RevitAssetProps.GenericDiffuse)
            is AssetPropertyDoubleArray4d generic)
        {
            var packed = ToArgb(generic);
            if (packed != null) return packed;
        }

        // Autres schémas : première couleur plausible de premier niveau.
        for (int i = 0; i < asset.Size; i++)
        {
            if (asset.Get(i) is AssetPropertyDoubleArray4d prop &&
                IsBaseColorName(prop.Name))
            {
                var packed = ToArgb(prop);
                if (packed != null) return packed;
            }
        }

        return null;
    }

    /// <summary>
    /// Un nom de propriété désigne-t-il une couleur de base ? (« diffuse »,
    /// « albedo » ou « color », insensible à la casse — couvre generic_diffuse,
    /// opaque_albedo, surface_albedo, *_color des schémas Autodesk).
    /// </summary>
    private static bool IsBaseColorName(string name)
        => name.IndexOf("diffuse", StringComparison.OrdinalIgnoreCase) >= 0
           || name.IndexOf("albedo", StringComparison.OrdinalIgnoreCase) >= 0
           || name.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// Convertit une AssetPropertyDoubleArray4d [R,G,B,A] normalisée 0-1 en
    /// ARGB int (alpha forcé à 255 : l'aperçu est opaque). Valeurs bornées
    /// 0-1 avant conversion (contenu tiers hors plage → jamais d'overflow).
    /// </summary>
    private static int? ToArgb(AssetPropertyDoubleArray4d prop)
    {
        var values = prop.GetValueAsDoubles();
        if (values == null || values.Count < 3) return null;
        return ArgbUtils.PackArgb(ToByte(values[0]), ToByte(values[1]), ToByte(values[2]));
    }

    /// <summary>Composante 0-1 → octet 0-255 (borne manuelle : pas de Math.Clamp en net48).</summary>
    private static byte ToByte(double component)
    {
        if (component <= 0) return 0;
        if (component >= 1) return 255;
        return (byte)Math.Round(component * 255);
    }
}
