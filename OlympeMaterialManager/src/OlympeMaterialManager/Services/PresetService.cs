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
    /// <summary>
    /// Noms des sous-dossiers de persistance du repertoire de projet (MAINT-08).
    /// </summary>
    private static class StorageFolders
    {
        public const string Presets = "presets";
        public const string Scenes = "scenes";
    }

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

    /// <summary>
    /// Ecrit un objet serialise en JSON de maniere atomique (DON-01) :
    /// ecriture dans un fichier temporaire puis remplacement via File.Replace
    /// (conserve un .bak de la version precedente). Si le fichier cible n'existe
    /// pas encore (premier write), simple File.Move du temporaire.
    /// </summary>
    private static void WriteJsonAtomic(string path, object obj)
    {
        var json = JsonSerializer.Serialize(obj, obj.GetType(), _options);
        var tmpPath = path + ".tmp";
        File.WriteAllText(tmpPath, json);

        if (File.Exists(path))
            File.Replace(tmpPath, path, path + ".bak");
        else
            File.Move(tmpPath, path);
    }

    /// <summary>
    /// Noms de fichiers reserves par Windows (SEC-01).
    /// </summary>
    private static readonly string[] _reservedFileNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Valide un nom destine a devenir un nom de fichier (SEC-01).
    /// Retourne null si le nom est valide, sinon un message d'erreur en francais.
    /// Point de verite unique : utilise par GetSafeFilePath et par les dialogs de saisie.
    /// </summary>
    public static string? ValidateFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Le nom ne peut pas etre vide.";

        // net48 : IsNullOrWhiteSpace n'a pas [NotNullWhen(false)], name est prouve non null ici.
        var trimmed = name!.Trim();
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Le nom contient des caracteres interdits (\\ / : * ? \" < > | ...).";

        if (Array.Exists(_reservedFileNames, r => string.Equals(r, trimmed, StringComparison.OrdinalIgnoreCase)))
            return $"\"{trimmed}\" est un nom reserve par Windows.";

        return null;
    }

    /// <summary>
    /// Construit le chemin &lt;directory&gt;\&lt;name&gt;.json apres validation du nom (SEC-01).
    /// Rejette les noms vides, les caracteres interdits, les noms reserves Windows,
    /// et verifie que le chemin resolu reste bien sous le dossier attendu (anti-traversal).
    /// </summary>
    private static string GetSafeFilePath(string directory, string name)
    {
        var error = ValidateFileName(name);
        if (error != null)
            throw new ArgumentException($"Nom de fichier invalide : {error}", nameof(name));

        var safeName = name.Trim();
        var fullPath = Path.GetFullPath(Path.Combine(directory, safeName + ".json"));
        var dirPrefix = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Le nom \"{name}\" produit un chemin en dehors du dossier attendu.", nameof(name));

        return fullPath;
    }

    /// <summary>
    /// Valide qu'un contenu JSON est un preset lisible (DON-09).
    /// Utilise AVANT de copier un fichier externe dans le dossier projet.
    /// </summary>
    public static bool IsValidPresetJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PresetCollectionDto>(json, _options) != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Valide qu'un contenu JSON est une scene lisible (DON-09).
    /// Utilise AVANT de copier un fichier externe dans le dossier projet.
    /// </summary>
    public static bool IsValidSceneJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SceneDto>(json, _options) != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Met en quarantaine un fichier illisible en le renommant en
    /// &lt;nom&gt;.corrupt-&lt;yyyyMMdd-HHmmss&gt; (DON-02). Le fichier original est
    /// ainsi preserve au lieu d'etre ecrase par une sauvegarde ulterieure.
    /// Retourne true si le renommage a reussi (false si le fichier est verrouille).
    /// </summary>
    private static bool TryQuarantineCorruptFile(string path)
    {
        try
        {
            var corruptPath = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Move(path, corruptPath);
            LogService.Log($"Fichier illisible mis en quarantaine : {corruptPath}");
            return true;
        }
        catch (Exception ex)
        {
            LogService.Error($"Echec de mise en quarantaine du fichier illisible : {path}", ex);
            return false;
        }
    }

    /// <summary>
    /// Constructeur. Le repertoire de donnees est injectable pour les tests ;
    /// par defaut (null), comportement inchange : repertoire de projet de
    /// config.json, ou %APPDATA% en fallback.
    /// </summary>
    public PresetService(string? projectDirectory = null)
    {
        _projectDir = projectDirectory ?? GetProjectDirectory() ?? _appDataDir;
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
        Directory.CreateDirectory(Path.Combine(path, StorageFolders.Presets));
        Directory.CreateDirectory(Path.Combine(path, StorageFolders.Scenes));

        var config = new ProjectConfigDto { ProjectDirectory = path };
        WriteJsonAtomic(_configPath, config);
    }

    /// <summary>
    /// Migre tous les fichiers (presets/, scenes/, settings.json) de l'ancien repertoire
    /// vers le nouveau repertoire de projet, puis met a jour config.json.
    /// </summary>
    public static void MigrateProjectDirectory(string newPath)
    {
        var oldPath = GetProjectDirectory() ?? _appDataDir;

        // TST-06 : normaliser puis rejeter un nouveau chemin identique ou imbrique
        // dans l'ancien — la copie recursive s'auto-repliquerait a l'infini.
        var oldFull = Path.GetFullPath(oldPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var newFull = Path.GetFullPath(newPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(oldFull, newFull, StringComparison.OrdinalIgnoreCase))
            return;

        if (newFull.StartsWith(oldFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Le nouveau repertoire de projet ne peut pas etre situe a l'interieur de l'ancien " +
                $"(\"{newFull}\" est dans \"{oldFull}\"). Choisissez un dossier en dehors.");

        Directory.CreateDirectory(newPath);

        // Copier settings.json
        var oldSettings = Path.Combine(oldPath, "settings.json");
        if (File.Exists(oldSettings))
            File.Copy(oldSettings, Path.Combine(newPath, "settings.json"), overwrite: true);

        // Copier le dossier presets/
        CopyDirectoryContents(
            Path.Combine(oldPath, StorageFolders.Presets), Path.Combine(newPath, StorageFolders.Presets));

        // Copier le dossier scenes/
        CopyDirectoryContents(
            Path.Combine(oldPath, StorageFolders.Scenes), Path.Combine(newPath, StorageFolders.Scenes));

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
    /// DON-06 (best-effort) : re-essaie une operation I/O sur IOException transitoire
    /// (fichier brievement verrouille par une autre instance Revit ou un client de
    /// synchronisation type OneDrive). 3 tentatives, backoff court 100/200 ms.
    /// Pas de mutex nomme : juge disproportionne pour un fichier de configuration.
    /// </summary>
    private static T RetryOnIOException<T>(Func<T> action)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                System.Threading.Thread.Sleep(100 * attempt);
            }
        }
    }

    /// <summary>
    /// Charge les settings depuis settings.json (dans le repertoire de projet).
    /// </summary>
    public AppSettingsDto LoadSettings()
    {
        // Fichier absent : cas normal (premier lancement), defaut silencieux.
        if (!File.Exists(_settingsPath)) return new AppSettingsDto();
        try
        {
            // DON-06 : un verrou transitoire ne doit pas envoyer le fichier en quarantaine.
            var json = RetryOnIOException(() => File.ReadAllText(_settingsPath));
            return JsonSerializer.Deserialize<AppSettingsDto>(json, _options) ?? new AppSettingsDto();
        }
        catch (Exception ex)
        {
            // Fichier present mais illisible : quarantaine + defaut (DON-02).
            LogService.Error($"settings.json illisible : {_settingsPath}", ex);
            TryQuarantineCorruptFile(_settingsPath);
            return new AppSettingsDto();
        }
    }

    /// <summary>
    /// Sauvegarde les settings dans settings.json (dans le repertoire de projet).
    /// </summary>
    public void SaveSettings(AppSettingsDto settings)
    {
        // DON-06 : retry court sur verrou transitoire (multi-instances / sync).
        RetryOnIOException<object?>(() =>
        {
            WriteJsonAtomic(_settingsPath, settings);
            return null;
        });
    }

    // ---- Multi-Preset System ----

    /// <summary>
    /// Retourne le dossier des presets (repertoire-de-projet/presets/).
    /// </summary>
    public string GetPresetsDirectory()
    {
        var dir = Path.Combine(_projectDir, StorageFolders.Presets);
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
    /// Fichier absent : retourne la collection par defaut (cas normal).
    /// Fichier present mais illisible (DON-02) : quarantaine en .corrupt-&lt;ts&gt;
    /// et retourne null pour que l'appelant signale l'erreur et bloque l'AutoSave.
    /// </summary>
    public PresetCollectionDto? LoadPreset(string name)
    {
        var path = GetSafeFilePath(GetPresetsDirectory(), name);
        if (!File.Exists(path)) return GetDefaultCollection();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PresetCollectionDto>(json, _options)
                   ?? GetDefaultCollection();
        }
        catch (Exception ex)
        {
            LogService.Error($"Preset illisible : {path}", ex);
            TryQuarantineCorruptFile(path);
            return null;
        }
    }

    /// <summary>
    /// Sauvegarde un preset par nom dans le dossier des presets.
    /// </summary>
    public void SavePreset(string name, PresetCollectionDto collection)
    {
        var path = GetSafeFilePath(GetPresetsDirectory(), name);
        WriteJsonAtomic(path, collection);
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
        var dir = Path.Combine(_projectDir, StorageFolders.Scenes);
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
    /// Fichier absent : retourne une scene vide (cas normal).
    /// Fichier present mais illisible (DON-02) : quarantaine en .corrupt-&lt;ts&gt;
    /// et retourne null pour que l'appelant signale l'erreur et bloque l'AutoSave.
    /// </summary>
    public SceneDto? LoadScene(string name)
    {
        var path = GetSafeFilePath(GetScenesDirectory(), name);
        if (!File.Exists(path)) return new SceneDto { Name = name };
        try
        {
            var json = File.ReadAllText(path);
            var scene = JsonSerializer.Deserialize<SceneDto>(json, _options);
            return scene ?? new SceneDto { Name = name };
        }
        catch (Exception ex)
        {
            LogService.Error($"Scene illisible : {path}", ex);
            TryQuarantineCorruptFile(path);
            return null;
        }
    }

    /// <summary>
    /// Sauvegarde une scene par nom dans le dossier des scenes.
    /// </summary>
    public void SaveScene(string name, SceneDto scene)
    {
        var path = GetSafeFilePath(GetScenesDirectory(), name);
        scene.Name = name;
        WriteJsonAtomic(path, scene);
    }

    /// <summary>
    /// Supprime le fichier d'une scene.
    /// </summary>
    public void DeleteScene(string name)
    {
        var path = GetSafeFilePath(GetScenesDirectory(), name);
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    // ---- Suppression de preset ----

    /// <summary>
    /// Supprime le fichier d'un preset et le retire des settings.
    /// </summary>
    public void DeletePreset(string name)
    {
        var path = GetSafeFilePath(GetPresetsDirectory(), name);
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
    /// loadFailed passe a true si au moins un fichier present etait illisible (DON-02) :
    /// l'appelant doit alors signaler l'erreur et bloquer l'AutoSave des scenes.
    /// </summary>
    public SceneCollectionDto LoadScenes(out bool loadFailed)
    {
        loadFailed = false;

        // Essayer d'abord le nouveau systeme multi-fichier
        var scenes = ListScenes();
        if (scenes.Count > 0)
        {
            var collection = new SceneCollectionDto();
            foreach (var name in scenes)
            {
                var scene = LoadScene(name);
                if (scene == null)
                {
                    loadFailed = true;
                    continue;
                }
                collection.Scenes.Add(scene);
            }
            return collection;
        }

        // Fallback : ancien fichier unique
        var legacyPath = Path.Combine(_projectDir, "scenes.json");
        if (!File.Exists(legacyPath)) return new SceneCollectionDto();
        try
        {
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
        catch (Exception ex)
        {
            LogService.Error($"Fichier de scenes legacy illisible : {legacyPath}", ex);
            TryQuarantineCorruptFile(legacyPath);
            loadFailed = true;
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
