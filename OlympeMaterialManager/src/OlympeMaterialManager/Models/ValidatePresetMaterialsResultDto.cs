namespace Olympe.MaterialManager.Models;

/// <summary>
/// Resultat de la validation B1 des materiaux d'un preset.
/// HasActiveDocument = false : aucun document ouvert, validation differee
/// silencieusement cote ViewModel (rien n'est memorise).
/// DocumentKey identifie le document valide (chemin, sinon titre) pour que le
/// ViewModel ne re-declenche pas le dialogue en boucle sur le meme couple
/// preset/document.
/// FoundMaterials (DR3-1) : couleurs actuelles des materiaux TROUVES, pour
/// rafraichir en place les couleurs persistees des presets (pastilles en mode
/// Réaliste des presets crees avant DR2, couleurs modifiees hors add-in).
/// </summary>
public class ValidatePresetMaterialsResultDto
{
    public bool HasActiveDocument { get; set; }
    public string DocumentKey { get; set; } = string.Empty;
    public List<MaterialRefDto> MissingMaterials { get; set; } = new();
    public List<RefreshedMaterialColorsDto> FoundMaterials { get; set; } = new();
}
