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

    /// <summary>
    /// Nom du materiau attendu (DON-04) : cle logique de validation.
    /// L'id n'est qu'un cache — si l'id ne resout pas un Material de ce nom
    /// dans le document courant, le handler re-resout par nom ou echoue proprement.
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;
}
