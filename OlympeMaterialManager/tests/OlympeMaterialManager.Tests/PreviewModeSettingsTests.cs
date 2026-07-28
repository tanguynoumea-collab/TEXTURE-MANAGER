using System.IO;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Tests du socle du mode d'aperçu (B10-S) :
/// round-trip du mode via settings.json, tolérance aux valeurs inconnues
/// (jamais de quarantaine pour un simple mode invalide) et compatibilité
/// avec les fichiers v1 existants sans le champ MaterialPreviewMode.
/// Chaque test travaille dans un répertoire temporaire isolé. Aucun type Revit.
/// </summary>
public class PreviewModeSettingsTests : IDisposable
{
    private readonly string _dir;
    private readonly PresetService _service;

    public PreviewModeSettingsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "OlympeTests-" + Guid.NewGuid().ToString("N"));
        _service = new PresetService(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    // ---- Round-trip ----

    [Fact]
    public void Store_ChangementDeMode_PersisteImmediatement_EtRechargeIdentique()
    {
        var store = new PreviewModeStore(_service);
        Assert.Equal(PreviewMode.UniformColor, store.CurrentMode);

        store.CurrentMode = PreviewMode.Realistic;

        // Persistance immédiate : le fichier contient la valeur STRING
        var raw = File.ReadAllText(SettingsPath);
        Assert.Contains("\"materialPreviewMode\": \"Realistic\"", raw);

        // Un nouveau store (nouvelle session) recharge le même mode
        var reloaded = new PreviewModeStore(new PresetService(_dir));
        Assert.Equal(PreviewMode.Realistic, reloaded.CurrentMode);
    }

    [Fact]
    public void SaveSettings_DefautSerialise_UniformColor()
    {
        _service.SaveSettings(new AppSettingsDto());
        var raw = File.ReadAllText(SettingsPath);
        Assert.Contains("\"materialPreviewMode\": \"UniformColor\"", raw);
    }

    // ---- Valeur inconnue → défaut, sans quarantaine ----

    [Theory]
    [InlineData("Fantaisie")]
    [InlineData("")]
    [InlineData("999")] // numérique hors plage : TryParse accepterait, Enum.IsDefined rejette
    public void Store_ValeurInconnue_RetombeSurDefaut_SansQuarantaine(string value)
    {
        File.WriteAllText(SettingsPath,
            $"{{ \"schemaVersion\": 1, \"presetFiles\": [], \"materialPreviewMode\": \"{value}\" }}");

        var store = new PreviewModeStore(_service);

        Assert.Equal(PreviewMode.UniformColor, store.CurrentMode);
        // Le fichier reste en place : une valeur de mode inconnue n'est PAS une corruption
        Assert.True(File.Exists(SettingsPath));
        Assert.Empty(Directory.GetFiles(_dir, "settings.json.corrupt-*"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Fantaisie")]
    public void Parse_ValeurInvalide_RetourneUniformColor(string? value)
    {
        Assert.Equal(PreviewMode.UniformColor, PreviewModeStore.Parse(value));
    }

    [Theory]
    [InlineData("UniformColor", PreviewMode.UniformColor)]
    [InlineData("Realistic", PreviewMode.Realistic)]
    [InlineData("realistic", PreviewMode.Realistic)] // tolérance de casse
    public void Parse_ValeurValide_RetourneLeMode(string value, PreviewMode expected)
    {
        Assert.Equal(expected, PreviewModeStore.Parse(value));
    }

    // ---- Migration DR2-2 : « Texture » (mode supprimé) → Realistic ----

    [Theory]
    [InlineData("Texture")]
    [InlineData("texture")] // tolérance de casse
    public void Parse_AncienneValeurTexture_MappeVersRealistic(string value)
    {
        Assert.Equal(PreviewMode.Realistic, PreviewModeStore.Parse(value));
    }

    [Fact]
    public void Store_FichierAvecAncienModeTexture_ChargeRealistic_SansQuarantaine()
    {
        // settings.json écrit par une version antérieure à DR2-2
        File.WriteAllText(SettingsPath,
            "{ \"schemaVersion\": 1, \"presetFiles\": [], \"materialPreviewMode\": \"Texture\" }");

        var store = new PreviewModeStore(_service);

        Assert.Equal(PreviewMode.Realistic, store.CurrentMode);
        Assert.True(File.Exists(SettingsPath));
        Assert.Empty(Directory.GetFiles(_dir, "settings.json.corrupt-*"));
    }

    // ---- Champ absent (fichier v1 existant) → défaut ----

    [Fact]
    public void Store_FichierV1SansChampMode_RetombeSurDefaut()
    {
        File.WriteAllText(SettingsPath,
            "{ \"schemaVersion\": 1, \"presetFiles\": [\"Preset par defaut\"], \"activePresetName\": \"Preset par defaut\" }");

        var store = new PreviewModeStore(_service);

        Assert.Equal(PreviewMode.UniformColor, store.CurrentMode);
    }

    [Fact]
    public void LoadSettings_FichierV1SansChampMode_DeserialiseAvecDefaut()
    {
        File.WriteAllText(SettingsPath, "{ \"schemaVersion\": 1, \"presetFiles\": [] }");

        var settings = _service.LoadSettings();

        Assert.Equal("UniformColor", settings.MaterialPreviewMode);
    }
}
