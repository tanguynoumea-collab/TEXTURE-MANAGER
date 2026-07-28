using System.IO;
using Olympe.MaterialManager.Helpers;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Tests de la résolution des chemins de texture (B10-TX) via la surcharge à
/// racines injectables (le cache et les racines machine ne sont pas exercés).
/// Contrat : « | » → premier existant ; absolu existant → tel quel ; relatif →
/// sondé contre les racines ; introuvable → null, jamais d'exception.
/// </summary>
public class TexturePathResolverTests : IDisposable
{
    private readonly string _root;

    public TexturePathResolverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "OlympeTex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private string CreateFile(string relativePath)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[] { 0x42 });
        return full;
    }

    private string[] Roots => new[] { _root };

    [Fact]
    public void Resolve_CheminAbsoluExistant_RetourneTelQuel()
    {
        var abs = CreateFile("beton.png");
        Assert.Equal(abs, TexturePathResolver.Resolve(abs, Roots));
    }

    [Fact]
    public void Resolve_CheminRelatif_SondeLesRacines()
    {
        var expected = CreateFile(Path.Combine("1", "Mats", "chene.jpg"));
        Assert.Equal(expected, TexturePathResolver.Resolve(@"1\Mats\chene.jpg", Roots));
    }

    [Fact]
    public void Resolve_CheminsMultiplesSeparesParPipe_RetourneLePremierExistant()
    {
        var existing = CreateFile("existe.png");
        var raw = @"C:\Introuvable\fantome.png|existe.png|autre.png";
        Assert.Equal(existing, TexturePathResolver.Resolve(raw, Roots));
    }

    [Fact]
    public void Resolve_CheminAbsoluDUneAutreMachine_RetombeSurLeNomDeFichier()
    {
        var expected = CreateFile("granit.tif");
        Assert.Equal(expected,
            TexturePathResolver.Resolve(@"D:\MachineOrigine\Textures\granit.tif", Roots));
    }

    [Fact]
    public void Resolve_CheminUnc_RetombeSurLeNomDeFichierContreLesRacinesLocales()
    {
        // FIA2-01 : le chemin UNC complet n'est pas sondé (risque de timeout SMB
        // sur le thread Revit) — seule la retombée « nom de fichier contre les
        // racines locales » s'applique.
        var expected = CreateFile("tex.png");
        Assert.Equal(expected,
            TexturePathResolver.Resolve(@"\\serveur\share\tex.png", Roots));
    }

    [Fact]
    public void Resolve_CheminUnc_Introuvable_RetourneNullRapidementSansException()
    {
        // FIA2-01 : sans sonde réseau, la résolution d'un UNC inconnu est
        // quasi instantanée (aucun File.Exists sur \\serveur...).
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = TexturePathResolver.Resolve(
            @"\\serveur-inexistant-olympe\share\fantome.png", Roots);
        sw.Stop();

        Assert.Null(result);
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Résolution UNC trop lente ({sw.ElapsedMilliseconds} ms) : une sonde réseau a probablement eu lieu.");
    }

    [Theory]
    [InlineData("introuvable.png")]
    [InlineData(@"C:\Nulle\Part\rien.jpg")]
    [InlineData("")]
    [InlineData("   |   ")]
    [InlineData("nom<invalide>.png")] // caractères interdits : ignoré sans exception
    public void Resolve_Introuvable_RetourneNull_SansException(string raw)
    {
        Assert.Null(TexturePathResolver.Resolve(raw, Roots));
    }

    [Fact]
    public void Resolve_ValeurBrute_NullOuVide_RetourneNull()
    {
        Assert.Null(TexturePathResolver.Resolve(null));
        Assert.Null(TexturePathResolver.Resolve("   "));
    }
}
