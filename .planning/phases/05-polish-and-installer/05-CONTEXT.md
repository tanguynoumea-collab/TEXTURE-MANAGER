# Phase 5: Polish and Installer - Context

**Gathered:** 2026-04-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Visual polish of all WPF controls for consistent Olympe theme, and WiX v5 installer that deploys the add-in to selected Revit versions. This is the final phase before distribution.

Requirements: UI-03, DEPLOY-01, DEPLOY-02, DEPLOY-03, DEPLOY-04, DEPLOY-05

</domain>

<decisions>
## Implementation Decisions

### Visual Polish
- **D-01:** Audit OlympeTheme.xaml and add missing styles: Popup, ContextMenu, MenuItem, ToolTip, Dialog (AddMaterialDialog), ScrollViewer, GroupBox, Expander. Ensure consistent dark theme across all controls.
- **D-02:** Review all Views for unstyled controls. Apply implicit styles via ResourceDictionary keys.
- **D-03:** Polish hover/focus/disabled states for all interactive controls. Disabled = 50% opacity. Focus = accent border.

### WiX v5 Installer
- **D-04:** Separate WiX project: OlympeMaterialManager.Installer/ at solution root level. Uses WixToolset.Sdk 5.0.2 NuGet.
- **D-05:** Installer type: MSI wrapped in EXE Bundle with Burn bootstrapper. The Bundle provides the version selection UI.
- **D-06:** Installation targets: assemblies go to %ProgramFiles%\Olympe\MaterialManager\. One subfolder per TFM: net48/ and net8.0-windows/.
- **D-07:** .addin file deployment: installer copies the correct .addin file to %APPDATA%\Autodesk\Revit\Addins\{year}\ for each selected version (2024, 2025, 2026). The .addin file's Assembly path points to the ProgramFiles location.
- **D-08:** Version detection: WiX RegistrySearch for Revit installation keys (HKLM\SOFTWARE\Autodesk\Revit\{version}). Only show checkboxes for detected versions.
- **D-09:** UI: Simple checkbox page — "Selectionner les versions de Revit :" with checkboxes for 2024/2025/2026 (enabled only if detected). Standard Install/Cancel buttons.
- **D-10:** Uninstall: removes ProgramFiles folder and .addin files from all installed versions.

### Build Integration
- **D-11:** The .wixproj references the main OlympeMaterialManager.csproj build output. Post-build copies assemblies to a staging folder that WiX harvests.
- **D-12:** Final output: OlympeMaterialManager.Setup.exe in the Installer/bin/Release/ folder.

### Claude's Discretion
- Exact WiX XML structure for Bundle/Chain/MsiPackage
- RegistrySearch key paths for Revit version detection
- Whether to use WiX UI extension or custom Burn UI
- Installer icon and branding

</decisions>

<canonical_refs>
## Canonical References

### Existing Code
- `OlympeMaterialManager/src/OlympeMaterialManager/Themes/OlympeTheme.xaml` — Existing theme to extend
- `OlympeMaterialManager/addin/OlympeMaterialManager.2024.addin` — .addin file template
- `OlympeMaterialManager/OlympeMaterialManager.sln` — Solution to add installer project

### Research
- `.planning/research/STACK.md` — WiX v5 recommendation, .addin deployment paths
- `.planning/research/PITFALLS.md` — WiX installer common mistakes

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- OlympeTheme.xaml — Already has 10 control styles, extend with missing ones
- .addin files — Already generated with correct GUIDs and paths
- Multi-target csproj — Produces both net48 and net8.0-windows outputs

### Integration Points
- OlympeMaterialManager.sln — Add .wixproj
- Post-build — Stage assemblies for WiX harvesting

</code_context>

<specifics>
## Specific Ideas

- The installer must feel professional (commercial product quality)
- Version detection should be reliable — don't show options for uninstalled Revit versions
- The polish pass should make every control feel part of the same design system

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 05-polish-and-installer*
*Context gathered: 2026-04-11*
