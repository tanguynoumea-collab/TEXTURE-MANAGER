using System.Collections.ObjectModel;

namespace Olympe.MaterialManager.Models;

/// <summary>
/// Unite racine de serialisation pour les presets (D-02).
/// Encapsule les groupes de presets pour le fichier JSON.
/// </summary>
public class PresetCollectionDto
{
    public ObservableCollection<PresetGroupDto> Groups { get; set; } = new();
}
