namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour la requete Set Mat sur les couches CompoundStructure (D-22).
/// Envoye au RevitEventBridge via SetMaterialOnLayers.
/// </summary>
public class SetMatRequestDto
{
    public long TargetTypeIdValue { get; set; }
    public int[] LayerIndices { get; set; } = [];
    public long MaterialIdValue { get; set; }
}
