---
phase: 03-preset-panel-and-set-mat
plan: 02
subsystem: ui, viewmodels
tags: [wpf, treeview, hierarchicaldatatemplate, preset, context-menu, argb-converter, modal-dialog]

# Dependency graph
requires:
  - phase: 03-preset-panel-and-set-mat
    plan: 01
    provides: PresetService, DialogService, DTOs (PresetGroupDto, PresetMaterialDto, PresetCollectionDto, DuplicateMaterialRequestDto), ArgbToColorConverter, RevitEventBridge handlers (GetAllMaterials, DuplicateMaterial)
provides:
  - RightPanelViewModel with full preset CRUD (CreerGroupe, AjouterMateriau, DupliquerMateriau, SupprimerMateriau)
  - RightPanelView.xaml with TreeView, HierarchicalDataTemplate, color swatches, context menus
  - AddMaterialDialog for selecting project materials with search filter and group picker
  - SelectedPresetMaterial exposed for Set Mat coordination in Plan 03
  - MainWindowViewModel wiring with PresetService injection and PropertyChanged subscription
affects: [03-03-PLAN (SetMat coordination via SelectedPresetMaterial)]

# Tech tracking
tech-stack:
  added: []
  patterns: [HierarchicalDataTemplate nested group/item, Tag proxy for ContextMenu in TreeView items, CollectionViewSource filter in dialog code-behind, AutoSave on every CRUD mutation]

key-files:
  created:
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/AddMaterialDialog.xaml
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/AddMaterialDialog.xaml.cs
  modified:
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/RightPanelViewModel.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/RightPanelView.xaml
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/MainWindowViewModel.cs

key-decisions:
  - "AddMaterialDialog uses code-behind for filter + dialog result pattern (not a full ViewModel) -- acceptable for simple pick-from-list dialog per MVVM convention"
  - "OnSelectedPresetMaterialChanged placeholder method in MainWindowViewModel -- Plan 03 will add AppliquerMateriauCommand.NotifyCanExecuteChanged()"
  - "Tag proxy pattern on StackPanel (not TreeViewItem) for material context menu -- material items use StackPanel.Tag to route commands through visual tree boundary"

patterns-established:
  - "HierarchicalDataTemplate nested templates: group template with ItemTemplate for child material items"
  - "AutoSave pattern: every mutation (add/remove/duplicate/create group) triggers PresetService.Save via AutoSave()"
  - "Dialog-from-ViewModel pattern: ViewModel creates modal dialog instance, sets properties, reads back results -- keeps business logic in ViewModel"
  - "CollectionViewSource with Refresh() for search filtering in dialog code-behind"

requirements-completed: [PRESET-01, PRESET-02, PRESET-03, PRESET-04, PRESET-05, PRESET-06, PRESET-07]

# Metrics
duration: 4min
completed: 2026-04-11
---

# Phase 3 Plan 2: Right Panel UI Summary

**Full RightPanelViewModel with preset CRUD commands (create group, add/duplicate/remove material), TreeView with HierarchicalDataTemplate showing grouped presets with color swatches and context menus, AddMaterialDialog with searchable material list**

## Performance

- **Duration:** 4 min
- **Started:** 2026-04-11T11:32:56Z
- **Completed:** 2026-04-11T11:37:07Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- RightPanelViewModel fully replaced: PresetGroups collection, SelectedPresetMaterial for Set Mat, all CRUD commands (CreerGroupe, AjouterMateriau, DupliquerMateriau, SupprimerMateriau), AutoSave on every modification, auto-load from stored JSON path on construction
- RightPanelView.xaml with TreeView using HierarchicalDataTemplate: group headers (name + count badge), material items (color swatch via ArgbToColorConverter + name), context menu for Dupliquer/Supprimer via Tag proxy pattern
- AddMaterialDialog: searchable ListBox of all project materials with CollectionViewSource filter, ComboBox group picker, modal dialog result pattern
- MainWindowViewModel wiring: PresetService injection, eventBridge forwarding to RightPanelVM, SelectedPresetMaterial change subscription with placeholder hook for Plan 03

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement RightPanelViewModel with full preset management** - `deac96c` (feat)
2. **Task 2: Create RightPanelView.xaml and AddMaterialDialog** - `ffd76e3` (feat)

## Files Created/Modified
- `ViewModels/RightPanelViewModel.cs` - Full preset management ViewModel: PresetGroups, SelectedPresetMaterial, CRUD commands, AutoSave, LoadPresets, FindGroupContaining
- `Views/RightPanelView.xaml` - TreeView with HierarchicalDataTemplate, color swatches, context menu, action buttons, group creation
- `Views/AddMaterialDialog.xaml` - Modal dialog: search TextBox, ListBox with material swatches, ComboBox group picker, Ajouter/Annuler buttons
- `Views/AddMaterialDialog.xaml.cs` - Code-behind: CollectionViewSource filter, dialog result, property accessors
- `ViewModels/MainWindowViewModel.cs` - PresetService injection, eventBridge wiring, OnSelectedPresetMaterialChanged hook

## Decisions Made
- AddMaterialDialog uses code-behind for filter + dialog result rather than a full ViewModel -- pragmatic for a simple pick-from-list dialog, keeps file count manageable
- OnSelectedPresetMaterialChanged as placeholder method in MainWindowViewModel -- avoids referencing non-existent AppliquerMateriauCommand, Plan 03 will fill in the body
- Tag proxy on StackPanel element within inner DataTemplate for context menu command routing -- follows Phase 2 established pattern

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Created AddMaterialDialog files in Task 1 commit**
- **Found during:** Task 1 (RightPanelViewModel implementation)
- **Issue:** RightPanelViewModel.AjouterMateriau() references AddMaterialDialog class which was planned for Task 2. Build would fail without the dialog files.
- **Fix:** Created AddMaterialDialog.xaml and AddMaterialDialog.xaml.cs as complete implementations in Task 1 (not stubs), then Task 2 focused on RightPanelView.xaml only
- **Files modified:** Views/AddMaterialDialog.xaml, Views/AddMaterialDialog.xaml.cs
- **Verification:** Build succeeds on both TFMs with 0 errors
- **Committed in:** deac96c (Task 1 commit)

**2. [Rule 3 - Blocking] Replaced AppliquerMateriauCommand reference with placeholder method**
- **Found during:** Task 1 (MainWindowViewModel update)
- **Issue:** Plan specified calling AppliquerMateriauCommand?.NotifyCanExecuteChanged() but that command does not exist yet (Plan 03)
- **Fix:** Created OnSelectedPresetMaterialChanged() placeholder method with comment for Plan 03 to fill in
- **Files modified:** ViewModels/MainWindowViewModel.cs
- **Verification:** Build succeeds, no unresolved references
- **Committed in:** deac96c (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (2 blocking)
**Impact on plan:** Both fixes necessary for compilation. Dialog files are complete (not stubs). No scope change -- same total output, just different task boundaries.

## Issues Encountered
None beyond the auto-fixed blocking issues.

## User Setup Required
None - no external service configuration required.

## Known Stubs
None - all implementations are complete with full logic. The OnSelectedPresetMaterialChanged() method is an intentional placeholder that Plan 03 will complete when adding the AppliquerMateriau command.

## Next Phase Readiness
- RightPanelViewModel exposes SelectedPresetMaterial for Set Mat coordination in Plan 03
- All CRUD operations functional: create group, add material (via dialog), duplicate material (via Revit bridge), remove material
- AutoSave triggers on every modification -- presets persist to JSON
- MainWindowViewModel already subscribed to SelectedPresetMaterial changes -- Plan 03 just needs to add the command and fill in the hook

## Self-Check: PASSED

All 5 created/modified files verified on disk. Both task commits (deac96c, ffd76e3) found in git log.

---
*Phase: 03-preset-panel-and-set-mat*
*Completed: 2026-04-11*
