namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour la requete de modification de teinte d'un materiau (D-04, D-05).
/// Envoye au RevitEventBridge via EditMaterialTint.
/// Necessite un AppearanceAsset sur le materiau cible.
/// </summary>
public class EditMaterialTintRequestDto
{
    public long MaterialIdValue { get; set; }
    public bool TintEnabled { get; set; }
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
}
