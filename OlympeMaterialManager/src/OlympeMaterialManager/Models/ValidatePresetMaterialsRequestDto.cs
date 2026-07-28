namespace Olympe.MaterialManager.Models;

/// <summary>
/// Requete de validation B1 : liste (id, nom) de TOUS les materiaux du preset
/// actif, a verifier contre le document Revit actif (lecture seule).
/// </summary>
public class ValidatePresetMaterialsRequestDto
{
    public List<MaterialRefDto> Materials { get; set; } = new();
}
