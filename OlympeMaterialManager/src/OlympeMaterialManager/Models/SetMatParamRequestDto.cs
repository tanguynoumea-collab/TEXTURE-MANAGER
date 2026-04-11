namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour la requete Set Mat sur les parametres materiaux (D-17).
/// Batch de noms de parametres dans une seule Transaction Revit.
/// </summary>
public class SetMatParamRequestDto
{
    public long TargetTypeIdValue { get; set; }
    public long MaterialIdValue { get; set; }
    public string[] ParameterDefinitionNames { get; set; } = [];
}
