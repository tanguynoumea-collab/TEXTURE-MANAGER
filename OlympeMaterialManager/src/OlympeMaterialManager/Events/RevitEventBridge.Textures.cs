using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Partie Textures (B10-TX) : lecture best-effort du chemin de texture bitmap
/// des assets d'apparence. FindTexturePath est restauré de l'historique
/// (f337be7^) — la marche récursive sur les assets connectés couvre une partie
/// des schémas PBR sans table exhaustive. La résolution disque (séparateur « | »,
/// racines Autodesk, Revit.ini) est déléguée à TexturePathResolver (testable).
/// Aucun décodage bitmap ici : le thread Revit ne lit que des chaînes.
/// Introuvable → null, JAMAIS d'exception : le fallback couleur côté UI est
/// le chemin nominal.
/// </summary>
public partial class RevitEventBridge
{
    /// <summary>
    /// Cache par session du bridge : AppearanceAssetId → chemin résolu (ou null).
    /// Les matériaux partageant le même asset (fréquent) ne coûtent qu'une marche.
    /// Accès uniquement depuis le thread Revit (Execute est séquentiel) —
    /// pas de synchronisation nécessaire. Un asset édité en cours de session
    /// peut rester en cache : accepté (coût/bénéfice, cache d'aperçu).
    /// </summary>
    private static readonly Dictionary<long, string?> _texturePathByAssetId = new();

    /// <summary>
    /// Retourne le chemin de la texture bitmap d'un matériau, résolu vers un
    /// fichier existant, ou null (pas d'asset, pas de bitmap, introuvable).
    /// </summary>
    private static string? GetMaterialTexturePath(Document doc, Material material)
    {
        try
        {
            var assetId = material.AppearanceAssetId;
            if (assetId == ElementId.InvalidElementId) return null;

            long cacheKey = ElementIdHelper.GetValue(assetId);
            if (_texturePathByAssetId.TryGetValue(cacheKey, out var cached))
                return cached;

            string? resolved = null;
            if (doc.GetElement(assetId) is AppearanceAssetElement assetElem)
            {
                var rawPath = FindTexturePath(assetElem.GetRenderingAsset());
                resolved = TexturePathResolver.Resolve(rawPath);
            }

            _texturePathByAssetId[cacheKey] = resolved;
            return resolved;
        }
        catch (Exception ex)
        {
            // Best-effort : un asset exotique ne doit jamais faire échouer la requête.
            LogService.Error(
                $"Lecture du chemin de texture impossible pour \"{material.Name}\"", ex);
            return null;
        }
    }

    /// <summary>
    /// Cherche recursivement un chemin de texture bitmap dans un Asset Revit
    /// (restauré de f337be7^). Parcourt les proprietes de type Asset (connectes)
    /// et String pour trouver "unifiedbitmap_Bitmap" ou tout chemin finissant
    /// par une extension image.
    /// </summary>
    private static string? FindTexturePath(Asset? asset)
    {
        if (asset == null) return null;

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
                        var found = FindTexturePath(connectedAsset);
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
                    return val;
                }
            }
        }

        return null;
    }
}
