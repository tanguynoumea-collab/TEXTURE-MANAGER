# AUDIT UI — Olympe MaterialManager (Alpha 1.3) — 2026-07-27

**Mode** : DAEDALUS REFONTE, étape 1 (diagnostic). Pipeline ZEUS, cycle d'audit n°1.
**Méthode** : audit statique complet du XAML (7 vues + thème) et des ViewModels (27 commandes recensées).
**Limite assumée (mode dégradé)** : l'add-in tourne dans Revit — aucune capture d'écran n'a pu être prise depuis cette session. Les défauts de *rendu* (alignements réels, densité perçue, drag & drop) sont donc à confirmer sur captures utilisateur. Les défauts structurels relevés ici (tokens, contrastes calculés, focus, états) sont eux certains, car lisibles dans le code.

## Verdict global

Interface bien structurée pour une Alpha — le layout 3 panneaux est sain, le thème sombre est réellement tokenisé et l'action primaire est identifiable — mais l'accent ambre est surexploité au point d'aplatir la hiérarchie, le focus clavier est invisible sur la plupart des contrôles, et plusieurs finitions (accents français absents, valeurs en dur dans les cartes de couches, contraste du bouton principal) trahissent le stade alpha.

## Ce qui fonctionne et doit être PRÉSERVÉ

- **Layout 3 panneaux** avec GridSplitters, largeurs minimales, redimensionnement pensé (colonne centrale en `*`). Le modèle mental gauche→centre→droite épouse le workflow réel.
- **Système de tokens** : palette déclarée une fois dans `OlympeTheme.xaml` (8 couleurs nommées), styles implicites couvrant ~15 contrôles, y compris ScrollBar, ContextMenu, ToolTip. C'est rare à ce stade et c'est la bonne fondation.
- **Action primaire unique** : « Appliquer le matériau » en style dédié (ambre, gras, MinWidth 200) dans une barre dédiée — conforme à l'heuristique « une seule action dominante ».
- **Feedback du Set Mat** : garde anti-double-clic (`IsSetMatBusy`), `CanExecute` réactif, message de statut effacé après 2 s, erreurs en MessageBox français.
- **Cartes de couches** distinctes visuellement (fond dédié, coins arrondis, bordure accent à la sélection) — conforme à ta préférence exprimée (couches bien distinguées).
- **Confirmations** sur les suppressions lourdes (scène, preset, sélection groupe/matériau en YesNo/YesNoCancel).
- **Clavier partiel** : touche Suppr sur l'arbre des types, Entrée/Échap sur les dialogues (`IsDefault`/`IsCancel`).
- **États non-nominaux partiels** : placeholder « Sélectionnez un type… », « Chargement… », messages d'erreur inline rouges par panneau, « Teinte non disponible ».
- **Menus contextuels** cohérents (Supprimer, Dupliquer) + drag & drop des matériaux entre groupes.

## Défauts par gravité

### [CRITIQUE]

1. **Focus clavier invisible sur la quasi-totalité des contrôles.** Les ControlTemplates custom (Button, ListBoxItem, TreeViewItem, ComboBox) ne définissent aucun visuel `IsFocused`/`FocusVisualStyle` — seul TextBox et CheckBox en ont un. → Heuristique 6 (« focus visible en permanence ») → un utilisateur clavier ne sait jamais où il est ; l'app est de fait inutilisable au clavier seul.
2. **Surexploitation de l'accent ambre — hiérarchie aplatie.** L'ambre sert à la fois : titres des 3 panneaux, en-têtes de groupes, libellés de fonction des couches, fonds des boutons « + » / « Ajouter par clic » / « Créer un groupe », message composite, texte de statut, ET l'action primaire. → Heuristique 1 (« une seule action primaire dominante ») → l'œil ne sait plus ce qui est important ; le bouton « Appliquer » perd sa dominance. Note : cela contredit aussi ta préférence enregistrée « boutons de création discrets » — les « + » ont un fond ambre plein.
3. **Contraste insuffisant du bouton primaire** : texte blanc sur ambre `#FF9800` ≈ 2.2:1 (AA exige 4.5:1 en 14 px). Le style ComboBoxItem/MenuItem sélectionné (texte sombre sur ambre ≈ 7:1) montre que la solution existe déjà : passer le texte du bouton en `BackgroundColor` sombre. → Heuristique 6.

### [MAJEUR]

4. **Hover = fond ambre plein sur TOUS les boutons** (style implicite Button). Le bouton destructif « − » réagit exactement comme le bouton créatif « + », à 4 px de distance, en 28×28. → Heuristiques 2 et 4 → risque de suppression par élan, signal hover disproportionné.
5. **Cibles < 32 px** pour des actions fréquentes : boutons « + » / « − » / « … » en 28×28, color picker « … » en 30×24. → Heuristique 2 (Fitts).
6. **Valeurs en dur hors tokens** dans `CenterPanelView.xaml` : `#FF383850`, `#FF424260`, `#FF3E3E56` (cartes de couches, dupliquées dans 2 ListBox), et `#FF9800` re-déclaré en dur dans `SetMatButtonStyle`. → Dérive du système de tokens ; un changement de thème cassera silencieusement ces écrans.
7. **Aucun accent français dans toute l'UI** : « Repertoire », « Selectionnez », « Materiau applique ! », « Creer ». Pour une interface dont la langue est un engagement du projet, c'est une faute de finition visible en permanence. (XAML est UTF-8 : aucun obstacle technique.)
8. **Boutons cryptiques sans nom d'accessibilité** : « + », « − », « … » n'ont ni `AutomationProperties.Name` ni libellé — seuls des tooltips. → Heuristique 6 ; c'est aussi ce qui rend l'app difficile à piloter par WPF-MCP pour les futures design-reviews automatisées.
9. **État de la fenêtre non persisté** (taille/position perdues à chaque session, 1400×750 imposé) et **champ de recherche du dialogue « Ajouter un matériau » sans placeholder ni libellé** — une TextBox nue en tête de dialogue, rien n'indique que c'est une recherche. → Heuristiques 7 et 3.

### [MINEUR]

10. « Ajouter par clic dans la vue » — le mode sélection 3D est un toggle mais rien dans le XAML n'indique l'état actif/inactif du mode (le bouton ne change pas d'apparence).
11. Suppression d'un matériau via menu contextuel sans confirmation ni annulation (les autres suppressions en ont une) — incohérence de protection.
12. « Chargement… » en texte simple, pas de progression pour les opérations longues sur gros modèles (application à N couches, scan des types visibles).
13. `MaterialSpherePreview.cs` : contrôle jamais référencé (code mort côté UI — la « sphère de preview » a été remplacée par un carré).
14. Pas d'état « aucun résultat » dans la recherche du dialogue matériaux (liste vide muette).
15. MessageBox système gris clair au milieu d'une app sombre — rupture de thème à chaque confirmation (limite de WPF MessageBox ; dialogue custom à envisager à terme).

## Workflows chronométrés (statique, à confirmer sur captures)

| Workflow | Actions | Cible |
|---|---|---|
| Appliquer un matériau (type + couche + preset déjà visibles) | 4 clics | ✅ 4 — bon |
| Ajouter un type à la scène par clic 3D | 2 clics + 1 pick Revit | ✅ bon |
| Ajouter un matériau au preset | ~5 actions (+, chercher, sélectionner, groupe, Ajouter) | ✅ acceptable |
| Créer scène + premier type | 3-4 actions | ✅ bon |

Les workflows sont courts — la structure 3 panneaux fait son travail. Aucun défaut d'architecture d'information.

## Inventaire fonctionnel (contrat d'iso-fonctionnalité)

27 commandes recensées : gestion de scènes (créer/supprimer/charger externe), ajout de types (par clic 3D, suppression, Suppr clavier), sélection couches/paramètres multi, application matériau (couches + paramètres familles), gestion presets (créer/supprimer/charger externe), groupes (créer/supprimer), matériaux preset (ajouter avec recherche, dupliquer, supprimer, drag & drop entre groupes), éditeur de matériau (nom, description, couleur RGB + picker, teinte on/off + RGB + picker), répertoire projet (ouvrir/migrer), fenêtre non-modale Revit.

## Ampleur de refonte recommandée : **RAFRAÎCHISSEMENT**

La structure (3 panneaux, navigation, workflows) est saine et doit rester intacte. Les défauts sont des défauts de *discipline visuelle* (accent, focus, tokens, finitions), pas d'organisation. Un lot correctif ciblé suffit ; aucune re-conception d'écran n'est justifiée.
