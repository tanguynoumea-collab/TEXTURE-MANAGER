using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Services;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Tests de la couleur moyenne de texture (DR4-2) via la méthode de calcul
/// synchrone (le cache et la tâche de fond de TryGetAverageArgb ne sont pas
/// exercés — non déterministes en test). Fixture générée à la volée : un PNG
/// damier rouge/bleu 16×16 dont la moyenne attendue est le violet (128, 0, 128).
/// </summary>
public class TextureAverageColorTests : IDisposable
{
    private readonly string _dir;

    public TextureAverageColorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "OlympeAvg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Écrit un PNG 16×16 en damier 2×2 rouge pur / bleu pur (moitié de chaque,
    /// sans anticrénelage : pixels posés un à un) et retourne son chemin.
    /// </summary>
    private string CreateCheckerboardPng()
    {
        const int size = 16;
        var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool red = ((x / 2) + (y / 2)) % 2 == 0;
                int i = (y * size + x) * 4;
                pixels[i] = red ? (byte)0 : (byte)255;     // B
                pixels[i + 1] = 0;                          // G
                pixels[i + 2] = red ? (byte)255 : (byte)0;  // R
                pixels[i + 3] = 255;                        // A
            }
        }
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, size, size), pixels, size * 4, 0);

        var path = Path.Combine(_dir, "damier_rouge_bleu.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    [Fact]
    public void ComputeAverageArgb_DamierRougeBleu_RetourneLeViolet()
    {
        var path = CreateCheckerboardPng();

        var result = TextureAverageColor.ComputeAverageArgb(path);

        Assert.NotNull(result);
        var (a, r, g, b) = ArgbUtils.UnpackArgb(result!.Value);
        Assert.Equal(255, a);           // résultat opaque
        Assert.Equal(128, r);           // moitié de 255, arrondi
        Assert.Equal(0, g);
        Assert.Equal(128, b);
    }

    [Fact]
    public void ComputeAverageArgb_FichierIntrouvable_RetourneNull_SansException()
    {
        Assert.Null(TextureAverageColor.ComputeAverageArgb(
            Path.Combine(_dir, "inexistant.png")));
    }

    [Fact]
    public void ComputeAverageArgb_FichierCorrompu_RetourneNull_SansException()
    {
        var path = Path.Combine(_dir, "corrompu.png");
        File.WriteAllBytes(path, new byte[] { 0x42, 0x42, 0x42 });

        Assert.Null(TextureAverageColor.ComputeAverageArgb(path));
    }

    [Fact]
    public void TryGetAverageArgb_CheminNullOuVide_RetourneNull()
    {
        Assert.Null(TextureAverageColor.TryGetAverageArgb(null));
        Assert.Null(TextureAverageColor.TryGetAverageArgb(string.Empty));
    }
}
