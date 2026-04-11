namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour la requete de modification de couleur de surface d'un materiau (D-09).
/// Envoye au RevitEventBridge via EditMaterialColor.
/// </summary>
public class EditMaterialColorRequestDto
{
    public long MaterialIdValue { get; set; }
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
}
