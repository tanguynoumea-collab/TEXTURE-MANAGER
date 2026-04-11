---
phase: 04-material-editing-and-3d-pick
plan: 01
subsystem: api
tags: [revit-api, material-editing, appearance-asset, pick-object, dto, messenger]

# Dependency graph
requires:
  - phase: 03-write-path-set-mat
    provides: RevitEventBridge pattern, Transaction handlers, PresetMaterialDto, ExtractColorArgb helper
provides:
  - MaterialDetailsDto for material visualizer (Plan 02)
  - 4 edit request DTOs for material editor UI commands (Plan 02)
  - MaterialSelectedMessage and MaterialEditedMessage for inter-VM communication (Plan 02, Plan 03)
  - 6 new RevitRequestType enum values for Phase 4 dispatch
  - 6 new RevitEventBridge handlers (GetMaterialDetails, EditMaterialName, EditMaterialDescription, EditMaterialColor, EditMaterialTint, PickElementInView)
affects: [04-02-PLAN, 04-03-PLAN]

# Tech tracking
tech-stack:
  added: []
  patterns: [AppearanceAssetEditScope with common_Tint_toggle/common_Tint_color, PickObject with window hide/show/finally pattern]

key-files:
  created:
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/MaterialDetailsDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/EditMaterialNameRequestDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/EditMaterialDescriptionRequestDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/EditMaterialColorRequestDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Models/EditMaterialTintRequestDto.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Messages/MaterialSelectedMessage.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Messages/MaterialEditedMessage.cs
  modified:
    - OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitRequestType.cs
    - OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitEventBridge.cs

key-decisions:
  - "common_Tint_toggle/common_Tint_color used for tint editing (cross-schema, not generic-only properties)"
  - "SetValueAsDoubles used instead of SetValueAsColor for tint color (avoids schema-specific exceptions per Pitfall 4)"
  - "Fully-qualified Autodesk.Revit.Exceptions.OperationCanceledException in PickObject catch (not System namespace)"
  - "try/finally pattern guarantees window re-show after PickObject regardless of exception type"

patterns-established:
  - "AppearanceAssetEditScope lifecycle: Transaction.Start -> scope.Start -> modify properties -> scope.Commit(true) -> tx.Commit"
  - "PickObject from modeless WPF: Hide -> PickObject -> Show in finally block via Dispatcher.Invoke"
  - "Material property edit handler: individual Transaction with French name for undo granularity"

requirements-completed: [MATEDIT-01, MATEDIT-02, MATEDIT-03, MATEDIT-04, MATEDIT-05, MATEDIT-06, MATEDIT-07, MATEDIT-08, SCENE-04, SCENE-09]

# Metrics
duration: 3min
completed: 2026-04-11
---

# Phase 4 Plan 01: Data Foundation Summary

**5 DTOs, 2 Messenger messages, 6 RevitRequestType enum values, and 6 RevitEventBridge handlers for material editing and 3D element pick**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-11T12:07:34Z
- **Completed:** 2026-04-11T12:10:55Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments
- Created MaterialDetailsDto with 8 properties (name, description, color, pattern, tint, thumbnail) for the material visualizer
- Created 4 typed request DTOs for material editing (name, description, color, tint) ensuring type safety at the handler boundary
- Created MaterialSelectedMessage and MaterialEditedMessage for inter-ViewModel communication following existing Messenger pattern
- Added 6 RevitEventBridge handlers: GetMaterialDetails (read), 4 edit handlers (write), PickElementInView (3D pick)
- HandleEditMaterialTint uses AppearanceAssetEditScope with common_Tint_toggle/common_Tint_color and SetValueAsDoubles
- HandlePickElementInView uses try/finally to guarantee window re-show, catches Autodesk.Revit.Exceptions.OperationCanceledException

## Task Commits

Each task was committed atomically:

1. **Task 1: Create DTOs and Messenger messages** - `f4cc481` (feat)
2. **Task 2: Add 6 enum values and 6 RevitEventBridge handlers** - `26682ef` (feat)

## Files Created/Modified
- `Models/MaterialDetailsDto.cs` - DTO for GetMaterialDetails response (8 properties)
- `Models/EditMaterialNameRequestDto.cs` - Request DTO for material rename
- `Models/EditMaterialDescriptionRequestDto.cs` - Request DTO for description edit
- `Models/EditMaterialColorRequestDto.cs` - Request DTO for surface color edit (RGB bytes)
- `Models/EditMaterialTintRequestDto.cs` - Request DTO for tint edit (toggle + RGB bytes)
- `Messages/MaterialSelectedMessage.cs` - ValueChangedMessage<PresetMaterialDto?> for selection
- `Messages/MaterialEditedMessage.cs` - ValueChangedMessage<long> for edit notification
- `Events/RevitRequestType.cs` - 6 new enum values for Phase 4
- `Events/RevitEventBridge.cs` - 6 new handler methods + GetPatternName helper

## Decisions Made
- Used common_Tint_toggle/common_Tint_color (cross-schema) instead of generic_diffuse/generic_is_metal (Generic-only), per Research recommendation
- Used SetValueAsDoubles instead of SetValueAsColor to avoid schema-specific exceptions (Pitfall 4)
- Thumbnail path left as best-effort (null if unavailable) per D-18 -- no blocking on missing thumbnails
- View3D validation done inside handler (throw if not 3D) rather than separate validation request

## Deviations from Plan

None -- plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None -- no external service configuration required.

## Known Stubs
None -- all handlers are fully implemented with complete logic.

## Next Phase Readiness
- All DTOs and messages ready for Plan 02 (material editor UI) and Plan 03 (3D pick UI) to consume
- RevitEventBridge handlers tested via build -- runtime validation deferred to Plan 02/03 integration
- No blockers for Plan 02 or Plan 03

## Self-Check: PASSED

- All 9 source files: FOUND
- SUMMARY.md: FOUND
- Commit f4cc481 (Task 1): FOUND
- Commit 26682ef (Task 2): FOUND
- Build: 0 errors, 0 warnings (both net48 and net8.0-windows)

---
*Phase: 04-material-editing-and-3d-pick*
*Completed: 2026-04-11*
