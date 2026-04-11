namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour les parametres applicatifs persistes dans settings.json (D-05).
/// Stocke le chemin du fichier de presets choisi par l'utilisateur.
/// </summary>
public class AppSettingsDto
{
    public string? PresetFilePath { get; set; }
}
