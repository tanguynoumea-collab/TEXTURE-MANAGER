# Palettes candidates — refonte chromatique (cycle 4)

Contexte : l'utilisateur juge la combinaison actuelle (fond #1E1E2E / accent ambre #FF9800) peu satisfaisante.
Principe directeur retenu : l'app est un **nuancier** — l'écrin doit être neutre et l'accent doit être une teinte
qu'aucun matériau de construction ne porte (l'ambre actuel entre en compétition avec bois, briques, terracotta).
Coût d'implémentation : un seul fichier (`Themes/OlympeTheme.xaml`), tout passe par des tokens depuis le cycle 1.

## ★ Candidat retenu en mémoire (2026-07-28) — « Acier & Azur » (proposition 4)

Neutre gris-bleu sobre, accent bleu : s'assoit naturellement à côté de Revit ; bleu = convention
« sélection » Windows/Revit, lecture immédiate. Réserve notée : le moins distinctif du premier lot.

| Rôle | Hex |
|---|---|
| Fond | `#111827` |
| Surface | `#1F2937` |
| Surface survol | `#374151` |
| Bordure | `#374151` |
| Accent | `#60A5FA` (texte sombre `#111827` dessus, ≈ 7:1) |
| Accent survol | `#93C5FD` |
| Texte primaire | `#F3F4F6` |
| Texte secondaire | `#9CA3AF` |

## Premier lot (thèmes sombres) — présenté le 2026-07-28

1. Ardoise & Cyan (`#0F172A` / `#22D3EE`) — zéro collision, le plus sûr
2. Graphite & Chaux (`#18181B` / `#A3E635`) — neutre achromatique, meilleur écrin (recommandation Daedalus)
3. Encre & Or pâle (`#171B26` / `#E3B341`) — évolution douce, conserve le défaut de collision
4. **Acier & Azur** (`#111827` / `#60A5FA`) — ★ retenu en mémoire
5. Basalte & Violet (`#1C1917` / `#A78BFA`) — neutre chaud, le plus mémorable

## Second lot (clairs et hybrides) — présenté le 2026-07-28

Argument métier : les matériaux sombres (acier, béton foncé) ont un liseré peu lisible sur fond sombre ;
un fond clair ou mi-ton les fait tous ressortir. Contre-argument : fenêtre claire flottant au-dessus d'un
Revit sombre = éblouissement en va-et-vient.

6. Papier & Ardoise (`#FAFAF9` / `#334155`) — clair chaud, planche d'architecte
7. Craie & Cobalt (`#F1F5F9` / `#2563EB`) — clair froid, plan technique
8. Lin & Sapin (`#F5F3EF` / `#15803D`) — clair chaud beige, atelier
9. Chrome & Plan (barres `#1F2937` + espace `#F3F4F6` / `#2563EB`) — hybride, reprend l'azur retenu
10. Gris moyen (`#D6D3D1` / `#1E40AF`) — standard des professionnels de la couleur (jugement fidèle)

## Troisième lot (accents sourds) — présenté le 2026-07-28

Directive utilisateur : « des couleurs principales moins contrastées avec le reste ». Objectif produit servi :
en assourdissant l'accent, les couleurs de matériaux deviennent les éléments les plus saillants de l'écran.
Réserve à surveiller : un accent trop sourd affaiblit aussi le repérage de l'action primaire et la
visibilité du focus clavier (corrigé en cycle 1, UI-C1) — compenser par taille, position et bordure de focus.

11. Azur poudré (`#131A26` / `#6F93BF`) — Acier & Azur assourdi, calme par proximité de teinte
12. Graphite & Sauge (`#1A1A1A` / `#8FAE94`) — calme par désaturation, le neutre le plus fidèle
13. Chrome & Plan sourd (chrome `#232B36`, espace `#EEF1F4`, accent `#4E6E96`, bouton teinté `#DCE6F0`) — calme par traitement du bouton
14. Gris moyen & Bleu fumée (`#C9C9C7` / `#4A5A70`) — calme par proximité de valeur, jugement colorimétrique fidèle
15. Brume & Ardoise (`#EDF0F3` / `#5B7085`) — clair tout en retenue

## ✅ DÉCISION (2026-07-28) — proposition 12 « Graphite & Sauge », en DEUX thèmes commutables

Choix utilisateur : partir de la 12 (neutre achromatique, accent sauge sourd) et en décliner une
version claire, avec un bouton de bascule. Réalise du même coup le candidat C5 de la roadmap.

### Thème sombre — Graphite & Sauge
| Rôle | Hex |
|---|---|
| Fond fenêtre | `#1A1A1A` |
| Surface panneau | `#262626` |
| Carte | `#2B2B2B` (sans bordure) |
| Survol | `#333333` · Bordure `#363636` |
| Accent | `#8FAE94` (texte `#151515` dessus) |
| Texte | `#E5E5E5` / secondaire `#9E9E9E` |
| Erreur | `#E88B8B` |

### Thème clair — Craie & Sauge (miroir de la logique de profondeur)
| Rôle | Hex |
|---|---|
| Fond fenêtre | `#EFEFEF` |
| Surface panneau | `#FAFAFA` |
| Carte | `#FFFFFF` (bordure `#E3E3E3`) |
| Survol | `#E7E7E7` · Bordure `#D8D8D8` |
| Accent | `#5F8168` (texte blanc dessus) · statut/texte accentué `#4A6852` |
| Texte | `#1C1C1C` / secondaire `#616161` |
| Erreur | `#B33232` |

### Règles actées avec ce lot
1. **Aucun emoji couleur dans l'UI** (retour utilisateur : logos invisibles en thème clair). Tous les
   glyphes sont monochromes et héritent du `Foreground` (Path XAML ou police d'icônes système) — la
   pipette 💧 actuelle doit être remplacée.
2. **Miroir de profondeur** : dans les deux thèmes, fenêtre → panneaux → cartes vont du plus « lointain »
   au plus proche ; l'ordre des valeurs s'inverse, la hiérarchie non.
3. **Accent sourd assumé** : compensation du repérage de l'action primaire par la position (barre dédiée)
   et maintien d'un anneau de focus un cran plus soutenu que l'accent (UI-C1 du cycle 1).
4. **Bascule instantanée** : les brushes doivent passer de `StaticResource` à `DynamicResource`
   (coût principal du lot) ; choix persisté dans les settings comme le mode d'aperçu.
5. Bouton de bascule : barre d'en-tête, à droite d'« Ouvrir/Migrer », 32×32, glyphe de destination
   (☀ en thème sombre), tooltip + AutomationProperties.
