using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Services;

/// <summary>
/// Service de persistance JSON pour les presets materiaux (D-04, D-05, D-06).
/// Gere le chargement/sauvegarde du fichier de presets et la memorisation du chemin.
/// </summary>
public class PresetService
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsPath;

    public PresetService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Olympe", "MaterialManager");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    /// <summary>
    /// Retourne le chemin du fichier de presets memorise, ou null si non defini.
    /// </summary>
    public string? GetStoredPresetPath()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return null;
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettingsDto>(json, _options);
            return settings?.PresetFilePath;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Memorise le chemin du fichier de presets dans settings.json.
    /// </summary>
    public void StorePresetPath(string path)
    {
        var settings = new AppSettingsDto { PresetFilePath = path };
        var json = JsonSerializer.Serialize(settings, _options);
        File.WriteAllText(_settingsPath, json);
    }

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
}
