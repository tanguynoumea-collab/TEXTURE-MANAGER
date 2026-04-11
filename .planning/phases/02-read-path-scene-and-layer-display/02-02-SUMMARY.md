---
phase: 02-read-path-scene-and-layer-display
plan: 02
subsystem: ui, viewmodel
tags: [wpf, mvvm, treeview, combobox, scene-management, messenger, observable-collection]

# Dependency graph
requires:
  - phase: 02-read-path-scene-and-layer-display
    plan: 01
    provides: SceneDto, SceneTypeDto, FamilyCategoryDto, GetTypeListRequestDto, TypeSelectedMessage, CategorySortComparer, RevitEventBridge handlers
provides:
  - LeftPanelViewModel with scene CRUD (create, switch), type add/remove, family/type ComboBox population, TreeView selection with Messenger notification
  - LeftPanelView.xaml with scene creation UI, scene selector, TreeView with category grouping, add-type ComboBoxes, context menu, Delete key binding
  - MainWindowViewModel passes eventBridge to LeftPanelViewModel
affects: [02-03, 03-write-path-set-mat]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Tag binding proxy pattern for ContextMenu command binding across visual tree boundary"
    - "EventTrigger/InvokeCommandAction for TreeView.SelectedItemChanged (non-bindable SelectedItem)"
    - "CollectionViewSource with PropertyGroupDescription for TreeView category grouping"
    - "BasedOn implicit style extension for TreeViewItem with ContextMenu addition"

key-files:
  created: []
  modified:
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/LeftPanelViewModel.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/MainWindowViewModel.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/LeftPanelView.xaml

key-decisions:
  - "ContextMenu binding uses Tag proxy pattern (PlacementTarget.Tag) since ContextMenu is in a separate visual tree"
  - "TreeViewItem style uses BasedOn to extend implicit theme style rather than duplicating the full ControlTemplate"
  - "CollectionViewSource grouping done in ViewModel SetupCustomSort() method rather than XAML-only CollectionViewSource resource for better control over sort + group lifecycle"

patterns-established:
  - "Tag binding proxy: TreeViewItem.Tag = DataContext from parent, ContextMenu.Command binds via PlacementTarget.Tag"
  - "EventTrigger with InvokeCommandAction for non-bindable WPF events (TreeView.SelectedItemChanged)"
  - "Scene management pattern: ObservableCollection<SceneDto> with ActiveScene switching"

requirements-completed: [SCENE-01, SCENE-02, SCENE-03, SCENE-05, SCENE-06, SCENE-07, SCENE-08]

# Metrics
duration: 3min
completed: 2026-04-11
---

# Phase 2 Plan 02: Left Panel ViewModel and View Summary

**Left panel with scene CRUD, TreeView category grouping, two-ComboBox type adding, and Messenger-based type selection notification**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-11T10:51:56Z
- **Completed:** 2026-04-11T10:55:46Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Implemented full LeftPanelViewModel with scene creation (CreerScene), type add/remove (AjouterType/SupprimerType), family/type ComboBox population via RevitEventBridge, and TypeSelectedMessage notification
- Created LeftPanelView.xaml with complete scene management UI: creation TextBox + Button, scene selector ComboBox, TreeView with category grouping (GroupStyle), add-type ComboBoxes with loading indicators, context menu for removal, and Delete key binding
- Wired MainWindowViewModel to pass eventBridge to LeftPanelViewModel constructor

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement LeftPanelViewModel with scene management and type operations** - `0b53d5b` (feat)
2. **Task 2: Implement LeftPanelView.xaml with scene UI, TreeView, and add-type ComboBoxes** - `53e4fbb` (feat)

## Files Created/Modified
- `ViewModels/LeftPanelViewModel.cs` - Full scene CRUD, family/type loading, TreeView selection, Messenger notification, custom sort setup
- `ViewModels/MainWindowViewModel.cs` - Updated to pass eventBridge to LeftPanelViewModel constructor
- `Views/LeftPanelView.xaml` - Complete left panel UI with scene creation, selector, TreeView grouping, ComboBoxes, context menu, key bindings

## Decisions Made
- ContextMenu command binding uses Tag proxy pattern (PlacementTarget.Tag.SupprimerTypeCommand) to cross visual tree boundary, since ContextMenu exists in a separate visual tree from the TreeViewItem
- TreeViewItem style uses BasedOn="{StaticResource {x:Type TreeViewItem}}" to extend the implicit OlympeTheme style rather than duplicating the full ControlTemplate
- SetupCustomSort() called from OnActiveSceneChanged partial method to configure CategorySortComparer and PropertyGroupDescription at the right lifecycle point
- CenterPanelVM was already wired with eventBridge by parallel plan 02-03; no conflict -- only LeftPanelVM line was updated

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Known Stubs
None - all files contain complete implementations. LeftPanelViewModel has all scene CRUD commands, family/type loading, and Messenger notification. LeftPanelView.xaml has all UI elements with proper bindings.

## Next Phase Readiness
- LeftPanelViewModel sends TypeSelectedMessage on type selection, ready for CenterPanelViewModel to receive (Plan 02-03)
- Family/type ComboBoxes wired to RevitEventBridge GetFamilyList/GetTypeList handlers from Plan 02-01
- TreeView with CategorySortComparer grouping ready for runtime display

## Self-Check: PASSED

All 3 files verified present. Both task commits (0b53d5b, 53e4fbb) verified in git log. Build green on both net48 and net8.0-windows.

---
*Phase: 02-read-path-scene-and-layer-display*
*Completed: 2026-04-11*
