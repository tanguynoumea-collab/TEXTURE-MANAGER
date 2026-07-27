using System.Collections.Generic;

namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour les parametres applicatifs persistes dans settings.json (D-05).
/// Stocke le chemin du fichier de presets choisi par l'utilisateur,
/// la liste des presets disponibles et le preset actif.
/// </summary>
public class AppSettingsDto
{
    public string? PresetFilePath { get; set; }

    /// <summary>
    /// Liste des noms de fichiers presets disponibles (sans extension).
    /// </summary>
    public List<string> PresetFiles { get; set; } = new();

    /// <summary>
    /// Nom du preset actuellement actif.
    /// </summary>
    public string? ActivePresetName { get; set; }
}
