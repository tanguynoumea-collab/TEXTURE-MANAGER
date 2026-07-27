using System.IO;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;
using Xunit;

namespace Olympe.MaterialManager.Tests;

/// <summary>
/// Tests de la couche de persistance PresetService (couture d'arbitrage du council,
/// DON-01 ecriture atomique, DON-02 quarantaine des fichiers illisibles).
/// Chaque test travaille dans un repertoire temporaire isole injecte au constructeur.
/// Aucun type Revit n'est exerce.
/// </summary>
public class PresetServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly PresetService _service;

    public PresetServiceTests()
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

    // ---- Chargement nominal / round-trip ----

    [Fact]
    public void SavePreset_PuisLoadPreset_RoundTrip()
    {
        var collection = new PresetCollectionDto();
        collection.Groups.Add(new PresetGroupDto { GroupName = "Facades" });
        collection.Groups.Add(new PresetGroupDto { GroupName = "Toitures" });

        _service.SavePreset("Test", collection);
        var loaded = _service.LoadPreset("Test");

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Groups.Count);
        Assert.Equal("Facades", loaded.Groups[0].GroupName);
        Assert.Equal("Toitures", loaded.Groups[1].GroupName);
    }

    [Fact]
    public void SaveScene_PuisLoadScene_RoundTrip()
    {
        var scene = new SceneDto { Name = "Scene A" };
        _service.SaveScene("Scene A", scene);

        var loaded = _service.LoadScene("Scene A");

        Assert.NotNull(loaded);
        Assert.Equal("Scene A", loaded!.Name);
    }

    // ---- Fichier absent -> defaut (comportement normal) ----

    [Fact]
    public void LoadPreset_FichierAbsent_RetourneCollectionParDefaut()
    {
        var loaded = _service.LoadPreset("Inexistant");

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded!.Groups.Count); // Murs, Sols, Autres
        Assert.False(File.Exists(PresetPath("Inexistant")));
    }

    // ---- Fichier illisible -> quarantaine + echec signale (DON-02) ----

    [Fact]
    public void LoadPreset_JsonTronque_RetourneNull_EtMetEnQuarantaine()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "presets"));
        File.WriteAllText(PresetPath("Casse"), "{ \"groups\": [ { \"groupName\": \"Mu");

        var loaded = _service.LoadPreset("Casse");

        Assert.Null(loaded); // echec signale a l'appelant -> AutoSave bloque cote VM
        Assert.False(File.Exists(PresetPath("Casse"))); // l'original n'est plus ecrasable
        var corrupt = Directory.GetFiles(Path.Combine(_dir, "presets"), "Casse.json.corrupt-*");
        Assert.Single(corrupt);
    }

    [Fact]
    public void LoadScene_JsonTronque_RetourneNull_EtMetEnQuarantaine()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "scenes"));
        File.WriteAllText(ScenePath("Cassee"), "pas du json");

        var loaded = _service.LoadScene("Cassee");

        Assert.Null(loaded);
        Assert.False(File.Exists(ScenePath("Cassee")));
        var corrupt = Directory.GetFiles(Path.Combine(_dir, "scenes"), "Cassee.json.corrupt-*");
        Assert.Single(corrupt);
    }

    [Fact]
    public void LoadSettings_JsonCorrompu_RetourneDefaut_EtMetEnQuarantaine()
    {
        var settingsPath = Path.Combine(_dir, "settings.json");
        File.WriteAllText(settingsPath, "{{{");

        var settings = _service.LoadSettings();

        Assert.NotNull(settings);
        Assert.False(File.Exists(settingsPath));
        var corrupt = Directory.GetFiles(_dir, "settings.json.corrupt-*");
        Assert.Single(corrupt);
    }

    [Fact]
    public void LoadScenes_UneSceneCorrompue_SignaleEchec_EtChargeLesValides()
    {
        _service.SaveScene("Valide", new SceneDto { Name = "Valide" });
        File.WriteAllText(ScenePath("Corrompue"), "{ tronque");

        var collection = _service.LoadScenes(out var loadFailed);

        Assert.True(loadFailed); // l'appelant doit bloquer l'AutoSave des scenes
        Assert.Single(collection.Scenes);
        Assert.Equal("Valide", collection.Scenes[0].Name);
    }

    // ---- Persistance de la fenetre (UI-M9) ----

    [Fact]
    public void SaveSettings_PlacementFenetre_RoundTrip()
    {
        var settings = new AppSettingsDto
        {
            WindowWidth = 1280.5,
            WindowHeight = 720,
            WindowLeft = -8,
            WindowTop = 42
        };

        _service.SaveSettings(settings);
        var loaded = _service.LoadSettings();

        Assert.Equal(1280.5, loaded.WindowWidth);
        Assert.Equal(720, loaded.WindowHeight);
        Assert.Equal(-8, loaded.WindowLeft);
        Assert.Equal(42, loaded.WindowTop);
    }

    [Fact]
    public void LoadSettings_SansPlacementFenetre_RetourneNull()
    {
        // Fichier v1 sans les champs fenetre : deserialisation tolerante, valeurs null
        _service.SaveSettings(new AppSettingsDto { ActivePresetName = "P" });
        var loaded = _service.LoadSettings();

        Assert.Null(loaded.WindowWidth);
        Assert.Null(loaded.WindowHeight);
        Assert.Null(loaded.WindowLeft);
        Assert.Null(loaded.WindowTop);
    }

    // ---- Ecriture atomique (DON-01) ----

    [Fact]
    public void SavePreset_DeuxSauvegardes_ConserveUnBak_SansResiduTmp()
    {
        var collection = PresetService.GetDefaultCollection();
        _service.SavePreset("Atomique", collection);
        collection.Groups.Add(new PresetGroupDto { GroupName = "Ajout" });
        _service.SavePreset("Atomique", collection);

        var path = PresetPath("Atomique");
        Assert.True(File.Exists(path));
        Assert.True(File.Exists(path + ".bak")); // version precedente conservee par File.Replace
        Assert.False(File.Exists(path + ".tmp")); // pas de residu temporaire

        var loaded = _service.LoadPreset("Atomique");
        Assert.NotNull(loaded);
        Assert.Equal(4, loaded!.Groups.Count); // la derniere version est bien celle relue
    }
}
