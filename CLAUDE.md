# CLAUDE.md — Olympe MaterialManager

## Contexte
Add-in Revit WPF pour la gestion visuelle des materiaux sur une vue 3D active.
Trois panneaux : Familles/Types (gauche) | Couches (centre) | Materiaux preset (droite).
Multi-version : Revit 2024, 2025, 2026.

## Stack
- C# / .NET Framework 4.8
- WPF + MVVM (CommunityToolkit.Mvvm)
- Revit API (2024 / 2025 / 2026 — CopyLocal = false)
- WiX Toolset v4 (installer .exe)

## Structure solution
OlympeMaterialManager/
├── OlympeMaterialManager.Shared/     <- Logique + ViewModels + Views XAML
├── OlympeMaterialManager.2024/       <- Cible Revit 2024, reference RevitAPI 2024
├── OlympeMaterialManager.2025/       <- Cible Revit 2025, reference RevitAPI 2025
├── OlympeMaterialManager.2026/       <- Cible Revit 2026, reference RevitAPI 2026
├── OlympeMaterialManager.Installer/  <- Projet WiX, genere le .exe
└── CLAUDE.md

## Conventions
- MVVM strict : pas de code-behind metier, RelayCommand, ObservableCollection
- Nommage : PascalCase classes/props, _camelCase champs prives
- Un ViewModel par vue/panneau
- IExternalEventHandler pour toute interaction Revit depuis l'UI
- Langue interface : francais

## Git
- Repo : https://github.com/tanguynoumea-collab/TEXTURE-MANAGER
- Developpement local uniquement
- .gitignore : bin/, obj/, .vs/, *.user, packages/
- Push final apres validation de l'installer

## Statut GSD
[Rempli apres /gsd export en fin de session]

<!-- GSD:project-start source:PROJECT.md -->
## Project

**Olympe MaterialManager**

Add-in Revit WPF permettant aux architectes et concepteurs de gerer visuellement les materiaux de tous les types visibles dans une vue 3D active. Interface a trois panneaux : familles/types (gauche), couches/parametres (centre), materiaux preset (droite). Multi-version Revit 2024, 2025 et 2026.

**Core Value:** L'architecte peut appliquer rapidement un materiau preset aux couches ou parametres materiaux de n'importe quel type Revit visible en 3D, en quelques clics depuis un editeur visuel unifie.

### Constraints

- **Revit API** : Assemblies en reference externe uniquement (CopyLocal = false), pas de thread separe sans Dispatcher
- **MVVM strict** : Pas de code-behind metier, RelayCommand, ObservableCollection, un ViewModel par vue
- **.NET 4.8** : Impose par Revit, pas de .NET Core/5+
- **Multi-version** : Le meme code partage doit compiler contre 3 versions de l'API Revit
- **Fichier .addin** : Requis dans %APPDATA%\Autodesk\Revit\Addins\{version}\ pour l'enregistrement
- **Langue** : Interface utilisateur en francais
- **Nommage** : PascalCase classes/proprietes, _camelCase champs prives
<!-- GSD:project-end -->

<!-- GSD:stack-start source:research/STACK.md -->
## Technology Stack

## Critical Finding: .NET Framework Split
| Revit Version | .NET Runtime        | Target Framework Moniker |
|---------------|---------------------|--------------------------|
| Revit 2024    | .NET Framework 4.8  | `net48`                  |
| Revit 2025    | .NET 8              | `net8.0-windows`         |
| Revit 2026    | .NET 8              | `net8.0-windows`         |
## Recommended Stack
### Solution Architecture
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
### MVVM Framework
| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| CommunityToolkit.Mvvm | 8.4.2 | MVVM infrastructure | Source generators for ObservableProperty, RelayCommand, ObservableObject. Targets netstandard2.0 (consumable by net48) and net8.0. Industry standard for WPF MVVM in 2025/2026. | HIGH |
| PolySharp | 1.15.0 | C# polyfills for net48 | Required to use C# 12 language features and CommunityToolkit.Mvvm source generators on .NET Framework 4.8. Generates source-only polyfills for missing runtime types. | HIGH |
### JSON Serialization (Presets Persistence)
| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| System.Text.Json | 8.0.5 (net8) / 6.0.x (net48) | JSON serialization for preset files | Cross-platform, no external dependency on net8. On net48, available via NuGet package `System.Text.Json`. Faster and lower memory than Newtonsoft.Json. Single API surface across both targets. | MEDIUM |
### Installer
| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| WiX Toolset | v5.0.2 (WixToolset.Sdk) | MSI/EXE installer | **NOT v4 as stated in PROJECT.md.** WiX v4 is end-of-life (out of community support since Feb 2025). WiX v5.0.2 is the last stable v5. WiX v6.0.2 and v7.0.0 are newer but v5 has the widest documentation and community examples. v5 syntax is highly compatible with v4. | HIGH |
- DLL to a chosen install directory (e.g., `%ProgramFiles%\Olympe\MaterialManager\`)
- `.addin` manifest file to `%APPDATA%\Autodesk\Revit\Addins\{version}\` for each selected Revit version
- Version selection UI in the installer (checkboxes for 2024/2025/2026)
### Supporting Libraries
| Library | Version | Purpose | When to Use | Confidence |
|---------|---------|---------|-------------|------------|
| PolySharp | 1.15.0 | C# language polyfills | Always -- enables C# 12 on net48 | HIGH |
| Nice3point.Revit.Extensions | 2024.x / 2026.x | Revit API extension methods | Convenience extensions for Element, Document, etc. Reduces boilerplate. | MEDIUM |
| System.Text.Json | 8.0.5 / NuGet for net48 | JSON preset persistence | Preset file read/write | MEDIUM |
| Microsoft.Xaml.Behaviors.Wpf | 1.1.122 | WPF behaviors (EventTrigger, etc.) | For MVVM-friendly event binding in XAML without code-behind | MEDIUM |
### Development Tools
| Tool | Version | Purpose | Confidence |
|------|---------|---------|------------|
| Visual Studio 2022 | 17.x (latest) | IDE | Required for .NET 8 SDK, WPF designer, and WiX extension. | HIGH |
| .NET 8 SDK | 8.0.x (latest) | Build toolchain | Required for Revit 2025/2026 targeting and source generator support. | HIGH |
| .NET Framework 4.8 Dev Pack | 4.8 | Build toolchain | Required for Revit 2024 targeting. | HIGH |
| HeatWave (FireGiant) | latest | WiX VS extension | Visual Studio integration for .wixproj editing. Free community edition. | MEDIUM |
## Revit API Version Differences for Material Management
### Stable API Surface (2024/2025/2026 -- no changes)
- `Material` class -- properties: Name, Color, SurfacePatternId, SurfaceForegroundPatternColor, CutPatternId, Transparency, Shininess, Smoothness, AppearanceAssetId, StructuralAssetId, ThermalAssetId
- `AppearanceAssetElement` -- wrapper for appearance assets
- `AppearanceAssetEditScope` -- edit scope pattern for modifying appearance properties (Start/Commit/Cancel)
- `Asset` / `AssetProperty` hierarchy -- for reading/writing individual appearance properties (color, texture, tint, etc.)
- `CompoundStructure` -- GetLayers(), SetLayers(), layer materials via CompoundStructureLayer.MaterialId
- `CompoundStructureLayer` -- MaterialId, Function, Width
- `ElementType.LookupParameter()` / `Element.get_Parameter()` -- for finding Material parameters on loaded families
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
## Thread Model and ExternalEvent Pattern
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
## Installation Commands
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
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

Conventions not yet established. Will populate as patterns emerge during development.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

Architecture not yet mapped. Follow existing patterns found in the codebase.
<!-- GSD:architecture-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd:quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd:debug` for investigation and bug fixing
- `/gsd:execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd:profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
