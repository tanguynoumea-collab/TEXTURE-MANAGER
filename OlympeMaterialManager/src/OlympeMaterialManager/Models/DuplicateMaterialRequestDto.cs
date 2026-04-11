namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour la requete de duplication d'un materiau (D-23).
/// Envoye au RevitEventBridge via DuplicateMaterial.
/// </summary>
public class DuplicateMaterialRequestDto
{
    public long MaterialIdValue { get; set; }
}
