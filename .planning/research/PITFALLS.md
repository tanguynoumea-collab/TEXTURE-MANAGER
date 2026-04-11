# Domain Pitfalls

**Domain:** Revit WPF material management add-in (multi-version 2024/2025/2026)
**Researched:** 2026-04-11
**Confidence:** HIGH (primary sources: Autodesk official docs, The Building Coder, Revit API Docs, Autodesk community forums)

---

## Critical Pitfalls

Mistakes that cause rewrites, data corruption, or shipping blockers.

---

### Pitfall 1: Calling Revit API from the WPF UI Thread

**What goes wrong:** Any Revit API call (read or write) made outside a valid Revit API context throws `InvalidOperationException`. A modeless WPF window runs on its own dispatcher -- every button click, every property getter that touches `Document` or `Element`, crashes Revit or throws silently.

**Why it happens:** Developers treat Revit like a normal WPF app where the ViewModel can call services freely. Revit's API is single-threaded and only valid inside callbacks (`IExternalCommand.Execute`, `IExternalEventHandler.Execute`, `Idling` event).

**Consequences:** Instant crash, corrupted model state, or silent failures where changes appear to succeed but are not committed. Debugging is painful because the exception may surface far from the actual illegal call.

**Prevention:**
1. Every Revit API call must go through an `IExternalEventHandler`. The WPF ViewModel calls `ExternalEvent.Raise()`, which queues work for Revit's main thread.
2. Never store `Document`, `UIDocument`, or `UIApplication` references long-term in ViewModels. Obtain them fresh inside `Execute()`.
3. Use a request/response pattern: ViewModel sets a request enum + parameters, raises the event, handler reads the request in `Execute()`, writes results to a shared DTO, ViewModel reads results via dispatcher callback.
4. Consider wrapping this in a `RevitCommandService` abstraction so ViewModels never import `Autodesk.Revit.DB` directly.

**Detection:** Any `using Autodesk.Revit.DB` in a ViewModel file is a code smell. Static analysis or code review rule: "No Revit namespace imports in ViewModels."

**Warning signs:** Sporadic `InvalidOperationException` with message "Running Revit API outside of context", UI freezing during operations, or changes that disappear after closing the window.

**Phase relevance:** Phase 1 (foundation) -- must establish the ExternalEvent communication pattern before any feature work.

---

### Pitfall 2: .NET Framework Split -- 2024 (net48) vs 2025/2026 (net8.0-windows)

**What goes wrong:** Revit 2024 requires .NET Framework 4.8. Revit 2025+ requires .NET 8 (net8.0-windows). A single project targeting one framework cannot load in the other. Building with the wrong target silently produces a DLL that Revit refuses to load.

**Why it happens:** Autodesk migrated from .NET Framework to .NET 8 in Revit 2025. This is not a minor version bump -- it is a completely different runtime. The project architecture (Shared + 3 target projects) exists precisely because of this split, but the `.csproj` files must be configured correctly.

**Consequences:** Add-in silently fails to load in one or more Revit versions with no error message to the user. The addin appears in Revit's add-in manager as "not loaded" or does not appear at all.

**Prevention:**
1. The Shared project contains all business logic with zero framework-specific code.
2. Each target project (2024, 2025, 2026) specifies the correct `TargetFramework`:
   - 2024: `<TargetFramework>net48</TargetFramework>`
   - 2025: `<TargetFramework>net8.0-windows</TargetFramework>`
   - 2026: `<TargetFramework>net8.0-windows</TargetFramework>` (or net10.0-windows if mandated)
3. Add `<FrameworkReference Include="Microsoft.WindowsDesktop.App"/>` to the .NET 8 target projects to resolve WPF/Windows-specific build warnings (MSB3277).
4. Use conditional compilation symbols `REVIT2024`, `REVIT2025`, `REVIT2026` (or `REVIT2024_OR_GREATER` pattern) in each target project's `DefineConstants`.
5. Test each build output separately against its target Revit version during CI or manual validation.

**Detection:** Build both targets and verify output directory DLL framework metadata. Run `dotnet --info` equivalent checks or just load-test in each Revit version early in development.

**Warning signs:** Build succeeds but DLL won't load in Revit. MSB3277 warnings during build. `CA1416` platform compatibility warnings.

**Phase relevance:** Phase 1 (project setup) -- get this wrong and nothing works. Must be validated before writing a single line of feature code.

---

### Pitfall 3: CompoundStructure is a Copy, Not a Reference

**What goes wrong:** `GetCompoundStructure()` returns a **copy** of the compound structure. Developers modify layers on this copy and assume changes are persisted. They are not -- you must call `SetCompoundStructure()` to write the copy back.

**Why it happens:** The API design is counterintuitive. Most Revit element properties are direct references. CompoundStructure breaks this pattern. Additionally, modifying a `HostObjAttributes` type's compound structure affects **all instances** of that type in the document.

**Consequences:** Changes appear to work in the debugger (the copy is modified) but never persist in the model. Or worse, developers correctly call `SetCompoundStructure` but on the wrong type instance, modifying every wall/floor/roof of that type when they intended to modify only one.

**Prevention:**
1. Always follow the pattern: `Get -> Modify -> Set` within a single transaction.
   ```csharp
   var cs = wallType.GetCompoundStructure();
   cs.SetMaterialId(layerIndex, newMaterialId);
   wallType.SetCompoundStructure(cs);
   ```
2. If the user wants to change layers on one instance without affecting others, duplicate the type first with `ElementType.Duplicate()` and modify the duplicate.
3. Document this behavior in code comments at every `GetCompoundStructure` call site.

**Detection:** Unit test or manual test: modify a layer, close and reopen the dialog, verify the change persisted.

**Warning signs:** Material assignments that "work" during the session but revert on undo or don't appear in the Revit type properties dialog.

**Phase relevance:** Phase 2 (CompoundStructure layer editing) -- this is the core feature and must be bulletproof.

---

### Pitfall 4: Transaction Must Be Open Before AppearanceAssetEditScope.Commit()

**What goes wrong:** `AppearanceAssetEditScope` has its own lifecycle (`Start` -> modify -> `Commit/Cancel`) but `Commit()` requires an open Transaction. Developers either forget to open a transaction before committing, or they open the transaction before `Start()` and the edit scope outlives the transaction.

**Why it happens:** The `AppearanceAssetEditScope` is a separate editing mechanism from Transactions. It doesn't auto-create a transaction. The official docs state: "the only part of the process when a transaction must be opened is when Commit() is called."

**Consequences:** `InvalidOperationException` at `Commit()`. Or if the transaction is committed before the edit scope, orphaned changes that may corrupt the appearance asset. The error message is not always clear about the root cause.

**Prevention:**
1. Wrap the entire edit flow:
   ```csharp
   using (var tx = new Transaction(doc, "Edit Material Appearance"))
   {
       tx.Start();
       var editScope = new AppearanceAssetEditScope(doc);
       var asset = editScope.Start(appearanceAssetElementId);
       // modify asset properties...
       editScope.Commit(true); // true = update open views
       tx.Commit();
   }
   ```
2. Only one `AppearanceAssetEditScope` can edit one top-level asset at a time. To edit multiple materials, commit/cancel the current scope before starting a new one.
3. The scope can be reused for another asset after commit/cancel, but starting a new one is clearer.

**Detection:** Try editing two different material appearances in sequence. If the second throws, the scope lifecycle is wrong.

**Warning signs:** "Edit scope already active" exceptions. Material appearance changes that don't visually update in the Revit viewport until manual refresh.

**Phase relevance:** Phase 3 (material editing -- nom, description, motif, couleur, teinte) -- directly impacts live editing feature.

---

### Pitfall 5: ElementId Breaking Change in Revit 2024 (int32 to int64)

**What goes wrong:** Revit 2024 changed `ElementId` from 32-bit to 64-bit storage. `ElementId.IntegerValue` (returns int) is deprecated and throws for large values. The `ElementId(int)` constructor was removed in Revit 2026. Code that casts ElementId values to `int` silently truncates or throws.

**Why it happens:** Autodesk expanded ElementId to support larger models. Old code and many Stack Overflow / Building Coder examples use `IntegerValue` and `new ElementId(int)`.

**Consequences:** Compilation errors in 2026 (constructor removed). Runtime exceptions in 2024/2025 when ElementId values exceed int32 range. Silent data corruption if values are cast to int and truncated.

**Prevention:**
1. Use `ElementId.Value` (returns `long`) everywhere. Never use `IntegerValue`.
2. Use `new ElementId(long)` constructor.
3. Add a conditional compilation guard for 2024 compatibility if the old constructor is needed:
   ```csharp
   #if REVIT2024
   // ElementId(int) still works but deprecated
   #else
   // Use ElementId(long) or ForgeTypeId-based construction
   #endif
   ```
4. Grep the codebase for `IntegerValue` and `new ElementId(` with int literals -- replace all occurrences.
5. `BuiltInParameter` and `BuiltInCategory` underlying type also changed to 64-bit. Casting these to int will fail.

**Detection:** Build against Revit 2026 API DLLs -- compilation errors for removed APIs surface immediately.

**Warning signs:** Compilation warnings about obsolete members when building against 2024/2025 API DLLs. Type cast exceptions at runtime.

**Phase relevance:** Phase 1 (project setup) -- must establish correct ElementId usage patterns from day one.

---

### Pitfall 6: WPF Window Lifecycle and Memory Leaks in Revit Context

**What goes wrong:** Modeless WPF windows that are not properly disposed leak memory for the entire Revit session (which can last hours/days). Event subscriptions from ViewModel to Revit events (DocumentChanged, ViewActivated, etc.) keep the entire window graph alive via strong references.

**Why it happens:** In a normal WPF app, closing the app releases everything. In Revit, the host process persists. A "closed" add-in window that still has event handlers registered to Revit events or ExternalEvent references will never be garbage collected.

**Consequences:** Memory grows with each open/close cycle of the add-in window. After several cycles, Revit becomes sluggish and eventually crashes with OutOfMemoryException. Users blame Revit, not the add-in.

**Prevention:**
1. Use `Window.Closed` event to unsubscribe from all Revit events and dispose resources.
2. Implement `IDisposable` on ViewModels. Call `Dispose()` from the Window's `Closed` handler.
3. Use weak event patterns (`WeakEventManager`) for Revit event subscriptions.
4. Use singleton window pattern: create the WPF window once in `IExternalApplication.OnStartup`, show/hide it. Dispose in `OnShutdown`. Never create new window instances on each command execution.
5. Unsubscribe from `ExternalEvent` -- while the event itself doesn't hold ViewModel references, the `IExternalEventHandler` implementation might.
6. Avoid capturing `this` (the ViewModel) in lambda event subscriptions to Revit events.

**Detection:** Open the add-in window, close it, repeat 20 times. Monitor Revit's memory usage in Task Manager. If it grows linearly, there's a leak.

**Warning signs:** Revit memory usage climbing during a session. Old event handlers firing after the window is "closed". Multiple instances of the same ViewModel in memory profiler.

**Phase relevance:** Phase 1 (window infrastructure) -- establish the singleton window + dispose pattern from the start.

---

## Moderate Pitfalls

---

### Pitfall 7: .addin Manifest Registration Errors

**What goes wrong:** The add-in does not appear in Revit, appears but fails to load, or loads the wrong version's DLL.

**Prevention:**
1. Place `.addin` files in `%APPDATA%\Autodesk\Revit\Addins\{year}\` (per-user) or `%ALLUSERSPROFILE%\Autodesk\Revit\Addins\{year}\` (all-users).
2. Each Revit version (2024, 2025, 2026) needs its own `.addin` file pointing to the correct version-specific DLL.
3. Ensure `<ClientId>` GUID is **unique per add-in** but **the same across versions**. If two `.addin` files share a GUID in the same Revit version, only one loads.
4. Use relative `<Assembly>` path (DLL name only) when the DLL sits alongside the `.addin` file. Use absolute paths with care -- `[TARGETDIR]` placeholders from WiX must be resolved, not left as literals.
5. Validate XML: the `.addin` file must start with `<?xml version="1.0" encoding="utf-8"?>`. Any stray characters before this line causes a parse failure.
6. Set `<VendorId>` and `<VendorDescription>` for commercial distribution -- Autodesk Exchange requires these.

**Detection:** After install, open Revit > Add-Ins tab > External Tools. If missing, check Revit journal file (`%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit {year}\Journals\`) for loading errors.

**Warning signs:** Add-in appears in Revit but all ribbon buttons are grayed out (wrong DLL version loaded). "Failed to initialize the add-in" message at Revit startup.

**Phase relevance:** Phase 1 (project setup) and Phase 5 (installer) -- must work in development (manual .addin) and in deployed state (installer-generated .addin).

---

### Pitfall 8: WiX v4 Installer Targeting Wrong Paths or Missing Version Selection

**What goes wrong:** The installer deploys DLLs and `.addin` files to incorrect directories, or deploys the 2025 DLL into the 2024 addins folder. WiX v4 syntax differs significantly from v3 -- many online examples use v3 XML which doesn't compile in v4.

**Prevention:**
1. Define separate WiX components for each Revit version. Each component targets:
   - DLL: `C:\Program Files\OlympeMaterialManager\{version}\` (or similar central location)
   - `.addin` file: `%ALLUSERSPROFILE%\Autodesk\Revit\Addins\{year}\`
2. WiX does not allow installing the same file to two locations in one component. Use separate components with separate file references, or use `CopyFile` element.
3. Implement version selection UI in the installer (checkboxes for 2024/2025/2026). Use WiX Feature hierarchy to let users pick which versions to install.
4. Use `StandardDirectory` references in WiX v4 (`CommonAppDataFolder` for `%ALLUSERSPROFILE%`), not hardcoded paths.
5. Test the installer on a clean machine (or VM) without Visual Studio installed -- missing .NET runtimes or VC++ redistributables will surface here.
6. For per-machine installation (recommended for commercial add-ins), use `Scope="perMachine"` in the Package element. Per-user installs with per-machine paths cause permission errors.

**Detection:** Install on a clean VM, open each target Revit version, verify the add-in loads and the correct DLL version is active.

**Warning signs:** WiX build errors about duplicate files. Installer succeeds but `.addin` file not found in expected directory. Permission denied errors during install (scope mismatch).

**Phase relevance:** Phase 5 (installer) -- dedicated phase, but prototype early to avoid surprises.

---

### Pitfall 9: PickObject/PickObjects User Cancellation Not Handled

**What goes wrong:** `Selection.PickObject()` and `Selection.PickObjects()` throw `Autodesk.Revit.Exceptions.OperationCanceledException` when the user presses Escape. This is NOT `System.OperationCanceledException` -- catching the wrong namespace silently passes.

**Prevention:**
1. Always wrap pick operations in try/catch catching the Revit-specific exception:
   ```csharp
   try
   {
       var reference = uiDoc.Selection.PickObject(ObjectType.Element, selectionFilter, "Select an element");
   }
   catch (Autodesk.Revit.Exceptions.OperationCanceledException)
   {
       // User pressed Escape -- graceful exit, not an error
       return;
   }
   ```
2. Note: `Autodesk.Revit.Exceptions.OperationCanceledException` is different from `System.OperationCanceledException`. A generic `catch (OperationCanceledException)` may catch the wrong one depending on `using` directives. Be explicit with the full namespace.
3. During a pick session, users cannot switch views, close documents, or interact with the Revit UI. Communicate this clearly with a status bar message parameter.
4. Implement `ISelectionFilter` to restrict selection to valid element types (walls, floors, etc.) to avoid processing invalid selections.

**Detection:** Test: press Escape during every pick operation. Verify the add-in returns gracefully without error dialogs.

**Warning signs:** Unhandled exception dialog in Revit when pressing Escape during selection. Add-in hangs waiting for a pick result that will never come.

**Phase relevance:** Phase 2 (adding types to scene via 3D view click) -- pick operations are integral to the "click in 3D view" workflow.

---

### Pitfall 10: Performance Degradation with Large Material Libraries

**What goes wrong:** Collecting all materials with `FilteredElementCollector` using LINQ post-filtering instead of native quick filters causes multi-second delays in projects with thousands of materials. The preset panel becomes unresponsive.

**Prevention:**
1. Use native quick filters before any LINQ processing:
   ```csharp
   var materials = new FilteredElementCollector(doc)
       .OfClass(typeof(Material))
       .Cast<Material>()
       .ToList(); // quick filter only, no LINQ Where before this
   ```
2. `OfClass` is a quick filter (operates on element headers, doesn't load full elements). Adding `.Where(m => m.Name.Contains(...))` after `ToList()` is acceptable. Adding `.Where()` before `ToList()` on the collector forces slow iteration.
3. Cache material collections. Don't re-collect on every UI interaction. Invalidate the cache when `DocumentChanged` event fires for material-related changes.
4. For the preset panel (JSON-based), load presets asynchronously and display a loading indicator. Don't block the UI thread parsing large JSON files.
5. Use virtualized `ListView`/`ListBox` in WPF (`VirtualizingStackPanel.IsVirtualizing="True"`) for material lists with 500+ items.
6. Avoid `ToElements()` on the collector unless you need the count. The collector itself is lazy -- iterate directly.

**Detection:** Profile with a test project containing 2000+ materials. Measure time from button click to UI populated. Target: under 500ms.

**Warning signs:** Noticeable delay when opening the material preset panel. UI freeze when switching scenes. Memory spikes during material collection.

**Phase relevance:** Phase 3 (preset panel) and Phase 4 (Set Mat operations) -- optimize after functionality works, but design for it from the start (caching pattern, virtualization).

---

### Pitfall 11: Revit 2026 CompoundStructure Core Layer Changes

**What goes wrong:** Revit 2026 removes the requirement for a core layer in CompoundStructures. Code that assumes every CompoundStructure has at least one core layer (accessing `GetCoreBoundaryLayerIndex`, iterating core layers) throws `ArgumentOutOfRangeException` or returns unexpected results in 2026.

**Prevention:**
1. Before accessing core-specific APIs, check if core layers exist:
   ```csharp
   var layers = cs.GetLayers();
   // Don't assume layers[coreIndex] exists
   ```
2. Use conditional compilation for 2026-specific behavior:
   ```csharp
   #if REVIT2026_OR_GREATER
   // Handle structures without core layers
   #endif
   ```
3. The `CompoundStructure.GetLayerPriority()` / `SetLayerPriority()` APIs are new in 2026 -- don't call them in 2024/2025 builds.
4. Test with wall types that have no core layer (only available in Revit 2026 models).

**Detection:** Create a test wall type in Revit 2026 with no core layer. Run the add-in. If it crashes or shows empty layers, the code makes core-layer assumptions.

**Warning signs:** `IndexOutOfRangeException` when processing wall types in Revit 2026. Layer display showing wrong layer count.

**Phase relevance:** Phase 2 (CompoundStructure display and editing) -- must handle gracefully across all three versions.

---

### Pitfall 12: CopyLocal = True on Revit API References

**What goes wrong:** Visual Studio defaults `CopyLocal` to `True` when adding Revit API DLL references. This copies `RevitAPI.dll` and `RevitAPIUI.dll` into the output directory. When Revit loads the add-in, .NET may load these local copies instead of Revit's own assemblies, causing version mismatches, type identity conflicts, and cryptic `InvalidCastException` or `MissingMethodException`.

**Prevention:**
1. Set `<Private>False</Private>` (the `.csproj` equivalent of CopyLocal=False) on all Revit API references:
   ```xml
   <Reference Include="RevitAPI">
     <HintPath>...\RevitAPI.dll</HintPath>
     <Private>False</Private>
   </Reference>
   ```
2. If using NuGet packages for Revit API (e.g., `Revit.API` from ricaun), verify they set `PrivateAssets="all"` or equivalent.
3. Post-build validation: check the output directory for `RevitAPI.dll` or `RevitAPIUI.dll`. If present, the reference is misconfigured.
4. Also set CopyLocal=False for `AdWindows.dll`, `UIFramework.dll`, and any other Autodesk assemblies that ship with Revit.

**Detection:** Check the `bin\Debug` or `bin\Release` folder after build. If any `RevitAPI*.dll` files are present, CopyLocal is wrong.

**Warning signs:** `System.IO.FileLoadException` or `MissingMethodException` at runtime. Debugging breakpoints not hitting. Revit loading old cached versions of the add-in DLL.

**Phase relevance:** Phase 1 (project setup) -- must be configured correctly in every target project's `.csproj`.

---

### Pitfall 13: Modeless Window Loses Revit Focus / Z-Order Issues

**What goes wrong:** A modeless WPF window using `Show()` either falls behind the Revit window (user can't find it) or steals focus so aggressively that Revit's own UI becomes unresponsive.

**Prevention:**
1. Set the Revit main window as the WPF window's owner:
   ```csharp
   var revitHandle = uiApp.MainWindowHandle; // or Process.GetCurrentProcess().MainWindowHandle
   var hwndSource = new WindowInteropHelper(wpfWindow);
   hwndSource.Owner = revitHandle;
   ```
2. Use `Topmost = false` (not true -- true steals focus from ALL windows on the desktop, which is hostile UX).
3. Setting the owner handle keeps the window above Revit but below other applications.
4. In Revit 2019+, use `UIApplication.MainWindowHandle` directly. For earlier versions, use `Process.GetCurrentProcess().MainWindowHandle`.

**Detection:** Open the add-in window, click in the Revit viewport, try to find the add-in window. If it's behind Revit, ownership is not set.

**Warning signs:** Users report "the add-in window disappeared" or "I have to Alt+Tab to find it."

**Phase relevance:** Phase 1 (window infrastructure) -- set this up when creating the main modeless window.

---

## Minor Pitfalls

---

### Pitfall 14: Material.MaterialId Returns InvalidElementId for Category-Based Layers

**What goes wrong:** When reading `CompoundStructureLayer.MaterialId`, a value of `ElementId.InvalidElementId` (-1) means the layer uses the category's default material, not that it has no material. Treating this as "no material" and displaying a blank entry confuses users.

**Prevention:**
1. When `MaterialId == ElementId.InvalidElementId`, display "< By Category >" or "< Par categorie >" (French UI) instead of empty/null.
2. When assigning a material to such a layer, the assignment works normally -- `SetMaterialId` with a valid ID overrides the category default.
3. Store this semantic distinction in the ViewModel layer model so the UI can render it appropriately.

**Phase relevance:** Phase 2 (center panel layer display).

---

### Pitfall 15: AppearanceAsset Connected Assets (Textures) Not Editable Through EditScope Directly

**What goes wrong:** The texture path is stored in a connected `RenderingAsset`, not the top-level `AppearanceAsset`. Developers attempt to find the texture path property directly on the appearance asset and fail. The `RenderingAsset` has no `ElementId` and cannot be passed to `AppearanceAssetEditScope.Start()`.

**Prevention:**
1. Access connected assets through the parent asset's properties. Texture properties are nested:
   ```csharp
   var asset = editScope.Start(assetElementId);
   var bitmapProperty = asset.FindByName("generic_diffuse") as AssetPropertyDoubleArray4d;
   var connectedAsset = bitmapProperty.GetConnectedProperty(0) as Asset;
   // Now access texture path from connectedAsset
   ```
2. The edit scope allows modifying connected assets through the parent -- you don't need a separate scope for the connected asset.
3. For the project's scope (nom, description, motif, couleur, teinte), most properties are on the top-level asset. Texture path editing may not be needed if the preview uses a fallback colored image (per Out of Scope).

**Phase relevance:** Phase 3 (material editing). Less critical if realistic texture rendering is out of scope.

---

### Pitfall 16: JSON Preset File Path Persistence Across Machines

**What goes wrong:** The user chooses a preset JSON file path, which is stored (presumably in user settings or registry). When the project moves to another machine, or the path uses a mapped network drive letter, the file is not found and presets fail to load with no clear error.

**Prevention:**
1. On startup, validate the stored path exists. If not, prompt the user to re-select.
2. Support both absolute paths and UNC paths (`\\server\share\presets.json`).
3. Store the path in a user-scoped settings file (e.g., `%APPDATA%\OlympeMaterialManager\settings.json`), not in the Revit project file.
4. Provide a "Browse" button that's always accessible, not hidden behind a settings menu.
5. Handle `IOException`, `UnauthorizedAccessException`, and `JsonException` with user-friendly French error messages.

**Phase relevance:** Phase 3 (preset panel) -- implement defensive loading from the start.

---

### Pitfall 17: Revit 2026 Dependency Isolation Changes

**What goes wrong:** Revit 2026 introduces add-in dependency isolation via `UseRevitContext` manifest setting. If set to `false`, each add-in loads in its own assembly context. Third-party dependencies (CommunityToolkit.Mvvm, Newtonsoft.Json, etc.) that were previously shared with Revit or other add-ins are now isolated, which can break inter-add-in communication or cause duplicate type loading.

**Prevention:**
1. Default is `UseRevitContext = true` (backward compatible). Don't change this unless you have dependency conflicts.
2. If adopting isolation, ensure ALL dependencies are bundled with the add-in -- you can no longer rely on Revit or other add-ins having loaded a compatible version.
3. Revit 2026 also removed CefSharp dependencies from Revit's install. If the add-in used CefSharp (unlikely for this project), it must bundle its own.
4. For this project (CommunityToolkit.Mvvm + WPF), isolation is unlikely to cause issues. But test in 2026 specifically.

**Phase relevance:** Phase 5 (installer) -- verify isolation behavior during deployment testing for 2026.

---

### Pitfall 18: Transaction Naming for Undo Stack Clarity

**What goes wrong:** Using generic transaction names like "Transaction" or "Modify" makes the Revit undo stack useless for users. Architects expect to see descriptive undo entries to selectively undo operations.

**Prevention:**
1. Use descriptive French transaction names matching the user action:
   - "Appliquer materiau '{name}' aux couches" for Set Mat
   - "Modifier materiau '{name}'" for material editing
   - "Dupliquer materiau '{name}'" for duplication
2. Wrap related multi-step operations in a `TransactionGroup` with a single user-facing name, then `Assimilate()` the group to merge inner transactions into one undo entry.
3. Never leave transactions unnamed or with English names in a French-language add-in.

**Phase relevance:** Phase 2+ (all phases that modify the model).

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| **Phase 1: Foundation / Project Setup** | .NET split (net48 vs net8.0), CopyLocal=True, ElementId int64, window lifecycle | Validate multi-version build produces correct DLLs before writing features. Establish ExternalEvent pattern. Set up singleton window with dispose. |
| **Phase 2: Scene + Layer Display** | CompoundStructure copy semantics, core layer optional (2026), MaterialId = -1 for category, PickObject cancellation | Implement Get->Modify->Set pattern. Handle missing core layers. Handle ESC during pick. |
| **Phase 3: Preset Panel + Material Editing** | AppearanceAssetEditScope + Transaction lifecycle, connected asset access, JSON path validation, performance with large libraries | Wrap EditScope inside Transaction. Validate file paths defensively. Cache material collections. |
| **Phase 4: Set Mat Operations** | Type modification affects all instances, transaction naming, multi-selection edge cases | Warn user before modifying shared types. Use descriptive transaction names. Handle empty selection gracefully. |
| **Phase 5: Installer** | WiX v4 syntax (not v3), wrong paths, version mismatch, dependency isolation (2026) | Test on clean VM. Use separate components per version. Verify .addin file placement. |

---

## Sources

### Official Documentation
- [Revit API 2026 Changes](https://www.revitapidocs.com/2026/news)
- [Revit API 2025 Changes](https://www.revitapidocs.com/2025/news)
- [External Events Documentation](https://help.autodesk.com/view/RVT/2024/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_External_Events_html)
- [Add-in Registration](https://help.autodesk.com/view/RVT/2026/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Add_in_Registration_html)
- [Add-in Dependency Isolation (2026)](https://help.autodesk.com/view/RVT/2026/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Add_in_Dependency_Isolation_html)
- [Migrating from .NET 4.8 to .NET 8](https://help.autodesk.com/cloudhelp/2025/DEU/Revit-API/files/Revit_API_Developers_Guide/Introduction/Getting_Started/Using_the_Autodesk_Revit_API/Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Using_the_Autodesk_Revit_API_NET8_Update_html.html)
- [What's New in Revit API 2026](https://rvtdocs.com/2026/whatsnew)
- [Major API Changes 2026](https://help.autodesk.com/view/RVT/2026/ENU/?guid=8af227f4-b765-4430-97ce-16108dfe3788)

### The Building Coder (Jeremy Tammik)
- [CompoundStructure Layer Updates](https://thebuildingcoder.typepad.com/blog/2012/03/updating-wall-compound-layer-structure.html)
- [Modifying Material Visual Appearance](https://thebuildingcoder.typepad.com/blog/2017/11/modifying-material-visual-appearance.html)
- [Modeless Form Focus and Z-Order](https://thebuildingcoder.typepad.com/blog/2017/10/modeless-form-keep-revit-focus-and-on-top.html)
- [Prompt Cancel Exception](https://thebuildingcoder.typepad.com/blog/2017/05/prompt-cancel-throws-exception-in-revit-2018.html)
- [Set Copy Local to False](https://jeremytammik.github.io/tbc/a/0634_copy_local_false.htm)
- [Transaction Groups](https://thebuildingcoder.typepad.com/blog/2015/02/using-transaction-groups.html)
- [FilteredElementCollector Performance](https://thebuildingcoder.typepad.com/blog/2010/10/filtered-element-collectors.html)

### Community / Developer Blogs
- [Revit API 2024 Obsolete APIs](https://ricaun.com/revit-api-2024-obsolete/)
- [Maintaining Revit Plugins for Multiple Versions](https://archi-lab.net/how-to-maintain-revit-plug-ins-for-multiple-versions/)
- [ElementId int32 vs int64 Discussion](https://forums.autodesk.com/t5/revit-api-forum/revit-2024-elementid-integervalue-int32-vs-elementid-value-int64/td-p/11911934)
- [AppearanceAssetEditScope Texture Path](https://forums.autodesk.com/t5/revit-api-forum/changing-material-texture-path-with-editscope/m-p/8017578)
- [Modeless WPF in Revit - Autodesk University](https://www.autodesk.com/autodesk-university/class/Modeless-Revit-Plug-Ins-Windows-Presentation-Foundation-2020)
- [WPF Memory Leaks - JetBrains Blog](https://blog.jetbrains.com/dotnet/2014/09/04/fighting-common-wpf-memory-leaks-with-dotmemory/)
- [Revit IFC WiX Installer Reference](https://github.com/Autodesk/revit-ifc/blob/master/Install/RevitIFCSetupWix/Product.wxs)
