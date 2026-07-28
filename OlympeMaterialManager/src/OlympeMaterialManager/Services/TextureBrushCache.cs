using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Olympe.MaterialManager.Services;

/// <summary>
/// Chargeur de miniatures de texture partagé, UI-ONLY (B8/B10-UI, restauré en
/// DR4-2). Décode chaque bitmap une seule fois par session (DecodePixelWidth=64,
/// CacheOption.OnLoad : le fichier est relâché immédiatement), fige l'image
/// (Freeze) pour un usage multi-bindings sans souci de thread d'affinité.
/// Échec de décodage (fichier corrompu, format inconnu, disque retiré) → null
/// mémorisé : le fallback couleur est le chemin nominal, jamais d'exception.
/// AUCUN décodage côté bridge/thread Revit : ce cache n'est appelé que par
/// les converters WPF sur le thread UI.
/// </summary>
public static class TextureBrushCache
{
    private static readonly ConcurrentDictionary<string, ImageSource?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Retourne la miniature figée d'une texture, ou null (chemin vide,
    /// fichier illisible). Le résultat — y compris l'échec — est mis en cache.
    /// </summary>
    public static ImageSource? GetThumbnail(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        return _cache.GetOrAdd(path!, Load);
    }

    private static ImageSource? Load(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = 64;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            // DR1-3 : l'échec de décodage reste non bloquant (fallback couleur),
            // mais il est tracé — une seule fois par chemin grâce au cache — pour
            // que le diagnostic de terrain distingue « chemin non résolu » (bridge)
            // de « fichier résolu mais illisible » (UI).
            LogService.Error($"Échec de décodage de la texture '{path}'", ex);
            return null;
        }
    }
}
