---
phase: 01-foundation-and-infrastructure
plan: 02
subsystem: ui
tags: [wpf, xaml, dark-theme, resourcedictionary, mvvm, communitytoolkit, gridsplitter, usercontrol]

# Dependency graph
requires:
  - phase: 01-01
    provides: "Multi-target SDK-style csproj with net48/net8.0-windows, NuGet packages"
provides:
  - "Olympe dark theme ResourceDictionary with all D-05 colors and D-06 control styles"
  - "Three-column MainWindow layout with GridSplitters (250px / * / 250px)"
  - "Shell ViewModels (MainWindow, LeftPanel, CenterPanel, RightPanel) using CommunityToolkit.Mvvm"
  - "Panel UserControls with French placeholder text"
affects: [01-03, 02-read-path, 03-write-path, 05-polish]

# Tech tracking
tech-stack:
  added: []
  patterns: [WPF ResourceDictionary dark theme, implicit styles with ControlTemplate, MVVM with ObservableObject/ObservableProperty, three-column Grid with GridSplitters, UserControl per panel with own ViewModel]

key-files:
  created:
    - OlympeMaterialManager/src/OlympeMaterialManager/Themes/OlympeTheme.xaml
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/MainWindow.xaml
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/MainWindow.xaml.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/LeftPanelView.xaml
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/LeftPanelView.xaml.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/CenterPanelView.xaml
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/CenterPanelView.xaml.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/RightPanelView.xaml
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/RightPanelView.xaml.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/MainWindowViewModel.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/LeftPanelViewModel.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/CenterPanelViewModel.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/RightPanelViewModel.cs
  modified: []

key-decisions:
  - "Comprehensive ComboBox ControlTemplate included rather than deferred -- dark theme requires full template override for correct appearance"
  - "Thin scrollbar style (8px width/height) with Track-only template for minimal visual footprint"
  - "TreeViewItem ControlTemplate with custom expander arrow (Path geometry) using accent color on expand/hover"

patterns-established:
  - "Dark theme pattern: Color resources -> SolidColorBrush resources -> implicit styles with ControlTemplate overrides"
  - "Panel pattern: UserControl with Border+StackPanel shell, AccentBrush header TextBlock, TextSecondaryBrush placeholder"
  - "ViewModel pattern: partial class inheriting ObservableObject with [ObservableProperty] fields"
  - "MainWindow pattern: 5-column Grid (panel/splitter/panel/splitter/panel) with child VM bindings"

requirements-completed: [UI-01, UI-02, UI-04, INFRA-08]

# Metrics
duration: 3min
completed: 2026-04-11
---

# Phase 1 Plan 2: Olympe Theme and WPF Shell Layout Summary

**Dark Olympe theme with 8 color resources, 10 styled controls, and 3-column MainWindow hosting panel UserControls with CommunityToolkit.Mvvm shell ViewModels**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-11T09:37:33Z
- **Completed:** 2026-04-11T09:41:12Z
- **Tasks:** 2
- **Files modified:** 13

## Accomplishments
- Complete dark theme ResourceDictionary with all 8 D-05 colors, brushes, and implicit styles for Button, TextBlock, TextBox, ComboBox, ListBox, TreeView, ScrollBar, GridSplitter -- all with CornerRadius=4
- Three-column MainWindow layout with 2 GridSplitters matching D-07 proportions (250px / * / 250px with MinWidth constraints)
- Four ViewModels using CommunityToolkit.Mvvm [ObservableProperty] source generators verified compiling on both net48 and net8.0-windows
- All visible UI text in French (per UI-04): "Scene active : aucune", "Selectionnez un type pour voir ses couches", "Aucun preset configure"

## Task Commits

Each task was committed atomically:

1. **Task 1: Create Olympe dark theme ResourceDictionary with all control styles** - `a24a4a3` (feat)
2. **Task 2: Create MainWindow, panel UserControls, and all ViewModels** - `9b61a5b` (feat)

## Files Created/Modified
- `OlympeMaterialManager/src/OlympeMaterialManager/Themes/OlympeTheme.xaml` - Dark theme ResourceDictionary with 8 colors, 8 brushes, keyed OlympeWindowStyle, and implicit styles for all D-06 controls
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/MainWindowViewModel.cs` - Root ViewModel coordinating three child panel ViewModels
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/LeftPanelViewModel.cs` - Left panel (Familles/Types) shell ViewModel
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/CenterPanelViewModel.cs` - Center panel (Couches/Parametres) shell ViewModel
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/RightPanelViewModel.cs` - Right panel (Materiaux Preset) shell ViewModel
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/MainWindow.xaml` - 3-column Grid layout with GridSplitters hosting panel UserControls
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/MainWindow.xaml.cs` - Minimal code-behind (InitializeComponent only)
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/LeftPanelView.xaml` - Left panel UserControl shell with French placeholder
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/LeftPanelView.xaml.cs` - Minimal code-behind
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/CenterPanelView.xaml` - Center panel UserControl shell with French placeholder
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/CenterPanelView.xaml.cs` - Minimal code-behind
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/RightPanelView.xaml` - Right panel UserControl shell with French placeholder
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/RightPanelView.xaml.cs` - Minimal code-behind

## Decisions Made
- **Comprehensive ComboBox ControlTemplate:** Included a full ControlTemplate with ToggleButton, Popup, and accent-colored drop arrow rather than deferring to Phase 5. The dark theme requires template overrides for correct appearance.
- **Thin scrollbar design:** 8px width for vertical, 8px height for horizontal scrollbars with Track-only template (no arrow buttons) for a modern minimal look.
- **TreeViewItem custom expander:** Used Path geometry with RotateTransform for expand/collapse arrow, colored with AccentBrush when expanded or hovered.

## Deviations from Plan

None -- plan executed exactly as written.

## Issues Encountered
None.

## Known Stubs

The panel UserControls contain intentional placeholder text that will be replaced with real content in future phases:
- `LeftPanelView.xaml` line 14: "Scene active : aucune" -- TreeView content added in Phase 2
- `CenterPanelView.xaml` line 14: "Selectionnez un type pour voir ses couches" -- CompoundStructure display added in Phase 2
- `RightPanelView.xaml` line 14: "Aucun preset configure" -- Preset list added in Phase 3

These stubs are intentional shell placeholders per the plan and do not block the plan's goal (establishing the themed WPF layout shell with MVVM infrastructure).

## User Setup Required
None -- no external service configuration required.

## Next Phase Readiness
- Theme and layout shell ready for Plan 01-03 (ExternalEvent bridge, App entry point, ribbon button)
- ViewModels ready to receive RevitEventBridge parameter injection in Plan 01-03
- Panel UserControls ready for real content population in Phase 2
- CommunityToolkit.Mvvm source generators confirmed working on both TFMs -- safe to use [RelayCommand] in future plans

## Self-Check: PASSED

All 13 created files verified present. Both task commits (a24a4a3, 9b61a5b) verified in git log.

---
*Phase: 01-foundation-and-infrastructure*
*Completed: 2026-04-11*
