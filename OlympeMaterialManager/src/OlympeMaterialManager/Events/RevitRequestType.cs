namespace Olympe.MaterialManager.Events;

/// <summary>
/// Types de requetes pour le dispatch ExternalEvent (D-09).
/// Chaque phase ajoute ses types ici.
/// </summary>
public enum RevitRequestType
{
    None,
    GetLayersForType,             // Phase 2 : data=long (typeElementId), returns List<LayerDto>
    GetMaterialParametersForType, // Phase 2 : data=long (typeElementId), returns List<MaterialParamDto>

    // Phase 3 : preset panel et Set Mat
    GetAllMaterials,              // Phase 3 : data=null, returns List<PresetMaterialDto>
    SetMaterialOnLayers,          // Phase 3 : data=SetMatRequestDto, returns null (success) or Exception
    SetMaterialOnParameter,       // Phase 3 : data=SetMatParamRequestDto, returns null (success) or Exception
    DuplicateMaterial,            // Phase 3 : data=DuplicateMaterialRequestDto, returns PresetMaterialDto

    // Phase 4 : edition materiau et pick 3D
    GetMaterialDetails,            // data=long (materialIdValue), returns MaterialDetailsDto
    EditMaterialName,              // data=EditMaterialNameRequestDto, returns null (success) or Exception
    EditMaterialDescription,       // data=EditMaterialDescriptionRequestDto, returns null (success) or Exception
    EditMaterialColor,             // data=EditMaterialColorRequestDto, returns null (success) or Exception
    EditMaterialTint,              // data=EditMaterialTintRequestDto, returns null (success) or Exception
    PickElementInView,             // data=null, returns List<SceneTypeDto> (multi-selection)
    HighlightElementsByType,       // data=long (typeElementIdValue), returns null (selection visuelle)
    GetCompositeSubTypes,          // data=long (typeElementIdValue), returns List<SceneTypeDto> (sous-types d'un composite)

    // Cycle 2 : visualisateur de materiau (B9)
    OpenMaterialsDialog,           // data=null, returns null (success) or Exception — PostCommand du gestionnaire de materiaux Revit

    // Cycle 3 : pipette materiau (B2)
    PickElementForMaterials,       // data=null, returns List<PresetMaterialDto> (un seul pick ; null = pick annule)
}
