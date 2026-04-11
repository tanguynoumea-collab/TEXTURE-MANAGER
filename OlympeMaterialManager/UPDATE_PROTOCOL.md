# Protocole de mise a jour — Olympe MaterialManager

## Problemes identifies

1. **Cache shadow copy Revit** : Revit copie les DLLs dans un dossier temporaire au demarrage et ne relit jamais l'original pendant la session.
2. **Cache AddInsSettings.json** : Revit memorise les metadonnees des add-ins et peut ignorer les changements.
3. **dotnet publish --no-restore** : Peut ne pas recompiler si MSBuild pense que les outputs sont a jour (timestamps).
4. **Revit.exe en arriere-plan** : Verrouille la DLL, le publish echoue silencieusement ou copie une ancienne version.

## Protocole obligatoire a chaque mise a jour

### Commande unique a executer :

```bash
# 1. Verifier que Revit est ferme
tasklist 2>/dev/null | grep -i "Revit.exe" && echo "ERREUR: Fermer Revit d'abord!" && exit 1

# 2. Incrementer la version (evite le cache)
# Modifier Version dans le .csproj si necessaire

# 3. Supprimer TOUS les artefacts de build
rm -rf OlympeMaterialManager/src/OlympeMaterialManager/bin
rm -rf OlympeMaterialManager/src/OlympeMaterialManager/obj

# 4. Supprimer les caches Revit
rm -rf "$LOCALAPPDATA/Autodesk/Revit/Autodesk Revit 2025/Extensions" 2>/dev/null
rm -f "$APPDATA/Autodesk/Revit/Autodesk Revit 2025/AddinsData/AddInsSettings.json" 2>/dev/null

# 5. Build propre + publish
cd OlympeMaterialManager/src/OlympeMaterialManager
dotnet publish -c Release -f net8.0-windows

# 6. Verifier la DLL
ls -la bin/Release/net8.0-windows/publish/OlympeMaterialManager.dll
date

# 7. Verifier le contenu (spot check)
cat bin/Release/net8.0-windows/publish/OlympeMaterialManager.dll | tr -d '\0' | grep -o "MARQUEUR_UNIQUE" | head -1
```

### Verification post-lancement Revit :

```bash
# Verifier dans le journal que la bonne version est chargee
grep "OlympeMaterialManager" "$LOCALAPPDATA/Autodesk/Revit/Autodesk Revit 2025/Journals/$(ls -t $LOCALAPPDATA/Autodesk/Revit/Autodesk\ Revit\ 2025/Journals/ | head -1)" | grep "Assembly Version"
```

## Script automatise

Utiliser la commande `/deploy` dans Claude Code qui execute tout le protocole.
