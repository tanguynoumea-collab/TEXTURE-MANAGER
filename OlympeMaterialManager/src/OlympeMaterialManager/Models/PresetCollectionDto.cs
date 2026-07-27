using System.Collections.ObjectModel;

namespace Olympe.MaterialManager.Models;

/// <summary>
/// Unite racine de serialisation pour les presets (D-02).
/// Encapsule les groupes de presets pour le fichier JSON.
/// </summary>
public class PresetCollectionDto
{
    /// <summary>
    /// Version du schema de persistance (DON-03). Les fichiers v0 (sans le champ)
    /// deserialisent avec le defaut 1 — acceptable, pas de logique de migration pour l'instant.
    /// A incrementer a la prochaine rupture de format.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    public ObservableCollection<PresetGroupDto> Groups { get; set; } = new();
}
