# DEV-COUNCIL-FIX — Rapport de correction LOT 2 — Olympe MaterialManager

**Date** : 2026-07-27 · **Source** : `.zeus/reports/dev-council-1.md` (Couche 2 — LOT 2)
**Protocole** : cycle atomique (1 finding = 1 commit), portail 4 portes par finding
(build net48 PUIS net8.0-windows, `dotnet test` 8/8, preuve de fermeture, warnings = baseline 3 nullable net48 / 0 net8).
**Triage humain** : validé en amont — tout le lot part en correction.

---

## Corrigé (5/5)

| ID | Résumé | Preuve de fermeture (porte 3) | Commit |
|---|---|---|---|
| **FIA-03** (Majeur) | Toutes les écritures fichier déclenchées depuis l'UI protégées : try/catch + `LogService.Error` + message d'état visible dans `AutoSave` (RightPanel → `StatusMessage`), `AutoSaveScenes` (LeftPanel → `ErrorMessage`), `OnActivePresetNameChanged`/SaveSettings (→ `StatusMessage`). Dernier filet `Dispatcher.UnhandledException` enregistré à la création de la MainWindow (ShowWindowCommand) : log + MessageBox français + `e.Handled = true` — le process Revit hôte ne peut plus tomber sur une exception WPF non gérée. S'intègre aux flags DON-02 du LOT 1 (`_presetLoadFailed`/`_scenesLoadFailed` vérifiés avant le try/catch). | Inspection lentille fiabilité : les 3 sites d'écriture UI sont chacun sous try/catch avec signalement, le handler `UnhandledException` est enregistré une seule fois à la création de la fenêtre. Builds + tests verts. | `2ad631e` |
| **FIA-05** (Majeur) | `MaterialEditorViewModel.OnEditResult` : sur `result is Exception ex`, l'échec n'est plus avalé — log + message d'erreur français + `FetchMaterialDetails()` pour resynchroniser les champs avec l'état réel du matériau Revit (la transaction côté bridge a rollback). | Le `if (result is Exception) return;` silencieux n'existe plus ; le chemin d'échec affiche le message et relance le fetch (resynchronisation UI↔modèle). Builds + tests verts. | `f739377` |
| **DON-04** (Majeur) | `SetMatRequestDto`/`SetMatParamRequestDto` transportent `MaterialName` (rempli par MainWindowViewModel depuis le preset sélectionné). Nouveau helper `ResolveMaterial(doc, idValue, name)` dans RevitEventBridge : `doc.GetElement(matId) is Material` + comparaison exacte du nom ; en écart, re-résolution par nom via `FilteredElementCollector.OfClass(typeof(Material))` ; introuvable → `InvalidOperationException` « Le matériau '<nom>' n'existe pas dans ce document » (rollback propre existant). Utilisé par `HandleSetMaterialOnLayers` et `HandleSetMaterialOnParameter`. | Grep : les 2 handlers Set Mat (l.662, l.695) passent par `ResolveMaterial` — plus aucun `FromValue(request.MaterialIdValue)` brut sur le chemin d'application preset→document. Un id en collision inter-documents ne peut plus appliquer silencieusement un mauvais matériau. Builds + tests verts. | `fbe5edc` |
| **MAINT-02** (Majeur, absorbe ARC-09/PKG-08/PRT-07/ADK-04) | CLAUDE.md racine réaligné sur le réel : Contexte/Stack → Revit 2023-2026, C# 12 multi-target net48+net8.0-windows, tableau TFM→package Nice3point→symbole (`2023.1.80`/`REVIT2023_OR_2024`, `2025.0.2`/`REVIT2025_OR_GREATER`), WiX v5 (WixToolset.Sdk 5.0.2) et MSI (plus de v4/.exe) ; « Structure solution » → mono-projet `src\OlympeMaterialManager` + `tests\` + `installer\` + `Directory.Build.props` version unique ; section « Build (obligatoire) » : TOUJOURS par TFM, jamais simultané (CS2001) — ferme MAINT-13 côté doc ; ligne Conventions : codes D-xx/SCENE-xx/MATEDIT-xx → `.planning/phases/` — ferme MAINT-14. Sections GSD/Workflow/Profil intactes. | Relecture du diff : chaque affirmation du doc vérifiée contre le csproj réel (TFMs, versions de packages, symboles), le wixproj (`WixToolset.Sdk/5.0.2`) et l'arborescence (`.planning/phases/` existe). Builds + tests verts (doc-only). | `b4521e3` |
| **ARC-01** (Majeur, opportuniste — arbitrage) | `DialogService` étendu : `Confirm(message, titre)` (YesNo), `ConfirmWithCancel` (YesNoCancel → `bool?`), `ShowError`, `ShowInfo`. Remplacement des `System.Windows.MessageBox.Show` directs UNIQUEMENT dans les fichiers touchés par ce lot : RightPanelViewModel (SupprimerPreset, SupprimerSelection), LeftPanelViewModel (SupprimerScene), MaterialEditorViewModel (OnEditResult), MainWindowViewModel (MigrerRepertoire, OnSetMatResult). Dialogues de saisie (CreateNameDialog, ChooseGroupDialog, AddMaterialDialog) et VMs non modifiés : intacts — le reste attend la bêta. | Grep : `MessageBox.Show` = 0 occurrence dans `ViewModels\` (ShowWindowCommand, hors périmètre, conserve les siens). Builds + tests verts, comportement des dialogues inchangé (mêmes boutons/icônes via le service). | `271c97b` |

---

## Annulé

Aucun. Les 4 portes sont passées au premier essai pour les 5 findings.

---

## En attente de décision

Aucun élément de ce lot — le triage humain avait validé l'application de tout le LOT 2 en amont.
Restent hors périmètre, à planifier : **LOT 3** (mineurs groupés par module, cf. rapport d'audit) et l'extension **TST-01/TST-07** (quick wins de tests sur comparateur/converters/LayerFunctionMapper, à saisir en mode opportuniste).

---

## Résumé chiffré

- **5 findings traités, 5 appliqués, 0 annulé** (FIA-03, FIA-05, DON-04, MAINT-02, ARC-01) — soldent aussi par absorption : DON-08, ARC-09, PKG-08, PRT-07, ADK-04, MAINT-13 (doc), MAINT-14, MAINT-17.
- **5 commits** : `2ad631e`, `f739377`, `fbe5edc`, `b4521e3`, `271c97b`.
- **Portail** : 5×4 portes vertes — net48 : 3 warnings nullable (baseline, aucun nouveau) ; net8.0-windows : 0 warning ; tests : 8/8 verts à chaque passage.
- **Bilan cumulé LOT 1 + LOT 2** : 12 findings corrigés, 2 Bloquants + 9 Majeurs (sur 11) fermés ; Majeurs restants : TST-01 (amorcé, extension continue), PKG-01/PKG-02 validation finale sur machine vierge (hors outillage).
