# Phase 1: Foundation and Infrastructure - Context

**Gathered:** 2026-04-11
**Status:** Ready for planning

<domain>
## Phase Boundary

The add-in loads and displays a themed modeless singleton window in Revit 2024, 2025, and 2026 with all core architecture patterns validated: multi-target build, ExternalEvent dispatch, MVVM with DTOs, and .addin registration.

Requirements: INFRA-01 through INFRA-09, UI-01, UI-02, UI-04

</domain>

<decisions>
## Implementation Decisions

### Project Structure
- **D-01:** Use a single SDK-style .csproj with `<TargetFrameworks>net48;net8.0-windows</TargetFrameworks>` and conditional PackageReferences per Revit version. This is the modern standard per Stack research. If WPF XAML compilation across frameworks fails, fall back to Shared Project (.shproj) + 2 target projects (net48, net8.0-windows).
- **D-02:** Use Nice3point.Revit.Api NuGet packages for Revit API references (CopyLocal=false by default). Version-specific: 2024.x for net48, 2025.x/2026.x for net8.0-windows.
- **D-03:** Use PolySharp 1.15.0 to enable C# 12 features on the net48 target.
- **D-04:** All ElementId usage must use `.Value` (long), never `.IntegerValue` (deprecated). This is a day-one convention.

### Theme Olympe
- **D-05:** Dark theme via WPF ResourceDictionary. Palette: Background #1E1E2E, Surface #2D2D3D, Accent #FF9800 (ambre), Accent hover #FFA726, Text primary #E0E0E0, Text secondary #A0A0A0, Border #3D3D4D, Error #EF5350.
- **D-06:** All controls styled in the ResourceDictionary: Button, TreeView, ListBox, ScrollBar, TextBox, ComboBox, GridSplitter. Consistent rounded corners (CornerRadius=4) and accent color on focus/hover.

### Layout Trois Colonnes
- **D-07:** MainWindow uses a Grid with 3 columns and 2 GridSplitters. Default proportions: left 250px, center *, right 250px. GridSplitters allow user resizing. MinWidth 200px on side panels, MinWidth 300px on center.
- **D-08:** Each panel is a UserControl with its own ViewModel, hosted in the Grid columns.

### ExternalEvent Pattern
- **D-09:** Single IExternalEventHandler implementation with an enum-based dispatch (RevitRequestType). One ExternalEvent instance created in IExternalApplication.OnStartup, shared via a static singleton (RevitEventBridge).
- **D-10:** The handler receives request data via a thread-safe queue or typed property. Results are marshalled back to ViewModels via DTOs (no Revit types in ViewModels).
- **D-11:** Nice3point.Revit.Toolkit ExternalEventHandler<T> base class to be evaluated — if it fits the enum dispatch pattern, use it instead of raw IExternalEventHandler.

### Entry Points
- **D-12:** IExternalApplication for startup: register ribbon button, create ExternalEvent singleton.
- **D-13:** Ribbon button opens/shows the modeless singleton window. Window.Closing event is intercepted to Hide() instead of Close(), preventing disposal.
- **D-14:** Three .addin files generated (one per Revit version) with unique GUIDs, pointing to the correct assembly path.

### MVVM
- **D-15:** CommunityToolkit.Mvvm 8.4.2 with source generators: [ObservableProperty], [RelayCommand], ObservableObject base class.
- **D-16:** ViewModels live in a ViewModels/ folder, Views in Views/. One ViewModel per panel (LeftPanelViewModel, CenterPanelViewModel, RightPanelViewModel) + MainWindowViewModel to coordinate.

### Claude's Discretion
- Exact folder structure within the project (Models/, Services/, Helpers/, etc.)
- NuGet package version pinning strategy
- Whether to use Nice3point.Revit.Extensions or hand-write extension methods
- Unit test framework choice (if any tests in Phase 1)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Research Documents
- `.planning/research/STACK.md` — Complete technology stack with versions, csproj examples, and multi-target configuration
- `.planning/research/ARCHITECTURE.md` — Component boundaries, ExternalEvent pattern, data flow, build order
- `.planning/research/PITFALLS.md` — 18 domain-specific pitfalls with prevention strategies (threading, transactions, ElementId 64-bit)

### Project Documents
- `.planning/PROJECT.md` — Project context, constraints, key decisions
- `.planning/REQUIREMENTS.md` — All v1 requirements with REQ-IDs and traceability

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None (greenfield project)

### Established Patterns
- None yet — this phase establishes them

### Integration Points
- .addin file registration in %APPDATA%\Autodesk\Revit\Addins\{version}\
- Revit ribbon UI for add-in button

</code_context>

<specifics>
## Specific Ideas

- The multi-target build spike is the critical gate — if single csproj fails for WPF XAML, fall back to Shared Project immediately rather than debugging for hours
- The ExternalEvent round-trip proof should be a simple "get document title" operation that proves the full UI -> Handler -> DTO -> ViewModel pipeline works
- The theme should feel professional and polished (commercial product quality)

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-foundation-and-infrastructure*
*Context gathered: 2026-04-11*
