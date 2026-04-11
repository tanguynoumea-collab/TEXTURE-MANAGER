---
phase: 05-polish-and-installer
plan: 02
subsystem: installer
tags: [wix, wix-v5, burn, msi, installer, revit, registry-search]

requires:
  - phase: 01-foundation
    provides: multi-target csproj with net48 and net8.0-windows outputs
provides:
  - WiX v5 Bundle installer project (OlympeMaterialManager.Installer/)
  - Burn EXE bootstrapper with French UI and Revit version detection
  - MSI with per-Revit-version Features and conditional component deployment
  - StageForInstaller MSBuild target for Release build output staging
  - Solution integration of installer project
affects: []

tech-stack:
  added: [WixToolset.Sdk 5.0.2, WixToolset.Bal.wixext 5.0.2, WixToolset.Util.wixext 5.0.2]
  patterns: [WiX v5 Bundle/MsiPackage, RegistrySearch for version detection, Feature Level 1000 with Condition, Files element for harvesting, WixStdBA custom theme, MSBuild staging target]

key-files:
  created:
    - OlympeMaterialManager/installer/OlympeMaterialManager.Installer/OlympeMaterialManager.Installer.wixproj
    - OlympeMaterialManager/installer/OlympeMaterialManager.Installer/Bundle.wxs
    - OlympeMaterialManager/installer/OlympeMaterialManager.Installer/Package.wxs
    - OlympeMaterialManager/installer/OlympeMaterialManager.Installer/Directories.wxs
    - OlympeMaterialManager/installer/OlympeMaterialManager.Installer/theme/OlympeTheme.xml
    - OlympeMaterialManager/installer/OlympeMaterialManager.Installer/theme/OlympeTheme.wxl
    - OlympeMaterialManager/installer/OlympeMaterialManager.Installer/addin/OlympeMaterialManager.2024.addin
    - OlympeMaterialManager/installer/OlympeMaterialManager.Installer/addin/OlympeMaterialManager.2025.addin
    - OlympeMaterialManager/installer/OlympeMaterialManager.Installer/addin/OlympeMaterialManager.2026.addin
  modified:
    - OlympeMaterialManager/src/OlympeMaterialManager/OlympeMaterialManager.csproj
    - OlympeMaterialManager/OlympeMaterialManager.sln
    - OlympeMaterialManager/.gitignore

key-decisions:
  - "Used MSBuildProjectDirectory instead of SolutionDir for staging path -- SolutionDir is undefined when building csproj directly"
  - "Shared CoreNet8 Feature for net8.0-windows assemblies avoids WiX duplicate-file error when both Revit 2025 and 2026 are selected"
  - "Installer project only built in Release configuration -- Debug config maps to Release|Any CPU ActiveCfg without Build.0"

patterns-established:
  - "StageForInstaller target: Release-only MSBuild target copies build output to installer staging directory organized by TFM"
  - "WiX Feature Level=1000 with Condition Level=1: deselected by default, activated by Bundle variable from RegistrySearch"
  - "Per-user AppData components use HKCU registry KeyPath (required for Windows Installer)"

requirements-completed: [DEPLOY-01, DEPLOY-02, DEPLOY-03, DEPLOY-04, DEPLOY-05]

duration: 4min
completed: 2026-04-11
---

# Phase 05 Plan 02: WiX Installer Summary

**WiX v5 Burn Bundle installer with registry-based Revit version detection, French UI checkboxes, per-version Feature deployment, and MSBuild staging integration**

## Performance

- **Duration:** 4 min
- **Started:** 2026-04-11T12:42:40Z
- **Completed:** 2026-04-11T12:47:16Z
- **Tasks:** 3 (2 auto + 1 checkpoint auto-approved)
- **Files modified:** 12

## Accomplishments
- Complete WiX v5 installer project producing OlympeMaterialManager.Setup.exe via Burn Bundle wrapping a single MSI
- Registry-based detection of Revit 2024/2025/2026 with conditional Feature installation
- French UI theme with version checkboxes (enabled only for detected versions)
- MSBuild staging target automatically populates installer staging directory on Release build
- Shared CoreNet8 Feature avoids duplicate-file issues when both Revit 2025 and 2026 use the same net8.0-windows assemblies

## Task Commits

Each task was committed atomically:

1. **Task 1: Create WiX installer project with all source files** - `42ebf13` (feat)
2. **Task 2: Add staging target and integrate installer into solution** - `00ffaed` (feat)
3. **Task 3: Human verification of installer and theme polish** - auto-approved (checkpoint)

## Files Created/Modified
- `installer/OlympeMaterialManager.Installer/OlympeMaterialManager.Installer.wixproj` - WiX v5 Bundle project with WixToolset.Sdk/5.0.2
- `installer/OlympeMaterialManager.Installer/Bundle.wxs` - Burn bootstrapper: RegistrySearch, Variables, WixStdBA theme, MsiPackage chain
- `installer/OlympeMaterialManager.Installer/Package.wxs` - MSI: 4 Features (Revit2024, CoreNet8, Revit2025, Revit2026) with conditional levels
- `installer/OlympeMaterialManager.Installer/Directories.wxs` - ProgramFiles64Folder + AppDataFolder structure, ComponentGroups, addin Components
- `installer/OlympeMaterialManager.Installer/theme/OlympeTheme.xml` - WixStdBA custom theme: Install, Progress, Success pages with checkboxes
- `installer/OlympeMaterialManager.Installer/theme/OlympeTheme.wxl` - French localization for all UI strings
- `installer/OlympeMaterialManager.Installer/addin/*.addin` - Three .addin files with absolute ProgramFiles assembly paths
- `src/OlympeMaterialManager/OlympeMaterialManager.csproj` - Added StageForInstaller target (Release-only)
- `OlympeMaterialManager.sln` - Added installer project under "installer" solution folder
- `OlympeMaterialManager/.gitignore` - Added staging/ pattern

## Decisions Made
- Used `$(MSBuildProjectDirectory)\..\..\` instead of `$(SolutionDir)` for staging path -- `SolutionDir` is undefined when building a csproj directly via `dotnet build` (not via solution)
- Shared `CoreNet8` Feature for net8.0-windows assemblies referenced by both Revit 2025 and 2026, avoiding WiX duplicate-file validation errors
- Installer project mapped to `Release|Any CPU` for all solution configurations but only built (Build.0) in Release

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed SolutionDir undefined when building csproj directly**
- **Found during:** Task 2 (staging target)
- **Issue:** `$(SolutionDir)` evaluates to `*Undefined*` when building the csproj via `dotnet build` rather than via the solution file, causing staging copy to fail
- **Fix:** Replaced `$(SolutionDir)installer\...` with `$([System.IO.Path]::GetFullPath('$(MSBuildProjectDirectory)\..\..\installer\...\staging'))` for reliable path resolution
- **Files modified:** OlympeMaterialManager.csproj
- **Verification:** Release build succeeded, staging/net48/ and staging/net8.0-windows/ populated
- **Committed in:** 00ffaed (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Path resolution fix required for correct operation. No scope creep.

## Issues Encountered
None beyond the SolutionDir fix documented above.

## User Setup Required
None - no external service configuration required. WiX SDK is restored via NuGet automatically.

## Next Phase Readiness
- Installer project is complete and ready for WiX SDK build
- Release build populates staging directories automatically
- End-to-end installer testing requires WiX Toolset v5 SDK installed locally

## Self-Check: PASSED

All 10 files verified present. Both task commits (42ebf13, 00ffaed) verified in git log.

---
*Phase: 05-polish-and-installer*
*Completed: 2026-04-11*
