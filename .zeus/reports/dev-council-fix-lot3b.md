# DEV-COUNCIL — Rapport de correction LOT 3b — Olympe MaterialManager

**Date** : 2026-07-27 · **Source** : `.zeus/reports/dev-council-1.md` (sous-ensemble LOT 3b validé par triage humain)
**Protocole** : cycle atomique, un commit par finding, portail 4 portes (build net48 PUIS net8.0-windows séparément, `dotnet test` 35/35, preuve de fermeture outillée, 0 warning maintenu ; pour PKG-05/06 : build wixproj Release + inspection des tables du MSI).
**Baseline** : départ 0 warning / 0 erreur sur les 2 TFM, 35/35 tests → **arrivée identique, analyseurs CA désormais actifs** (`AnalysisLevel latest-recommended` + `EnforceCodeStyleInBuild`).

---

## Corrigé (15 findings, 16 commits)

| # | ID | Résumé | Preuve de fermeture (porte 3) | SHA |
|---|---|---|---|---|
| 1 | MAINT-05 | 2 commentaires faux corrigés : doc de classe du bridge décrit le mécanisme réel (ConcurrentQueue drainée sur le thread Revit, callbacks via `Dispatcher.BeginInvoke` — plus de « lock » fantôme) ; doc de `HandlePickElementInView` alignée (sélection additive, pas de toggle). | Lecture du diff : les 2 commentaires correspondent au code exécuté. | `0795128` |
| 2 | MAINT-08 | Valeurs magiques centralisées : `RevitAssetProps` (`common_Tint_toggle/color` — lecture et écriture ne peuvent plus diverger), `UiLabels` (`ByCategory`/`Inconnu`/`Aucun`), `StorageFolders` (`presets`/`scenes`) dans PresetService, `ArgbUtils.PackArgb/UnpackArgb` remplace les 8 extractions bit-shift (bridge, MaterialEditorViewModel, ArgbToColorConverter). | Grep APRÈS : 0 littéral `common_Tint_*`, `< Par categorie >`, `>> 16) & 0xFF`, `"presets"`/`"scenes"` hors définitions de constantes et doc XML. | `c31f3b2` |
| 3 | MAINT-04 | `BuildLayerDto(doc, layer, index, prefix?)` extrait — les 2 clones de `HandleGetLayersForType` (chemin direct + sous-murs empilés) le réutilisent. `IsStackedWallType` : **sans objet après 3a** — le clone jumeau vivait dans `HandleGetTypeList` (supprimé) ; le site restant du pick opère sur l'instance déjà en main, pas de collector. | Grep : 3 occurrences de `BuildLayerDto` (1 déf + 2 appels), 0 duplication résiduelle du corps. | `4fe648c` |
| 4 | FIA-04 | Retour de `Raise()` testé dans `MakeRequest` : ni Accepted ni Pending → `RemoveFromQueue` (drain + re-enqueue des autres entrées) + `callback(new InvalidOperationException("Revit n'a pas accepté la requête (état : ...)"))` — les flags busy/pick se libèrent. | Lecture du diff : chemin d'échec complet (retrait + callback + LogService.Error). Builds + tests verts. | `7c13b60` |
| 5 | FIA-06 | Les 7 `ActiveUIDocument!` des handlers d'écriture remplacés par `GetActiveDocument(uiApp)` → test null → `InvalidOperationException("Aucun document actif.")` (pattern de HandleGetMaterialDetails généralisé). | Grep APRÈS : 0 `ActiveUIDocument!` dans le code (1 mention en doc XML), 7 appels `GetActiveDocument`. | `b21684e` |
| 6 | FIA-07 + FIA-08 | Échec du nettoyage des overrides verts : loggé (`LogService.Error`) + remonté à l'utilisateur via le callback (exception après le `finally` → `ErrorMessage` du VM, consigne Ctrl+Z) ; null-check `Application.Current` du pick aligné sur `ProcessSingleRequest` ; catch vide du callback de secours → `LogService.Error`. | Grep : 0 `catch { }` dans le bridge. | `e3a6cb3` |
| 7 | FIA-09 | `BuildInstancesByTypeMap(doc)` : UN collector par session de pick construisant le dictionnaire typeId→instances, réutilisé à chaque clic (l'ancien code relançait un collector complet par type cliqué). Les handlers restants sont déjà à un collector par requête mono-type. | Grep : plus de `FilteredElementCollector` dans la boucle de pick ; 1 seul dans `BuildInstancesByTypeMap`. | `c4ef925` |
| 8 | ADK-02 | Édition de teinte sur asset PBR : `FindByName` null (toggle OU couleur) → `InvalidOperationException("La teinte n'est pas modifiable sur ce type de matériau.")` au lieu du no-op silencieux ; rollback transactionnel existant + affichage/resync FIA-05 côté VM. | Lecture du diff : les 2 chemins null lèvent ; le scope AppearanceAssetEditScope est disposé sans Commit (annulation propre). | `13dc328` |
| 9 | ARC-05 | Hide/show de la fenêtre sorti du handler Revit : `WindowService.Hide/ShowMainWindow` (thread UI) appelé par `LeftPanelViewModel.AjouterParClic` avant `MakeRequest(PickElementInView)` et dans son callback. Le bridge ne touche plus jamais à la fenêtre WPF. | Grep : 0 référence à `App.MainWindow` dans `Events\` ; seul `Application.Current` restant = marshalling Dispatcher d'infrastructure. | `72839ea` |
| 10 | **ARC-02 (abaissé) + MAINT-03 — STRUCTUREL** | Bridge découpé en `partial class` par domaine, **déplacement pur, zéro changement de logique** : `RevitEventBridge.cs` (dispatch+infra, 209 l.), `.Queries.cs` (lectures, 417 l.), `.Materials.cs` (écritures transactionnelles, 337 l.), `.Pick.cs` (sélection 3D, 213 l.). | Iso-fonctionnel prouvé par builds 2 TFM + 35/35 tests + inventaire des 26 méthodes (exactement 1 occurrence chacune). **⚠ Structurel appliqué — à re-vérifier dans Revit (session réelle : requêtes, pick, éditions).** | `951d11b` |
| 11 | MAINT-10 | Sous-intentions extraites, iso-fonctionnel : `HandlePickElementInView` 180 l. → 39 l. d'orchestration (`ShowPickInstructions`/`RunPickLoop`/`CreatePickedTypeDto`/`MarkInstancesGreen`/`CleanupGreenOverrides`) avec invariant du `finally` documenté (« overrides toujours nettoyés ») ; `SupprimerSelection` → `SupprimerGroupe`/`ResoudreSortDesMateriaux`/`ChoisirGroupeCible` ; callback `FetchMaterialDetails` → `ApplyMaterialDetails` (rôle du flag `_isFetching` documenté). | Corps de `HandlePickElementInView` = 39 lignes ; builds + tests verts. | `a4dc753` |
| 12 | PKG-06 | `<Assembly>` des 4 .addin réécrit à l'installation via `util:XmlFile` (`setValue /RevitAddIns/AddIn/Assembly` → `[INSTALLFOLDER]net48\...` ou `[INSTALLFOLDER]net8.0-windows\...`, Permanent) au lieu du littéral `C:\Program Files\...`. | Table `Wix4XmlFile` du MSI Release : 4 entrées avec `[INSTALLFOLDER]`, flag 65536 (permanent). | `74c0e8f` |
| 13 | PKG-05 | Les 4 composants .addin migrent de `AppDataFolder` (per-user) vers `CommonAppDataFolder` (`%ProgramData%\Autodesk\Revit\Addins\{version}` — reconnu par Revit pour tous les utilisateurs, cohérent perMachine) ; KeyPath = fichier .addin ; `RegistryValue` HKCU supprimées ; `SuppressIces ICE64;ICE91` retirés du wixproj. | Tables du MSI : `AutodeskDir` → parent `CommonAppDataFolder` ; KeyPath des 4 composants = `Addin20xxFile` ; table `Registry` absente du MSI ; build Release 0 warning **avec validation ICE active**. | `1a16d2b` |
| 14 | PKG-07 | `addin/README.md` (3 points) : `addin\` = jeu dev du protocole de deploy manuel (conservé), `installer\...\addin\` = source du MSI ; seule `<Assembly>` diffère (vérifié par diff des 4 paires) ; `AddInId`/`FullClassName` à garder strictement synchronisés (identiques au 2026-07-27). | Diff des 2 jeux : uniquement la balise `<Assembly>` diverge. | `79432e7` |
| 15 | ARC-08 + MAINT-15 | `.editorconfig` racine figeant le style constaté (4 espaces, usings hors namespace, namespaces file-scoped, `_camelCase` privés, var) + `AnalysisLevel latest-recommended` + `EnforceCodeStyleInBuild` dans `Directory.Build.props`. Les 6 warnings CA révélés corrigés (CA1822 ×4 → static dont `PresetService.Load` + call site ; CA1852 → sealed ; CA1859 → retour typé `List<SceneTypeDto>?`). 8 règles abaissées en `suggestion` avec justification d'une ligne chacune (CA1031, CA1303, CA1305/1310/1311, CA1848, CA1707, CA2249 — cette dernière parce que `Contains(string, StringComparison)` n'existe pas sur net48). **Aucune règle désactivée en silence.** | Rebuild : 0 warning net48, 0 net8, 0 wixproj, tests 35/35 — analyseurs actifs (le « 0 warning » est désormais probant, contrairement au constat de l'audit). | `54a54e4` |
| 16 | FIA-11 (Info) | `LogService.VerboseEnabled` (défaut false) coupe les traces verbeuses par requête ; `Error` toujours écrit via un `Write` interne unique ; catch ciblés (IOException/UnauthorizedAccessException) dans le logger. | Lecture du diff : `Log` gated, `Error` inconditionnel. | `67062c8` |

## Annulé

Aucun. Aucune porte n'a échoué. (Incident mineur sans revert : les 6 warnings CA révélés par l'activation des analyseurs au finding 15 ont été corrigés dans le même diff, conformément à la contrainte de porte du triage.)

## Reportés (décision du triage humain, pas de code)

- **SEC-02** (signature Authenticode) : nécessite l'achat d'un certificat de signature de code — décision utilisateur, hors périmètre correcteur.
- **PKG-09** (archivage des MSI livrés par tag/release, fr-FR seul) : protocole de release — à traiter en phase PUBLICATION du pipeline.

## En attente de décision

Rien : le triage humain avait validé l'ensemble du lot ; le seul point structurel (ARC-02/MAINT-03) a été appliqué en commit isolé et signalé « à re-vérifier dans Revit ».

## Notes de non-régression

- Portail 4 portes passé sur les 16 commits : build `-f net48` PUIS `-f net8.0-windows` (jamais ensemble, MAINT-13), 35/35 tests constants, 0 warning constant — et durci en fin de lot par l'activation des analyseurs.
- MSI reconstruit et inspecté (tables Directory/Component/File/Wix4XmlFile) après chaque finding packaging ; l'installation réelle sur machine vierge reste le critère final (comme pour PKG-01, hors portée en lecture seule).
- Changements de comportement assumés et documentés : Denied/TimedOut de `Raise()` produit désormais une erreur visible (FIA-04) ; teinte sur PBR produit un message au lieu d'un silence (ADK-02) ; échec de nettoyage du pick produit un message + perte de la sélection en cours (FIA-07, cas rarissime) ; les .addin s'installent pour tous les utilisateurs de la machine (PKG-05).

## Résumé chiffré

- **15 findings fermés** (16 commits : 15 fix + 1 refactor structurel) + **2 reportés** (SEC-02, PKG-09) = **17/17 traités**.
- Bridge : 1454 l. (audit) → 4 fichiers de 209/417/337/213 l. ; plus aucune méthode > 40 l. dans le pick.
- Warnings : 0 → 0, mais avec `latest-recommended` + style build désormais actifs (6 CA corrigés, 8 règles justifiées en suggestion).
- MSI : .addin perMachine dans ProgramData, chemin `<Assembly>` dynamique, 0 suppression ICE, 0 clé HKCU.
- Tests : 35/35 constants sur tout le lot.
