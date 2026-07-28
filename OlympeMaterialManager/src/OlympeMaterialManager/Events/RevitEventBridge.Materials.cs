using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Partie Materials (ARC-02/MAINT-03) : handlers d'ecriture sur les materiaux —
/// application aux couches/parametres, duplication, edition (nom, description,
/// couleur, teinte). Chaque handler ouvre sa Transaction avec rollback systematique.
/// </summary>
public partial class RevitEventBridge
{
    /// <summary>
    /// Resout le materiau a appliquer de maniere sure (DON-04).
    /// L'ElementId persiste n'est qu'un cache : sur un autre document, le meme id
    /// peut designer un autre element. Regle : si l'id ne resout pas un Material
    /// portant exactement le nom attendu, re-resolution par nom dans le document ;
    /// si le nom est introuvable, echec propre — jamais d'application silencieuse
    /// d'un materiau dont le nom ne correspond pas.
    /// </summary>
    private static Material ResolveMaterial(Document doc, long materialIdValue, string materialName)
    {
        var matId = ElementIdHelper.FromValue(materialIdValue);

        if (doc.GetElement(matId) is Material byId &&
            (string.IsNullOrEmpty(materialName) || byId.Name == materialName))
        {
            return byId;
        }

        // Id invalide ou nom divergent : re-resolution par nom (comparaison exacte)
        if (!string.IsNullOrEmpty(materialName))
        {
            var byName = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => m.Name == materialName);
            if (byName != null)
                return byName;
        }

        throw new InvalidOperationException(
            $"Le matériau '{materialName}' n'existe pas dans ce document.");
    }

    /// <summary>
    /// Applique un materiau aux couches CompoundStructure selectionnees (D-16, D-22).
    /// Pattern Get-Modify-Set : GetCompoundStructure retourne une COPIE.
    /// </summary>
    private static void HandleSetMaterialOnLayers(UIApplication uiApp, SetMatRequestDto request)
    {
        var doc = GetActiveDocument(uiApp);
        using var tx = new Transaction(doc, "Olympe : Appliquer matériau aux couches");
        tx.Start();

        try
        {
            var typeId = ElementIdHelper.FromValue(request.TargetTypeIdValue);
            var hostAttrs = doc.GetElement(typeId) as HostObjAttributes;
            if (hostAttrs == null)
                throw new InvalidOperationException("Le type sélectionné n'est pas un type à couches.");

            // COPY -- must call SetCompoundStructure() to persist
            var cs = hostAttrs.GetCompoundStructure();
            if (cs == null)
                throw new InvalidOperationException("Le type n'a pas de structure composée.");

            // DON-04 : valider id + nom avant application (jamais de materiau silencieusement errone)
            var matId = ResolveMaterial(doc, request.MaterialIdValue, request.MaterialName).Id;

            foreach (int layerIndex in request.LayerIndices)
            {
                cs.SetMaterialId(layerIndex, matId);
            }

            hostAttrs.SetCompoundStructure(cs); // PERSISTS changes
            tx.Commit();
        }
        catch
        {
            if (tx.HasStarted() && !tx.HasEnded())
                tx.RollBack();
            throw;
        }
    }

    /// <summary>
    /// Applique un materiau aux parametres materiaux selectionnes (D-17).
    /// Batch de tous les parametres dans une seule Transaction (un seul undo step).
    /// </summary>
    private static void HandleSetMaterialOnParameter(UIApplication uiApp, SetMatParamRequestDto request)
    {
        var doc = GetActiveDocument(uiApp);
        using var tx = new Transaction(doc, "Olympe : Appliquer matériau aux paramètres");
        tx.Start();

        try
        {
            var typeId = ElementIdHelper.FromValue(request.TargetTypeIdValue);
            var element = doc.GetElement(typeId);
            // DON-04 : valider id + nom avant application (jamais de materiau silencieusement errone)
            var matId = ResolveMaterial(doc, request.MaterialIdValue, request.MaterialName).Id;

            foreach (string paramName in request.ParameterDefinitionNames)
            {
                var param = element.LookupParameter(paramName);
                if (param == null || param.IsReadOnly)
                    throw new InvalidOperationException(
                        $"Le paramètre '{paramName}' est introuvable ou en lecture seule.");

                bool success = param.Set(matId);
                if (!success)
                    throw new InvalidOperationException(
                        $"Échec de l'assignation du matériau au paramètre '{paramName}'.");
            }

            tx.Commit();
        }
        catch
        {
            if (tx.HasStarted() && !tx.HasEnded())
                tx.RollBack();
            throw;
        }
    }

    /// <summary>
    /// Duplique un materiau Revit avec nom automatique "[Original] copie" (D-23).
    /// Gere les collisions de nom (copie 2, copie 3...).
    /// Note : les AppearanceAssets sont partages par reference (acceptable pour Phase 3).
    /// </summary>
    private static PresetMaterialDto HandleDuplicateMaterial(UIApplication uiApp, DuplicateMaterialRequestDto request)
    {
        var doc = GetActiveDocument(uiApp);
        using var tx = new Transaction(doc, "Olympe : Dupliquer matériau");
        tx.Start();

        try
        {
            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
            var original = doc.GetElement(matId) as Material;
            if (original == null)
                throw new InvalidOperationException("Matériau source introuvable.");

            string newName = $"{original.Name} copie";
            // Gestion des collisions de nom
            int counter = 2;
            while (new FilteredElementCollector(doc)
                       .OfClass(typeof(Material))
                       .Cast<Material>()
                       .Any(m => m.Name == newName))
            {
                newName = $"{original.Name} copie {counter++}";
            }

            Material duplicate = original.Duplicate(newName);
            tx.Commit();

            return new PresetMaterialDto
            {
                MaterialName = duplicate.Name,
                MaterialElementIdValue = ElementIdHelper.GetValue(duplicate.Id),
                ColorArgb = ExtractColorArgb(duplicate),
                // B10-TX : l'AppearanceAsset est partage par reference -> meme texture
                TexturePath = GetMaterialTexturePath(doc, duplicate)
            };
        }
        catch
        {
            if (tx.HasStarted() && !tx.HasEnded())
                tx.RollBack();
            throw;
        }
    }

    /// <summary>
    /// Renomme un materiau Revit (D-07, D-10).
    /// Transaction individuelle pour granularite undo.
    /// </summary>
    private static void HandleEditMaterialName(UIApplication uiApp, EditMaterialNameRequestDto request)
    {
        var doc = GetActiveDocument(uiApp);
        using var tx = new Transaction(doc, "Olympe : Renommer matériau");
        tx.Start();

        try
        {
            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
            var material = doc.GetElement(matId) as Material;
            if (material == null)
                throw new InvalidOperationException("Matériau introuvable.");

            material.Name = request.NewName;
            tx.Commit();
        }
        catch
        {
            if (tx.HasStarted() && !tx.HasEnded())
                tx.RollBack();
            throw;
        }
    }

    /// <summary>
    /// Modifie la description d'un materiau Revit (D-08, D-10).
    /// Utilise BuiltInParameter.ALL_MODEL_DESCRIPTION.
    /// </summary>
    private static void HandleEditMaterialDescription(UIApplication uiApp, EditMaterialDescriptionRequestDto request)
    {
        var doc = GetActiveDocument(uiApp);
        using var tx = new Transaction(doc, "Olympe : Modifier description matériau");
        tx.Start();

        try
        {
            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
            var material = doc.GetElement(matId) as Material;
            if (material == null)
                throw new InvalidOperationException("Matériau introuvable.");

            var descParam = material.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);
            if (descParam == null || descParam.IsReadOnly)
                throw new InvalidOperationException("Le paramètre description est introuvable ou en lecture seule.");

            descParam.Set(request.NewDescription);
            tx.Commit();
        }
        catch
        {
            if (tx.HasStarted() && !tx.HasEnded())
                tx.RollBack();
            throw;
        }
    }

    /// <summary>
    /// Modifie la couleur de surface (premier plan) d'un materiau Revit (D-09, D-10).
    /// Note : bug connu REVIT-134700 si la couleur correspond a Material.Color.
    /// </summary>
    private static void HandleEditMaterialColor(UIApplication uiApp, EditMaterialColorRequestDto request)
    {
        var doc = GetActiveDocument(uiApp);
        using var tx = new Transaction(doc, "Olympe : Modifier couleur de surface");
        tx.Start();

        try
        {
            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
            var material = doc.GetElement(matId) as Material;
            if (material == null)
                throw new InvalidOperationException("Matériau introuvable.");

            // Modifier la couleur de base du materiau (Material.Color)
            // ET la couleur du motif de surface (SurfaceForegroundPatternColor)
            material.Color = new Color(request.Red, request.Green, request.Blue);
            material.SurfaceForegroundPatternColor = new Color(request.Red, request.Green, request.Blue);
            tx.Commit();
        }
        catch
        {
            if (tx.HasStarted() && !tx.HasEnded())
                tx.RollBack();
            throw;
        }
    }

    /// <summary>
    /// Modifie la teinte (tint) d'un materiau via AppearanceAssetEditScope (D-04, D-05, D-06, D-10).
    /// Utilise common_Tint_toggle et common_Tint_color avec SetValueAsDoubles (pas SetValueAsColor).
    /// </summary>
    private static void HandleEditMaterialTint(UIApplication uiApp, EditMaterialTintRequestDto request)
    {
        var doc = GetActiveDocument(uiApp);
        using var tx = new Transaction(doc, "Olympe : Modifier teinte matériau");
        tx.Start();

        try
        {
            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
            var material = doc.GetElement(matId) as Material;
            if (material == null)
                throw new InvalidOperationException("Matériau introuvable.");

            var assetElemId = material.AppearanceAssetId;
            if (assetElemId == ElementId.InvalidElementId)
                throw new InvalidOperationException("Teinte non disponible : pas d'AppearanceAsset.");

            var assetElem = doc.GetElement(assetElemId) as AppearanceAssetElement;
            if (assetElem == null)
                throw new InvalidOperationException("AppearanceAssetElement introuvable.");

            using (var scope = new AppearanceAssetEditScope(doc))
            {
                Asset editableAsset = scope.Start(assetElemId);

                // ADK-02 : sur les assets PBR modernes, les proprietes generiques
                // common_Tint_* n'existent pas et FindByName retourne null. Echec
                // explicite plutot que no-op silencieux : la transaction rollback,
                // le VM affiche le message et resynchronise l'UI (FIA-05).
                var tintToggle = editableAsset.FindByName(RevitAssetProps.TintToggle)
                    as AssetPropertyBoolean;
                if (tintToggle == null)
                    throw new InvalidOperationException(
                        "La teinte n'est pas modifiable sur ce type de matériau.");
                tintToggle.Value = request.TintEnabled;

                // Couleur de teinte (RGB en doubles normalises 0.0-1.0)
                if (request.TintEnabled)
                {
                    var tintColor = editableAsset.FindByName(RevitAssetProps.TintColor)
                        as AssetPropertyDoubleArray4d;
                    if (tintColor == null)
                        throw new InvalidOperationException(
                            "La teinte n'est pas modifiable sur ce type de matériau.");
                    tintColor.SetValueAsDoubles(new double[]
                    {
                        request.Red / 255.0,
                        request.Green / 255.0,
                        request.Blue / 255.0,
                        1.0 // Alpha
                    });
                }

                scope.Commit(true); // true = forcer la mise a jour des vues
            }

            tx.Commit();
        }
        catch
        {
            if (tx.HasStarted() && !tx.HasEnded())
                tx.RollBack();
            throw;
        }
    }
}
