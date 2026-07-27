# Deux jeux de manifests .addin (PKG-07)

- `addin\` (ce dossier) : jeu **dev** pour le protocole de deploiement manuel (UPDATE_PROTOCOL.md) — `<Assembly>` pointe vers `..\src\...\bin\Release\`.
- `installer\OlympeMaterialManager.Installer\addin\` : jeu **source du MSI** — `<Assembly>` est reecrit a l'installation via util:XmlFile vers `[INSTALLFOLDER]` (PKG-06).
- Seul `<Assembly>` doit differer entre les deux jeux : **`AddInId` et `FullClassName` doivent rester strictement synchronises** (verifie le 2026-07-27 : identiques).
