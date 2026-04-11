namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO de requete pour GetTypeList.
/// Discrimine les familles systeme (BuiltInCategory) des familles chargees (FamilyElementIdValue).
/// Resout le probleme de dispatch unifie (RESEARCH open question 3).
/// POCO pur -- aucune dependance Revit API.
/// </summary>
public class GetTypeListRequestDto
{
    public long FamilyElementIdValue { get; set; } = -1;
    public bool IsSystemFamily { get; set; }
    public long BuiltInCategoryValue { get; set; }
}
