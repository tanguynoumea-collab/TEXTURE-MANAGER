# Tâches GSD — Cycle 2 (B8/B9/B10/B5) — 2026-07-28

Plan de design validé (checkpoint 1) : .zeus/DESIGN_PLAN.md — Q1 = (a) segment Réaliste visible désactivé, Q2 = (a) édition inline conservée sous « Visualisateur de matériau ».

## DoD du cycle
- [ ] `dotnet build -f net48` et `-f net8.0-windows` : 0 warning / 0 erreur (analyseurs actifs)
- [ ] `dotnet test` : 37 tests existants verts + nouveaux tests (settings mode, prédicat de recherche)
- [ ] Aucune valeur en dur : tokens du thème uniquement ; accents français ; AutomationProperties sur nouveaux contrôles
- [ ] Conformité DESIGN_PLAN §2 (emplacements, liseré 6 px, sélecteur segmenté, états non-nominaux)
- [ ] Un commit par tâche, messages `feat(cycle2): <ID> — …`

## Lot A (séquentiel 1)
- [x] B5-G : recherche panneau gauche (Filter sur ListCollectionView, debounce 200 ms, insensible accents, aucun-résultat + effacer) — commit 56a1db2
- [x] B5-D : recherche panneau droit (projection FilteredGroups — JAMAIS filtrer _collection.Groups en place, auto-expand, aucun-résultat + effacer) — commit fa756a6
- [x] B9 : renommage « Visualisateur de matériau » + bouton « Ouvrir dans Revit » (PostCommand via bridge, presse-papiers, Topmost, tooltip) — commit 1ac3af0

## Lot B (séquentiel 2)
- [x] B10-S : socle mode (enum PreviewMode, AppSettingsDto.MaterialPreviewMode string tolérant, PreviewModeChangedMessage, PreviewModeStore + tests) — commit 27e6e2b
- [x] B10-TX : infra texture (FindTexturePath restauré de f337be7^, chemins « | », racines Autodesk + Revit.ini, cache session ; TexturePath dans MaterialDetailsDto/LayerDto/MaterialParamDto/PresetMaterialDto ; ColorArgb int? nullable ; TexturePathResolver testé) — commit 71ee19b
- [x] B10-UI : sélecteur segmenté 3 positions dans l'en-tête du visualisateur (Réaliste désactivé « Disponible en phase 2 »), persistance immédiate, aperçu 60 px suivant le mode + tooltip d'échec — commit efd13ec
- [x] B8 : liseré 6 px bord gauche (CardItemStyle, couches + paramètres), SolidColorBrush/ImageBrush tuilé selon mode, null → transparent, fallback couleur ; pastilles preset suivent le mode — commit 47af7ed

## Lot DR1 (design-review itération 1)
- [x] DR1-1 : liseré matériau des deux côtés des cartes (gauche + droite) — commit 0da423a
- [x] DR1-2 : affordance presse-papiers visible sur « Ouvrir dans Revit » — commit df82aa8
- [x] DR1-3 : diagnostic du mode Texture (olympe.log) + fallback visible sans hover — commit 3adf0e5

## Lot DR2 (design-review itération 2 — refonte des modes d'aperçu)
Décision utilisateur après diagnostic terrain (olympe.log : ZÉRO texture bitmap résolvable — placeholders, chemins d'une autre machine) : mode « Texture » supprimé, « Réaliste » actif = couleur d'apparence (diffuse/albedo de l'asset). Voir DESIGN_PLAN §2.3-2.4 et §4.
- [x] DR2-1 : GetMaterialAppearanceColorArgb dans le bridge (RevitEventBridge.Appearance.cs, Generic.GenericDiffuse + balayage DoubleArray4d diffuse/albedo/color, cache par (document, assetId), diagnostic olympe.log) ; DTOs : TexturePath → AppearanceColorArgb (int?) — commit 0383c12
- [x] DR2-2 : sélecteur 2 segments Couleur │ Réaliste ; enum PreviewMode sans Texture, parse « Texture » → Realistic ; converter sans ImageBrush (apparence → fallback couleur graphique → transparent) ; indicateur « Pas d'apparence — couleur graphique » ; tests migration — commit e0c81e5
- [x] DR2-3 : suppression infra texture morte (TexturePathResolver + 12 tests, TextureBrushCache, UnifiedBitmapPath) ; grep avant/après = 0 référence ; total tests 79 → 67 — commit 5727ec4
- [x] DR2-4 : presse-papiers robuste (CLIPBRD_E_CANT_OPEN : 3 retries 100 ms, SetDataObject en dernier recours, sinon nom affiché en clair) — commit 9db7e2a

# Tâches GSD — Cycle 3 (B2/B3/B1) — 2026-07-28

Plan de design : .zeus/DESIGN_PLAN.md §3bis (delta cycle 3, enchaînement autorisé).

## DoD du cycle
- Builds séparés `dotnet build -f net48` puis `-f net8.0-windows` : 0 warning / 0 erreur
- `dotnet test` : tous verts (67 en entrée de cycle)
- Tokens du thème uniquement ; accents français ; AutomationProperties sur nouveaux contrôles

## Lot C
- [x] B2 : pipette matériau (PickElementForMaterials mono-pick sans surbrillance, matériaux du type via couches/paramètres, dédoublonnage PresetMaterialMerge testé, groupe sélectionné sinon « Autre », AutoSave + StatusMessage, Échap silencieux, fenêtre ré-affichée dans tous les chemins) — commit 336c9da
- [x] B3 : drag & drop preset → carte (cartes couches ET paramètres, DataFormat partagé PresetMaterialDto.DragDropFormat, bordure accent au survol via propriété attachée DropTargetIndicator, application mono-cible par le chemin AppliquerMateriau + OnSetMatResult, garde IsSetMatBusy, MoveMaterial du panneau droit intact) — commit df680c2
- Portes passées : net48 0/0, net8.0-windows 0/0, 74 tests verts (67 + 7 PresetMaterialMergeTests)

## Lot D
- [x] B1 : matériaux absents à l'activation d'un preset (ValidatePresetMaterials lecture seule — un seul collector, même logique id+nom que ResolveMaterial ; dialogue « Matériaux introuvables » liste scrollable + pastilles, Conserver défaut/Échap, Supprimer du preset en style discret ; purge PresetMaterialValidation sur les groupes SOURCES, groupes vides conservés, AutoSave + StatusMessage ; garde-fous : clé document+preset anti-boucle, _presetLoadFailed, aucun document → différé silencieux, erreur bridge → log seul ; import externe même nom rechargé + re-validé)
- Portes passées : net48 0/0, net8.0-windows 0/0, 82 tests verts (74 + 8 PresetMaterialValidationTests)
