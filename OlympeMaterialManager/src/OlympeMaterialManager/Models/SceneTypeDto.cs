namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour un type Revit dans une scene.
/// Contient les informations necessaires a l'affichage et au dispatch vers le panneau centre.
/// POCO pur -- aucune dependance Revit API (D-01).
/// </summary>
public class SceneTypeDto
{
    public long ElementIdValue { get; set; } = -1;
    public string FamilyName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool HasCompoundStructure { get; set; }
}
