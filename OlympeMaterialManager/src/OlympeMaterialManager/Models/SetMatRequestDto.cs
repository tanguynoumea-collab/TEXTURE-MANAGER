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

    /// <summary>
    /// Nom du materiau attendu (DON-04) : cle logique de validation.
    /// L'id n'est qu'un cache — si l'id ne resout pas un Material de ce nom
    /// dans le document courant, le handler re-resout par nom ou echoue proprement.
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;
}
