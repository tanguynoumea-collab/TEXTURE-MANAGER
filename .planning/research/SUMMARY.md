# Project Research Summary

**Project:** Olympe MaterialManager
**Domain:** Revit WPF Add-in - Material Management (BIM tooling, architect-targeted)
**Researched:** 2026-04-11
**Confidence:** HIGH

## Executive Summary

Olympe MaterialManager is a professional Revit add-in targeting architects who need fast, visual material assignment across CompoundStructure layers and loaded families. The existing market (ModPlus, BIMsmith, native Material Browser) has clear gaps: no tool combines a unified family/type tree, layer editor, and persistent preset palette in a single modeless window. The recommended build approach is a multi-targeting SDK-style .csproj (net48 for Revit 2024, net8.0-windows for Revit 2025/2026), using the Nice3point ecosystem for Revit API NuGet packages, CommunityToolkit.Mvvm 8.4.2 for MVVM infrastructure, and WiX v5 for the installer. The original PROJECT.md assumptions about solution structure and toolchain versions require several corrections before any code is written.

The most important architectural decision is the .NET runtime split: Revit 2024 runs on .NET Framework 4.8 while Revit 2025 and 2026 run on .NET 8. This is not a minor configuration detail -- it affects every dependency choice, project file structure, and build pipeline. All Revit API interaction must be routed through a single IExternalEventHandler (the ExternalEvent pattern), because the API is single-threaded and only callable inside specific Revit-managed callbacks. ViewModels must never reference Revit types; they communicate with the Revit layer through plain DTOs and ElementId values stored as long (never int, after the Revit 2024 int64 change).

The highest-risk areas are the ExternalEvent communication pattern (correctness here prevents crashes), the CompoundStructure Get-Modify-Set pattern (forgetting SetCompoundStructure silently loses changes), and the AppearanceAssetEditScope transaction lifecycle (tint editing requires an open Transaction before Commit). These are well-documented in the Revit developer community but remain the most common sources of bugs. Building the infrastructure skeleton and validating multi-version loading before writing any feature code is mandatory.

---

## Key Findings

### Recommended Stack

The stack is built around the Nice3point ecosystem, which provides clean NuGet-distributed Revit API assemblies with companion Toolkit and Extensions packages. A single SDK-style .csproj with conditional PackageReferences per TargetFramework replaces the Shared Project plus 3-target-project structure described in PROJECT.md. CommunityToolkit.Mvvm 8.4.2 provides source-generator-based MVVM (ObservableProperty, RelayCommand) that works on both net48 and net8.0-windows, provided the project uses SDK-style format and PackageReference -- PolySharp 1.15.0 fills in missing runtime types on .NET Framework 4.8. The installer uses WiX v5.0.2, replacing WiX v4 stated in PROJECT.md (v4 is end-of-life as of February 2025).

**Core technologies:**
- C# 12 / .NET multi-target (net48 + net8.0-windows): Required by the Revit 2024/2025-2026 runtime split -- not optional
- Nice3point.Revit.Api.RevitAPI + RevitAPIUI (2024.4.0 / 2026.4.0): NuGet-distributed Revit API assemblies, CopyLocal=false -- reproducible builds without Revit installed
- Nice3point.Revit.Toolkit (2024.x / 2026.x): Base classes for ExternalCommand, typed ExternalEventHandler, AsyncExternalEvent -- eliminates boilerplate
- CommunityToolkit.Mvvm 8.4.2: Source-generator MVVM, netstandard2.0, works on both runtimes -- industry standard
- PolySharp 1.15.0: Source-only polyfills enabling C# 12 features on net48 -- mandatory for the multi-target approach
- System.Text.Json 8.0.5 (net48 via NuGet, built-in on net8): Preset JSON persistence -- zero external dependency on .NET 8
- WiX Toolset SDK v5.0.2: MSI installer with per-version feature selection -- v4 is EOL, v5 is the safe choice
- WPF (built-in): UI framework -- native to both net48 and net8.0-windows, no third-party UI library needed

**Critical corrections to PROJECT.md assumptions:**
- .NET Framework 4.8 only is wrong -- must multi-target net48 AND net8.0-windows
- WiX v4 is EOL -- use WiX v5.0.2
- Shared Project plus 3 target projects is the legacy pattern -- single SDK-style multi-target csproj is simpler and the modern standard (validate WPF XAML resolution across framework families before committing)
- packages.config is incompatible with CommunityToolkit.Mvvm source generators on net48 -- SDK-style PackageReference is required

### Expected Features

The core UX differentiator is the unified 3-panel layout: no competitor combines a family/type TreeView (left), CompoundStructure layer/parameter editor (center), and persistent preset palette (right) in a single modeless window. No existing tool occupies this exact market niche.

**Must have (table stakes):**
- List all materials in document with search -- baseline for any material tool
- Material color swatch display -- primary visual identifier
- CompoundStructure layer listing for walls/floors/roofs/ceilings -- primary use case
- Apply material to CompoundStructure layer (Set Mat) -- core action
- Material duplication -- needed for variant workflows
- Undo/Redo via Revit Transactions -- users expect Ctrl+Z to work
- Multi-version Revit support (2024/2025/2026) -- professional add-in standard
- Persistent preset JSON library -- users must not lose work between sessions

**Should have (differentiators):**
- Unified 3-panel visual editor (TreeView + layers + presets) -- the core UX differentiator
- Scene/subset management -- novel concept; allows working on facade study vs interior finishes subsets
- Preset groups (Murs/Sols/Autres + custom) -- organized by architectural intent, not alphabetically
- Loaded family material parameter support -- most tools ignore non-system families
- Live material editing (name, description, color, surface pattern, tint) -- reduces round-trips to native Material Browser
- Appearance tint editing via AppearanceAssetEditScope -- native browser requires 4+ clicks
- 3D view pick to add types to scene -- faster than browsing lists
- Material preview (color + pattern swatch, not realistic sphere) -- visual feedback in preset panel
- Multi-selection batch apply -- apply one material to multiple selected layers at once

**Defer (v2+):**
- Realistic material sphere render -- no reliable Revit API; colored swatch is acceptable
- Cloud material library -- BIMsmith dominates; adds auth/network complexity for no advantage
- Full PBR texture/map editing -- Architextures owns this; complex and brittle API
- Material creation from scratch -- 19+ asset schemas, high complexity, low frequency
- Scene persistence across sessions -- type references may become invalid; session-only is correct
- Revit < 2024 support -- different API surfaces, not worth the compatibility burden
- Structural/Thermal asset editing -- wrong persona (engineers, not architects)

### Architecture Approach

The architecture is a modeless WPF window (shown via IExternalCommand.Execute) with a strict DTO boundary between the Revit API layer and the ViewModels. A single IExternalEventHandler (RevitEventBridge) with enum-based action dispatch handles all Revit API calls from the modeless UI. ViewModels communicate with RevitEventBridge by setting a RequestType enum and payload, calling ExternalEvent.Raise(), and receiving results back via Dispatcher callback with plain DTOs. The ExternalEvent is created in IExternalApplication.OnStartup and persists for the entire Revit session. The window is a singleton (created once, shown/hidden) to prevent memory leaks from repeated open/close cycles.

**Major components:**
1. App (IExternalApplication) -- registers ribbon, creates ExternalEvent plus RevitEventBridge, manages singleton window lifecycle
2. RevitEventBridge (IExternalEventHandler) -- single handler with enum dispatch; the ONLY component that calls Revit API from UI-initiated actions
3. MainViewModel -- orchestrates all three panels, holds scene state, routes RelayCommands through RevitEventBridge
4. FamilyTreeViewModel -- left panel TreeView of families/types per active scene
5. LayerParameterViewModel -- center panel: CompoundStructure layers OR material parameters per selected type (DataTemplateSelector)
6. PresetViewModel -- right panel: preset groups, material cards, drag source
7. MaterialEditorViewModel -- inline material property editing (name, color, pattern, tint)
8. SceneService -- in-memory scene management (session-only, no persistence by design)
9. PresetService -- JSON serialization/deserialization of PresetStore to user-chosen file path
10. RevitDataService -- reads Revit model data inside ExternalEventHandler context only
11. MaterialMapper -- maps Revit Material to MaterialDto (plain POCO, no Revit API types)

**Key patterns:**
- DTO Boundary Layer: all data crossing Revit/ViewModel boundary is plain DTOs; ElementId stored as long
- Single-Handler Enum Dispatch: one ExternalEvent, one handler, enum-based routing
- Transaction Grouping: related changes in one named Transaction (French names) for clean undo stack
- Singleton Window plus IDisposable ViewModels: prevent session-long memory leaks
- Get-Modify-Set for CompoundStructure: always call SetCompoundStructure() after modifying layers

### Critical Pitfalls

1. Calling Revit API from the WPF UI thread -- Every Revit API call must go through IExternalEventHandler. ViewModels must never import Autodesk.Revit.DB. Static analysis rule: no Revit namespace in ViewModel files.

2. .NET Framework split (net48 vs net8.0-windows) -- Build and load-test against all three Revit versions before writing any feature code. A wrong TargetFramework produces a DLL that silently fails to load with no user-visible error.

3. CompoundStructure is a copy, not a reference -- GetCompoundStructure() returns a copy. Always call SetCompoundStructure(cs) after modifications. Without this, changes silently evaporate. This is the most common Revit add-in bug.

4. AppearanceAssetEditScope requires an open Transaction before Commit() -- Wrap the entire flow: open Transaction, scope.Start(), modify, scope.Commit(true), then Transaction.Commit(). Order matters.

5. ElementId int64 breaking change -- Revit 2024+ uses 64-bit ElementId. Never use .IntegerValue (deprecated) or new ElementId(int) (removed in Revit 2026). Always use ElementId.Value (returns long) and new ElementId(long).

6. CopyLocal=True on Revit API references -- Set Private=False on all Revit API DLL references. If RevitAPI.dll appears in the build output directory, the configuration is wrong.

7. WPF window memory leaks -- Use singleton window pattern. Implement IDisposable on ViewModels. Unsubscribe all Revit event handlers in Window.Closed. Repeated open/close cycles without disposal fill Revit process memory over a session.

---

## Implications for Roadmap

Based on research, suggested phase structure (5 phases):

### Phase 1: Foundation and Project Infrastructure
**Rationale:** The .NET split, ExternalEvent pattern, CopyLocal discipline, ElementId int64 usage, and singleton window lifecycle must all be correct before writing a single feature. Every subsequent phase depends on this being solid. Getting it wrong requires a rewrite.
**Delivers:** Multi-targeting build loading in all 3 Revit versions; ribbon button; skeleton modeless singleton window with proper show/hide/dispose; ExternalEvent bridge skeleton; DTO models; WPF Olympe dark theme ResourceDictionary; PresetService (pure .NET, no Revit dependency).
**Addresses:** Table stakes: multi-version support, undo/redo infrastructure, persistent preset foundation.
**Avoids:** Pitfalls 1 (API thread), 2 (.NET split), 5 (ElementId int64), 6 (CopyLocal), 7 (window lifecycle).

### Phase 2: Read Path -- Scene, TreeView, and Layer Display
**Rationale:** You must display data before you can modify it. The read path establishes the full ExternalEvent data flow under real conditions with zero risk of model corruption. Scene management belongs here because the TreeView is meaningless without it.
**Delivers:** Left panel (TreeView of families/types per active scene), center panel (CompoundStructure layers and material parameters displayed), scene creation/switching, RevitDataService, MaterialMapper, FilteredElementCollector with quick filters.
**Uses:** FilteredElementCollector.OfClass() quick filter, MaterialMapper DTOs, DataTemplateSelector for compound vs. non-compound types.
**Avoids:** Pitfall 3 (CompoundStructure copy in read path), Pitfall 11 (Revit 2026 optional core layer), Pitfall 14 (MaterialId=-1 displayed as By Category), Pitfall 10 (FilteredElementCollector performance).

### Phase 3: Preset Panel and Write Path -- Set Mat Core Action
**Rationale:** This is the primary value proposition. After read is working, implement the right panel and the Set Mat write operation. This is the first phase where Revit model data is modified, so Transaction discipline and CompoundStructure Get-Modify-Set correctness are exercised for real.
**Delivers:** Right panel preset palette with groups (Murs/Sols/Autres), material card display with color swatch, Set Mat for CompoundStructure layers, Set Mat for loaded family material parameters, material duplication, JSON preset load/save with defensive path validation.
**Uses:** Transaction with descriptive French names, TransactionGroup for multi-step operations, System.Text.Json, AppearanceAssetEditScope (tint).
**Avoids:** Pitfall 3 (CompoundStructure Get-Modify-Set), Pitfall 4 (AppearanceAssetEditScope + Transaction lifecycle), Pitfall 16 (JSON path validation), Pitfall 18 (transaction naming).

### Phase 4: Material Editing, Multi-Selection, and 3D Pick
**Rationale:** Differentiating features built on a working core. Live material editing via AppearanceAssetEditScope should be deferred until Phase 3 validates that pattern. Multi-selection and 3D pick require the ExternalEvent pattern to be proven stable.
**Delivers:** Live material property editing (name, description, color, surface pattern, tint) from preset panel; multi-selection of layers/parameters for batch assignment; 3D view PickObject to add types to scene; graceful PickObject cancellation (Autodesk.Revit.Exceptions.OperationCanceledException).
**Uses:** AppearanceAssetEditScope, ISelectionFilter, UIDocument.Selection.PickObject().
**Avoids:** Pitfall 9 (PickObject cancellation), Pitfall 15 (connected asset texture access), Pitfall 4 (appearance edit scope lifecycle).

### Phase 5: Performance, Polish, and Installer
**Rationale:** Non-feature work that enables distribution. WPF virtualization and cache invalidation ensure usability in real-world projects with 1000+ materials. The WiX v5 installer handles multi-version deployment correctly.
**Delivers:** ListView virtualization (VirtualizingStackPanel.IsVirtualizing=True), material collection caching with DocumentChanged invalidation, WiX v5 MSI installer with version selection UI (2024/2025/2026 checkboxes), per-version .addin manifest deployment to correct %APPDATA% folders, Revit 2026 dependency isolation verification.
**Avoids:** Pitfall 8 (WiX v5 paths and per-version components), Pitfall 10 (large library performance), Pitfall 17 (Revit 2026 dependency isolation).

### Phase Ordering Rationale

- Foundation first: the .NET split validation and ExternalEvent skeleton are preconditions for everything. Discovering a build or load problem in Phase 3 is vastly more expensive than in Phase 1.
- Read before write: the DTO pipeline and ExternalEvent round-trip must be proven correct in a low-stakes context before modifying the Revit model.
- Core write path before differentiating features: Set Mat is the stated primary value proposition. Live material editing and 3D pick are enhancements.
- Performance and installer last: requirements become clearer once the feature surface is finalized; they do not block feature development.
- PresetService in Phase 1 (not Phase 3): it has zero Revit dependency and can be developed and tested in isolation while Revit integration is being validated.

### Research Flags

Phases likely needing deeper research during planning:
- Phase 1: Multi-target csproj with WPF XAML -- ARCHITECTURE.md and STACK.md reached opposite conclusions (Shared Project vs. single csproj). A 1-day build spike before roadmap execution is recommended to validate WPF XAML resource resolution across net48/net8.0-windows in a single project.
- Phase 4: AppearanceAssetEditScope connected asset traversal for tint -- the exact property chain for generic_diffuse varies by material schema. If tint editing is included, a focused API spike is needed before committing.

Phases with well-documented patterns (skip research-phase):
- Phase 2: FilteredElementCollector, CompoundStructure reading, modeless WPF with ExternalEvent -- extensively documented in The Building Coder, Autodesk SDK samples, and community add-ins.
- Phase 3: CompoundStructure Get-Modify-Set, Transaction patterns, System.Text.Json -- canonical patterns with official documentation.
- Phase 5: WiX v5 installer -- Autodesk open-source IFC installer provides a working WiX reference for Revit add-in deployment.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | .NET split confirmed via official Autodesk migration docs. Nice3point confirmed via NuGet metadata and active maintenance. WiX v4 EOL confirmed via FireGiant (Feb 2025). CommunityToolkit.Mvvm net48 confirmed via resolved GitHub issue #695. |
| Features | MEDIUM-HIGH | Competitor analysis is current. Revit API capabilities confirmed via official docs. Feature prioritization involves judgment from community workflow discussions. |
| Architecture | HIGH | ExternalEvent pattern, DTO boundary, CompoundStructure Get-Modify-Set are canonical patterns. One unresolved: single multi-target csproj vs. Shared Project for WPF XAML -- needs a build spike. |
| Pitfalls | HIGH | All critical pitfalls sourced from official Autodesk docs, The Building Coder, and Autodesk community forums with verified resolutions. ElementId int64 change confirmed via Revit 2024/2026 API docs. |

**Overall confidence:** HIGH

### Gaps to Address

- Single csproj vs Shared Project: STACK.md recommends single multi-target SDK-style csproj; ARCHITECTURE.md recommends Shared Project. Resolve with a build spike: create both, verify WPF XAML designer works, then commit. Single csproj is simpler if XAML compiles; Shared Project is the established fallback.

- ElementId API per version: Use ElementId.Value (long) everywhere from day one. Never use IntegerValue or new ElementId(int). This eliminates the uncertainty entirely.

- CommunityToolkit.Mvvm source generators in Shared Projects: Works confirmed in standard SDK-style projects. If the Shared Project approach is chosen, validate source generators early in Phase 1 before building all ViewModels on the assumption they work.

- Revit 2026 dependency isolation (UseRevitContext): Default is backward-compatible (true). Test the add-in in Revit 2026 before the Phase 5 installer milestone to confirm CommunityToolkit.Mvvm and System.Text.Json load correctly.

---

## Sources

### Primary (HIGH confidence)
- Autodesk .NET 8 Migration Guide -- runtime split, net48/net8.0-windows requirements per Revit version
- Autodesk Revit API 2026 changelog -- CompoundStructure core layer optional, ElementId changes, dependency isolation
- Autodesk Revit API 2025 Changes -- .NET 8 migration confirmation
- FireGiant WiX v3/v4 EOL announcement (Feb 2025) -- WiX version decision rationale
- WixToolset.Sdk 5.0.2 NuGet -- installer toolchain version confirmation
- Nice3point RevitToolkit GitHub -- ExternalEventHandler typed API
- CommunityToolkit.Mvvm NuGet (netstandard2.0) -- MVVM infrastructure
- GitHub CommunityToolkit/dotnet issue #695 -- net48 source generator resolution confirmed
- Autodesk External Events Documentation -- ExternalEvent/IExternalEventHandler canonical pattern
- Autodesk Add-in Registration / Dependency Isolation (2026) -- .addin manifest requirements

### Secondary (MEDIUM confidence)
- The Building Coder (Jeremy Tammik) -- CompoundStructure layer updates, AppearanceAssetEditScope, modeless WPF patterns, CopyLocal=False, FilteredElementCollector performance, transaction groups, Z-order
- archi-lab.net multi-version guide -- multi-target project structure patterns
- ricaun.com .NET 8 guide and Revit 2024 obsolete APIs -- ElementId int64 change context
- revitapidocs.com / rvtdocs.com -- API surface confirmation per Revit version
- GitHub RevitWPF (varolomer) -- modeless WPF pattern reference implementation
- Autodesk University: Modeless Revit Plug-Ins with WPF -- ExternalEvent architecture patterns
- ModPlus Material Manager -- competitor feature analysis

### Tertiary (LOW confidence -- needs validation)
- AppearanceAssetEditScope connected asset (texture) traversal -- single Autodesk forum thread; needs validation before committing to texture editing
- CommunityToolkit.Mvvm source generators in .shproj Shared Project compilation -- inferred from SDK-style project behavior; not directly confirmed for shared project format

---
*Research completed: 2026-04-11*
*Ready for roadmap: yes*