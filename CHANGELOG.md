# Changelog — Olympe MaterialManager

Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/). Versions en cycle alpha.

## [1.6.0] — 2026-07-28 — « Alpha 1.6 : gestes rapides et nouvelle identité »

Deux cycles réunis : les gestes qui font gagner du temps au quotidien, et une refonte complète de l'apparence de l'add-in.

### Ajouté

- **Pipette** : un bouton dans le panneau des presets, un clic sur un élément de la vue 3D, et tous ses matériaux rejoignent votre preset — rangés dans le groupe sélectionné, ou dans un groupe « Autres » créé au besoin. Les doublons sont ignorés automatiquement. L'ajout par la liste reste disponible.
- **Glisser-déposer un matériau sur une couche** : attrapez un matériau dans le panneau de droite et lâchez-le sur une carte du panneau central pour l'appliquer directement. Si plusieurs couches sont sélectionnées et que vous déposez sur l'une d'elles, **toutes** reçoivent le matériau — en une seule opération annulable d'un seul Ctrl+Z.
- **Alerte des matériaux introuvables** : à l'ouverture d'un preset sur une maquette qui ne contient pas ses matériaux, la liste complète des manquants s'affiche, avec le choix de les conserver ou de les retirer du preset (confirmation explicite, le preset étant un fichier partagé).
- **Deux thèmes, clair et sombre**, commutables par un bouton dans la barre d'en-tête ; le choix est mémorisé entre les sessions.
- **Nouveau logo** : une palette de peintre, déclinée en versions claire et sombre — l'add-in choisit la bonne selon le thème de votre Revit.
- **Intégration continue** : chaque version est désormais reconstruite et testée automatiquement sur une machine neuve, avec l'installeur produit en artefact.

### Modifié

- **Nouvelle palette « Graphite & Sauge »** : un fond neutre sans dominante et un accent vert sourd, choisis pour que les couleurs de vos matériaux soient les éléments les plus visibles de l'écran — l'ambre précédent entrait en concurrence avec les bois et les terres cuites.
- Les icônes de l'interface sont désormais des tracés qui suivent la couleur du texte : elles restent lisibles dans les deux thèmes.
- Le sélecteur d'aperçu Couleur / Réaliste est remonté en haut à droite du panneau des presets, visible en permanence.
- Les pastilles des matériaux preset suivent le mode d'aperçu, comme les liserés : leurs couleurs sont rafraîchies depuis la maquette à chaque ouverture de preset.

### Corrigé

- **Les couleurs et textures s'affichent dès l'ouverture** de la fenêtre en mode Réaliste — il fallait auparavant basculer de mode pour les faire apparaître. La bibliothèque de textures est également préparée dès le démarrage de Revit.
- L'icône du ruban était affichée trop grande et rognée.
- Robustesse : le presse-papiers réessaie en cas d'échec, l'absence de document actif est signalée clairement, et l'alerte des matériaux introuvables ne peut plus s'afficher au mauvais moment.

## [1.5.0] — 2026-07-28 — « Alpha 1.5 : visualisation des matériaux »

Cycle de fonctionnalités validé par le pipeline complet (council technique, plan de design, design-review en 2 itérations sur données réelles).

### Ajouté

- **Liseré matériau sur les cartes de couches** : chaque carte du panneau central porte une bande de couleur de 6 px **des deux côtés**, reprenant la couleur du matériau associé — le panneau devient un nuancier vivant, mis à jour à l'instant où un matériau est appliqué. Les pastilles des presets suivent le même principe.
- **Deux modes d'aperçu : Couleur │ Réaliste**, mémorisés entre les sessions. « Couleur » affiche la couleur graphique (vue Couleurs cohérentes) ; « Réaliste » affiche la **couleur d'apparence** du matériau (onglet Apparence — celle de la vue 3D Réaliste). Le mode pilote l'aperçu, les pastilles et le liseré. Matériau sans apparence → retombée expliquée (« Pas d'apparence — couleur graphique »).
- **Visualisateur de matériau** (ex-« Éditeur », l'édition reste disponible) avec bouton **« Ouvrir dans Revit »** : ouvre le gestionnaire de matériaux natif, le nom du matériau est copié automatiquement (collez-le dans la recherche du gestionnaire) — avec message de confirmation et repli robuste si le presse-papiers est occupé.
- **Recherche dans les panneaux** types et matériaux : filtrage instantané insensible aux accents (« beton » trouve « Béton »), état « aucun résultat » avec effacement en un clic. La recherche est sûre : elle ne modifie jamais les fichiers de presets partagés.

### Modifié

- Le mode « Texture » (aperçu par image de texture) a été retiré après diagnostic sur données réelles : les bibliothèques Revit courantes n'exposent pas de bitmaps exploitables (placeholders, chemins d'autres machines). La couleur d'apparence du mode Réaliste rend le service attendu. Un rendu réaliste complet reste envisageable plus tard.
- Diagnostic de terrain : la résolution des couleurs d'apparence est tracée dans le journal du plugin.

### Corrigé

- Durcissements issus de la re-vérification du council : protection contre les gels réseau (chemins UNC), caches invalidés au changement de document, garde anti-récursion sur les assets, constantes d'API typées, anti double-clic sur l'ouverture du gestionnaire.

## [1.4.0] — 2026-07-28 — « Alpha 1.4 : cycle d'audit complet »

Version issue d'un audit complet (UI + code, 9 auditeurs + revue Daedalus) suivi de la correction de 54 findings. Aucune fonctionnalité nouvelle : fiabilité, sécurité des données et finition de l'interface.

### Corrigé

- **Vos presets et scènes ne peuvent plus être perdus silencieusement** : un fichier corrompu ou verrouillé (OneDrive, antivirus) est mis en quarantaine (`.corrupt-<date>`) avec message explicite, et la sauvegarde automatique est bloquée tant que le chargement n'a pas réussi — auparavant le fichier était écrasé par une collection vide au premier clic.
- **Toutes les sauvegardes sont désormais atomiques** avec copie de secours `.bak` : un crash en pleine écriture ne peut plus tronquer un fichier.
- **L'installeur MSI livrait un add-in non fonctionnel sur Revit 2025 et 2026** (bibliothèques manquantes) — corrigé et vérifié ; l'installeur n'embarque plus non plus de fichiers périmés.
- **Les mises à jour de l'installeur fonctionnent** : la version (1.4.0) est désormais unique et partagée entre le programme, l'assembly et le MSI — installer une nouvelle alpha remplace proprement l'ancienne.
- **L'add-in s'ouvre même si le répertoire de projet est devenu inaccessible** (lecteur réseau débranché, OneDrive) : il propose d'en choisir un autre au lieu de refuser de démarrer.
- **Un échec d'édition de matériau n'est plus silencieux** (nom en doublon, document en lecture seule) : message clair et champs resynchronisés avec l'état réel de Revit.
- **Appliquer un preset venant d'un autre projet ne peut plus toucher le mauvais matériau** : le matériau est validé par identifiant ET par nom, avec re-résolution par nom si besoin, sinon refus explicite.
- **Les erreurs d'écriture disque** (disque plein, fichier verrouillé) sont interceptées avec message — plus aucun risque de faire planter Revit sur une sauvegarde.
- Boutons qui restaient bloqués si Revit refusait une requête ; surbrillance verte de sélection qui pouvait rester collée dans le modèle sans signalement ; teinte silencieusement inopérante sur les matériaux PBR (message explicite désormais).
- **Les noms invalides ne cassent plus rien** (« Sol/Béton », caractères interdits, noms réservés Windows) : validation à la saisie et à l'import, protection contre l'écriture hors des dossiers de l'application.
- **Import de presets/scènes externes** : le fichier est validé avant import et l'écrasement d'un homonyme demande confirmation.
- Migration du répertoire de projet : garde contre le choix d'un dossier imbriqué dans l'ancien (boucle de copie infinie).
- Gel possible de Revit sur les gros modèles lors du chargement des types (un seul scan du document au lieu d'un par type).

### Interface

- **Navigation clavier réelle** : le focus est visible sur tous les contrôles (liseré pointillé ambre).
- **Hiérarchie visuelle clarifiée** : l'accent ambre est réservé à l'action principale « Appliquer le matériau », aux sélections et au feedback ; les boutons de création sont discrets.
- **Contraste corrigé** sur le bouton principal (texte sombre sur ambre, ratio ≈ 7:1).
- Boutons « + » / « − » / « … » agrandis à 32 px ; les boutons de suppression se distinguent au survol (liseré rouge).
- **Accents français restaurés** dans toute l'interface (« Matériau appliqué ! », « Sélectionnez un type… »).
- **La taille et la position de la fenêtre sont mémorisées** entre les sessions.
- Le bouton « Ajouter par clic dans la vue » indique clairement quand la sélection 3D est en cours.
- Dialogue d'ajout de matériau : champ de recherche avec invite « Rechercher un matériau… » et état « aucun résultat ».
- Confirmation avant « Supprimer du preset » (cohérent avec les autres suppressions).
- Noms d'accessibilité sur tous les boutons à glyphe (lecteurs d'écran, pilotage automatisé).

### Modifié

- **Installation pour tous les utilisateurs du poste** : les manifests d'add-in sont déposés dans ProgramData (plus de dépendance au profil de l'installateur) et les chemins d'installation sont résolus dynamiquement (installation sur un autre lecteur possible).
- Nettoyage interne : ~1 000 lignes de code mort supprimées (ancien aperçu sphère, sélecteur par listes déroulantes abandonné), pont Revit réorganisé en modules, documentation projet (CLAUDE.md) réalignée sur la réalité.

### Ajouté

- **Suite de tests automatisés** (37 tests : persistance, corruption, sanitisation, migration) — le projet n'en avait aucun.
- Analyseurs de code Roslyn actifs avec zéro avertissement, style verrouillé par `.editorconfig`.
- Fichiers de données versionnés (`SchemaVersion`) pour préparer les évolutions futures du format.

## [1.3.0] — 2026 — « Alpha 1.3 »

- Correction couleur matériau + carré preset + compatibilité Revit 2023-2026.

## [1.2.0] — « Alpha 1.2 »

- Sélection 3D avec surbrillance verte persistante.

## [1.0.0] — « Alpha 1.0 »

- Installeur WiX MSI pour Revit 2023-2026.
- Trois panneaux : familles/types, couches, matériaux preset. Scènes, presets, éditeur de matériau, mode sélection 3D.
