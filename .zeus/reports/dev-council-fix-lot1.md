# DEV-COUNCIL — Rapport de correction LOT 1

**Date** : 2026-07-27 · **Source** : `.zeus/reports/dev-council-1.md` (Couche 2 — LOT 1) · **Protocole** : cycle atomique, portail 4 portes (constitution §10)
**Baseline mesurée avant intervention** : net48 = 3 warnings nullable distincts (CS8600, CS8601, CS8604 — comptés 6 par le double passage XamlPreCompile/CoreCompile, cohérent avec le rapport d'audit) ; net8.0-windows = 0 warning.

---

## Corrigé (7/7)

| # | ID | Commit | Résumé | Preuve de fermeture (porte 3) |
|---|----|--------|--------|-------------------------------|
| 1 | **DON-01** (Majeur) | `f140876` | Helper unique `WriteJsonAtomic(path, obj)` (écriture `.tmp` → `File.Replace` avec `.bak`, `File.Move` au premier write). Les 5 sites `File.WriteAllText` de `PresetService.cs` remplacés. | Grep : plus aucun `File.WriteAllText` hors du write du `.tmp` interne au helper. Test `SavePreset_DeuxSauvegardes_ConserveUnBak_SansResiduTmp` vert (ajouté au point 3). |
| 2 | **DON-02** (Bloquant) | `f42d599` | `LoadPreset`/`LoadScene` retournent `null` sur fichier présent mais illisible (après quarantaine `<nom>.json.corrupt-<yyyyMMdd-HHmmss>` + log via `LogService`) ; `LoadSettings` quarantaine + défaut ; `LoadScenes(out bool loadFailed)`. VMs : `_presetLoadFailed` (RightPanel) et `_scenesLoadFailed` (LeftPanel) bloquent `AutoSave`/`AutoSaveScenes` + message utilisateur (`StatusMessage`/`ErrorMessage`). Si le rename échoue (fichier verrouillé), le fichier n'est PAS écrasé : échec signalé, AutoSave reste bloqué. Fichier absent = défaut silencieux (inchangé). | Tests verts : `LoadPreset_JsonTronque_RetourneNull_EtMetEnQuarantaine`, `LoadScene_JsonTronque_...`, `LoadSettings_JsonCorrompu_...`, `LoadScenes_UneSceneCorrompue_SignaleEchec_EtChargeLesValides`. Inspection : `AutoSave` garde `if (_presetLoadFailed) return;`. |
| 3 | **Couture d'arbitrage** (unanime) | `a215b02` | Répertoire de données de `PresetService` injectable (`PresetService(string? projectDirectory = null)`, défaut = comportement actuel, zéro impact runtime Revit). Projet `tests\OlympeMaterialManager.Tests` (xunit, net8.0-windows, référence au csproj principal) ajouté à la .sln (dossier solution `tests`). 8 tests sur répertoires temporaires isolés, aucun type Revit exercé. | `dotnet test` : **8/8 verts** (nominal round-trip preset et scène, absent → défaut, tronqué → quarantaine + échec signalé, écriture atomique .bak, settings corrompus). |
| 4 | **FIA-01** (Majeur) | `e992198` | `ShowWindowCommand` : avant construction des VMs, `IsProjectDirectoryAccessible` (Directory.Exists + sonde d'écriture d'un fichier temp supprimé aussitôt). En échec : MessageBox français + boucle de re-sélection via `DialogService.ShowFolderBrowser` (flux premier lancement) ; annulation → `Result.Cancelled` propre, aucune exception. | Inspection : la boucle `while (!IsProjectDirectoryAccessible(...))` précède `new MainWindowViewModel(...)`. Builds des 2 TFM verts. |
| 5 | **PKG-01** (Bloquant) | `6364fd8` | `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` dans le csproj + exclusion `$(OutputPath)publish\**` des `StagingFiles`. | Après build Release + wixproj Release : `staging\net8.0-windows\` contient **CommunityToolkit.Mvvm.dll** et **Microsoft.Xaml.Behaviors.dll** ; **absence** de RevitAPI*.dll, Nice3point.Revit.Toolkit.dll et de publish\ (aucune exclusion manuelle nécessaire : les packages Nice3point excluent bien leurs assemblies du runtime). Note : System.Text.Json non copié sur net8 = normal (version inbox du framework). MSI construit sans erreur. Validation finale par install sur machine vierge = hors périmètre outillé (comme noté dans l'audit). |
| 6 | **PKG-02** (Majeur) | `27713ab` | `<RemoveDir Directories="$(StagingDir)\$(TargetFramework)" />` en tête de `StageForInstaller` + `ProjectSection(ProjectDependencies)` wixproj → csproj dans la .sln. **Adaptation mécanique** : purge par TFM et non du staging entier, car la cible s'exécute une fois par TFM (une purge globale effacerait le staging net48 pendant le build net8). | Preuve outillée : DLL fantôme `Fantome.dll` plantée dans `staging\net8.0-windows\`, rebuild Release → disparue, staging net48 intact. `dotnet sln list` OK, wixproj Release OK. |
| 7 | **PKG-03** (Majeur) | `e83fbbc` | `Directory.Build.props` racine solution avec `<Version>1.4.0</Version>` ; Version/AssemblyVersion/FileVersion retirés du csproj ; wixproj `DefineConstants` `Version=$(Version)` ; `Package.wxs` `Version="$(var.Version)"`. | Preuve outillée : DLL Release FileVersion = **1.4.0.0** ; table Property du MSI : ProductVersion = **1.4.0** (> 1.0.0.0 → MajorUpgrade opérationnel). |

## Annulé

Aucun. Les 4 portes ont été vertes pour chacun des 7 findings (builds par target net48 puis net8.0-windows, tests 8/8 dès le point 3, preuve de fermeture, warnings strictement identiques à la baseline).

## En attente de décision (lot groupé)

1. **PKG-03 — choix de version 1.4.0** : l'AssemblyVersion passe de **2.0.0.0 → 1.4.0.0** (downgrade d'assembly, aligné sur la nomenclature git « Alpha 1.x » et > 1.0.0.0 du MSI). Appliqué comme demandé par le triage, mais à valider : si des manifests/`.addin` ou d'anciens installs référencent la 2.0.0.0, ou si tu préfères une numérotation 2.x, ajuster **uniquement** `OlympeMaterialManager\Directory.Build.props` (source unique désormais).
2. **PKG-01 — validation terrain** : l'audit exige une install propre sur machine vierge + lancement Revit 2025 pour clôturer définitivement (non réalisable ici en lecture seule). Le protocole de déploiement Revit 2025 mémorisé s'applique.

---

**Bilan : 7 appliqués · 0 annulé · 2 points en attente de décision/validation humaine.**
Commits `f140876` → `e83fbbc` sur `master`. Warnings finaux identiques à la baseline ; MSI Release reconstruit avec succès.
