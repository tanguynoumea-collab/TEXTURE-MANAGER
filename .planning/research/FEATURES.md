# Feature Landscape

**Domain:** Revit Material Management Add-in (WPF, Architect-targeted)
**Researched:** 2026-04-11
**Overall confidence:** MEDIUM-HIGH (based on Revit API docs, competitor analysis, community forums)

## Table Stakes

Features users expect from any Revit material management tool. Missing = product feels incomplete or amateurish.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| List all materials in document | Every competitor does this (ModPlus, native Material Browser). Baseline for any material tool. | Low | `FilteredElementCollector` with `OfClass(typeof(Material))` |
| Material name display and search | ModPlus has case-insensitive search. Users manage hundreds of materials per project. | Low | Simple string filtering on `Material.Name` |
| Material color display | Color swatch is the primary visual identifier. Native browser shows it. | Low | `Material.Color` property (RGB) |
| Surface pattern display | Architects think in patterns (hatch, crosshatch). Core to identity data. | Medium | `Material.SurfaceForegroundPatternId` -> `FillPatternElement` |
| CompoundStructure layer listing for system families | Walls/floors/roofs/ceilings are the primary use case. All layer tools expose this. | Medium | `HostObjAttributes.GetCompoundStructure().GetLayers()` returns ordered layers |
| Apply material to CompoundStructure layer | Core workflow: pick a layer, assign a material. BIMsmith Forge and Dynamo scripts do this. | Medium | `CompoundStructure.SetMaterialId(layerIndex, materialId)` then `SetCompoundStructure()` |
| Material duplication | Native Revit and ModPlus both support this. Needed for "variant" workflows. | Low | `Material.Duplicate(name)` |
| Undo/Redo via Revit Transactions | Users expect Ctrl+Z to work. Revit API requires explicit Transaction wrapping. | Low | All modifications inside `Transaction` -- Revit handles undo stack natively |
| Multi-version Revit support (2024/2025/2026) | Professional add-ins support at least 2-3 recent versions. CTC, DiRoots, ModPlus all do. | Medium | Shared project + per-version targets with CopyLocal=false API refs |
| Persistent presets/library across sessions | Architextures, BIMsmith, and native `.adsklib` all persist material definitions externally. Users expect to not lose work. | Medium | JSON file at user-chosen path. Must survive Revit restarts. |

## Differentiators

Features that set Olympe MaterialManager apart. Not standard in competitors, but solve real pain points.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Unified 3-panel visual editor** | No competitor offers a single view combining family/type tree + layer/parameter editor + preset palette. ModPlus is 2-panel (list+edit). Native browser is single-panel. BIMsmith is cloud-oriented. This is the core UX differentiator. | High | Left (TreeView) + Center (layers/params) + Right (presets). Requires tight MVVM binding. |
| **Scene/subset management** | No existing Revit add-in offers "scene" concept for material work. Architects work on subsets (e.g., "facade study", "interior finishes"). Filtering visible types by user-created groups is novel. | High | In-memory scene objects, add types via dropdown or 3D pick. Session-only persistence (per PROJECT.md). |
| **3D view pick to add types** | Click an element in the 3D view to add its type to the scene. Faster than browsing lists. Uses `UIDocument.Selection.PickObject()`. | Medium | Must handle `ISelectionFilter`, return to WPF after pick. Thread coordination with `IExternalEventHandler`. |
| **Material parameter support for loaded families** | Most tools focus on system families (walls/floors). Loaded families (furniture, fixtures, custom) have Material-type parameters that are rarely exposed in batch tools. | High | Must discover `Parameter` with `StorageType == ElementId` and value type Material. Multiple material params possible per family -- user must choose which to modify. |
| **Preset groups (Murs/Sols/Autres + custom)** | Organizes presets by architectural intent, not just alphabetically. No competitor does this with user-definable groups. | Medium | JSON structure with group hierarchy. UI drag-drop or context menu to organize. |
| **Live material editing** | Edit name, description, surface pattern, color, and appearance tint directly from the preset panel. Competitors force you back to Revit's Material Browser dialog. | High | `AppearanceAssetEditScope` for tint. `Material.Color`, `Material.SurfaceForegroundPatternId` for identity properties. All within Revit Transaction. |
| **Appearance tint editing** | Direct tint color modification without opening Revit's Material Editor. This is a pain point -- the native editor is 4+ clicks deep. | High | Uses `AppearanceAssetEditScope.Start()` / `.Commit()`. Must handle the "generic_diffuse" asset property (DoubleArray4d). Known API quirk: assign AppearanceAsset before setting UseRenderAppearance. |
| **Material preview sphere** | Visual preview of what the material looks like. Native browser has this but it's buried. Having it inline in the preset panel is a differentiator. | High | API limitation: `Material` thumbnail path may not exist until rendering tab is opened. Fallback: generate a colored circle/sphere from `Material.Color` + pattern overlay. See Anti-Features for realistic render. |
| **Multi-selection of layers/parameters** | Select multiple layers across different types and apply a material to all at once. Batch operation not available in native Revit. | Medium | `ObservableCollection<SelectedLayer>` with checkbox or Ctrl+click. Apply iterates all selections within single Transaction. |
| **Olympe dark theme with amber/orange accent** | Modern, distinctive visual identity. Revit 2024+ supports dark mode natively, but add-in UIs often look dated. A polished custom theme signals quality. | Medium | WPF ResourceDictionary with custom styles. Must handle Revit's own dark/light theme detection (session-specific). |

## Anti-Features

Features to explicitly NOT build. Tempting but wrong for this product.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Realistic material sphere render** | Revit API has no built-in material render-to-image. Thumbnail paths are unreliable (may not exist). Building a custom renderer (OpenGL/DirectX sphere) is massive scope for marginal value. PROJECT.md explicitly marks this out of scope. | Use a stylized preview: colored circle with pattern overlay, or retrieve thumbnail when available and show a placeholder when not. Acceptable per PROJECT.md. |
| **Cloud material library** | BIMsmith already dominates cloud material libraries with 300K+ materials and manufacturer partnerships. Competing here is pointless for a desktop-focused tool. Adds auth, networking, sync complexity. | Local JSON file for presets. User chooses path (can be on network drive for team sharing). Simple, reliable, no server dependency. |
| **Full PBR texture/map editing** | Architextures owns this space (bump maps, normal maps, displacement). The API for editing texture bitmap paths is fragile and version-dependent. Beyond architect workflow needs. | Edit only the properties architects care about in daily work: name, description, color, surface pattern, tint. Leave full PBR to Architextures or native Material Editor. |
| **Material creation from scratch** | Creating new materials programmatically requires handling 19+ asset schemas (Ceramic, Concrete, Generic, Metal, etc.) with complex validation. High complexity, low frequency need. | Support duplication of existing materials (which copies all asset data). User duplicates a close match and tweaks properties. |
| **External database / server persistence** | PROJECT.md explicitly excludes this. Adds deployment complexity, auth, network dependencies. Architects work offline frequently (site visits, travel). | JSON file persistence. In-memory during session. File path remembered in user settings. |
| **Scene persistence across sessions** | PROJECT.md marks this out of scope. Scenes are working subsets, not permanent configurations. Persisting them adds complexity (type references may become invalid between sessions). | Session-only scenes. Users can quickly recreate scenes from the family/type list or 3D pick. |
| **Support for Revit < 2024** | .NET 4.8 is imposed by Revit. Older versions have different API surfaces, especially around `AppearanceAssetEditScope` (introduced 2018.1). Not worth the compatibility burden. | Target 2024/2025/2026 only. Three-version support is industry standard. |
| **IFC/gbXML material export** | Different domain (interoperability). Specialized tools like Revit's native IFC exporter handle this. Low overlap with material visual management. | Stay focused on visual material assignment workflow. |
| **Structural/Thermal asset editing** | Physical and thermal properties (Young's modulus, thermal conductivity) are engineering concerns, not architecture visual design. Different user persona entirely. | Only expose identity (name, description) and appearance (color, pattern, tint) properties. |
| **Mobile/web companion app** | PROJECT.md excludes this. The value is in Revit integration, not remote access. | Desktop WPF only, running in Revit's process. |

## Feature Dependencies

```
Foundation Layer:
  Material data model (read Material, Color, Pattern, AppearanceAsset)
    -> CompoundStructure layer reading
    -> Loaded family material parameter discovery
    -> Material preset JSON persistence

Core Interaction:
  TreeView of families/types (left panel)
    -> Scene management (creates/filters the tree)
    -> 3D pick to add type (adds to scene -> updates tree)
  
  Layer/parameter display (center panel)
    -> Depends on: TreeView selection
    -> Multi-selection of layers
    -> Material application ("Set Mat")

  Preset palette (right panel)
    -> Depends on: Material data model
    -> Material duplication
    -> Preset group organization
    -> Live material editing (name, desc, pattern, color, tint)
    -> Material preview visualization

Application Flow:
  "Set Mat" operation
    -> Requires: selected layers/params (center) + selected preset (right)
    -> For system families: CompoundStructure.SetMaterialId()
    -> For loaded families: Parameter.Set(materialId) with user param choice

Infrastructure:
  Multi-version support (shared project architecture)
    -> All features depend on this being correct
  
  IExternalEventHandler thread model
    -> All Revit API calls depend on this
    -> WPF UI updates via Dispatcher
  
  Dark theme (ResourceDictionary)
    -> All UI panels depend on this for visual consistency
```

## MVP Recommendation

**Prioritize in this order:**

1. **Material data model + CompoundStructure reading** -- Foundation for everything. Without this, nothing works.
2. **3-panel layout with TreeView (left) + layers (center) + presets (right)** -- The core UX differentiator. Get the layout right first.
3. **Preset JSON persistence with groups** -- Users need to save/load presets or the tool has no memory.
4. **"Set Mat" for system families (walls/floors/roofs/ceilings)** -- The primary action. This is the value proposition.
5. **Material preview (simplified: color + pattern, not realistic sphere)** -- Visual feedback for preset selection. Acceptable fallback per PROJECT.md.
6. **Scene management** -- Differentiator that makes large projects manageable. Can ship without it initially but should be Phase 2.
7. **Loaded family material parameter support** -- Important but secondary to system families. More complex (param discovery, user choice).
8. **Live material editing (name, color, pattern, tint)** -- Power feature. AppearanceAssetEditScope is complex. Ship as enhancement.
9. **3D view pick** -- Nice UX shortcut but requires IExternalEventHandler coordination. Add after core is stable.

**Defer:**
- **Appearance tint editing**: High complexity due to `AppearanceAssetEditScope` quirks. Defer until core workflow is solid.
- **Multi-selection batch apply**: Nice optimization but single-selection works first.

## Competitive Landscape Summary

| Competitor | Type | Strengths | Weaknesses | Overlap with Olympe |
|-----------|------|-----------|------------|-------------------|
| **Native Material Browser** | Built-in | Always available, full editing | Poor batch workflow, no layer-to-preset mapping, buried UI | Low -- Olympe complements, doesn't replace |
| **ModPlus Material Manager** | Free plugin | Good table view, search, unused material cleanup | 2-panel only, no CompoundStructure editing, no presets | Medium -- similar list/edit but Olympe adds layers+presets |
| **BIMsmith Forge** | Free cloud | 300K+ materials, manufacturer data, assembly builder | Cloud-dependent, no offline, no preset management | Low -- different market (content sourcing vs. management) |
| **Architextures** | Paid add-in | PBR textures, bump maps, seamless textures | Import-only, no editing, no layer assignment | Low -- texture sourcing vs. material assignment |
| **CTC BIM Manager** | Paid suite | Batch family processing, material replace in families | Overkill for visual material work, expensive, BIM manager focus | Low -- different persona (BIM manager vs. architect) |
| **Dynamo scripts** | Free/custom | Flexible, scriptable, batch operations | Requires Dynamo knowledge, no persistent UI, brittle | Low -- power users only, Olympe targets daily workflow |

**Olympe's niche:** The unified visual editor for architects who need to rapidly assign and manage materials across compound layers and loaded families in a 3D context, with persistent presets. No existing tool occupies this exact space.

## Sources

- [ModPlus Material Manager](https://modplus.org/en/revitplugins/mprmaterialmanager) -- Competitor feature analysis
- [Autodesk App Store - Materials category](https://apps.autodesk.com/RVT/en/List/Search?facet=__category::Materials) -- Market overview
- [BIMsmith Forge](https://blog.bimsmith.com/How-to-Design-with-300000-Revit-Materials-with-BIMsmith-Forge) -- Cloud material library competitor
- [Architextures for Revit](https://architextures.org/page/architextures-for-revit) -- Texture import competitor
- [CTC BIM Manager Suite](https://apps.autodesk.com/RVT/en/Detail/Index?id=7318700961041173172&appLang=en&os=Win64) -- Batch processing competitor
- [Revit API Material docs (2025)](https://help.autodesk.com/cloudhelp/2025/PTB/Revit-API/files/Revit_API_Developers_Guide/Revit_Geometric_Elements/Material/Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Material_General_Material_Information_html.html) -- API capabilities
- [CompoundStructure API](https://www.revitapidocs.com/2016/dc1a081e-8dab-565f-145d-a429098d353c.htm) -- Layer manipulation API
- [AppearanceAssetEditScope](https://www.revitapidocs.com/2019/743c74ba-12de-4d77-a677-325229525955.htm) -- Visual appearance editing API
- [The Building Coder - Modifying Material Appearance](https://thebuildingcoder.typepad.com/blog/2017/11/modifying-material-visual-appearance.html) -- API workarounds and gotchas
- [Revit Material Browser help](https://help.autodesk.com/cloudhelp/2023/ENU/Revit-Customize/files/GUID-0AA0E65D-55A4-4391-AA29-C53C06C048F4.htm) -- Native workflow reference
- [FilteredElementCollector visible in view](https://thebuildingcoder.typepad.com/blog/2017/05/retrieving-elements-visible-in-view.html) -- 3D view element collection
- [Revit material management workflow discussion](https://www.revitforum.org/forum/revit-architecture-forum-rac/architecture-and-general-revit-questions/456109-revit-materials-management-what-s-your-workflow) -- Community workflows
- [Material library best practices](https://novedge.com/blogs/design-news/revit-tip-optimizing-material-library-management-in-revit-for-enhanced-workflow-efficiency) -- Library management patterns
- [About Material Libraries (Revit 2025)](https://help.autodesk.com/view/RVT/2025/ENU/?guid=GUID-26E4614F-6BAF-4F63-A285-99F1B3BE02F5) -- Official library docs
- [Material thumbnail path forum](https://forums.autodesk.com/t5/revit-api-forum/get-an-up-to-date-material-preview-image-file-path/td-p/7731827) -- Preview image limitations
- [Revit duplicate materials issue](https://forums.autodesk.com/t5/revit-architecture-forum/revit-2024-duplicate-materials/td-p/13317157) -- Common pain point
- [Dark theme styling for Revit add-ins](https://sharpbim.hashnode.dev/styling-revit-ui) -- WPF theming approach
