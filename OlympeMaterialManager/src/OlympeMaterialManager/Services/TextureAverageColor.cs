using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Olympe.MaterialManager.Helpers;

namespace Olympe.MaterialManager.Services;

/// <summary>
/// Couleur MOYENNE d'une image de texture, UI-ONLY (DR4-2) — alimente le liseré
/// des cartes du panneau central et les pastilles du panneau droit en mode
/// Réaliste. DÉCISION UTILISATEUR EXPLICITE (design-review cycle 3) : la
/// moyenne de l'image est retenue malgré l'objection « brun indiscriminant »
/// du council — ce choix utilisateur l'outrepasse, motivé par la nouvelle
/// donnée disque (bibliothèques Autodesk présentes, textures résolvables).
/// Calcul « au fil de l'eau » : MESURE sur les bibliothèques réelles
/// (20 images, DecodePixelWidth=16, cache disque froid) = 15 ms en moyenne,
/// 128 ms au pire par image — PAS « quelques ms », donc pas de calcul synchrone
/// sur le thread UI : la moyenne est calculée en tâche de fond (Task.Run) au
/// premier besoin ; tant qu'elle n'est pas prête, l'appelant retombe sur la
/// couleur d'apparence et le prochain rafraîchissement affichera la moyenne.
/// Échec de décodage → null mémorisé (fallback couleur, jamais d'exception).
/// </summary>
public static class TextureAverageColor
{
    /// <summary>
    /// Cache par session : chemin → couleur moyenne ARGB (null = échec de
    /// décodage mémorisé). Présence de la clé = calcul terminé.
    /// </summary>
    private static readonly ConcurrentDictionary<string, int?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Chemins dont le calcul de fond est déjà programmé (évite de re-poster
    /// une tâche à chaque passage du converter avant la fin du calcul).
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte> _pending =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Retourne la couleur moyenne ARGB si elle est déjà calculée, sinon null
    /// IMMÉDIATEMENT (jamais d'attente sur le thread UI) en programmant le
    /// calcul en tâche de fond — le prochain rafraîchissement des bindings la
    /// trouvera dans le cache.
    /// </summary>
    public static int? TryGetAverageArgb(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        if (_cache.TryGetValue(path!, out var cached)) return cached;

        if (_pending.TryAdd(path!, 0))
        {
            Task.Run(() =>
            {
                _cache[path!] = ComputeAverageArgb(path!);
                _pending.TryRemove(path!, out _);
            });
        }
        return null;
    }

    /// <summary>
    /// Calcule la couleur moyenne d'une image (synchrone, testable en xunit) :
    /// décodage DecodePixelWidth=16 (OnLoad : fichier relâché), conversion
    /// Bgra32, moyenne arithmétique des composantes R, G, B (alpha ignoré,
    /// résultat opaque). Échec (fichier corrompu, format inconnu) → null.
    /// </summary>
    public static int? ComputeAverageArgb(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = 16;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            converted.Freeze();

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            if (width <= 0 || height <= 0) return null;

            var pixels = new byte[width * height * 4];
            converted.CopyPixels(pixels, width * 4, 0);

            long sumR = 0, sumG = 0, sumB = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                sumB += pixels[i];
                sumG += pixels[i + 1];
                sumR += pixels[i + 2];
            }

            int count = width * height;
            return ArgbUtils.PackArgb(
                (byte)Math.Round(sumR / (double)count),
                (byte)Math.Round(sumG / (double)count),
                (byte)Math.Round(sumB / (double)count));
        }
        catch (Exception ex)
        {
            // Même contrat que TextureBrushCache : échec non bloquant, tracé une
            // seule fois par chemin (le cache mémorise le null).
            LogService.Error($"Échec du calcul de la couleur moyenne de '{path}'", ex);
            return null;
        }
    }
}
