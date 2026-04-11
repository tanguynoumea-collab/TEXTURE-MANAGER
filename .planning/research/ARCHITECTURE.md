# Architecture Patterns

**Domain:** Revit WPF material management add-in (BIM tooling)
**Researched:** 2026-04-11

## CRITICAL FINDING: .NET Version Mismatch

**The PROJECT.md states ".NET Framework 4.8" as the stack, but this is only partially correct.**

| Revit Version | .NET Target | Framework |
|---------------|-------------|-----------|
| Revit 2024 | `net48` | .NET Framework 4.8 |
| Revit 2025 | `net8.0-windows` | .NET 8 |
| Revit 2026 | `net8.0-windows` | .NET 8 |

**Confidence: HIGH** -- verified via Autodesk official migration docs, NuGet Autodesk.Revit.SDK 2026 package (targets .NET 8), and multiple community sources.

**Implication:** The solution cannot be a single .NET 4.8 project. It requires multi-targeting: `net48` for Revit 2024 and `net8.0-windows` for Revit 2025/2026. This is the single most important architectural decision for the project.

---

## Recommended Architecture

### High-Level Solution Structure

```
OlympeMaterialManager.sln
|
|-- OlympeMaterialManager.Shared/          [Shared Project - .shproj]
|   |-- Models/
|   |-- ViewModels/
|   |-- Views/
|   |-- Services/
|   |-- Converters/
|   |-- Themes/
|   +-- Events/
|
|-- OlympeMaterialManager.2024/            [Target Project - net48]
|   |-- Properties/
|   |-- App.cs  (IExternalApplication entry)
|   +-- OlympeMaterialManager.2024.csproj
|
|-- OlympeMaterialManager.2025/            [Target Project - net8.0-windows]
|   |-- Properties/
|   |-- App.cs
|   +-- OlympeMaterialManager.2025.csproj
|
|-- OlympeMaterialManager.2026/            [Target Project - net8.0-windows]
|   |-- Properties/
|   |-- App.cs
|   +-- OlympeMaterialManager.2026.csproj
|
+-- OlympeMaterialManager.Installer/       [WiX v4 Project]
    +-- ...
```

### Why Shared Project (.shproj) Over Multi-Targeting

**Use a Shared Project (.shproj), not a single multi-targeting csproj.** Rationale:

1. Shared Projects compile source files directly into each referencing project -- no intermediate DLL, no assembly loading issues.
2. Each target project references its own version of `RevitAPI.dll` and `RevitAPIUI.dll` (CopyLocal=false).
3. Conditional compilation constants (`REVIT2024`, `REVIT2025`, `REVIT2026`) are defined per target project, available in shared code via `#if` directives.
4. Avoids the `net48` + `net8.0-windows` multi-targeting complications in a single csproj (MSBuild treats these as fundamentally different runtime families, causing WPF resource resolution issues).

**Confidence: HIGH** -- this is the established pattern in the Revit add-in ecosystem (The Building Coder, multiple open-source add-ins).

### Alternative Considered: Single Multi-Target csproj

A single SDK-style csproj with `<TargetFrameworks>net48;net8.0-windows</TargetFrameworks>` is technically possible but introduces:
- Build complexity with conditional `<ItemGroup>` per framework
- WPF XAML resource resolution problems across framework families
- CommunityToolkit.Mvvm source generator inconsistencies on net48 (known issue #695)

**Recommendation: Avoid.** Shared Project approach is simpler and more reliable.

---

## Component Boundaries

### Component Map

| Component | Responsibility | Lives In | Communicates With |
|-----------|---------------|----------|-------------------|
| **App (Entry Point)** | IExternalApplication: registers ribbon button, creates ExternalEvent | Target projects | RevitEventBridge, MainWindow |
| **MainWindow** | WPF Window (modeless), hosts three-panel layout | Shared/Views | ViewModels (DataContext) |
| **RevitEventBridge** | IExternalEventHandler: dispatches queued actions to Revit thread | Shared/Events | App, ViewModels |
| **MainViewModel** | Orchestrates all panels, holds scene state | Shared/ViewModels | RevitEventBridge, child VMs |
| **FamilyTreeViewModel** | Left panel: TreeView of families/types per scene | Shared/ViewModels | MainViewModel |
| **LayerParameterViewModel** | Center panel: layers or material parameters | Shared/ViewModels | MainViewModel, RevitEventBridge |
| **PresetViewModel** | Right panel: preset materials, groups | Shared/ViewModels | MainViewModel, PresetService |
| **MaterialEditorViewModel** | Material property editing (name, color, pattern, tint) | Shared/ViewModels | RevitEventBridge |
| **SceneService** | Active scene management (in-memory) | Shared/Services | MainViewModel |
| **PresetService** | JSON persistence of material presets | Shared/Services | PresetViewModel |
| **RevitDataService** | Reads Revit model data (materials, types, compounds) | Shared/Services | RevitEventBridge, ViewModels |
| **MaterialMapper** | Maps Revit Material to MaterialDto (thread-safe POCO) | Shared/Models | RevitDataService, ViewModels |

### Strict Boundary Rules

1. **ViewModels NEVER reference Revit API types directly.** They work with DTOs/POCOs only. Revit `ElementId` values are stored as `int` (or `long` for Revit 2024+ forward compatibility).
2. **Views NEVER contain business logic.** Code-behind limited to window lifecycle (Loaded, Closing) and visual-only concerns.
3. **RevitEventBridge is the ONLY component that calls Revit API** from ViewModel-initiated actions.
4. **Services are injected** into ViewModels (constructor injection, manual -- no DI container needed for this scale).

---

## Data Flow

### 1. Startup Flow

```
Revit starts
  --> Reads .addin file from %APPDATA%\Autodesk\Revit\Addins\{version}\
  --> Loads assembly, calls IExternalApplication.OnStartup()
  --> App.OnStartup():
      1. Creates ExternalEvent + RevitEventBridge (IExternalEventHandler)
      2. Registers Ribbon panel with button
      3. Stores UIControlledApplication reference
  --> User clicks ribbon button (IExternalCommand.Execute)
      1. Creates services (SceneService, PresetService, RevitDataService)
      2. Creates MainViewModel with services + ExternalEvent reference
      3. Creates MainWindow, sets DataContext = MainViewModel
      4. Shows window modeless (window.Show())
```

### 2. UI-to-Revit Data Flow (Reading)

```
User action in WPF (e.g. selects a type in TreeView)
  --> ViewModel sets RequestType = ReadCompoundLayers
  --> ViewModel stores context (ElementId as int) in RevitEventBridge
  --> ViewModel calls ExternalEvent.Raise()
  --> [Revit idles, picks up event]
  --> RevitEventBridge.Execute(UIApplication):
      1. Reads from RequestType enum
      2. Opens Revit document, reads CompoundStructure
      3. Maps to DTOs via MaterialMapper
      4. Stores result in shared data holder
      5. Calls Dispatcher.Invoke() to push data back to UI thread
  --> ViewModel receives DTOs, updates ObservableCollections
  --> WPF bindings refresh UI
```

### 3. UI-to-Revit Data Flow (Writing -- Set Material)

```
User drags preset onto layer selection
  --> ViewModel sets RequestType = SetLayerMaterial
  --> ViewModel stores (targetTypeId, layerIndices[], materialId) in bridge
  --> ViewModel calls ExternalEvent.Raise()
  --> [Revit idles, picks up event]
  --> RevitEventBridge.Execute(UIApplication):
      1. Opens Transaction("Set Material")
      2. Gets HostObjAttributes from ElementId
      3. Gets CompoundStructure (COPY -- not reference)
      4. For each layer index:
         - cs.GetLayers()[i].MaterialId = new ElementId(materialId)
         OR modifies via SetLayerFunction / layer replacement
      5. Calls hostObjAttrs.SetCompoundStructure(cs) -- PERSISTS changes
      6. Transaction.Commit()
      7. Dispatcher.Invoke() to update UI with confirmation
```

### 4. Material Editing Data Flow

```
User edits material property in editor panel
  --> MaterialEditorViewModel sets RequestType = EditMaterial
  --> Stores (materialId, propertyName, newValue) in bridge
  --> ExternalEvent.Raise()
  --> RevitEventBridge.Execute(UIApplication):
      1. Transaction("Edit Material")
      2. Material mat = doc.GetElement(new ElementId(materialId)) as Material
      3. For basic properties (Name, Color, SurfacePatternId):
         - Direct property assignment on Material object
      4. For appearance properties (tint, texture):
         - AppearanceAssetEditScope scope = new(doc)
         - Asset editableAsset = scope.Start(mat.AppearanceAssetId)
         - Modify asset properties
         - scope.Commit(true)  // force view update
      5. Transaction.Commit()
```

### 5. Preset Persistence Flow

```
Presets load:
  User opens add-in --> PresetService reads JSON from stored path
  --> Deserializes to List<PresetGroup> containing List<MaterialPreset>
  --> PresetViewModel populates ObservableCollections

Presets save:
  User modifies presets --> PresetViewModel calls PresetService.Save()
  --> Serializes to JSON, writes to user-chosen path
  --> Path stored in user settings (app.config or registry)
```

---

## Entry Point Architecture

### IExternalApplication vs IExternalCommand

**Use IExternalApplication as the primary entry point.** Rationale:

- `IExternalApplication.OnStartup()` runs when Revit loads. This is where you register the ribbon button and create the ExternalEvent/handler pair that persists for the Revit session.
- The ribbon button triggers an `IExternalCommand.Execute()` that opens the modeless WPF window.
- `IExternalApplication.OnShutdown()` disposes the window, event handlers, and services.

**Do NOT create ExternalEvent inside IExternalCommand.Execute().** The ExternalEvent must outlive the command execution -- it is used throughout the modeless dialog session.

```
[Entry Points]

App : IExternalApplication
  - OnStartup(): Register ribbon, create ExternalEvent + Handler
  - OnShutdown(): Dispose resources

ShowWindowCommand : IExternalCommand
  - Execute(): Instantiate ViewModels, create Window, Show() modeless
```

### .addin File Structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Assembly>path\to\OlympeMaterialManager.{version}.dll</Assembly>
    <AddInId>{GUID}</AddInId>
    <FullClassName>OlympeMaterialManager.App</FullClassName>
    <Name>Olympe MaterialManager</Name>
    <VendorId>OLYMPE</VendorId>
    <VendorDescription>Olympe, https://olympe.com</VendorDescription>
  </AddIn>
</RevitAddIns>
```

One .addin file per Revit version, placed in:
- `%APPDATA%\Autodesk\Revit\Addins\2024\OlympeMaterialManager.addin`
- `%APPDATA%\Autodesk\Revit\Addins\2025\OlympeMaterialManager.addin`
- `%APPDATA%\Autodesk\Revit\Addins\2026\OlympeMaterialManager.addin`

Each points to the version-specific DLL. The WiX installer handles copying the correct DLL + .addin to the correct folder.

---

## RevitEventBridge Pattern (IExternalEventHandler)

### Design: Enum-Based Action Dispatcher

**Use a single IExternalEventHandler with an enum-based action dispatcher.** This avoids creating a separate ExternalEvent per action type (messy, hard to manage).

```
RevitEventBridge : IExternalEventHandler
  Properties:
    - RequestType : RevitRequest (enum)
    - RequestData : object (payload -- typed per request)
    - ResultCallback : Action<object> (invoked via Dispatcher)

  Execute(UIApplication uiApp):
    switch (RequestType):
      case ReadCompoundLayers: ...
      case SetLayerMaterial: ...
      case EditMaterial: ...
      case DuplicateMaterial: ...
      case Read3DViewTypes: ...
      ...

  GetName(): "OlympeMaterialManager.RevitEventBridge"
```

### Thread Safety Contract

```
WPF UI Thread                          Revit Main Thread
--------------                          -----------------
1. Set RequestType + RequestData
2. Call ExternalEvent.Raise()
   (returns immediately)
                                        3. Revit idles, calls Execute()
                                        4. Handler reads RequestType
                                        5. Performs Revit API operations
                                        6. Calls Dispatcher.Invoke(() => callback(result))
7. Callback updates ViewModel
8. WPF bindings update UI
```

**Key rules:**
- Never store Revit `Element` objects in ViewModel. Store only `ElementId` as `int`.
- Never call Revit API from WPF thread. Always go through ExternalEvent.Raise().
- Use `Application.Current.Dispatcher.Invoke()` to push results back to UI thread.
- One request at a time -- guard with a boolean `_isProcessing` flag.

**Confidence: HIGH** -- this is the canonical pattern documented by The Building Coder, Autodesk SDK samples (ModelessForm_ExternalEvent), and multiple production add-ins.

---

## MVVM in Revit WPF Context

### ViewModel Design Principles

1. **No Revit API types in ViewModels.** ViewModels reference only DTOs, primitives, and framework types. This enables WPF Designer preview and prevents thread safety issues.

2. **CommunityToolkit.Mvvm for MVVM infrastructure.** Use `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject` base class. Works on both net48 and net8.0-windows (targets .NET Standard 2.0). **Caveat:** Source generators on net48 require PackageReference format (not packages.config) and VS 2022+ / .NET 6+ SDK. This is a known friction point (GitHub issue #695) but works with proper setup.

3. **Commands route through RevitEventBridge.** A RelayCommand in the ViewModel does NOT call Revit API directly. It sets up the request and calls `ExternalEvent.Raise()`.

### ViewModel Hierarchy

```
MainViewModel (ObservableObject)
  |-- ActiveScene : SceneModel
  |-- FamilyTreeVM : FamilyTreeViewModel
  |-- LayerParameterVM : LayerParameterViewModel
  |-- PresetVM : PresetViewModel
  |-- MaterialEditorVM : MaterialEditorViewModel
  |
  |-- [RelayCommand] AddTypeToScene
  |-- [RelayCommand] SwitchScene
  |-- [RelayCommand] SetMaterial
```

### Dispatcher Usage

The WPF window is shown modeless from `IExternalCommand.Execute()`, which runs on the Revit main thread. The window's UI thread IS the Revit main thread when using `Show()` (not a separate thread). However, when `RevitEventBridge.Execute()` runs (also on Revit main thread during idle), it needs to update ViewModel properties that trigger UI updates. Since both run on the same thread in this pattern, `Dispatcher.Invoke` is technically a same-thread call, but using it is still good practice for safety and clarity.

**Important nuance:** If you ever create the WPF window on a separate thread (some add-ins do this for responsiveness), then `Dispatcher.Invoke` becomes mandatory for cross-thread UI updates. For this project, the simpler same-thread approach is recommended since the UI is not computationally heavy.

---

## CompoundStructure Read/Write Architecture

### Reading Layers

```
RevitDataService.GetCompoundLayers(Document doc, int typeId):
  1. Element elem = doc.GetElement(new ElementId(typeId))
  2. HostObjAttributes hostAttrs = elem as HostObjAttributes
  3. CompoundStructure cs = hostAttrs.GetCompoundStructure()
     -- Returns COPY, safe to read without Transaction
  4. IList<CompoundStructureLayer> layers = cs.GetLayers()
  5. For each layer:
     - layerDto.Index = i
     - layerDto.Function = layer.Function.ToString()
     - layerDto.Width = layer.Width (in feet, convert to mm for display)
     - layerDto.MaterialId = layer.MaterialId.IntegerValue
     - layerDto.MaterialName = doc.GetElement(layer.MaterialId)?.Name ?? "<By Category>"
     - layerDto.MaterialColor = ExtractColor(material)
  6. Return List<LayerDto>
```

### Writing Material to Layers

```
RevitEventBridge (inside Execute, valid API context):
  1. Transaction t = new Transaction(doc, "Olympe: Set Material")
  2. t.Start()
  3. HostObjAttributes hostAttrs = doc.GetElement(new ElementId(typeId)) as HostObjAttributes
  4. CompoundStructure cs = hostAttrs.GetCompoundStructure()
  5. IList<CompoundStructureLayer> layers = cs.GetLayers()
  6. For each target layer index:
     - CompoundStructureLayer layer = layers[index]
     - layer = new CompoundStructureLayer(layer.Width, layer.Function, new ElementId(materialId))
     - layers[index] = layer
  7. cs.SetLayers(layers)
  8. hostAttrs.SetCompoundStructure(cs)  // PERSISTS to document
  9. t.Commit()
```

### Family Instance Material Parameters (Non-Compound)

For loaded families without CompoundStructure, material is assigned via parameters:

```
RevitDataService.GetMaterialParameters(Document doc, int typeId):
  1. ElementType type = doc.GetElement(new ElementId(typeId)) as ElementType
  2. For each Parameter p in type.Parameters:
     - If p.Definition.GetDataType() == SpecTypeId.Reference.Material:
       - paramDto.Name = p.Definition.Name
       - paramDto.MaterialId = p.AsElementId().IntegerValue
       - paramDto.MaterialName = doc.GetElement(p.AsElementId())?.Name
  3. Return List<MaterialParameterDto>

To set:
  1. Transaction("Olympe: Set Material Parameter")
  2. type.get_Parameter(paramGuid).Set(new ElementId(materialId))
     OR type.LookupParameter(paramName).Set(new ElementId(materialId))
  3. Commit()
```

---

## Material Editing Transaction Patterns

### Basic Properties (Name, Description, Color, Surface Pattern)

```csharp
using (Transaction t = new Transaction(doc, "Olympe: Edit Material"))
{
    t.Start();
    Material mat = doc.GetElement(new ElementId(materialId)) as Material;

    // Name
    mat.Name = newName;

    // Color (surface)
    mat.Color = new Color(r, g, b);

    // Surface foreground pattern
    mat.SurfaceForegroundPatternId = new ElementId(patternId);
    mat.SurfaceForegroundPatternColor = new Color(r, g, b);

    t.Commit();
}
```

### Appearance Properties (Tint, Texture -- via AppearanceAssetEditScope)

```csharp
using (Transaction t = new Transaction(doc, "Olympe: Edit Appearance"))
{
    t.Start();
    Material mat = doc.GetElement(new ElementId(materialId)) as Material;
    AppearanceAssetElement assetElem = doc.GetElement(mat.AppearanceAssetId)
        as AppearanceAssetElement;

    using (AppearanceAssetEditScope scope = new AppearanceAssetEditScope(doc))
    {
        Asset editableAsset = scope.Start(assetElem.Id);
        // Modify appearance properties on editableAsset
        // e.g., tint color, generic diffuse, etc.
        scope.Commit(true); // true = force refresh open views
    }
    t.Commit();
}
```

**Key constraint:** A single AppearanceAssetEditScope can only edit ONE top-level rendering asset. For editing multiple materials, you need separate scopes (each within the same or separate transactions).

### Material Duplication

```csharp
using (Transaction t = new Transaction(doc, "Olympe: Duplicate Material"))
{
    t.Start();
    Material original = doc.GetElement(new ElementId(materialId)) as Material;
    Material duplicate = original.Duplicate(newName) as Material;
    // duplicate is now a new Material with a new ElementId
    t.Commit();
}
```

---

## WPF DataTemplate Patterns for Material Visualization

### Material Card Template

Each material preset in the right panel is visualized as a "card" using DataTemplate:

```xml
<DataTemplate DataType="{x:Type models:MaterialPresetDto}">
  <Border CornerRadius="4" Padding="8" Background="{StaticResource CardBrush}">
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="48"/>  <!-- Preview -->
        <ColumnDefinition Width="*"/>   <!-- Info -->
      </Grid.ColumnDefinitions>

      <!-- Color/Pattern preview swatch -->
      <Border Grid.Column="0" Width="40" Height="40" CornerRadius="4">
        <Border.Background>
          <SolidColorBrush Color="{Binding SurfaceColor, Converter={StaticResource ColorConverter}}"/>
        </Border.Background>
        <!-- Overlay pattern lines if pattern assigned -->
      </Border>

      <!-- Material info -->
      <StackPanel Grid.Column="1" Margin="8,0,0,0">
        <TextBlock Text="{Binding Name}" FontWeight="SemiBold"/>
        <TextBlock Text="{Binding Description}" Opacity="0.7" TextTrimming="CharacterEllipsis"/>
      </StackPanel>
    </Grid>
  </Border>
</DataTemplate>
```

### Layer Row Template (Center Panel)

```xml
<DataTemplate DataType="{x:Type models:LayerDto}">
  <Grid Height="32">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="20"/>   <!-- Layer color swatch -->
      <ColumnDefinition Width="*"/>    <!-- Function name -->
      <ColumnDefinition Width="60"/>   <!-- Width (mm) -->
      <ColumnDefinition Width="*"/>    <!-- Material name -->
    </Grid.ColumnDefinitions>

    <Rectangle Grid.Column="0" Fill="{Binding MaterialColor, Converter={StaticResource ColorConverter}}" Width="12" Height="12"/>
    <TextBlock Grid.Column="1" Text="{Binding Function}"/>
    <TextBlock Grid.Column="2" Text="{Binding WidthMm, StringFormat='{}{0:F0} mm'}"/>
    <TextBlock Grid.Column="3" Text="{Binding MaterialName}"/>
  </Grid>
</DataTemplate>
```

### DataTemplateSelector for Compound vs Non-Compound Types

Use a `DataTemplateSelector` to switch between layer view (for walls/floors/roofs/ceilings) and parameter view (for loaded families):

```csharp
public class TypeContentTemplateSelector : DataTemplateSelector
{
    public DataTemplate CompoundLayerTemplate { get; set; }
    public DataTemplate MaterialParameterTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is CompoundTypeViewModel) return CompoundLayerTemplate;
        if (item is FamilyTypeViewModel) return MaterialParameterTemplate;
        return base.SelectTemplate(item, container);
    }
}
```

---

## Preset Persistence Architecture

### Data Model

```
PresetStore
  |-- FilePath : string (user-chosen, stored in settings)
  |-- Groups : List<PresetGroup>
       |-- Name : string ("Murs", "Sols", "Autres", custom...)
       |-- Presets : List<MaterialPreset>
            |-- Id : Guid (stable preset identity)
            |-- Name : string
            |-- Description : string
            |-- RevitMaterialId : int (ElementId -- document-specific)
            |-- SurfaceColor : (R, G, B)
            |-- SurfacePatternName : string
            |-- AppearanceTintColor : (R, G, B)
            |-- SourceDocumentPath : string (for reference)
```

### JSON Serialization

**Use System.Text.Json** (available on both .NET 4.8 via NuGet and .NET 8 natively). Rationale:
- No external dependency on .NET 8
- Smaller and faster than Newtonsoft.Json
- .NET 4.8 support via `System.Text.Json` NuGet package (works with net48)
- Simpler API, less configuration surface

```csharp
public class PresetService
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PresetStore Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<PresetStore>(json, _options);
    }

    public void Save(PresetStore store, string path)
    {
        string json = JsonSerializer.Serialize(store, _options);
        File.WriteAllText(path, json);
    }
}
```

### Path Storage

Store the user's chosen preset file path in:
- **Option A (Recommended):** `Environment.GetFolderPath(SpecialFolder.ApplicationData)\Olympe\MaterialManager\settings.json` -- a simple JSON settings file with the preset path and other user preferences.
- **Option B:** Windows Registry under `HKCU\Software\Olympe\MaterialManager`.

Option A is simpler, portable, and consistent with the JSON-first approach.

---

## Multi-Version Project Configuration

### Target Project csproj (Revit 2024 -- net48)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <UseWPF>true</UseWPF>
    <LangVersion>latest</LangVersion>
    <DefineConstants>REVIT2024</DefineConstants>
    <AssemblyName>OlympeMaterialManager.2024</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="RevitAPI">
      <HintPath>..\libs\2024\RevitAPI.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="RevitAPIUI">
      <HintPath>..\libs\2024\RevitAPIUI.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
  </ItemGroup>

  <Import Project="..\OlympeMaterialManager.Shared\OlympeMaterialManager.Shared.projitems"
          Label="Shared" />
</Project>
```

### Target Project csproj (Revit 2025/2026 -- net8.0-windows)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <DefineConstants>REVIT2025</DefineConstants>  <!-- or REVIT2026 -->
    <AssemblyName>OlympeMaterialManager.2025</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Autodesk.Revit.SDK" Version="2025.0.0" />
    <!-- OR reference local DLLs like the 2024 project -->
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <!-- System.Text.Json is included in .NET 8 -- no NuGet needed -->
  </ItemGroup>

  <Import Project="..\OlympeMaterialManager.Shared\OlympeMaterialManager.Shared.projitems"
          Label="Shared" />
</Project>
```

### Conditional Compilation in Shared Code

```csharp
// Example: API difference handling
public static int GetElementIdValue(ElementId id)
{
#if REVIT2024
    return id.IntegerValue;  // .NET 4.8 API
#else
    return (int)id.Value;    // .NET 8 API (if changed)
#endif
}
```

---

## Patterns to Follow

### Pattern 1: DTO Boundary Layer

**What:** All data crossing the Revit-API / ViewModel boundary is mapped to plain DTOs (Data Transfer Objects). No Revit types leak into ViewModels.

**When:** Always. Every piece of Revit data that reaches the UI.

**Why:** Thread safety, testability, WPF Designer support, and decoupling from Revit API version changes.

```csharp
// DTO -- lives in Shared/Models
public class LayerDto
{
    public int Index { get; set; }
    public string Function { get; set; }
    public double WidthMm { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; }
    public byte ColorR { get; set; }
    public byte ColorG { get; set; }
    public byte ColorB { get; set; }
}
```

### Pattern 2: Single-Handler Action Dispatch

**What:** One `IExternalEventHandler` with enum-based request routing, not N separate handlers.

**When:** For all Revit API interactions from the modeless UI.

**Why:** Cleaner lifecycle management, single ExternalEvent instance, easier to reason about concurrency.

### Pattern 3: Transaction Grouping

**What:** Group related Revit changes into a single Transaction with a descriptive name.

**When:** Setting material on multiple layers, editing multiple material properties.

**Why:** Single undo step for the user, better performance, atomic operations.

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Storing Revit Elements in ViewModels

**What:** Keeping references to `Element`, `Material`, `WallType` objects in ViewModel properties.

**Why bad:** These objects are only valid within a Revit API context. They become stale after transactions, and accessing them from the WPF thread can crash Revit.

**Instead:** Store `ElementId` as `int`. Re-fetch the element inside the ExternalEventHandler when needed.

### Anti-Pattern 2: Direct Revit API Calls from ViewModel Commands

**What:** Calling `doc.GetElement()` or `Transaction.Start()` directly in a RelayCommand handler.

**Why bad:** RelayCommand runs on the WPF/UI thread, which is outside a valid Revit API context when the window is modeless. Will throw `InvalidOperationException` or corrupt the document.

**Instead:** Route through ExternalEvent.Raise(). The handler's Execute() runs in a valid context.

### Anti-Pattern 3: Multiple ExternalEvent Instances

**What:** Creating a new `ExternalEvent.Create(handler)` for each action type.

**Why bad:** Proliferates lifecycle management. Revit limits concurrent ExternalEvents. Debugging which event fired becomes difficult.

**Instead:** Single ExternalEvent, single handler, enum-based dispatch.

### Anti-Pattern 4: Modifying CompoundStructure Without SetCompoundStructure

**What:** Calling `GetCompoundStructure()`, modifying layers, but forgetting to call `SetCompoundStructure()`.

**Why bad:** `GetCompoundStructure()` returns a COPY. Modifications to the copy are silently lost without the setter call. This is one of the most common Revit API bugs.

**Instead:** Always call `hostObjAttrs.SetCompoundStructure(cs)` after modifications.

---

## Suggested Build Order (Dependencies)

Build order based on component dependencies:

### Phase 1: Foundation (no Revit API needed for most)
1. **Solution structure** -- Shared project + target projects + build verification
2. **DTO models** -- LayerDto, MaterialDto, MaterialPresetDto, SceneModel
3. **PresetService** -- JSON serialization (pure .NET, no Revit dependency)
4. **WPF theme** -- Olympe dark theme, resource dictionaries, base styles

### Phase 2: Revit Integration Core
5. **App entry point** -- IExternalApplication, ribbon registration
6. **RevitEventBridge** -- IExternalEventHandler with enum dispatch skeleton
7. **.addin files** -- Registration for each version
8. **Basic modeless window** -- Show/hide, lifecycle management

### Phase 3: Read Path (Left + Center Panels)
9. **RevitDataService** -- Read 3D view visible types, read CompoundStructure layers, read material parameters
10. **MaterialMapper** -- Revit Material to DTO conversion
11. **SceneService** -- Active scene management
12. **FamilyTreeViewModel** -- TreeView population from scene
13. **LayerParameterViewModel** -- Display layers or parameters for selected type

### Phase 4: Write Path (Right Panel + Actions)
14. **PresetViewModel** -- Preset groups, material cards, drag source
15. **Set Material action** -- Write material to CompoundStructure layers
16. **Set Material Parameter action** -- Write material to family type parameter
17. **MaterialEditorViewModel** -- Edit name, description, color, pattern, tint
18. **Material duplication** -- Clone with auto-naming

### Phase 5: Polish + Installer
19. **Multi-selection** -- Select multiple layers/parameters for batch assignment
20. **Scene switching** -- Create, rename, switch scenes
21. **3D view click** -- Add type to scene by clicking in view
22. **WiX installer** -- Multi-version deployment

**Rationale:** Read-before-write. You need to display data before you can modify it. The ExternalEvent bridge must exist before any Revit interaction. Presets are independent of Revit (JSON), so can be developed early but are needed when the write path begins.

---

## Sources

### HIGH Confidence (Official / Authoritative)
- [Autodesk: Migrating from .NET 4.8 to .NET Core 8](https://blog.autodesk.io/migrating-from-net-48-to-net-core-8/)
- [Autodesk: .NET 8 Upgrade (What's New in 2025)](https://help.autodesk.com/view/RVT/2025/ENU/?guid=GUID-50024FD2-16BE-40BE-96E6-550294D9537D)
- [NuGet: Autodesk.Revit.SDK 2026 (targets .NET 8)](https://www.nuget.org/packages/Autodesk.Revit.SDK)
- [Autodesk: General Material Information (2025 API)](https://help.autodesk.com/cloudhelp/2025/PTB/Revit-API/files/Revit_API_Developers_Guide/Revit_Geometric_Elements/Material/Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Material_General_Material_Information_html.html)
- [Autodesk: Element Material (2025 API)](https://help.autodesk.com/cloudhelp/2025/ITA/Revit-API/files/Revit_API_Developers_Guide/Revit_Geometric_Elements/Material/Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Material_Element_Material_html.html)
- [Revit API Docs: CompoundStructure.GetLayers (2025)](https://rvtdocs.com/2025/105b59e9-9cea-1988-a5a7-cc9cde49145c)
- [Revit API Docs: AppearanceAssetEditScope](https://www.revitapidocs.com/2019/743c74ba-12de-4d77-a677-325229525955.htm)
- [Microsoft: CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [CommunityToolkit.Mvvm NuGet (.NET Standard 2.0)](https://www.nuget.org/packages/CommunityToolkit.Mvvm)

### MEDIUM Confidence (Verified Community)
- [The Building Coder: Modeless WPF with MVVM](https://jeremytammik.github.io/tbc/a/1675_10year_modeless.html)
- [The Building Coder: Multi-Targeting Revit Versions](https://jeremytammik.github.io/tbc/a/1668_multi_target_addin.html)
- [The Building Coder: Updating Wall Compound Layer Structure](https://jeremytammik.github.io/tbc/a/0727_balaji_setcompoundstr.htm)
- [Easy Revit API: MVVM Pattern for Revit Part 2](https://easyrevitapi.com/index.php/2023/11/01/m-v-vm-pattern-for-revit-part-2/)
- [GitHub: RevitWPF (modeless WPF pattern)](https://github.com/varolomer/RevitWPF)
- [GitHub: CommunityToolkit.Mvvm Issue #695 (net48 source generators)](https://github.com/CommunityToolkit/dotnet/issues/695)
- [Revit API Forum: .NET 8 migration](https://forums.autodesk.com/t5/revit-api-forum/net-8-migration/td-p/13301211)

### LOW Confidence (Needs Validation)
- ElementId API changes between Revit 2024 and 2025/2026 (IntegerValue vs Value) -- verify against actual API reference per version
- Exact CommunityToolkit.Mvvm source generator behavior on net48 with Shared Projects (works with standard projects, shared project compilation path may differ)
