namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour une famille Revit groupee par categorie.
/// Utilise pour peupler les ComboBox d'ajout de types a une scene (D-08).
/// POCO pur -- aucune dependance Revit API.
/// </summary>
public class FamilyCategoryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public long FamilyElementIdValue { get; set; } = -1;
    public long BuiltInCategoryValue { get; set; }
    public bool IsSystemFamily { get; set; }
}
