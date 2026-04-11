# Phase 4: Material Editing and 3D Pick - Context

**Gathered:** 2026-04-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can edit material properties live (name, description, pattern/color, tint) and add types to scenes by clicking elements in the 3D view. This adds power-user features on top of the core preset/SetMat system.

Requirements: MATEDIT-01, MATEDIT-02, MATEDIT-03, MATEDIT-04, MATEDIT-05, MATEDIT-06, MATEDIT-07, MATEDIT-08, SCENE-04, SCENE-09

</domain>

<decisions>
## Implementation Decisions

### Material Visualizer
- **D-01:** Material visualizer is a dedicated zone in the right panel, below the preset TreeView. Shows details of the currently selected preset material.
- **D-02:** Displays: MaterialName (editable TextBox), Description (editable TextBox), SurfaceForegroundPatternColor (editable ColorPicker or RGB fields), SurfacePatternName (read-only), TintEnabled (CheckBox), TintColor (editable RGB when enabled), Preview image.
- **D-03:** Edits trigger immediate Revit Transactions via RevitEventBridge. Each field change = one Transaction for responsiveness.

### AppearanceAsset Tint Editing
- **D-04:** Tint editing uses AppearanceAssetEditScope within an open Transaction. Pattern: Transaction.Start -> scope = new AppearanceAssetEditScope(doc, assetElementId) -> modify generic_diffuse/generic_diffuse_modifier -> scope.Commit() -> Transaction.Commit().
- **D-05:** generic_diffuse uses AssetPropertyDoubleArray4d (RGBA). Tint enable = set generic_is_metal or generic_diffuse_image_modifier. The exact property names come from the Revit SDK BuiltInAssetPropertyNames.
- **D-06:** When no AppearanceAsset exists on a material: disable tint controls, show "Teinte non disponible" label. No creation of new AppearanceAssets in Phase 4.

### Material Property Editing
- **D-07:** Name edit: Material.Name = newValue in Transaction.
- **D-08:** Description edit: Material.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION).Set(newValue) in Transaction.
- **D-09:** Surface pattern color edit: Material.SurfaceForegroundPatternColor = new Color(r,g,b) in Transaction.
- **D-10:** Each edit is a separate Transaction for undo granularity.

### 3D Pick
- **D-11:** "Ajouter par clic" button in the left panel toggles pick mode. When active, WPF window hides, Revit UI regains focus, PickObject runs via ExternalEvent.
- **D-12:** On successful pick: extract Element -> get ElementType -> create SceneTypeDto -> add to active scene -> show WPF window again.
- **D-13:** On cancel (OperationCanceledException from user pressing Escape): show WPF window again, no error.
- **D-14:** Validate active view is View3D before enabling pick button. If not 3D: button disabled with tooltip "Vue 3D requise".

### New RevitEventBridge Operations
- **D-15:** New enum values: PickElementInView, EditMaterialName, EditMaterialDescription, EditMaterialColor, EditMaterialTint, GetMaterialDetails.
- **D-16:** GetMaterialDetails returns MaterialDetailsDto: Name, Description, ColorArgb, PatternName, HasAppearanceAsset, TintEnabled, TintColorArgb, ThumbnailPath (string, nullable).
- **D-17:** PickElementInView uses UIDocument.Selection.PickObject(ObjectType.Element). Returns SceneTypeDto on success, null on cancel.

### Preview
- **D-18:** Attempt to read Material thumbnail via Material.get_Parameter or AppearanceAssetElement path. If thumbnail exists on disk: display as Image. If not: display a Rectangle filled with SurfaceForegroundPatternColor as fallback.
- **D-19:** Preview refreshes after every material edit (re-fetch MaterialDetailsDto).

### Inter-ViewModel Communication
- **D-20:** MaterialSelectedMessage carries PresetMaterialDto from RightPanelViewModel to a new MaterialEditorSection in RightPanelViewModel (or separate sub-VM).
- **D-21:** MaterialEditedMessage sent after each edit to trigger preset list refresh (name may have changed).

### Claude's Discretion
- Exact layout proportions for visualizer vs preset TreeView
- ColorPicker implementation (inline RGB TextBoxes vs WPF color dialog)
- Whether MaterialEditor is a sub-ViewModel or inline in RightPanelViewModel
- Loading indicator during 3D pick mode

</decisions>

<canonical_refs>
## Canonical References

### Existing Code to Extend
- `OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitEventBridge.cs` — Add 6 new handlers
- `OlympeMaterialManager/src/OlympeMaterialManager/Events/RevitRequestType.cs` — Add 6 enum values
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/RightPanelViewModel.cs` — Add material editor section
- `OlympeMaterialManager/src/OlympeMaterialManager/ViewModels/LeftPanelViewModel.cs` — Add pick mode toggle
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/RightPanelView.xaml` — Add visualizer zone
- `OlympeMaterialManager/src/OlympeMaterialManager/Views/LeftPanelView.xaml` — Add pick button

### Research
- `.planning/research/PITFALLS.md` — AppearanceAssetEditScope lifecycle, PickObject edge cases
- `.planning/research/FEATURES.md` — Material preview strategies

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- RevitEventBridge pattern — extend with 6 new operations
- Transaction pattern from Phase 3 handlers — reuse for material edits
- PresetMaterialDto — use for material selection context
- ArgbToColorConverter — reuse for color display
- TypeSelectedMessage/RefreshLayersMessage pattern — follow for new messages

### Integration Points
- RightPanelView.xaml — add visualizer below TreeView
- LeftPanelView.xaml — add "Ajouter par clic" button
- App.cs — may need UIApplication reference for PickObject access

</code_context>

<specifics>
## Specific Ideas

- Material editing should feel instant — no lag between typing and seeing changes
- The 3D pick should be seamless: click button, window disappears, pick in Revit, window reappears with new type added
- Tint editing is a differentiating feature — make it visually clear when tint is active vs inactive

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 04-material-editing-and-3d-pick*
*Context gathered: 2026-04-11*
