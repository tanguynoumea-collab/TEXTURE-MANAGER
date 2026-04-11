# Phase 1: Foundation and Infrastructure - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-11
**Phase:** 01-foundation-and-infrastructure
**Areas discussed:** Projet structure, Theme Olympe, Layout trois colonnes, ExternalEvent pattern
**Mode:** Auto (all recommended defaults selected)

---

## Projet Structure

| Option | Description | Selected |
|--------|-------------|----------|
| Single SDK csproj multi-target | Pattern moderne, net48+net8.0-windows dans un seul .csproj | auto |
| Shared Project + 2 cibles | Pattern classique, .shproj avec net48 et net8.0-windows separement | |
| Shared Project + 3 cibles | Un .shproj + un projet par version Revit (original brief) | |

**User's choice:** Single SDK csproj multi-target (recommended by Stack research)
**Notes:** Architecture research recommandait Shared Project mais Stack research le contredit avec le pattern moderne. Decision: essayer single csproj d'abord, fallback Shared Project si WPF XAML cross-framework echoue.

---

## Theme Olympe

| Option | Description | Selected |
|--------|-------------|----------|
| Dark standard avec accent ambre | #1E1E2E fond, #FF9800 accent, VS Code-like | auto |
| Dark Material Design | Material palette dark avec orange 600 | |
| Dark minimal | Noir pur #000000, accent plus subtil | |

**User's choice:** Dark standard avec accent ambre (recommended — professionnel et lisible)
**Notes:** L'utilisateur a dit "theme dark classique, je te fais confiance" lors du questioning initial.

---

## Layout Trois Colonnes

| Option | Description | Selected |
|--------|-------------|----------|
| GridSplitter redimensionnable | 250/*/250 avec splitters | auto |
| Proportions fixes | Colonnes a largeur fixe, pas de redimensionnement | |
| DockPanel | Panneaux lateraux ancres, centre auto-fill | |

**User's choice:** GridSplitter redimensionnable (recommended — standard pour editeurs multi-panneaux)
**Notes:** MinWidth sur chaque panneau pour eviter l'ecrasement.

---

## ExternalEvent Pattern

| Option | Description | Selected |
|--------|-------------|----------|
| Enum dispatch unique | Un handler, un ExternalEvent, enum de routing | auto |
| Handlers specialises | Un IExternalEventHandler par operation | |
| Async ExternalEvent (Toolkit) | Nice3point AsyncExternalEvent<T> avec typed responses | |

**User's choice:** Enum dispatch unique (recommended by Architecture research)
**Notes:** Nice3point Toolkit ExternalEventHandler<T> a evaluer comme implementation alternative du meme pattern.

---

## Claude's Discretion

- Folder structure interne du projet
- Versions exactes des NuGet packages
- Nice3point.Revit.Extensions usage
- Unit test framework

## Deferred Ideas

None
