namespace Olympe.MaterialManager.Events;

/// <summary>
/// Types de requetes pour le dispatch ExternalEvent (D-09).
/// Chaque phase ajoute ses types ici.
/// </summary>
public enum RevitRequestType
{
    None,
    GetDocumentInfo,              // Phase 1 : round-trip proof
    GetFamilyList,                // Phase 2 : data=null, returns List<FamilyCategoryDto>
    GetTypeList,                  // Phase 2 : data=GetTypeListRequestDto, returns List<SceneTypeDto>
    GetLayersForType,             // Phase 2 : data=long (typeElementId), returns List<LayerDto>
    GetMaterialParametersForType, // Phase 2 : data=long (typeElementId), returns List<MaterialParamDto>
}
