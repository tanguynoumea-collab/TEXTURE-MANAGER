# DESIGN_PLAN — Olympe MaterialManager

Établi le 2026-07-28 (cycle 2, Daedalus mode ÉVOLUTION). Section 1 = contrat rétro-ingénieré de l'UI existante (acceptée en 1.4.0). Section 2 = delta du cycle B8/B9/B10/B5. Le plan est contractuel une fois validé : tokens via ressources, jamais de valeur en dur.

---

## 1. Contrat existant (rétro-ingénierie, v1.4.0)

**Ancrage** : outil de production BIM pour architecte, utilisé en va-et-vient avec la vue 3D Revit. Job de l'écran unique : *appliquer vite un matériau de sa bibliothèque aux couches d'un type visible*. Densité « outil 8 h/jour » : compacte, sombre, sans décoration.

**Tokens** (Themes/OlympeTheme.xaml — tous existants) :
- Fond `#1E1E2E` · Surface `#2D2D3D` · SurfaceHover `#3A3A4E` · Bordure `#3D3D4D`
- Accent ambre `#FF9800` (+ hover `#FFA726`) — **réservé** : action primaire, sélection, feedback, focus
- Texte primaire `#E0E0E0` / secondaire `#A0A0A0` · Erreur `#EF5350`
- Cartes de couches : `CardBrush`/`CardSelectedBrush`/`CardHoverBrush` + `CardItemStyle` partagé
- Segoe UI 13, coins 4 px, grille 4 px, cibles ≥ 32 px, focus `OlympeFocusVisualStyle` (pointillé accent)

**Structure** : 3 panneaux (types | couches | presets) + barre d'action basse avec l'unique bouton accentué « Appliquer le matériau ». Boutons de création discrets (préférence utilisateur actée). Langue : français accentué.

**L'élément signature du produit** (nouveau statut, à acter) : le **liseré matériau** introduit par ce cycle — la couleur/matière visible sur chaque carte fait de l'écran central un nuancier vivant. Tout le reste reste calme.

---

## 2. Delta du cycle 2

### 2.1 B5 — Recherche (panneaux gauche et droit)

Un champ par panneau, inséré **sous la rangée sélecteur de scène/preset**, pleine largeur :

```
┌─ PANNEAU GAUCHE ────────────────┐   ┌─ PANNEAU DROIT ─────────────────┐
│ Types dans la scène             │   │ Matériaux preset                │
│ [Scène ▾] [+][−][…]             │   │ [Preset ▾] [+][−][…]            │
│ [🔍 Rechercher un type…       ] │   │ [Créer un groupe] [+][−]        │
│ [Ajouter par clic…] [−]         │   │ [🔍 Rechercher un matériau…   ] │
│ ▸ Murs                          │   │ ▸ Bétons (3)                    │
│   Mur de base : Béton 20        │   │   ▪ Béton banché C25            │
│ …                               │   │ …                               │
└─────────────────────────────────┘   └─────────────────────────────────┘
```

- TextBox standard du thème, placeholder « Rechercher un type… » / « Rechercher un matériau… » (pattern watermark de AddMaterialDialog réutilisé), bouton ✕ d'effacement quand non vide.
- Insensible casse ET accents. Les groupes restent visibles si un enfant matche (auto-dépliés).
- État « Aucun type/matériau ne correspond à la recherche » (pattern UI-m14) + lien « Effacer la recherche ».
- Hiérarchie : le champ est en style discret (pas d'accent) — il ne concurrence pas l'action primaire.

### 2.2 B9 — Visualisateur de matériau + ouverture Revit

En-tête de la section basse du panneau droit :

```
│ Visualisateur de matériau      [Ouvrir dans Revit ⧉] │
│ [aperçu]  Nom : …  Description : …  Couleur : …      │
```

- Titre renommé « Visualisateur de matériau ». Bouton « Ouvrir dans Revit » en style discret, aligné à droite du titre, tooltip : « Ouvre le gestionnaire de matériaux Revit (le nom du matériau est copié — collez-le dans la recherche). S'ouvre lorsque Revit reprend le focus. »
- Au clic : nom copié au presse-papiers + `PostCommand` via bridge + Topmost temporairement désactivé.
- **Question ouverte au checkpoint (Q2)** : la section conserve aujourd'hui l'édition inline (nom, description, couleur, teinte). Options : (a) garder l'édition sous le nom « Visualisateur » (le terme couvre l'usage dominant, l'édition reste un bonus) ; (b) garder l'édition et titrer « Matériau » tout court ; (c) retirer l'édition inline (Revit devient le seul éditeur). **Recommandation Daedalus : (a)** — ne pas retirer de fonctionnalité (iso-fonctionnalité), le renommage traduit l'intention d'usage.

### 2.3 B10 — Sélecteur de mode d'aperçu (révisé DR2)

Barre segmentée **2 positions** dans l'en-tête du visualisateur, sous le titre :

```
│ Visualisateur de matériau      [Ouvrir dans Revit ⧉] │
│ Aperçu :  [ Couleur ]│[ Réaliste ]                    │
```

- Style ToggleButton segmenté aux tokens existants : segment actif = fond Surface + bordure accent (pattern « sélection », PAS fond ambre plein) ; inactifs discrets. Cibles ≥ 32 px de haut.
- Le mode s'applique : au carré d'aperçu du visualisateur, aux pastilles des matériaux preset, au liseré B8. Persisté immédiatement (valeur historique « Texture » mappée vers Réaliste à la lecture, sans quarantaine).
- Mode Réaliste (DR2) : **couleur d'apparence** du matériau — la diffuse/albedo de l'asset d'apparence, celle que la vue 3D Réaliste de Revit affiche pour un matériau sans texture. Absente → fallback couleur graphique + indicateur « Pas d'apparence — couleur graphique » (échec expliqué, jamais muet dans le visualisateur).
- Historique Q1 : le segment « Réaliste » avait été livré désactivé (« Disponible en phase 2 ») aux côtés d'un mode « Texture » par bitmap. Après diagnostic terrain (itération 2 de la design-review), le mode Texture est supprimé et Réaliste devient actif — voir §4.

### 2.4 B8 — Liseré matériau sur les cartes (l'élément signature)

```
┌─ Carte de couche ──────────────────┐
│▌ Finition 1              20.0 mm   │   ▌= liseré 6 px, bord gauche,
│▌ Béton banché C25                  │      coins arrondis côté gauche
└────────────────────────────────────┘
```

- Bande verticale de **6 px** sur toute la hauteur du bord gauche de chaque carte (couches ET paramètres matériaux), intégrée au `CardItemStyle` (DR1-1 : bande miroir aussi sur le bord droit).
- Mode Couleur → `SolidColorBrush` de la couleur graphique du matériau. Mode Réaliste (DR2) → `SolidColorBrush` de la **couleur d'apparence** ; absente → couleur graphique. Matériau « Par catégorie » sans apparence → liseré transparent (aucune matière = aucune bande, pas de gris menteur).
- La sélection de carte reste la bordure accent périphérique existante — le liseré vit À L'INTÉRIEUR de cette bordure, les deux ne se confondent pas (accent = état, liseré = donnée).
- Après « Appliquer », le rafraîchissement existant met à jour le liseré — le changement de matériau devient visible instantanément : c'est le cœur de la demande.
- Les pastilles 12 px du panneau droit suivent le même mode (couleur graphique/couleur d'apparence) pour la cohérence.

### 2.5 Mapping fonctionnalités → emplacements (iso-fonctionnalité)

| Fonctionnalité | Emplacement | Statut |
|---|---|---|
| B5 recherche types | Panneau gauche, sous sélecteur de scène | Nouveau |
| B5 recherche matériaux | Panneau droit, sous boutons groupe | Nouveau |
| B9 renommage + Ouvrir dans Revit | En-tête visualisateur | Nouveau |
| B10 sélecteur 2 modes (Couleur/Réaliste, DR2) | En-tête visualisateur | Nouveau |
| B8 liseré | Cartes du panneau central + pastilles droite | Nouveau |
| Toutes fonctions 1.4.0 | Inchangées — aucun déplacement | Préservé |

### 2.6 États non-nominaux du delta

- Recherche sans résultat : message + « Effacer la recherche » (les deux panneaux, même formulation).
- Mode Réaliste sans couleur d'apparence (pas d'asset, ou asset sans couleur exploitable) : fallback couleur graphique + indicateur « Pas d'apparence — couleur graphique » sur l'aperçu, tooltip + texte visible (jamais silencieux dans le visualisateur ; silencieux sur le liseré — 6 px ne portent pas de tooltip fiable).
- « Par catégorie » : liseré transparent, aperçu du visualisateur inchangé (comportement actuel).
- Chargement des couches : inchangé (« Chargement… »).

## 3. Passe B — auto-critique

- *« Produirais-je ce plan pour une autre app ? »* Non : le liseré-nuancier et le sélecteur de mode lié à la vue 3D Revit sont spécifiques au métier de cette app ; la recherche et le bouton Revit sont génériques mais s'insèrent dans le langage existant sans créer de deuxième style.
- **Heuristiques** : hiérarchie préservée (aucun nouvel élément accentué plein — le segment actif utilise le pattern sélection) ; Fitts OK (champ pleine largeur, segments ≥ 32 px) ; charge cognitive : +2 contrôles par panneau au maximum, dans des zones déjà structurées — la règle 7±2 tient ; cohérence : watermark, « aucun résultat », confirmations et tokens repris de l'existant ; feedback : chaque échec de texture est expliqué dans le visualisateur.
- **Principe de Chanel — retiré du plan** : l'indicateur « aperçu couleur » sur chaque *liseré* en fallback (bruit sur 6 px, le tooltip du visualisateur suffit) ; l'idée d'un liseré sur les en-têtes de groupes du panneau droit (décoration sans donnée nouvelle).
- **Points faibles assumés** : la lisibilité d'un motif tuilé dans 6 px n'est prouvable que sur capture réelle → critère explicite de la design-review (repli prévu : couleur diffuse d'asset) ; le mode ne change pas la vue 3D Revit elle-même (hors périmètre — le réglage aligne l'add-in sur le style que l'utilisateur a choisi dans Revit, il ne le pilote pas).

## 3bis. Delta du cycle 3 (B2/B3/B1 — 2026-07-28, enchaînement autorisé)

### B2 — Pipette matériau (panneau droit)
- Bouton « pipette » (glyphe, 32×32, style discret, AutomationProperties « Récupérer les matériaux d'un élément ») dans la rangée « Créer un groupe / + / − » du panneau droit. Tooltip : « Cliquez un élément dans la vue 3D pour récupérer ses matériaux dans le preset ».
- Comportement (spec utilisateur, décision d'approche consignée) : pick d'UN élément (réutilise l'infra PickObject/masquage de fenêtre existante) → lecture des matériaux du type (couches CompoundStructure ou paramètres matériaux) → **ajout automatique sans dialogue** de tous les matériaux valides (dédoublonnés par rapport au groupe cible) dans le **groupe sélectionné ou actif**, sinon dans un groupe **« Autre »** créé au besoin. Feedback : StatusMessage « N matériau(x) ajouté(s) au groupe ‹X› » (les « Par catégorie » et doublons sont ignorés, comptés dans le message si utile).
- Alternative rejetée : dialogue de sélection à cocher — contredit le « automatiquement » de la spec utilisateur ; la suppression d'un matériau indésirable reste à un clic (menu contextuel).

### B3 — Drag & drop preset → carte de couche (panneau central)
- Les cartes de couches ET de paramètres acceptent le drop d'un matériau preset (source de drag existante du panneau droit). Survol d'une cible valide → la carte prend la bordure accent (même vocabulaire que la sélection) ; drop → application immédiate à CETTE couche/paramètre (même chemin transactionnel que « Appliquer », mono-cible), liseré mis à jour par le refresh existant.
- Le bouton « Appliquer le matériau » reste l'action primaire pour le multi-couches — le drop est un raccourci mono-couche, aucun changement de hiérarchie.

### B1 — Matériaux absents à l'activation d'un preset
- À l'activation d'un preset (changement de ComboBox, chargement initial, import externe), validation des matériaux via le bridge (id+nom, même logique que ResolveMaterial). S'il y a des absents : dialogue listant TOUS les matériaux introuvables + message « Ces matériaux n'existent pas dans ce document. Réimportez votre matériauthèque, ou supprimez-les du preset. » Boutons : « Supprimer du preset » (⚠️ garde actée : c'est une confirmation explicite — le preset est un fichier partagé d'équipe) / « Conserver » (défaut). Suppression → purge + sauvegarde ; conservation → comportement actuel (échec propre à l'application).
- États : aucun document ouvert → validation différée sans message.

### Journal cycle 3
- Choix rejeté : dialogue à cocher pour la pipette (voir B2). Purge automatique sans confirmation pour B1 (rejetée dès le triage roadmap — fichier partagé).

## 4. Journal des choix rejetés

- Couleur moyenne de texture pour le liseré — rejetée (brun indiscriminant, council unanime après arbitrage).
- **Mode Texture par bitmap — supprimé après diagnostic terrain (DR2, itération 2 de la design-review)** : olympe.log sur les données réelles de l'utilisateur a montré ZÉRO texture bitmap résolvable (placeholders UnifiedBitmap.png par défaut, chemins d'une autre machine, bibliothèques absentes). Le mode entier (FindTexturePath, TexturePathResolver, TextureBrushCache, branche ImageBrush) est retiré ; remplacé par « Réaliste » = couleur d'apparence (diffuse/albedo de l'asset), lecture mémoire pure sans I/O disque.
- Restauration immédiate du pipeline de rendu pour « Réaliste » — rejetée (mutation du document, gel, findings ré-ouverts) → phase 2 sur cache disque à la demande.
- Liseré en contour complet de carte — rejeté (entrerait en collision avec la bordure accent de sélection ; bord gauche = lecture verticale rapide en colonne).
- Champ de recherche dans la barre d'en-tête globale — rejeté (la recherche est par panneau, pas globale).
