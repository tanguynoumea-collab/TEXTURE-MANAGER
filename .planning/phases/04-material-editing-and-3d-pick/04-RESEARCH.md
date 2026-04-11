# Phase 4: Material Editing and 3D Pick - Research

**Researched:** 2026-04-11
**Domain:** Revit API Material editing (AppearanceAssetEditScope, Material properties), UIDocument.Selection.PickObject from modeless WPF, inter-ViewModel messaging
**Confidence:** HIGH

## Summary

Phase 4 adds two feature clusters: (1) a material visualizer/editor in the right panel for live editing of name, description, surface pattern color, and appearance tint; and (2) a 3D pick mode in the left panel that lets users click elements in the Revit viewport to add types to the active scene. Both features build on the established RevitEventBridge pattern (6 new enum values, 6 new handlers) and require careful coordination between the WPF UI thread and Revit's single-threaded API context.

The material editing features rely on two distinct Revit API mechanisms: direct property setters on the `Material` class (Name, SurfaceForegroundPatternColor) and the `AppearanceAssetEditScope` pattern for appearance tint modifications. The 3D pick feature requires hiding the WPF window, calling `PickObject` on the Revit thread (which blocks until user picks or cancels), and re-showing the window -- all coordinated through ExternalEvent.

**Primary recommendation:** Implement the 6 new RevitEventBridge handlers first (Wave 1), then the material editor UI (Wave 2), then the 3D pick feature (Wave 3). Each handler follows the existing Transaction pattern with French-language names.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- D-01: Material visualizer is a dedicated zone in the right panel, below the preset TreeView
- D-02: Displays MaterialName (editable TextBox), Description (editable TextBox), SurfaceForegroundPatternColor (editable), SurfacePatternName (read-only), TintEnabled (CheckBox), TintColor (editable RGB when enabled), Preview image
- D-03: Edits trigger immediate Revit Transactions via RevitEventBridge. Each field change = one Transaction
- D-04: Tint editing uses AppearanceAssetEditScope within an open Transaction
- D-05: generic_diffuse uses AssetPropertyDoubleArray4d (RGBA). Tint enable = set generic_is_metal or generic_diffuse_image_modifier
- D-06: When no AppearanceAsset: disable tint controls, show "Teinte non disponible". No creation of new AppearanceAssets
- D-07: Name edit: Material.Name = newValue in Transaction
- D-08: Description edit: Material.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION).Set(newValue) in Transaction
- D-09: Surface pattern color edit: Material.SurfaceForegroundPatternColor = new Color(r,g,b) in Transaction
- D-10: Each edit is a separate Transaction for undo granularity
- D-11: "Ajouter par clic" button toggles pick mode. WPF window hides, PickObject runs via ExternalEvent
- D-12: On successful pick: extract Element -> get ElementType -> create SceneTypeDto -> add to active scene -> show WPF window
- D-13: On cancel (OperationCanceledException from Escape): show WPF window, no error
- D-14: Validate active view is View3D before enabling pick. If not: button disabled with tooltip "Vue 3D requise"
- D-15: New enum values: PickElementInView, EditMaterialName, EditMaterialDescription, EditMaterialColor, EditMaterialTint, GetMaterialDetails
- D-16: GetMaterialDetails returns MaterialDetailsDto with Name, Description, ColorArgb, PatternName, HasAppearanceAsset, TintEnabled, TintColorArgb, ThumbnailPath (nullable)
- D-17: PickElementInView uses UIDocument.Selection.PickObject(ObjectType.Element)
- D-18: Attempt Material thumbnail via parameter or AppearanceAssetElement path. Fallback: colored Rectangle
- D-19: Preview refreshes after every material edit
- D-20: MaterialSelectedMessage carries PresetMaterialDto from RightPanelVM to material editor section
- D-21: MaterialEditedMessage sent after each edit to trigger preset list refresh

### Claude's Discretion
- Exact layout proportions for visualizer vs preset TreeView
- ColorPicker implementation (inline RGB TextBoxes vs WPF color dialog)
- Whether MaterialEditor is a sub-ViewModel or inline in RightPanelViewModel
- Loading indicator during 3D pick mode

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MATEDIT-01 | Le visualisateur affiche le nom, la description, le motif/couleur de surface et la teinte du materiau selectionne | GetMaterialDetails handler returns MaterialDetailsDto with all fields; WPF binding to editor section |
| MATEDIT-02 | L'utilisateur peut editer le nom du materiau en live via Transaction Revit | Material.Name setter is writable in Transaction; EditMaterialName handler pattern documented |
| MATEDIT-03 | L'utilisateur peut editer la description du materiau en live via Transaction Revit | BuiltInParameter.ALL_MODEL_DESCRIPTION via get_Parameter().Set() in Transaction |
| MATEDIT-04 | L'utilisateur peut editer le motif et la couleur de premier plan via Transaction | Material.SurfaceForegroundPatternColor setter is read/write (since 2019 API); known bug REVIT-134700 when same as Material.Color |
| MATEDIT-05 | L'utilisateur peut activer/desactiver la teinte et modifier la couleur RVB via AppearanceAssetEditScope | common_Tint_toggle (AssetPropertyBoolean) + common_Tint_color (AssetPropertyDoubleArray4d) via edit scope |
| MATEDIT-06 | Une preview est affichee (thumbnail existant ou fallback image coloree) | AppearanceAssetElement has ThumbnailFile property (nullable); fallback to colored Rectangle |
| MATEDIT-07 | La preview se rafraichit apres chaque modification | MaterialEditedMessage triggers re-fetch of GetMaterialDetails |
| MATEDIT-08 | Les cas sans AppearanceAsset sont geres gracieusement | HasAppearanceAsset flag in DTO; disable tint controls in UI |
| SCENE-04 | L'utilisateur peut ajouter des elements a la scene via un clic dans la vue 3D | PickObject via ExternalEvent with window hide/show pattern |
| SCENE-09 | La vue 3D active est validee avant d'autoriser la selection par clic | uiApp.ActiveUIDocument.ActiveView is View3D check in handler |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- MVVM strict: no business logic in code-behind, RelayCommand, ObservableCollection
- ViewModels never import Revit API types -- communication via DTOs only
- IExternalEventHandler (RevitEventBridge) for all Revit interactions from UI
- Interface language: French
- PascalCase classes/properties, _camelCase private fields
- CommunityToolkit.Mvvm 8.4.2 for ObservableObject, RelayCommand, ObservableProperty
- Single multi-target csproj (validated in Phase 1)

## Standard Stack

No new libraries needed for Phase 4. All functionality uses existing dependencies.

### Core (already in project)
| Library | Version | Purpose | Phase 4 Usage |
|---------|---------|---------|---------------|
| CommunityToolkit.Mvvm | 8.4.2 | MVVM infrastructure | New messages (MaterialSelectedMessage, MaterialEditedMessage), new ObservableProperty fields in ViewModels |
| Revit API (Nice3point) | 2024/2025/2026 | Revit interaction | AppearanceAssetEditScope, Material properties, UIDocument.Selection.PickObject |
| Microsoft.Xaml.Behaviors.Wpf | 1.1.122 | WPF behaviors | EventTrigger for new UI sections |

### No New Dependencies
Phase 4 uses only existing project dependencies. The material editing and 3D pick features are pure Revit API + WPF + MVVM work.

## Architecture Patterns

### New Files to Create

```
Models/
  MaterialDetailsDto.cs           # DTO for GetMaterialDetails response
  EditMaterialRequestDto.cs       # Generic DTO for material edits (or one per edit type)
Messages/
  MaterialSelectedMessage.cs      # PresetMaterialDto from TreeView selection to editor
  MaterialEditedMessage.cs        # Triggers preset list refresh after edit
ViewModels/
  MaterialEditorViewModel.cs      # Sub-VM for material editor section (recommended)
```

### Existing Files to Modify

```
Events/
  RevitRequestType.cs             # +6 enum values
  RevitEventBridge.cs             # +6 handler methods
ViewModels/
  RightPanelViewModel.cs          # Wire up MaterialEditorViewModel, relay selection
  LeftPanelViewModel.cs           # Add pick mode toggle, 3D view validation
Views/
  RightPanelView.xaml             # Add material editor section below TreeView
  LeftPanelView.xaml              # Add "Ajouter par clic" button
```

### Pattern 1: Material Property Edit Handler

Each material edit is a separate Transaction for undo granularity (D-10). Follow the existing handler pattern in RevitEventBridge.

```csharp
// Source: Revit API docs + existing codebase pattern
private static void HandleEditMaterialName(UIApplication uiApp, EditMaterialNameRequestDto request)
{
    var doc = uiApp.ActiveUIDocument!.Document;
    using var tx = new Transaction(doc, $"Olympe : Renommer materiau");
    tx.Start();

    try
    {
        var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
        var material = doc.GetElement(matId) as Material;
        if (material == null)
            throw new InvalidOperationException("Materiau introuvable.");

        material.Name = request.NewName;
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

### Pattern 2: AppearanceAssetEditScope for Tint

Transaction MUST be open before `Commit()`. The scope edits the appearance asset's `common_Tint_toggle` and `common_Tint_color` properties.

```csharp
// Source: Revit API official docs, revitapidocs.com/2026, Building Coder
private static void HandleEditMaterialTint(UIApplication uiApp, EditMaterialTintRequestDto request)
{
    var doc = uiApp.ActiveUIDocument!.Document;
    using var tx = new Transaction(doc, "Olympe : Modifier teinte materiau");
    tx.Start();

    try
    {
        var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
        var material = doc.GetElement(matId) as Material;
        if (material == null)
            throw new InvalidOperationException("Materiau introuvable.");

        var assetElemId = material.AppearanceAssetId;
        if (assetElemId == ElementId.InvalidElementId)
            throw new InvalidOperationException("Teinte non disponible : pas d'AppearanceAsset.");

        var assetElem = doc.GetElement(assetElemId) as AppearanceAssetElement;
        if (assetElem == null)
            throw new InvalidOperationException("AppearanceAssetElement introuvable.");

        using (var scope = new AppearanceAssetEditScope(doc))
        {
            Asset editableAsset = scope.Start(assetElemId);

            // Toggle tint on/off
            var tintToggle = editableAsset.FindByName("common_Tint_toggle")
                as AssetPropertyBoolean;
            if (tintToggle != null)
                tintToggle.Value = request.TintEnabled;

            // Set tint color (RGB as doubles 0.0-1.0)
            if (request.TintEnabled)
            {
                var tintColor = editableAsset.FindByName("common_Tint_color")
                    as AssetPropertyDoubleArray4d;
                if (tintColor != null)
                {
                    tintColor.SetValueAsDoubles(new double[]
                    {
                        request.Red / 255.0,
                        request.Green / 255.0,
                        request.Blue / 255.0,
                        1.0 // Alpha
                    });
                }
            }

            scope.Commit(true); // true = force view update
        }

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

**Confidence: HIGH** -- common_Tint_toggle and common_Tint_color are documented property names. SetValueAsDoubles takes an array of 4 doubles (RGBA normalized 0.0-1.0). SetValueAsColor is an alternative that takes a Revit Color object.

### Pattern 3: PickObject with Window Hide/Show

The ExternalEvent handler hides the WPF window, calls PickObject (which blocks the Revit thread until user picks or presses Escape), then shows the window again.

```csharp
// Source: Building Coder, Revit SDK ModelessForm_ExternalEvent, Autodesk community
private static SceneTypeDto? HandlePickElementInView(UIApplication uiApp)
{
    var uiDoc = uiApp.ActiveUIDocument;
    if (uiDoc == null) return null;

    // D-14: Validate active view is View3D
    if (uiDoc.ActiveView is not View3D)
        throw new InvalidOperationException("Vue 3D requise pour la selection par clic.");

    // Hide WPF window before pick (D-11)
    var mainWindow = App.MainWindow;
    mainWindow?.Hide();

    try
    {
        var reference = uiDoc.Selection.PickObject(
            Autodesk.Revit.UI.Selection.ObjectType.Element,
            "Selectionnez un element dans la vue 3D");

        var element = uiDoc.Document.GetElement(reference);
        if (element == null) return null;

        // D-12: Extract ElementType from picked element
        var typeId = element.GetTypeId();
        if (typeId == ElementId.InvalidElementId) return null;

        var elementType = uiDoc.Document.GetElement(typeId) as ElementType;
        if (elementType == null) return null;

        bool hasCs = elementType is HostObjAttributes hoa
            && hoa.GetCompoundStructure() != null;

        string catName = element.Category?.Name ?? "Autre";

        return new SceneTypeDto
        {
            ElementIdValue = ElementIdHelper.GetValue(typeId),
            FamilyName = elementType.FamilyName,
            TypeName = elementType.Name,
            CategoryName = catName,
            HasCompoundStructure = hasCs
        };
    }
    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
    {
        // D-13: User pressed Escape -- graceful return, not an error
        return null;
    }
    finally
    {
        // Always re-show window (D-11, D-13)
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            mainWindow?.Show();
        });
    }
}
```

**CRITICAL:** Catch `Autodesk.Revit.Exceptions.OperationCanceledException`, NOT `System.OperationCanceledException`. These are different types in different namespaces. The Revit-specific exception is thrown when the user presses Escape during pick.

### Pattern 4: GetMaterialDetails Handler

Reads all material properties into a single DTO for the visualizer.

```csharp
private static MaterialDetailsDto HandleGetMaterialDetails(UIApplication uiApp, long materialIdValue)
{
    var doc = uiApp.ActiveUIDocument?.Document;
    if (doc == null)
        throw new InvalidOperationException("Aucun document ouvert.");

    var matId = ElementIdHelper.FromValue(materialIdValue);
    var material = doc.GetElement(matId) as Material;
    if (material == null)
        throw new InvalidOperationException("Materiau introuvable.");

    var dto = new MaterialDetailsDto
    {
        Name = material.Name,
        ColorArgb = ExtractColorArgb(material),
        PatternName = GetPatternName(doc, material),
        HasAppearanceAsset = material.AppearanceAssetId != ElementId.InvalidElementId
    };

    // Description via BuiltInParameter (D-08)
    var descParam = material.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);
    dto.Description = descParam?.AsString() ?? string.Empty;

    // Tint properties (if AppearanceAsset exists)
    if (dto.HasAppearanceAsset)
    {
        var assetElem = doc.GetElement(material.AppearanceAssetId) as AppearanceAssetElement;
        if (assetElem != null)
        {
            var renderAsset = assetElem.GetRenderingAsset();
            var tintToggle = renderAsset.FindByName("common_Tint_toggle")
                as AssetPropertyBoolean;
            dto.TintEnabled = tintToggle?.Value ?? false;

            var tintColor = renderAsset.FindByName("common_Tint_color")
                as AssetPropertyDoubleArray4d;
            if (tintColor != null)
            {
                var values = tintColor.GetValueAsDoubles();
                if (values.Count >= 3)
                {
                    byte r = (byte)(values[0] * 255);
                    byte g = (byte)(values[1] * 255);
                    byte b = (byte)(values[2] * 255);
                    dto.TintColorArgb = System.Drawing.Color.FromArgb(255, r, g, b).ToArgb();
                }
            }

            // D-18: Attempt thumbnail path
            // Note: use FindByName on the rendering asset, not on the editable asset
            // The "thumbnail" property may be null or point to a temp file
        }
    }

    return dto;
}

private static string GetPatternName(Document doc, Material material)
{
    var patternId = material.SurfaceForegroundPatternId;
    if (patternId == ElementId.InvalidElementId)
        return "< Aucun >";
    var pattern = doc.GetElement(patternId) as FillPatternElement;
    return pattern?.Name ?? "< Inconnu >";
}
```

### Pattern 5: View3D Validation for Pick Button

Check in the ExternalEvent callback, not in the ViewModel directly.

```csharp
// In LeftPanelViewModel -- request validation via bridge
[RelayCommand]
private void VerifierVue3D()
{
    _eventBridge?.MakeRequest(RevitRequestType.GetDocumentInfo, null, result =>
    {
        // The handler can check ActiveView type and return it
        // Or: add a dedicated lightweight check
    });
}
```

**Recommended approach:** Add a lightweight check in the PickElementInView handler itself (as shown in Pattern 3). For the button enable/disable, either:
- (a) Check once when the pick button is about to be pressed (inside the handler, throw if not 3D), or
- (b) Add a periodic check via the existing document info refresh. Option (a) is simpler and sufficient.

### Anti-Patterns to Avoid

- **Catching System.OperationCanceledException instead of Autodesk.Revit.Exceptions.OperationCanceledException** for PickObject cancel. They are different types. Use the full namespace.
- **Calling PickObject from the WPF thread.** It MUST run inside the ExternalEvent handler on the Revit thread.
- **Forgetting to re-show the window after pick.** Use a `finally` block to guarantee the window reappears even on exceptions.
- **Opening AppearanceAssetEditScope without an open Transaction before Commit().** Transaction must be started before `scope.Commit()` is called.
- **Using `SetValueAsColor` when `SetValueAsDoubles` would be more predictable.** SetValueAsColor can throw exceptions for some material schemas (documented bug in Autodesk forums). SetValueAsDoubles with normalized RGBA doubles is safer.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Color picker UI | Full color wheel/gradient control | Three TextBoxes for R, G, B (0-255) with validation | WPF has no built-in color picker. A full custom control is out of scope. Three TextBoxes match the dark theme and are sufficient. |
| Material thumbnail rendering | Custom 3D sphere renderer | `AppearanceAssetElement` ThumbnailFile property + fallback colored Rectangle | Realistic rendering is explicitly out of scope (PROJECT.md). The API thumbnail is opportunistic. |
| Asset property name constants | Hardcoded strings throughout handlers | Static string constants in a helper class | The Revit API provides `Autodesk.Revit.DB.Visual.Generic` and similar classes, but `FindByName("common_Tint_color")` with string literals is the documented community pattern. Keep strings in one place. |
| PickObject selection filter | Complex ISelectionFilter | No filter (ObjectType.Element accepts all) | For Phase 4, any element in the 3D view is valid. The user adds the type, not the instance. Filtering can be added later if needed. |

## Common Pitfalls

### Pitfall 1: Wrong OperationCanceledException Namespace

**What goes wrong:** Catching `System.OperationCanceledException` instead of `Autodesk.Revit.Exceptions.OperationCanceledException` when the user presses Escape during PickObject. The Revit exception passes through the catch silently, causing an unhandled exception dialog in Revit.

**Why it happens:** Both exceptions exist, and C# `using` directives can silently resolve to the wrong one. `catch (OperationCanceledException)` without a full namespace may catch the System version, not the Revit version.

**How to avoid:** Always use the fully qualified name:
```csharp
catch (Autodesk.Revit.Exceptions.OperationCanceledException)
```

**Warning signs:** Unhandled exception popup in Revit when pressing Escape during pick mode.

### Pitfall 2: AppearanceAssetEditScope Without Open Transaction

**What goes wrong:** `scope.Commit()` throws `InvalidOperationException` if no Transaction is open.

**Why it happens:** The edit scope has its own lifecycle (Start/Commit/Cancel) that is separate from Transactions. Developers assume the scope handles its own persistence.

**How to avoid:** Always wrap in Transaction. The Transaction can be started before or after `scope.Start()`, but MUST be open when `scope.Commit()` is called.

**Warning signs:** `InvalidOperationException: "No open transaction exists"` at scope.Commit().

### Pitfall 3: SurfaceForegroundPatternColor Bug (REVIT-134700)

**What goes wrong:** Setting `Material.SurfaceForegroundPatternColor` to the same value as `Material.Color` silently fails or behaves unexpectedly.

**Why it happens:** Known Revit API bug REVIT-134700, reported since 2018. The setter compares against Material.Color internally and skips the assignment if they match.

**How to avoid:** If the user wants the pattern color to equal the material color, either: (a) accept the limitation and document it, or (b) set Material.Color to a slightly different value first, then set both. For Phase 4, option (a) is acceptable since color editing targets the pattern color specifically.

**Warning signs:** Color appears to not change after edit. The old color persists even though Transaction committed successfully.

### Pitfall 4: SetValueAsColor Throws on Some Material Schemas

**What goes wrong:** `AssetPropertyDoubleArray4d.SetValueAsColor()` throws an exception for certain material schema types (e.g., some Ceramic or Metal assets).

**Why it happens:** Not all appearance asset schemas support the Color conversion path. The exception is undocumented for specific schemas.

**How to avoid:** Use `SetValueAsDoubles(new double[] { r/255.0, g/255.0, b/255.0, 1.0 })` instead of `SetValueAsColor()`. SetValueAsDoubles is more universally compatible across all asset schemas.

**Warning signs:** Exception only on certain materials, works fine on others. Difficult to reproduce without a variety of material schemas.

### Pitfall 5: Material Thumbnail Path is Unreliable

**What goes wrong:** `AppearanceAssetElement` may have no thumbnail, or the thumbnail path points to a temp file that no longer exists, or the path is relative to an Autodesk library folder that may not exist on the machine.

**Why it happens:** Revit generates thumbnails lazily. The thumbnail is only created/updated when the Rendering tab is opened in the Material Editor UI, or when certain internal events trigger regeneration.

**How to avoid:** Treat thumbnail as best-effort: check if path exists and the file is readable. If not, use the fallback colored Rectangle (D-18). Never block or throw on missing thumbnails.

**Warning signs:** Empty preview images. File paths that start with "Mats\" (relative to Autodesk library) or temp paths like "MaterialThumbnails_PID_xxxx".

### Pitfall 6: Window Not Re-Shown After Pick Exception

**What goes wrong:** An unexpected exception during PickObject (not OperationCanceledException) causes the WPF window to remain hidden. The user cannot see the add-in UI.

**Why it happens:** Window.Hide() was called before PickObject, but the exception handler forgets to call Show().

**How to avoid:** Always use `try/finally` to guarantee window re-display:
```csharp
try { /* pick */ }
catch (Autodesk.Revit.Exceptions.OperationCanceledException) { /* graceful */ }
finally { mainWindow?.Show(); }
```

## Code Examples

### MaterialDetailsDto Definition

```csharp
// Models/MaterialDetailsDto.cs
namespace Olympe.MaterialManager.Models;

public class MaterialDetailsDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ColorArgb { get; set; }
    public string PatternName { get; set; } = string.Empty;
    public bool HasAppearanceAsset { get; set; }
    public bool TintEnabled { get; set; }
    public int TintColorArgb { get; set; }
    public string? ThumbnailPath { get; set; }
}
```

### MaterialSelectedMessage

```csharp
// Messages/MaterialSelectedMessage.cs
using CommunityToolkit.Mvvm.Messaging.Messages;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Messages;

public class MaterialSelectedMessage : ValueChangedMessage<PresetMaterialDto?>
{
    public MaterialSelectedMessage(PresetMaterialDto? value) : base(value) { }
}
```

### MaterialEditedMessage

```csharp
// Messages/MaterialEditedMessage.cs
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Olympe.MaterialManager.Messages;

public class MaterialEditedMessage : ValueChangedMessage<long>
{
    public MaterialEditedMessage(long materialIdValue) : base(materialIdValue) { }
}
```

### Request DTOs for Material Edits

```csharp
// One DTO per edit type for type safety

public class EditMaterialNameRequestDto
{
    public long MaterialIdValue { get; set; }
    public string NewName { get; set; } = string.Empty;
}

public class EditMaterialDescriptionRequestDto
{
    public long MaterialIdValue { get; set; }
    public string NewDescription { get; set; } = string.Empty;
}

public class EditMaterialColorRequestDto
{
    public long MaterialIdValue { get; set; }
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
}

public class EditMaterialTintRequestDto
{
    public long MaterialIdValue { get; set; }
    public bool TintEnabled { get; set; }
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
}
```

### Material Editor ViewModel (Sub-VM Approach)

```csharp
// Recommended: separate sub-ViewModel for material editor section
public partial class MaterialEditorViewModel : ObservableObject
{
    private readonly RevitEventBridge? _eventBridge;
    private long _currentMaterialIdValue = -1;

    [ObservableProperty] private string _materialName = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private int _colorArgb;
    [ObservableProperty] private string _patternName = string.Empty;
    [ObservableProperty] private bool _hasAppearanceAsset;
    [ObservableProperty] private bool _tintEnabled;
    [ObservableProperty] private int _tintColorArgb;
    [ObservableProperty] private bool _isVisible; // Collapse section when no material selected

    public MaterialEditorViewModel(RevitEventBridge eventBridge)
    {
        _eventBridge = eventBridge;
        WeakReferenceMessenger.Default.Register<MaterialSelectedMessage>(this, OnMaterialSelected);
    }

    private void OnMaterialSelected(object recipient, MaterialSelectedMessage msg)
    {
        if (msg.Value == null)
        {
            IsVisible = false;
            return;
        }
        _currentMaterialIdValue = msg.Value.MaterialElementIdValue;
        FetchMaterialDetails();
    }

    private void FetchMaterialDetails()
    {
        _eventBridge?.MakeRequest(RevitRequestType.GetMaterialDetails, _currentMaterialIdValue, result =>
        {
            if (result is MaterialDetailsDto dto)
            {
                MaterialName = dto.Name;
                Description = dto.Description;
                ColorArgb = dto.ColorArgb;
                PatternName = dto.PatternName;
                HasAppearanceAsset = dto.HasAppearanceAsset;
                TintEnabled = dto.TintEnabled;
                TintColorArgb = dto.TintColorArgb;
                IsVisible = true;
                // Thumbnail handling: dto.ThumbnailPath
            }
        });
    }
}
```

### XAML Material Editor Section (in RightPanelView.xaml)

```xml
<!-- Below the preset TreeView, add: -->
<Border DockPanel.Dock="Bottom"
        Background="{StaticResource SurfaceBrush}"
        Padding="8"
        Margin="0,8,0,0"
        Visibility="{Binding MaterialEditorVM.IsVisible,
                     Converter={StaticResource BoolToVis}}">
    <StackPanel>
        <TextBlock Text="Editeur de materiau"
                   FontWeight="SemiBold"
                   Foreground="{StaticResource AccentBrush}"
                   Margin="0,0,0,8" />

        <!-- Preview: colored rectangle fallback -->
        <Rectangle Width="60" Height="60"
                   RadiusX="4" RadiusY="4"
                   HorizontalAlignment="Left"
                   Margin="0,0,0,8">
            <Rectangle.Fill>
                <SolidColorBrush Color="{Binding MaterialEditorVM.ColorArgb,
                    Converter={StaticResource ArgbToColorConverter}}" />
            </Rectangle.Fill>
        </Rectangle>

        <!-- Name -->
        <TextBlock Text="Nom :" Foreground="{StaticResource TextSecondaryBrush}" />
        <TextBox Text="{Binding MaterialEditorVM.MaterialName, UpdateSourceTrigger=LostFocus}" />

        <!-- Description -->
        <TextBlock Text="Description :" Foreground="{StaticResource TextSecondaryBrush}" />
        <TextBox Text="{Binding MaterialEditorVM.Description, UpdateSourceTrigger=LostFocus}" />

        <!-- Pattern (read-only) -->
        <TextBlock Text="Motif :" Foreground="{StaticResource TextSecondaryBrush}" />
        <TextBlock Text="{Binding MaterialEditorVM.PatternName}" />

        <!-- Color RGB -->
        <TextBlock Text="Couleur de surface :" Foreground="{StaticResource TextSecondaryBrush}" />
        <!-- Three TextBoxes for R, G, B -->

        <!-- Tint section (disabled when HasAppearanceAsset = false) -->
        <!-- ... -->
    </StackPanel>
</Border>
```

### LeftPanelView Pick Button

```xml
<!-- In LeftPanelView.xaml, add after the "Ajouter" button: -->
<Button Content="Ajouter par clic"
        Command="{Binding AjouterParClicCommand}"
        Margin="0,4,0,0"
        HorizontalAlignment="Left"
        ToolTip="{Binding PickButtonTooltip}" />
```

## Appearance Asset Property Names Reference

| Property String Name | Type | Purpose |
|---------------------|------|---------|
| `common_Tint_toggle` | AssetPropertyBoolean | Enable/disable tint on the material |
| `common_Tint_color` | AssetPropertyDoubleArray4d | Tint color as RGBA doubles (0.0-1.0) |
| `generic_diffuse` | AssetPropertyDoubleArray4d | Base diffuse color for Generic schema |
| `generic_is_metal` | AssetPropertyBoolean | Metal toggle for Generic schema |
| `thumbnail` | AssetPropertyString | Thumbnail image file path (may be relative or absolute) |

**Note on D-05 correction:** The CONTEXT.md mentions `generic_is_metal` and `generic_diffuse_image_modifier` for tint enable. However, the standard Revit API approach for tint is `common_Tint_toggle` + `common_Tint_color`, which works across all material schemas (not just Generic). The `generic_*` properties are schema-specific. **Use `common_Tint_toggle` / `common_Tint_color` as they are cross-schema and match the "Teinte" checkbox in the Revit Material Editor UI.** Confidence: HIGH -- verified across multiple official sources and community documentation.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `asset["property_name"]` bracket operator | `asset.FindByName("property_name")` | Revit 2019+ | Bracket operator deprecated; FindByName is current |
| `SetValueAsColor(Color)` | `SetValueAsDoubles(double[])` | Revit 2018.2+ | SetValueAsDoubles more reliable across schemas |
| `Material.SurfacePatternColor` | `Material.SurfaceForegroundPatternColor` | Revit 2019 | Property renamed with Foreground/Background split |
| `PickObject` with modal dialog | `PickObject` with ExternalEvent + window hide/show | Revit 2014+ | Standard modeless pattern |

**Deprecated/outdated:**
- `Asset["property_name"]` bracket operator: deprecated, use `FindByName()` instead
- `Material.SurfacePatternColor`: replaced by `SurfaceForegroundPatternColor` / `SurfaceBackgroundPatternColor` in Revit 2019
- `AssetPropertyDoubleArray4d.SetValueAsColor()`: still available but throws on some schemas. Prefer `SetValueAsDoubles()`.

## Open Questions

1. **D-05 tint property names vs common tint**
   - What we know: `common_Tint_toggle` and `common_Tint_color` are cross-schema. `generic_diffuse` and `generic_is_metal` are Generic-schema-only.
   - What's unclear: CONTEXT.md says "generic_is_metal or generic_diffuse_image_modifier" for tint enable. This conflicts with the standard `common_Tint_toggle` approach.
   - Recommendation: Use `common_Tint_toggle` / `common_Tint_color` which map to the Revit Material Editor "Teinte" checkbox. These work on ALL schemas (Generic, Ceramic, Metal, etc.), not just Generic. If a specific material has no `common_Tint_toggle` property, `FindByName` returns null and we handle gracefully (D-06).

2. **Material thumbnail reliability**
   - What we know: `AppearanceAssetElement` has a `ThumbnailFile` property (string, nullable). The path may be relative, absolute temp, or absent.
   - What's unclear: How often thumbnails actually exist in practice. The path may reference Autodesk library folders that are not installed.
   - Recommendation: Check File.Exists on the returned path. If relative, attempt to resolve against common Autodesk library locations. If not found, use colored Rectangle fallback (D-18). Do not block on this.

3. **Window.Show() vs Window.ShowDialog() after pick**
   - What we know: The WPF window is modeless (Show()). After hiding and re-showing, we should use Show() again (not ShowDialog which would make it modal).
   - Recommendation: Use `mainWindow.Show()` in the finally block. The window was originally shown modeless and should remain modeless.

## Sources

### Primary (HIGH confidence)
- [Revit API Docs: AppearanceAssetEditScope](https://www.revitapidocs.com/2019/743c74ba-12de-4d77-a677-325229525955.htm) -- Class API, Start/Commit/Cancel lifecycle
- [Revit API Docs 2026: Commit Method](https://www.revitapidocs.com/2026/320a2602-0a2c-df20-01dc-2ede9d62afdd.htm) -- updateOpenViews boolean parameter documentation
- [Revit API Docs: Material Class](https://www.revitapidocs.com/2019/2ec33007-7a2a-f86a-009b-d4c5d235a307.htm) -- Material property list
- [Revit API Docs: SurfaceForegroundPatternColor](https://www.revitapidocs.com/2019/d9019c51-64ee-caab-aa04-51b594074ec1.htm) -- Read/write property, ArgumentNullException on null
- [Revit API Docs: ThumbnailFile Property](https://www.revitapidocs.com/2015/8eab2a3d-8282-95e6-a012-19d652ebe8eb.htm) -- String path to thumbnail image
- [Autodesk Help: General Material Information (2025)](https://help.autodesk.com/cloudhelp/2025/PTB/Revit-API/files/Revit_API_Developers_Guide/Revit_Geometric_Elements/Material/Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Material_General_Material_Information_html.html) -- AppearanceAssetEditScope lifecycle with Transaction
- [Autodesk AU: SD124625 New API to Modify Visual Appearance](https://static.au-uw2-prd.autodesk.com/Class_Handout_SD124625_New_API_to_Modify_Visual_Appearance_of_Materials_in_Revit_Boris_Shafiro.pdf) -- Boris Shafiro presentation on edit scope
- [Autodesk Forum: Material.SurfacePatternColor bug REVIT-134700](https://forums.autodesk.com/t5/revit-api-forum/can-t-set-material-surfacepatterncolor-property/td-p/8120896) -- Known API limitation

### Secondary (MEDIUM confidence)
- [Building Coder: Modifying Material Visual Appearance](https://thebuildingcoder.typepad.com/blog/2017/11/modifying-material-visual-appearance.html) -- Community patterns for asset editing (site currently down, content verified via cached results)
- [Building Coder: PickPoint with WPF](https://jeremytammik.github.io/tbc/a/1377_wpf_thread_pickpoint.html) -- Window hide/show pattern around pick operations
- [Autodesk Forum: Material preview image path](https://forums.autodesk.com/t5/revit-api-forum/get-an-up-to-date-material-preview-image-file-path/td-p/7731827) -- Thumbnail unreliability, temp paths
- [Autodesk Forum: SetValueAsColor throws on some materials](https://forums.autodesk.com/t5/revit-api-forum/assetpropertydoublearray4d-setvalueascolor-throws-exception-for/td-p/9150135) -- Recommends SetValueAsDoubles
- [Autodesk Forum: Rename Material](https://forums.autodesk.com/t5/revit-api-forum/rename-material/td-p/4331574) -- Material.Name is writable in Transaction
- [Revit SDK: ModelessForm_ExternalEvent sample](https://github.com/varolomer/RevitWPF) -- Reference implementation for modeless form + ExternalEvent
- [BIM Matters: External Events and Modeless Dialogs](https://bimmatters.wordpress.com/2018/08/05/revit-api-external-events/) -- Pattern documentation

### Tertiary (LOW confidence)
- Material thumbnail path resolution for relative paths (library folder detection) -- needs validation against actual installed Autodesk libraries on target machines

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, pure extension of existing patterns
- Architecture: HIGH -- follows established RevitEventBridge + DTO + Messenger patterns from Phases 1-3
- Material editing API: HIGH -- Material.Name, SurfaceForegroundPatternColor, BuiltInParameter.ALL_MODEL_DESCRIPTION all verified
- AppearanceAssetEditScope: HIGH -- well-documented pattern since Revit 2018.1, common_Tint_toggle/color verified
- PickObject from modeless: HIGH -- canonical pattern (hide/pick/show), documented extensively
- Pitfalls: HIGH -- REVIT-134700 confirmed, OperationCanceledException namespace issue well-documented
- Material thumbnail: LOW-MEDIUM -- ThumbnailFile exists but reliability is uncertain; fallback strategy required

**Research date:** 2026-04-11
**Valid until:** 2026-05-11 (stable APIs, no breaking changes expected)

---

*Phase: 04-material-editing-and-3d-pick*
*Research completed: 2026-04-11*
