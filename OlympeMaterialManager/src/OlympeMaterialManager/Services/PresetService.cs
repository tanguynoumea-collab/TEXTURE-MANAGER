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
///
/// Le repertoire de projet (configurable par l'utilisateur) sert de base pour
/// presets/, scenes/ et settings.json. Seul le fichier config.json reste dans %APPDATA%.
/// </summary>
public class PresetService
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Chemin du fichier de configuration global dans %APPDATA%.
    /// Ne contient que le chemin du repertoire de projet.
    /// </summary>
    private static readonly string _configPath;

    /// <summary>
    /// Dossier %APPDATA%/Olympe/MaterialManager/ (config globale uniquement).
    /// </summary>
    private static readonly string _appDataDir;

    static PresetService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _appDataDir = Path.Combine(appData, "Olympe", "MaterialManager");
        Directory.CreateDirectory(_appDataDir);
        _configPath = Path.Combine(_appDataDir, "config.json");
    }

    private readonly string _settingsPath;
    private readonly string _projectDir;

    public PresetService()
    {
        _projectDir = GetProjectDirectory() ?? _appDataDir;
        Directory.CreateDirectory(_projectDir);
        _settingsPath = Path.Combine(_projectDir, "settings.json");
    }

    // ---- Project Directory Management ----

    /// <summary>
    /// Retourne true si un repertoire de projet est defini dans config.json.
    /// </summary>
    public static bool IsProjectDirectorySet()
    {
        if (!File.Exists(_configPath)) return false;
        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<ProjectConfigDto>(json, _options);
            return !string.IsNullOrWhiteSpace(config?.ProjectDirectory);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Retourne le chemin du repertoire de projet stocke dans config.json,
    /// ou null si non defini.
    /// </summary>
    public static string? GetProjectDirectory()
    {
        if (!File.Exists(_configPath)) return null;
        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<ProjectConfigDto>(json, _options);
            return string.IsNullOrWhiteSpace(config?.ProjectDirectory) ? null : config!.ProjectDirectory;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Definit le repertoire de projet dans config.json (%APPDATA%).
    /// Cree les sous-dossiers presets/ et scenes/ si necessaire.
    /// </summary>
    public static void SetProjectDirectory(string path)
    {
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, "presets"));
        Directory.CreateDirectory(Path.Combine(path, "scenes"));

        var config = new ProjectConfigDto { ProjectDirectory = path };
        var json = JsonSerializer.Serialize(config, _options);
        File.WriteAllText(_configPath, json);
    }

    /// <summary>
    /// Migre tous les fichiers (presets/, scenes/, settings.json) de l'ancien repertoire
    /// vers le nouveau repertoire de projet, puis met a jour config.json.
    /// </summary>
    public static void MigrateProjectDirectory(string newPath)
    {
        var oldPath = GetProjectDirectory() ?? _appDataDir;
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            return;

        Directory.CreateDirectory(newPath);

        // Copier settings.json
        var oldSettings = Path.Combine(oldPath, "settings.json");
        if (File.Exists(oldSettings))
            File.Copy(oldSettings, Path.Combine(newPath, "settings.json"), overwrite: true);

        // Copier le dossier presets/
        CopyDirectoryContents(Path.Combine(oldPath, "presets"), Path.Combine(newPath, "presets"));

        // Copier le dossier scenes/
        CopyDirectoryContents(Path.Combine(oldPath, "scenes"), Path.Combine(newPath, "scenes"));

        // Mettre a jour config.json
        SetProjectDirectory(newPath);
    }

    /// <summary>
    /// Copie le contenu d'un dossier source vers un dossier destination.
    /// </summary>
    private static void CopyDirectoryContents(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir)) return;
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryContents(dir, destSubDir);
        }
    }

    // ---- Settings ----

    /// <summary>
    /// Charge les settings depuis settings.json (dans le repertoire de projet).
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
    /// Sauvegarde les settings dans settings.json (dans le repertoire de projet).
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
    /// Retourne le dossier des presets (repertoire-de-projet/presets/).
    /// </summary>
    public string GetPresetsDirectory()
    {
        var dir = Path.Combine(_projectDir, "presets");
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
    /// Retourne le dossier des scenes (repertoire-de-projet/scenes/).
    /// </summary>
    public string GetScenesDirectory()
    {
        var dir = Path.Combine(_projectDir, "scenes");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Liste les noms des scenes disponibles (noms des fichiers JSON sans extension).
    /// </summary>
    public List<string> ListScenes()
    {
        var dir = GetScenesDirectory();
        var result = new List<string>();
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.GetFiles(dir, "*.json"))
                result.Add(Path.GetFileNameWithoutExtension(file));
        }
        return result;
    }

    /// <summary>
    /// Charge une scene par nom depuis le dossier des scenes.
    /// </summary>
    public SceneDto LoadScene(string name)
    {
        var path = Path.Combine(GetScenesDirectory(), name + ".json");
        try
        {
            if (!File.Exists(path)) return new SceneDto { Name = name };
            var json = File.ReadAllText(path);
            var scene = JsonSerializer.Deserialize<SceneDto>(json, _options);
            return scene ?? new SceneDto { Name = name };
        }
        catch
        {
            return new SceneDto { Name = name };
        }
    }

    /// <summary>
    /// Sauvegarde une scene par nom dans le dossier des scenes.
    /// </summary>
    public void SaveScene(string name, SceneDto scene)
    {
        var path = Path.Combine(GetScenesDirectory(), name + ".json");
        scene.Name = name;
        var json = JsonSerializer.Serialize(scene, _options);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Supprime le fichier d'une scene.
    /// </summary>
    public void DeleteScene(string name)
    {
        var path = Path.Combine(GetScenesDirectory(), name + ".json");
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    // ---- Suppression de preset ----

    /// <summary>
    /// Supprime le fichier d'un preset et le retire des settings.
    /// </summary>
    public void DeletePreset(string name)
    {
        var path = Path.Combine(GetPresetsDirectory(), name + ".json");
        try { if (File.Exists(path)) File.Delete(path); } catch { }

        var settings = LoadSettings();
        settings.PresetFiles.Remove(name);
        if (settings.ActivePresetName == name)
            settings.ActivePresetName = settings.PresetFiles.Count > 0 ? settings.PresetFiles[0] : null;
        SaveSettings(settings);
    }

    // ---- Legacy compat ----

    /// <summary>
    /// Charge les scenes depuis l'ancien fichier unique (migration).
    /// </summary>
    public SceneCollectionDto LoadScenes()
    {
        // Essayer d'abord le nouveau systeme multi-fichier
        var scenes = ListScenes();
        if (scenes.Count > 0)
        {
            var collection = new SceneCollectionDto();
            foreach (var name in scenes)
                collection.Scenes.Add(LoadScene(name));
            return collection;
        }

        // Fallback : ancien fichier unique
        var legacyPath = Path.Combine(_projectDir, "scenes.json");
        try
        {
            if (!File.Exists(legacyPath)) return new SceneCollectionDto();
            var json = File.ReadAllText(legacyPath);
            var result = JsonSerializer.Deserialize<SceneCollectionDto>(json, _options)
                         ?? new SceneCollectionDto();

            // Migrer vers le nouveau systeme
            foreach (var scene in result.Scenes)
            {
                if (!string.IsNullOrEmpty(scene.Name))
                    SaveScene(scene.Name, scene);
            }

            return result;
        }
        catch
        {
            return new SceneCollectionDto();
        }
    }

    /// <summary>
    /// Sauvegarde toutes les scenes (compat legacy -- sauvegarde chaque scene individuellement).
    /// </summary>
    public void SaveScenes(SceneCollectionDto collection)
    {
        foreach (var scene in collection.Scenes)
        {
            if (!string.IsNullOrEmpty(scene.Name))
                SaveScene(scene.Name, scene);
        }
    }

    // ---- DTO interne pour config.json ----

    private class ProjectConfigDto
    {
        public string? ProjectDirectory { get; set; }
    }
}
