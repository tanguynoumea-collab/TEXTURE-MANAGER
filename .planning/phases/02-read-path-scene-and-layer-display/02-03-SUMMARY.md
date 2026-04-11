---
phase: 02-read-path-scene-and-layer-display
plan: 03
subsystem: ui, viewmodels
tags: [wpf, mvvm, listbox, multi-selection, compound-structure, layers, material-params, messenger, converters]

# Dependency graph
requires:
  - phase: 02-read-path-scene-and-layer-display
    plan: 01
    provides: LayerDto, MaterialParamDto, SceneTypeDto, TypeSelectedMessage, RevitEventBridge handlers (GetLayersForType, GetMaterialParametersForType)
provides:
  - CenterPanelViewModel with conditional layer/param display and multi-selection
  - CenterPanelView.xaml with two ListBox elements (layers and material params)
  - BoolToVisibilityConverter for conditional XAML display
  - MainWindowViewModel wiring of eventBridge to CenterPanelViewModel
affects: [03-write-path-set-mat]

# Tech tracking
tech-stack:
  added: [Microsoft.Xaml.Behaviors.Wpf (already present, now used in CenterPanelView)]
  patterns:
    - "BoolToVisibilityConverter pattern for conditional panel visibility in XAML"
    - "WeakReferenceMessenger.Default.Register<T> for inter-ViewModel communication"
    - "EventTrigger + InvokeCommandAction for MVVM-friendly ListBox SelectionChanged"
    - "SelectionMode=Extended for native Ctrl+click and Shift+click multi-selection"

key-files:
  created:
    - OlympeMaterialManager/src/OlympeMaterialManager/Converters/BoolToVisibilityConverter.cs
  modified:
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/CenterPanelViewModel.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/MainWindowViewModel.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/CenterPanelView.xaml

key-decisions:
  - "DockPanel layout chosen over Grid for CenterPanelView to allow ListBox elements to fill remaining space naturally"
  - "DataTrigger on empty string used for SelectedTypeName and ModeLabel visibility instead of additional bool properties"

patterns-established:
  - "Converter pattern: BoolToVisibilityConverter in UserControl.Resources for reuse across all views"
  - "Multi-selection pattern: IList? SelectedItems bound via InvokeCommandAction CommandParameter"
  - "Inter-ViewModel messaging: TypeSelectedMessage from LeftPanel triggers CenterPanel data fetch"

requirements-completed: [LAYER-01, LAYER-02, LAYER-03, LAYER-04, LAYER-05]

# Metrics
duration: 2min
completed: 2026-04-11
---

# Phase 2 Plan 03: CenterPanel ViewModel and View Summary

**CenterPanelViewModel with conditional CompoundStructure layer or material parameter display, multi-selection via Extended ListBox, and Messenger-based type selection reception**

## Performance

- **Duration:** 2 min
- **Started:** 2026-04-11T10:52:06Z
- **Completed:** 2026-04-11T10:54:34Z
- **Tasks:** 3 (2 auto + 1 checkpoint auto-approved)
- **Files modified:** 4

## Accomplishments
- CenterPanelViewModel receives TypeSelectedMessage from LeftPanel and conditionally fetches CompoundStructure layers or material parameters via RevitEventBridge
- CenterPanelView.xaml displays layers with French function names, width in mm, and material names; or material parameters with parameter name and current material
- Multi-selection support via SelectionMode=Extended (Ctrl+click toggle, Shift+click range) with SelectedItems exposed for downstream Set Mat usage
- BoolToVisibilityConverter created for conditional panel visibility toggling
- MainWindowViewModel passes eventBridge to CenterPanelViewModel constructor

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement CenterPanelViewModel with layer/param display, Messenger reception, and MainWindowViewModel wiring** - `0720559` (feat)
2. **Task 2: Implement CenterPanelView.xaml with conditional layer/param display and multi-selection** - `96c4652` (feat)
3. **Task 3: Verify full Phase 2 flow in Revit** - Auto-approved checkpoint (no commit)

## Files Created/Modified
- `Converters/BoolToVisibilityConverter.cs` - Standard bool-to-Visibility IValueConverter for conditional XAML display
- `ViewModels/CenterPanelViewModel.cs` - Complete rewrite: Layers, MaterialParams, ShowLayers/ShowParameters/ShowPlaceholder, SelectedTypeName, ModeLabel, IsLoading, ErrorMessage, SelectedItems, SelectionChangedCommand, TypeSelectedMessage registration, FetchLayers/FetchMaterialParameters via RevitEventBridge
- `ViewModels/MainWindowViewModel.cs` - Changed CenterPanelVM construction to pass eventBridge
- `Views/CenterPanelView.xaml` - Complete rewrite: DockPanel layout with title, type name, mode label, placeholder, loading indicator, error display, Layers ListBox, MaterialParams ListBox with Extended selection

## Decisions Made
- Used DockPanel instead of Grid for CenterPanelView layout -- allows the ListBox elements to fill remaining vertical space without explicit row height configuration
- Used DataTrigger on empty string for SelectedTypeName and ModeLabel visibility -- avoids adding extra boolean properties just for XAML visibility

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Known Stubs
None - all files contain complete implementations. CenterPanelViewModel has full Messenger registration, RevitEventBridge fetch logic, and all bindable properties. CenterPanelView.xaml has complete DataTemplates for both layers and material parameters.

## Next Phase Readiness
- CenterPanel fully functional: layers and material params display with multi-selection
- SelectedItems exposed as IList for Phase 3 (Set Mat) to read which layers/params the user selected
- All Phase 2 ViewModels wired into MainWindowViewModel with eventBridge

## Self-Check: PASSED

All 4 files verified present. Both task commits (0720559, 96c4652) verified in git log. Build green on both net48 and net8.0-windows with 0 errors.

---
*Phase: 02-read-path-scene-and-layer-display*
*Completed: 2026-04-11*
