namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour une couche de CompoundStructure.
/// Fonction en francais, epaisseur en mm, materiau resolu (D-11).
/// POCO pur -- aucune dependance Revit API.
/// </summary>
public class LayerDto
{
    public int LayerIndex { get; set; }
    public string Function { get; set; } = string.Empty;
    public double Width { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public long MaterialElementIdValue { get; set; } = -1;
}
