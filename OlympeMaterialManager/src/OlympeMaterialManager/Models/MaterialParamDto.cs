namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour un parametre de type Material sur une famille chargee.
/// Utilise quand le type n'a pas de CompoundStructure (D-14).
/// POCO pur -- aucune dependance Revit API.
/// </summary>
public class MaterialParamDto
{
    public string ParameterName { get; set; } = string.Empty;
    public string CurrentMaterialName { get; set; } = string.Empty;
    public long CurrentMaterialIdValue { get; set; } = -1;
    public string ParameterDefinitionName { get; set; } = string.Empty;
}
