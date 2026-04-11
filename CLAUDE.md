# CLAUDE.md — Olympe MaterialManager

## Contexte
Add-in Revit WPF pour la gestion visuelle des materiaux sur une vue 3D active.
Trois panneaux : Familles/Types (gauche) | Couches (centre) | Materiaux preset (droite).
Multi-version : Revit 2024, 2025, 2026.

## Stack
- C# / .NET Framework 4.8
- WPF + MVVM (CommunityToolkit.Mvvm)
- Revit API (2024 / 2025 / 2026 — CopyLocal = false)
- WiX Toolset v4 (installer .exe)

## Structure solution
OlympeMaterialManager/
├── OlympeMaterialManager.Shared/     <- Logique + ViewModels + Views XAML
├── OlympeMaterialManager.2024/       <- Cible Revit 2024, reference RevitAPI 2024
├── OlympeMaterialManager.2025/       <- Cible Revit 2025, reference RevitAPI 2025
├── OlympeMaterialManager.2026/       <- Cible Revit 2026, reference RevitAPI 2026
├── OlympeMaterialManager.Installer/  <- Projet WiX, genere le .exe
└── CLAUDE.md

## Conventions
- MVVM strict : pas de code-behind metier, RelayCommand, ObservableCollection
- Nommage : PascalCase classes/props, _camelCase champs prives
- Un ViewModel par vue/panneau
- IExternalEventHandler pour toute interaction Revit depuis l'UI
- Langue interface : francais

## Git
- Repo : https://github.com/tanguynoumea-collab/TEXTURE-MANAGER
- Developpement local uniquement
- .gitignore : bin/, obj/, .vs/, *.user, packages/
- Push final apres validation de l'installer

## Statut GSD
[Rempli apres /gsd export en fin de session]
