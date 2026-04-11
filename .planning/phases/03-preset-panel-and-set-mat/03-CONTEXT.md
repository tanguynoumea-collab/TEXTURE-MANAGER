# Phase 3: Preset Panel and Set Mat - Context

**Gathered:** 2026-04-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can manage a persistent preset palette (groups of materials) and apply a selected preset material to selected layers or parameters in one click via Set Mat. This is the core value proposition of the add-in.

Requirements: PRESET-01, PRESET-02, PRESET-03, PRESET-04, PRESET-05, PRESET-06, PRESET-07, PRESET-08, PRESET-09, PRESET-10, UI-05

</domain>

<decisions>
## Implementation Decisions

### Preset Data Model
- **D-01:** PresetGroupDto contains: GroupName (string), Materials (ObservableCollection<PresetMaterialDto>). PresetMaterialDto contains: MaterialName (string), MaterialElementIdValue (long), ColorArgb (int, for visual indicator).
- **D-02:** PresetCollectionDto wraps ObservableCollection<PresetGroupDto> and is the root serialization unit.
- **D-03:** Three default groups created on first use: "Murs", "Sols", "Autres".

### JSON Persistence
- **D-04:** Presets serialized to JSON via System.Text.Json. File path chosen by user via FolderBrowserDialog on first save.
- **D-05:** The chosen path is stored in a settings file at %APPDATA%/Olympe/MaterialManager/settings.json. On next launch, the path is read from settings and presets auto-loaded.
- **D-06:** PresetService class handles Load/Save/GetDefaultPath. Injected into RightPanelViewModel.
- **D-07:** Auto-save on every preset modification (add material, create group, duplicate).

### Right Panel UI
- **D-08:** TreeView with PresetGroups as root items, PresetMaterials as children. Each material shows: color swatch (Rectangle with Fill from ColorArgb) + MaterialName.
- **D-09:** Group headers show group name + count badge. Expandable/collapsible.
- **D-10:** "Ajouter au preset" button opens a dialog/flyout showing all project materials (fetched via RevitEventBridge.GetAllMaterials). User picks material and target group.
- **D-11:** "Creer un groupe" button with inline TextBox for group name.
- **D-12:** Right-click context menu on material: "Dupliquer", "Supprimer du preset".
- **D-13:** Duplication creates "[Original] copie" with a new Material in Revit (via Transaction) and adds to same group.

### Set Mat Action
- **D-14:** Set Mat button is a large prominent Button with accent style (#FF9800), positioned between center and right panels in a dedicated row or overlay area. Text: "Appliquer le materiau".
- **D-15:** Set Mat reads: selected layers/parameters from CenterPanelViewModel + selected preset material from RightPanelViewModel.
- **D-16:** For CompoundStructure layers: Transaction wrapping GetCompoundStructure(), modify layer.MaterialId for each selected layer, SetCompoundStructure(). Single Transaction for all selected layers.
- **D-17:** For family material parameters: Transaction wrapping element.get_Parameter().Set(materialId) for each selected parameter.
- **D-18:** On error: Transaction.RollBack() + MessageBox with French error description.
- **D-19:** On success: brief visual feedback (button flash or status text "Materiau applique") + refresh center panel to show updated material names.

### New RevitEventBridge Operations
- **D-20:** New enum values: GetAllMaterials, SetMaterialOnLayers, SetMaterialOnParameter, DuplicateMaterial.
- **D-21:** GetAllMaterials returns List<PresetMaterialDto> with all materials from FilteredElementCollector.OfClass(typeof(Material)).
- **D-22:** SetMaterialOnLayers takes a SetMatRequestDto (targetTypeIdValue, layerIndices[], materialIdValue) and performs the Transaction.
- **D-23:** DuplicateMaterial takes materialIdValue, returns new PresetMaterialDto with "[Original] copie" name.

### Inter-ViewModel Communication
- **D-24:** SetMatCommand lives in MainWindowViewModel (coordinates between panels). It reads SelectedLayers/SelectedParams from CenterPanelVM and SelectedPresetMaterial from RightPanelVM.
- **D-25:** After Set Mat success, send a RefreshLayersMessage via Messenger to trigger CenterPanelViewModel to re-fetch.

### Claude's Discretion
- Exact dialog/flyout design for "Ajouter au preset"
- Whether to cache GetAllMaterials or fetch each time
- Animation/transition for success feedback
- Error message wording details

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 1 & 2 Outputs (existing code to extend)
- `OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitEventBridge.cs` — Add new handlers
- `OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitRequestType.cs` — Add new enum values
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/RightPanelViewModel.cs` — Shell to populate
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/RightPanelView.xaml` — Shell to populate
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/MainWindowViewModel.cs` — Add SetMatCommand
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/CenterPanelViewModel.cs` — Read selected items
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/MainWindow.xaml` — Add Set Mat button

### Research Documents
- `.planning/research/ARCHITECTURE.md` — Transaction patterns, CompoundStructure write
- `.planning/research/PITFALLS.md` — CompoundStructure copy semantics, Transaction rollback, AppearanceAsset

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- RevitEventBridge pattern — extend with 4 new operations
- DTO pattern (SceneTypeDto, LayerDto) — follow for PresetMaterialDto, SetMatRequestDto
- WeakReferenceMessenger — reuse for RefreshLayersMessage
- OlympeTheme.xaml — TreeView/Button styles apply automatically
- CenterPanelViewModel.SelectedLayers/SelectedMaterialParams — read for Set Mat

### Established Patterns
- RevitEventBridge.MakeRequest(type, data) -> ExternalEvent.Raise() -> ProcessRequest -> callback
- ObservableCollection for all list data
- [RelayCommand] for all commands
- French UI text throughout

### Integration Points
- RightPanelView.xaml — currently placeholder
- MainWindow.xaml — needs Set Mat button between panels
- MainWindowViewModel — coordinates SetMat between 3 panel VMs

</code_context>

<specifics>
## Specific Ideas

- Set Mat button must be impossible to miss — it's the core action
- Success feedback should be immediate and satisfying (the user just changed materials)
- Preset groups should feel like a library the user curates over time
- The JSON file path dialog should remember the last used location

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 03-preset-panel-and-set-mat*
*Context gathered: 2026-04-11*
