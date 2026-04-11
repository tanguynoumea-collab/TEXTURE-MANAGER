---
phase: 03-preset-panel-and-set-mat
plan: 01
subsystem: api, models, services
tags: [revit-api, system-text-json, compound-structure, transaction, dto, preset, wpf-converter]

# Dependency graph
requires:
  - phase: 02-read-path-layers-params
    provides: RevitEventBridge dispatch pattern, CenterPanelViewModel, LayerDto, MaterialParamDto
provides:
  - 7 DTOs defining preset data model and Set Mat request contracts
  - PresetService for JSON preset persistence with settings path management
  - DialogService for cross-TFM folder browsing
  - ArgbToColorConverter for color swatch binding
  - RefreshLayersMessage for post-SetMat center panel refresh
  - 4 new RevitEventBridge handlers (GetAllMaterials, SetMaterialOnLayers, SetMaterialOnParameter, DuplicateMaterial)
  - CenterPanelViewModel.CurrentTypeIdValue for SetMat coordination
affects: [03-02-PLAN (right panel UI), 03-03-PLAN (SetMat coordination)]

# Tech tracking
tech-stack:
  added: [System.Text.Json 8.0.5, System.Windows.Forms (net48 conditional)]
  patterns: [Get-Modify-Set CompoundStructure, Transaction with explicit RollBack, ExtractColorArgb helper, batched parameter assignment]

key-files:
  created:
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/PresetMaterialDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/PresetGroupDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/PresetCollectionDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/SetMatRequestDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/SetMatParamRequestDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/DuplicateMaterialRequestDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/AppSettingsDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Services/PresetService.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Services/DialogService.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Converters/ArgbToColorConverter.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Messages/RefreshLayersMessage.cs
  modified:
    - OlympeMaterialManager/src/OlympeMaterialManager/OlympeMaterialManager.csproj
    - OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitRequestType.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitEventBridge.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/CenterPanelViewModel.cs

key-decisions:
  - "Void handlers (SetMaterialOnLayers, SetMaterialOnParameter) called without result assignment in switch dispatch -- null result signals success to callback"
  - "SetMatParamRequestDto batches multiple ParameterDefinitionNames in a single Transaction for one undo step"
  - "ExtractColorArgb extracted as private static helper reused by GetAllMaterials and DuplicateMaterial handlers"

patterns-established:
  - "Get-Modify-Set CompoundStructure: GetCompoundStructure() returns COPY, modify layers, call SetCompoundStructure() to persist"
  - "Transaction with explicit RollBack: try { tx.Commit() } catch { if started && !ended: RollBack(); throw; }"
  - "PresetService JSON persistence: System.Text.Json with camelCase + WriteIndented, settings at %APPDATA%/Olympe/MaterialManager/settings.json"
  - "Conditional folder dialog: OpenFolderDialog on REVIT2025_OR_GREATER, FolderBrowserDialog on net48"

requirements-completed: [PRESET-01, PRESET-02, PRESET-04, PRESET-05, PRESET-06, PRESET-07, PRESET-08, PRESET-09, PRESET-10]

# Metrics
duration: 4min
completed: 2026-04-11
---

# Phase 3 Plan 1: Data Layer Summary

**7 preset DTOs, PresetService JSON persistence, 4 RevitEventBridge handlers (GetAllMaterials, SetMat layers/params, DuplicateMaterial) with Transaction patterns and CompoundStructure Get-Modify-Set**

## Performance

- **Duration:** 4 min
- **Started:** 2026-04-11T11:26:09Z
- **Completed:** 2026-04-11T11:30:13Z
- **Tasks:** 2
- **Files modified:** 15

## Accomplishments
- Complete preset data model: 7 DTOs (PresetMaterialDto, PresetGroupDto, PresetCollectionDto, SetMatRequestDto, SetMatParamRequestDto, DuplicateMaterialRequestDto, AppSettingsDto)
- PresetService with JSON Load/Save using System.Text.Json, settings path management at %APPDATA%, and 3 default groups (Murs, Sols, Autres)
- 4 new RevitEventBridge handlers implementing GetAllMaterials (FilteredElementCollector), SetMaterialOnLayers (CompoundStructure Get-Modify-Set), SetMaterialOnParameter (batched single Transaction), DuplicateMaterial (name collision handling)
- CenterPanelViewModel extended with CurrentTypeIdValue property and RefreshLayersMessage handler for post-SetMat refresh

## Task Commits

Each task was committed atomically:

1. **Task 1: Create DTOs, Services, Converter, Message, and add System.Text.Json NuGet** - `7a13b00` (feat)
2. **Task 2: Add 4 RevitEventBridge handlers and extend CenterPanelViewModel** - `19536eb` (feat)

## Files Created/Modified
- `Models/PresetMaterialDto.cs` - POCO: MaterialName, MaterialElementIdValue (long), ColorArgb (int)
- `Models/PresetGroupDto.cs` - POCO: GroupName, ObservableCollection<PresetMaterialDto>
- `Models/PresetCollectionDto.cs` - Root serialization unit wrapping groups
- `Models/SetMatRequestDto.cs` - TargetTypeIdValue, LayerIndices[], MaterialIdValue
- `Models/SetMatParamRequestDto.cs` - TargetTypeIdValue, MaterialIdValue, ParameterDefinitionNames[]
- `Models/DuplicateMaterialRequestDto.cs` - MaterialIdValue for duplication request
- `Models/AppSettingsDto.cs` - PresetFilePath for settings persistence
- `Services/PresetService.cs` - Load/Save/GetStoredPresetPath/StorePresetPath/GetDefaultCollection
- `Services/DialogService.cs` - ShowFolderBrowser with conditional compilation
- `Converters/ArgbToColorConverter.cs` - int ARGB to System.Windows.Media.Color
- `Messages/RefreshLayersMessage.cs` - ValueChangedMessage<long> for post-SetMat refresh
- `OlympeMaterialManager.csproj` - Added System.Text.Json 8.0.5, System.Windows.Forms (net48)
- `Events/RevitRequestType.cs` - 4 new enum values
- `Events/RevitEventBridge.cs` - 4 new handler methods + ExtractColorArgb helper
- `ViewModels/CenterPanelViewModel.cs` - CurrentTypeIdValue + RefreshLayersMessage registration

## Decisions Made
- Void handlers (SetMaterialOnLayers, SetMaterialOnParameter) return null result to callback on success, Exception on failure -- consistent with existing bridge pattern
- SetMatParamRequestDto batches multiple parameter names in a single Transaction for one undo step (per Research recommendation)
- ExtractColorArgb extracted as private static helper to avoid duplication between GetAllMaterials and DuplicateMaterial

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed void-to-object assignment in switch dispatch**
- **Found during:** Task 2 (RevitEventBridge handlers)
- **Issue:** SetMaterialOnLayers and SetMaterialOnParameter return void but were assigned to `result` (object?) in the switch, causing CS0029 compiler error
- **Fix:** Changed `result = HandleSetMaterialOnLayers(...)` to `HandleSetMaterialOnLayers(...)` (no assignment) -- null result correctly signals success
- **Files modified:** Events/RevitEventBridge.cs
- **Verification:** Build succeeds with 0 errors on both TFMs
- **Committed in:** 19536eb (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Trivial fix for void return type in dispatch switch. No scope change.

## Issues Encountered
None beyond the auto-fixed void assignment.

## User Setup Required
None - no external service configuration required.

## Known Stubs
None - all implementations are complete with full logic.

## Next Phase Readiness
- All DTOs, services, converter, message, and bridge handlers are ready for Plan 02 (right panel UI)
- CenterPanelViewModel.CurrentTypeIdValue is exposed for Plan 03 (SetMat coordination in MainWindowViewModel)
- PresetService.GetDefaultCollection() provides the 3 default groups for initial UI population

## Self-Check: PASSED

All 11 created files verified on disk. Both task commits (7a13b00, 19536eb) found in git log.

---
*Phase: 03-preset-panel-and-set-mat*
*Completed: 2026-04-11*
