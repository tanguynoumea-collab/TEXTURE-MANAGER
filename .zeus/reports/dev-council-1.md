# DEV-COUNCIL — Rapport d'audit complet n°1 — Olympe MaterialManager (Alpha 1.3)

**Date** : 2026-07-27 · **Pipeline** : ZEUS cycle d'audit n°1 · **Mode** : audit complet (tous tiers + spécialiste autodesk)
**Roster** : architecte, mainteneur, tests, fiabilité, sécurité, données, packaging, pertinence + autodesk (RevitAPI détecté). Filtre sceptique + cross-challenges par paires + arbitrage council (3 juges aveugles).
**Vérité-terrain** : builds réels sur les 2 TFM ; sondes de compilation contre RevitAPI 2023.1.80 / 2024.3.30 / 2025.0.2 / 2026.4.0 (les 4 surfaces API vérifiées par compilateur) ; table File du MSI extraite ; signatures Authenticode vérifiées ; grep croisés exhaustifs.

---

## Couche 1 — Verdict et résumé exécutif

**Verdict global** : fondations saines (étanchéité UI↔Revit API exemplaire, discipline multi-target remarquable, transactions Revit toutes protégées par rollback, pattern ExternalEvent thread-correct) mais **deux bloquants confirmés** qui touchent le cœur du produit : la **persistance peut détruire silencieusement les presets de l'utilisateur** (DON-02) et **le MSI livre un add-in mort pour Revit 2025/2026** (PKG-01). Le thème transversal côté code : *tout est concret et global* (zéro interface, statiques, dialogues en dur) — arbitré : remédiation phasée, pas de chantier DI en alpha.

**84 findings bruts → après filtre sceptique et fusions : 45 findings canoniques**
(2 invalidés, 21 absorbés comme doublons, 11 sévérités abaissées, 1 arbitrage résolu à l'unanimité)

| Sévérité | Nombre | IDs canoniques |
|---|---|---|
| **Bloquant** | 2 | DON-02, PKG-01 |
| **Majeur** | 11 | FIA-01, ARC-01, ARC-03*, MAINT-02, TST-01, FIA-03, FIA-05, DON-01, DON-04, PKG-02, PKG-03 |
| **Mineur** | 25 | ARC-02, ARC-04, ARC-05, ARC-06, ARC-07, MAINT-01, MAINT-04→06, MAINT-08→13, TST-06, FIA-04, FIA-06→09, SEC-01, SEC-02, DON-03, DON-05, DON-06, DON-09, PKG-05→07, PRT-02, ADK-02 |
| **Info** | 12 | ARC-08, MAINT-07, MAINT-14→16, TST-07, FIA-11, PKG-09, PRT-05, PRT-08, PRT-09, ADK-03 (positif), SEC-06 (positif) |

\* ARC-03 : arbitré (voir § Arbitrage).

**Convergences indépendantes fortes** (crédibilité maximale) :
- La chaîne de perte de presets relevée par 3 agents séparément (données, tests, fiabilité).
- Le code mort « preview sphère » relevé par 3 agents (pertinence, mainteneur, + invalidation de 2 findings qui vivaient dedans).
- CLAUDE.md mensonger relevé par 5 agents (architecte, mainteneur, packaging, pertinence, autodesk).

**Points forts prouvés** (à préserver) : aucun `using Autodesk` hors App/Commands/Events/Helpers — les VMs ne parlent qu'en DTOs ; `#if` confiné à 2 fichiers d'abstraction ; rollback transactionnel systématique et propre dans tous les handlers d'écriture ; marshalling UI `Dispatcher.BeginInvoke` correct ; aucun secret, désérialisation à types fermés ; code 2026 « CompoundStructure sans noyau » déjà conforme (ADK-03) ; API ForgeTypeId moderne partout.

**Points non vérifiés faute d'outil** : installation/désinstallation réelle du MSI (lecture seule) ; comportement runtime dans Revit (Raise() Denied, exceptions WPF non gérées sous Revit) ; analyseurs Roslyn CA jamais exécutés (non activés dans le csproj — le « 0 warning » du build n'est pas probant) ; couverture de tests sans objet (aucun test n'existe) ; scénarios OneDrive/verrous réels jugés sur code, pas reproduits.

---

## Arbitrage council (ARC-03 / TST-03 — « interfaces maintenant ou en bêta ? »)

3 juges indépendants en aveugle, **verdict unanime « B amendé »** :
1. Corriger les bloquants et majeurs **maintenant**, shipper l'alpha suivante.
2. **Une seule couture immédiate** : extraire la persistance presets/scènes en service injectable (répertoire en paramètre de constructeur, pas de framework DI) et créer LE premier projet de tests (5-8 tests : JSON corrompu/vide/tronqué, écriture atomique, migration) — posée **pendant** le fix DON-02, parce que c'est le seul endroit où un test protège immédiatement des données utilisateur.
3. Le correctif MSI (PKG-01) se valide par install propre sur machine vierge, pas par test unitaire.
4. IDialogService, façade typée du bridge, IMessenger injecté : **bêta, en mode opportuniste** (au moment où un correctif traverse un VM, jamais en campagne dédiée).

---

## Couche 2 — Plan de remédiation priorisé

### LOT 1 — Avant toute prochaine livraison (bloquants + amplificateurs directs)

**A. Persistance : stopper la perte de données** — `Services\PresetService.cs`
1. **DON-02 (Bloquant)** : distinguer « fichier absent » (défaut OK) de « fichier illisible » → renommer en `.json.corrupt-<timestamp>`, avertir l'utilisateur, **bloquer l'AutoSave** tant qu'un chargement n'a pas réussi. (Absorbe TST-02, FIA-02.)
2. **DON-01 (Majeur)** : helper unique `WriteJsonAtomic(path, obj)` = temp + `File.Replace` (conserve un `.bak` gratuit) — couvre les 5 sites `File.WriteAllText`.
3. **Arbitrage** : au passage, extraire la persistance en classe injectable + projet `OlympeMaterialManager.Tests` (xunit, net8.0-windows) avec 5-8 tests sur ces chemins exacts.
4. **FIA-01 (Majeur)** : valider l'accessibilité du répertoire projet dans `ShowWindowCommand` avant construction des VMs ; en cas d'échec, proposer la re-sélection (réutiliser le flux premier lancement) au lieu de propager l'exception.

**B. Packaging : livrer un MSI qui fonctionne** — `csproj` + `installer\`
5. **PKG-01 (Bloquant)** : `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` + exclure `publish\**` du staging. Validation : install sur machine vierge + lancement Revit 2025.
6. **PKG-02 (Majeur)** : purger `staging\` en début de cible `StageForInstaller` (`RemoveDir`) + dépendance de build wixproj→csproj (la DLL fantôme `Nice3point.Revit.Toolkit.dll` actuellement dans le MSI disparaît).
7. **PKG-03 (Majeur)** : source de version unique (`Directory.Build.props` → csproj + `-d Version` WiX), alignée sur la nomenclature git ; incrémenter à chaque livraison.

### LOT 2 — Majeurs restants (correctifs ciblés, sans refactor)

8. **FIA-03** : try/catch + `StatusMessage` autour de toutes les écritures appelées depuis l'UI (`AutoSave`, `AutoSaveScenes`, `OnActivePresetNameChanged`) + `Dispatcher.UnhandledException` en dernier filet. (Absorbe DON-08.)
9. **FIA-05** : dans `MaterialEditorViewModel.OnEditResult`, sur `Exception` → message utilisateur + `FetchMaterialDetails()` pour resynchroniser l'UI. 
10. **DON-04** : avant `SetMaterialId`/`param.Set`, valider `doc.GetElement(matId) is Material m` **et** comparer `m.Name` au `MaterialName` persisté ; en écart → re-résolution par nom ou refus explicite. (Le nom devient la clé logique, l'id un cache — prépare la portabilité des presets inter-projets.)
11. **MAINT-02** : réécrire les sections Structure/Stack/Versions de CLAUDE.md : mono-projet multi-target, périmètre Revit 2023-2026, mapping TFM→package→symbole, WiX v5, contournement build par TFM (MAINT-13), pointeur vers `.planning/` pour les codes D-xx (MAINT-14). (Absorbe ARC-09, PKG-08, PRT-07, ADK-04.)
12. **ARC-01** : opportuniste (règle de l'arbitrage) — étendre `DialogService` (Confirm, PromptName, PickColor) et l'utiliser dans les VMs retouchés par les correctifs ci-dessus. (Absorbe MAINT-17.)
13. **TST-01** : amorcé mécaniquement par le point 3 ; étendre aux quick wins TST-07 (comparateur, converters, LayerFunctionMapper) quand l'occasion se présente.

### LOT 3 — Mineurs groupés par module (à planifier librement)

- **Code mort** (gain immédiat, zéro risque) : chaîne sphère complète 3 couches (MAINT-01←PRT-01, ~600 l.), orphelins PresetService/enum (MAINT-12←PRT-03/04), appel `ChargerFamilles` fossile à chaque changement de scène + chaîne ComboBox (PRT-02), xmlns orphelin (PRT-09). Note : FIA-10 et SEC-05 invalidés car vivant dans ce code mort — sa suppression les solde définitivement.
- **Sanitisation unifiée** (SEC-01←SEC-03/04, TST-05, DON-07) : un unique `GetSafeFilePath(kind, name)` dans la couche persistance (6 sites `Path.Combine` recensés) + forcer `scene.Name = nomFichierAssaini` à l'import + validation du JSON **avant** `File.Copy` et confirmation d'écrasement homonyme (DON-09←FIA-12) + garde `newPath` hors de `oldPath` dans la migration (TST-06).
- **RevitEventBridge** : découpage en `partial` par domaine + extraction `BuildLayerDto`/`IsStackedWallType` dédupliqués (ARC-02←MAINT-03/TST-04, MAINT-04) ; corriger les 2 commentaires faux (MAINT-05) ; remplacer `ActiveUIDocument!` par un test null + message français (FIA-06) ; tester le retour de `Raise()` (FIA-04) ; logger l'échec du nettoyage des overrides verts (FIA-07) + catch vide l.166 (FIA-08) ; un seul collector par requête au lieu d'un par type (FIA-09) ; constantes pour `common_Tint_*`, libellés, dossiers (MAINT-08) ; feedback si `FindByName` null sur assets PBR (ADK-02).
- **ElementId multi-version** : `checked` ou garde `> int.MaxValue → InvalidElementId` dans `ElementIdHelper.FromValue` (DON-05←ADK-01).
- **Installeur** : `.addin` vers `CommonAppDataFolder` pour un scope perMachine (PKG-05←SEC-07) ; réécriture du chemin `<Assembly>` via util:XmlFile à l'install (PKG-06) ; unifier les deux jeux de manifests (PKG-07←PRT-06).
- **Divers VM/vues** : doc XML décalée (MAINT-06), duplication `FindGroupContaining` (MAINT-09), 4 méthodes longues (MAINT-10), 6 warnings nullable (MAINT-11), `SetupCustomSort` sur un seul chemin (MAINT-16), concurrence settings best-effort (DON-06, abaissé), `SchemaVersion` dans les DTOs racine (DON-03), signature de code avant élargissement de la diffusion (SEC-02←PKG-04).
- **Outillage** : `.editorconfig` + `AnalysisLevel latest-recommended` (ARC-08, MAINT-15) ; archiver les MSI livrés par tag/release (PKG-09).

---

## Couche 3 — Findings détaillés

Les rapports complets par agent (preuves, extraits, sorties d'outils) sont dans les transcripts du council. Synthèse par finding canonique — statut de challenge visible :

### Bloquants
- **DON-02** — Fichier JSON corrompu/verrouillé silencieusement remplacé par la collection par défaut, puis écrasé par l'AutoSave. `PresetService.cs:247-250` (catch-all), `RightPanelViewModel.cs:501-506`. Même mécanique pour les scènes. **Statut : validé par sceptique, confirmé par 3 agents indépendants.** Absorbe TST-02, FIA-02.
- **PKG-01** — Le MSI installe l'add-in net8 sans `CommunityToolkit.Mvvm` ni `Microsoft.Xaml.Behaviors` (prouvé par la table File du MSI + `deps.json` ; cause : `dotnet build` ne copie pas les dépendances NuGet en net8, `StageForInstaller` hérite du trou). Add-in mort au chargement sur Revit 2025/2026. **Statut : validé (preuve outillée incontestée).**

### Majeurs
- **FIA-01** — Répertoire projet inaccessible (OneDrive/réseau) → exception dans le constructeur VM via `ShowWindowCommand` → fenêtre impossible à ouvrir, aucune récupération in-app. **Abaissé Bloquant→Majeur** (condition environnementale, réparation manuelle possible). Cause racine : I/O dans les constructeurs (ARC-06).
- **ARC-01** — VMs instancient Views/MessageBox/ColorDialog WinForms (violation du « MVVM strict » du CLAUDE.md, 9 sites). **Validé.** Absorbe MAINT-17.
- **ARC-03** — Zéro interface, `new PresetService()` dans les VMs, messenger singleton. **Arbitré à l'unanimité : constat exact, remédiation phasée** (couture presets maintenant, reste en bêta opportuniste). Absorbe TST-03.
- **MAINT-02** — CLAUDE.md décrit une solution fictive (Shared+3 projets, versions 2024-2026, WiX v4) vs réel (mono-projet multi-target, 2023-2026, WiX v5). Le doc le plus lu du repo induit chaque session en erreur. **Validé, relevé par 5 agents.** Absorbe ARC-09, PKG-08, PRT-07, ADK-04.
- **TST-01** — Aucun projet de tests, 0 % de couverture de facto sur ~4 200 l. dont transactions destructives et persistance. **Validé.**
- **FIA-03** — Écritures fichiers sans try/catch déclenchées par setters bindés sur le thread UI du process Revit ; pas de `Dispatcher.UnhandledException`. Risque de crash de l'hôte Revit. **Validé.** Absorbe DON-08.
- **FIA-05** — Échec d'édition matériau avalé (`OnEditResult` : `if (result is Exception) return;`) → UI désynchronisée du modèle sans aucun signal. **Validé.**
- **DON-01** — Écritures par écrasement direct non atomiques (5 sites), amplificateur direct de DON-02. **Validé.**
- **DON-04** — ElementId persistés appliqués sans validation : sur un autre document, collision d'id → **mauvais matériau commité silencieusement** (mécanisme confirmé au cross-challenge fiabilité ; l'échec franc, lui, rollback proprement). **Validé.**
- **PKG-02** — Staging jamais purgé + aucune dépendance de build wixproj→csproj : DLL fantôme `Nice3point.Revit.Toolkit.dll` **prouvée dans le MSI actuel**. Reproductibilité nulle. **Validé.**
- **PKG-03** — Trois versions divergentes (MSI 1.0.0.0 figé, assembly 2.0.0, git Alpha 1.3) → `MajorUpgrade` inopérant, installations côte à côte chez les testeurs. **Validé.**

### Mineurs (canoniques, après abaissements/fusions)
ARC-02 (bridge 1454 l. : taille/duplication — abaissé, le pattern enum+ExternalEvent lui-même est canonique ; absorbe MAINT-03, TST-04) · ARC-04 (statiques App↔Bridge — abaissé : idiome add-in courant, coût déjà porté par ARC-03) · ARC-05 (bridge cache la fenêtre WPF) · ARC-06 (PresetService double nature, I/O dans ctors — cause racine de FIA-01) · ARC-07 (DTOs triple usage ; absorbe DON-10) · MAINT-01 (chaîne sphère morte ~600 l. — abaissé : zéro impact utilisateur ; absorbe PRT-01) · MAINT-04 (duplications BuildLayerDto/stacked-wall) · MAINT-05 (2 commentaires faux : « lock » inexistant, toggle contredit) · MAINT-06 (doc XML décalée sur une action destructive) · MAINT-08 (valeurs magiques : `common_Tint_*` divergeables lecture/écriture, dossiers, ARGB ×8) · MAINT-09 (FindGroupContaining dupliquée) · MAINT-10 (4 méthodes de 70-132 l.) · MAINT-11 (6 warnings nullable net48) · MAINT-12 (orphelins : GetStoredPresetPath, StorePresetPath, Save(path), GetDocumentInfo ; absorbe PRT-03/04) · MAINT-13 (`dotnet build` multi-TFM simultané échoue CS2001, non documenté) · TST-06 (migration : récursion auto-réplicante si destination imbriquée) · FIA-04 (retour `Raise()` ignoré → flags bloqués à vie — abaissé : Denied rarissime en modeless) · FIA-06 (`ActiveUIDocument!` ×7 → NRE message technique) · FIA-07 (nettoyage overrides verts avalé → surbrillance persistante commitée) · FIA-08 (catch vide l.166) · FIA-09 (un collector complet PAR WallType → gel Revit gros modèle) · SEC-01 (sanitisation noms/chemins — abaissé : vecteur externe étroit pour une alpha ; canonique de la famille, absorbe SEC-03/04, TST-05, DON-07 ; remédiation : `GetSafeFilePath` unique + validation à l'import, confirmée au cross-challenge) · SEC-02 (non-signature — abaissé : exigence de distribution future ; absorbe PKG-04) · DON-03 (pas de SchemaVersion — abaissé) · DON-05 (troncature long→int + API dépréciées Revit 2024, CS0618 prouvé par sonde — abaissé : cas rare ; absorbe ADK-01) · DON-06 (course multi-instances settings — abaissé : perte limitée à la config) · DON-09 (import externe : écrasement homonyme sans confirmation, JSON non validé ; absorbe FIA-12) · PKG-05 (perMachine + AppData/HKCU ; absorbe SEC-07) · PKG-06 (chemin `C:\Program Files` en dur dans les .addin livrés) · PKG-07 (double jeu de manifests ; absorbe PRT-06) · PRT-02 (chaîne ComboBox fossile + aller-retour Revit inutile à chaque scène) · ADK-02 (noms AssetProperty en dur → teinte no-op silencieux sur PBR).

### Infos
ARC-08 (analyseurs CA non activés) · MAINT-07 (nommage bilingue — abaissé en Info : préférence de style) · MAINT-14 (127 refs D-xx sans glossaire pointé) · MAINT-15 (pas d'.editorconfig) · MAINT-16 (SetupCustomSort sur un seul des deux chemins d'ajout) · TST-07 (~600 l. de logique pure testables immédiatement) · FIA-11 (logging synchrone verbeux non borné) · PKG-09 (MSI non archivés, fr-FR seul, 6 warnings) · PRT-05 (migrations legacy peut-être vides — question produit) · PRT-08 (BoolToVisibilityConverter double le natif, usage incohérent) · PRT-09 (xmlns orphelin) · **ADK-03 (positif : gestion CompoundStructure 2026 sans noyau déjà conforme)** · **SEC-06 (positif : posture sécurité saine par construction)**.

### Invalidés (annexe des écartés)
- **FIA-10** (fichier temp preview partagé) et **SEC-05** (temp prévisible) : les deux vivent dans le sous-système preview **mort** (`RequestRevitRender` sans appelant, vérifié par grep exhaustif) — défauts non matérialisables, soldés par la suppression du code mort (MAINT-01).

---

## Transition (§11)

Des Bloquants et Majeurs sont présents → au prochain passage en correction, `/dev-council-fix` peut appliquer ce plan lot par lot (cycle atomique, portail 4 portes). Le LOT 1 est le périmètre minimal avant toute nouvelle livraison d'alpha.
