---
phase: 02-read-path-scene-and-layer-display
verified: 2026-04-11T00:00:00Z
status: human_needed
score: 5/5 must-haves verified
human_verification:
  - test: "Create a named scene, add a wall type, and confirm it appears in the TreeView under a 'Murs' group header"
    expected: "New scene created, family/type comboboxes load Revit families, wall type added appears in TreeView grouped as 'Murs' with 'FamilyName : TypeName' format"
    why_human: "Requires Revit active with a loaded document; TreeView grouping and ComboBox population depend on live Revit API data"
  - test: "Select a WallType in the TreeView and confirm the center panel displays CompoundStructure layers"
    expected: "Center panel shows 'Couches' mode label; list shows each layer as '[Fonction] -- [Epaisseur] mm -- [Materiau]' with French function names (e.g., Noyau, Finition 1)"
    why_human: "Requires Revit with a project containing wall types that have CompoundStructure layers"
  - test: "Select a loaded family type in the TreeView and confirm the center panel displays material parameters"
    expected: "Center panel shows 'Parametres materiaux' mode label; list shows '[Nom parametre] -- [Materiau actuel]'"
    why_human: "Requires Revit with a loaded family that has Material parameters"
  - test: "Multi-select layers with Ctrl+click and Shift+click in the center panel"
    expected: "Multiple layers highlighted simultaneously; both Ctrl+click toggle and Shift+click range selection work"
    why_human: "Multi-selection behavior can only be verified interactively in the running WPF window"
  - test: "Remove a type from a scene via Delete key and right-click context menu 'Supprimer'"
    expected: "Type removed from TreeView immediately; both Delete key and context menu work"
    why_human: "Requires Revit runtime and interactive input to test both removal methods"
---

# Phase 2: Read Path -- Scene and Layer Display Verification Report

**Phase Goal:** Users can create scenes, populate them with types, and inspect CompoundStructure layers or material parameters for any selected type
**Verified:** 2026-04-11
**Status:** human_needed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths (from ROADMAP.md Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can create a named scene, switch between scenes, and see types organized in a TreeView with Murs/Sols sorted first | VERIFIED | `LeftPanelViewModel.CreerScene()` creates SceneDto and sets ActiveScene; ComboBox bound to `Scenes` with `SelectedItem=ActiveScene`; TreeView uses `CategorySortComparer` (Murs=0, Sols=1, rest=100) and `PropertyGroupDescription("CategoryName")` |
| 2 | User can add types to a scene via dropdown (famille then type) and remove types from the scene | VERIFIED | `AjouterType()`/`SupprimerType()` commands wired; family ComboBox triggers `OnSelectedFamilyChanged` which calls `GetTypeList` via EventBridge; Delete key binding and context menu "Supprimer" both wired to `SupprimerTypeCommand` |
| 3 | Selecting a wall/floor/roof/ceiling type displays CompoundStructure layers with function, thickness, and current material | VERIFIED | `CenterPanelViewModel.OnTypeSelected()` dispatches to `FetchLayers()` when `HasCompoundStructure=true`; handler reads `CompoundStructure.GetLayers()`, converts width via `UnitUtils.ConvertFromInternalUnits(...UnitTypeId.Millimeters)`, maps function via `LayerFunctionMapper.ToFrench()`; XAML renders `[Function] -- [Width:F1] mm -- [MaterialName]` |
| 4 | Selecting a loaded family type without layers displays its material parameters | VERIFIED | `FetchMaterialParameters()` called when `HasCompoundStructure=false`; handler iterates `element.Parameters`, filters by `StorageType.ElementId` and `SpecTypeId.Reference.Material`; XAML renders `[ParameterName] -- [CurrentMaterialName]` |
| 5 | User can select one or multiple layers/parameters in the center panel (Ctrl+click, Shift+click) | VERIFIED | Both ListBoxes use `SelectionMode="Extended"`; `SelectionChangedCommand` collects `SelectedItems` via `InvokeCommandAction`; `SelectedItems` exposed as `IList?` on `CenterPanelViewModel` for Phase 3 consumption |

**Score:** 5/5 truths verified (automated checks)

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Models/SceneDto.cs` | Scene data model | VERIFIED | POCO with Name, ObservableCollection<SceneTypeDto>; no Revit types |
| `Models/SceneTypeDto.cs` | Type entry DTO | VERIFIED | ElementIdValue (long), FamilyName, TypeName, CategoryName, HasCompoundStructure |
| `Models/FamilyCategoryDto.cs` | Family grouped by category | VERIFIED | CategoryName, FamilyName, FamilyElementIdValue (long), BuiltInCategoryValue, IsSystemFamily |
| `Models/LayerDto.cs` | CompoundStructure layer DTO | VERIFIED | LayerIndex, Function (French), Width (mm, double), MaterialName, MaterialElementIdValue |
| `Models/MaterialParamDto.cs` | Material parameter DTO | VERIFIED | ParameterName, CurrentMaterialName, CurrentMaterialIdValue, ParameterDefinitionName |
| `Models/GetTypeListRequestDto.cs` | Request DTO for type list | VERIFIED | IsSystemFamily, BuiltInCategoryValue, FamilyElementIdValue -- discriminates system vs loaded families |
| `Messages/TypeSelectedMessage.cs` | LeftPanel->CenterPanel Messenger message | VERIFIED | ValueChangedMessage<SceneTypeDto?> from CommunityToolkit.Mvvm.Messaging |
| `Helpers/CategorySortComparer.cs` | IComparer: Murs first, Sols second | VERIFIED | Murs=0, Sols=1 in priority dict; TryGetValue pattern (net48 compatible) |
| `Helpers/LayerFunctionMapper.cs` | French layer function names | VERIFIED | All 7 MaterialFunctionAssignment values mapped; Insulation -> "Isolation thermique / Air", Structure -> "Noyau", Membrane -> "Membrane" (corrected from PLAN's MembraneLayer/ThermalOrAir) |
| `Converters/BoolToVisibilityConverter.cs` | Bool to Visibility converter | VERIFIED | Standard IValueConverter; true=Visible, false=Collapsed |
| `Events/RevitRequestType.cs` | Extended with 4 new enum values | VERIFIED | GetFamilyList, GetTypeList, GetLayersForType, GetMaterialParametersForType all present |
| `Events/RevitEventBridge.cs` | Extended with 4 new handlers | VERIFIED | All 4 handlers implemented with real Revit API calls; FilteredElementCollector, CompoundStructure.GetLayers(), SpecTypeId.Reference.Material |
| `ViewModels/LeftPanelViewModel.cs` | Full scene management VM | VERIFIED | CreerScene, AjouterType, SupprimerType, ChargerFamilles commands; TypeSelectedMessage sent on selection; SetupCustomSort configures CategorySortComparer + PropertyGroupDescription |
| `ViewModels/CenterPanelViewModel.cs` | Conditional layer/param display VM | VERIFIED | TypeSelectedMessage received via WeakReferenceMessenger; FetchLayers/FetchMaterialParameters; ShowLayers/ShowParameters/ShowPlaceholder flags; SelectionChangedCommand |
| `ViewModels/MainWindowViewModel.cs` | Wires eventBridge to both VMs | VERIFIED | `LeftPanelVM = new LeftPanelViewModel(eventBridge)` and `CenterPanelVM = new CenterPanelViewModel(eventBridge)` |
| `Views/LeftPanelView.xaml` | Scene creation, selector, TreeView, ComboBoxes | VERIFIED | TextBox + "Creer" button; Scenes ComboBox; family/type ComboBoxes with loading indicators; TreeView with GroupStyle (category headers), ItemTemplate (FamilyName : TypeName), EventTrigger for selection, Delete key binding, context menu |
| `Views/CenterPanelView.xaml` | Conditional layer/param ListBoxes | VERIFIED | Two ListBoxes with `SelectionMode="Extended"`; ShowLayers/ShowParameters visibility via BoolToVisibilityConverter; DataTemplates with function/width/material and parameter/material formats; EventTrigger for multi-selection |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `LeftPanelViewModel.OnSelectedTypeChanged` | `CenterPanelViewModel.OnTypeSelected` | `WeakReferenceMessenger.Default.Send(new TypeSelectedMessage(value))` / `Register<TypeSelectedMessage>` | WIRED | Send in LeftPanel line 283; Register in CenterPanel constructor line 63-66 |
| `LeftPanelView.xaml TreeView` | `LeftPanelViewModel.TreeViewSelectionChangedCommand` | `EventTrigger(SelectedItemChanged) -> InvokeCommandAction` | WIRED | Microsoft.Xaml.Behaviors.Wpf used correctly |
| `LeftPanelViewModel.ChargerFamilles` | `RevitEventBridge.HandleGetFamilyList` | `MakeRequest(GetFamilyList)` -> `ProcessRequest switch` | WIRED | Enum value matched in switch statement |
| `LeftPanelViewModel.OnSelectedFamilyChanged` | `RevitEventBridge.HandleGetTypeList` | `MakeRequest(GetTypeList, GetTypeListRequestDto)` | WIRED | DTO cast `(GetTypeListRequestDto)data!` in bridge |
| `CenterPanelViewModel.FetchLayers` | `RevitEventBridge.HandleGetLayersForType` | `MakeRequest(GetLayersForType, typeIdValue)` | WIRED | Long cast `(long)data!` in bridge |
| `CenterPanelViewModel.FetchMaterialParameters` | `RevitEventBridge.HandleGetMaterialParametersForType` | `MakeRequest(GetMaterialParametersForType, typeIdValue)` | WIRED | Long cast `(long)data!` in bridge |
| `MainWindow.xaml LeftPanelView` | `MainWindowViewModel.LeftPanelVM` | `DataContext="{Binding LeftPanelVM}"` | WIRED | Grid Column=0 |
| `MainWindow.xaml CenterPanelView` | `MainWindowViewModel.CenterPanelVM` | `DataContext="{Binding CenterPanelVM}"` | WIRED | Grid Column=2 |
| `App.OnStartup` | `RevitEventBridge.ProcessRequest` | `ToolkitExternalEvent(uiApp => EventBridge.ProcessRequest(uiApp))` | WIRED | App.cs lines 30-33 |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `CenterPanelView.xaml` Layers ListBox | `Layers` (ObservableCollection<LayerDto>) | `HandleGetLayersForType` -> `CompoundStructure.GetLayers()` -> `UnitUtils.ConvertFromInternalUnits` | Yes -- real Revit DB query | FLOWING |
| `CenterPanelView.xaml` MaterialParams ListBox | `MaterialParams` (ObservableCollection<MaterialParamDto>) | `HandleGetMaterialParametersForType` -> `element.Parameters` iteration filtering by `SpecTypeId.Reference.Material` | Yes -- real Revit API parameter iteration | FLOWING |
| `LeftPanelView.xaml` TreeView | `ActiveSceneTypes` (ObservableCollection<SceneTypeDto>) | User-driven: `AjouterType()` adds items from `FamilyTypes` which are loaded via `HandleGetTypeList` from Revit FilteredElementCollector | Yes -- types sourced from Revit DB | FLOWING |
| `LeftPanelView.xaml` Families ComboBox | `Families` (ObservableCollection<FamilyCategoryDto>) | `HandleGetFamilyList` -> FilteredElementCollector for WallType, FloorType, RoofType, CeilingType + Family collector | Yes -- real Revit DB query | FLOWING |

---

### Behavioral Spot-Checks

Step 7b: SKIPPED (no runnable entry points without Revit host; this is a Revit add-in, not a standalone executable)

---

### Build Verification

| Check | Result | Status |
|-------|--------|--------|
| `dotnet build` on both TFMs (net48, net8.0-windows) | 0 errors, 0 warnings | PASS |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| SCENE-01 | 02-01, 02-02 | Creer une scene avec nom personnalise | SATISFIED | `CreerScene()` command creates SceneDto with trimmed name; wired to TextBox + Button in XAML |
| SCENE-02 | 02-02 | Switcher entre plusieurs scenes via selecteur | SATISFIED | ComboBox bound to `Scenes` with `SelectedItem=ActiveScene`; `OnActiveSceneChanged` triggers sort setup |
| SCENE-03 | 02-01, 02-02 | Ajouter familles/types via liste (dropdown famille puis type) | SATISFIED | Family ComboBox -> `OnSelectedFamilyChanged` -> GetTypeList; Type ComboBox -> `AjouterTypeCommand` |
| SCENE-05 | 02-02 | Retirer un type de la scene active | SATISFIED | `SupprimerTypeCommand`; Delete key binding; context menu "Supprimer" via Tag proxy pattern |
| SCENE-06 | 02-01, 02-02 | TreeView des familles/types | SATISFIED | TreeView with ItemsSource=ActiveSceneTypes, ItemTemplate showing FamilyName : TypeName |
| SCENE-07 | 02-01, 02-02 | TreeView trie Murs/Sols en tete | SATISFIED | `CategorySortComparer` with Murs=0, Sols=1 priorities; `SetupCustomSort()` sets ListCollectionView.CustomSort |
| SCENE-08 | 02-02 | Selection d'un type met a jour le panneau centre | SATISFIED | TreeView SelectedItemChanged -> TreeViewSelectionChangedCommand -> OnSelectedTypeChanged -> TypeSelectedMessage -> CenterPanelViewModel.OnTypeSelected |
| LAYER-01 | 02-01, 02-03 | Panneau centre affiche couches CompoundStructure | SATISFIED | ShowLayers=true when HasCompoundStructure; Layers ListBox renders data from HandleGetLayersForType |
| LAYER-02 | 02-01, 02-03 | Chaque couche affiche fonction, epaisseur, materiau | SATISFIED | DataTemplate: Function (French) + Width (mm, F1 format) + MaterialName; "< Par categorie >" fallback for InvalidElementId |
| LAYER-03 | 02-01, 02-03 | Panneau centre affiche parametres Material pour familles sans couches | SATISFIED | ShowParameters=true when !HasCompoundStructure; MaterialParams ListBox renders ParameterName + CurrentMaterialName |
| LAYER-04 | 02-03 | Selectionner une ou plusieurs couches/parametres | SATISFIED | SelectionMode="Extended" on both ListBoxes; SelectedItems exposed on CenterPanelViewModel |
| LAYER-05 | 02-03 | Multi-selection Ctrl+clic, Shift+clic | SATISFIED (code) | SelectionMode=Extended provides native Ctrl+click and Shift+click; needs human verification at runtime |

**All 12 required requirements (SCENE-01, 02, 03, 05, 06, 07, 08, LAYER-01, 02, 03, 04, 05) are satisfied by implementation evidence.**

Note: SCENE-04 (3D pick) and SCENE-09 (3D view validation) are Phase 4 requirements -- correctly absent from this phase.

---

### Anti-Patterns Found

| File | Pattern | Severity | Assessment |
|------|---------|----------|------------|
| `Views/RightPanelView.xaml` | "Aucun preset configure" placeholder text | INFO | Expected -- right panel is Phase 3 scope; correctly deferred |
| `CenterPanelViewModel.cs` line 38 | `_showPlaceholder = true` initial value | INFO | Intentional UX state (no type selected yet); not a stub -- replaced with real data when type selected |

No blocker or warning anti-patterns found. No TODO/FIXME/XXX comments. No empty return {} or null implementations in Phase 2 code.

---

### Human Verification Required

#### 1. Scene creation and TreeView display

**Test:** Launch Revit, open a project, open the MaterialManager window. Type a scene name (e.g. "Test") and click "Creer". Observe the scene appears in the scene selector ComboBox and the "Ajouter un type" section becomes visible.
**Expected:** Scene "Test" appears in ComboBox and is selected. Add-type section visible. Family ComboBox loads when a scene is active.
**Why human:** Requires Revit host with active document; TreeView grouping only verifiable with live Revit data.

#### 2. Type add via dropdowns and TreeView display with Murs/Sols priority

**Test:** Select a wall category in the family ComboBox, select a wall type in the type ComboBox, click "Ajouter". Then add a floor type. Observe the TreeView.
**Expected:** Both types appear in the TreeView grouped by category (Murs group before Sols group). Items display as "FamilyName : TypeName".
**Why human:** Sort order and group header rendering require live WPF TreeView with CollectionViewSource grouping.

#### 3. CompoundStructure layer display with French function names

**Test:** Click on a wall type in the TreeView.
**Expected:** Center panel shows mode label "Couches", then a list of layers formatted as "[Fonction FR] -- [X.X] mm -- [Materiau]". Verify function names are in French (e.g., "Noyau", "Finition 1", "Substrat").
**Why human:** Requires Revit document with wall types; French mapping correctness only verifiable against real wall data.

#### 4. Material parameter display for loaded families

**Test:** Click on a loaded family type (e.g. a door or furniture family) in the TreeView.
**Expected:** Center panel shows mode label "Parametres materiaux", then a list of material parameters formatted as "[Nom parametre] -- [Materiau actuel]".
**Why human:** Requires a project with loaded families that have Material-type parameters.

#### 5. Multi-selection (Ctrl+click, Shift+click)

**Test:** With layers visible, Ctrl+click two non-adjacent layers. Then Shift+click to extend the range.
**Expected:** Multiple layers highlighted simultaneously. Both selection modes work independently.
**Why human:** Multi-selection interaction requires live WPF window.

---

### Summary

Phase 2 goal is fully achieved in code. All 17 artifacts exist, are substantive (real implementations, not stubs), and are correctly wired into the data flow. The build compiles clean (0 errors, 0 warnings) on both net48 and net8.0-windows targets. All 12 required requirements have implementation evidence traceable to specific files and code patterns.

The only items requiring verification are interactive Revit runtime behaviors (TreeView rendering with live data, French function name display, multi-selection feel) which cannot be verified programmatically without a running Revit host. These are flagged for human verification before the phase is considered fully closed.

Key technical decisions verified correct:
- `MaterialFunctionAssignment.Membrane` (not `MembraneLayer`) and `Insulation` (not `ThermalOrAir`) -- correct API enum names
- `TryGetValue` pattern instead of `GetValueOrDefault` in CategorySortComparer -- net48 compatible
- `SpecTypeId.Reference.Material` for language-independent material parameter detection
- Tag proxy pattern for ContextMenu command binding across WPF visual tree boundary

---

_Verified: 2026-04-11_
_Verifier: Claude (gsd-verifier)_
