# Phase 1: Foundation and Infrastructure - Research

**Researched:** 2026-04-11
**Domain:** Revit WPF add-in multi-target build, modeless window lifecycle, MVVM foundation, dark theme
**Confidence:** HIGH

## Summary

Phase 1 establishes the entire build and architectural foundation for a multi-version Revit WPF add-in. The critical gate is validating that a single SDK-style csproj can compile WPF XAML across both `net48` (Revit 2024) and `net8.0-windows` (Revit 2025/2026). Research confirms this works with `Microsoft.NET.Sdk` on .NET SDK 5.0.200+ (the development machine has SDK 10.0.201), but an alternative configuration-based approach (Nice3point.Revit.Sdk) is the Revit ecosystem standard and avoids multi-target XAML pitfalls entirely. The CONTEXT.md decision D-01 locks the single multi-target csproj approach with a Shared Project fallback -- this research provides the exact configuration needed and clarifies the risk profile.

The Nice3point.Revit.Toolkit provides `ExternalEvent<T>` which passes typed data at raise time, but does NOT fit the enum-dispatch pattern from D-09. The enum dispatch pattern requires a single event instance with mutable request state, while `ExternalEvent<T>` creates a lambda-bound handler at construction time. The recommended approach is to use the raw Nice3point `ExternalEvent` (non-generic) with the enum dispatch bridge pattern, or adopt multiple typed `ExternalEvent<T>` instances (one per action type) as a cleaner alternative to enum dispatch.

**Primary recommendation:** Start with the single SDK-style multi-target csproj approach (D-01). Use `Microsoft.NET.Sdk` with `<UseWPF>true</UseWPF>` and `<TargetFrameworks>net48;net8.0-windows</TargetFrameworks>`. If WPF XAML compilation fails, fall back to the Shared Project approach immediately. The ExternalEvent pattern should use Nice3point.Revit.Toolkit's non-generic `ExternalEvent` combined with a `RevitEventBridge` singleton for enum dispatch.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Use a single SDK-style .csproj with `<TargetFrameworks>net48;net8.0-windows</TargetFrameworks>` and conditional PackageReferences per Revit version. This is the modern standard per Stack research. If WPF XAML compilation across frameworks fails, fall back to Shared Project (.shproj) + 2 target projects (net48, net8.0-windows).
- **D-02:** Use Nice3point.Revit.Api NuGet packages for Revit API references (CopyLocal=false by default). Version-specific: 2024.x for net48, 2025.x/2026.x for net8.0-windows.
- **D-03:** Use PolySharp 1.15.0 to enable C# 12 features on the net48 target.
- **D-04:** All ElementId usage must use `.Value` (long), never `.IntegerValue` (deprecated). This is a day-one convention.
- **D-05:** Dark theme via WPF ResourceDictionary. Palette: Background #1E1E2E, Surface #2D2D3D, Accent #FF9800 (ambre), Accent hover #FFA726, Text primary #E0E0E0, Text secondary #A0A0A0, Border #3D3D4D, Error #EF5350.
- **D-06:** All controls styled in the ResourceDictionary: Button, TreeView, ListBox, ScrollBar, TextBox, ComboBox, GridSplitter. Consistent rounded corners (CornerRadius=4) and accent color on focus/hover.
- **D-07:** MainWindow uses a Grid with 3 columns and 2 GridSplitters. Default proportions: left 250px, center *, right 250px. GridSplitters allow user resizing. MinWidth 200px on side panels, MinWidth 300px on center.
- **D-08:** Each panel is a UserControl with its own ViewModel, hosted in the Grid columns.
- **D-09:** Single IExternalEventHandler implementation with an enum-based dispatch (RevitRequestType). One ExternalEvent instance created in IExternalApplication.OnStartup, shared via a static singleton (RevitEventBridge).
- **D-10:** The handler receives request data via a thread-safe queue or typed property. Results are marshalled back to ViewModels via DTOs (no Revit types in ViewModels).
- **D-11:** Nice3point.Revit.Toolkit ExternalEventHandler<T> base class to be evaluated -- if it fits the enum dispatch pattern, use it instead of raw IExternalEventHandler.
- **D-12:** IExternalApplication for startup: register ribbon button, create ExternalEvent singleton.
- **D-13:** Ribbon button opens/shows the modeless singleton window. Window.Closing event is intercepted to Hide() instead of Close(), preventing disposal.
- **D-14:** Three .addin files generated (one per Revit version) with unique GUIDs, pointing to the correct assembly path.
- **D-15:** CommunityToolkit.Mvvm 8.4.2 with source generators: [ObservableProperty], [RelayCommand], ObservableObject base class.
- **D-16:** ViewModels live in a ViewModels/ folder, Views in Views/. One ViewModel per panel (LeftPanelViewModel, CenterPanelViewModel, RightPanelViewModel) + MainWindowViewModel to coordinate.

### Claude's Discretion
- Exact folder structure within the project (Models/, Services/, Helpers/, etc.)
- NuGet package version pinning strategy
- Whether to use Nice3point.Revit.Extensions or hand-write extension methods
- Unit test framework choice (if any tests in Phase 1)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| INFRA-01 | Multi-target build (net48 + net8.0-windows) single SDK-style csproj | Detailed csproj configuration with conditional PackageReferences; fallback to Shared Project documented |
| INFRA-02 | Nice3point Revit API NuGet packages with CopyLocal=false | Verified versions: 2024.3.30 (net48), 2025.4.20 (net8), 2026.4.0 (net8); CopyLocal=false is default |
| INFRA-03 | .addin file registration per Revit version | Complete XML structure documented; per-user path pattern confirmed |
| INFRA-04 | IExternalApplication startup with ExternalEvent singleton | Nice3point.Revit.Toolkit ExternalApplication base class with ribbon helpers; ExternalEvent construction pattern |
| INFRA-05 | IExternalEventHandler with enum dispatch | ExternalEvent<T> evaluated -- does NOT fit enum dispatch; use non-generic ExternalEvent + RevitEventBridge |
| INFRA-06 | Modeless singleton WPF window with persist across show/hide | WindowInteropHelper owner pattern; Hide-on-close via Closing event cancellation; singleton enforcement |
| INFRA-07 | ViewModels import no Revit API types -- DTOs only | DTO boundary pattern documented with code examples |
| INFRA-08 | CommunityToolkit.Mvvm 8.4.2 with source generators | Works on net48 via netstandard2.0 target + PolySharp; LangVersion 12.0 required |
| INFRA-09 | Build produces loadable assemblies for Revit 2024, 2025, 2026 | Multi-target build outputs to separate bin folders per TFM; .addin points to correct DLL |
| UI-01 | Three-column layout (familles, couches, materiaux) | Grid with 3 columns + 2 GridSplitters; MinWidth constraints; UserControls per panel |
| UI-02 | Dark Olympe theme via ResourceDictionary | Custom ResourceDictionary with DynamicResource; ControlTemplate overrides for all standard controls |
| UI-04 | Interface entirely in French | Hardcoded French strings in XAML; no localization framework needed for v1 |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **MVVM strict**: No business logic in code-behind. RelayCommand, ObservableCollection.
- **Naming**: PascalCase for classes/properties, _camelCase for private fields.
- **One ViewModel per view/panel**.
- **IExternalEventHandler for ALL Revit interactions from UI**.
- **Interface language**: French.
- **Git**: Push only after installer validation. .gitignore: bin/, obj/, .vs/, *.user, packages/.
- **Solution structure per CLAUDE.md**: OlympeMaterialManager/ root with Shared, per-version, and Installer projects. NOTE: D-01 overrides this to a single multi-target csproj -- CLAUDE.md structure is the fallback.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| C# | 12.0 (LangVersion) | Language | Modern syntax; works on net48 via PolySharp |
| .NET Framework 4.8 | 4.8 | Runtime for Revit 2024 | Required by Revit 2024 host |
| .NET 8 | 8.0 | Runtime for Revit 2025/2026 | Required by Revit 2025+ host |
| WPF | built-in | UI framework | Required for Revit modeless windows |

### Revit API NuGet Packages

| Package | Version | TFM | Purpose |
|---------|---------|-----|---------|
| Nice3point.Revit.Api.RevitAPI | 2024.3.30 | net48 | Revit 2024 API assemblies |
| Nice3point.Revit.Api.RevitAPIUI | 2024.3.30 | net48 | Revit 2024 UI API |
| Nice3point.Revit.Toolkit | 2024.3.0 | net48 | Base classes, ExternalEvent helpers |
| Nice3point.Revit.Api.RevitAPI | 2026.4.0 | net8.0 | Revit 2025/2026 API assemblies |
| Nice3point.Revit.Api.RevitAPIUI | 2026.4.0 | net8.0 | Revit 2025/2026 UI API |
| Nice3point.Revit.Toolkit | 2026.1.0 | net8.0 | Base classes, ExternalEvent helpers |

**Note on Revit 2025 vs 2026:** Both target net8.0-windows. Using Revit 2026 API assemblies for both is acceptable because the 2026 Material API is backward-compatible with 2025. Use `#if REVIT2026` conditional compilation only if a 2026-only API is needed. Define custom symbols: `REVIT2024` for net48, `REVIT2025_OR_GREATER` for net8.0-windows.

### MVVM and Supporting

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| CommunityToolkit.Mvvm | 8.4.2 | MVVM infrastructure | Source generators: [ObservableProperty], [RelayCommand], ObservableObject. Targets netstandard2.0 (compatible with net48). |
| PolySharp | 1.15.0 | C# 12 polyfills for net48 | Enables modern C# syntax on .NET Framework 4.8. Source-only (PrivateAssets=all). |
| Microsoft.Xaml.Behaviors.Wpf | 1.1.142 | WPF behaviors | MVVM-friendly event binding without code-behind. Supports both net462+ and net8.0-windows. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Single multi-target csproj | Nice3point.Revit.Sdk configuration-based approach | Nice3point SDK is the ecosystem standard, avoids TargetFrameworks multi-target complexity. But CONTEXT.md D-01 locks the multi-target approach. |
| Single multi-target csproj | Shared Project + 2 target projects | Fallback if XAML compilation fails across TFMs. More files but guaranteed to work. |
| Non-generic ExternalEvent + enum dispatch | Multiple ExternalEvent<T> instances | Cleaner per-action typing but multiple event instances instead of one. CONTEXT.md D-09 locks enum dispatch. |

**Installation (csproj):**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net48;net8.0-windows</TargetFrameworks>
    <LangVersion>12.0</LangVersion>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Olympe.MaterialManager</RootNamespace>
    <AssemblyName>OlympeMaterialManager</AssemblyName>
  </PropertyGroup>

  <!-- Conditional compilation symbols -->
  <PropertyGroup Condition="'$(TargetFramework)' == 'net48'">
    <DefineConstants>$(DefineConstants);REVIT2024</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition="'$(TargetFramework)' == 'net8.0-windows'">
    <DefineConstants>$(DefineConstants);REVIT2025_OR_GREATER</DefineConstants>
  </PropertyGroup>

  <!-- Shared packages (all TFMs) -->
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageReference Include="PolySharp" Version="1.15.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.1.142" />
  </ItemGroup>

  <!-- Revit 2024 (net48) -->
  <ItemGroup Condition="'$(TargetFramework)' == 'net48'">
    <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2024.3.30" />
    <PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="2024.3.30" />
    <PackageReference Include="Nice3point.Revit.Toolkit" Version="2024.3.0" />
  </ItemGroup>

  <!-- Revit 2025/2026 (net8.0-windows) -->
  <ItemGroup Condition="'$(TargetFramework)' == 'net8.0-windows'">
    <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2026.4.0" />
    <PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="2026.4.0" />
    <PackageReference Include="Nice3point.Revit.Toolkit" Version="2026.1.0" />
  </ItemGroup>
</Project>
```

## Architecture Patterns

### Recommended Project Structure
```
OlympeMaterialManager/
  OlympeMaterialManager.sln
  src/
    OlympeMaterialManager/
      OlympeMaterialManager.csproj          # Multi-target: net48;net8.0-windows
      App.cs                                 # IExternalApplication (via Nice3point ExternalApplication)
      Commands/
        ShowWindowCommand.cs                 # IExternalCommand (via Nice3point ExternalCommand)
      Events/
        RevitEventBridge.cs                  # IExternalEventHandler singleton
        RevitRequestType.cs                  # Enum for dispatch
        RevitRequestData.cs                  # Request/response DTOs
      Models/
        LayerDto.cs                          # DTO for compound layers
        MaterialDto.cs                       # DTO for material data
        RevitDocInfoDto.cs                   # DTO for document info (Phase 1 proof)
      ViewModels/
        MainWindowViewModel.cs               # Coordinates child VMs
        LeftPanelViewModel.cs                # Familles/Types (shell in Phase 1)
        CenterPanelViewModel.cs              # Couches/Parametres (shell in Phase 1)
        RightPanelViewModel.cs               # Materiaux preset (shell in Phase 1)
      Views/
        MainWindow.xaml / .xaml.cs           # Modeless singleton window
        LeftPanelView.xaml / .xaml.cs         # UserControl (shell)
        CenterPanelView.xaml / .xaml.cs       # UserControl (shell)
        RightPanelView.xaml / .xaml.cs        # UserControl (shell)
      Themes/
        OlympeTheme.xaml                     # Dark theme ResourceDictionary
      Helpers/
        ElementIdHelper.cs                   # Safe ElementId.Value access
      Properties/
        AssemblyInfo.cs                      # Only if needed for net48
    OlympeMaterialManager.Installer/
      OlympeMaterialManager.Installer.wixproj  # WiX v5 (Phase 5)
  addin/
    OlympeMaterialManager.2024.addin         # .addin manifest for dev
    OlympeMaterialManager.2025.addin
    OlympeMaterialManager.2026.addin
```

### Pattern 1: Nice3point ExternalApplication Base Class

**What:** Inherit from `Nice3point.Revit.Toolkit.ExternalApplication` instead of implementing `IExternalApplication` directly.
**When to use:** Always for the main entry point.
**Why:** Provides automatic dependency resolution (avoids `FileNotFoundException`), simplified API, built-in `Application` property, and ribbon panel creation helpers.

```csharp
// Source: https://github.com/Nice3point/RevitToolkit
public class App : ExternalApplication
{
    internal static ExternalEvent RevitEvent { get; private set; }
    internal static RevitEventBridge EventBridge { get; private set; }
    internal static MainWindow MainWindow { get; private set; }

    public override void OnStartup()
    {
        // Create the event bridge and external event
        EventBridge = new RevitEventBridge();
        RevitEvent = new ExternalEvent(application =>
        {
            EventBridge.ProcessRequest(application);
        });

        // Create ribbon panel with button
        var panel = Application.CreatePanel("Olympe", "OlympeMaterialManager");
        panel.AddPushButton<ShowWindowCommand>("Materiaux")
            .SetLargeImage("/OlympeMaterialManager;component/Resources/Icons/MaterialManager32.png")
            .SetImage("/OlympeMaterialManager;component/Resources/Icons/MaterialManager16.png");
    }

    public override void OnShutdown()
    {
        MainWindow?.Close();
        RevitEvent?.Dispose();
    }
}
```

### Pattern 2: ExternalEvent with Enum Dispatch (RevitEventBridge)

**What:** Single handler class with enum-based routing for all Revit API calls.
**When to use:** For all ViewModel-initiated Revit operations.
**Why:** One ExternalEvent instance, clean dispatch, no Revit types in ViewModels.

**CRITICAL FINDING: Nice3point.Revit.Toolkit's `ExternalEvent<T>` does NOT fit the enum dispatch pattern (D-09/D-11).**

`ExternalEvent<T>` accepts a lambda at construction and passes a typed argument at `Raise(T)` time. It is designed for one-handler-per-action, not one-handler-with-enum-dispatch. The generic `T` is the data argument, not the request type.

**Recommendation for D-11:** Do NOT use `ExternalEvent<T>` for enum dispatch. Instead use the non-generic `ExternalEvent` from the Toolkit combined with a custom `RevitEventBridge`:

```csharp
// RevitRequestType.cs
public enum RevitRequestType
{
    None,
    GetDocumentInfo,      // Phase 1 proof-of-concept
    // Future: ReadCompoundLayers, SetLayerMaterial, EditMaterial, etc.
}

// RevitEventBridge.cs
public class RevitEventBridge
{
    private volatile RevitRequestType _requestType = RevitRequestType.None;
    private volatile object _requestData;
    private Action<object> _resultCallback;
    private readonly object _lock = new();

    public void MakeRequest(RevitRequestType type, object data, Action<object> callback)
    {
        lock (_lock)
        {
            _requestType = type;
            _requestData = data;
            _resultCallback = callback;
        }
        App.RevitEvent.Raise();
    }

    public void ProcessRequest(UIApplication uiApp)
    {
        RevitRequestType type;
        object data;
        Action<object> callback;

        lock (_lock)
        {
            type = _requestType;
            data = _requestData;
            callback = _resultCallback;
            _requestType = RevitRequestType.None;
        }

        object result = null;
        try
        {
            switch (type)
            {
                case RevitRequestType.GetDocumentInfo:
                    result = HandleGetDocumentInfo(uiApp);
                    break;
            }
        }
        catch (Exception ex)
        {
            result = ex; // Pass error back to VM
        }

        if (callback != null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => callback(result));
        }
    }

    private RevitDocInfoDto HandleGetDocumentInfo(UIApplication uiApp)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        return new RevitDocInfoDto
        {
            Title = doc?.Title ?? "(aucun document)",
            PathName = doc?.PathName ?? "",
            IsValid = doc != null
        };
    }
}
```

### Pattern 3: Modeless Singleton Window with Hide-on-Close

**What:** Create the window once, show/hide it. Intercept closing to hide instead of dispose.
**When to use:** The main add-in window.
**Why:** Prevents InvalidOperationException from showing a disposed window. Preserves UI state across show/hide.

```csharp
// ShowWindowCommand.cs
[Transaction(TransactionMode.Manual)]
public class ShowWindowCommand : ExternalCommand
{
    public override void Execute()
    {
        if (App.MainWindow == null)
        {
            var vm = new MainWindowViewModel(App.EventBridge);
            App.MainWindow = new MainWindow { DataContext = vm };

            // Set Revit as owner for proper Z-order
            var helper = new WindowInteropHelper(App.MainWindow);
            helper.Owner = Application.MainWindowHandle;

            // Intercept close to hide instead
            App.MainWindow.Closing += (sender, e) =>
            {
                e.Cancel = true;
                App.MainWindow.Hide();
            };
        }

        App.MainWindow.Show();
        App.MainWindow.Activate();
    }
}
```

**IMPORTANT NUANCES:**
- `WindowInteropHelper.Owner` must be set to `UIApplication.MainWindowHandle` (available via `Application.MainWindowHandle` in Nice3point Toolkit's ExternalCommand).
- `Closing` event is intercepted with `e.Cancel = true` followed by `Hide()`. This prevents the window from being disposed.
- The window is created lazily on first command execution, not in `OnStartup()`. This avoids creating WPF resources before the ribbon button is clicked.
- Set `Topmost = false` (never `true` -- that steals focus from all desktop windows).
- In `OnShutdown()`, call `MainWindow?.Close()` -- at that point, detach the Closing handler first or set a flag to allow actual close.

### Pattern 4: WPF Dark Theme via ResourceDictionary

**What:** Custom Olympe dark theme applied via MergedDictionaries.
**When to use:** All windows and controls in the add-in.
**Why:** Consistent professional appearance. No third-party UI library needed.

```xml
<!-- Themes/OlympeTheme.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Couleurs Olympe -->
    <Color x:Key="BackgroundColor">#1E1E2E</Color>
    <Color x:Key="SurfaceColor">#2D2D3D</Color>
    <Color x:Key="AccentColor">#FF9800</Color>
    <Color x:Key="AccentHoverColor">#FFA726</Color>
    <Color x:Key="TextPrimaryColor">#E0E0E0</Color>
    <Color x:Key="TextSecondaryColor">#A0A0A0</Color>
    <Color x:Key="BorderColor">#3D3D4D</Color>
    <Color x:Key="ErrorColor">#EF5350</Color>

    <!-- Pinceaux -->
    <SolidColorBrush x:Key="BackgroundBrush" Color="{StaticResource BackgroundColor}" />
    <SolidColorBrush x:Key="SurfaceBrush" Color="{StaticResource SurfaceColor}" />
    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}" />
    <SolidColorBrush x:Key="AccentHoverBrush" Color="{StaticResource AccentHoverColor}" />
    <SolidColorBrush x:Key="TextPrimaryBrush" Color="{StaticResource TextPrimaryColor}" />
    <SolidColorBrush x:Key="TextSecondaryBrush" Color="{StaticResource TextSecondaryColor}" />
    <SolidColorBrush x:Key="BorderBrush" Color="{StaticResource BorderColor}" />
    <SolidColorBrush x:Key="ErrorBrush" Color="{StaticResource ErrorColor}" />

    <!-- Style Window global -->
    <Style TargetType="Window" x:Key="OlympeWindowStyle">
        <Setter Property="Background" Value="{StaticResource BackgroundBrush}" />
        <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}" />
        <Setter Property="FontFamily" Value="Segoe UI" />
        <Setter Property="FontSize" Value="13" />
    </Style>

    <!-- Style Button Olympe -->
    <Style TargetType="Button">
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
        <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Padding" Value="12,6" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="4"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center"
                                          VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background"
                                    Value="{StaticResource AccentBrush}" />
                            <Setter TargetName="border" Property="BorderBrush"
                                    Value="{StaticResource AccentBrush}" />
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="border" Property="Background"
                                    Value="{StaticResource AccentHoverBrush}" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="border" Property="Opacity" Value="0.5" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Style TextBlock -->
    <Style TargetType="TextBlock">
        <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}" />
    </Style>

    <!-- Style TextBox -->
    <Style TargetType="TextBox">
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
        <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
        <Setter Property="CaretBrush" Value="{StaticResource TextPrimaryBrush}" />
        <Setter Property="Padding" Value="6,4" />
    </Style>

    <!-- Styles for ListBox, TreeView, ScrollBar, ComboBox, GridSplitter
         must also be defined. Each needs a full ControlTemplate override
         for dark theme to work properly. -->
</ResourceDictionary>
```

**Integration in MainWindow.xaml:**
```xml
<Window x:Class="Olympe.MaterialManager.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="Olympe MaterialManager"
        Width="900" Height="650" MinWidth="750" MinHeight="500"
        Style="{StaticResource OlympeWindowStyle}">
    <Window.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="/Themes/OlympeTheme.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Window.Resources>
    <!-- ... -->
</Window>
```

**IMPORTANT:** In a multi-target csproj, XAML `Source` URIs use the assembly name, not the project name. Since both TFMs produce `OlympeMaterialManager.dll`, the pack URI format `"/Themes/OlympeTheme.xaml"` works the same for both net48 and net8.0-windows.

### Pattern 5: Three-Column Layout with GridSplitters

```xml
<!-- MainWindow.xaml content area -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="250" MinWidth="200" />
        <ColumnDefinition Width="5" />  <!-- GridSplitter -->
        <ColumnDefinition Width="*" MinWidth="300" />
        <ColumnDefinition Width="5" />  <!-- GridSplitter -->
        <ColumnDefinition Width="250" MinWidth="200" />
    </Grid.ColumnDefinitions>

    <!-- Panneau gauche : Familles/Types -->
    <views:LeftPanelView Grid.Column="0" DataContext="{Binding LeftPanelVM}" />

    <GridSplitter Grid.Column="1" Width="5" HorizontalAlignment="Stretch"
                  Background="{StaticResource BorderBrush}" />

    <!-- Panneau centre : Couches/Parametres -->
    <views:CenterPanelView Grid.Column="2" DataContext="{Binding CenterPanelVM}" />

    <GridSplitter Grid.Column="3" Width="5" HorizontalAlignment="Stretch"
                  Background="{StaticResource BorderBrush}" />

    <!-- Panneau droit : Materiaux preset -->
    <views:RightPanelView Grid.Column="4" DataContext="{Binding RightPanelVM}" />
</Grid>
```

### Anti-Patterns to Avoid
- **Revit API types in ViewModels:** Any `using Autodesk.Revit.DB` in a ViewModel file is a code smell. ViewModels work only with DTOs and primitives.
- **ExternalEvent.Raise() on Revit thread:** Only call Raise() from the UI thread (ViewModel commands). Never from inside `IExternalEventHandler.Execute()`.
- **Multiple ExternalEvent instances for dispatch:** Stick to one ExternalEvent + enum dispatch per D-09.
- **Window.Close() for hide:** Always use `Hide()` via Closing cancellation. `Close()` disposes the window permanently.
- **ElementId.IntegerValue:** Always use `.Value` (long) per D-04.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| IExternalApplication boilerplate | Raw interface implementation | Nice3point.Revit.Toolkit `ExternalApplication` | Automatic dependency resolution, simplified OnStartup/OnShutdown, ribbon helpers |
| IExternalCommand boilerplate | Raw interface implementation | Nice3point.Revit.Toolkit `ExternalCommand` | Same benefits; built-in Application property |
| ExternalEvent lifecycle | Manual ExternalEvent.Create() + IExternalEventHandler | Nice3point.Revit.Toolkit `ExternalEvent` (non-generic) | No need to implement IExternalEventHandler interface; lambda-based; auto-initializes |
| MVVM boilerplate (INotifyPropertyChanged) | Manual OnPropertyChanged | CommunityToolkit.Mvvm [ObservableProperty] source generator | Zero-boilerplate, compile-time safe |
| Command plumbing (ICommand) | Custom ICommand implementation | CommunityToolkit.Mvvm [RelayCommand] | Automatic CanExecute, async support |
| C# 12 on net48 | Manual polyfill types | PolySharp 1.15.0 | Generates all needed polyfills automatically |
| WPF event-to-command | Custom attached behaviors | Microsoft.Xaml.Behaviors.Wpf EventTrigger + InvokeCommandAction | Standard, tested, MVVM-friendly |

**Key insight:** Nice3point.Revit.Toolkit eliminates ~60% of Revit add-in boilerplate. CommunityToolkit.Mvvm eliminates ~70% of MVVM boilerplate. Together they keep the codebase focused on business logic.

## Common Pitfalls

### Pitfall 1: WPF XAML Compilation Failure Across net48 and net8.0-windows
**What goes wrong:** The `Microsoft.NET.Sdk` may fail to compile XAML resources when both `net48` and `net8.0-windows` are in `<TargetFrameworks>`. Errors include `MC1000: Unknown build error`, missing `netstandard` assembly references, or XAML resources not found at runtime.
**Why it happens:** The XAML compilation pipeline (PresentationBuildTasks) has different code paths for .NET Framework vs .NET Core. In .NET SDK < 5.0.200, `UseWPF` did not work with net48 under `Microsoft.NET.Sdk` (required `Microsoft.NET.Sdk.WindowsDesktop`). The dev machine has SDK 10.0.201, so this should be resolved, but edge cases remain.
**How to avoid:** Build the multi-target project first as a spike (empty project, one XAML ResourceDictionary, one Window). If XAML compilation fails, immediately fall back to Shared Project approach (D-01 fallback). Do NOT spend hours debugging MSBuild XAML issues.
**Warning signs:** Build errors mentioning `PresentationBuildTasks`, `MC1000`, or `netstandard` assembly. Resources loading as `null` at runtime despite compiling.

### Pitfall 2: CopyLocal=True on Revit API References
**What goes wrong:** If Revit API DLLs are copied to output, .NET loads the local copies instead of Revit's own, causing type identity conflicts.
**Why it happens:** NuGet packages may not always default to `PrivateAssets=all`. The Nice3point packages do set CopyLocal=false by default, but verify after restore.
**How to avoid:** After first `dotnet restore`, check `bin/Debug/net48/` and `bin/Debug/net8.0-windows/` for `RevitAPI.dll` or `RevitAPIUI.dll`. If present, add `<PrivateAssets>all</PrivateAssets>` or `<Private>false</Private>` explicitly.
**Warning signs:** `InvalidCastException`, `MissingMethodException`, or `FileLoadException` at runtime in Revit.

### Pitfall 3: Modeless Window Memory Leak
**What goes wrong:** Each show/close cycle creates new event subscriptions without unsubscribing. The window graph stays rooted in memory.
**Why it happens:** In Revit, the host process persists for hours/days. A "closed" window that still has event handlers registered never gets garbage collected.
**How to avoid:** Use the singleton pattern (D-13): create once, show/hide. The Closing event is canceled and the window is hidden, not disposed. In `OnShutdown()`, actually close and dispose. Never create new MainWindow instances per command execution.
**Warning signs:** Memory grows after each show/hide cycle. Old event handlers fire after the window is hidden.

### Pitfall 4: ExternalEvent Not Raised in Valid Context
**What goes wrong:** `ExternalEvent.Raise()` is called from the wrong thread or at the wrong time. The event silently fails (returns `Pending` or `Denied`).
**Why it happens:** Raise() must be called from a non-Revit-API context (the WPF UI thread). If called from inside `IExternalEventHandler.Execute()` or during a transaction, it fails.
**How to avoid:** Only call `Raise()` from ViewModel commands (which run on the UI thread). Check the return value of `Raise()` -- it returns `ExternalEventRequest.Accepted`, `Pending`, or `Denied`.
**Warning signs:** Raise() returns `Denied` or `Pending`. The handler never fires after button click.

### Pitfall 5: ResourceDictionary Source URI Resolution in Multi-Target
**What goes wrong:** XAML `Source="/Themes/OlympeTheme.xaml"` fails to resolve because the assembly name or resource path differs between net48 and net8.0-windows builds.
**Why it happens:** Resource paths in XAML are pack URIs. The format depends on the assembly name. If the assembly name changes between targets, the URI breaks.
**How to avoid:** Ensure `<AssemblyName>` is identical for both TFMs in the csproj. Use relative Source paths (`Source="/Themes/OlympeTheme.xaml"`) not full pack URIs. Both TFMs will produce `OlympeMaterialManager.dll` with the same assembly name.
**Warning signs:** `XamlParseException` or `IOException` when loading ResourceDictionary at runtime. Theme not applied -- default Windows chrome visible.

## Code Examples

### .addin File Format (Revit 2024 Example)

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Olympe MaterialManager</Name>
    <Assembly>C:\path\to\OlympeMaterialManager.dll</Assembly>
    <AddInId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</AddInId>
    <FullClassName>Olympe.MaterialManager.App</FullClassName>
    <VendorId>OLYMPE</VendorId>
    <VendorDescription>Olympe</VendorDescription>
  </AddIn>
</RevitAddIns>
```

**Per-version placement:**
- `%APPDATA%\Autodesk\Revit\Addins\2024\OlympeMaterialManager.addin`
- `%APPDATA%\Autodesk\Revit\Addins\2025\OlympeMaterialManager.addin`
- `%APPDATA%\Autodesk\Revit\Addins\2026\OlympeMaterialManager.addin`

Each file has the SAME `<AddInId>` GUID but different `<Assembly>` paths pointing to the net48 or net8.0-windows output:
- 2024: `...\bin\Release\net48\OlympeMaterialManager.dll`
- 2025/2026: `...\bin\Release\net8.0-windows\OlympeMaterialManager.dll`

**GUID generation:** Generate ONE GUID for the add-in. Use the same GUID across all three .addin files. Different GUIDs would register as separate add-ins.

### CommunityToolkit.Mvvm ViewModel Example (Phase 1 Shell)

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Olympe.MaterialManager.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _titre = "Olympe MaterialManager";

    [ObservableProperty]
    private string _documentInfo = "Aucun document";

    public LeftPanelViewModel LeftPanelVM { get; }
    public CenterPanelViewModel CenterPanelVM { get; }
    public RightPanelViewModel RightPanelVM { get; }

    private readonly RevitEventBridge _eventBridge;

    public MainWindowViewModel(RevitEventBridge eventBridge)
    {
        _eventBridge = eventBridge;
        LeftPanelVM = new LeftPanelViewModel();
        CenterPanelVM = new CenterPanelViewModel();
        RightPanelVM = new RightPanelViewModel();
    }

    [RelayCommand]
    private void RafraichirDocument()
    {
        _eventBridge.MakeRequest(
            RevitRequestType.GetDocumentInfo,
            null,
            result =>
            {
                if (result is RevitDocInfoDto info)
                {
                    DocumentInfo = info.IsValid
                        ? $"Document : {info.Title}"
                        : "Aucun document ouvert";
                }
            });
    }
}
```

### ElementId Safe Usage Helper

```csharp
namespace Olympe.MaterialManager.Helpers;

public static class ElementIdHelper
{
    /// <summary>
    /// Gets the long value from an ElementId. Always use .Value, never .IntegerValue.
    /// Convention D-04: day-one rule.
    /// </summary>
    public static long GetValue(Autodesk.Revit.DB.ElementId id)
    {
        return id.Value; // long -- safe for 64-bit ElementIds
    }

    public static Autodesk.Revit.DB.ElementId FromValue(long value)
    {
        return new Autodesk.Revit.DB.ElementId(value);
    }
}
```

### Shared Project Fallback Structure (if D-01 primary fails)

If multi-target XAML compilation fails:

```
OlympeMaterialManager/
  OlympeMaterialManager.sln
  src/
    OlympeMaterialManager.Shared/
      OlympeMaterialManager.Shared.shproj     # Shared Project
      OlympeMaterialManager.Shared.projitems  # File list
      (all .cs and .xaml files here)
    OlympeMaterialManager.2024/
      OlympeMaterialManager.2024.csproj       # net48, imports Shared
    OlympeMaterialManager.Net8/
      OlympeMaterialManager.Net8.csproj       # net8.0-windows, imports Shared
```

Both target projects import the shared project:
```xml
<Import Project="..\OlympeMaterialManager.Shared\OlympeMaterialManager.Shared.projitems"
        Label="Shared" />
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Shared Project + N target projects | Single multi-target csproj OR Nice3point.Revit.Sdk configuration-based | 2024+ (.NET SDK improvements) | Simpler build, fewer files. But Shared Project still valid fallback. |
| Raw IExternalEventHandler | Nice3point.Revit.Toolkit ExternalEvent / AsyncExternalEvent | 2023+ (Toolkit matured) | Lambda-based, no interface boilerplate, auto-initialization |
| ElementId.IntegerValue (int32) | ElementId.Value (int64) | Revit 2024 | BREAKING: IntegerValue deprecated, constructor changed. Must use long. |
| WiX v3/v4 | WiX v5.0.2 | Feb 2025 (v4 EOL) | v4 no longer receives security patches |
| .NET Framework 4.8 only | net48 + net8.0-windows | Revit 2025 | Must multi-target for 2024+2025/2026 compatibility |

## Open Questions

1. **WPF XAML compilation across TFMs**
   - What we know: SDK 10.0.201 should resolve the `UseWPF` + net48 issue (fixed in 5.0.200). Nice3point ecosystem uses configuration-based approach which avoids this entirely.
   - What's unclear: Whether complex XAML (ResourceDictionary with full ControlTemplates) compiles without issues in both TFMs simultaneously. No firsthand verification.
   - Recommendation: Build the multi-target spike as the very first task. Keep it to under 2 hours. Fall back to Shared Project if any XAML issue surfaces.

2. **Nice3point.Revit.Toolkit ExternalEvent thread safety**
   - What we know: The Toolkit's ExternalEvent can be created anywhere (not just in Revit API context). Raise() queues the delegate.
   - What's unclear: Whether the lambda capture in `new ExternalEvent(app => { bridge.ProcessRequest(app); })` has any thread safety implications when the bridge has mutable state.
   - Recommendation: Use explicit locking in RevitEventBridge (shown in code example above). The Revit API guarantees single-threaded execution of the handler, but the MakeRequest() call comes from the UI thread.

3. **Net8.0-windows vs net8.0 for Revit API packages**
   - What we know: Nice3point.Revit.Api packages target `net8.0` (not `net8.0-windows`). Our project targets `net8.0-windows` (required for WPF).
   - What's unclear: Whether NuGet resolution handles `net8.0` packages consumed by `net8.0-windows` projects seamlessly.
   - Recommendation: This should work (net8.0-windows is a superset of net8.0). Verify during build spike.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build toolchain | Yes | 10.0.201 | -- |
| .NET 8 Runtime | Revit 2025/2026 target | Yes | 8.0.25 | -- |
| .NET Framework 4.8 Targeting Pack | Revit 2024 target | Yes | 4.8 | -- |
| WPF Desktop Runtime | WPF compilation | Yes | via WindowsDesktop.App 8.0.25 | -- |
| Git | Version control | Yes | 2.53.0 | -- |
| Revit 2024 | Runtime testing | Not verified | -- | Manual test on target machine |
| Revit 2025 | Runtime testing | Not verified | -- | Manual test on target machine |
| Revit 2026 | Runtime testing | Not verified | -- | Manual test on target machine |

**Missing dependencies with no fallback:**
- Revit installations cannot be verified from the build machine. Testing requires manual deployment of .addin files and DLLs to a machine with Revit installed.

**Missing dependencies with fallback:**
- None. All build-time dependencies are present.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Manual validation (no automated test framework in Phase 1) |
| Config file | None -- Phase 1 focuses on build validation and runtime testing in Revit |
| Quick run command | `dotnet build src/OlympeMaterialManager/OlympeMaterialManager.csproj` |
| Full suite command | Build + deploy .addin + launch Revit + verify ribbon button + open window |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| INFRA-01 | Multi-target build succeeds | build | `dotnet build -c Release` | Wave 0 |
| INFRA-02 | CopyLocal=false verified | build/manual | Check output dir for RevitAPI.dll absence | Manual |
| INFRA-03 | .addin file loads in Revit | manual | Deploy + launch Revit | Manual |
| INFRA-04 | Ribbon button appears | manual | Visual check in Revit | Manual |
| INFRA-05 | ExternalEvent dispatches | manual | Click "Rafraichir" button, verify document info updates | Manual |
| INFRA-06 | Window show/hide persistence | manual | Open, close, reopen window -- verify no crash | Manual |
| INFRA-07 | No Revit types in ViewModels | review | Code review / grep for `Autodesk.Revit` in ViewModels/ | Manual |
| INFRA-08 | CommunityToolkit.Mvvm works | build | Build succeeds with [ObservableProperty] in ViewModel | Wave 0 |
| INFRA-09 | Both TFM outputs load in Revit | manual | Test net48 DLL in Revit 2024, net8 DLL in Revit 2025/2026 | Manual |
| UI-01 | Three-column layout visible | manual | Visual check | Manual |
| UI-02 | Dark theme applied | manual | Visual check -- dark background, amber accent | Manual |
| UI-04 | French text | manual | Visual check -- all labels in French | Manual |

### Sampling Rate
- **Per task commit:** `dotnet build src/OlympeMaterialManager/OlympeMaterialManager.csproj`
- **Per wave merge:** Full build both TFMs + visual inspection
- **Phase gate:** All manual Revit tests pass

### Wave 0 Gaps
- [ ] Build spike validation script (dotnet build + check output directories)
- [ ] No automated UI tests -- all Phase 1 validation is build-time or manual Revit testing

## Sources

### Primary (HIGH confidence)
- [Nice3point/RevitToolkit GitHub](https://github.com/Nice3point/RevitToolkit) -- ExternalApplication, ExternalCommand, ExternalEvent API
- [Nice3point.Revit.Toolkit NuGet 2024.3.0](https://www.nuget.org/packages/Nice3point.Revit.Toolkit/2024.3.0) -- targets net48
- [Nice3point.Revit.Toolkit NuGet 2026.1.0](https://www.nuget.org/packages/Nice3point.Revit.Toolkit/2026.1.0) -- targets net8.0-windows7.0
- [Nice3point.Revit.Api.RevitAPI NuGet 2024.3.30](https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPI/2024.3.30) -- targets net48
- [CommunityToolkit.Mvvm NuGet 8.4.2](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2) -- targets netstandard2.0 + net8.0
- [PolySharp NuGet 1.15.0](https://www.nuget.org/packages/PolySharp) -- source generator, netstandard2.0
- [Microsoft.Xaml.Behaviors.Wpf NuGet 1.1.142](https://www.nuget.org/packages/Microsoft.Xaml.Behaviors.Wpf) -- supports net462+ and net8.0-windows
- [Microsoft Learn: Target frameworks in SDK-style projects](https://learn.microsoft.com/en-us/dotnet/standard/frameworks)
- [dotnet/wpf Issue #3865: UseWpf with .NET Framework](https://github.com/dotnet/wpf/issues/3865) -- resolved in SDK 5.0.200
- [dotnet/wpf Issue #3534: WPF net48 SDK build failure](https://github.com/dotnet/wpf/issues/3534) -- tracked in dotnet/sdk#13939
- [Autodesk: Add-in Registration](https://help.autodesk.com/cloudhelp/2024/PTB/Revit-API/files/Revit_API_Developers_Guide/Introduction/Add_In_Integration/Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Add_in_Registration_html.html)

### Secondary (MEDIUM confidence)
- [DeepWiki: Nice3point/RevitTemplates](https://deepwiki.com/Nice3point/RevitTemplates/5-samples) -- configuration-based multi-version approach
- [The Building Coder: Modeless Form Focus](https://jeremytammik.github.io/tbc/a/1591_modeless_focus.html) -- WindowInteropHelper owner pattern
- [Medium: Prevent Multiple Windows in Revit Plugin](https://medium.com/@khalidfathyuar/prevent-multiple-windows-in-a-revit-plugin-manual-singleton-vs-window-controller-b84370851097) -- singleton patterns
- [David Rickard: Dark Theme in WPF](https://engy.us/blog/2018/10/20/dark-theme-in-wpf/) -- ResourceDictionary theming approach
- [Microsoft Q&A: Multi-targeting net48;net8.0](https://learn.microsoft.com/en-us/answers/questions/1856743/i-am-trying-to-target-same-project-file-to-net-fra) -- confirmed feasible with caveats
- [CommunityToolkit.Mvvm Issue #695](https://github.com/CommunityToolkit/dotnet/issues/695) -- net48 source generator compatibility

### Tertiary (LOW confidence)
- WPF XAML resource name differences between net48 and net8 builds -- reported in dotnet/winforms#12267 (WinForms, not WPF, but similar build pipeline concern)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all packages verified on NuGet with exact versions and TFM targets
- Architecture: HIGH -- patterns derived from official Nice3point Toolkit docs and established Revit community patterns
- Multi-target csproj: MEDIUM -- SDK version supports it, but WPF XAML compilation across TFMs has not been firsthand verified. Build spike is essential.
- Pitfalls: HIGH -- sourced from official docs, The Building Coder, and Autodesk forums
- Theme: HIGH -- standard WPF ResourceDictionary approach, no novel techniques

**Research date:** 2026-04-11
**Valid until:** 2026-05-11 (stable domain -- Revit API changes annually, NuGet versions may update)
