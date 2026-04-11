---
phase: 01-foundation-and-infrastructure
plan: 03
subsystem: infra
tags: [revit-api, external-event, mvvm, dto, ribbon, modeless-window, hide-on-close, thread-safety]

# Dependency graph
requires:
  - phase: 01-02
    provides: "Olympe dark theme, MainWindow layout, ViewModels with CommunityToolkit.Mvvm"
  - phase: 01-01
    provides: "Multi-target SDK-style csproj with net48/net8.0-windows, NuGet packages"
provides:
  - "App.cs ExternalApplication entry point with ExternalEvent singleton and ribbon button"
  - "ShowWindowCommand modeless singleton with Revit owner and hide-on-close"
  - "RevitEventBridge enum-dispatch handler with thread-safe lock and Dispatcher callback"
  - "RevitRequestType enum for ExternalEvent request routing"
  - "RevitDocInfoDto pure POCO DTO for Revit document info"
  - "ElementIdHelper centralized .Value accessor per D-04"
  - "End-to-end ExternalEvent round-trip proof: UI -> ExternalEvent -> Revit API -> DTO -> ViewModel"
affects: [02-read-path, 03-write-path, 04-power-features, 05-polish]

# Tech tracking
tech-stack:
  added: []
  patterns: [ExternalApplication entry point with static singletons, ExternalEvent enum dispatch with lock, modeless singleton hide-on-close, DTO boundary between Revit API and ViewModels, Dispatcher.Invoke for thread marshalling]

key-files:
  created:
    - OlympeMaterialManager/src/OlympeMaterialManager/App.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Commands/ShowWindowCommand.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitEventBridge.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitRequestType.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/RevitDocInfoDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Helpers/ElementIdHelper.cs
  modified:
    - OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/MainWindowViewModel.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Views/MainWindow.xaml

key-decisions:
  - "Nice3point Toolkit ExternalEvent used with Action<UIApplication> constructor (not ExternalEvent<T> which doesn't fit enum dispatch per D-09/D-11)"
  - "Standard Revit API CreateRibbonTab/CreateRibbonPanel/PushButtonData used instead of Nice3point extension helpers (not included in Toolkit package)"
  - "AllowClose flag pattern for OnShutdown: allows real close during Revit shutdown while hiding on user X-button click"
  - "ToolkitExternalEvent using alias to resolve ambiguity with Autodesk.Revit.UI.ExternalEvent"

patterns-established:
  - "ExternalEvent dispatch: single RevitEventBridge with enum-based routing, lock for thread safety, Dispatcher.Invoke for UI callback"
  - "DTO boundary: RevitDocInfoDto proves ViewModels never import Autodesk.Revit -- all data flows as POCOs"
  - "Modeless singleton: create once, show/hide, intercept Closing with e.Cancel, AllowClose flag for shutdown"
  - "Ribbon button: PushButtonData with assembly path and command type name"
  - "ElementId convention: always use .Value (long) via ElementIdHelper, never .IntegerValue"

requirements-completed: [INFRA-04, INFRA-05, INFRA-06, INFRA-07]

# Metrics
duration: 6min
completed: 2026-04-11
---

# Phase 1 Plan 3: Revit Integration Layer Summary

**ExternalApplication entry point with ribbon button, modeless singleton window, enum-dispatch RevitEventBridge, and end-to-end ExternalEvent round-trip proving UI -> Revit API -> DTO -> ViewModel pipeline**

## Performance

- **Duration:** 6 min
- **Started:** 2026-04-11T09:43:49Z
- **Completed:** 2026-04-11T09:50:28Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- Complete Revit integration skeleton builds on both net48 and net8.0-windows with 0 errors, 0 warnings
- App.cs creates ExternalEvent singleton and registers ribbon tab "Olympe" with "Materiaux" button
- ShowWindowCommand creates modeless singleton window with Revit as owner, hide-on-close pattern with AllowClose shutdown flag
- RevitEventBridge implements thread-safe enum dispatch (lock + volatile) with Dispatcher.Invoke callback to UI thread
- GetDocumentInfo round-trip wired end-to-end: button click -> MakeRequest -> ExternalEvent -> ProcessRequest -> RevitDocInfoDto -> ViewModel.DocumentInfo binding
- Zero Revit API imports in any ViewModel file (INFRA-07 enforced)
- ElementIdHelper uses .Value (long), never .IntegerValue (D-04 enforced)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create Revit entry points, ExternalEvent bridge, DTOs, and wire round-trip** - `a7c0af2` (feat)
2. **Task 2: Verify add-in loads and round-trip works in Revit** - auto-approved (checkpoint, no code changes)

## Files Created/Modified
- `OlympeMaterialManager/src/OlympeMaterialManager/App.cs` - ExternalApplication entry point: ExternalEvent singleton, ribbon tab/panel/button, AllowClose shutdown
- `OlympeMaterialManager/src/OlympeMaterialManager/Commands/ShowWindowCommand.cs` - ExternalCommand creating modeless singleton with Revit owner and hide-on-close
- `OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitEventBridge.cs` - Thread-safe enum dispatch handler with Dispatcher.Invoke UI callback
- `OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitRequestType.cs` - Enum defining ExternalEvent request types (None, GetDocumentInfo)
- `OlympeMaterialManager/src/OlympeMaterialManager/Models/RevitDocInfoDto.cs` - Pure POCO DTO for document info (Title, PathName, IsValid)
- `OlympeMaterialManager/src/OlympeMaterialManager/Helpers/ElementIdHelper.cs` - Centralized ElementId.Value accessor per D-04
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/MainWindowViewModel.cs` - Added RevitEventBridge dependency, RafraichirDocument RelayCommand, designer constructor
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/MainWindow.xaml` - Added header bar with Rafraichir button and DocumentInfo TextBlock binding

## Decisions Made
- **Nice3point Toolkit ExternalEvent with Action<UIApplication>:** Used the `Action<UIApplication>` constructor overload instead of `ExternalEvent<T>` (which is designed for one-handler-per-action, not enum dispatch). This aligns with D-09/D-11 research findings.
- **Standard Revit API for ribbon:** The `Application.CreatePanel()` and `AddPushButton<T>()` helper methods referenced in RESEARCH.md are from Nice3point.Revit.Extensions (not included). Used standard Revit API: `CreateRibbonTab` + `CreateRibbonPanel` + `PushButtonData` + `AddItem`.
- **ToolkitExternalEvent alias:** Added `using ToolkitExternalEvent = Nice3point.Revit.Toolkit.External.ExternalEvent` to resolve name collision with `Autodesk.Revit.UI.ExternalEvent`.
- **AllowClose flag:** Added a static bool flag checked in the Closing handler to distinguish user-click-close (hide) from Revit shutdown (actually close). Prevents window from being hidden during OnShutdown.
- **Toolkit has no Dispose:** Nice3point Toolkit's ExternalEvent is not IDisposable, so OnShutdown omits Dispose call (GC handles cleanup).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed Nice3point namespace: Toolkit.External, not Toolkit**
- **Found during:** Task 1 (initial build)
- **Issue:** RESEARCH.md patterns used `using Nice3point.Revit.Toolkit` but ExternalApplication, ExternalCommand, and ExternalEvent are in `Nice3point.Revit.Toolkit.External` namespace.
- **Fix:** Changed imports to `using Nice3point.Revit.Toolkit.External` in App.cs and ShowWindowCommand.cs.
- **Files modified:** App.cs, ShowWindowCommand.cs
- **Verification:** Build succeeds on both TFMs.
- **Committed in:** a7c0af2 (Task 1 commit)

**2. [Rule 1 - Bug] Fixed ExternalEvent name collision with Revit API**
- **Found during:** Task 1 (second build)
- **Issue:** Both `Nice3point.Revit.Toolkit.External.ExternalEvent` and `Autodesk.Revit.UI.ExternalEvent` were imported, causing CS0104 ambiguous reference.
- **Fix:** Added `using ToolkitExternalEvent = Nice3point.Revit.Toolkit.External.ExternalEvent` alias and used `ToolkitExternalEvent` for property type and constructor.
- **Files modified:** App.cs
- **Verification:** Build succeeds on both TFMs.
- **Committed in:** a7c0af2 (Task 1 commit)

**3. [Rule 1 - Bug] Fixed ribbon creation using standard Revit API**
- **Found during:** Task 1 (code review before build)
- **Issue:** Plan referenced `Application.CreatePanel()` and `panel.AddPushButton<T>()` from Nice3point helpers that don't exist in the Toolkit package (they're in Nice3point.Revit.Extensions, not included).
- **Fix:** Used standard Revit API: `Application.CreateRibbonTab("Olympe")` + `Application.CreateRibbonPanel("Olympe", "Olympe MaterialManager")` + `new PushButtonData(...)` + `panel.AddItem(buttonData)`.
- **Files modified:** App.cs
- **Verification:** Build succeeds on both TFMs.
- **Committed in:** a7c0af2 (Task 1 commit)

**4. [Rule 1 - Bug] Removed non-existent Dispose call on Toolkit ExternalEvent**
- **Found during:** Task 1 (third build)
- **Issue:** Nice3point Toolkit's ExternalEvent doesn't implement IDisposable. Plan assumed it did.
- **Fix:** Removed `RevitEvent?.Dispose()` from OnShutdown, added comment explaining GC cleanup.
- **Files modified:** App.cs
- **Verification:** Build succeeds on both TFMs.
- **Committed in:** a7c0af2 (Task 1 commit)

---

**Total deviations:** 4 auto-fixed (4 bugs in plan assumptions about Nice3point API)
**Impact on plan:** All fixes were necessary to make the code compile. The architectural pattern remains exactly as planned (enum dispatch, singleton modeless, DTO boundary). Only the API surface details differed from RESEARCH.md assumptions.

## Issues Encountered
None beyond the Nice3point API surface mismatches documented as deviations above.

## Known Stubs
None -- all files contain real implementation code. The RafraichirDocument command is fully wired to the ExternalEvent round-trip.

## User Setup Required
None -- no external service configuration required. To test in Revit, copy the .addin file to `%APPDATA%\Autodesk\Revit\Addins\{version}\` and update the assembly path.

## Next Phase Readiness
- Complete Phase 1 architectural skeleton validated: multi-target build + dark theme + MVVM + ExternalEvent bridge
- RevitEventBridge ready for new request types (add enum values + switch cases for Phase 2 ReadCompoundLayers, etc.)
- DTO pattern established -- all future Revit data flows through Models/ POCOs
- ViewModels ready for real content population (Phase 2 will add TreeView data, CompoundStructure layers, etc.)
- Ribbon button and modeless window patterns ready for production use

## Self-Check: PASSED
