# Phase 5: Polish and Installer - Research

**Researched:** 2026-04-11
**Domain:** WiX v5 installer (Bundle/Burn + MSI), WPF dark theme polish
**Confidence:** HIGH

## Summary

Phase 5 has two distinct work streams: (1) completing the Olympe dark theme by adding missing implicit styles for all WPF controls used across the application, and (2) building a WiX v5 installer that detects installed Revit versions via registry, lets the user select target versions, and deploys assemblies to ProgramFiles plus .addin files to per-user AppData.

The WPF polish work is straightforward -- the existing OlympeTheme.xaml already has 12 control styles (Button, TextBlock, TextBox, ComboBox, ComboBoxItem, ListBox, ListBoxItem, TreeView, TreeViewItem, ScrollBar, GridSplitter, SetMatButton). The audit identified 7 missing control types that need implicit styles: ContextMenu, MenuItem, ToolTip, CheckBox, Separator, Expander/GroupBox (if used), and the AddMaterialDialog window chrome.

The installer architecture uses a WiX v5 Bundle (Burn bootstrapper) wrapping a single MSI. The Bundle provides the version-selection UI via WixStandardBootstrapperApplication with a custom theme. The MSI uses WiX v5 Features -- one per Revit version -- with install conditions tied to Bundle variables set by RegistrySearch. The `Files` element (new in v5, replaces Heat) harvests build output.

**Primary recommendation:** Use a single MSI with three Features (Revit2024, Revit2025, Revit2026) wrapped in a Burn Bundle. The Bundle handles version detection (RegistrySearch) and UI (WixStdBA custom theme with checkboxes). Each Feature contains components that deploy the correct TFM assemblies to ProgramFiles and the .addin file to the user's AppData. This is simpler than three separate MSIs and avoids the complexity of custom Burn bootstrapper applications.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Audit OlympeTheme.xaml and add missing styles: Popup, ContextMenu, MenuItem, ToolTip, Dialog (AddMaterialDialog), ScrollViewer, GroupBox, Expander. Ensure consistent dark theme across all controls.
- **D-02:** Review all Views for unstyled controls. Apply implicit styles via ResourceDictionary keys.
- **D-03:** Polish hover/focus/disabled states for all interactive controls. Disabled = 50% opacity. Focus = accent border.
- **D-04:** Separate WiX project: OlympeMaterialManager.Installer/ at solution root level. Uses WixToolset.Sdk 5.0.2 NuGet.
- **D-05:** Installer type: MSI wrapped in EXE Bundle with Burn bootstrapper. The Bundle provides the version selection UI.
- **D-06:** Installation targets: assemblies go to %ProgramFiles%\Olympe\MaterialManager\. One subfolder per TFM: net48/ and net8.0-windows/.
- **D-07:** .addin file deployment: installer copies the correct .addin file to %APPDATA%\Autodesk\Revit\Addins\{year}\ for each selected version (2024, 2025, 2026). The .addin file's Assembly path points to the ProgramFiles location.
- **D-08:** Version detection: WiX RegistrySearch for Revit installation keys (HKLM keys). Only show checkboxes for detected versions.
- **D-09:** UI: Simple checkbox page -- "Selectionner les versions de Revit :" with checkboxes for 2024/2025/2026 (enabled only if detected). Standard Install/Cancel buttons.
- **D-10:** Uninstall: removes ProgramFiles folder and .addin files from all installed versions.
- **D-11:** The .wixproj references the main OlympeMaterialManager.csproj build output. Post-build copies assemblies to a staging folder that WiX harvests.
- **D-12:** Final output: OlympeMaterialManager.Setup.exe in the Installer/bin/Release/ folder.

### Claude's Discretion
- Exact WiX XML structure for Bundle/Chain/MsiPackage
- RegistrySearch key paths for Revit version detection
- Whether to use WiX UI extension or custom Burn UI
- Installer icon and branding

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| UI-03 | Tous les controles WPF sont styles de maniere coherente (boutons, listes, TreeView, scrollbars) | Theme audit identifies 7 missing control types; pattern for ControlTemplate + triggers documented |
| DEPLOY-01 | Un installer .exe est genere via WiX v5 | WiX v5 Bundle .wixproj format, WixToolset.Sdk 5.0.2, Files element for harvesting |
| DEPLOY-02 | L'installer detecte les versions de Revit installees sur la machine | Verified registry paths: HKLM\SOFTWARE\Autodesk\Revit\Autodesk Revit {year} -- confirmed on this machine |
| DEPLOY-03 | L'utilisateur peut choisir pour quelle(s) version(s) de Revit installer l'add-in | WixStdBA custom theme with checkboxes tied to Bundle Variables and RegistrySearch results |
| DEPLOY-04 | L'installer copie les assemblies et le fichier .addin dans le bon dossier selon la version choisie | MSI Features with conditional components; AppDataFolder directory for .addin; ProgramFiles64Folder for assemblies |
| DEPLOY-05 | L'installer fonctionne correctement sur Windows 10 et Windows 11 | perMachine MSI scope, standard WiX patterns, no OS-specific APIs |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- MVVM strict: pas de code-behind metier, RelayCommand, ObservableCollection
- Nommage: PascalCase classes/props, _camelCase champs prives
- Un ViewModel par vue/panneau
- IExternalEventHandler pour toute interaction Revit depuis l'UI
- Langue interface: francais
- WiX Toolset v5.0.2 (NOT v4 -- v4 is EOL)
- Single SDK-style multi-target csproj (net48;net8.0-windows)

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| WixToolset.Sdk | 5.0.2 | MSBuild SDK for WiX installer projects | Last stable v5 release. v6/v7 exist but v5 has widest community docs. v4 is EOL. |
| WixToolset.Bal.wixext | 5.0.2 | Burn bootstrapper application extension | Required for WixStandardBootstrapperApplication in Bundle |
| WixToolset.Util.wixext | 5.0.2 | Utility extension (RegistrySearch, etc.) | Required for RegistrySearch in Bundle to detect Revit versions |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| WixToolset.UI.wixext | 5.0.2 | Standard MSI UI dialogs | Only if custom MSI-level dialogs needed (likely not -- Bundle UI handles selection) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| WixStdBA (standard BA) | Custom Burn BA (.NET WPF) | Full control over UI but requires a separate EXE project, out-of-process hosting in v5, much more complex. Not worth it for a simple checkbox page. |
| Single MSI + Features | Multiple MSIs in Bundle Chain | Multiple MSIs means duplicating components and managing three separate .wxs files. Single MSI with Features is simpler and standard. |
| Files element (v5) | HeatWave harvesting | HeatWave is FireGiant commercial extension. Files element is built-in to v5, free, and sufficient. |

## Architecture Patterns

### Recommended Project Structure
```
OlympeMaterialManager/
  OlympeMaterialManager.sln
  src/
    OlympeMaterialManager/
      OlympeMaterialManager.csproj
      Themes/
        OlympeTheme.xaml           <-- extend with missing styles
      Views/
        MainWindow.xaml
        LeftPanelView.xaml
        CenterPanelView.xaml
        RightPanelView.xaml
        AddMaterialDialog.xaml
  installer/
    OlympeMaterialManager.Installer/
      OlympeMaterialManager.Installer.wixproj   <-- Bundle project
      Bundle.wxs                                  <-- Burn Bundle definition
      Package.wxs                                 <-- MSI Package definition  
      Directories.wxs                             <-- Directory structure
      addin/                                      <-- .addin files for installer
        OlympeMaterialManager.2024.addin
        OlympeMaterialManager.2025.addin
        OlympeMaterialManager.2026.addin
      theme/
        OlympeTheme.xml                           <-- Custom WixStdBA theme
        OlympeTheme.wxl                           <-- Localized strings (French)
```

### Pattern 1: WiX v5 Bundle .wixproj (OutputType=Bundle)

**What:** The installer project uses WixToolset.Sdk as an MSBuild SDK with OutputType=Bundle to produce the EXE bootstrapper.
**When to use:** Always -- this is the mandatory format for v5 Burn bundles.

```xml
<!-- OlympeMaterialManager.Installer.wixproj -->
<Project Sdk="WixToolset.Sdk/5.0.2">
  <PropertyGroup>
    <OutputType>Bundle</OutputType>
    <OutputName>OlympeMaterialManager.Setup</OutputName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WixToolset.Bal.wixext" Version="5.0.2" />
    <PackageReference Include="WixToolset.Util.wixext" Version="5.0.2" />
  </ItemGroup>
</Project>
```

**CRITICAL v5 difference:** In WiX v5, Burn launches bootstrapper applications as separate processes (out-of-process), not loaded as DLLs. This affects custom BAs but NOT WixStdBA -- the standard BA handles this automatically.

### Pattern 2: Bundle.wxs with RegistrySearch + Variables + WixStdBA

**What:** The Bundle definition declares variables for each Revit version, uses RegistrySearch to detect installations, and references WixStdBA with a custom theme for the checkbox UI.

```xml
<!-- Bundle.wxs -->
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:bal="http://wixtoolset.org/schemas/v4/wxs/bal"
     xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">

  <Bundle Name="Olympe MaterialManager"
          Version="1.0.0.0"
          Manufacturer="Olympe"
          UpgradeCode="PUT-GUID-HERE">

    <!-- Variables for Revit version detection -->
    <Variable Name="Revit2024Detected" Type="string" Value="" />
    <Variable Name="Revit2025Detected" Type="string" Value="" />
    <Variable Name="Revit2026Detected" Type="string" Value="" />

    <!-- User selection variables (overridable by UI) -->
    <Variable Name="InstallRevit2024" Type="numeric" Value="0"
              bal:Overridable="yes" />
    <Variable Name="InstallRevit2025" Type="numeric" Value="0"
              bal:Overridable="yes" />
    <Variable Name="InstallRevit2026" Type="numeric" Value="0"
              bal:Overridable="yes" />

    <!-- Registry searches for Revit detection -->
    <util:RegistrySearch Variable="Revit2024Detected"
                         Root="HKLM"
                         Key="SOFTWARE\Autodesk\Revit\Autodesk Revit 2024\Components"
                         Value="ProductName"
                         Result="value" />
    <util:RegistrySearch Variable="Revit2025Detected"
                         Root="HKLM"
                         Key="SOFTWARE\Autodesk\Revit\Autodesk Revit 2025\Components"
                         Value="ProductName"
                         Result="value" />
    <util:RegistrySearch Variable="Revit2026Detected"
                         Root="HKLM"
                         Key="SOFTWARE\Autodesk\Revit\Autodesk Revit 2026\Components"
                         Value="ProductName"
                         Result="value" />

    <!-- Bootstrapper Application: WixStdBA with custom theme -->
    <BootstrapperApplication>
      <bal:WixStandardBootstrapperApplication
          LicenseUrl=""
          Theme="none"
          ThemeFile="theme\OlympeTheme.xml"
          LocalizationFile="theme\OlympeTheme.wxl" />
    </BootstrapperApplication>

    <!-- Chain: single MSI with properties from Bundle variables -->
    <Chain>
      <MsiPackage SourceFile="$(var.PackagePath)">
        <MsiProperty Name="INSTALL_REVIT2024" Value="[InstallRevit2024]" />
        <MsiProperty Name="INSTALL_REVIT2025" Value="[InstallRevit2025]" />
        <MsiProperty Name="INSTALL_REVIT2026" Value="[InstallRevit2026]" />
      </MsiPackage>
    </Chain>

  </Bundle>
</Wix>
```

### Pattern 3: MSI Package.wxs with Features and Conditional Components

**What:** The MSI package defines Features for each Revit version. Each Feature contains components for deploying the correct assemblies and .addin file. Features use install conditions tied to the INSTALL_REVIT{year} properties passed from the Bundle.

```xml
<!-- Package.wxs -->
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">

  <Package Name="Olympe MaterialManager"
           Version="1.0.0.0"
           Manufacturer="Olympe"
           UpgradeCode="PUT-DIFFERENT-GUID-HERE"
           InstallerVersion="500"
           Scope="perMachine"
           Compressed="yes">

    <MajorUpgrade DowngradeErrorMessage=
      "Une version plus recente est deja installee." />
    <MediaTemplate EmbedCab="yes" />

    <!-- Public properties for Bundle communication -->
    <Property Id="INSTALL_REVIT2024" Value="0" Secure="yes" />
    <Property Id="INSTALL_REVIT2025" Value="0" Secure="yes" />
    <Property Id="INSTALL_REVIT2026" Value="0" Secure="yes" />

    <!-- Features: one per Revit version -->
    <Feature Id="Revit2024" Title="Revit 2024" Level="1000">
      <Condition Level="1">INSTALL_REVIT2024 = "1"</Condition>
      <ComponentGroupRef Id="Revit2024Assemblies" />
      <ComponentRef Id="Revit2024Addin" />
    </Feature>

    <Feature Id="Revit2025" Title="Revit 2025" Level="1000">
      <Condition Level="1">INSTALL_REVIT2025 = "1"</Condition>
      <ComponentGroupRef Id="Revit2025Assemblies" />
      <ComponentRef Id="Revit2025Addin" />
    </Feature>

    <Feature Id="Revit2026" Title="Revit 2026" Level="1000">
      <Condition Level="1">INSTALL_REVIT2026 = "1"</Condition>
      <ComponentGroupRef Id="Revit2026Assemblies" />
      <ComponentRef Id="Revit2026Addin" />
    </Feature>

  </Package>
</Wix>
```

**Key insight:** Feature Level="1000" means "not selected by default." The Condition element sets Level="1" (selected) when the property equals "1". The Bundle UI controls which properties are set.

### Pattern 4: Directory Structure and .addin Deployment

```xml
<!-- Directories.wxs -->
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>

    <!-- Program Files installation -->
    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="OlympeDir" Name="Olympe">
        <Directory Id="INSTALLFOLDER" Name="MaterialManager">
          <Directory Id="Net48Dir" Name="net48" />
          <Directory Id="Net8Dir" Name="net8.0-windows" />
        </Directory>
      </Directory>
    </StandardDirectory>

    <!-- AppData: per-user .addin file locations -->
    <StandardDirectory Id="AppDataFolder">
      <Directory Id="AutodeskDir" Name="Autodesk">
        <Directory Id="RevitDir" Name="Revit">
          <Directory Id="AddinsDir" Name="Addins">
            <Directory Id="Addins2024" Name="2024" />
            <Directory Id="Addins2025" Name="2025" />
            <Directory Id="Addins2026" Name="2026" />
          </Directory>
        </Directory>
      </Directory>
    </StandardDirectory>

  </Fragment>

  <!-- Revit 2024 components (net48) -->
  <Fragment>
    <ComponentGroup Id="Revit2024Assemblies" Directory="Net48Dir">
      <Files Include="$(var.StagingDir)\net48\**" />
    </ComponentGroup>

    <Component Id="Revit2024Addin" Directory="Addins2024" Guid="PUT-GUID-HERE">
      <File Source="addin\OlympeMaterialManager.2024.addin"
            Name="OlympeMaterialManager.addin"
            KeyPath="no" />
      <RegistryValue Root="HKCU"
                     Key="Software\Olympe\MaterialManager"
                     Name="Revit2024Installed"
                     Type="integer" Value="1"
                     KeyPath="yes" />
      <RemoveFolder Id="RemoveAddins2024" On="uninstall" />
    </Component>
  </Fragment>

  <!-- Revit 2025 components (net8.0-windows) -->
  <Fragment>
    <ComponentGroup Id="Revit2025Assemblies" Directory="Net8Dir">
      <Files Include="$(var.StagingDir)\net8.0-windows\**" />
    </ComponentGroup>

    <Component Id="Revit2025Addin" Directory="Addins2025" Guid="PUT-GUID-HERE">
      <File Source="addin\OlympeMaterialManager.2025.addin"
            Name="OlympeMaterialManager.addin"
            KeyPath="no" />
      <RegistryValue Root="HKCU"
                     Key="Software\Olympe\MaterialManager"
                     Name="Revit2025Installed"
                     Type="integer" Value="1"
                     KeyPath="yes" />
      <RemoveFolder Id="RemoveAddins2025" On="uninstall" />
    </Component>
  </Fragment>

  <!-- Revit 2026 components (net8.0-windows, same TFM as 2025) -->
  <Fragment>
    <ComponentGroup Id="Revit2026Assemblies" Directory="Net8Dir">
      <Files Include="$(var.StagingDir)\net8.0-windows\**" />
    </ComponentGroup>

    <Component Id="Revit2026Addin" Directory="Addins2026" Guid="PUT-GUID-HERE">
      <File Source="addin\OlympeMaterialManager.2026.addin"
            Name="OlympeMaterialManager.addin"
            KeyPath="no" />
      <RegistryValue Root="HKCU"
                     Key="Software\Olympe\MaterialManager"
                     Name="Revit2026Installed"
                     Type="integer" Value="1"
                     KeyPath="yes" />
      <RemoveFolder Id="RemoveAddins2026" On="uninstall" />
    </Component>
  </Fragment>

</Wix>
```

**CRITICAL: .addin files for deployed state.** The existing .addin files in the project use relative paths like `..\src\OlympeMaterialManager\bin\Release\net48\OlympeMaterialManager.dll`. The installer needs .addin files with absolute paths pointing to the ProgramFiles installation:

```xml
<!-- addin/OlympeMaterialManager.2024.addin (for installer) -->
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Olympe MaterialManager</Name>
    <Assembly>C:\Program Files\Olympe\MaterialManager\net48\OlympeMaterialManager.dll</Assembly>
    <AddInId>2557E4F8-7B71-44E5-8FED-3313C3C2269E</AddInId>
    <FullClassName>Olympe.MaterialManager.App</FullClassName>
    <VendorId>OLYMPE</VendorId>
    <VendorDescription>Olympe</VendorDescription>
  </AddIn>
</RevitAddIns>
```

**Note on Revit 2025 vs 2026:** Both use the same net8.0-windows TFM and the same DLL. They share the Net8Dir installation folder. Only the .addin file differs (deployed to different Addins/{year}/ folders). The DLL components for 2025 and 2026 can reference the same ComponentGroup to avoid file duplication issues in WiX (one file = one component rule).

### Pattern 5: WPF Theme Extension Pattern

**What:** Add missing implicit styles to OlympeTheme.xaml following the established pattern.

Existing controls already styled (12 total): Window (keyed), Button, TextBlock, TextBox, ComboBox, ComboBoxItem, ListBox, ListBoxItem, TreeView, TreeViewItem, ScrollBar (both orientations), GridSplitter, SetMatButton (keyed).

**Controls needing styles (identified from view audit):**

| Control | Used In | Current State |
|---------|---------|---------------|
| ContextMenu | LeftPanelView, RightPanelView | Unstyled -- uses Windows default light theme |
| MenuItem | Inside ContextMenus | Unstyled -- white background on dark app |
| ToolTip | LeftPanelView (PickButton tooltip) | Unstyled -- light theme default |
| CheckBox | RightPanelView (tint toggle) | Unstyled -- default checkbox on dark background |
| Separator | Potential use in ContextMenu | Not yet used but should be styled for completeness |
| ScrollViewer | Inside ListBox/TreeView templates | Already styled indirectly via ScrollBar, but standalone instances need review |
| Dialog chrome | AddMaterialDialog | Uses OlympeWindowStyle but WindowStyle=ToolWindow has OS chrome |

**Style pattern (consistent with existing theme):**

```xml
<!-- ContextMenu implicit style -->
<Style TargetType="ContextMenu">
    <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="2" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="ContextMenu">
                <Border Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="4"
                        Padding="{TemplateBinding Padding}">
                    <StackPanel IsItemsHost="True"
                                KeyboardNavigation.DirectionalNavigation="Cycle" />
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<!-- MenuItem implicit style -->
<Style TargetType="MenuItem">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}" />
    <Setter Property="Padding" Value="8,4" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="MenuItem">
                <Border x:Name="border"
                        Background="{TemplateBinding Background}"
                        Padding="{TemplateBinding Padding}">
                    <ContentPresenter ContentSource="Header" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsHighlighted" Value="True">
                        <Setter TargetName="border" Property="Background"
                                Value="{StaticResource SurfaceBrush}" />
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter Property="Opacity" Value="0.5" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<!-- CheckBox implicit style -->
<Style TargetType="CheckBox">
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}" />
    <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="CheckBox">
                <StackPanel Orientation="Horizontal">
                    <Border x:Name="checkBorder"
                            Width="18" Height="18"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="1"
                            CornerRadius="3"
                            VerticalAlignment="Center">
                        <Path x:Name="checkMark"
                              Data="M 2 6 L 6 10 L 14 2"
                              Stroke="{StaticResource AccentBrush}"
                              StrokeThickness="2"
                              Visibility="Collapsed"
                              Margin="1" />
                    </Border>
                    <ContentPresenter Margin="6,0,0,0"
                                      VerticalAlignment="Center" />
                </StackPanel>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsChecked" Value="True">
                        <Setter TargetName="checkMark" Property="Visibility"
                                Value="Visible" />
                        <Setter TargetName="checkBorder" Property="BorderBrush"
                                Value="{StaticResource AccentBrush}" />
                    </Trigger>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="checkBorder" Property="BorderBrush"
                                Value="{StaticResource AccentBrush}" />
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter Property="Opacity" Value="0.5" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<!-- ToolTip implicit style -->
<Style TargetType="ToolTip">
    <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="8,4" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="ToolTip">
                <Border Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="4"
                        Padding="{TemplateBinding Padding}">
                    <ContentPresenter />
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

### Anti-Patterns to Avoid

- **Inline styles overriding theme styles:** Several views (CenterPanelView, AddMaterialDialog) define local ListBoxItem styles that do NOT use `BasedOn="{StaticResource {x:Type ListBoxItem}}"`. This bypasses the theme and loses the ControlTemplate (CornerRadius, left-border accent). Fix: add BasedOn to all local styles.
- **Custom Burn BA for simple UI:** Building a custom WPF bootstrapper application for just a checkbox page is massive overkill. WixStdBA with a custom theme.xml handles this with zero C# code.
- **Three separate MSIs:** Duplicating components across three .wxs files for three Revit versions. Use one MSI with three Features instead.
- **Hardcoded install paths in .addin files:** The .addin files for the installer must use the actual ProgramFiles path, not relative dev paths. Use `[INSTALLFOLDER]` resolution or have separate .addin files for installer vs development.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Installer UI framework | Custom WPF Burn BA | WixStdBA custom theme.xml | A custom BA requires a separate EXE project, out-of-process hosting plumbing, IBootstrapperApplication implementation. WixStdBA does checkboxes with XML only. |
| File harvesting | Manual Component/File elements for every DLL | WiX v5 `Files` element with wildcards | The build output may include 10+ DLLs (dependencies). Manual listing is fragile. `Files` auto-generates Components. |
| Registry detection | Custom action C# code | WiX `util:RegistrySearch` | RegistrySearch is declarative, runs before UI, and automatically populates Bundle variables. |
| Version upgrade logic | Custom upgrade detection | `MajorUpgrade` element | WiX handles upgrade/downgrade detection automatically with proper GUIDs. |

## Verified Registry Paths for Revit Detection

**Confidence: HIGH -- verified on this development machine (2026-04-11)**

The following registry paths are confirmed to exist on this Windows 11 machine with Revit 2024 and 2025 installed:

| Revit Version | Registry Key | Value to Check | Result Type |
|---------------|-------------|----------------|-------------|
| 2024 | `HKLM\SOFTWARE\Autodesk\Revit\Autodesk Revit 2024\Components` | `ProductName` | REG_SZ "Revit 2024" |
| 2025 | `HKLM\SOFTWARE\Autodesk\Revit\Autodesk Revit 2025\Components` | `ProductName` | REG_SZ (present) |
| 2026 | `HKLM\SOFTWARE\Autodesk\Revit\Autodesk Revit 2026\Components` | `ProductName` | Expected same pattern |

**Alternative simpler approach:** Check for key existence only (Result="exists"):
- `HKLM\SOFTWARE\Autodesk\Revit\Autodesk Revit 2024` -- exists = installed
- `HKLM\SOFTWARE\Autodesk\Revit\Autodesk Revit 2025` -- exists = installed  
- `HKLM\SOFTWARE\Autodesk\Revit\Autodesk Revit 2026` -- exists = installed

**Recommended approach:** Use `Result="exists"` on the `Components` subkey. This is more reliable than checking value content, and the `Components` subkey is specific to actual Revit installations (not just language packs or worksharing monitors).

```xml
<util:RegistrySearch Variable="Revit2024Detected"
                     Root="HKLM"
                     Key="SOFTWARE\Autodesk\Revit\Autodesk Revit 2024\Components"
                     Result="exists" />
```

This sets `Revit2024Detected` to `"true"` or `"false"`.

## WiX v5 vs v4 Key Differences

| Area | v4 | v5 | Impact |
|------|----|----|--------|
| **Namespace** | `http://wixtoolset.org/schemas/v4/wxs` | Same (backward compatible) | No change needed |
| **File harvesting** | Heat tool or HeatWave extension | Built-in `Files` element with wildcards | Use `Files` instead of Heat |
| **Bootstrapper apps** | Loaded as DLL in Burn process | Out-of-process (separate EXE) | Only affects custom BAs, not WixStdBA |
| **Default MajorUpgrade** | Must be explicitly authored | Auto-generated if missing | Still author explicitly for control |
| **INSTALLFOLDER** | Must define Directory | Auto-created if referenced but undefined | Nice convenience, still define explicitly |
| **virtual/override** | Not available | Symbol identifiers can be virtual/override | Useful for extensible installer components |
| **Standard directories** | `<Directory Id="ProgramFiles64Folder" />` | `<StandardDirectory Id="ProgramFiles64Folder">` | Syntax change for standard Windows folders |
| **Package element** | `<Product>` with separate `<Package>` | Single `<Package>` element combines both | Major structural change from v3; v4-compatible in v5 |
| **Bal extension package** | WixToolset.Mba.Core + WixToolset.BalUtil | Single WixToolset.BootstrapperApplicationApi | Only for custom BAs |

**Most online WiX examples are v3.** Key v3-to-v5 translations:
- `<Product>` + `<Package>` => `<Package>` (single element)
- `<Directory Id="TARGETDIR">` => `<StandardDirectory Id="...">` or just `<Fragment>` with directory defs
- Heat.exe => `<Files>` element
- `xmlns="http://schemas.microsoft.com/wix/2006/wi"` => `xmlns="http://wixtoolset.org/schemas/v4/wxs"`

## .addin File Deployment Strategy

The existing .addin files use development relative paths. The installer needs .addin files with absolute installed paths.

**Two approaches:**

1. **Separate .addin files for installer** (recommended): Create dedicated .addin files in the installer project with the installed Assembly path (`C:\Program Files\Olympe\MaterialManager\{tfm}\OlympeMaterialManager.dll`). Keep the dev .addin files in `addin/` as-is.

2. **XML manipulation at install time** (complex): Use `util:XmlFile` to patch the Assembly path during installation. More flexible but harder to debug. The Revit IFC installer uses this pattern.

**Recommendation:** Approach 1. The paths are fixed (ProgramFiles is standard). Three small XML files in the installer project is simpler than custom actions.

**TFM mapping for .addin Assembly paths:**
- 2024 => `[ProgramFiles64Folder]Olympe\MaterialManager\net48\OlympeMaterialManager.dll`
- 2025 => `[ProgramFiles64Folder]Olympe\MaterialManager\net8.0-windows\OlympeMaterialManager.dll`
- 2026 => `[ProgramFiles64Folder]Olympe\MaterialManager\net8.0-windows\OlympeMaterialManager.dll`

**Per-user AppData component rules:**
- Components installing to user-profile directories (AppDataFolder) MUST use a registry key under HKCU as their KeyPath, not a file. This is a Windows Installer requirement.
- Add `<RemoveFolder>` for clean uninstall of the Addins/{year}/ directory.
- The MSI must be `Scope="perMachine"` for the ProgramFiles assemblies. The AppData components work because Windows Installer resolves `[AppDataFolder]` to the installing user's profile.

## Common Pitfalls

### Pitfall 1: perMachine MSI with per-user AppData deployment
**What goes wrong:** A perMachine MSI installs files for the user who runs the installer. Other users on the same machine do not get the .addin files in their AppData.
**Why it happens:** `[AppDataFolder]` resolves to the current user's roaming profile. In a perMachine install, this is the admin user who ran the installer, not all users.
**How to avoid:** For a single-user development tool, this is acceptable. The architect running the installer is the one using the add-in. If multi-user support is needed later, use `[CommonAppDataFolder]` (maps to `%ProgramData%\Autodesk\Revit\Addins\{year}\`) instead, which is the all-users add-in location.
**Warning signs:** Add-in works for the user who installed but not for other Windows users on the same machine.

### Pitfall 2: WiX duplicate file error when 2025 and 2026 share the same DLL
**What goes wrong:** Revit 2025 and 2026 both use net8.0-windows. If both Features try to install the same DLL to the same Net8Dir folder, WiX throws a duplicate file error (one file = one component = one feature).
**Why it happens:** WiX enforces that each file belongs to exactly one component. Two features cannot install the same file to the same location.
**How to avoid:** Share the assembly ComponentGroup between the Revit2025 and Revit2026 Features. The net8.0-windows assemblies are installed once to Net8Dir and both .addin files point to them. Only the .addin file components differ between 2025 and 2026.
**Warning signs:** ICE validation errors about duplicate files or components during WiX build.

### Pitfall 3: ContextMenu renders outside the Window's ResourceDictionary scope
**What goes wrong:** ContextMenu in WPF opens in a separate visual tree (Popup). Implicit styles from the parent Window's ResourceDictionary may not apply. The context menu appears with the default light Windows theme.
**Why it happens:** The Popup that hosts the ContextMenu is a separate visual tree root. It does NOT inherit resources from the parent element.
**How to avoid:** Ensure ContextMenu styles are defined in App.xaml or in a ResourceDictionary merged at the App level. Alternatively, in this project where OlympeTheme.xaml is merged in each Window's Resources, the ContextMenu style should work because ContextMenu does inherit resources from its logical parent (the FrameworkElement that declares it). Test after implementation.
**Warning signs:** ContextMenu appears with white background while the rest of the app is dark.

### Pitfall 4: WiX v3 examples used verbatim in v5
**What goes wrong:** Most WiX examples online are for v3. Key structural differences: `<Product>` is now `<Package>`, the namespace is different, `<Directory>` nesting works differently, Heat is replaced by `<Files>`.
**Why it happens:** WiX v3 had a decade of community examples. v4/v5 are relatively new.
**How to avoid:** Always check the WiX v5 schema reference at docs.firegiant.com. Use the patterns documented in this research. Key tells: if you see `xmlns="http://schemas.microsoft.com/wix/2006/wi"`, it is v3 -- do not copy.
**Warning signs:** XML validation errors about unknown elements or attributes during build.

### Pitfall 5: Build output staging not set up before WiX build
**What goes wrong:** The WiX `Files` element references a staging directory that does not exist or is empty when the installer project builds.
**Why it happens:** The main csproj and the installer wixproj build independently. Without proper project references or build order, the staging directory is not populated.
**How to avoid:** Add a `<ProjectReference>` in the .wixproj to the main .csproj. Use MSBuild properties to define the staging directory path. The ProjectReference ensures build order. Alternatively, use a post-build target in the main csproj that copies output to the staging directory.
**Warning signs:** WiX build succeeds but the MSI/EXE contains no files. Or WiX build fails with "no files matching pattern."

### Pitfall 6: Missing focus/hover states make the UI feel broken
**What goes wrong:** Controls without focus indicators (TextBox, CheckBox) appear unresponsive. Users cannot tell which control is active when tabbing.
**Why it happens:** Custom ControlTemplates that override the default template lose the built-in focus adorner. The developer must re-implement it.
**How to avoid:** Every interactive control's ControlTemplate MUST include: (1) IsMouseOver trigger with accent border/highlight, (2) IsFocused trigger with accent border, (3) IsEnabled=False trigger with Opacity=0.5. This is explicitly required by D-03.
**Warning signs:** Tab key appears to do nothing. No visual feedback when hovering over controls.

## Code Examples

### Build Output Staging (MSBuild target in main csproj)

```xml
<!-- Add to OlympeMaterialManager.csproj -->
<Target Name="StageForInstaller" AfterTargets="Build"
        Condition="'$(Configuration)' == 'Release'">
  <PropertyGroup>
    <StagingDir>$(SolutionDir)..\installer\OlympeMaterialManager.Installer\staging</StagingDir>
  </PropertyGroup>
  <ItemGroup>
    <StagingFiles Include="$(OutputPath)**\*.*"
                  Exclude="$(OutputPath)**\*.pdb;$(OutputPath)**\*.xml" />
  </ItemGroup>
  <Copy SourceFiles="@(StagingFiles)"
        DestinationFolder="$(StagingDir)\$(TargetFramework)\%(RecursiveDir)" />
</Target>
```

### WixStdBA Custom Theme File

```xml
<!-- theme/OlympeTheme.xml -->
<Theme xmlns="http://wixtoolset.org/schemas/v4/thmutil">
  <Window Width="500" Height="400" HexStyle="100a0000"
          FontId="0" Caption="#(loc.Caption)">

    <!-- Page: Install -->
    <Page Name="Install">
      <Text X="20" Y="20" Width="460" Height="30"
            FontId="1">#(loc.Title)</Text>

      <Text X="20" Y="60" Width="460" Height="24"
            FontId="0">#(loc.SelectVersions)</Text>

      <Checkbox Name="Revit2024Checkbox" X="40" Y="90"
                Width="400" Height="20"
                FontId="0"
                EnableCondition="Revit2024Detected">#(loc.Revit2024)</Checkbox>

      <Checkbox Name="Revit2025Checkbox" X="40" Y="115"
                Width="400" Height="20"
                FontId="0"
                EnableCondition="Revit2025Detected">#(loc.Revit2025)</Checkbox>

      <Checkbox Name="Revit2026Checkbox" X="40" Y="140"
                Width="400" Height="20"
                FontId="0"
                EnableCondition="Revit2026Detected">#(loc.Revit2026)</Checkbox>

      <Text X="40" Y="170" Width="400" Height="20"
            FontId="2" DisableCondition="Revit2024Detected OR Revit2025Detected OR Revit2026Detected">
        #(loc.NoRevitDetected)
      </Text>

      <Button Name="InstallButton" X="320" Y="340"
              Width="80" Height="30"
              FontId="0">#(loc.InstallButton)</Button>
      <Button Name="CancelButton" X="410" Y="340"
              Width="80" Height="30"
              FontId="0">#(loc.CancelButton)</Button>
    </Page>

    <!-- Page: Progress -->
    <Page Name="Progress">
      <Text X="20" Y="20" Width="460" Height="30"
            FontId="1">#(loc.ProgressTitle)</Text>
      <Progressbar Name="OverallProgress" X="20" Y="70"
                   Width="460" Height="20" />
      <Text Name="OverallProgressText" X="20" Y="100"
            Width="460" Height="20" FontId="0" />
      <Button Name="CancelButton" X="410" Y="340"
              Width="80" Height="30"
              FontId="0">#(loc.CancelButton)</Button>
    </Page>

    <!-- Page: Success -->
    <Page Name="Success">
      <Text X="20" Y="20" Width="460" Height="30"
            FontId="1">#(loc.SuccessTitle)</Text>
      <Text X="20" Y="60" Width="460" Height="40"
            FontId="0">#(loc.SuccessMessage)</Text>
      <Button Name="CloseButton" X="410" Y="340"
              Width="80" Height="30"
              FontId="0">#(loc.CloseButton)</Button>
    </Page>

  </Window>
</Theme>
```

### WixStdBA Localization File (French)

```xml
<!-- theme/OlympeTheme.wxl -->
<WixLocalization xmlns="http://wixtoolset.org/schemas/v4/wxl"
                 Culture="fr-FR">
  <String Id="Caption">Olympe MaterialManager - Installation</String>
  <String Id="Title">Installation d'Olympe MaterialManager</String>
  <String Id="SelectVersions">Selectionner les versions de Revit :</String>
  <String Id="Revit2024">Revit 2024</String>
  <String Id="Revit2025">Revit 2025</String>
  <String Id="Revit2026">Revit 2026</String>
  <String Id="NoRevitDetected">Aucune version de Revit detectee sur cette machine.</String>
  <String Id="InstallButton">Installer</String>
  <String Id="CancelButton">Annuler</String>
  <String Id="ProgressTitle">Installation en cours...</String>
  <String Id="SuccessTitle">Installation terminee</String>
  <String Id="SuccessMessage">Olympe MaterialManager a ete installe avec succes. Redemarrez Revit pour utiliser l'add-in.</String>
  <String Id="CloseButton">Fermer</String>
</WixLocalization>
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| WiX v3 Heat.exe for file harvesting | WiX v5 `Files` element (declarative wildcards) | WiX v5 (2024) | No external tool needed; patterns in .wxs directly |
| WiX v3 `<Product>` + `<Package>` | WiX v5 single `<Package>` element | WiX v4 (2023) | Structural XML change; most v3 examples invalid |
| In-process Burn BA (DLL loaded by Burn) | Out-of-process BA (separate EXE) | WiX v5 (2024) | Custom BAs need rework; WixStdBA unaffected |
| WiX v3 `xmlns:wix="http://schemas.microsoft.com/wix/2006/wi"` | `xmlns="http://wixtoolset.org/schemas/v4/wxs"` | WiX v4 (2023) | All XML files need namespace update |
| WiX v4.0.x | WiX v5.0.2 (v4 is EOL) | Feb 2025 | v4 no longer receives security patches |

## Open Questions

1. **WixStdBA checkbox-to-variable binding**
   - What we know: WixStdBA themes support Checkbox elements with Name attributes. Variables in Bundle should be automatically linked by naming convention.
   - What's unclear: Exact naming convention for checkbox-to-variable binding in v5. Some sources suggest `Name="InstallRevit2024"` maps to `Variable Name="InstallRevit2024"`.
   - Recommendation: Start with the naming convention approach. If it does not work, the Wix4BurnTutorial GitHub repo has working examples. Test early.

2. **Shared net8.0-windows components between Revit 2025 and 2026 Features**
   - What we know: WiX enforces one-file-per-component. Both versions need the same DLL.
   - What's unclear: Whether a single ComponentGroup referenced by two Features works without ICE validation errors.
   - Recommendation: Put the net8.0-windows assemblies in a shared "CoreNet8" Feature that is always installed when either 2025 or 2026 is selected. Use `Level="1"` with a condition `INSTALL_REVIT2025 = "1" OR INSTALL_REVIT2026 = "1"`.

3. **WixStdBA EnableCondition for checkboxes**
   - What we know: The `EnableCondition` attribute on Checkbox should disable the checkbox when the condition is false.
   - What's unclear: Whether `EnableCondition="Revit2024Detected"` works with a string variable set by RegistrySearch (Result="exists"), or if it needs a specific boolean comparison.
   - Recommendation: Test with `EnableCondition="Revit2024Detected"`. If it fails, try `EnableCondition="Revit2024Detected = &quot;true&quot;"`.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build toolchain | Yes | 10.0.201 | OK (builds net48 and net8.0-windows) |
| WixToolset.Sdk 5.0.2 | Installer project | NuGet package (not local tool) | 5.0.2 | Auto-restored via NuGet during build |
| Revit 2024 | Testing installer | Yes | Installed (registry confirmed) | -- |
| Revit 2025 | Testing installer | Yes | Installed (registry confirmed) | -- |
| Revit 2026 | Testing installer | No | Not installed | Test .addin deployment path only; verify on target machine later |
| Visual Studio 2022 | IDE + WiX extension | Assumed present | -- | MSBuild CLI (`dotnet build`) works without VS |

**Missing dependencies with no fallback:**
- None blocking

**Missing dependencies with fallback:**
- Revit 2026 not installed: can still build and test 2024/2025 paths. 2026 .addin deployment is identical pattern.

## Sources

### Primary (HIGH confidence)
- Verified registry paths on this machine: `HKLM\SOFTWARE\Autodesk\Revit\Autodesk Revit 2024\Components\ProductName` = "Revit 2024"
- WixToolset.Sdk 5.0.2 NuGet: https://www.nuget.org/packages/WixToolset.Sdk/5.0.2
- WiX v5 Files element: https://docs.firegiant.com/wix/schema/wxs/files/
- WiX v5 Tutorial: https://docs.firegiant.com/wix/tutorial/
- RegistrySearch (Util extension): https://docs.firegiant.com/wix/schema/util/registrysearch/
- WiX v5 What's New: https://docs.firegiant.com/wix/whatsnew/
- WixStandardBootstrapperApplication: https://docs.firegiant.com/wix/tools/burn/wixstdba/
- Burn bundles: https://docs.firegiant.com/wix/tools/burn/

### Secondary (MEDIUM confidence)
- WiX 4/5 Burn Tutorial (GitHub): https://github.com/rsmart8452/Wix4BurnTutorial
- WiX 5 and HeatWave blog: https://mustafacanyucel.com/blog/blog-23.html
- WiX conditional features: https://medium.com/@willfsays/fun-with-wix-toolset-conditional-features-ab1134b53dd5
- FireGiant pass properties to MsiPackage: https://support.firegiant.com/hc/en-us/articles/230912207
- WiX per-user AppData discussion: https://github.com/orgs/wixtoolset/discussions/6997
- Revit IFC WiX installer: https://github.com/Autodesk/revit-ifc/blob/master/Install/RevitIFCSetupWix/Product.wxs
- Microsoft WPF ContextMenu styles: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/contextmenu-styles-and-templates

### Tertiary (LOW confidence)
- WixStdBA checkbox-to-variable naming convention: inferred from v3 examples, needs v5 validation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - WixToolset.Sdk 5.0.2 is verified on NuGet, patterns confirmed in official docs
- Architecture (MSI + Bundle): HIGH - standard WiX pattern, verified with Revit IFC installer reference
- Registry paths: HIGH - verified on this machine with actual Revit installations
- WPF theme audit: HIGH - direct audit of all .xaml files in the project
- WixStdBA custom theme: MEDIUM - syntax based on v3/v4 examples adapted to v5; checkbox binding needs testing
- Pitfalls: HIGH - based on verified WiX behavior and WPF visual tree rules

**Research date:** 2026-04-11
**Valid until:** 2026-05-11 (WiX v5 is stable; no breaking changes expected)
