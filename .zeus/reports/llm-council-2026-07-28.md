# LLM-COUNCIL — Cycle 2 (B8/B9/B10/B5) — 2026-07-28

5 membres indépendants (Pragmatique, Red-teamer, Rigoriste, Premiers principes, Généraliste) → relecture croisée à l'aveugle par 5 relecteurs (qui ont re-vérifié les claims sur le dépôt) → synthèse du Président.
**Classement agrégé** (position moyenne) : Rigoriste 1.0 · Premiers principes 2.2 · Red-teamer 2.8 · Pragmatique 4.0 · Généraliste 5.0. Les relecteurs ont confirmé sur pièces tous les faits déterminants.

## Approche technique retenue

### Socle B10 — le réglage de mode (à faire en premier)
- Enum `PreviewMode { UniformColor, Texture, Realistic }` côté code (3 valeurs dès maintenant, schéma stable).
- Persistance : `public string MaterialPreviewMode { get; set; } = "UniformColor";` dans `AppSettingsDto` — **string + `Enum.TryParse` tolérant, jamais un enum sérialisé** : une `JsonException` sur valeur inconnue enverrait le settings.json entier en quarantaine (mécanisme DON-02 vérifié). Ajout additif → pas de bump `SchemaVersion`. Tests xunit : round-trip, valeur inconnue → défaut, absence → défaut.
- Le mode vit en un point unique et se diffuse par `WeakReferenceMessenger` (`PreviewModeChangedMessage`) — pattern existant.
- **Le bridge ne connaît jamais le mode** : il livre des faits (couleurs, chemins), l'UI décide de la présentation.

### B8 — liseré
- Étendre `LayerDto` **et** `MaterialParamDto` (cohérence des deux types de cartes) : `int? ColorArgb` (null = « Par catégorie » → liseré transparent, jamais un gris menteur) + `string? TexturePath`.
- Remplissage dans `BuildLayerDto` : coût nul (le `Material` y est déjà résolu, `ExtractColorArgb` existe). Mise à jour après application : gratuite via `RefreshLayersMessage` existant.
- **Arbitrage du Président sur le point le plus débattu** (4 positions différentes) : en mode Couleur → `Material.Color` ; en mode Texture → **ImageBrush en miniature tuilée directement dans le liseré** (proposition Premiers principes, jugée la plus élégante par 4 relecteurs sur 5 : elle respecte le « dynamique selon le mode » validé ET évite le piège du « brun boueux » de la couleur moyenne qui rendrait chêne et béton indistincts). Texture introuvable → fallback silencieux `Material.Color`. La couleur moyenne est ABANDONNÉE ; la diffuse d'asset (proposition Rigoriste) reste une option de repli si le motif tuilé s'avère illisible en 5-6 px — à trancher sur captures en design-review.

### Mode Texture — résolution des chemins
- **Restaurer `FindTexturePath` depuis l'historique git (f337be7^)** — la marche récursive sur les assets connectés était la partie saine du code supprimé et couvre une partie des schémas PBR.
- Compléments : chemins multiples séparés par `|` (prendre le premier existant) ; chemins relatifs → sonder les racines Autodesk connues + les chemins additionnels de `Revit.ini` (aucune API publique — assumé en commentaire) ; cache par session ; introuvable → null, jamais d'exception. Conçu comme une fonction **qui a le droit d'échouer** (fallback couleur = chemin nominal).
- Décodage image **côté UI uniquement** (`DecodePixelWidth` 16-64, `Freeze()`, try/catch, cache) — jamais de décodage bitmap sur le thread Revit.

### Mode Réaliste — REPORTÉ en phase 2 (unanimité 5/5)
Restaurer le pipeline sphère supprimé la veille (mutation du document → undo pollué + flag modifié + risques worksets, secondes de gel par matériau, invalidation de cache sans stamp fiable, ré-ouverture du finding SEC-05) est rejeté par tout le conseil. Le réglage reste à 3 valeurs (schéma stable). La présentation UI du 3e mode (bouton grisé « Phase 2 » vs absent) = décision design au checkpoint humain. Si la phase 2 se confirme : cache disque par hash d'asset + rendus regroupés dans une transaction annulée (`RollBack` après export), à la demande explicite — jamais dans le flux du liseré. Noter : B9 donne déjà l'aperçu réaliste natif de Revit en un clic.

### B9 — Visualisateur + ouverture Revit
- Nouveau `RevitRequestType.OpenMaterialsDialog` → `uiApp.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.Materials))` via le bridge (cohérence ExternalEvent). Le dialogue s'ouvre quand Revit reprend la main — à documenter dans le tooltip.
- Pièges retenus : désactiver `Topmost` de la fenêtre au clic si actif (le dialogue modal Revit peut s'ouvrir derrière) ; copier le nom du matériau dans le presse-papiers au clic (palliatif à l'absence de présélection par API).
- **Question produit levée par le Red-teamer (unique au conseil, vérifiée : 6 commandes d'édition dans le VM)** : renommer en « Visualisateur » en conservant l'édition inline est une incohérence de nommage → question posée au checkpoint humain.

### B5 — recherche
- Filtrage 100 % en mémoire, zéro appel Revit. Panneau gauche : `Filter` branché sur la `ListCollectionView` existante (coexiste avec le grouping). Panneau droit : **piège critique vérifié — `PresetGroups` EST la collection persistée par l'AutoSave** ; filtrer en place détruirait le preset sur disque → projection `FilteredGroups` (clones légers référençant les mêmes instances de matériaux), jamais de flag UI dans les DTOs sérialisés.
- Comparaison **insensible aux accents** (`CompareInfo.IndexOf` + `IgnoreCase|IgnoreNonSpace` : « beton » matche « Béton ») — un `Contains(OrdinalIgnoreCase)` serait une faute en UI française. Debounce ~200 ms sur le panneau gauche. État « aucun résultat » : pattern UI-m14 existant réutilisé tel quel.

### Ordre de livraison
B5 (risque nul) → B9 → B10 socle (enum + setting + messenger + sélecteur) → B8 (liseré, dépend du socle). Chaque étape utilisable seule.

## Notes du conseil
- **Accord unanime** : report du Réaliste, string tolérant en settings, DTOs enrichis dans BuildLayerDto, bridge agnostique du mode, filtrage en mémoire.
- **Le vrai désaccord** : la source visuelle du liseré en mode Texture (toujours-couleur / diffuse d'asset / moyenne d'image / motif tuilé). Le Président a choisi le **motif tuilé** (respecte la spec validée + résout le problème de discriminabilité), avec repli « diffuse » si illisible sur captures.
- **Outrepassé** : rien — mais deux questions remontent au checkpoint humain (présentation du mode Réaliste dans l'UI ; édition inline vs « Visualisateur »).
- Idées écartées consignées : couleur moyenne d'image (indiscriminante), restauration immédiate du pipeline de rendu (coûts structurels), table exhaustive des schémas PBR (fragile — fallback générique préféré).
