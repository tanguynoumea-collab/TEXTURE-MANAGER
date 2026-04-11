using System.Collections.ObjectModel;

namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour un groupe de presets.
/// ObservableCollection pour le binding UI, serialisable par System.Text.Json (D-01).
/// </summary>
public class PresetGroupDto
{
    public string GroupName { get; set; } = string.Empty;
    public ObservableCollection<PresetMaterialDto> Materials { get; set; } = new();
}
