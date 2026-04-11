# Phase 2: Read Path -- Scene and Layer Display - Context

**Gathered:** 2026-04-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can create named scenes, populate them with types from the project, and inspect CompoundStructure layers or material parameters for any selected type. This is the read-only data path — no modifications to Revit model.

Requirements: SCENE-01, SCENE-02, SCENE-03, SCENE-05, SCENE-06, SCENE-07, SCENE-08, LAYER-01, LAYER-02, LAYER-03, LAYER-04, LAYER-05

</domain>

<decisions>
## Implementation Decisions

### Scene Data Model
- **D-01:** A Scene is a `SceneDto` with `Name` (string) and `Types` (ObservableCollection<SceneTypeDto>). SceneTypeDto contains: `ElementIdValue` (long), `FamilyName` (string), `TypeName` (string), `CategoryName` (string), `HasCompoundStructure` (bool). No Revit types in the DTO.
- **D-02:** Scenes are stored in LeftPanelViewModel as ObservableCollection<SceneDto>. ActiveScene is the currently selected scene.
- **D-03:** Scene creation uses a simple TextBox + Button in the left panel header. No dialog.

### TreeView Structure
- **D-04:** TreeView uses CollectionViewSource with GroupDescription by CategoryName. Groups display as expandable headers.
- **D-05:** Custom IComparer sort order: Murs (Walls) first, Sols (Floors) second, then alphabetical. Within each category, types are sorted alphabetically by TypeName.
- **D-06:** TreeView items display "{FamilyName} : {TypeName}" format. Category group headers display the French category name.

### Revit Data Fetching for Scene Population
- **D-07:** Add new RevitRequestType enum values: GetFamilyList, GetTypeList, GetLayersForType, GetMaterialParametersForType.
- **D-08:** GetFamilyList returns List<FamilyCategoryDto> grouped by category. GetTypeList(familyId) returns List<SceneTypeDto>.
- **D-09:** The dropdown approach for adding types: first ComboBox selects a category+family, second ComboBox shows types for that family. "Ajouter" button adds selected type to scene.
- **D-10:** Remove type from scene: right-click context menu or Delete key on selected TreeView item.

### Layer Display (Center Panel)
- **D-11:** For types with CompoundStructure: CenterPanelViewModel exposes ObservableCollection<LayerDto>. LayerDto contains: LayerIndex (int), Function (string, French), Width (double, in mm), MaterialName (string), MaterialElementIdValue (long).
- **D-12:** Display as ListBox with DataTemplate: "[Fonction] — [Epaisseur] mm — [Materiau]". Each item is selectable.
- **D-13:** Layer function names mapped to French: Finish 1 → "Finition 1", Finish 2 → "Finition 2", Substrate → "Substrat", Core → "Noyau", Membrane → "Membrane", Structure → "Structure".

### Material Parameter Display (for loaded families without layers)
- **D-14:** For types without CompoundStructure: CenterPanelViewModel exposes ObservableCollection<MaterialParamDto>. MaterialParamDto contains: ParameterName (string), CurrentMaterialName (string), CurrentMaterialIdValue (long), ParameterDefinitionName (string).
- **D-15:** Display as ListBox with DataTemplate: "[Nom parametre] — [Materiau actuel]". Each item is selectable.
- **D-16:** Parameter discovery uses Element.Parameters iteration, filtering for StorageType.ElementId where the value is a Material.

### Selection
- **D-17:** Center panel ListBox uses SelectionMode=Extended for multi-selection (Ctrl+click, Shift+click).
- **D-18:** Selected items exposed as ObservableCollection or IList in CenterPanelViewModel for downstream Set Mat usage.

### Coordination
- **D-19:** When a type is selected in the TreeView (left panel), LeftPanelViewModel raises an event/message to trigger CenterPanelViewModel to fetch layers/parameters via ExternalEvent.
- **D-20:** Use CommunityToolkit.Mvvm Messenger (WeakReferenceMessenger) for inter-ViewModel communication.

### Claude's Discretion
- Exact DTO property types (nullable vs non-nullable)
- Whether to cache Revit data or fetch each time a type is selected
- Error display approach when Revit data fetch fails
- Loading indicator pattern while waiting for ExternalEvent response

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 1 Outputs (existing code)
- `OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitEventBridge.cs` — ExternalEvent handler to extend with new request types
- `OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitRequestType.cs` — Enum to extend
- `OlympeMaterialManager/src/OlympeMaterialManager/Models/RevitDocInfoDto.cs` — DTO pattern to follow
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/LeftPanelViewModel.cs` — Shell to populate
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/CenterPanelViewModel.cs` — Shell to populate
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/LeftPanelView.xaml` — Shell to populate
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/CenterPanelView.xaml` — Shell to populate

### Research Documents
- `.planning/research/ARCHITECTURE.md` — CompoundStructure read patterns, data flow
- `.planning/research/PITFALLS.md` — CompoundStructure copy semantics, threading
- `.planning/research/FEATURES.md` — Feature landscape, material parameter discovery

### Project Documents
- `.planning/PROJECT.md` — Constraints and scope
- `.planning/REQUIREMENTS.md` — Requirement details

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `RevitEventBridge.cs` — Extend with new request types for scene/layer data
- `RevitRequestType.cs` — Add GetFamilyList, GetTypeList, GetLayersForType, GetMaterialParametersForType
- `RevitDocInfoDto.cs` — Pattern for new DTOs (SceneTypeDto, LayerDto, MaterialParamDto, FamilyCategoryDto)
- `OlympeTheme.xaml` — All new controls inherit dark theme styling automatically

### Established Patterns
- MVVM: ObservableObject, [ObservableProperty], [RelayCommand]
- Revit communication: MakeRequest(type) -> ExternalEvent.Raise() -> ProcessRequest -> Dispatcher callback
- DTOs: Pure POCOs, no Revit types, long for ElementId values

### Integration Points
- LeftPanelView.xaml — Currently placeholder, needs TreeView + scene management UI
- CenterPanelView.xaml — Currently placeholder, needs layer/parameter list
- RevitEventBridge.ProcessRequest switch — Extend with new cases
- MainWindowViewModel — May need to coordinate panel selection state

</code_context>

<specifics>
## Specific Ideas

- The TreeView must feel responsive — data should load quickly when switching types
- Scene names should be editable (inline rename or rename button)
- The center panel should clearly differentiate between "couches" mode and "parametres" mode with a header or visual indicator
- French function names for CompoundStructure layers are important for the target users

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-read-path-scene-and-layer-display*
*Context gathered: 2026-04-11*
