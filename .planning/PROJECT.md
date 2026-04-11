# Olympe MaterialManager

## What This Is

Add-in Revit WPF permettant aux architectes et concepteurs de gerer visuellement les materiaux de tous les types visibles dans une vue 3D active. Interface a trois panneaux : familles/types (gauche), couches/parametres (centre), materiaux preset (droite). Multi-version Revit 2024, 2025 et 2026.

## Core Value

L'architecte peut appliquer rapidement un materiau preset aux couches ou parametres materiaux de n'importe quel type Revit visible en 3D, en quelques clics depuis un editeur visuel unifie.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Scene Active : creer, nommer, switcher des sous-ensembles de familles/types
- [ ] Ajout de types a la scene via liste (dropdown famille > type) ou clic dans la vue 3D
- [ ] Panneau gauche : TreeView des familles/types de la scene active, tri Murs/Sols en tete
- [ ] Panneau centre : affichage des couches CompoundStructure pour murs/sols/toits/plafonds
- [ ] Panneau centre : affichage des parametres Material pour familles chargees sans couches
- [ ] Selection multiple de couches/parametres dans le panneau centre
- [ ] Panneau droit : liste de materiaux preset organises par groupes (Murs/Sols/Autres + custom)
- [ ] Persistance des presets dans un fichier JSON, chemin choisi par l'utilisateur et memorise
- [ ] Visualisateur materiau : nom, description, motif/couleur surface, teinte apparence, preview sphere
- [ ] Edition live du materiau (nom, description, motif, couleur, teinte) via Transaction Revit
- [ ] Set Mat : appliquer le materiau preset aux couches selectionnees (types a couches)
- [ ] Set Mat : choix du parametre Material a modifier (familles chargees sans couches)
- [ ] Duplication d'un materiau preset avec nom automatique
- [ ] Theme visuel Olympe : palette sombre, accent ambre/orange
- [ ] Installer .exe via WiX avec choix de version Revit (2024/2025/2026)
- [ ] Interface en francais

### Out of Scope

- Persistance des scenes actives entre sessions — session memoire uniquement
- Rendu realiste de la preview materiau — fallback image coloree acceptable
- Cloud build / CI/CD — developpement 100% local
- Support Revit < 2024 — non supporte
- Base de donnees externe — tout en memoire Revit + fichier JSON presets
- Application mobile ou web — desktop WPF uniquement

## Context

- **Ecosystem :** Revit API (Autodesk), marche des add-ins BIM pour architectes
- **Stack decidee :** C# / .NET Framework 4.8 / WPF / MVVM (CommunityToolkit.Mvvm) / Revit API / WiX Toolset v4
- **Architecture solution :** 1 projet partage (Shared) + 3 projets cibles (2024/2025/2026) + 1 projet Installer
- **Thread Revit :** L'UI WPF tourne dans le thread Revit via IExternalCommand, interactions via IExternalEventHandler
- **Commercial a terme :** L'outil pourra etre distribue/vendu, code propre et UX soignee attendus
- **Repo GitHub :** tanguynoumea-collab/TEXTURE-MANAGER

## Constraints

- **Revit API** : Assemblies en reference externe uniquement (CopyLocal = false), pas de thread separe sans Dispatcher
- **MVVM strict** : Pas de code-behind metier, RelayCommand, ObservableCollection, un ViewModel par vue
- **.NET 4.8** : Impose par Revit, pas de .NET Core/5+
- **Multi-version** : Le meme code partage doit compiler contre 3 versions de l'API Revit
- **Fichier .addin** : Requis dans %APPDATA%\Autodesk\Revit\Addins\{version}\ pour l'enregistrement
- **Langue** : Interface utilisateur en francais
- **Nommage** : PascalCase classes/proprietes, _camelCase champs prives

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| CommunityToolkit.Mvvm pour MVVM | Standard moderne, reduit le boilerplate, maintenu activement | -- Pending |
| WiX Toolset v4 pour installer | Standard industrie pour MSI/EXE .NET, gratuit, scriptable | -- Pending |
| Presets JSON avec chemin utilisateur | Flexibilite maximale, pas de dependance a l'emplacement du .rvt | -- Pending |
| Set Mat avec choix parametre pour familles | Les familles chargees peuvent avoir plusieurs parametres Material, l'utilisateur doit choisir | -- Pending |
| Tous types visibles en 3D supportes | Pas seulement murs/sols -- inclut toits, plafonds, familles chargees | -- Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? -> Move to Out of Scope with reason
2. Requirements validated? -> Move to Validated with phase reference
3. New requirements emerged? -> Add to Active
4. Decisions to log? -> Add to Key Decisions
5. "What This Is" still accurate? -> Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check -- still the right priority?
3. Audit Out of Scope -- reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-04-11 after initialization*
