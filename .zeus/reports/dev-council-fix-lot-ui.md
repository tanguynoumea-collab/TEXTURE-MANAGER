# Rapport de correction — LOT UI (design-review-1) — 2026-07-27

**Source** : `.zeus/reports/design-review-1.md` (audit Daedalus, cycle n°1).
**Triage humain** : tout en correction, sauf m12/m15 reportés (conception, bêta).
**Protocole** : un finding = un commit, portail 4 portes par finding
(build `net48` PUIS `net8.0-windows`, 0 warning maintenu, `dotnet test`, preuve grep/lecture XAML).

## Corrigé (12/12 findings applicables — 0 annulé)

| ID | Résumé | Preuve de fermeture (porte 3) | Commit |
|---|---|---|---|
| UI-C1 | Focus clavier visible : `OlympeFocusVisualStyle` (pointillé accent, adorner WPF navigation clavier) appliqué à Button, ComboBox, ListBoxItem, TreeViewItem, header d'Expander et cartes du panneau central. TextBox et CheckBox avaient déjà un visuel `IsFocused` (bordure accent) — conservés. | grep : 7 setters `FocusVisualStyle` (6 thème + 1 CardItemStyle) ; style keyed défini avant les styles qui le référencent (pas de forward-reference) | 82ca1b2 |
| UI-C2 | Discipline de l'ambre : titres des 3 panneaux + « Éditeur de matériau » → TextPrimary SemiBold ; Function/ParameterName des cartes → TextPrimary SemiBold ; en-têtes de groupes (gauche + presets) → TextSecondary SemiBold ; boutons de création « + » / « Créer un groupe » / « Ajouter par clic » → style discret par défaut (préférence utilisateur) ; message composite → TextSecondary italique. | grep `AccentBrush` dans les 3 vues : 0 occurrence à gauche/droite, 2 au centre = bordures de sélection des cartes (usage autorisé). L'ambre reste : action primaire, sélection, statut, glyphes hover | e91fa17 |
| UI-C3 | Bouton primaire : Foreground → `BackgroundBrush` (#1E1E2E sur ambre ≈ 7:1), Background → `StaticResource AccentBrush`. | grep `#FF9800` : seule occurrence restante = le token `AccentColor` du thème | a99644a |
| UI-M4 | Hover proportionné : tokens `SurfaceHoverColor` #3A3A4E + `SurfaceHoverBrush` ; Button implicite : hover = fond éclairci + bordure accent, fond accent réservé au pressed (texte sombre) ; `SetMatButtonStyle` : template dédié hover AccentHover (le primaire reste ambre) ; `DangerButtonStyle` (hover bordure ErrorBrush, pressed ErrorBrush) appliqué aux 4 boutons « − ». | Lecture du thème : triggers IsMouseOver ≠ fond accent plein ; grep `DangerButtonStyle` : 4 usages (2 gauche, 2 droite) | e73b1d9 |
| UI-M5 | Cibles ≥ 32 px : 6 boutons « + » / « − » / « … » 28×28 → 32×32 ; 2 color pickers 30×24 → 32×28. StackPanels horizontaux sans hauteur fixe : pas de clipping. | grep : plus aucun `Width="28"`/`Width="30"` sur les boutons ; 9× 32×32 + 2× 32×28 | 1090608 |
| UI-M6 | Tokens `CardBrush`/`CardHoverBrush`/`CardSelectedBrush` dans OlympeTheme.xaml ; style de carte dupliqué (2 ListBox) extrait en `CardItemStyle` keyed dans les ressources de CenterPanelView, référencé par les deux listes. | grep `FF383850|FF424260|FF3E3E56` dans CenterPanelView : 0 (uniquement les tokens du thème) | 82bb780 |
| UI-M7 | Accents restaurés dans TOUTES les chaînes visibles : XAML (« Répertoire de projet », « Sélectionnez un type… », « Type composé — sélectionnez… », « Créer un groupe », « Ajouter un matériau au preset », titre du dialogue, tooltips, [Composé]) ; VMs (« Matériau appliqué ! », statuts, confirmations, erreurs) ; PresetService (« Le nom ne peut pas être vide. », etc.) ; dialogues ; ruban (« Matériaux ») ; TaskDialog pick ; noms de transactions Revit (visibles dans Annuler). Identifiants de code inchangés. Noms de fichiers persistés (« Preset par defaut », « Preset migre ») volontairement inchangés (sans rien casser : ce sont des clés de fichiers existants). Assertion de test alignée (« interieur » → « intérieur »). Fichiers UTF-8, builds verts sur les 2 TFM. | grep chaînes UI sans accents (Text/Content/ToolTip/Title/Header + littéraux C# hors logs) : 0 restante | 03a33a2 |
| UI-M8 | `AutomationProperties.Name` français sur les 11 boutons cryptiques : « Créer une scène », « Supprimer la scène active », « Charger une scène externe », « Retirer le type sélectionné de la scène », « Créer un preset », « Supprimer le preset actif », « Charger un preset externe », « Ajouter un matériau au preset », « Supprimer le groupe ou matériau sélectionné », « Choisir la couleur de surface », « Choisir la couleur de teinte ». | grep `AutomationProperties.Name` : 4 (gauche) + 7 (droite) + 1 SearchBox (M9) | f09c37d |
| UI-M9 | (a) `AppSettingsDto` : WindowWidth/Height/Left/Top nullable ; `WindowService.RestoreWindowPlacement` (garde écran virtuel multi-moniteurs + MinWidth/MinHeight + RestoreBounds si maximisée) et `SaveWindowPlacement` ; branché dans ShowWindowCommand : restauration à la création, sauvegarde sur `Closing` (croix ET arrêt Revit), try/catch FIA-03. (b) Placeholder « Rechercher un matériau… » en TextBlock overlay (DataTrigger sur Text vide, IsHitTestVisible=False). | 2 nouveaux tests round-trip du DTO (présence + absence des champs) : 35 → **37 tests verts** ; lecture XAML du pattern overlay | 895d03f |
| UI-m10 | Le bouton « Ajouter par clic dans la vue » reflète `IsPickMode` (propriété existante du VM) : fond/bordure accent + « Sélection en cours — cliquez dans la vue » pendant le pick, retour automatique (IsPickMode repasse à false dans le callback). | Lecture XAML : Content par défaut en Setter (pas de valeur locale qui écraserait le trigger) | 1b7ed65 |
| UI-m11 | `DialogService.Confirm` avant la suppression d'un matériau du preset — placé dans `SupprimerMateriau`, couvre le menu contextuel ET le bouton « − » (via SupprimerSelection). | Lecture code : garde Confirm avant `group.Materials.Remove` | 10fb108 |
| UI-m14 | « Aucun matériau ne correspond à la recherche » (TextBlock overlay sur la liste) visible quand la vue filtrée est vide et la recherche non vide ; mis à jour à chaque Refresh. | Lecture code : `UpdateNoResultVisibility()` sur TextChanged + init | 50fb441 |

## Annulé

Aucun. Les 4 portes sont passées pour chacun des 12 findings
(seul incident : le test `MigrateProjectDirectory_CheminImbrique` vérifiait littéralement
le message français « interieur » — assertion mise à jour dans le même commit UI-M7,
ce qui est la fermeture correcte du finding, pas un contournement).

## Reportés (décision de conception — bêta)

- **UI-m12** — Progression déterminée sur opérations longues (application à N couches,
  scan des types visibles) : nécessite un canal de progression dans RevitEventBridge → conception.
- **UI-m15** — MessageBox système thémées (dialogue custom sombre) : re-conception du
  DialogService → conception. Les MessageBox restent système (rupture de thème connue).
- **m13 (code mort MaterialSpherePreview)** : hors périmètre du lot UI transmis (non listé
  dans le triage) — à traiter dans un lot maintenance.

## État final

- Builds : `net48` et `net8.0-windows`, **0 warning / 0 erreur** chacun (analyseurs CA actifs).
- Tests : **37/37** (35 existants + 2 nouveaux round-trip UI-M9).
- 12 commits atomiques `fix(ui): <ID> — …`, arbre propre.

## Limite assumée

Le **rendu visuel réel n'a pas pu être vérifié** ici (l'add-in tourne dans Revit ; aucune
capture possible depuis cette session). Les preuves de fermeture sont statiques
(grep/lecture XAML + builds/tests). **La design-review Phase 2 sur captures reste due en
session Revit** (protocole de deploy : voir feedback_deploy_protocol) — points à contrôler
visuellement : lisibilité du pointillé de focus, équilibre du hover SurfaceHover, hiérarchie
après retrait de l'ambre des titres, non-clipping des boutons 32 px dans les barres,
restauration de fenêtre en multi-écrans.
