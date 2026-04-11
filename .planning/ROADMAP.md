# Roadmap: Olympe MaterialManager

## Overview

Olympe MaterialManager delivers a 3-panel modeless WPF add-in for Revit architects to visually manage materials across CompoundStructure layers and loaded families. The roadmap progresses from build infrastructure validation (Phase 1) through read-only display (Phase 2), the core Set Mat write path (Phase 3), differentiating power features (Phase 4), and finally polish with installer packaging (Phase 5). Each phase delivers a coherent, verifiable capability and builds on proven foundations from prior phases. The critical constraint is validating the .NET multi-target build (net48 + net8.0-windows) before any feature work.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Foundation and Infrastructure** - Multi-target build, ExternalEvent skeleton, singleton themed window with 3-panel layout shell
- [ ] **Phase 2: Read Path -- Scene and Layer Display** - Scene management, TreeView, CompoundStructure layers and material parameters display
- [ ] **Phase 3: Preset Panel and Set Mat** - Preset palette with JSON persistence, core Set Mat write operation for layers and parameters
- [ ] **Phase 4: Material Editing and 3D Pick** - Live material property editing, material preview, 3D view pick-to-add
- [ ] **Phase 5: Polish and Installer** - Control styling consistency, WiX v5 installer with multi-version deployment

## Phase Details

### Phase 1: Foundation and Infrastructure
**Goal**: The add-in loads and displays a themed modeless window in Revit 2024, 2025, and 2026 with the correct architecture patterns validated
**Depends on**: Nothing (first phase)
**Requirements**: INFRA-01, INFRA-02, INFRA-03, INFRA-04, INFRA-05, INFRA-06, INFRA-07, INFRA-08, INFRA-09, UI-01, UI-02, UI-04
**Success Criteria** (what must be TRUE):
  1. The add-in loads without error in Revit 2024, 2025, and 2026 via ribbon button
  2. A modeless singleton window opens with a dark Olympe theme (fond sombre, accent ambre/orange) and three-column layout shell
  3. The window persists across show/hide cycles without memory leaks or duplicate instances
  4. A round-trip ExternalEvent request from the UI triggers a handler callback and returns a DTO to the ViewModel (skeleton proof)
  5. All UI labels and text are in French
**Plans**: 3 plans

Plans:
- [x] 01-01-PLAN.md -- Solution structure, multi-target csproj build spike, .addin registration files
- [x] 01-02-PLAN.md -- Olympe dark theme, MainWindow 3-column layout, panel UserControls, ViewModels (shell)
- [x] 01-03-PLAN.md -- Revit entry points, ExternalEvent bridge, DTOs, round-trip proof, human verification
**UI hint**: yes

### Phase 2: Read Path -- Scene and Layer Display
**Goal**: Users can create scenes, populate them with types, and inspect CompoundStructure layers or material parameters for any selected type
**Depends on**: Phase 1
**Requirements**: SCENE-01, SCENE-02, SCENE-03, SCENE-05, SCENE-06, SCENE-07, SCENE-08, LAYER-01, LAYER-02, LAYER-03, LAYER-04, LAYER-05
**Success Criteria** (what must be TRUE):
  1. User can create a named scene, switch between scenes, and see types organized in a TreeView with Murs/Sols sorted first
  2. User can add types to a scene via dropdown (famille then type) and remove types from the scene
  3. Selecting a wall/floor/roof/ceiling type in the TreeView displays its CompoundStructure layers with function, thickness, and current material
  4. Selecting a loaded family type without layers displays its material parameters
  5. User can select one or multiple layers/parameters in the center panel (Ctrl+click, Shift+click)
**Plans**: 3 plans

Plans:
- [ ] 02-01-PLAN.md -- DTOs, helpers, Messenger message, and RevitEventBridge handlers (data foundation)
- [ ] 02-02-PLAN.md -- Left panel: scene management, TreeView with grouping, type add/remove via ComboBoxes
- [ ] 02-03-PLAN.md -- Center panel: conditional layer/parameter display, multi-selection, human verification
**UI hint**: yes

### Phase 3: Preset Panel and Set Mat
**Goal**: Users can manage a persistent preset palette and apply materials to selected layers or parameters in one click
**Depends on**: Phase 2
**Requirements**: PRESET-01, PRESET-02, PRESET-03, PRESET-04, PRESET-05, PRESET-06, PRESET-07, PRESET-08, PRESET-09, PRESET-10, UI-05
**Success Criteria** (what must be TRUE):
  1. The right panel displays material presets organized in groups (Murs, Sols, Autres, and user-created groups)
  2. User can add project materials to preset groups, duplicate presets, and the data persists to a user-chosen JSON file path remembered across sessions
  3. Clicking Set Mat applies the selected preset material to all selected CompoundStructure layers via a Revit Transaction
  4. For loaded families without layers, Set Mat lets the user choose which material parameter to modify
  5. Set Mat handles errors with rollback and displays a clear message to the user
**Plans**: TBD

Plans:
- [ ] 03-01: TBD
- [ ] 03-02: TBD
- [ ] 03-03: TBD
**UI hint**: yes

### Phase 4: Material Editing and 3D Pick
**Goal**: Users can edit material properties live and add types to scenes by clicking elements in the 3D view
**Depends on**: Phase 3
**Requirements**: MATEDIT-01, MATEDIT-02, MATEDIT-03, MATEDIT-04, MATEDIT-05, MATEDIT-06, MATEDIT-07, MATEDIT-08, SCENE-04, SCENE-09
**Success Criteria** (what must be TRUE):
  1. The material visualizer displays name, description, surface pattern/color, appearance tint, and a preview thumbnail (or colored fallback)
  2. User can edit material name, description, surface pattern/color, and appearance tint live with changes reflected in Revit immediately
  3. The preview refreshes after each material modification
  4. User can add types to the active scene by clicking an element in the 3D view (with 3D view validation and graceful cancellation)
  5. Materials without an AppearanceAsset display a graceful fallback (tint section disabled, not an error)
**Plans**: TBD

Plans:
- [ ] 04-01: TBD
- [ ] 04-02: TBD
- [ ] 04-03: TBD
**UI hint**: yes

### Phase 5: Polish and Installer
**Goal**: The add-in is visually polished with consistent control styling and packaged as an installer for distribution
**Depends on**: Phase 4
**Requirements**: UI-03, DEPLOY-01, DEPLOY-02, DEPLOY-03, DEPLOY-04, DEPLOY-05
**Success Criteria** (what must be TRUE):
  1. All WPF controls (buttons, lists, TreeView, scrollbars) are styled consistently with the Olympe dark theme
  2. An .exe installer is generated that detects installed Revit versions and lets the user choose which to target
  3. After installation, the add-in loads correctly in each selected Revit version (assemblies and .addin file in correct folders)
  4. The installer works on both Windows 10 and Windows 11
**Plans**: TBD

Plans:
- [ ] 05-01: TBD
- [ ] 05-02: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation and Infrastructure | 3/3 | Complete | 2026-04-11 |
| 2. Read Path -- Scene and Layer Display | 0/3 | Planned | - |
| 3. Preset Panel and Set Mat | 0/3 | Not started | - |
| 4. Material Editing and 3D Pick | 0/3 | Not started | - |
| 5. Polish and Installer | 0/2 | Not started | - |
