# Phase 2: Read Path -- Scene and Layer Display - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.

**Date:** 2026-04-11
**Phase:** 02-read-path-scene-and-layer-display
**Areas discussed:** Modele Scene, TreeView hierarchie, Affichage couches, Parametres familles
**Mode:** Auto (all recommended defaults selected)

---

## Modele Scene

| Option | Description | Selected |
|--------|-------------|----------|
| ObservableCollection<SceneTypeDto> avec SceneDto | DTO pur, MVVM compatible | auto |
| Dictionary<string, List<ElementId>> | Plus simple mais pas observable | |

**User's choice:** ObservableCollection<SceneTypeDto> avec SceneDto wrapper
**Notes:** Coherent avec le pattern DTO-only des ViewModels (D-10 Phase 1)

---

## TreeView hierarchie

| Option | Description | Selected |
|--------|-------------|----------|
| CollectionViewSource avec GroupDescription | Pattern WPF standard | auto |
| TreeView hierarchique nested | Plus de controle mais plus complexe | |

**User's choice:** CollectionViewSource avec GroupDescription par categorie
**Notes:** Tri custom Murs/Sols first via IComparer

---

## Affichage couches

| Option | Description | Selected |
|--------|-------------|----------|
| ListBox avec DataTemplate | Selectionnable, MVVM friendly | auto |
| DataGrid | Plus structure mais overkill | |

**User's choice:** ListBox avec DataTemplate "[Fonction] - [Epaisseur mm] - [Materiau]"
**Notes:** SelectionMode=Extended pour multi-selection

---

## Parametres familles

| Option | Description | Selected |
|--------|-------------|----------|
| Element.Parameters iteration + StorageType filter | Approche API standard | auto |
| ParameterFilterElement | Plus performant mais plus complexe | |

**User's choice:** Element.Parameters iteration, filtre StorageType.ElementId pointant Material
**Notes:** Seule approche fiable pour decouvrir tous les parametres Material

---

## Claude's Discretion

- Cache strategy, error display, loading indicators
- Nullable DTO properties

## Deferred Ideas

None
