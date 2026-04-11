# Phase 3: Preset Panel and Set Mat - Research

**Researched:** 2026-04-11
**Domain:** Revit WPF MVVM -- preset material management, CompoundStructure write, JSON persistence, folder dialog multi-target
**Confidence:** HIGH

## Summary

Phase 3 implements the core value proposition: a persistent preset palette of materials in the right panel, and a "Set Mat" action that writes a selected preset material onto selected CompoundStructure layers or material parameters. This requires four new RevitEventBridge operations (GetAllMaterials, SetMaterialOnLayers, SetMaterialOnParameter, DuplicateMaterial), a PresetService for JSON persistence, a fully-populated RightPanelViewModel, and a coordinated SetMatCommand in MainWindowViewModel.

The key technical challenges are: (1) CompoundStructure write semantics -- `GetCompoundStructure()` returns a copy, must call `SetCompoundStructure()` after modification within a Transaction; (2) multi-target folder dialog -- `Microsoft.Win32.OpenFolderDialog` exists only in .NET 8+, requiring conditional compilation with `System.Windows.Forms.FolderBrowserDialog` fallback on net48; (3) System.Text.Json on net48 pulls 7 transitive dependencies and needs version pinning to avoid System.Memory conflicts; (4) Material.Duplicate() shares appearance assets by reference -- for this project's scope (simple duplication with rename) that is acceptable, but must be documented.

**Primary recommendation:** Build in this order: DTOs/Models first, then PresetService (pure .NET, testable in isolation), then RevitEventBridge handlers, then RightPanelViewModel + XAML, then SetMatCommand coordination in MainWindowViewModel, and finally the Set Mat button in MainWindow.xaml.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** PresetGroupDto contains: GroupName (string), Materials (ObservableCollection<PresetMaterialDto>). PresetMaterialDto contains: MaterialName (string), MaterialElementIdValue (long), ColorArgb (int, for visual indicator).
- **D-02:** PresetCollectionDto wraps ObservableCollection<PresetGroupDto> and is the root serialization unit.
- **D-03:** Three default groups created on first use: "Murs", "Sols", "Autres".
- **D-04:** Presets serialized to JSON via System.Text.Json. File path chosen by user via FolderBrowserDialog on first save.
- **D-05:** The chosen path is stored in a settings file at %APPDATA%/Olympe/MaterialManager/settings.json. On next launch, the path is read from settings and presets auto-loaded.
- **D-06:** PresetService class handles Load/Save/GetDefaultPath. Injected into RightPanelViewModel.
- **D-07:** Auto-save on every preset modification (add material, create group, duplicate).
- **D-08:** TreeView with PresetGroups as root items, PresetMaterials as children. Each material shows: color swatch (Rectangle with Fill from ColorArgb) + MaterialName.
- **D-09:** Group headers show group name + count badge. Expandable/collapsible.
- **D-10:** "Ajouter au preset" button opens a dialog/flyout showing all project materials (fetched via RevitEventBridge.GetAllMaterials). User picks material and target group.
- **D-11:** "Creer un groupe" button with inline TextBox for group name.
- **D-12:** Right-click context menu on material: "Dupliquer", "Supprimer du preset".
- **D-13:** Duplication creates "[Original] copie" with a new Material in Revit (via Transaction) and adds to same group.
- **D-14:** Set Mat button is a large prominent Button with accent style (#FF9800), positioned between center and right panels in a dedicated row or overlay area. Text: "Appliquer le materiau".
- **D-15:** Set Mat reads: selected layers/parameters from CenterPanelViewModel + selected preset material from RightPanelViewModel.
- **D-16:** For CompoundStructure layers: Transaction wrapping GetCompoundStructure(), modify layer.MaterialId for each selected layer, SetCompoundStructure(). Single Transaction for all selected layers.
- **D-17:** For family material parameters: Transaction wrapping element.get_Parameter().Set(materialId) for each selected parameter.
- **D-18:** On error: Transaction.RollBack() + MessageBox with French error description.
- **D-19:** On success: brief visual feedback (button flash or status text "Materiau applique") + refresh center panel to show updated material names.
- **D-20:** New enum values: GetAllMaterials, SetMaterialOnLayers, SetMaterialOnParameter, DuplicateMaterial.
- **D-21:** GetAllMaterials returns List<PresetMaterialDto> with all materials from FilteredElementCollector.OfClass(typeof(Material)).
- **D-22:** SetMaterialOnLayers takes a SetMatRequestDto (targetTypeIdValue, layerIndices[], materialIdValue) and performs the Transaction.
- **D-23:** DuplicateMaterial takes materialIdValue, returns new PresetMaterialDto with "[Original] copie" name.
- **D-24:** SetMatCommand lives in MainWindowViewModel (coordinates between panels). It reads SelectedLayers/SelectedParams from CenterPanelVM and SelectedPresetMaterial from RightPanelVM.
- **D-25:** After Set Mat success, send a RefreshLayersMessage via Messenger to trigger CenterPanelViewModel to re-fetch.

### Claude's Discretion
- Exact dialog/flyout design for "Ajouter au preset"
- Whether to cache GetAllMaterials or fetch each time
- Animation/transition for success feedback
- Error message wording details

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PRESET-01 | Le panneau droit affiche une liste de materiaux preset organises par groupes | TreeView with HierarchicalDataTemplate binding to PresetGroupDto/PresetMaterialDto. Existing OlympeTheme TreeView/TreeViewItem styles apply. |
| PRESET-02 | Trois groupes par defaut existent : Murs, Sols, Autres | PresetService.GetDefaultCollection() creates these on first use when no JSON file exists. |
| PRESET-03 | L'utilisateur peut creer des groupes de preset personnalises | RightPanelViewModel.CreerGroupeCommand + inline TextBox. ObservableCollection<PresetGroupDto> auto-notifies TreeView. |
| PRESET-04 | L'utilisateur peut ajouter un materiau du projet a un groupe de preset | New GetAllMaterials handler in RevitEventBridge. FilteredElementCollector.OfClass(typeof(Material)) is fast even with 5000+ materials (native quick filter). |
| PRESET-05 | Les presets sont persistes dans un fichier JSON dont le chemin est choisi par l'utilisateur | System.Text.Json 8.0.5 with conditional folder dialog: OpenFolderDialog (.NET 8) / FolderBrowserDialog (net48). |
| PRESET-06 | Le chemin du fichier JSON est memorise et reutilise automatiquement aux sessions suivantes | Settings stored at %APPDATA%/Olympe/MaterialManager/settings.json. PresetService reads on startup. |
| PRESET-07 | L'utilisateur peut dupliquer un materiau preset (nom automatique "[Original] copie") | Material.Duplicate(string name) returns Material. Must run inside Transaction via DuplicateMaterial handler. |
| PRESET-08 | Le bouton Set Mat applique le materiau preset selectionne aux couches selectionnees via Transaction Revit | CompoundStructure.SetMaterialId(int layerIdx, ElementId materialId) inside Transaction, then SetCompoundStructure(). |
| PRESET-09 | Pour les familles sans couches, Set Mat permet a l'utilisateur de choisir quel parametre Material modifier | Parameter.Set(ElementId) returns bool. Read from CenterPanelVM.SelectedItems which are MaterialParamDto. |
| PRESET-10 | Set Mat gere le rollback en cas d'erreur et affiche un message utilisateur | try/catch wrapping Transaction.Commit() with Transaction.RollBack() in catch. Dispatcher.Invoke for error MessageBox. |
| UI-05 | Le bouton Set Mat est visuellement proemirent et centre entre les panneaux centre et droit | Keyed style "SetMatButtonStyle" with Background=#FF9800, large padding, prominent placement in MainWindow.xaml Grid. |
</phase_requirements>

## Standard Stack

### Core (already in project)
| Library | Version | Purpose | Notes |
|---------|---------|---------|-------|
| CommunityToolkit.Mvvm | 8.4.2 | ObservableProperty, RelayCommand, Messenger | Already referenced. Use WeakReferenceMessenger for RefreshLayersMessage. |
| System.Text.Json | 8.0.5 (NuGet) | Preset JSON serialization | Needs explicit PackageReference for net48. On net8.0 it is in-box (NuGet package is a no-op shim). |
| Microsoft.Xaml.Behaviors.Wpf | 1.1.142 | EventTrigger for MVVM event binding | Already referenced. Reuse for TreeView SelectedItemChanged in preset panel. |
| Nice3point.Revit.Api.RevitAPI | 2024.3.30 / 2026.4.0 | Revit API (Material, CompoundStructure, Transaction) | Already referenced per TFM. |

### New for Phase 3
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json | 8.0.5 | JSON preset file read/write | Add `<PackageReference Include="System.Text.Json" Version="8.0.5" />` to shared ItemGroup. On net8.0 it resolves to the in-box assembly (no extra DLL). On net48 it pulls ~7 transitive deps. |

**No additional NuGet packages needed.** The folder dialog uses built-in WPF APIs with conditional compilation.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.Text.Json | Newtonsoft.Json 13.x | More features but adds unnecessary dependency; STJ is lighter and forward-looking. |
| Ookii.Dialogs.Wpf for folder dialog | Built-in conditional compilation | Ookii 5.0.1 does not target net8.0-windows; would need .NETCore3.1 compat shim. Not worth the dependency. |
| Popup/Flyout for "Ajouter au preset" | Modal Window | Popup is lighter, stays within the panel. Modal window is heavier but simpler for complex selection. Recommend: ListBox inside a WPF Popup with search TextBox. |

**Installation:**
```xml
<!-- In the shared ItemGroup of OlympeMaterialManager.csproj -->
<PackageReference Include="System.Text.Json" Version="8.0.5" />
```

## Architecture Patterns

### New Files to Create
```
src/OlympeMaterialManager/
  Models/
    PresetGroupDto.cs          # GroupName + ObservableCollection<PresetMaterialDto>
    PresetMaterialDto.cs       # MaterialName, MaterialElementIdValue (long), ColorArgb (int)
    PresetCollectionDto.cs     # ObservableCollection<PresetGroupDto> (root serialization unit)
    SetMatRequestDto.cs        # TargetTypeIdValue (long), LayerIndices (int[]), MaterialIdValue (long)
    DuplicateMaterialRequestDto.cs  # MaterialIdValue (long), NewName (string)
    AppSettingsDto.cs          # PresetFilePath (string)
  Services/
    PresetService.cs           # Load/Save/GetDefaultPath/GetDefaultCollection
    DialogService.cs           # ShowFolderBrowser (abstraction for folder dialog)
  Messages/
    RefreshLayersMessage.cs    # Empty message to trigger CenterPanel re-fetch
    PresetMaterialSelectedMessage.cs  # (optional -- or expose property directly)
  ViewModels/
    RightPanelViewModel.cs     # REPLACE shell -- full preset management
  Views/
    RightPanelView.xaml        # REPLACE shell -- TreeView with presets
    AddMaterialDialog.xaml     # Popup/flyout for material selection + group target
    AddMaterialDialog.xaml.cs  # Minimal code-behind (window lifecycle only)
```

### Files to Modify
```
  Events/
    RevitRequestType.cs        # ADD: GetAllMaterials, SetMaterialOnLayers, SetMaterialOnParameter, DuplicateMaterial
    RevitEventBridge.cs        # ADD: 4 new handler methods + switch cases
  ViewModels/
    MainWindowViewModel.cs     # ADD: SetMatCommand, _eventBridge usage, coordinate panels
    CenterPanelViewModel.cs    # ADD: RefreshLayersMessage handler, expose selected type id for refresh
  Views/
    MainWindow.xaml            # ADD: Set Mat button between center and right panels
  Themes/
    OlympeTheme.xaml           # ADD: SetMatButtonStyle (keyed), ContextMenu style
```

### Pattern 1: CompoundStructure Write (Get-Modify-Set)
**What:** Read the compound structure copy, modify material IDs on target layers, write back the modified copy.
**When to use:** Every time Set Mat targets a system type with CompoundStructure (walls, floors, roofs, ceilings).
**CRITICAL:** `GetCompoundStructure()` returns a COPY. Changes are lost without `SetCompoundStructure()`.

```csharp
// Inside RevitEventBridge.ProcessRequest, on Revit thread
private static void HandleSetMaterialOnLayers(UIApplication uiApp, SetMatRequestDto request)
{
    var doc = uiApp.ActiveUIDocument.Document;
    using var tx = new Transaction(doc, "Olympe : Appliquer materiau aux couches");
    tx.Start();

    try
    {
        var typeId = ElementIdHelper.FromValue(request.TargetTypeIdValue);
        var hostAttrs = doc.GetElement(typeId) as HostObjAttributes;
        if (hostAttrs == null)
            throw new InvalidOperationException("Le type selectionne n'est pas un type a couches.");

        var cs = hostAttrs.GetCompoundStructure();
        if (cs == null)
            throw new InvalidOperationException("Le type n'a pas de structure composee.");

        var matId = ElementIdHelper.FromValue(request.MaterialIdValue);

        foreach (int layerIndex in request.LayerIndices)
        {
            cs.SetMaterialId(layerIndex, matId);
        }

        hostAttrs.SetCompoundStructure(cs); // PERSISTS changes
        tx.Commit();
    }
    catch
    {
        if (tx.HasStarted() && !tx.HasEnded())
            tx.RollBack();
        throw;
    }
}
```

### Pattern 2: Material Parameter Write
**What:** Set a Material-type parameter on a FamilySymbol or other ElementType.
**When to use:** Set Mat targets a loaded family type with material parameters.

```csharp
private static void HandleSetMaterialOnParameter(UIApplication uiApp, SetMatParamRequestDto request)
{
    var doc = uiApp.ActiveUIDocument.Document;
    using var tx = new Transaction(doc, "Olympe : Appliquer materiau au parametre");
    tx.Start();

    try
    {
        var typeId = ElementIdHelper.FromValue(request.TargetTypeIdValue);
        var element = doc.GetElement(typeId);
        var matId = ElementIdHelper.FromValue(request.MaterialIdValue);

        var param = element.LookupParameter(request.ParameterDefinitionName);
        if (param == null || param.IsReadOnly)
            throw new InvalidOperationException(
                $"Le parametre '{request.ParameterDefinitionName}' est introuvable ou en lecture seule.");

        bool success = param.Set(matId);
        if (!success)
            throw new InvalidOperationException("Echec de l'assignation du materiau au parametre.");

        tx.Commit();
    }
    catch
    {
        if (tx.HasStarted() && !tx.HasEnded())
            tx.RollBack();
        throw;
    }
}
```

### Pattern 3: Material Duplication
**What:** Duplicate a Material in Revit, return a DTO for the new material.
**When to use:** User right-clicks a preset material and selects "Dupliquer".

```csharp
private static PresetMaterialDto HandleDuplicateMaterial(UIApplication uiApp, DuplicateMaterialRequestDto request)
{
    var doc = uiApp.ActiveUIDocument.Document;
    using var tx = new Transaction(doc, "Olympe : Dupliquer materiau");
    tx.Start();

    var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
    var original = doc.GetElement(matId) as Material;
    if (original == null)
        throw new InvalidOperationException("Materiau source introuvable.");

    string newName = $"{original.Name} copie";
    // Handle name collision
    int counter = 2;
    while (new FilteredElementCollector(doc).OfClass(typeof(Material))
               .Cast<Material>().Any(m => m.Name == newName))
    {
        newName = $"{original.Name} copie {counter++}";
    }

    Material duplicate = original.Duplicate(newName);
    tx.Commit();

    return new PresetMaterialDto
    {
        MaterialName = duplicate.Name,
        MaterialElementIdValue = ElementIdHelper.GetValue(duplicate.Id),
        ColorArgb = ExtractColorArgb(duplicate)
    };
}
```

### Pattern 4: Folder Dialog with Conditional Compilation
**What:** Show a folder picker dialog that works on both net48 and net8.0-windows.

```csharp
public static class DialogService
{
    /// <summary>
    /// Shows a folder browser dialog. Returns selected path or null if cancelled.
    /// Uses OpenFolderDialog on .NET 8+ and FolderBrowserDialog on net48.
    /// </summary>
    public static string? ShowFolderBrowser(string title)
    {
#if REVIT2025_OR_GREATER
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
#else
        // net48: use WinForms FolderBrowserDialog
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = title,
            ShowNewFolderButton = true
        };
        var result = dialog.ShowDialog();
        return result == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
#endif
    }
}
```

**Note for net48:** Requires `<FrameworkReference Include="System.Windows.Forms" />` or `<Reference Include="System.Windows.Forms" />` in the net48 ItemGroup. Since the project uses SDK-style csproj, add a conditional reference:
```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net48'">
    <Reference Include="System.Windows.Forms" />
</ItemGroup>
```

### Pattern 5: PresetService JSON Persistence

```csharp
public class PresetService
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsPath;

    public PresetService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Olympe", "MaterialManager");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public string? GetStoredPresetPath()
    {
        if (!File.Exists(_settingsPath)) return null;
        var settings = JsonSerializer.Deserialize<AppSettingsDto>(
            File.ReadAllText(_settingsPath), _options);
        return settings?.PresetFilePath;
    }

    public void StorePresetPath(string path)
    {
        var settings = new AppSettingsDto { PresetFilePath = path };
        File.WriteAllText(_settingsPath,
            JsonSerializer.Serialize(settings, _options));
    }

    public PresetCollectionDto Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<PresetCollectionDto>(json, _options)
               ?? GetDefaultCollection();
    }

    public void Save(PresetCollectionDto collection, string path)
    {
        var json = JsonSerializer.Serialize(collection, _options);
        File.WriteAllText(path, json);
    }

    public static PresetCollectionDto GetDefaultCollection()
    {
        return new PresetCollectionDto
        {
            Groups = new ObservableCollection<PresetGroupDto>
            {
                new() { GroupName = "Murs" },
                new() { GroupName = "Sols" },
                new() { GroupName = "Autres" }
            }
        };
    }
}
```

### Pattern 6: SetMatCommand Coordination in MainWindowViewModel

```csharp
// In MainWindowViewModel
[RelayCommand(CanExecute = nameof(CanAppliquerMateriau))]
private void AppliquerMateriau()
{
    var selectedMaterial = RightPanelVM.SelectedPresetMaterial;
    if (selectedMaterial == null) return;

    // Determine mode: layers or parameters
    if (CenterPanelVM.ShowLayers)
    {
        var selectedLayers = CenterPanelVM.SelectedItems?
            .Cast<LayerDto>()
            .Select(l => l.LayerIndex)
            .ToArray();

        if (selectedLayers == null || selectedLayers.Length == 0) return;

        var request = new SetMatRequestDto
        {
            TargetTypeIdValue = CenterPanelVM.CurrentTypeIdValue,
            LayerIndices = selectedLayers,
            MaterialIdValue = selectedMaterial.MaterialElementIdValue
        };

        _eventBridge?.MakeRequest(RevitRequestType.SetMaterialOnLayers, request, OnSetMatResult);
    }
    else if (CenterPanelVM.ShowParameters)
    {
        // For parameters: apply to each selected parameter
        var selectedParams = CenterPanelVM.SelectedItems?.Cast<MaterialParamDto>().ToList();
        if (selectedParams == null || selectedParams.Count == 0) return;

        foreach (var param in selectedParams)
        {
            var request = new SetMatParamRequestDto
            {
                TargetTypeIdValue = CenterPanelVM.CurrentTypeIdValue,
                MaterialIdValue = selectedMaterial.MaterialElementIdValue,
                ParameterDefinitionName = param.ParameterDefinitionName
            };
            _eventBridge?.MakeRequest(RevitRequestType.SetMaterialOnParameter, request, OnSetMatResult);
        }
    }
}

private bool CanAppliquerMateriau()
    => RightPanelVM.SelectedPresetMaterial != null
       && CenterPanelVM.SelectedItems?.Count > 0;
```

### Anti-Patterns to Avoid
- **Modifying CompoundStructure without SetCompoundStructure():** Changes to the copy are silently lost. Always call `hostAttrs.SetCompoundStructure(cs)` after modifications.
- **Calling Transaction methods from WPF thread:** All Transaction operations must go through RevitEventBridge. The WPF ViewModel never touches `Transaction` directly.
- **Relying on automatic Transaction rollback:** Always explicitly call `RollBack()` in catch blocks. Do not let the `using` dispose do it silently.
- **Storing Revit Material objects in DTO collections:** Store only `ElementId.Value` (long) and resolved name strings. Re-fetch the `Material` element inside the handler when needed.
- **Using LINQ Where() before ToList() on FilteredElementCollector:** Apply `OfClass(typeof(Material))` first (native quick filter), then `ToList()`, then LINQ. Pre-ToList LINQ forces slow managed-code iteration.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization | Custom file format parser | System.Text.Json 8.0.5 | Handles edge cases (escaping, encoding, null handling). Works on both TFMs. |
| Folder dialog abstraction | Win32 API P/Invoke | Conditional compilation with OpenFolderDialog / FolderBrowserDialog | Built-in WPF/.NET APIs are stable and well-tested. |
| MVVM messaging | Custom event aggregator | CommunityToolkit.Mvvm WeakReferenceMessenger | Already in project. Type-safe, memory-safe. |
| Observable collections with grouping | Custom grouped list | ObservableCollection + TreeView HierarchicalDataTemplate | WPF's built-in TreeView data templating handles this natively. |
| Transaction error handling | Custom undo/redo stack | Revit Transaction.RollBack() | Revit owns the undo stack. RollBack() integrates with it. |

## Common Pitfalls

### Pitfall 1: CompoundStructure Copy Semantics
**What goes wrong:** Developer modifies layers via `GetCompoundStructure()` but forgets to call `SetCompoundStructure()`. Changes appear to work in the debugger but are never persisted.
**Why it happens:** API returns a copy, not a reference. This is the #1 most common Revit API bug.
**How to avoid:** Always follow Get -> Modify -> Set pattern within a single Transaction. Add a code comment at every `GetCompoundStructure()` call: `// COPY -- must call SetCompoundStructure() to persist`.
**Warning signs:** Material assignments that "work" during the session but revert on undo or don't show in type properties dialog.

### Pitfall 2: Material.Duplicate() Shares Appearance Assets
**What goes wrong:** After duplicating a material, editing the appearance of the copy also changes the original.
**Why it happens:** `Material.Duplicate()` copies the material definition but the AppearanceAssetId, StructuralAssetId, and ThermalAssetId point to the same shared asset elements.
**How to avoid:** For Phase 3 scope (simple duplication with rename), this is acceptable -- the user wants a copy with a different name. Document that appearance assets are shared. If Phase 4 needs independent appearance editing, duplicate the assets too using `AppearanceAssetElement.Duplicate()`.
**Warning signs:** Editing duplicated material's color/tint changes the original.

### Pitfall 3: System.Text.Json on net48 -- System.Memory Conflict
**What goes wrong:** System.Text.Json 8.0.5 on net48 pulls `System.Memory >= 4.5.5` as a transitive dependency. Some environments or other add-ins may load a different version, causing `FileLoadException`.
**Why it happens:** Revit 2024 (net48) loads assemblies into a shared AppDomain. If another add-in ships a different System.Memory version, binding redirects are needed.
**How to avoid:** Pin `System.Memory` to version 4.5.5 in the csproj. Add binding redirects in app.config if needed. Test with other add-ins installed. Since this project is for internal use, the risk is manageable.
**Warning signs:** `FileLoadException` mentioning `System.Memory` or `System.Buffers` at runtime in Revit 2024.

### Pitfall 4: JSON File Path Becomes Invalid
**What goes wrong:** User moves the preset JSON file or opens the project on a different machine. The stored path in settings.json points to nothing. Presets fail to load silently.
**Why it happens:** Path is stored as absolute string. No validation on startup.
**How to avoid:** On startup, validate the stored path exists. If not, show a "Fichier presets introuvable" message and offer to browse for a new location. Handle `IOException`, `UnauthorizedAccessException`, and `JsonException` with French error messages.
**Warning signs:** Presets panel shows empty groups after moving the file.

### Pitfall 5: CanExecute Not Refreshing for SetMatCommand
**What goes wrong:** The Set Mat button stays disabled even after selecting both a layer and a preset material, or stays enabled when it shouldn't be.
**Why it happens:** `CanAppliquerMateriau()` depends on properties from two different ViewModels. CommunityToolkit.Mvvm's `[RelayCommand(CanExecute=...)]` only auto-notifies when the CanExecute method depends on properties of the same ViewModel.
**How to avoid:** Manually call `AppliquerMateriauCommand.NotifyCanExecuteChanged()` from: (1) RightPanelVM when SelectedPresetMaterial changes (via Messenger or event), (2) CenterPanelVM when SelectedItems changes (via Messenger or event). Use PropertyChanged subscriptions or a lightweight message.
**Warning signs:** Button state is stale -- user has to click elsewhere to refresh it.

### Pitfall 6: Transaction Naming for Undo Stack
**What goes wrong:** Generic transaction names like "Modify" make Revit's undo stack useless.
**Why it happens:** Developer uses placeholder names during development.
**How to avoid:** Use descriptive French names: "Olympe : Appliquer materiau aux couches", "Olympe : Dupliquer materiau '{name}'", "Olympe : Appliquer materiau au parametre".
**Warning signs:** User cannot identify which undo entry corresponds to the add-in's action.

## Code Examples

### GetAllMaterials Handler

```csharp
// Source: Revit API FilteredElementCollector documentation + project DTO pattern
private static List<PresetMaterialDto> HandleGetAllMaterials(UIApplication uiApp)
{
    var doc = uiApp.ActiveUIDocument?.Document;
    if (doc == null) return new List<PresetMaterialDto>();

    return new FilteredElementCollector(doc)
        .OfClass(typeof(Material))  // Native quick filter -- fast even with 5000+ materials
        .Cast<Material>()
        .Select(m => new PresetMaterialDto
        {
            MaterialName = m.Name,
            MaterialElementIdValue = ElementIdHelper.GetValue(m.Id),
            ColorArgb = (m.Color.IsValid
                ? System.Drawing.Color.FromArgb(255, m.Color.Red, m.Color.Green, m.Color.Blue).ToArgb()
                : System.Drawing.Color.Gray.ToArgb())
        })
        .OrderBy(m => m.MaterialName)
        .ToList();
}
```

**Performance note:** `OfClass(typeof(Material))` is a native Revit quick filter that operates on element headers without loading full elements. It is efficient even with 5000+ materials. The `.Cast<Material>()` forces element resolution but this only happens once per call. Caching is recommended (D-10 discretion): fetch once when the "Ajouter au preset" dialog opens, don't re-fetch on every keystroke.

### TreeView with HierarchicalDataTemplate (RightPanelView.xaml)

```xml
<!-- Source: WPF HierarchicalDataTemplate pattern -->
<TreeView ItemsSource="{Binding PresetGroups}"
          VirtualizingStackPanel.IsVirtualizing="True"
          VirtualizingStackPanel.VirtualizationMode="Recycling">
    <TreeView.ItemTemplate>
        <HierarchicalDataTemplate ItemsSource="{Binding Materials}">
            <!-- Group header: name + count badge -->
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding GroupName}"
                           FontWeight="SemiBold"
                           Foreground="{StaticResource TextPrimaryBrush}" />
                <Border Background="{StaticResource SurfaceBrush}"
                        CornerRadius="8" Padding="6,1" Margin="8,0,0,0">
                    <TextBlock Text="{Binding Materials.Count}"
                               FontSize="11"
                               Foreground="{StaticResource TextSecondaryBrush}" />
                </Border>
            </StackPanel>
            <!-- Material child template -->
            <HierarchicalDataTemplate.ItemTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal" Margin="0,2">
                        <!-- Color swatch from ColorArgb -->
                        <Rectangle Width="14" Height="14"
                                   RadiusX="2" RadiusY="2"
                                   Margin="0,0,8,0">
                            <Rectangle.Fill>
                                <SolidColorBrush Color="{Binding ColorArgb,
                                    Converter={StaticResource ArgbToColorConverter}}" />
                            </Rectangle.Fill>
                        </Rectangle>
                        <TextBlock Text="{Binding MaterialName}"
                                   Foreground="{StaticResource TextPrimaryBrush}" />
                    </StackPanel>
                </DataTemplate>
            </HierarchicalDataTemplate.ItemTemplate>
        </HierarchicalDataTemplate>
    </TreeView.ItemTemplate>
</TreeView>
```

### ArgbToColorConverter (new converter needed)

```csharp
// Source: Standard WPF IValueConverter pattern
public class ArgbToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int argb)
        {
            var color = System.Drawing.Color.FromArgb(argb);
            return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }
        return System.Windows.Media.Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

### Set Mat Button Style (MainWindow.xaml)

```xml
<!-- Inside MainWindow.xaml, between center and right panels -->
<!-- Option: Add a row in the Grid for the button, or overlay it -->
<Button Content="Appliquer le materiau"
        Command="{Binding AppliquerMateriauCommand}"
        Style="{StaticResource SetMatButtonStyle}"
        Grid.Column="2"
        HorizontalAlignment="Right"
        VerticalAlignment="Bottom"
        Margin="0,0,8,8" />

<!-- In OlympeTheme.xaml or MainWindow.Resources -->
<Style x:Key="SetMatButtonStyle" TargetType="Button"
       BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Background" Value="#FF9800" />
    <Setter Property="Foreground" Value="White" />
    <Setter Property="FontSize" Value="14" />
    <Setter Property="FontWeight" Value="Bold" />
    <Setter Property="Padding" Value="20,10" />
    <Setter Property="MinWidth" Value="180" />
</Style>
```

### RefreshLayersMessage Pattern

```csharp
// Messages/RefreshLayersMessage.cs
public class RefreshLayersMessage : ValueChangedMessage<long>
{
    public RefreshLayersMessage(long typeIdValue) : base(typeIdValue) { }
}

// In CenterPanelViewModel constructor -- register handler
WeakReferenceMessenger.Default.Register<RefreshLayersMessage>(this, (r, m) =>
{
    var vm = (CenterPanelViewModel)r;
    if (vm.ShowLayers)
        vm.FetchLayers(m.Value);
    else if (vm.ShowParameters)
        vm.FetchMaterialParameters(m.Value);
});
```

### Context Menu for Preset Materials

```xml
<!-- On the inner DataTemplate for materials in TreeView -->
<DataTemplate>
    <StackPanel Orientation="Horizontal" Margin="0,2"
                Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType=TreeView}}">
        <StackPanel.ContextMenu>
            <ContextMenu>
                <MenuItem Header="Dupliquer"
                          Command="{Binding PlacementTarget.Tag.DupliquerMateriauCommand,
                                   RelativeSource={RelativeSource AncestorType=ContextMenu}}"
                          CommandParameter="{Binding}" />
                <MenuItem Header="Supprimer du preset"
                          Command="{Binding PlacementTarget.Tag.SupprimerMateriauCommand,
                                   RelativeSource={RelativeSource AncestorType=ContextMenu}}"
                          CommandParameter="{Binding}" />
            </ContextMenu>
        </StackPanel.ContextMenu>
        <!-- swatch + name -->
    </StackPanel>
</DataTemplate>
```

**Note:** The `Tag` proxy pattern for ContextMenu binding is already established in the project (Phase 2 used `PlacementTarget.Tag` for crossing the visual tree boundary).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `ElementId(int)` constructor | `ElementId(long)` constructor | Revit 2024 | Project already uses `ElementIdHelper.FromValue(long)` -- no changes needed. |
| No folder dialog in WPF | `Microsoft.Win32.OpenFolderDialog` | .NET 8 (2023) | Use conditional compilation: OpenFolderDialog on net8.0, FolderBrowserDialog on net48. |
| Newtonsoft.Json as default | System.Text.Json as default | .NET Core 3.0+ | Project uses System.Text.Json per decision D-04. |
| CompoundStructure requires core layer | Core layer optional | Revit 2026 | Existing HandleGetLayersForType already handles this (iterates all layers by index). SetMaterialId by index works regardless of core layer presence. |

## Open Questions

1. **CenterPanelViewModel.CurrentTypeIdValue -- not currently exposed**
   - What we know: CenterPanelVM knows which type is loaded (it receives TypeSelectedMessage), but does not currently expose the type's ElementIdValue as a public property.
   - What's unclear: Whether to store it as a property or re-derive from the message.
   - Recommendation: Add a `public long CurrentTypeIdValue` property to CenterPanelViewModel, set in `OnTypeSelected`. The SetMatCommand needs it to construct SetMatRequestDto.

2. **Multiple parameter Set Mat -- sequential or batched?**
   - What we know: D-17 says Transaction wrapping `element.get_Parameter().Set(materialId)` for each selected parameter. D-22 defines SetMatRequestDto for layers with an array of indices.
   - What's unclear: Should parameter assignment be a single Transaction with multiple Set() calls, or multiple requests?
   - Recommendation: Single Transaction with a loop over all selected parameters (same pattern as layers). Create a `SetMatParamRequestDto` with `ParameterDefinitionNames (string[])` to batch them. One Revit API round-trip, one undo step.

3. **"Ajouter au preset" dialog design (Claude's discretion)**
   - Recommendation: Use a WPF Popup or small modal Window with: (1) a TextBox for filtering material names, (2) a ListBox of filtered PresetMaterialDto items, (3) a ComboBox to pick the target group, (4) an "Ajouter" button. Fetch GetAllMaterials once when the dialog opens, filter client-side. This avoids a complex flyout and is consistent with the existing dark theme. Cache the material list for the dialog's lifetime (don't re-fetch on every keystroke).

4. **Success feedback animation (Claude's discretion)**
   - Recommendation: After successful Set Mat, briefly change the button text to "Materiau applique !" with a green background for 2 seconds, then revert. Use a DispatcherTimer. Lightweight, no animation framework needed.

## Project Constraints (from CLAUDE.md)

The following directives from CLAUDE.md apply to Phase 3 implementation:

- **MVVM strict:** No code-behind business logic. RelayCommand for all commands. ObservableCollection for all list data. One ViewModel per view/panel.
- **Naming:** PascalCase classes/properties, _camelCase private fields.
- **IExternalEventHandler:** All Revit API interaction goes through RevitEventBridge. ViewModels never import Revit types.
- **French UI:** All user-visible text in French.
- **CopyLocal = false:** Revit API references are external. Already configured in csproj.
- **Multi-target:** Code must compile on both net48 and net8.0-windows. Use conditional compilation (`REVIT2024` / `REVIT2025_OR_GREATER`) where APIs differ.
- **GSD Workflow:** Do not make direct repo edits outside a GSD workflow.

## Revit API Reference Summary

### Material.Duplicate(string name)
- **Returns:** `Material` (the new duplicated material), or `null` if duplication fails for non-name reasons.
- **Exceptions:** `ArgumentException` if name contains prohibited characters (`{}[]|;<>?~`) or if name is already in use. `ArgumentNullException` if name is null.
- **IMPORTANT:** Shares appearance/structural/thermal assets by reference with the original. Editing appearance of the copy also changes the original unless assets are separately duplicated.
- **Transaction:** Required (must be inside an open Transaction).

### CompoundStructure.SetMaterialId(int layerIdx, ElementId materialId)
- **Returns:** void
- **Exceptions:** `ArgumentNullException` if materialId is null. `ArgumentOutOfRangeException` if layerIdx is out of range.
- **IMPORTANT:** Does NOT verify that materialId corresponds to a valid Material element. Invalid IDs silently create broken layer references.
- **Transaction:** Not required on the CompoundStructure copy itself, but `SetCompoundStructure()` to persist requires a Transaction.

### Parameter.Set(ElementId value)
- **Returns:** `bool` (true if successful, false otherwise)
- **Exceptions:** `InvalidOperationException` if parameter is read-only.
- **Precondition:** Only call when `StorageType == StorageType.ElementId`.
- **Transaction:** Required.

### FilteredElementCollector.OfClass(typeof(Material))
- **Performance:** Native quick filter operating on element headers. Fast even with 5000+ materials. Do NOT add LINQ `.Where()` before `.ToList()` -- post-filter after materialization.
- **Best practice:** `.OfClass(typeof(Material)).Cast<Material>().ToList()` then LINQ on the list.

## Sources

### Primary (HIGH confidence)
- [Revit API Docs: Material.Duplicate()](https://www.revitapidocs.com/2023/683b9b3b-fcd7-299d-e42f-712ac1550f17.htm) - Method signature, exceptions, return type
- [Revit API Docs: CompoundStructure.SetMaterialId()](https://www.revitapidocs.com/2015/19cb546b-1c9b-f658-7a66-4206af0b4b80.htm) - Method signature, exceptions
- [Revit API Docs: Parameter.Set(ElementId)](https://www.revitapidocs.com/2024/992097b4-0477-249f-581d-7903dfafd66d.htm) - Method signature, exceptions
- [Microsoft: WPF File Dialog Improvements in .NET 8](https://devblogs.microsoft.com/dotnet/wpf-file-dialog-improvements-in-dotnet-8/) - OpenFolderDialog availability
- [NuGet: System.Text.Json 8.0.5](https://www.nuget.org/packages/System.Text.Json/8.0.5) - Framework targets, dependencies
- [The Building Coder: Updating Wall Compound Layer Structure](https://thebuildingcoder.typepad.com/blog/2012/03/updating-wall-compound-layer-structure.html) - Get-Modify-Set pattern
- [The Building Coder: Filtered Element Collectors](https://thebuildingcoder.typepad.com/blog/2010/10/filtered-element-collectors.html) - Performance guidance
- [The Building Coder: Handling Transaction Status and Errors](https://thebuildingcoder.typepad.com/blog/2014/11/handling-transaction-status-and-errors.html) - Transaction best practices

### Secondary (MEDIUM confidence)
- [Autodesk Forums: Set Materials on CompoundStructureLayer](https://forums.autodesk.com/t5/revit-api-forum/set-materials-on-compoundstructurelayer/td-p/8456904) - Community examples of SetMaterialId
- [BIM Chapters: Duplicating a Material and its assets](https://bimchapters.blogspot.com/2020/03/duplicating-material-and-its-assets.html) - Asset sharing behavior on Material.Duplicate
- [Autodesk Forums: Text.Json fails to load in Revit 2025](https://forums.autodesk.com/t5/revit-api-forum/text-json-fails-to-load-in-revit-2025-targeting-net-8/td-p/13613988) - System.Text.Json assembly loading issues
- [GitHub: dotnet/runtime #109827](https://github.com/dotnet/runtime/issues/109827) - System.Memory 4.6.0 release issue with NET48
- [Ookii.Dialogs.Wpf NuGet](https://www.nuget.org/packages/Ookii.Dialogs.Wpf) - Framework targets (does not target net8.0-windows)

### Tertiary (LOW confidence)
- Exact behavior of System.Text.Json 8.0.5 transitive dependency resolution in a Revit 2024 AppDomain with other add-ins installed -- needs runtime testing.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Verified package versions, framework targets, and API signatures against official docs and NuGet.
- Architecture: HIGH - Patterns derived from existing codebase (RevitEventBridge, DTO boundary, Messenger) and Revit API official documentation.
- Pitfalls: HIGH - CompoundStructure copy semantics, Material.Duplicate asset sharing, and Transaction patterns verified against multiple authoritative sources.
- Folder dialog: HIGH - OpenFolderDialog .NET 8+ availability confirmed by Microsoft blog. Conditional compilation with FolderBrowserDialog on net48 is established pattern.
- System.Text.Json on net48: MEDIUM - Works but transitive dependency conflicts possible in Revit hosting context. Pin System.Memory to 4.5.5.

**Research date:** 2026-04-11
**Valid until:** 2026-05-11 (30 days -- stable Revit API, stable NuGet versions)
