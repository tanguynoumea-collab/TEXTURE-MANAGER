namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour la requete de renommage d'un materiau (D-07).
/// Envoye au RevitEventBridge via EditMaterialName.
/// </summary>
public class EditMaterialNameRequestDto
{
    public long MaterialIdValue { get; set; }
    public string NewName { get; set; } = string.Empty;
}
