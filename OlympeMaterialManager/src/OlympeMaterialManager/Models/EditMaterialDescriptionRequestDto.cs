namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour la requete de modification de description d'un materiau (D-08).
/// Envoye au RevitEventBridge via EditMaterialDescription.
/// </summary>
public class EditMaterialDescriptionRequestDto
{
    public long MaterialIdValue { get; set; }
    public string NewDescription { get; set; } = string.Empty;
}
