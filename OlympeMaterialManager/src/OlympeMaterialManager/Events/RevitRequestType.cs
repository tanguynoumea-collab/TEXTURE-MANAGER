namespace Olympe.MaterialManager.Events;

/// <summary>
/// Types de requetes pour le dispatch ExternalEvent (D-09).
/// Chaque phase ajoute ses types ici.
/// </summary>
public enum RevitRequestType
{
    None,
    GetDocumentInfo,  // Phase 1 : round-trip proof
    // Future phases : ReadCompoundLayers, SetLayerMaterial, EditMaterial, etc.
}
