using System.Collections.Generic;

namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour les parametres applicatifs persistes dans settings.json (D-05).
/// Stocke le chemin du fichier de presets choisi par l'utilisateur,
/// la liste des presets disponibles et le preset actif.
/// </summary>
public class AppSettingsDto
{
    /// <summary>
    /// Version du schema de persistance (DON-03). Les fichiers v0 (sans le champ)
    /// deserialisent avec le defaut 1 — acceptable, pas de logique de migration pour l'instant.
    /// A incrementer a la prochaine rupture de format.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    public string? PresetFilePath { get; set; }

    /// <summary>
    /// Liste des noms de fichiers presets disponibles (sans extension).
    /// </summary>
    public List<string> PresetFiles { get; set; } = new();

    /// <summary>
    /// Nom du preset actuellement actif.
    /// </summary>
    public string? ActivePresetName { get; set; }

    // ---- Persistance de la fenetre principale (UI-M9) ----
    // Null tant que la fenetre n'a jamais ete fermee : la taille par defaut s'applique.

    /// <summary>Largeur de la fenetre principale a la derniere fermeture.</summary>
    public double? WindowWidth { get; set; }

    /// <summary>Hauteur de la fenetre principale a la derniere fermeture.</summary>
    public double? WindowHeight { get; set; }

    /// <summary>Position gauche de la fenetre principale a la derniere fermeture.</summary>
    public double? WindowLeft { get; set; }

    /// <summary>Position haute de la fenetre principale a la derniere fermeture.</summary>
    public double? WindowTop { get; set; }

    /// <summary>
    /// Mode d'apercu des materiaux (B10) : "UniformColor" / "Texture" / "Realistic".
    /// STRING volontairement (jamais l'enum serialise) : une valeur inconnue est
    /// toleree par Enum.TryParse cote lecture au lieu de provoquer une JsonException
    /// qui enverrait settings.json en quarantaine (DON-02). Ajout additif :
    /// pas de bump de SchemaVersion.
    /// </summary>
    public string MaterialPreviewMode { get; set; } = "UniformColor";
}
