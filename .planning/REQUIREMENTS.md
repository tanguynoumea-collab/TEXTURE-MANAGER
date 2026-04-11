# Requirements: Olympe MaterialManager

**Defined:** 2026-04-11
**Core Value:** L'architecte peut appliquer rapidement un materiau preset aux couches ou parametres materiaux de n'importe quel type Revit visible en 3D, en quelques clics depuis un editeur visuel unifie.

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### Infrastructure

- [x] **INFRA-01**: Le projet compile en multi-target (net48 pour Revit 2024, net8.0-windows pour Revit 2025/2026) avec un single SDK-style csproj
- [x] **INFRA-02**: Les references Revit API (Nice3point NuGet packages) sont configurees avec CopyLocal=false pour chaque version cible
- [x] **INFRA-03**: L'add-in s'enregistre via un fichier .addin dans le dossier Revit Addins pour chaque version supportee
- [x] **INFRA-04**: Un IExternalApplication demarre l'add-in et cree l'ExternalEvent singleton au lancement de Revit
- [x] **INFRA-05**: Un IExternalEventHandler centralise avec dispatch par enum gere toutes les interactions UI-vers-Revit
- [x] **INFRA-06**: La fenetre WPF principale est un singleton modeless qui persiste pendant la session Revit
- [x] **INFRA-07**: Les ViewModels n'importent aucun type Revit API — communication via DTOs uniquement
- [x] **INFRA-08**: CommunityToolkit.Mvvm 8.4.2 est utilise pour ObservableObject, RelayCommand, ObservableProperty
- [x] **INFRA-09**: Le build produit des assemblies chargeables dans Revit 2024, 2025 et 2026 sans erreur

### Scene Active

- [x] **SCENE-01**: L'utilisateur peut creer une scene active avec un nom personnalise
- [x] **SCENE-02**: L'utilisateur peut switcher entre plusieurs scenes actives via un selecteur
- [x] **SCENE-03**: L'utilisateur peut ajouter des familles/types a la scene via un mode liste (dropdown famille puis type)
- [ ] **SCENE-04**: L'utilisateur peut ajouter des elements a la scene via un clic dans la vue 3D (PickObject via IExternalEventHandler)
- [x] **SCENE-05**: L'utilisateur peut retirer un type de la scene active
- [x] **SCENE-06**: Le panneau gauche affiche un TreeView des familles/types de la scene active
- [x] **SCENE-07**: Le TreeView trie les Murs et Sols en tete, le reste en ordre alphabetique
- [x] **SCENE-08**: La selection d'un type dans le TreeView met a jour le panneau centre
- [ ] **SCENE-09**: La vue 3D active est validee avant d'autoriser la selection par clic

### Couches et Parametres

- [x] **LAYER-01**: Pour un type a couches (mur, sol, toit, plafond), le panneau centre affiche la liste des couches CompoundStructure
- [x] **LAYER-02**: Chaque couche affiche sa fonction, son epaisseur et le materiau actuellement assigne
- [x] **LAYER-03**: Pour une famille chargee sans couches, le panneau centre affiche la liste des parametres de type Material
- [x] **LAYER-04**: L'utilisateur peut selectionner une ou plusieurs couches/parametres dans le panneau centre
- [x] **LAYER-05**: La selection multiple est supportee (Ctrl+clic, Shift+clic)

### Presets et Set Mat

- [x] **PRESET-01**: Le panneau droit affiche une liste de materiaux preset organises par groupes
- [x] **PRESET-02**: Trois groupes par defaut existent : Murs, Sols, Autres
- [ ] **PRESET-03**: L'utilisateur peut creer des groupes de preset personnalises
- [x] **PRESET-04**: L'utilisateur peut ajouter un materiau du projet a un groupe de preset
- [x] **PRESET-05**: Les presets sont persistes dans un fichier JSON dont le chemin est choisi par l'utilisateur
- [x] **PRESET-06**: Le chemin du fichier JSON est memorise et reutilise automatiquement aux sessions suivantes
- [x] **PRESET-07**: L'utilisateur peut dupliquer un materiau preset (nom automatique "[Original] copie")
- [x] **PRESET-08**: Le bouton Set Mat applique le materiau preset selectionne aux couches selectionnees via Transaction Revit
- [x] **PRESET-09**: Pour les familles sans couches, Set Mat permet a l'utilisateur de choisir quel parametre Material modifier
- [x] **PRESET-10**: Set Mat gere le rollback en cas d'erreur et affiche un message utilisateur

### Edition de Materiau

- [ ] **MATEDIT-01**: Le visualisateur affiche le nom, la description, le motif/couleur de surface et la teinte d'apparence du materiau selectionne
- [ ] **MATEDIT-02**: L'utilisateur peut editer le nom du materiau en live via Transaction Revit
- [ ] **MATEDIT-03**: L'utilisateur peut editer la description du materiau en live via Transaction Revit
- [ ] **MATEDIT-04**: L'utilisateur peut editer le motif et la couleur de premier plan (onglet Graphique) via Transaction Revit
- [ ] **MATEDIT-05**: L'utilisateur peut activer/desactiver la teinte d'apparence et modifier la couleur RVB via AppearanceAssetEditScope
- [ ] **MATEDIT-06**: Une preview du materiau est affichee (thumbnail existant ou fallback image coloree)
- [ ] **MATEDIT-07**: La preview se rafraichit apres chaque modification de materiau
- [ ] **MATEDIT-08**: Les cas sans AppearanceAsset sont geres gracieusement (teinte non disponible)

### Interface et Theme

- [x] **UI-01**: La fenetre principale utilise un layout trois colonnes (familles | couches | materiaux)
- [x] **UI-02**: Un theme sombre Olympe est applique via ResourceDictionary (fond ~#1E1E1E, accent ambre/orange)
- [ ] **UI-03**: Tous les controles WPF sont styles de maniere coherente (boutons, listes, TreeView, scrollbars)
- [x] **UI-04**: L'interface est entierement en francais
- [ ] **UI-05**: Le bouton Set Mat est visuellement proemirent et centre entre les panneaux centre et droit

### Deploiement

- [ ] **DEPLOY-01**: Un installer .exe est genere via WiX v5
- [ ] **DEPLOY-02**: L'installer detecte les versions de Revit installees sur la machine
- [ ] **DEPLOY-03**: L'utilisateur peut choisir pour quelle(s) version(s) de Revit installer l'add-in
- [ ] **DEPLOY-04**: L'installer copie les assemblies et le fichier .addin dans le bon dossier selon la version choisie
- [ ] **DEPLOY-05**: L'installer fonctionne correctement sur Windows 10 et Windows 11

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Ameliorations UX

- **UX-01**: Drag & drop de materiaux depuis le panneau preset vers une couche
- **UX-02**: Recherche/filtre dans la liste des materiaux preset
- **UX-03**: Historique d'annulation des Set Mat (undo stack)
- **UX-04**: Raccourcis clavier pour les actions frequentes

### Fonctionnalites avancees

- **ADV-01**: Import/export de presets entre projets Revit
- **ADV-02**: Templates de scenes predefinies par type de projet
- **ADV-03**: Comparaison cote-a-cote de deux materiaux
- **ADV-04**: Batch apply : appliquer un materiau a tous les types d'une categorie en un clic

### Internationalisation

- **I18N-01**: Support multi-langue (anglais, allemand)
- **I18N-02**: Detection automatique de la langue Revit

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Rendu realiste de la preview materiau | API limitee, complexite disproportionnee -- fallback image coloree suffisant |
| Persistance des scenes actives entre sessions | Complexite de serialisation des ElementId, pas prioritaire pour v1 |
| Cloud build / CI/CD | Developpement 100% local, pas necessaire pour un add-in desktop |
| Support Revit < 2024 | API trop ancienne, ElementId 32-bit, pas de marche suffisant |
| Base de donnees externe | Surdimensionne -- JSON presets + memoire Revit suffisent |
| Application mobile ou web | Desktop WPF uniquement, pas de use case pour mobile/web |
| Chat en temps reel ou collaboration | Outil individuel, pas un outil d'equipe |
| Marketplace Autodesk publishing | Peut etre considere en v2+ si le produit est valide |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| INFRA-01 | Phase 1 | Complete |
| INFRA-02 | Phase 1 | Complete |
| INFRA-03 | Phase 1 | Complete |
| INFRA-04 | Phase 1 | Complete |
| INFRA-05 | Phase 1 | Complete |
| INFRA-06 | Phase 1 | Complete |
| INFRA-07 | Phase 1 | Complete |
| INFRA-08 | Phase 1 | Complete |
| INFRA-09 | Phase 1 | Complete |
| SCENE-01 | Phase 2 | Complete |
| SCENE-02 | Phase 2 | Complete |
| SCENE-03 | Phase 2 | Complete |
| SCENE-04 | Phase 4 | Pending |
| SCENE-05 | Phase 2 | Complete |
| SCENE-06 | Phase 2 | Complete |
| SCENE-07 | Phase 2 | Complete |
| SCENE-08 | Phase 2 | Complete |
| SCENE-09 | Phase 4 | Pending |
| LAYER-01 | Phase 2 | Complete |
| LAYER-02 | Phase 2 | Complete |
| LAYER-03 | Phase 2 | Complete |
| LAYER-04 | Phase 2 | Complete |
| LAYER-05 | Phase 2 | Complete |
| PRESET-01 | Phase 3 | Complete |
| PRESET-02 | Phase 3 | Complete |
| PRESET-03 | Phase 3 | Pending |
| PRESET-04 | Phase 3 | Complete |
| PRESET-05 | Phase 3 | Complete |
| PRESET-06 | Phase 3 | Complete |
| PRESET-07 | Phase 3 | Complete |
| PRESET-08 | Phase 3 | Complete |
| PRESET-09 | Phase 3 | Complete |
| PRESET-10 | Phase 3 | Complete |
| MATEDIT-01 | Phase 4 | Pending |
| MATEDIT-02 | Phase 4 | Pending |
| MATEDIT-03 | Phase 4 | Pending |
| MATEDIT-04 | Phase 4 | Pending |
| MATEDIT-05 | Phase 4 | Pending |
| MATEDIT-06 | Phase 4 | Pending |
| MATEDIT-07 | Phase 4 | Pending |
| MATEDIT-08 | Phase 4 | Pending |
| UI-01 | Phase 1 | Complete |
| UI-02 | Phase 1 | Complete |
| UI-03 | Phase 5 | Pending |
| UI-04 | Phase 1 | Complete |
| UI-05 | Phase 3 | Pending |
| DEPLOY-01 | Phase 5 | Pending |
| DEPLOY-02 | Phase 5 | Pending |
| DEPLOY-03 | Phase 5 | Pending |
| DEPLOY-04 | Phase 5 | Pending |
| DEPLOY-05 | Phase 5 | Pending |

**Coverage:**
- v1 requirements: 51 total
- Mapped to phases: 51
- Unmapped: 0

---
*Requirements defined: 2026-04-11*
*Last updated: 2026-04-11 after roadmap creation*
