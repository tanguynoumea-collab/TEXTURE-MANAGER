# Technology Stack

**Project:** Olympe MaterialManager
**Researched:** 2026-04-11
**Overall confidence:** HIGH

---

## Critical Finding: .NET Framework Split

**The PROJECT.md states ".NET Framework 4.8" as the only runtime. This is incorrect for multi-version targeting.**

| Revit Version | .NET Runtime        | Target Framework Moniker |
|---------------|---------------------|--------------------------|
| Revit 2024    | .NET Framework 4.8  | `net48`                  |
| Revit 2025    | .NET 8              | `net8.0-windows`         |
| Revit 2026    | .NET 8              | `net8.0-windows`         |

Revit 2025 moved to .NET 8 (confirmed by Autodesk migration guide). Revit 2026 continues on .NET 8. Any add-in targeting all three versions MUST multi-target `net48` and `net8.0-windows`. This is the single most important architectural decision for the project.

**Confidence:** HIGH -- verified via Autodesk official migration docs and NuGet package metadata.

**Source:** https://help.autodesk.com/view/RVT/2025/CHS/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Using_the_Autodesk_Revit_API_NET8_Update_html

---

## Recommended Stack

### Solution Architecture

Use a **single SDK-style .csproj with multi-targeting**, NOT the Shared Project approach from PROJECT.md.

**Rationale:** The PROJECT.md proposes "1 shared project + 3 target projects + 1 installer project." With SDK-style csproj and `<TargetFrameworks>`, a single project can output DLLs for all three Revit versions. This eliminates 3 redundant project files and simplifies dependency management. The Shared Project approach is legacy; the modern Revit community (ricaun, Nice3point, archi-lab) has converged on multi-targeting in SDK-style csproj.

**Recommended solution structure:**
```
OlympeMaterialManager.sln
  |-- src/OlympeMaterialManager/OlympeMaterialManager.csproj  (multi-target: net48;net8.0-windows)
  |-- src/OlympeMaterialManager.Installer/OlympeMaterialManager.Installer.wixproj
```

**Confidence:** HIGH -- this is the standard modern approach confirmed by multiple Revit developer resources (Building Coder, archi-lab, Nice3point templates, ricaun).

**Source:** https://archi-lab.net/how-to-maintain-revit-plugins-for-multiple-versions-continued/

---

### Core Framework

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| C# | 12.0 (LangVersion) | Language | Modern syntax (primary expressions, collection expressions). Works on net48 via PolySharp polyfills. Use `<LangVersion>12.0</LangVersion>` in csproj. | HIGH |
| .NET Framework 4.8 | 4.8 | Runtime for Revit 2024 | Required by Revit 2024 host process | HIGH |
| .NET 8 | 8.0 | Runtime for Revit 2025/2026 | Required by Revit 2025+ host process | HIGH |
| WPF | built-in | UI framework | Required -- Revit dockable panes and modeless windows use WPF. Native to both net48 and net8.0-windows. | HIGH |

### Revit API NuGet Packages

| Package | Version(s) | Purpose | Why | Confidence |
|---------|-----------|---------|-----|------------|
| Nice3point.Revit.Api.RevitAPI | 2024.x / 2025.x / 2026.x | Revit API assemblies | Clean NuGet distribution of official Revit API DLLs. CopyLocal=false by default. Version-specific packages per Revit year. Maintained actively with 2026.4.0 already published. | HIGH |
| Nice3point.Revit.Api.RevitAPIUI | 2024.x / 2025.x / 2026.x | Revit UI API assemblies | Same rationale -- provides UIApplication, ribbon, dialogs. | HIGH |
| Nice3point.Revit.Toolkit | 2024.x / 2025.x / 2026.x | Revit development helpers | Provides base classes: ExternalCommand, ExternalApplication, AsyncExternalCommand, typed ExternalEventHandler<T>, AsyncExternalEvent<T>, automatic dependency resolution. Eliminates boilerplate. | HIGH |

**Why Nice3point over Revit_All_Main_Versions_API_x64:** Nice3point packages are version-specific (one package per Revit year), which maps cleanly to conditional PackageReferences in the multi-target csproj. The `Revit_All_Main_Versions_API_x64` package is a single monolithic package -- it works but is less granular. Nice3point also provides the companion Toolkit and Extensions packages.

**Why Nice3point over raw Revit DLL references:** NuGet packages are reproducible, version-locked, and do not require Revit to be installed on the build machine.

**Conditional PackageReference pattern:**
```xml
<PropertyGroup>
  <TargetFrameworks>net48;net8.0-windows</TargetFrameworks>
  <LangVersion>12.0</LangVersion>
  <UseWPF>true</UseWPF>
</PropertyGroup>

<!-- Revit 2024 (net48) -->
<ItemGroup Condition="'$(TargetFramework)' == 'net48'">
  <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2024.4.0" />
  <PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="2024.4.0" />
  <PackageReference Include="Nice3point.Revit.Toolkit" Version="2024.0.0" />
</ItemGroup>

<!-- Revit 2025/2026 (net8.0-windows) - use 2025 as base, #if for 2026 specifics -->
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0-windows'">
  <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2026.4.0" />
  <PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="2026.4.0" />
  <PackageReference Include="Nice3point.Revit.Toolkit" Version="2026.0.0" />
</ItemGroup>
```

**Note on Revit 2025 vs 2026 targeting:** Both use net8.0-windows. To produce separate DLLs for 2025 and 2026, use custom build configurations or MSBuild properties (e.g., `RevitVersion` property) rather than separate TargetFrameworks. The API differences between 2025 and 2026 are minimal for material management. The 2026 API is backward-compatible with 2025 for the material/CompoundStructure surface area used by this project. Use the 2026 assemblies and test against both Revit 2025 and 2026 -- if a 2026-only API is needed, use `#if` conditional compilation with a custom `REVIT2026` symbol.

**Confidence:** HIGH

**Source:** https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPI, https://github.com/Nice3point/RevitToolkit

---

### MVVM Framework

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| CommunityToolkit.Mvvm | 8.4.2 | MVVM infrastructure | Source generators for ObservableProperty, RelayCommand, ObservableObject. Targets netstandard2.0 (consumable by net48) and net8.0. Industry standard for WPF MVVM in 2025/2026. | HIGH |
| PolySharp | 1.15.0 | C# polyfills for net48 | Required to use C# 12 language features and CommunityToolkit.Mvvm source generators on .NET Framework 4.8. Generates source-only polyfills for missing runtime types. | HIGH |

**Critical: Source generators on .NET Framework 4.8 WORK but require:**
1. SDK-style csproj (not legacy format) -- **mandatory**
2. `<PackageReference>` format (not packages.config) -- **mandatory**
3. `<LangVersion>12.0</LangVersion>` (or at least 8.0) -- **mandatory**
4. PolySharp NuGet package -- **recommended** for full C# 12 compatibility on net48

This was verified against GitHub issue #695 on CommunityToolkit/dotnet. The source generators fail ONLY when using legacy project format or packages.config. With SDK-style csproj + PackageReference, [ObservableProperty], [RelayCommand], and ObservableObject all work on net48.

**Do NOT put ViewModels in a separate .NET Standard 2.0 project.** That workaround (frequently cited in forums) is unnecessary if you use SDK-style csproj correctly. It adds complexity for no gain.

**Confidence:** HIGH -- verified via official GitHub issue resolution and multiple community confirmations.

**Source:** https://github.com/CommunityToolkit/dotnet/issues/695, https://www.nuget.org/packages/CommunityToolkit.Mvvm

---

### JSON Serialization (Presets Persistence)

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| System.Text.Json | 8.0.5 (net8) / 6.0.x (net48) | JSON serialization for preset files | Cross-platform, no external dependency on net8. On net48, available via NuGet package `System.Text.Json`. Faster and lower memory than Newtonsoft.Json. Single API surface across both targets. | MEDIUM |

**Alternative considered:** Newtonsoft.Json 13.x -- more features, wider community familiarity. But System.Text.Json is built into .NET 8 (zero dependency) and available as a NuGet for net48. For the simple preset JSON schema (groups of materials with names/descriptions/colors), System.Text.Json is sufficient.

**If complex JSON needs arise** (dynamic objects, extensive polymorphism), switch to Newtonsoft.Json -- it works identically on both net48 and net8.

**Confidence:** MEDIUM -- either choice works; System.Text.Json is the forward-looking choice but Newtonsoft.Json is lower-risk for net48 compatibility.

---

### Installer

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| WiX Toolset | v5.0.2 (WixToolset.Sdk) | MSI/EXE installer | **NOT v4 as stated in PROJECT.md.** WiX v4 is end-of-life (out of community support since Feb 2025). WiX v5.0.2 is the last stable v5. WiX v6.0.2 and v7.0.0 are newer but v5 has the widest documentation and community examples. v5 syntax is highly compatible with v4. | HIGH |

**Why WiX v5 over v4:** v4 is EOL. No security patches. v5.0.2 is stable and well-documented.

**Why WiX v5 over v6/v7:** v6 introduced an "Open Source Maintenance Fee" and v7 is brand new (April 2026). v5 is the safe, proven choice. If v5 goes fully EOL during development, v6 is a straightforward upgrade (syntax compatible).

**Why WiX over Inno Setup or NSIS:** WiX produces proper MSI packages (Windows Installer), which is the professional standard for commercial add-ins. MSI supports per-user installation, repair, silent install, and proper uninstall -- all important for commercial distribution.

**Installer must deploy:**
- DLL to a chosen install directory (e.g., `%ProgramFiles%\Olympe\MaterialManager\`)
- `.addin` manifest file to `%APPDATA%\Autodesk\Revit\Addins\{version}\` for each selected Revit version
- Version selection UI in the installer (checkboxes for 2024/2025/2026)

**Confidence:** HIGH

**Source:** https://www.firegiant.com/blog/2025/2/6/wix-v3-and-wix-v4-are-no-longer-in-community-support/, https://www.nuget.org/packages/WixToolset.Sdk/5.0.2

---

### Supporting Libraries

| Library | Version | Purpose | When to Use | Confidence |
|---------|---------|---------|-------------|------------|
| PolySharp | 1.15.0 | C# language polyfills | Always -- enables C# 12 on net48 | HIGH |
| Nice3point.Revit.Extensions | 2024.x / 2026.x | Revit API extension methods | Convenience extensions for Element, Document, etc. Reduces boilerplate. | MEDIUM |
| System.Text.Json | 8.0.5 / NuGet for net48 | JSON preset persistence | Preset file read/write | MEDIUM |
| Microsoft.Xaml.Behaviors.Wpf | 1.1.122 | WPF behaviors (EventTrigger, etc.) | For MVVM-friendly event binding in XAML without code-behind | MEDIUM |

---

### Development Tools

| Tool | Version | Purpose | Confidence |
|------|---------|---------|------------|
| Visual Studio 2022 | 17.x (latest) | IDE | Required for .NET 8 SDK, WPF designer, and WiX extension. | HIGH |
| .NET 8 SDK | 8.0.x (latest) | Build toolchain | Required for Revit 2025/2026 targeting and source generator support. | HIGH |
| .NET Framework 4.8 Dev Pack | 4.8 | Build toolchain | Required for Revit 2024 targeting. | HIGH |
| HeatWave (FireGiant) | latest | WiX VS extension | Visual Studio integration for .wixproj editing. Free community edition. | MEDIUM |

---

## Revit API Version Differences for Material Management

### Stable API Surface (2024/2025/2026 -- no changes)

The Material API has been stable across all three target versions:

- `Material` class -- properties: Name, Color, SurfacePatternId, SurfaceForegroundPatternColor, CutPatternId, Transparency, Shininess, Smoothness, AppearanceAssetId, StructuralAssetId, ThermalAssetId
- `AppearanceAssetElement` -- wrapper for appearance assets
- `AppearanceAssetEditScope` -- edit scope pattern for modifying appearance properties (Start/Commit/Cancel)
- `Asset` / `AssetProperty` hierarchy -- for reading/writing individual appearance properties (color, texture, tint, etc.)
- `CompoundStructure` -- GetLayers(), SetLayers(), layer materials via CompoundStructureLayer.MaterialId
- `CompoundStructureLayer` -- MaterialId, Function, Width
- `ElementType.LookupParameter()` / `Element.get_Parameter()` -- for finding Material parameters on loaded families

**Confidence:** HIGH -- Material API has been stable since Revit 2019.

### Revit 2026-Specific Changes (relevant to this project)

| Change | Impact | Action |
|--------|--------|--------|
| CompoundStructure: core layer no longer required | Elements can now have finish-only walls. The add-in must handle `CompoundStructure` with zero core layers gracefully. | Check for core layer existence before assuming index positions. Use `GetLayers()` and iterate, do not assume core layer at fixed index. |
| CompoundStructure: customizable layer priority | New `LayerPriority` property, `GetLayerPriority()`, `SetLayerPriority()`, `IsValidLayerPriority()`, `ResetLayerPriority()`, `ResetAllLayersPriorities()` | Display layer priority in the UI if useful, but not required for MVP. |
| BuiltInParameter renames (classification/assembly codes) | Some BuiltInParameter enum values renamed | Unlikely to affect material management directly. Use conditional compilation `#if REVIT2026` if a renamed parameter is needed. |

### Revit 2025 Changes (relevant)

| Change | Impact | Action |
|--------|--------|--------|
| .NET 8 migration | Runtime change. All types, namespaces, APIs remain identical. | Multi-target the csproj. No code changes needed for material APIs. |

**Confidence:** HIGH -- verified via https://www.revitapidocs.com/2025/news and https://rvtdocs.com/2026/whatsnew

---

## Thread Model and ExternalEvent Pattern

The Revit API is **single-threaded**. All API calls must run on the Revit main thread. The modeless WPF window runs on the Revit UI thread (via `Window.Show()` from `IExternalCommand.Execute()`).

**Pattern for this project:**

1. `IExternalCommand.Execute()` opens the WPF window via `Show()` (modeless).
2. The window's DataContext is a ViewModel using CommunityToolkit.Mvvm.
3. When the user clicks "Set Mat", the ViewModel calls `ExternalEvent.Raise()`.
4. Revit schedules and calls `IExternalEventHandler.Execute()` on the main thread.
5. The handler runs the Transaction (material assignment) inside a valid API context.
6. Results are passed back to the ViewModel via properties or callbacks.

**Use Nice3point.Revit.Toolkit's typed event handlers:** `ExternalEvent<T>` and `AsyncExternalEvent<T>` simplify this pattern by allowing typed argument passing and async/await syntax.

**Do NOT use Revit.Async (KennanChan):** While it works, Nice3point.Revit.Toolkit provides the same async event functionality with better integration and active maintenance for Revit 2025/2026. Revit.Async targets netstandard2.1 and has not been explicitly updated for .NET 8.

**MVVM boundary rule:** ViewModels must NOT reference Revit API types directly. Pass ElementIds (as integers), material names, and other primitives between the ViewModel and the ExternalEventHandler. This keeps ViewModels testable and avoids accidental cross-thread API access.

**Confidence:** HIGH

**Source:** https://github.com/Nice3point/RevitToolkit, https://github.com/varolomer/RevitWPF

---

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Solution structure | Single multi-target csproj | Shared Project + 3 target projects | Redundant complexity. SDK-style multi-targeting is simpler, well-supported, and the modern standard. |
| Revit API NuGet | Nice3point.Revit.Api.* | Revit_All_Main_Versions_API_x64 | Monolithic package, less granular version control. Nice3point also provides Toolkit and Extensions. |
| Revit API NuGet | Nice3point.Revit.Api.* | Raw DLL references from Revit install | Requires Revit installed on build machine. Not reproducible. |
| MVVM | CommunityToolkit.Mvvm 8.4.2 | Prism.Wpf | Prism is heavyweight (navigation, regions, modules) -- overkill for a single-window add-in. CommunityToolkit is lighter, source-generator-based, and Microsoft-maintained. |
| MVVM | CommunityToolkit.Mvvm 8.4.2 | MVVM Light | Abandoned/unmaintained since 2018. |
| MVVM | CommunityToolkit.Mvvm 8.4.2 | Hand-rolled INotifyPropertyChanged | Boilerplate-heavy, error-prone, no justification when source generators exist. |
| JSON | System.Text.Json | Newtonsoft.Json 13.x | Both work. System.Text.Json is zero-dependency on net8 and forward-looking. Newtonsoft.Json is a valid fallback if complex scenarios arise. |
| Installer | WiX v5.0.2 | WiX v4.0.6 | v4 is EOL (no security patches since Feb 2025). |
| Installer | WiX v5.0.2 | Inno Setup | Not MSI-based. Inno Setup produces EXE installers. MSI is preferred for commercial/enterprise distribution. |
| Installer | WiX v5.0.2 | NSIS | Same concern as Inno Setup -- not MSI. |
| Async Revit | Nice3point.Revit.Toolkit (AsyncExternalEvent) | Revit.Async (KennanChan) | Less maintained, netstandard2.1 only, not updated for .NET 8 explicitly. Nice3point covers the same use case. |
| Polyfills | PolySharp 1.15.0 | None | Without PolySharp, C# 12 features (init properties, required members, etc.) fail on net48. |

---

## What NOT to Use

| Technology | Reason |
|------------|--------|
| .NET Core / .NET 5/6/7 | Revit 2025/2026 require specifically .NET 8. Earlier .NET Core versions are not supported. |
| WiX v4 | End-of-life. No security support. |
| WiX v3 | Ancient. End-of-life. |
| packages.config | Breaks CommunityToolkit.Mvvm source generators. Use PackageReference only. |
| Legacy .csproj format | Incompatible with multi-targeting. Use SDK-style. |
| MVVM Light | Abandoned since 2018. |
| Prism | Overkill for a single-window add-in. |
| Revit Macro Manager | Not relevant for add-in development. |
| DevExpress / Telerik WPF | Commercial UI libraries -- unnecessary cost. WPF built-in controls + custom styles are sufficient for the three-panel layout. |
| MaterialDesignInXAML | Adds significant dependency weight. The project specifies a custom "Olympe" dark theme with amber accents -- better to build custom ResourceDictionaries. |

---

## Installation Commands

```xml
<!-- In OlympeMaterialManager.csproj -->
<PropertyGroup>
  <TargetFrameworks>net48;net8.0-windows</TargetFrameworks>
  <LangVersion>12.0</LangVersion>
  <UseWPF>true</UseWPF>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <RootNamespace>Olympe.MaterialManager</RootNamespace>
  <AssemblyName>OlympeMaterialManager</AssemblyName>
</PropertyGroup>

<!-- Shared packages -->
<ItemGroup>
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
  <PackageReference Include="PolySharp" Version="1.15.0" PrivateAssets="all" />
  <PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.1.122" />
</ItemGroup>

<!-- net48 only (Revit 2024) -->
<ItemGroup Condition="'$(TargetFramework)' == 'net48'">
  <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2024.4.0" />
  <PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="2024.4.0" />
  <PackageReference Include="Nice3point.Revit.Toolkit" Version="2024.0.0" />
  <PackageReference Include="System.Text.Json" Version="8.0.5" />
</ItemGroup>

<!-- net8.0-windows (Revit 2025/2026) -->
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0-windows'">
  <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2026.4.0" />
  <PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="2026.4.0" />
  <PackageReference Include="Nice3point.Revit.Toolkit" Version="2026.0.0" />
</ItemGroup>
```

```xml
<!-- In OlympeMaterialManager.Installer.wixproj -->
<Project Sdk="WixToolset.Sdk/5.0.2">
  <PropertyGroup>
    <OutputType>Package</OutputType>
  </PropertyGroup>
</Project>
```

---

## Sources

### Official / Authoritative
- Autodesk .NET 8 Migration Guide: https://help.autodesk.com/view/RVT/2025/CHS/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Using_the_Autodesk_Revit_API_NET8_Update_html
- Revit API 2026 What's New: https://rvtdocs.com/2026/whatsnew
- Revit API 2025 Changes: https://www.revitapidocs.com/2025/news
- CommunityToolkit.Mvvm docs: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/
- CommunityToolkit.Mvvm 8.4 announcement: https://devblogs.microsoft.com/dotnet/announcing-the-dotnet-community-toolkit-840/
- WiX v4/v5 EOL notice: https://www.firegiant.com/blog/2025/2/6/wix-v3-and-wix-v4-are-no-longer-in-community-support/
- WixToolset.Sdk 5.0.2 NuGet: https://www.nuget.org/packages/WixToolset.Sdk/5.0.2

### Community / Ecosystem
- Nice3point RevitToolkit: https://github.com/Nice3point/RevitToolkit
- Nice3point RevitTemplates: https://github.com/Nice3point/RevitTemplates
- Nice3point RevitAPI NuGet: https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPI
- CommunityToolkit.Mvvm net48 issue: https://github.com/CommunityToolkit/dotnet/issues/695
- PolySharp: https://github.com/Sergio0694/PolySharp
- archi-lab multi-version guide: https://archi-lab.net/how-to-maintain-revit-plugins-for-multiple-versions-continued/
- ricaun .NET Core guide: https://ricaun.com/revit-api-net-core/
- RevitWPF modeless pattern: https://github.com/varolomer/RevitWPF
- Building Coder blog: https://thebuildingcoder.typepad.com/blog/net/
