---
phase: 03-preset-panel-and-set-mat
verified: 2026-04-11T12:00:00Z
status: passed
score: 5/5 must-haves verified
re_verification: false
gaps: []
human_verification:
  - test: "Right panel preset groups display and expand/collapse in Revit"
    expected: "Three default groups (Murs, Sols, Autres) visible with name + count badge, expandable via TreeView"
    why_human: "WPF TreeView rendering and data binding can only be verified at runtime in the Revit host"
  - test: "Add material dialog opens with project materials list"
    expected: "Clicking 'Ajouter au preset' fetches all Revit materials via ExternalEvent and shows a searchable dialog"
    why_human: "Requires a live Revit document with materials; ExternalEvent dispatch cannot be verified without the Revit host"
  - test: "Set Mat applies material to CompoundStructure layers via Revit Transaction"
    expected: "Selecting layers + preset + clicking button modifies Revit type, shows 'Materiau applique !', center panel refreshes"
    why_human: "Requires Revit host process, active document, and live Transaction execution"
  - test: "Set Mat applies material to loaded family parameters"
    expected: "Selecting material parameters + preset + clicking button applies via SetMaterialOnParameter handler"
    why_human: "Requires Revit host with loaded families"
  - test: "JSON persistence: presets survive window close/reopen"
    expected: "Preset file path stored in %APPDATA%/Olympe/MaterialManager/settings.json; reloaded on next open"
    why_human: "Requires running the add-in across two sessions in Revit"
---

# Phase 3: Preset Panel and Set Mat — Verification Report

**Phase Goal:** Users can manage a persistent preset palette and apply materials to selected layers or parameters in one click
**Verified:** 2026-04-11
**Status:** PASSED (automated checks) / Human verification items documented
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths (from ROADMAP.md Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Right panel displays material presets organized in groups (Murs, Sols, Autres, and user-created groups) | VERIFIED | `RightPanelView.xaml` has `HierarchicalDataTemplate` with `ItemsSource="{Binding PresetGroups}"`. `RightPanelViewModel.LoadPresets()` calls `PresetService.GetDefaultCollection()` which produces 3 groups. `CreerGroupeCommand` adds user-defined groups. |
| 2 | User can add project materials to preset groups, duplicate presets, and the data persists to a user-chosen JSON file path remembered across sessions | VERIFIED | `AjouterMateriauCommand` calls `RevitRequestType.GetAllMaterials` then opens `AddMaterialDialog`. `DupliquerMateriauCommand` calls `RevitRequestType.DuplicateMaterial`. `AutoSave()` persists via `PresetService.Save()`. `StorePresetPath()` writes path to `settings.json`. `GetStoredPresetPath()` reads it on startup. |
| 3 | Clicking Set Mat applies the selected preset material to all selected CompoundStructure layers via a Revit Transaction | VERIFIED | `AppliquerMateriau()` in `MainWindowViewModel` reads `CenterPanelVM.SelectedItems`, casts to `LayerDto[]`, constructs `SetMatRequestDto`, calls `RevitRequestType.SetMaterialOnLayers`. `HandleSetMaterialOnLayers()` uses Get-Modify-Set pattern with `SetCompoundStructure()` inside a named Transaction with explicit `RollBack()` in catch. |
| 4 | For loaded families without layers, Set Mat lets the user choose which material parameter to modify | VERIFIED | `AppliquerMateriau()` branches on `CenterPanelVM.ShowParameters`, casts `SelectedItems` to `MaterialParamDto[]`, extracts `ParameterDefinitionName`, constructs `SetMatParamRequestDto`, calls `RevitRequestType.SetMaterialOnParameter`. User selects which parameters via center panel multi-selection (Ctrl+click/Shift+click from Phase 2). |
| 5 | Set Mat handles errors with rollback and displays a clear message to the user | VERIFIED | `HandleSetMaterialOnLayers()` and `HandleSetMaterialOnParameter()` both catch exceptions and call `tx.RollBack()` before rethrowing. `OnSetMatResult()` in `MainWindowViewModel` shows a French `MessageBox` with the error and sets `SetMatStatusText`. |

**Score: 5/5 truths verified**

---

### Required Artifacts

#### Plan 01 — Data Layer

| Artifact | Line Count | Status | Evidence |
|----------|-----------|--------|----------|
| `Models/PresetMaterialDto.cs` | 13 | VERIFIED | `MaterialName`, `MaterialElementIdValue` (long), `ColorArgb` (int). POCO, no Revit imports. |
| `Models/PresetGroupDto.cs` | 13 | VERIFIED | `GroupName`, `ObservableCollection<PresetMaterialDto> Materials`. |
| `Models/PresetCollectionDto.cs` | 12 | VERIFIED | Root serialization unit: `ObservableCollection<PresetGroupDto> Groups`. |
| `Models/SetMatRequestDto.cs` | 12 | VERIFIED | `TargetTypeIdValue`, `LayerIndices` (int[]), `MaterialIdValue`. |
| `Models/SetMatParamRequestDto.cs` | 12 | VERIFIED | `TargetTypeIdValue`, `MaterialIdValue`, `ParameterDefinitionNames` (string[]). |
| `Models/DuplicateMaterialRequestDto.cs` | 10 | VERIFIED | `MaterialIdValue` for duplication request. |
| `Models/AppSettingsDto.cs` | 10 | VERIFIED | `PresetFilePath` (string?) for settings persistence. |
| `Services/PresetService.cs` | 108 | VERIFIED | `Load`, `Save`, `GetStoredPresetPath`, `StorePresetPath`, `GetDefaultCollection` — all substantive with try/catch and JSON logic. |
| `Services/DialogService.cs` | 32 | VERIFIED | `ShowFolderBrowser` with conditional compilation `#if REVIT2025_OR_GREATER` / `#else` net48. |
| `Converters/ArgbToColorConverter.cs` | 27 | VERIFIED | `IValueConverter` converting `int` ARGB to `System.Windows.Media.Color` via `System.Drawing.Color`. |
| `Messages/RefreshLayersMessage.cs` | 12 | VERIFIED | `ValueChangedMessage<long>` carrying `typeIdValue` for post-SetMat center panel refresh. |
| `Events/RevitRequestType.cs` | 22 | VERIFIED | 4 new enum values: `GetAllMaterials`, `SetMaterialOnLayers`, `SetMaterialOnParameter`, `DuplicateMaterial`. |
| `Events/RevitEventBridge.cs` | 512 | VERIFIED | 4 new handler methods: `HandleGetAllMaterials`, `HandleSetMaterialOnLayers`, `HandleSetMaterialOnParameter`, `HandleDuplicateMaterial`. Plus `ExtractColorArgb` helper. |
| `ViewModels/CenterPanelViewModel.cs` | 185 | VERIFIED | `[ObservableProperty] private long _currentTypeIdValue`. `RefreshLayersMessage` registered in constructor. `CurrentTypeIdValue` set in `OnTypeSelected`. |

#### Plan 02 — Right Panel UI

| Artifact | Line Count | Status | Evidence |
|----------|-----------|--------|----------|
| `ViewModels/RightPanelViewModel.cs` | 230 | VERIFIED | `PresetGroups`, `SelectedPresetMaterial`, `CreerGroupeCommand`, `AjouterMateriauCommand`, `DupliquerMateriauCommand`, `SupprimerMateriauCommand`, `AutoSave`, `LoadPresets`, `FindGroupContaining`. Full implementation, no stubs. |
| `Views/RightPanelView.xaml` | 143 | VERIFIED | `HierarchicalDataTemplate`, `ArgbToColorConverter`, `DupliquerMateriauCommand`, `SupprimerMateriauCommand`, `AjouterMateriauCommand`, `TreeViewSelectionChangedCommand` — all bound. Color swatches via `Rectangle.Fill`. Context menu via Tag proxy pattern. |
| `Views/AddMaterialDialog.xaml` | 87 | VERIFIED | `ListBox` with material items, `TextBox` for search, `ComboBox` for group picker, Ajouter/Annuler buttons. |
| `Views/AddMaterialDialog.xaml.cs` | 89 | VERIFIED | `CollectionViewSource` filter, `InitializeCollectionView()`, dialog result on Ajouter_Click. No business logic. |

#### Plan 03 — Set Mat Command and Button

| Artifact | Line Count | Status | Evidence |
|----------|-----------|--------|----------|
| `ViewModels/MainWindowViewModel.cs` | 239 | VERIFIED | `AppliquerMateriau`, `CanAppliquerMateriau`, `OnSetMatResult`, `StartFeedbackTimer`, `IsSetMatBusy`, `SetMatStatusText`, `NotifyCanExecuteChanged` on cross-ViewModel PropertyChanged. |
| `Views/MainWindow.xaml` | 98 | VERIFIED | 3-row Grid layout. Row 2 = Set Mat bar with `AppliquerMateriauCommand` binding, `SetMatButtonStyle`, `SetMatStatusText`. |
| `Themes/OlympeTheme.xaml` | 515 | VERIFIED | `SetMatButtonStyle` keyed style with `Background="#FF9800"`, `FontWeight="Bold"`, `MinWidth="200"`. |

---

### Key Link Verification

#### Plan 01 Key Links

| From | To | Via | Status | Evidence |
|------|----|-----|--------|---------|
| `Services/PresetService.cs` | `Models/PresetCollectionDto.cs` | `System.Text.Json` serialization | WIRED | `JsonSerializer.Serialize(collection, _options)` and `JsonSerializer.Deserialize<PresetCollectionDto>(json, _options)` — both present in `Load()` and `Save()`. |
| `Events/RevitEventBridge.cs` | `Models/SetMatRequestDto.cs` | Handler parameter casting | WIRED | `HandleSetMaterialOnLayers(uiApp, (SetMatRequestDto)data!)` in switch case. |
| `ViewModels/CenterPanelViewModel.cs` | `Messages/RefreshLayersMessage.cs` | `WeakReferenceMessenger.Register` | WIRED | `WeakReferenceMessenger.Default.Register<RefreshLayersMessage>(this, ...)` in constructor. |

#### Plan 02 Key Links

| From | To | Via | Status | Evidence |
|------|----|-----|--------|---------|
| `ViewModels/RightPanelViewModel.cs` | `Services/PresetService.cs` | `Load/Save` calls | WIRED | `_presetService.GetStoredPresetPath()`, `_presetService.Load()`, `_presetService.Save()`, `_presetService.StorePresetPath()` — all called. |
| `ViewModels/RightPanelViewModel.cs` | `Events/RevitEventBridge.cs` | `MakeRequest` for GetAllMaterials, DuplicateMaterial | WIRED | `_eventBridge.MakeRequest(RevitRequestType.GetAllMaterials, ...)` in `AjouterMateriau`. `_eventBridge.MakeRequest(RevitRequestType.DuplicateMaterial, ...)` in `DupliquerMateriau`. |
| `Views/RightPanelView.xaml` | `ViewModels/RightPanelViewModel.cs` | Data binding to PresetGroups, SelectedPresetMaterial, commands | WIRED | `ItemsSource="{Binding PresetGroups}"`, `Command="{Binding AjouterMateriauCommand}"`, `Command="{Binding CreerGroupeCommand}"`, `Command="{Binding TreeViewSelectionChangedCommand}"`, `DupliquerMateriauCommand`, `SupprimerMateriauCommand` — all bound. |
| `ViewModels/MainWindowViewModel.cs` | `ViewModels/RightPanelViewModel.cs` | Constructor injection of eventBridge and PresetService | WIRED | `var presetService = new PresetService()` then `RightPanelVM = new RightPanelViewModel(eventBridge, presetService)`. |

#### Plan 03 Key Links

| From | To | Via | Status | Evidence |
|------|----|-----|--------|---------|
| `ViewModels/MainWindowViewModel.cs` | `ViewModels/CenterPanelViewModel.cs` | Read SelectedItems, ShowLayers, ShowParameters, CurrentTypeIdValue | WIRED | `CenterPanelVM.ShowLayers`, `CenterPanelVM.SelectedItems?.Cast<LayerDto>()`, `CenterPanelVM.CurrentTypeIdValue`, `CenterPanelVM.ShowParameters` — all accessed in `AppliquerMateriau()`. |
| `ViewModels/MainWindowViewModel.cs` | `ViewModels/RightPanelViewModel.cs` | Read SelectedPresetMaterial | WIRED | `RightPanelVM.SelectedPresetMaterial` read in `AppliquerMateriau()` and `CanAppliquerMateriau()`. |
| `ViewModels/MainWindowViewModel.cs` | `Events/RevitEventBridge.cs` | MakeRequest for SetMaterialOnLayers and SetMaterialOnParameter | WIRED | `_eventBridge?.MakeRequest(RevitRequestType.SetMaterialOnLayers, ...)` and `_eventBridge?.MakeRequest(RevitRequestType.SetMaterialOnParameter, ...)` — both called in `AppliquerMateriau()`. |
| `ViewModels/MainWindowViewModel.cs` | `Messages/RefreshLayersMessage.cs` | Send RefreshLayersMessage on success | WIRED | `WeakReferenceMessenger.Default.Send(new RefreshLayersMessage(CenterPanelVM.CurrentTypeIdValue))` in `OnSetMatResult()` success branch. |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|--------------|--------|--------------------|--------|
| `RightPanelView.xaml` | `PresetGroups` | `RightPanelViewModel.LoadPresets()` → `PresetService.Load()` or `GetDefaultCollection()` | Yes — 3 real default groups or JSON file content | FLOWING |
| `AddMaterialDialog.xaml` | `MaterialList.ItemsSource` | `RevitRequestType.GetAllMaterials` → `FilteredElementCollector(doc).OfClass(typeof(Material))` | Yes — real Revit materials from document | FLOWING |
| `MainWindow.xaml` (Set Mat bar) | `SetMatStatusText` | `OnSetMatResult()` callback from `RevitEventBridge` | Yes — set to "Materiau applique !" on success or error message on failure | FLOWING |
| `Views/RightPanelView.xaml` (color swatches) | `ColorArgb` | `PresetMaterialDto.ColorArgb` populated by `ExtractColorArgb(m)` in `HandleGetAllMaterials` and `HandleDuplicateMaterial` | Yes — computed from `m.Color.Red/Green/Blue` or Gray fallback | FLOWING |

---

### Behavioral Spot-Checks

Step 7b: Build-level verification only (cannot start Revit host in this environment).

| Behavior | Check | Result | Status |
|----------|-------|--------|--------|
| Build compiles on net48 | `dotnet build --no-restore` | `OlympeMaterialManager.dll` (net48) produced — 0 errors, 0 warnings | PASS |
| Build compiles on net8.0-windows | `dotnet build --no-restore` | `OlympeMaterialManager.dll` (net8.0-windows) produced — 0 errors, 0 warnings | PASS |
| RevitRequestType has 4 new enum values | grep `GetAllMaterials` in `RevitRequestType.cs` | Lines 17-20 contain all 4 Phase 3 values | PASS |
| HandleSetMaterialOnLayers uses SetCompoundStructure | grep `SetCompoundStructure` in `RevitEventBridge.cs` | Line 405: `hostAttrs.SetCompoundStructure(cs)` present (Get-Modify-Set pattern) | PASS |
| Transaction rollback pattern present | grep `RollBack` in `RevitEventBridge.cs` | Lines 410, 449, 494: all 3 write handlers have `tx.RollBack()` in catch | PASS |
| SetMatButtonStyle has #FF9800 | grep `FF9800` in `OlympeTheme.xaml` | Lines 12 (AccentColor) and 507 (SetMatButtonStyle background) | PASS |
| AppliquerMateriauCommand bound in MainWindow.xaml | grep `AppliquerMateriauCommand` in `MainWindow.xaml` | Line 86: `Command="{Binding AppliquerMateriauCommand}"` | PASS |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|---------|
| PRESET-01 | 03-01, 03-02 | Panneau droit avec liste presets par groupes | SATISFIED | `RightPanelView.xaml` TreeView bound to `PresetGroups`, `RightPanelViewModel` manages collection |
| PRESET-02 | 03-01, 03-02 | Trois groupes par defaut : Murs, Sols, Autres | SATISFIED | `PresetService.GetDefaultCollection()` returns exactly these 3 groups |
| PRESET-03 | 03-02 | Creer des groupes personnalises | SATISFIED | `CreerGroupeCommand` adds new `PresetGroupDto` to `PresetGroups`, auto-saves |
| PRESET-04 | 03-01, 03-02 | Ajouter un materiau du projet a un groupe | SATISFIED | `AjouterMateriauCommand` → `GetAllMaterials` → `AddMaterialDialog` → `group.Materials.Add()` |
| PRESET-05 | 03-01, 03-02 | Presets persistes dans un fichier JSON choisi par l'utilisateur | SATISFIED | `AutoSave()` → `DialogService.ShowFolderBrowser()` → `PresetService.Save()` |
| PRESET-06 | 03-01, 03-02 | Chemin du fichier JSON memorise automatiquement | SATISFIED | `PresetService.StorePresetPath()` writes `settings.json`; `GetStoredPresetPath()` reads it on startup |
| PRESET-07 | 03-01, 03-02 | Dupliquer un materiau preset ("[Original] copie") | SATISFIED | `DupliquerMateriauCommand` → `RevitRequestType.DuplicateMaterial` → `HandleDuplicateMaterial()` with name collision handling (`copie`, `copie 2`, ...) |
| PRESET-08 | 03-01, 03-03 | Set Mat applique aux couches via Transaction Revit | SATISFIED | `AppliquerMateriau()` → `SetMaterialOnLayers` → `HandleSetMaterialOnLayers()` with `SetCompoundStructure()` |
| PRESET-09 | 03-01, 03-03 | Set Mat sur parametres de familles sans couches | SATISFIED | `AppliquerMateriau()` → `SetMaterialOnParameter` → `HandleSetMaterialOnParameter()` batching all selected params |
| PRESET-10 | 03-01, 03-03 | Set Mat avec rollback et message d'erreur francais | SATISFIED | All 3 write handlers: `tx.RollBack()` in catch; `OnSetMatResult()` shows French `MessageBox` |
| UI-05 | 03-03 | Bouton Set Mat visuellement prominent, entre les panneaux | SATISFIED | `SetMatButtonStyle` with `Background="#FF9800"`, `FontWeight="Bold"`, `MinWidth="200"`, placed in dedicated `Grid.Row="2"` bottom bar |

**All 11 requirements: SATISFIED**

---

### Anti-Patterns Found

No blocker or warning anti-patterns detected.

| File | Pattern | Severity | Finding |
|------|---------|----------|---------|
| All Phase 3 files | TODO/FIXME/placeholder | None found | Clean |
| `RevitEventBridge.cs` | Empty return from handlers | None found | All handlers implement real logic |
| `RightPanelViewModel.cs` | Stub commands | None found | All 5 commands have full implementation |
| `MainWindowViewModel.cs` | `OnSelectedPresetMaterialChanged` placeholder | None found | Placeholder from Plan 02 was replaced with `AppliquerMateriauCommand.NotifyCanExecuteChanged()` |

---

### Human Verification Required

These items cannot be verified without a running Revit host:

#### 1. Right Panel Preset Display

**Test:** Open Revit, launch Olympe MaterialManager, inspect the right panel
**Expected:** Three default groups (Murs, Sols, Autres) visible as expandable TreeView nodes with name + count badge. Groups can be expanded/collapsed.
**Why human:** WPF TreeView rendering and HierarchicalDataTemplate can only be confirmed at runtime

#### 2. Add Material Dialog

**Test:** Click "Ajouter au preset", verify a dialog opens listing all project materials with search filter and group picker
**Expected:** Dialog shows searchable list of project materials with color swatches. Selecting a material + group + clicking "Ajouter" adds it to the right panel.
**Why human:** Requires live Revit document with materials; ExternalEvent round-trip cannot be simulated

#### 3. Set Mat on CompoundStructure Layers

**Test:** Select a wall type in left panel → select layers in center panel → select preset material in right panel → click "Appliquer le materiau"
**Expected:** Button shows "Materiau applique !" for ~2 seconds, center panel refreshes with updated material names, Revit type properties reflect the change
**Why human:** Requires Revit Transaction execution in a live document

#### 4. Set Mat on Loaded Family Parameters

**Test:** Select a loaded family type in left panel → center panel shows material parameters → select parameters → click Set Mat
**Expected:** Material parameter(s) updated in Revit, center panel refreshes
**Why human:** Requires loaded families with material parameters in a live Revit document

#### 5. JSON Persistence Across Sessions

**Test:** Add presets, close and reopen the MaterialManager window
**Expected:** Presets are still present (loaded from stored JSON path). On first save, user is prompted for folder; path is remembered.
**Why human:** Session persistence requires two run cycles in the Revit host

---

### Gaps Summary

No gaps found. All automated verifications passed:

- All 11 artifact files from plans 01-03 exist on disk with substantive implementations (no stubs)
- All key links are wired (imports, method calls, data bindings)
- Data flows are connected from Revit API to UI bindings
- Build succeeds with 0 errors and 0 warnings on both net48 and net8.0-windows target frameworks
- All 11 Phase 3 requirements (PRESET-01 through PRESET-10, UI-05) have implementation evidence

The phase goal — "Users can manage a persistent preset palette and apply materials to selected layers or parameters in one click" — is structurally achieved. The remaining 5 human verification items are all runtime/visual confirmations that cannot be automated without the Revit host process.

---

_Verified: 2026-04-11_
_Verifier: Claude (gsd-verifier)_
