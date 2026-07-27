# DEV-COUNCIL — Rapport de correction LOT 3a — Olympe MaterialManager

**Date** : 2026-07-27 · **Source** : `.zeus/reports/dev-council-1.md` (Couches 2 et 3, sous-ensemble LOT 3 validé par triage humain)
**Protocole** : cycle atomique, un commit par finding, portail 4 portes (build net48 PUIS net8.0-windows séparément, `dotnet test`, preuve de fermeture par grep/relance d'outil, zéro nouveau warning).
**Baseline warnings** : départ 3 nullable uniques net48 / 0 net8 → **arrivée 0 / 0** (exigence du finding MAINT-11 atteinte).
**Suite de tests** : départ 8/8 → **arrivée 35/35** (27 tests ajoutés).

---

## Corrigé (14 findings, 13 commits)

| ID | Résumé | Preuve de fermeture (porte 3) | SHA |
|---|---|---|---|
| MAINT-01 + PRT-09 | Chaîne preview sphère morte supprimée sur 3 couches : `Controls\MaterialSpherePreview.cs`, 9 propriétés VM jamais bindées + `RequestRevitRender`/`LoadRenderPreview`, handlers bridge `HandleRenderMaterialPreview`/`CreateSphereDirectShape`/`CreateIsolatedView`/`FindTexturePath`, membre d'enum `RenderMaterialPreview` + case, champs DTO `ThumbnailPath`/`TexturePath`/`Transparency`/`Shininess`/`Smoothness`, xmlns `controls` orphelin de RightPanelView. **Solde définitivement FIA-10 et SEC-05** (invalidés, vivaient dans ce code mort). | Grep AVANT : 0 binding dans `Views\`, 0 usage `controls:`. Grep APRÈS : 0 occurrence de `Sphere\|RenderMaterialPreview\|FindTexturePath\|ThumbnailPath\|TexturePath` hors bin/obj. Builds verts par TFM. | `f337be7` |
| MAINT-12 | Orphelins supprimés : `GetStoredPresetPath`/`StorePresetPath`/`Save(collection, path)` de PresetService, requête `GetDocumentInfo` (enum + case + handler), `Models\RevitDocInfoDto.cs`, `AppSettingsDto.ScenesFilePath`. **`Load(path)` et `PresetFilePath` conservés** : encore consommés par la migration legacy (`RightPanelViewModel` l.468-471, vérifié par grep). | Grep AVANT : 0 appelant pour chaque symbole supprimé. Grep APRÈS : 0 occurrence. | `b9b7358` |
| PRT-02 | Chaîne ComboBox fossile supprimée : `AjouterType`, propriétés Families/SelectedFamily/FamilyTypes/SelectedFamilyType/IsLoadingFamilies/IsLoadingTypes, `OnSelectedFamilyChanged`/`OnSelectedFamilyTypeChanged`, appel `ChargerFamillesCommand.Execute` à chaque changement de scène (aller-retour Revit inutile), et côté bridge `HandleGetFamilyList`/`HandleGetTypeList` + enum + DTOs `FamilyCategoryDto`/`GetTypeListRequestDto`. | Grep AVANT : 0 binding XAML sur toute la chaîne, `GetFamilyList`/`GetTypeList` sans autre consommateur, handlers autonomes (aucun helper partagé). `GetLayersForType` et pick 3D (`PickElementInView`) intacts — builds + tests verts. | `4a6f59d` |
| PRT-08 | `Converters\BoolToVisibilityConverter.cs` supprimé (sémantique identique au natif), remplacé par `BooleanToVisibilityConverter` WPF dans CenterPanelView et RightPanelView (aligné sur LeftPanelView), xmlns `converters` retiré de CenterPanelView. | Grep APRÈS : 0 occurrence de `BoolToVisibilityConverter`. Builds XAML verts par TFM. | `9923411` |
| SEC-01 (absorbe SEC-03/04, TST-05, DON-07) | Point de vérité unique dans PresetService : `ValidateFileName` public (vide, `Path.GetInvalidFileNameChars`, noms réservés CON/PRN/AUX/NUL/COM1-9/LPT1-9) + `GetSafeFilePath` privé avec garde anti-traversal (`Path.GetFullPath` sous le dossier attendu, sinon `ArgumentException` française), appliqué aux **6 sites** Save/Load/Delete preset et scène. Import externe : `scene.Name` forcé au nom de fichier (plus de Name injecté par le JSON). CreateNameDialog valide via le même helper. | Grep : les 6 sites passent par `GetSafeFilePath` (l.324, 345, 442, 463, 473, 484), 0 `Path.Combine(Get*Directory(), name...)` restant. Tests xunit dédiés verts (voir TST-07). | `e7e7c40` |
| DON-09 (absorbe FIA-12) | `ChargerPresetExterne` et `ChargerSceneExterne` : JSON désérialisé/validé AVANT `File.Copy` (`IsValidPresetJson`/`IsValidSceneJson` sur les mêmes options que la persistance ; invalide → message français + abandon) ; collision de nom → `DialogService.Confirm` avant écrasement. | Code en place dans les deux VMs ; helpers testés par xunit (JSON tronqué/non-JSON → false). | `006171c` |
| TST-06 | Garde dans `MigrateProjectDirectory` : chemins normalisés (`GetFullPath`, OrdinalIgnoreCase, séparateur final), identique → no-op, imbriqué dans l'ancien → `ArgumentException` française **avant** toute création de dossier (empêche la copie récursive auto-réplicante). Remonte via le try/catch existant de `MigrerRepertoire` (ShowError). | Test xunit : nested → throw + aucun dossier créé ; identique avec séparateur final → no-op. | `b09df33` |
| DON-05 (absorbe ADK-01) | `ElementIdHelper.FromValue` branche net48 : cast `(int)` silencieux remplacé par garde hors plage int → `ElementId.InvalidElementId` ; les consommateurs échouent proprement via la validation ResolveMaterial du LOT 2 (`GetElement` null → refus explicite). | Build net48 (la branche `#if REVIT2023_OR_2024` est compilée par cette target) vert, 0 warning. | `8430922` |
| DON-03 | `SchemaVersion { get; set; } = 1` sur les 3 DTOs racine persistés (`PresetCollectionDto`, `SceneDto`, `AppSettingsDto`). Pas de logique de migration (fichiers v0 sans champ → défaut 1, documenté en commentaire). | Tests xunit : `schemaVersion: 1` présent dans le JSON écrit, round-trip OK, fichier v0 → défaut 1. | `b05389e` |
| MAINT-06 | Doc XML réalignée : `SupprimerPreset` documenté comme action destructive avec confirmation (portait le summary de création), `CreerPreset` récupère son propre summary. | Lecture directe du diff. | `7d09b75` |
| MAINT-09 | `FindGroupContaining` dédupliqué : méthode du VM passée `internal`, le code-behind drag-and-drop de `RightPanelView.xaml.cs` la réutilise, copie locale supprimée. | Grep : 1 seule occurrence restante dans le code-behind (l'appel), 1 définition dans le VM. | `7c6711c` |
| MAINT-16 | **Résolu structurellement par PRT-02, sans diff.** Vérification faite : l'unique chemin d'ajout restant (`AjouterParClic`, seul site `Types.Add`) appelle `SetupCustomSort()`, et toute affectation d'`ActiveScene` passe par `OnActiveSceneChanged` → `SetupCustomSort()`. Le chemin non couvert était `AjouterType`, supprimé. | Grep : sites `Types.Add`/`ActiveScene =` tous couverts (l.291→304, l.379). | — (via `4a6f59d`) |
| MAINT-11 | Dernier warning nullable net48 traité (CS8604 `SupprimerPreset` : `!` justifié par commentaire — `IsNullOrEmpty` sans `[NotNullWhen]` sur net48). Les 2 autres warnings de la baseline étaient tombés naturellement avec MAINT-01 (`LoadRenderPreview` supprimé) et SEC-01 (validation CreateNameDialog). | Rebuild complet : **0 warning net48, 0 warning net8** — nouvelle baseline exigée pour la suite. | `2aa0cad` |
| DON-06 (abaissé) | Retry best-effort sur `IOException` transitoire dans `LoadSettings`/`SaveSettings` : 3 tentatives, backoff 100/200 ms ; un verrou passager n'envoie plus settings.json en quarantaine. Pas de mutex nommé (jugé disproportionné par le sceptique). | Build + tests verts ; helper `RetryOnIOException` en place sur les deux chemins. | `fe442b7` |
| TST-07 (opportuniste) | 27 tests xunit ajoutés (`SanitizationAndSchemaTests.cs`) : noms invalides/réservés/traversal via SavePreset/DeleteScene, `ValidateFileName` valide/invalide, garde de migration imbriquée + no-op identique, SchemaVersion round-trip + défaut v0, `IsValidPresetJson`/`IsValidSceneJson`. Suite **35/35 verte**. | `dotnet test` : 35 réussis, 0 échec. | `28ee7c9` |

## Annulé

Aucun. Aucune porte n'a échoué de manière définitive ; le seul incident (nouveau warning CS8602 introduit par le code SEC-01 lui-même sur net48) a été corrigé dans le même diff avant commit.

## En attente de décision

Rien dans ce lot (le LOT 3a ne contenait que du mécanique validé au triage humain). Restent hors périmètre, à planifier :
- LOT 3 restant : ARC-02 (découpage bridge en partial + dédup BuildLayerDto), MAINT-04/05/08/10, FIA-04/06/07/08/09, ADK-02, PKG-05/06/07, ARC-08/MAINT-15 (analyseurs + .editorconfig), PKG-09, SEC-02 (signature).
- PRT-05 (migrations legacy peut-être vides) : question produit pour l'utilisateur.

## Notes de non-régression

- Chaque suppression de code mort a été précédée d'un grep exhaustif prouvant 0 référence (bindings XAML compris) ; aucune référence inattendue rencontrée.
- `Load(path)` + `PresetFilePath` volontairement conservés (migration legacy vivante).
- Les 4 portes ont été passées sur les 13 commits : build net48 puis net8.0-windows (jamais ensemble, MAINT-13), 8 puis 35 tests verts, warnings 3→2→1→0 uniques net48 (jamais de hausse), 0 constant sur net8.
