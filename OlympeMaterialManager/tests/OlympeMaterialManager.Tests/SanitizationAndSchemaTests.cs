using System.IO;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Tests des comportements introduits par le LOT 3a du council :
/// SEC-01 (sanitisation canonique des noms de fichiers),
/// TST-06 (garde anti-imbrication de la migration de repertoire),
/// DON-03 (SchemaVersion des DTOs racine),
/// DON-09 (validation JSON avant import externe).
/// Chaque test travaille dans un repertoire temporaire isole. Aucun type Revit.
/// </summary>
public class SanitizationAndSchemaTests : IDisposable
{
    private readonly string _dir;
    private readonly PresetService _service;

    public SanitizationAndSchemaTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "OlympeTests-" + Guid.NewGuid().ToString("N"));
        _service = new PresetService(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private string PresetPath(string name) => Path.Combine(_dir, "presets", name + ".json");
    private string ScenePath(string name) => Path.Combine(_dir, "scenes", name + ".json");

    // ---- SEC-01 : noms invalides rejetes par la persistance ----

    [Theory]
    [InlineData("a/b")]            // separateur
    [InlineData("a\\b")]           // separateur (traversal ..\..\x passe par ici)
    [InlineData("a:b")]            // caractere interdit
    [InlineData("a?b")]            // caractere interdit
    [InlineData("CON")]            // nom reserve Windows
    [InlineData("lpt1")]           // nom reserve, insensible a la casse
    [InlineData("")]               // vide
    [InlineData("   ")]            // espaces
    public void SavePreset_NomInvalide_LeveArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(
            () => _service.SavePreset(name, PresetService.GetDefaultCollection()));
    }

    [Theory]
    [InlineData("..\\evil")]
    [InlineData("NUL")]
    public void DeleteScene_NomInvalide_LeveArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() => _service.DeleteScene(name));
    }

    [Theory]
    [InlineData("Mur beton")]
    [InlineData("scene-01")]
    [InlineData("Facade_Sud.v2")]
    public void ValidateFileName_NomValide_RetourneNull(string name)
    {
        Assert.Null(PresetService.ValidateFileName(name));
    }

    [Theory]
    [InlineData("a<b")]
    [InlineData("COM3")]
    [InlineData(null)]
    public void ValidateFileName_NomInvalide_RetourneMessageFrancais(string? name)
    {
        var error = PresetService.ValidateFileName(name);
        Assert.NotNull(error);
        Assert.NotEqual(string.Empty, error);
    }

    [Fact]
    public void SavePreset_NomValide_EcritBienSousLeDossierPresets()
    {
        _service.SavePreset("Valide", PresetService.GetDefaultCollection());
        Assert.True(File.Exists(PresetPath("Valide")));
    }

    // ---- TST-06 : garde de migration ----

    [Fact]
    public void MigrateProjectDirectory_CheminIdentique_NoOpSansException()
    {
        // Le chemin courant (config reelle ou fallback %APPDATA%) redonne a l'identique :
        // la methode doit retourner sans rien copier ni lever.
        var oldPath = PresetService.GetProjectDirectory()
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Olympe", "MaterialManager");

        // Variante avec separateur final : doit etre normalisee et rester un no-op.
        PresetService.MigrateProjectDirectory(oldPath + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void MigrateProjectDirectory_CheminImbrique_LeveArgumentException()
    {
        var oldPath = PresetService.GetProjectDirectory()
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Olympe", "MaterialManager");
        var nested = Path.Combine(oldPath, "sous-dossier", "projet");

        var ex = Assert.Throws<ArgumentException>(
            () => PresetService.MigrateProjectDirectory(nested));
        Assert.Contains("interieur", ex.Message); // message francais explicite

        // La garde doit lever AVANT toute creation de dossier
        Assert.False(Directory.Exists(nested));
    }

    // ---- DON-03 : SchemaVersion ----

    [Fact]
    public void SavePreset_EcritSchemaVersion_EtRoundTrip()
    {
        _service.SavePreset("Versionne", PresetService.GetDefaultCollection());

        var raw = File.ReadAllText(PresetPath("Versionne"));
        Assert.Contains("\"schemaVersion\": 1", raw);

        var loaded = _service.LoadPreset("Versionne");
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded!.SchemaVersion);
    }

    [Fact]
    public void LoadPreset_FichierV0SansSchemaVersion_DeserialiseAvecDefautUn()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "presets"));
        File.WriteAllText(PresetPath("Legacy"),
            "{ \"groups\": [ { \"groupName\": \"Murs\", \"materials\": [] } ] }");

        var loaded = _service.LoadPreset("Legacy");

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded!.SchemaVersion);
        Assert.Single(loaded.Groups);
    }

    [Fact]
    public void LoadScene_FichierV0SansSchemaVersion_DeserialiseAvecDefautUn()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "scenes"));
        File.WriteAllText(ScenePath("LegacyScene"), "{ \"name\": \"LegacyScene\", \"types\": [] }");

        var loaded = _service.LoadScene("LegacyScene");

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded!.SchemaVersion);
    }

    // ---- DON-09 : validation JSON avant import externe ----

    [Theory]
    [InlineData("{ tronque")]
    [InlineData("pas du json")]
    public void IsValidPresetJson_JsonInvalide_RetourneFalse(string json)
    {
        Assert.False(PresetService.IsValidPresetJson(json));
    }

    [Fact]
    public void IsValidPresetJson_JsonValide_RetourneTrue()
    {
        Assert.True(PresetService.IsValidPresetJson(
            "{ \"schemaVersion\": 1, \"groups\": [] }"));
    }

    [Fact]
    public void IsValidSceneJson_JsonInvalide_RetourneFalse()
    {
        Assert.False(PresetService.IsValidSceneJson("<xml/>"));
    }

    [Fact]
    public void IsValidSceneJson_JsonValide_RetourneTrue()
    {
        Assert.True(PresetService.IsValidSceneJson(
            "{ \"name\": \"S\", \"types\": [] }"));
    }
}
