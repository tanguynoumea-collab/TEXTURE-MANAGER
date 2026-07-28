# Exploration améliorations & ajouts — Olympe MaterialManager — 2026-07-27

Candidats pour la roadmap, nourris par l'audit UI (Daedalus) et l'audit code (dev-council). **Rien n'est écrit dans la roadmap sans ta confirmation** (checkpoint ZEUS n°4). Classés par valeur estimée pour l'architecte utilisateur.

## A. Consolidation (issus des audits — le socle avant les nouveautés)

| # | Idée | Origine | Effort |
|---|---|---|---|
| A1 | **Lot 1 dev-council** : persistance blindée (atomique + récupération corrompu) + MSI net8 réparé + version unifiée | DON-02, DON-01, PKG-01/02/03 | 1-2 sessions |
| A2 | **Lot UI critique Daedalus** : focus clavier visible, discipline de l'accent ambre (une seule action dominante, boutons création discrets — ta préférence), contraste bouton primaire (texte sombre sur ambre) | Audit UI C1-C3 | 1 session |
| A3 | Finitions UI : accents français partout, tokens pour les cartes de couches, persistance taille/position de fenêtre, placeholder du champ recherche, indicateur visuel du mode pick 3D actif | Audit UI M4-M9, m10 | 1 session |
| A4 | Nettoyage code mort (chaîne sphère ~600 l., chaîne ComboBox familles, orphelins) — clarifie le terrain avant toute évolution | MAINT-01/12, PRT-02 | ½ session |

## B. Améliorations UX à forte valeur (nouvelles, hors audit)

| # | Idée | Détail | Effort |
|---|---|---|---|
| B1 | **Gestion des matériaux absents à l'import d'un preset** (redéfini par l'utilisateur le 2026-07-28). Philosophie produit actée : les presets/scènes se partagent **entre personnes sur la même maquette** (bibliothèque de matériaux commune par construction) — pas de portabilité inter-projets. Sur une autre maquette : message d'erreur listant TOUS les matériaux introuvables + demande « réimportez votre matériautèque », puis **suppression du preset des matériaux absents**. | La résolution par nom (DON-04) reste le mécanisme de rattachement. ⚠️ Garde-fou à trancher en conception : la purge suivie de l'AutoSave modifie le fichier de preset PARTAGÉ par l'équipe — prévoir une confirmation explicite avant purge (sinon ouvrir la mauvaise maquette par erreur ampute le preset commun). | ½-1 session |
| B2 | **Pipette matériau** : cliquer un élément en 3D → récupérer son matériau directement dans le preset actif (l'inverse du pick actuel) | Réutilise l'infrastructure PickObject existante | 1 session |
| B3 | **Drag & drop preset → couche** : glisser un matériau du panneau droit sur une carte de couche du panneau central pour l'appliquer (alternative au bouton « Appliquer ») | L'infra drag & drop existe déjà dans le panneau droit | 1 session |
| B4 | **Annuler la dernière application** : bouton/Ctrl+Z qui restaure le matériau précédent des couches modifiées (les transactions Revit sont déjà nommées — TransactionGroup ou mémorisation du dernier état) | Sécurise le workflow cœur | 1 session |
| B5 | **Recherche/filtre** dans les 3 panneaux (types, couches, presets) — indispensable dès que les scènes et presets grossissent | État « aucun résultat » à prévoir (audit UI) | ½-1 session |
| B6 | **Aperçu matériau réel** : re-brancher (ou re-concevoir en plus simple) la preview — le rendu Revit existe côté bridge mais est débranché ; décision à prendre : supprimer (A4) OU rebrancher en vignette dans l'éditeur | Trancher PRT-01 en décision produit | 1-2 sessions si rebranché |
| B7 | **Appliquer à plusieurs types d'un coup** : multi-sélection dans le panneau gauche + application du preset aux couches homologues (même fonction de couche) de tous les types sélectionnés | Multiplie la valeur du geste central | 1-2 sessions |

## B-bis. Idées utilisateur (dictées le 2026-07-28, après validation fonctionnelle de l'Alpha 1.4)

| # | Idée | Détail | Notes techniques |
|---|---|---|---|
| B8 | **Liseré couleur matériau sur les cartes de couches** | Chaque carte de couche du panneau central reçoit une ligne de contour reprenant la couleur du matériau associé → on voit d'un coup d'œil la couleur de chaque matériau et son changement après application | La couleur est déjà disponible côté bridge (`MaterialDetailsDto`) ; il faudra l'ajouter au `LayerDto`. Le liseré suit dynamiquement le mode d'affichage choisi (voir B10) |
| B9 | **« Éditeur de matériau » → « Visualisateur de matériau » + ouverture dans Revit** | Renommer la section du panneau droit, et ajouter un bouton qui ouvre le matériau dans l'interface native Revit (menu Matériaux) pour le modifier en direct | API : `PostableCommand.Materials` ouvre le dialogue Matériaux natif ; la présélection du matériau exact est à investiguer (pas d'API directe connue — piste : sélection préalable ou presse-papier du nom) |
| B10 | **Trois modes de visualisation des aperçus** : Couleur uniforme / Texture / Réaliste | L'aperçu couleur du visualisateur (et les pastilles/liserés) s'adapte au style d'affichage utilisé dans la vue 3D : couleur uniforme (`Material.Color`), texture (bitmap de l'appearance asset), réaliste (rendu d'apparence). Le mode choisi est **persisté** (settings) | Le mode pilote aussi le liseré B8. « Texture »/« Réaliste » nécessitent la lecture de l'appearance asset (chemin bitmap — attention schémas PBR, cf. ADK-02) voire un rendu ; le pipeline de rendu supprimé en LOT 3a (sphère) peut servir de référence via l'historique git |

## C. Ajouts plus ambitieux (candidats bêta / v2)

| # | Idée | Détail |
|---|---|---|
| C1 | **Matériaux par face (Paint)** : appliquer un preset sur des faces via l'outil Paint de l'API — couvre les cas hors CompoundStructure | Nouveau domaine API (Document.Paint) |
| C2 | **Bibliothèque partagée d'équipe** : un répertoire projet réseau assumé (avec les verrous/concurrence traités proprement — DON-06) + fusion de presets | Fait du point faible actuel une feature |
| C3 | **Rapport matériaux** : export de la liste types/couches/matériaux de la scène (CSV/PDF) pour les livrables | Réutilise les DTOs existants |
| C4 | **Textures/apparence avancée** : édition du bitmap de l'AppearanceAsset (chemin de texture, échelle), au-delà de la teinte | Prolonge MaterialEditor ; attention ADK-02 (schémas PBR) |
| C5 | **Thème clair** + densité réglable | Le système de tokens le permet une fois A3 fait |

## D. Chantiers techniques (fond de roadmap)

| # | Idée | Origine |
|---|---|---|
| D1 | CI GitHub Actions : build 2 TFM + tests + MSI artefact + tag/release par alpha (archivage des MSI livrés) | PKG-09, TST-01 |
| D2 | Signature de code avant élargissement de la diffusion | SEC-02 |
| D3 | `.editorconfig` + analyseurs Roslyn + traitement des 6 warnings nullable | ARC-08, MAINT-11/15 |
| D4 | SchemaVersion dans les fichiers JSON + stratégie de migration | DON-03 |
| D5 | Refactor testabilité opportuniste en bêta (interfaces, façade typée du bridge, partials) | Arbitrage ARC-03 |
| D6 | Détection des versions Revit installées dans le MSI (Features conditionnelles) + .addin en ProgramData | PKG-05/08 |

## Recommandation ZEUS (ordre suggéré)

1. **Cycle correctif immédiat** : A1 (lot 1) → gate tests → mini design-review — c'est la condition pour que l'alpha suivante soit fiable.
2. **Cycle UI** : A2 + A3 + A4 ensemble (même zone de code, une seule passe Daedalus).
3. **Premier cycle « nouveauté »** : B1 (portabilité des presets) — c'est l'amélioration qui change le plus la valeur du produit, et elle s'appuie sur le fix DON-04 du lot 2.
4. B2/B3/B5 en cycles courts ensuite ; trancher B6 (sphère : supprimer ou rebrancher) au moment de A4.
