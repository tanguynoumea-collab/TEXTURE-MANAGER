namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour les informations du document Revit actif.
/// Aucune dependance Revit API -- POCO pur (INFRA-07).
/// </summary>
public class RevitDocInfoDto
{
    public string Title { get; set; } = string.Empty;
    public string PathName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
}
