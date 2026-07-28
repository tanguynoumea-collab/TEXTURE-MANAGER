using System.IO;
using Olympe.MaterialManager.Helpers;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Tests de la résolution des chemins de texture (B10-TX, restaurée et corrigée
/// en DR4-1) via la surcharge à racines injectables (le cache, les racines
/// machine et l'index de fond ne sont pas exercés — la retombée par nom de
/// fichier est injectée). Contrat : préfixe « lib:?... » retiré (tout jusqu'à
/// « ? » inclus) ; « / » → « \ » ; « | » → premier existant ; absolu existant →
/// tel quel ; relatif → sondé contre les racines ; introuvable → null, jamais
/// d'exception.
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

    [Theory]
    [InlineData(@"Maps\UnifiedBitmap\UnifiedBitmap.png", true)]
    [InlineData(@"lib:?Maps\UnifiedBitmap\UnifiedBitmap.png", true)]
    [InlineData(@"C:\Program Files (x86)\Common Files\Autodesk Shared\Materials\2023\assetlibrary_base.fbm\Maps\UnifiedBitmap\UnifiedBitmap.png", true)]
    [InlineData("Maps/UnifiedBitmap/UnifiedBitmap.png", true)]
    [InlineData(@"UNIFIEDBITMAP.PNG", true)]
    [InlineData(@"Mats\Brick\Brick_Running.png", false)]
    [InlineData(@"C:\Textures\MaBrique.jpg", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsGenericPlaceholder_DetecteLePlaceholderAutodesk(string? path, bool attendu)
    {
        Assert.Equal(attendu, TexturePathResolver.IsGenericPlaceholder(path));
    }

    [Theory]
    [InlineData(@"Mats\Metal\external.dependency\Simple_Metal_Mtl_Break_pattern.jpg", true)]
    [InlineData(@"Simple_Concrete_Mtl_BroomCurved_pattern.jpg", true)]
    [InlineData(@"Brick_bump.png", true)]
    [InlineData(@"Wood_normal.tif", true)]
    [InlineData(@"Metal_gloss.jpg", true)]
    [InlineData(@"Fence_cutout.png", true)]
    [InlineData(@"1\Mats\Brick_Non_Uniform_Running_Burgundy.png", false)]
    [InlineData(@"cmu_running_200x400_gray.png", false)]
    [InlineData(null, false)]
    public void IsNonColorMap_DetecteLesCartesTechniques(string? path, bool attendu)
    {
        Assert.Equal(attendu, TexturePathResolver.IsNonColorMap(path));
    }

    [Fact]
    public void Resolve_CheminRelatifStyleTextures_ResoutContreRacineTextures()
    {
        // Reproduit le cas terrain « 1\mats\brick_non_uniform_running_burgundy.png »
        // avec une racine simulant Materials\Textures (casse differente sur disque).
        var expected = CreateFile(@"1\Mats\Brick_Non_Uniform_Running_Burgundy.png");
        var resolved = TexturePathResolver.Resolve(
            @"1\mats\brick_non_uniform_running_burgundy.png", Roots);
        Assert.Equal(expected, resolved, ignoreCase: true);
    }

    [Fact]
    public void Resolve_CheminAbsoluExistant_RetourneTelQuel()
    {
        var abs = CreateFile("beton.png");
        Assert.Equal(abs, TexturePathResolver.Resolve(abs, Roots));
    }

    [Fact]
    public void Resolve_CheminRelatif_SondeLesRacines()
    {
        var expected = CreateFile(Path.Combine("Maps", "UnifiedBitmap", "UnifiedBitmap.png"));
        Assert.Equal(expected,
            TexturePathResolver.Resolve(@"Maps\UnifiedBitmap\UnifiedBitmap.png", Roots));
    }

    [Fact]
    public void Resolve_PrefixeLib_EstRetireJusquAuPointDInterrogationInclus()
    {
        // DR4-1 : observé dans olympe.log — « lib:?Maps\... ». Tout ce qui
        // précède et inclut « ? » est retiré avant la sonde.
        var expected = CreateFile(Path.Combine("Maps", "chene.jpg"));
        Assert.Equal(expected,
            TexturePathResolver.Resolve(@"lib:?Maps\chene.jpg", Roots));
    }

    [Fact]
    public void Resolve_SeparateursSlash_SontNormalisesEnAntislash()
    {
        // DR4-1 : les assets étrangers utilisent « / » (ex. C:/Users/...).
        var expected = CreateFile(Path.Combine("Maps", "granit.png"));
        Assert.Equal(expected,
            TexturePathResolver.Resolve("Maps/granit.png", Roots));
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
    public void Resolve_CheminAbsoluEtranger_RetombeSurLIndexNomDeFichier()
    {
        // DR4-1 : chemin absolu d'une autre machine (« C:/Users/shenj/... » du
        // log) dont le fichier vit dans un SOUS-DOSSIER d'une racine — la sonde
        // directe racine\nom échoue, l'index nom → chemin (injecté ici) résout.
        var expected = CreateFile(Path.Combine("Mats", "1", "brique.png"));
        string? Lookup(string fileName) =>
            string.Equals(fileName, "brique.png", StringComparison.OrdinalIgnoreCase)
                ? expected : null;

        Assert.Equal(expected,
            TexturePathResolver.Resolve("C:/Users/shenj/Textures/brique.png", Roots, Lookup));
    }

    [Fact]
    public void Resolve_CheminUnc_RetombeSurLeNomDeFichierContreLesRacinesLocales()
    {
        // FIA2-01 (conservé) : le chemin UNC complet n'est pas sondé (risque de
        // timeout SMB sur le thread Revit) — seule la retombée « nom de fichier
        // contre les racines locales » s'applique.
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
    [InlineData("lib:?")]
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

    [Fact]
    public void WarmUp_EstIdempotentEtNeLevePas()
    {
        // DR6-2 : prechauffage appele depuis App.OnStartup — il doit rendre la
        // main immediatement (construction en tache de fond, jamais d'attente sur
        // le thread Revit) et supporter d'etre appele plusieurs fois (la garde
        // Interlocked n'autorise qu'une seule construction par session).
        var sw = System.Diagnostics.Stopwatch.StartNew();
        TexturePathResolver.WarmUp();
        TexturePathResolver.WarmUp();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"WarmUp a bloqué {sw.ElapsedMilliseconds} ms : l'index doit se construire en tâche de fond.");
    }
}
