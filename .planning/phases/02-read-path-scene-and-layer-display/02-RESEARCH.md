# Phase 2: Read Path -- Scene and Layer Display - Research

**Researched:** 2026-04-11
**Domain:** Revit API data reading (CompoundStructure, Material parameters, FilteredElementCollector), WPF MVVM (TreeView grouping, Messenger, ListBox multi-select)
**Confidence:** HIGH

## Summary

Phase 2 implements the read-only data path: scene management (create, switch, populate with types) in the left panel, and CompoundStructure layer / material parameter display in the center panel. The existing codebase provides a solid foundation: `RevitEventBridge` with lock-protected request/response, `RevitRequestType` enum, `ElementIdHelper` for safe `long`-based ElementId access, and shell ViewModels for both panels. The phase requires extending the bridge with four new request types, creating five new DTO classes, and building TreeView grouping with custom sort in the left panel plus conditional layer/parameter display in the center panel.

The Revit API surface for this phase is stable across 2024/2025/2026, with one exception: Revit 2026 allows CompoundStructures without core layers, so code must not assume a core layer exists. French category names are obtained via `LabelUtils.GetLabelFor(BuiltInCategory)` which returns localized strings based on Revit's running language. Layer widths are stored internally in feet and must be converted via `UnitUtils.ConvertFromInternalUnits(width, UnitTypeId.Millimeters)`.

**Primary recommendation:** Build the four RevitEventBridge handlers first (GetFamilyList, GetTypeList, GetLayersForType, GetMaterialParametersForType), then the DTO/ViewModel layer, then the XAML views. Use CommunityToolkit.Mvvm `WeakReferenceMessenger` for LeftPanel-to-CenterPanel type selection notification.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** A Scene is a `SceneDto` with `Name` (string) and `Types` (ObservableCollection<SceneTypeDto>). SceneTypeDto contains: `ElementIdValue` (long), `FamilyName` (string), `TypeName` (string), `CategoryName` (string), `HasCompoundStructure` (bool). No Revit types in the DTO.
- **D-02:** Scenes are stored in LeftPanelViewModel as ObservableCollection<SceneDto>. ActiveScene is the currently selected scene.
- **D-03:** Scene creation uses a simple TextBox + Button in the left panel header. No dialog.
- **D-04:** TreeView uses CollectionViewSource with GroupDescription by CategoryName. Groups display as expandable headers.
- **D-05:** Custom IComparer sort order: Murs (Walls) first, Sols (Floors) second, then alphabetical. Within each category, types are sorted alphabetically by TypeName.
- **D-06:** TreeView items display "{FamilyName} : {TypeName}" format. Category group headers display the French category name.
- **D-07:** Add new RevitRequestType enum values: GetFamilyList, GetTypeList, GetLayersForType, GetMaterialParametersForType.
- **D-08:** GetFamilyList returns List<FamilyCategoryDto> grouped by category. GetTypeList(familyId) returns List<SceneTypeDto>.
- **D-09:** The dropdown approach for adding types: first ComboBox selects a category+family, second ComboBox shows types for that family. "Ajouter" button adds selected type to scene.
- **D-10:** Remove type from scene: right-click context menu or Delete key on selected TreeView item.
- **D-11:** For types with CompoundStructure: CenterPanelViewModel exposes ObservableCollection<LayerDto>. LayerDto contains: LayerIndex (int), Function (string, French), Width (double, in mm), MaterialName (string), MaterialElementIdValue (long).
- **D-12:** Display as ListBox with DataTemplate: "[Fonction] -- [Epaisseur] mm -- [Materiau]". Each item is selectable.
- **D-13:** Layer function names mapped to French: Finish 1 -> "Finition 1", Finish 2 -> "Finition 2", Substrate -> "Substrat", Core -> "Noyau", Membrane -> "Membrane", Structure -> "Structure".
- **D-14:** For types without CompoundStructure: CenterPanelViewModel exposes ObservableCollection<MaterialParamDto>. MaterialParamDto contains: ParameterName (string), CurrentMaterialName (string), CurrentMaterialIdValue (long), ParameterDefinitionName (string).
- **D-15:** Display as ListBox with DataTemplate: "[Nom parametre] -- [Materiau actuel]". Each item is selectable.
- **D-16:** Parameter discovery uses Element.Parameters iteration, filtering for StorageType.ElementId where the value is a Material.
- **D-17:** Center panel ListBox uses SelectionMode=Extended for multi-selection (Ctrl+click, Shift+click).
- **D-18:** Selected items exposed as ObservableCollection or IList in CenterPanelViewModel for downstream Set Mat usage.
- **D-19:** When a type is selected in the TreeView (left panel), LeftPanelViewModel raises an event/message to trigger CenterPanelViewModel to fetch layers/parameters via ExternalEvent.
- **D-20:** Use CommunityToolkit.Mvvm Messenger (WeakReferenceMessenger) for inter-ViewModel communication.

### Claude's Discretion
- Exact DTO property types (nullable vs non-nullable)
- Whether to cache Revit data or fetch each time a type is selected
- Error display approach when Revit data fetch fails
- Loading indicator pattern while waiting for ExternalEvent response

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SCENE-01 | L'utilisateur peut creer une scene active avec un nom personnalise | TextBox + Button in left panel header (D-03). SceneDto model (D-01). ObservableCollection<SceneDto> in LeftPanelViewModel (D-02). |
| SCENE-02 | L'utilisateur peut switcher entre plusieurs scenes actives via un selecteur | ComboBox bound to Scenes collection, SelectedItem = ActiveScene. CollectionViewSource rebinds TreeView on switch. |
| SCENE-03 | L'utilisateur peut ajouter des familles/types a la scene via un mode liste | Two-ComboBox pattern (D-09): GetFamilyList -> GetTypeList -> Add to ActiveScene.Types. FilteredElementCollector patterns documented below. |
| SCENE-05 | L'utilisateur peut retirer un type de la scene active | Right-click context menu or Delete key on TreeView item (D-10). Remove from ActiveScene.Types ObservableCollection. |
| SCENE-06 | Le panneau gauche affiche un TreeView des familles/types de la scene active | TreeView with CollectionViewSource grouping by CategoryName (D-04). HierarchicalDataTemplate for groups. |
| SCENE-07 | Le TreeView trie les Murs et Sols en tete, le reste en ordre alphabetique | Custom IComparer (D-05). CategorySortComparer puts "Murs" first, "Sols" second, rest alphabetical. |
| SCENE-08 | La selection d'un type dans le TreeView met a jour le panneau centre | WeakReferenceMessenger sends TypeSelectedMessage from LeftPanelViewModel (D-19, D-20). CenterPanelViewModel receives and triggers ExternalEvent. |
| LAYER-01 | Pour un type a couches, le panneau centre affiche la liste des couches CompoundStructure | GetLayersForType handler reads CompoundStructure.GetLayers() (D-11). LayerDto ObservableCollection in CenterPanelViewModel. |
| LAYER-02 | Chaque couche affiche sa fonction, son epaisseur et le materiau actuellement assigne | LayerDto fields: Function (French), Width (mm), MaterialName (D-11, D-12). French function mapping (D-13). |
| LAYER-03 | Pour une famille chargee sans couches, le panneau centre affiche la liste des parametres de type Material | GetMaterialParametersForType handler discovers material params via SpecTypeId.Reference.Material (D-14, D-16). |
| LAYER-04 | L'utilisateur peut selectionner une ou plusieurs couches/parametres dans le panneau centre | ListBox SelectionMode=Extended (D-17). Selected items exposed for downstream use (D-18). |
| LAYER-05 | La selection multiple est supportee (Ctrl+clic, Shift+clic) | WPF ListBox SelectionMode=Extended natively supports Ctrl+click and Shift+click (D-17). |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **MVVM strict**: No code-behind business logic, RelayCommand, ObservableCollection, one ViewModel per view
- **Naming**: PascalCase classes/properties, _camelCase private fields
- **IExternalEventHandler** for all Revit interaction from UI
- **No Revit API types in ViewModels** (INFRA-07): DTOs only, `long` for ElementId values
- **Interface language**: French
- **CommunityToolkit.Mvvm 8.4.2**: ObservableObject, [ObservableProperty], [RelayCommand]
- **ElementId**: Always use `ElementIdHelper.GetValue()` / `ElementIdHelper.FromValue()` -- never `.IntegerValue`
- **Multi-target**: net48 + net8.0-windows single csproj with REVIT2024 / REVIT2025_OR_GREATER defines

## Standard Stack

### Core (already installed -- no new packages needed)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| CommunityToolkit.Mvvm | 8.4.2 | MVVM + Messenger | Already in csproj. WeakReferenceMessenger for inter-VM comms. |
| Nice3point.Revit.Api.RevitAPI | 2024.3.30 / 2026.4.0 | Revit API (CompoundStructure, FilteredElementCollector, Material, LabelUtils) | Already in csproj. |
| PolySharp | 1.15.0 | C# 12 polyfills for net48 | Already in csproj. |
| Microsoft.Xaml.Behaviors.Wpf | 1.1.142 | EventTrigger for Delete key, SelectedItems binding | Already in csproj. |

### No New Packages Required

Phase 2 uses only existing dependencies. All Revit API classes needed (FilteredElementCollector, CompoundStructure, LabelUtils, UnitUtils, Material, SpecTypeId.Reference.Material) are in the already-referenced RevitAPI packages. WeakReferenceMessenger is part of CommunityToolkit.Mvvm 8.4.2.

## Architecture Patterns

### New Files to Create

```
OlympeMaterialManager/src/OlympeMaterialManager/
  Models/
    SceneDto.cs                    # Scene with Name + Types collection
    SceneTypeDto.cs                # Type entry in a scene
    FamilyCategoryDto.cs           # Family grouped by category (for ComboBox)
    LayerDto.cs                    # CompoundStructure layer data
    MaterialParamDto.cs            # Material parameter data
  Messages/
    TypeSelectedMessage.cs         # Messenger message for type selection
  Helpers/
    CategorySortComparer.cs        # IComparer for Murs-first, Sols-second sort
    LayerFunctionMapper.cs         # Maps CompoundStructureLayerFunction to French
```

### Files to Modify

```
  Events/
    RevitRequestType.cs            # Add 4 new enum values
    RevitEventBridge.cs            # Add 4 new handler methods
  ViewModels/
    LeftPanelViewModel.cs          # Full implementation: scenes, TreeView, ComboBoxes
    CenterPanelViewModel.cs        # Full implementation: layers/params display
    MainWindowViewModel.cs         # Pass EventBridge to child VMs
  Views/
    LeftPanelView.xaml             # TreeView + scene UI + add-type ComboBoxes
    CenterPanelView.xaml           # Conditional layer/param ListBox
```

### Pattern 1: RevitEventBridge Extension with Typed Request Data

**What:** Add new cases to the ProcessRequest switch with typed data passing.
**When:** Each new Revit API operation needs a bridge handler.
**How:** The existing bridge uses `object? data` parameter. Cast to appropriate type in each handler.

```csharp
// In RevitRequestType.cs -- add:
GetFamilyList,            // data: null, returns: List<FamilyCategoryDto>
GetTypeList,              // data: long (familyElementId), returns: List<SceneTypeDto>
GetLayersForType,         // data: long (typeElementId), returns: List<LayerDto>
GetMaterialParametersForType  // data: long (typeElementId), returns: List<MaterialParamDto>

// In RevitEventBridge.cs ProcessRequest switch -- add:
case RevitRequestType.GetFamilyList:
    result = HandleGetFamilyList(uiApp);
    break;
case RevitRequestType.GetTypeList:
    result = HandleGetTypeList(uiApp, (long)data!);
    break;
case RevitRequestType.GetLayersForType:
    result = HandleGetLayersForType(uiApp, (long)data!);
    break;
case RevitRequestType.GetMaterialParametersForType:
    result = HandleGetMaterialParametersForType(uiApp, (long)data!);
    break;
```

**Confidence: HIGH** -- follows the established pattern already in use for GetDocumentInfo.

### Pattern 2: WeakReferenceMessenger for Type Selection

**What:** When user selects a type in the left panel TreeView, notify the center panel to fetch layer/parameter data.
**When:** Every type selection change.
**How:** Define a message class, send from LeftPanelViewModel, receive in CenterPanelViewModel.

```csharp
// Messages/TypeSelectedMessage.cs
using CommunityToolkit.Mvvm.Messaging.Messages;

public class TypeSelectedMessage : ValueChangedMessage<SceneTypeDto?>
{
    public TypeSelectedMessage(SceneTypeDto? value) : base(value) { }
}

// In LeftPanelViewModel -- when SelectedType changes:
partial void OnSelectedTypeChanged(SceneTypeDto? value)
{
    WeakReferenceMessenger.Default.Send(new TypeSelectedMessage(value));
}

// In CenterPanelViewModel constructor:
WeakReferenceMessenger.Default.Register<TypeSelectedMessage>(this, (r, m) =>
{
    ((CenterPanelViewModel)r).OnTypeSelected(m.Value);
});
```

**Confidence: HIGH** -- verified against official CommunityToolkit.Mvvm docs. WeakReferenceMessenger.Default is thread-safe singleton, no manual unregister needed (weak refs handle GC).

### Pattern 3: TreeView with CollectionViewSource Grouping

**What:** Display a flat ObservableCollection<SceneTypeDto> as grouped TreeView items, grouped by CategoryName.
**When:** Displaying the active scene's types in the left panel.
**How:** CollectionViewSource in XAML resources with PropertyGroupDescription.

```xml
<!-- In LeftPanelView.xaml Resources -->
<CollectionViewSource x:Key="GroupedTypes" Source="{Binding ActiveSceneTypes}">
    <CollectionViewSource.GroupDescriptions>
        <PropertyGroupDescription PropertyName="CategoryName" />
    </CollectionViewSource.GroupDescriptions>
    <CollectionViewSource.SortDescriptions>
        <!-- SortDescriptions don't support IComparer directly.
             Use CustomSort on the view in code-behind or VM. -->
    </CollectionViewSource.SortDescriptions>
</CollectionViewSource>

<!-- TreeView binding -->
<TreeView ItemsSource="{Binding Source={StaticResource GroupedTypes}}">
    <TreeView.GroupStyle>
        <GroupStyle>
            <GroupStyle.HeaderTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding Name}"
                               FontWeight="SemiBold"
                               Foreground="{StaticResource AccentBrush}" />
                </DataTemplate>
            </GroupStyle.HeaderTemplate>
        </GroupStyle>
    </TreeView.GroupStyle>
    <TreeView.ItemTemplate>
        <DataTemplate>
            <TextBlock>
                <Run Text="{Binding FamilyName}" />
                <Run Text=" : " />
                <Run Text="{Binding TypeName}" />
            </TextBlock>
        </DataTemplate>
    </TreeView.ItemTemplate>
</TreeView>
```

**Important note on custom sorting:** WPF CollectionViewSource.SortDescriptions only supports SortDescription (property + direction). For the custom IComparer (Murs first, Sols second), you must set `CustomSort` on the `ListCollectionView` in code. The cleanest MVVM approach is to set it from the ViewModel via the view's Loaded event or by accessing the view from the CollectionViewSource:

```csharp
// In LeftPanelViewModel or code-behind on Loaded:
var view = (ListCollectionView)CollectionViewSource.GetDefaultView(ActiveSceneTypes);
view.CustomSort = new CategorySortComparer();
```

**Confidence: HIGH** -- standard WPF pattern, verified via multiple sources.

### Pattern 4: Conditional Center Panel Display

**What:** Show layers ListBox when type has CompoundStructure, material parameters ListBox when it does not.
**When:** Type is selected in the TreeView.
**How:** Use Visibility binding on two panels based on a `HasCompoundStructure` bool in CenterPanelViewModel.

```xml
<!-- Layers mode -->
<StackPanel Visibility="{Binding ShowLayers, Converter={StaticResource BoolToVisConverter}}">
    <TextBlock Text="Couches" FontWeight="SemiBold" />
    <ListBox ItemsSource="{Binding Layers}" SelectionMode="Extended" />
</StackPanel>

<!-- Parameters mode -->
<StackPanel Visibility="{Binding ShowParameters, Converter={StaticResource BoolToVisConverter}}">
    <TextBlock Text="Parametres materiaux" FontWeight="SemiBold" />
    <ListBox ItemsSource="{Binding MaterialParams}" SelectionMode="Extended" />
</StackPanel>
```

**Confidence: HIGH** -- standard WPF visibility toggle.

### Anti-Patterns to Avoid

- **Storing Revit Element references in ViewModels:** Store only `long` ElementId values via `ElementIdHelper.GetValue()`. Re-fetch inside bridge handlers.
- **Calling FilteredElementCollector from ViewModel:** All collector calls go through RevitEventBridge handlers.
- **Using TreeView SelectedItem binding directly:** TreeView.SelectedItem is read-only in WPF. Use EventTrigger/Behavior or TreeViewItem.IsSelected with a wrapper, or handle SelectionChanged in code-behind routing to ViewModel command.
- **Assuming core layer exists:** In Revit 2026, CompoundStructure can have zero core layers. Always iterate all layers without index assumptions.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| French category names | String dictionary mapping BuiltInCategory -> French | `LabelUtils.GetLabelFor(BuiltInCategory)` | Returns localized names in Revit's current language automatically. Works across all versions. |
| Unit conversion (feet to mm) | Manual multiplication by 304.8 | `UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters)` | Handles precision correctly. Standard across all Revit versions 2022+. |
| Inter-ViewModel messaging | Custom event aggregator | `WeakReferenceMessenger.Default` from CommunityToolkit.Mvvm | Already a dependency. Weak references, thread-safe, no manual cleanup needed. |
| TreeView grouping | Manual ViewModel hierarchy with category parent nodes | `CollectionViewSource` + `PropertyGroupDescription` in XAML | Built into WPF. Keeps data model flat while presenting hierarchy. |
| Material parameter type detection | Check parameter name strings | `param.Definition.GetDataType() == SpecTypeId.Reference.Material` | Reliable, language-independent. Works across all parameter naming conventions. |

**Key insight:** The Revit API already provides localization (LabelUtils), unit conversion (UnitUtils), and parameter type identification (SpecTypeId) -- using these instead of hand-rolled solutions avoids localization bugs, rounding errors, and fragile string matching.

## Common Pitfalls

### Pitfall 1: CompoundStructure Copy Semantics (Read Path)
**What goes wrong:** `GetCompoundStructure()` returns a COPY. For Phase 2 this is read-only so no risk of lost writes, but developers may cache the CompoundStructure object and assume it updates when the model changes.
**Why it happens:** API returns a snapshot, not a live reference.
**How to avoid:** Fetch fresh CompoundStructure data each time a type is selected (or invalidate cache on document change). Never persist a CompoundStructure object across requests.
**Warning signs:** Stale layer data after model changes.

### Pitfall 2: Revit 2026 No-Core-Layer CompoundStructures
**What goes wrong:** Code assumes every CompoundStructure has at least one core layer. In Revit 2026, finish-only walls are valid.
**Why it happens:** All pre-2026 examples assume core layer exists.
**How to avoid:** Iterate `GetLayers()` without assuming fixed indices. Map all `CompoundStructureLayerFunction` values including when no core layers present.
**Warning signs:** `IndexOutOfRangeException` or empty layer list when processing 2026 models.

### Pitfall 3: MaterialId == ElementId.InvalidElementId for Category Default
**What goes wrong:** `CompoundStructureLayer.MaterialId` returns `InvalidElementId` (-1) when the layer uses the category's default material. Treating this as "no material" confuses users.
**Why it happens:** Revit uses sentinel value for "inherit from category."
**How to avoid:** When `MaterialId` is invalid, display "< Par categorie >" in the UI. Use `ElementIdHelper.GetValue()` and check against `ElementId.InvalidElementId.Value` (which is -1).
**Warning signs:** Blank material name in layer display.

### Pitfall 4: TreeView SelectedItem Is Read-Only
**What goes wrong:** Attempting to bind TreeView.SelectedItem with TwoWay binding fails because it is read-only in WPF.
**Why it happens:** WPF TreeView design limitation.
**How to avoid:** Use one of these approaches:
  1. `TreeViewItem.IsSelected` with attached behavior
  2. `EventTrigger` from Microsoft.Xaml.Behaviors.Wpf to invoke a command on SelectionChanged
  3. Minimal code-behind in the view that calls a ViewModel method (acceptable per MVVM conventions for view-only concerns)
**Warning signs:** Binding error in output window, selection never reaching ViewModel.

### Pitfall 5: FilteredElementCollector Slow Filters
**What goes wrong:** Using LINQ `.Where()` before `.ToList()` on the collector forces slow element-by-element evaluation instead of using Revit's optimized internal filters.
**Why it happens:** Developers chain LINQ early, not realizing `OfClass`/`OfCategory` are quick filters and `.Where()` is a slow filter.
**How to avoid:** Always apply `OfClass()` and `OfCategory()` FIRST, then `.ToList()` or `.Cast<T>()`, THEN any LINQ post-filtering.
**Warning signs:** Multi-second delays when opening the family/type ComboBox.

### Pitfall 6: UnitTypeId vs DisplayUnitType for Unit Conversion
**What goes wrong:** On Revit 2024 (net48), `DisplayUnitType.DUT_MILLIMETERS` is deprecated but still functional. On 2025+ it may not compile depending on API version.
**Why it happens:** Revit 2022+ migrated from `DisplayUnitType` to `UnitTypeId`.
**How to avoid:** Use `UnitTypeId.Millimeters` on all versions (available since Revit 2021). The Nice3point packages for 2024 include `UnitTypeId`.
**Warning signs:** Compilation warnings about deprecated `DisplayUnitType`.

## Code Examples

### Revit Handler: GetFamilyList (Families grouped by category)

```csharp
// Source: Revit API docs + Building Coder collector patterns
private static List<FamilyCategoryDto> HandleGetFamilyList(UIApplication uiApp)
{
    var doc = uiApp.ActiveUIDocument?.Document;
    if (doc == null) return new List<FamilyCategoryDto>();

    var result = new List<FamilyCategoryDto>();

    // System families: Wall, Floor, Roof, Ceiling
    var systemCategories = new[]
    {
        (typeof(WallType), BuiltInCategory.OST_Walls),
        (typeof(FloorType), BuiltInCategory.OST_Floors),
        (typeof(RoofType), BuiltInCategory.OST_Roofs),
        (typeof(CeilingType), BuiltInCategory.OST_Ceilings),
    };

    foreach (var (typeClass, bic) in systemCategories)
    {
        var types = new FilteredElementCollector(doc)
            .OfClass(typeClass)
            .Cast<ElementType>()
            .ToList();

        if (types.Any())
        {
            string categoryName = LabelUtils.GetLabelFor(bic); // French name auto
            result.Add(new FamilyCategoryDto
            {
                CategoryName = categoryName,
                BuiltInCategoryValue = (long)bic,
                FamilyName = categoryName,  // System families: category IS the family
                FamilyElementIdValue = -1,  // No distinct family element for system types
                IsSystemFamily = true
            });
        }
    }

    // Loaded families: FamilySymbol grouped by Family
    var families = new FilteredElementCollector(doc)
        .OfClass(typeof(Family))
        .Cast<Family>()
        .Where(f => f.FamilyCategoryId != ElementId.InvalidElementId)
        .ToList();

    foreach (var family in families)
    {
        var symbolIds = family.GetFamilySymbolIds();
        if (symbolIds.Count == 0) continue;

        // Check if any symbol has material parameters
        var firstSymbol = doc.GetElement(symbolIds.First()) as FamilySymbol;
        if (firstSymbol == null) continue;

        string catName = family.FamilyCategory?.Name ?? "Autre";
        result.Add(new FamilyCategoryDto
        {
            CategoryName = catName,
            FamilyName = family.Name,
            FamilyElementIdValue = ElementIdHelper.GetValue(family.Id),
            IsSystemFamily = false
        });
    }

    return result;
}
```

**Confidence: HIGH** -- FilteredElementCollector.OfClass pattern is canonical Revit API.

### Revit Handler: GetLayersForType (CompoundStructure reading)

```csharp
// Source: Revit API docs, ARCHITECTURE.md patterns, Building Coder
private static List<LayerDto> HandleGetLayersForType(UIApplication uiApp, long typeIdValue)
{
    var doc = uiApp.ActiveUIDocument?.Document;
    if (doc == null) return new List<LayerDto>();

    var elementId = ElementIdHelper.FromValue(typeIdValue);
    var element = doc.GetElement(elementId);

    // System family type with CompoundStructure
    if (element is HostObjAttributes hostAttrs)
    {
        var cs = hostAttrs.GetCompoundStructure();
        if (cs == null) return new List<LayerDto>(); // Possible for certain types

        var layers = cs.GetLayers();
        var result = new List<LayerDto>(layers.Count);

        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            var matId = layer.MaterialId;
            string matName = "< Par categorie >";
            long matIdValue = ElementIdHelper.GetValue(matId);

            if (matId != ElementId.InvalidElementId)
            {
                var mat = doc.GetElement(matId);
                matName = mat?.Name ?? "< Inconnu >";
            }

            double widthMm = UnitUtils.ConvertFromInternalUnits(
                layer.Width, UnitTypeId.Millimeters);

            result.Add(new LayerDto
            {
                LayerIndex = i,
                Function = LayerFunctionMapper.ToFrench(layer.Function),
                Width = Math.Round(widthMm, 1),
                MaterialName = matName,
                MaterialElementIdValue = matIdValue
            });
        }

        return result;
    }

    return new List<LayerDto>();
}
```

**Confidence: HIGH** -- pattern verified from ARCHITECTURE.md and Revit API official docs.

### Revit Handler: GetMaterialParametersForType

```csharp
// Source: Revit API Element Material docs, SpecTypeId.Reference.Material
private static List<MaterialParamDto> HandleGetMaterialParametersForType(
    UIApplication uiApp, long typeIdValue)
{
    var doc = uiApp.ActiveUIDocument?.Document;
    if (doc == null) return new List<MaterialParamDto>();

    var elementId = ElementIdHelper.FromValue(typeIdValue);
    var element = doc.GetElement(elementId);
    if (element == null) return new List<MaterialParamDto>();

    var result = new List<MaterialParamDto>();

    foreach (Parameter param in element.Parameters)
    {
        if (param.StorageType != StorageType.ElementId) continue;
        if (param.Definition.GetDataType() != SpecTypeId.Reference.Material) continue;

        var matId = param.AsElementId();
        string matName = "< Aucun >";
        long matIdValue = ElementIdHelper.GetValue(matId);

        if (matId != ElementId.InvalidElementId)
        {
            var mat = doc.GetElement(matId);
            matName = mat?.Name ?? "< Inconnu >";
        }

        result.Add(new MaterialParamDto
        {
            ParameterName = param.Definition.Name,
            ParameterDefinitionName = param.Definition.Name,
            CurrentMaterialName = matName,
            CurrentMaterialIdValue = matIdValue
        });
    }

    return result;
}
```

**Confidence: HIGH** -- `SpecTypeId.Reference.Material` is the modern (2022+) way to detect Material parameters. Available on both net48 (2024) and net8.0 (2025/2026) via the Nice3point packages.

### LayerFunctionMapper: French Mapping

```csharp
// Source: D-13 decision + CompoundStructureLayerFunction enum values
using Autodesk.Revit.DB;

public static class LayerFunctionMapper
{
    public static string ToFrench(MaterialFunctionAssignment function)
    {
        return function switch
        {
            MaterialFunctionAssignment.Finish1 => "Finition 1",
            MaterialFunctionAssignment.Finish2 => "Finition 2",
            MaterialFunctionAssignment.Substrate => "Substrat",
            MaterialFunctionAssignment.Structure => "Structure",
            MaterialFunctionAssignment.MembraneLayer => "Membrane",
            MaterialFunctionAssignment.ThermalOrAir => "Isolation thermique / Air",
            MaterialFunctionAssignment.StructuralDeck => "Plancher structurel",
            _ => function.ToString()
        };
    }
}
```

**Note:** The `CompoundStructureLayer.Function` property returns `MaterialFunctionAssignment` enum (not `CompoundStructureLayerFunction` -- they share values but the property type is `MaterialFunctionAssignment`). Verify at implementation time.

**Confidence: MEDIUM** -- enum values verified from revitapidocs, but the exact property return type (MaterialFunctionAssignment vs CompoundStructureLayerFunction) should be confirmed against the actual Revit 2024 API assembly. The enum values themselves are stable.

### CategorySortComparer: Custom Sort

```csharp
// Source: D-05 decision
using System.Collections;

public class CategorySortComparer : IComparer
{
    private static readonly Dictionary<string, int> _priorityMap = new()
    {
        { "Murs", 0 },
        { "Sols", 1 },
    };

    public int Compare(object? x, object? y)
    {
        // CollectionViewGroup items come as CollectionViewGroup
        // when sorting groups, or as SceneTypeDto when sorting within groups
        string catX = GetCategoryName(x);
        string catY = GetCategoryName(y);

        int prioX = _priorityMap.GetValueOrDefault(catX, 100);
        int prioY = _priorityMap.GetValueOrDefault(catY, 100);

        if (prioX != prioY) return prioX.CompareTo(prioY);
        return string.Compare(catX, catY, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCategoryName(object? item)
    {
        // Adapt based on actual item type at runtime
        if (item is SceneTypeDto dto) return dto.CategoryName;
        return item?.ToString() ?? "";
    }
}
```

**Confidence: HIGH** -- standard IComparer pattern. The priority map keys ("Murs", "Sols") will match the French category names returned by `LabelUtils.GetLabelFor()` when Revit runs in French.

### WeakReferenceMessenger Usage

```csharp
// Source: Microsoft CommunityToolkit.Mvvm docs
// Message definition
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Olympe.MaterialManager.Messages;

public class TypeSelectedMessage : ValueChangedMessage<SceneTypeDto?>
{
    public TypeSelectedMessage(SceneTypeDto? value) : base(value) { }
}

// Sender (LeftPanelViewModel):
using CommunityToolkit.Mvvm.Messaging;

partial void OnSelectedTypeChanged(SceneTypeDto? value)
{
    WeakReferenceMessenger.Default.Send(new TypeSelectedMessage(value));
}

// Receiver (CenterPanelViewModel constructor):
public CenterPanelViewModel(RevitEventBridge eventBridge)
{
    _eventBridge = eventBridge;
    WeakReferenceMessenger.Default.Register<TypeSelectedMessage>(this, (r, m) =>
    {
        var vm = (CenterPanelViewModel)r;
        vm.OnTypeSelected(m.Value);
    });
}
```

**Confidence: HIGH** -- verified against official Microsoft docs.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `DisplayUnitType.DUT_MILLIMETERS` | `UnitTypeId.Millimeters` | Revit 2022 | Must use UnitTypeId for forward compatibility |
| `param.Definition.ParameterType == ParameterType.Material` | `param.Definition.GetDataType() == SpecTypeId.Reference.Material` | Revit 2022 | ParameterType enum deprecated. GetDataType() is the modern API. |
| `ElementId.IntegerValue` (int) | `ElementId.Value` (long) | Revit 2024 | Project uses `ElementIdHelper` already. |
| CompoundStructure requires core layer | Core layer optional | Revit 2026 | Must handle 0-core-layer structures gracefully. |
| `Category.GetCategory(doc, bic).Name` | `LabelUtils.GetLabelFor(bic)` | Revit 2020 | LabelUtils is cleaner, returns localized string directly. |

## Discretion Recommendations

### Caching Strategy
**Recommendation: Fetch each time a type is selected (no cache for v1).**
Rationale: Cache invalidation on model changes is complex (requires DocumentChanged event subscription). The ExternalEvent round-trip for reading layers is fast (sub-100ms for typical compound structures). Caching adds complexity without meaningful UX benefit for v1. Can optimize later if needed.

### Error Display
**Recommendation: Show inline error text in the center panel.**
When a Revit data fetch fails (ExternalEvent callback receives Exception), display the error message in the center panel as a TextBlock with ErrorBrush foreground: "Erreur : [message]". No modal dialog -- it would block the modeless window interaction.

### Loading Indicator
**Recommendation: Simple "Chargement..." text in center panel.**
Set a `IsLoading` bool property when making a request, show "Chargement..." TextBlock while true, hide when callback arrives. No spinner animation needed for v1 -- the ExternalEvent round-trip is typically < 200ms.

### DTO Nullability
**Recommendation: Use non-nullable with sensible defaults.**
`string` properties default to `string.Empty`. `long` properties default to -1L (matching `ElementId.InvalidElementId.Value`). This avoids NullReferenceException throughout the binding chain and simplifies XAML bindings.

## Open Questions

1. **TreeView SelectedItem binding approach**
   - What we know: WPF TreeView.SelectedItem is read-only, cannot use standard TwoWay binding.
   - What's unclear: Whether to use Behavior from Microsoft.Xaml.Behaviors.Wpf, an attached property, or minimal code-behind.
   - Recommendation: Use `EventToCommandBehavior` from Microsoft.Xaml.Behaviors.Wpf (already in csproj) to invoke a command on `SelectedItemChanged`. Pass the `SelectedItem` via `EventArgs` converter or use `InvokeCommandAction` with `CommandParameter` binding.

2. **CompoundStructureLayer.Function exact return type**
   - What we know: The enum values are `Finish1`, `Finish2`, `Substrate`, `Structure`, `MembraneLayer`, `ThermalOrAir`, `StructuralDeck`. The property is documented as returning `MaterialFunctionAssignment`.
   - What's unclear: Whether Revit 2024 vs 2026 use the same enum type for this property.
   - Recommendation: Verify at implementation time against the actual API assembly. The mapping logic works regardless of the enum type name -- just adjust the type in the switch expression.

3. **GetTypeList for system families vs loaded families**
   - What we know: D-08 says `GetTypeList(familyId)` returns `List<SceneTypeDto>`. But system families (walls, floors) don't have a Family element -- types are collected via `FilteredElementCollector.OfClass(typeof(WallType))`.
   - What's unclear: How to unify the two collection approaches under a single `GetTypeList` call.
   - Recommendation: Pass a discriminator in the request data: for system families, pass the `BuiltInCategory` as a long; for loaded families, pass the Family ElementId. The handler checks which path to take. Alternatively, use a small request DTO: `{ FamilyElementIdValue: long, IsSystemFamily: bool, BuiltInCategoryValue: long }`.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | No test framework installed yet |
| Config file | None -- Wave 0 needed |
| Quick run command | N/A |
| Full suite command | N/A |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SCENE-01 | Create scene with custom name | manual | Launch in Revit, create scene | N/A |
| SCENE-02 | Switch between scenes | manual | Launch in Revit, switch scenes | N/A |
| SCENE-03 | Add types via dropdown | manual | Launch in Revit, use ComboBoxes | N/A |
| SCENE-05 | Remove type from scene | manual | Right-click -> remove | N/A |
| SCENE-06 | TreeView displays grouped types | manual | Visual inspection in Revit | N/A |
| SCENE-07 | Custom sort: Murs first, Sols second | unit | Test CategorySortComparer | -- Wave 0 |
| SCENE-08 | Type selection updates center panel | manual | Click type, verify center panel | N/A |
| LAYER-01 | Layers displayed for compound types | manual | Select wall type, verify layers | N/A |
| LAYER-02 | Each layer shows function, width, material | manual | Visual inspection | N/A |
| LAYER-03 | Material params for loaded families | manual | Select loaded family type | N/A |
| LAYER-04 | Select one or more layers/params | manual | Click items in center panel | N/A |
| LAYER-05 | Multi-selection with Ctrl/Shift | manual | Ctrl+click, Shift+click | N/A |

### Sampling Rate
- **Per task commit:** Manual validation in Revit (load add-in, test specific requirement)
- **Per wave merge:** Full manual walkthrough of all requirements
- **Phase gate:** All 12 requirements pass manual testing in Revit 2024 and/or 2026

### Wave 0 Gaps
- [ ] Test framework not installed -- most requirements are Revit-dependent (manual only)
- [ ] CategorySortComparer can be unit tested if test framework is added
- [ ] LayerFunctionMapper can be unit tested if test framework is added
- [ ] DTO construction logic can be unit tested

*(Note: Revit add-in testing is inherently manual -- the API requires a running Revit instance. Unit tests are only viable for non-Revit-dependent logic like DTOs, mappers, and comparers.)*

## Sources

### Primary (HIGH confidence)
- [CommunityToolkit.Mvvm Messenger docs](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/messenger) -- WeakReferenceMessenger patterns, ValueChangedMessage, Register/Send/Unregister
- [Revit API CompoundStructure Class](https://www.revitapidocs.com/2016/dc1a081e-8dab-565f-145d-a429098d353c.htm) -- GetLayers(), layer properties
- [Revit API CompoundStructureLayerFunction Enumeration](https://www.revitapidocs.com/2015/13db2f6e-0b32-bc59-a7f0-5924737d1664.htm) -- Enum values: Structure, Substrate, ThermalOrAir, Finish1, Finish2, MembraneLayer, StructuralDeck
- [Revit API LabelUtils Methods](https://www.revitapidocs.com/2019/31ea4740-85a4-aff7-ac64-f974d71aa6d7.htm) -- GetLabelFor(BuiltInCategory) for French category names
- [Revit API SpecTypeId.Reference Class](https://www.revitapidocs.com/2023/a5674b4f-6ba6-dab9-d58e-448c5638620f.htm) -- SpecTypeId.Reference.Material for parameter type detection
- [Revit API What's New 2026](https://rvtdocs.com/2026/whatsnew) -- CompoundStructure core layer no longer required
- [Revit API UnitUtils.ConvertFromInternalUnits](https://www.revitapidocs.com/2015/9cc2c0ea-f59f-9d76-ce19-ae7eede03bbd.htm) -- Unit conversion from feet to mm
- [Autodesk Element Material docs (2025)](https://help.autodesk.com/cloudhelp/2025/ITA/Revit-API/files/Revit_API_Developers_Guide/Revit_Geometric_Elements/Material/Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Material_Element_Material_html.html) -- Material parameter discovery pattern

### Secondary (MEDIUM confidence)
- [The Building Coder: CompoundStructure Layer Updates](https://thebuildingcoder.typepad.com/blog/2012/03/updating-wall-compound-layer-structure.html) -- Get-Modify-Set pattern
- [The Building Coder: FilteredElementCollector Performance](https://thebuildingcoder.typepad.com/blog/2010/10/filtered-element-collectors.html) -- Quick vs slow filters
- [LearnRevitAPI: Material Layers from Wall Types](https://www.learnrevitapi.com/blog/how-to-get-material-layers-from-wall-types) -- Practical examples
- [LabelUtils.GetLabelFor() blog post](https://spiderinnet.typepad.com/blog/2020/05/revit-net-api-labelutilsgetlabelfor.html) -- Localized enum labels
- [Autodesk Forum: Unit Conversion](https://forums.autodesk.com/t5/revit-api-forum/converting-from-internal-units-to-meters-or-millimeters-in-revit/td-p/12025099) -- UnitTypeId.Millimeters pattern
- [WPF TreeView Grouping (bstollnitz)](https://github.com/bstollnitz/old-wpf-blog/tree/master/15-GroupingTreeView) -- CollectionViewSource grouping pattern
- [WPF Tutorial: ListView Grouping](https://wpf-tutorial.com/listview-control/listview-grouping/) -- GroupStyle / GroupDescription usage

### Tertiary (LOW confidence)
- CompoundStructureLayer.Function return type (MaterialFunctionAssignment vs CompoundStructureLayerFunction) -- needs verification against actual API assembly at implementation time

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new packages needed, all APIs documented and verified
- Architecture: HIGH -- extends established patterns from Phase 1 (RevitEventBridge, DTO pattern, MVVM)
- Revit API patterns: HIGH -- CompoundStructure, FilteredElementCollector, LabelUtils, UnitUtils all well-documented
- WPF patterns: HIGH -- CollectionViewSource grouping, Messenger, ListBox SelectionMode are standard WPF
- Pitfalls: HIGH -- documented from PITFALLS.md research + official Revit API changes
- French localization: HIGH -- LabelUtils.GetLabelFor() returns localized strings, layer function mapping is a simple dictionary

**Research date:** 2026-04-11
**Valid until:** 2026-05-11 (stable domain -- Revit API does not change mid-version)
