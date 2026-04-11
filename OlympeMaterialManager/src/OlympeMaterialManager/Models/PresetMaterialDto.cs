namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour un materiau preset.
/// POCO pur pour serialisation JSON (System.Text.Json).
/// Pas de dependance Revit API (D-01).
/// </summary>
public class PresetMaterialDto
{
    public string MaterialName { get; set; } = string.Empty;
    public long MaterialElementIdValue { get; set; } = -1;
    public int ColorArgb { get; set; }
}
