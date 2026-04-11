using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Services;

/// <summary>
/// Service de persistance JSON pour les presets materiaux (D-04, D-05, D-06).
/// Gere le chargement/sauvegarde des fichiers de presets et la memorisation des parametres.
/// Supporte le systeme multi-preset (chaque preset est un fichier JSON separe).
/// </summary>
public class PresetService
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsPath;
    private readonly string _appDataDir;

    public PresetService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _appDataDir = Path.Combine(appData, "Olympe", "MaterialManager");
        Directory.CreateDirectory(_appDataDir);
        _settingsPath = Path.Combine(_appDataDir, "settings.json");
    }

    // ---- Settings ----

    /// <summary>
    /// Charge les settings depuis settings.json.
    /// </summary>
    public AppSettingsDto LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return new AppSettingsDto();
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettingsDto>(json, _options) ?? new AppSettingsDto();
        }
        catch
        {
            return new AppSettingsDto();
        }
    }

    /// <summary>
    /// Sauvegarde les settings dans settings.json.
    /// </summary>
    public void SaveSettings(AppSettingsDto settings)
    {
        var json = JsonSerializer.Serialize(settings, _options);
        File.WriteAllText(_settingsPath, json);
    }

    /// <summary>
    /// Retourne le chemin du fichier de presets memorise, ou null si non defini.
    /// </summary>
    public string? GetStoredPresetPath()
    {
        var settings = LoadSettings();
        return settings.PresetFilePath;
    }

    /// <summary>
    /// Memorise le chemin du fichier de presets dans settings.json.
    /// </summary>
    public void StorePresetPath(string path)
    {
        var settings = LoadSettings();
        settings.PresetFilePath = path;
        SaveSettings(settings);
    }

    // ---- Multi-Preset System ----

    /// <summary>
    /// Retourne le dossier des presets (%APPDATA%/Olympe/MaterialManager/presets/).
    /// </summary>
    public string GetPresetsDirectory()
    {
        var dir = Path.Combine(_appDataDir, "presets");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Liste les noms des presets disponibles (noms des fichiers JSON sans extension).
    /// </summary>
    public List<string> ListPresets()
    {
        var dir = GetPresetsDirectory();
        var result = new List<string>();

        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                result.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        return result;
    }

    /// <summary>
    /// Charge un preset par nom depuis le dossier des presets.
    /// </summary>
    public PresetCollectionDto LoadPreset(string name)
    {
        var path = Path.Combine(GetPresetsDirectory(), name + ".json");
        try
        {
            if (!File.Exists(path)) return GetDefaultCollection();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PresetCollectionDto>(json, _options)
                   ?? GetDefaultCollection();
        }
        catch
        {
            return GetDefaultCollection();
        }
    }

    /// <summary>
    /// Sauvegarde un preset par nom dans le dossier des presets.
    /// </summary>
    public void SavePreset(string name, PresetCollectionDto collection)
    {
        var path = Path.Combine(GetPresetsDirectory(), name + ".json");
        var json = JsonSerializer.Serialize(collection, _options);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Cree un nouveau preset vide avec 3 groupes par defaut (Murs, Sols, Autres).
    /// </summary>
    public PresetCollectionDto CreatePreset(string name)
    {
        var collection = GetDefaultCollection();
        SavePreset(name, collection);

        // Mettre a jour les settings
        var settings = LoadSettings();
        if (!settings.PresetFiles.Contains(name))
            settings.PresetFiles.Add(name);
        settings.ActivePresetName = name;
        SaveSettings(settings);

        return collection;
    }

    // ---- Legacy single-file support ----

    /// <summary>
    /// Charge une collection de presets depuis un fichier JSON.
    /// Retourne la collection par defaut en cas d'erreur.
    /// </summary>
    public PresetCollectionDto Load(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PresetCollectionDto>(json, _options)
                   ?? GetDefaultCollection();
        }
        catch (IOException)
        {
            return GetDefaultCollection();
        }
        catch (JsonException)
        {
            return GetDefaultCollection();
        }
    }

    /// <summary>
    /// Sauvegarde une collection de presets en JSON.
    /// </summary>
    public void Save(PresetCollectionDto collection, string path)
    {
        var json = JsonSerializer.Serialize(collection, _options);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Retourne une collection par defaut avec 3 groupes : Murs, Sols, Autres (D-03).
    /// </summary>
    public static PresetCollectionDto GetDefaultCollection()
    {
        return new PresetCollectionDto
        {
            Groups = new ObservableCollection<PresetGroupDto>
            {
                new() { GroupName = "Murs" },
                new() { GroupName = "Sols" },
                new() { GroupName = "Autres" }
            }
        };
    }

    // ---- Scenes multi-file system ----

    /// <summary>
    /// Retourne le dossier des scenes (%APPDATA%/Olympe/MaterialManager/scenes/).
    /// </summary>
    public string GetScenesDirectory()
    {
        var dir = Path.Combine(_appDataDir, "scenes");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Retourne le chemin du fichier de scenes memorise, ou le chemin par defaut dans AppData.
    /// </summary>
    public string GetScenesPath()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettingsDto>(json, _options);
                if (!string.IsNullOrEmpty(settings?.ScenesFilePath))
                    return settings!.ScenesFilePath!;
            }
        }
        catch { /* fallback */ }

        // Chemin par defaut : meme dossier que settings
        return Path.Combine(Path.GetDirectoryName(_settingsPath)!, "scenes.json");
    }

    /// <summary>
    /// Charge les scenes depuis le fichier JSON.
    /// </summary>
    public SceneCollectionDto LoadScenes()
    {
        var path = GetScenesPath();
        try
        {
            if (!File.Exists(path)) return new SceneCollectionDto();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SceneCollectionDto>(json, _options)
                   ?? new SceneCollectionDto();
        }
        catch
        {
            return new SceneCollectionDto();
        }
    }

    /// <summary>
    /// Sauvegarde les scenes dans le fichier JSON.
    /// </summary>
    public void SaveScenes(SceneCollectionDto collection)
    {
        var path = GetScenesPath();
        var json = JsonSerializer.Serialize(collection, _options);
        File.WriteAllText(path, json);
    }
}
