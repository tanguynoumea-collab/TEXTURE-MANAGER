using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Partie Textures (B10-TX, restaurée en DR4-1) : lecture best-effort du chemin
/// de texture bitmap des assets d'apparence. FindTexturePath est restauré de
/// l'historique (5727ec4^) — la marche récursive sur les assets connectés couvre
/// une partie des schémas PBR sans table exhaustive. La résolution disque
/// (préfixe « lib:? », séparateur « | », racines assetlibrary_*.fbm, Revit.ini,
/// index nom de fichier) est déléguée à TexturePathResolver (testable).
/// Aucun décodage bitmap ici : le thread Revit ne lit que des chaînes.
/// Introuvable → null, JAMAIS d'exception : le fallback couleur d'apparence
/// côté UI est le chemin nominal (mode opportuniste, décision utilisateur DR4).
/// </summary>
public partial class RevitEventBridge
{
    /// <summary>
    /// DR1-3 : issue de la résolution de texture d'un matériau, pour la ligne
    /// de synthèse de HandleGetAllMaterials. NoBitmap couvre à la fois
    /// « pas d'asset d'apparence » et « asset sans propriété bitmap ».
    /// </summary>
    private enum TextureResolution { Resolved, NoBitmap, Unresolved }

    /// <summary>
    /// Cache par session du bridge : AppearanceAssetId → (chemin résolu ou null,
    /// statut de résolution). Les matériaux partageant le même asset (fréquent)
    /// ne coûtent qu'une marche. Accès uniquement depuis le thread Revit
    /// (Execute est séquentiel) — pas de synchronisation nécessaire. Un asset
    /// édité en cours de session peut rester en cache : accepté (coût/bénéfice,
    /// cache d'aperçu).
    /// </summary>
    private static readonly Dictionary<long, (string? Path, TextureResolution Status)>
        _texturePathByAssetId = new();

    /// <summary>
    /// FIA2-02 (conservé) : clé (PathName, ou titre si non enregistré) du
    /// document ayant rempli le cache. Un changement de document actif vide le
    /// cache — sans cette dimension, une valeur d'ElementId identique dans un
    /// autre document servirait le chemin de texture de l'ancien document.
    /// </summary>
    private static string? _texturePathCacheDocKey;

    /// <summary>
    /// Retourne le chemin de la texture bitmap d'un matériau, résolu vers un
    /// fichier existant, ou null (pas d'asset, pas de bitmap, introuvable).
    /// </summary>
    private static string? GetMaterialTexturePath(Document doc, Material material)
        => GetMaterialTexturePath(doc, material, out _);

    /// <summary>
    /// Variante avec statut de résolution (DR1-3, synthèse de HandleGetAllMaterials).
    /// Diagnostic de terrain : UNE ligne LogService.Info par matériau à la première
    /// résolution DÉFINITIVE de son asset (les matériaux partageant un asset déjà
    /// résolu passent par le cache, sans nouvelle ligne) — jamais conditionnée au
    /// verbose. DR4-1 : un échec pendant que l'index nom de fichier se construit
    /// est PROVISOIRE — ni mis en cache ni tracé, il sera retenté au prochain
    /// rafraîchissement.
    /// </summary>
    private static string? GetMaterialTexturePath(Document doc, Material material,
        out TextureResolution status)
    {
        try
        {
            // FIA2-02 : invalider le cache si le document a changé.
            var docKey = string.IsNullOrEmpty(doc.PathName) ? doc.Title : doc.PathName;
            if (!string.Equals(_texturePathCacheDocKey, docKey, StringComparison.Ordinal))
            {
                _texturePathByAssetId.Clear();
                _texturePathCacheDocKey = docKey;
            }

            var assetId = material.AppearanceAssetId;
            if (assetId == ElementId.InvalidElementId)
            {
                // Pas d'asset d'apparence : rien à résoudre (pas de ligne de log,
                // le partial Apparence trace déjà ce cas une fois par matériau).
                status = TextureResolution.NoBitmap;
                return null;
            }

            long cacheKey = ElementIdHelper.GetValue(assetId);
            if (_texturePathByAssetId.TryGetValue(cacheKey, out var cached))
            {
                status = cached.Status;
                return cached.Path;
            }

            string? rawPath = null;
            string? resolved = null;
            if (doc.GetElement(assetId) is AppearanceAssetElement assetElem)
            {
                rawPath = FindTexturePath(assetElem.GetRenderingAsset());
                resolved = TexturePathResolver.Resolve(rawPath);
            }

            // Ceinture et bretelles (retour terrain DR4) : si la résolution
            // aboutit malgré tout au placeholder générique (ex. via l'index nom
            // de fichier), le traiter comme « pas de texture ».
            if (resolved != null && TexturePathResolver.IsGenericPlaceholder(resolved))
            {
                rawPath = null;
                resolved = null;
            }

            // DR4-1 : chemin présent mais non résolu ALORS QUE l'index nom de
            // fichier n'est pas prêt → verdict provisoire : pas de cache, pas de
            // trace (elle se répéterait à chaque rafraîchissement), le prochain
            // rafraîchissement profitera de l'index.
            if (resolved == null && rawPath != null &&
                !TexturePathResolver.IsFileNameIndexReady)
            {
                status = TextureResolution.Unresolved;
                return null;
            }

            // DR1-3 : trace de terrain à la première résolution — permet de lire
            // dans olympe.log pourquoi le mode Réaliste retombe (ou non) en couleur.
            if (resolved != null)
            {
                status = TextureResolution.Resolved;
                LogService.Info($"Texture '{material.Name}': OK {resolved}");
            }
            else if (rawPath == null)
            {
                status = TextureResolution.NoBitmap;
                LogService.Info($"Texture '{material.Name}': asset sans bitmap");
            }
            else
            {
                status = TextureResolution.Unresolved;
                LogService.Info($"Texture '{material.Name}': chemin non résolu '{rawPath}'");
            }

            _texturePathByAssetId[cacheKey] = (resolved, status);
            return resolved;
        }
        catch (Exception ex)
        {
            // Best-effort : un asset exotique ne doit jamais faire échouer la requête.
            LogService.Error(
                $"Lecture du chemin de texture impossible pour \"{material.Name}\"", ex);
            status = TextureResolution.Unresolved;
            return null;
        }
    }

    /// <summary>
    /// FIA2-03 (conservé) : profondeur maximale de la marche récursive sur les
    /// assets connectés. Les schémas d'apparence légitimes tiennent en 2-3
    /// niveaux ; la garde évite un StackOverflow fatal (non rattrapable) sur un
    /// graphe d'assets cyclique produit par un contenu tiers.
    /// </summary>
    private const int MaxAssetWalkDepth = 8;

    /// <summary>
    /// Cherche recursivement un chemin de texture bitmap dans un Asset Revit
    /// (restauré de 5727ec4^). Parcourt les proprietes de type Asset (connectes)
    /// et String pour trouver "unifiedbitmap_Bitmap" ou tout chemin finissant
    /// par une extension image. Profondeur bornée par MaxAssetWalkDepth (FIA2-03).
    /// </summary>
    private static string? FindTexturePath(Asset? asset, int depth = 0)
    {
        if (asset == null || depth >= MaxAssetWalkDepth) return null;

        for (int i = 0; i < asset.Size; i++)
        {
            var prop = asset.Get(i);
            if (prop == null) continue;

            // Chercher dans les sous-assets connectes (ex: generic_diffuse -> unifiedbitmap)
            if (prop.NumberOfConnectedProperties > 0)
            {
                for (int c = 0; c < prop.NumberOfConnectedProperties; c++)
                {
                    if (prop.GetConnectedProperty(c) is Asset connectedAsset)
                    {
                        var found = FindTexturePath(connectedAsset, depth + 1);
                        if (found != null) return found;
                    }
                }
            }

            // Chercher "unifiedbitmap_Bitmap" ou propriete String contenant un chemin image
            if (prop is AssetPropertyString strProp && !string.IsNullOrEmpty(strProp.Value))
            {
                string val = strProp.Value;
                if (strProp.Name == RevitAssetProps.UnifiedBitmapPath ||
                    val.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    val.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    val.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                    val.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                    val.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
                {
                    // Retour terrain DR4 : ignorer le placeholder générique et
                    // CONTINUER la marche — la vraie texture peut vivre dans un
                    // autre canal du même asset.
                    if (!TexturePathResolver.IsGenericPlaceholder(val))
                        return val;
                }
            }
        }

        return null;
    }
}
