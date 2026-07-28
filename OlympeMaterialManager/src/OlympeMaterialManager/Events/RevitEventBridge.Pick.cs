using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Partie Pick (ARC-02/MAINT-03) : selection interactive dans la vue 3D —
/// boucle PickObject avec surbrillance verte, et surbrillance des instances
/// d'un type via Selection.SetElementIds.
/// </summary>
public partial class RevitEventBridge
{
    /// <summary>
    /// Selection additive dans la vue 3D via PickObject en boucle.
    /// Chaque clic sur un element ajoute son type a la selection ; un clic sur un
    /// type deja selectionne est ignore (pas de deselection).
    /// Toutes les instances du type sont mises en surbrillance dans la vue Revit.
    /// Echap ou Entree (OperationCanceledException) valide la selection courante.
    /// Retourne List&lt;SceneTypeDto&gt; des types actuellement selectionnes.
    /// CRITIQUE : catch Autodesk.Revit.Exceptions.OperationCanceledException (pas System).
    /// ARC-05 : le hide/show de la fenetre WPF est gere par le ViewModel appelant
    /// (LeftPanelViewModel.AjouterParClic) — le bridge ne touche jamais a la fenetre.
    /// </summary>
    private static List<SceneTypeDto>? HandlePickElementInView(UIApplication uiApp)
    {
        var uiDoc = uiApp.ActiveUIDocument;
        if (uiDoc == null) return null;
        var doc = uiDoc.Document;

        if (uiDoc.ActiveView is not View3D)
            throw new InvalidOperationException("Vue 3D requise pour la sélection par clic.");

        ShowPickInstructions();

        var selectedTypes = new Dictionary<long, SceneTypeDto>();
        var markedElementIds = new List<ElementId>();
        var activeView = uiDoc.ActiveView;
        string? cleanupError = null;

        // FIA-09 : UN seul FilteredElementCollector pour toute la session de pick.
        // Le dictionnaire typeId -> instances est construit une fois et reutilise a
        // chaque clic (l'ancien code relancait un collector complet par type clique).
        var instancesByType = BuildInstancesByTypeMap(doc);

        try
        {
            RunPickLoop(uiDoc, activeView, instancesByType, selectedTypes, markedElementIds);
            LogService.Log($"HandlePickElementInView: validated {selectedTypes.Count} types");
        }
        finally
        {
            // Invariant : les overrides verts sont TOUJOURS nettoyes en sortie de pick,
            // que la boucle se termine par validation ou par exception.
            cleanupError = CleanupGreenOverrides(doc, activeView, markedElementIds);
        }

        // FIA-07 : remonter l'echec de nettoyage via le callback (ErrorMessage cote VM)
        if (cleanupError != null)
            throw new InvalidOperationException(cleanupError);

        return selectedTypes.Values.ToList();
    }

    /// <summary>
    /// Affiche le TaskDialog d'instructions avant la boucle de pick (MAINT-10).
    /// </summary>
    private static void ShowPickInstructions()
    {
        var td = new TaskDialog("Sélection 3D")
        {
            MainInstruction = "Sélection d'éléments dans la vue 3D",
            MainContent = "Cliquez sur les éléments à ajouter à la scène.\n" +
                          "Toutes les occurrences du type seront marquées en vert.\n\n" +
                          "Appuyez sur ÉCHAP pour valider.",
            CommonButtons = TaskDialogCommonButtons.Ok
        };
        td.Show();
    }

    /// <summary>
    /// Boucle PickObject (MAINT-10) : chaque clic ajoute le type de l'element clique
    /// a selectedTypes et marque ses instances en vert ; ECHAP sort de la boucle.
    /// </summary>
    private static void RunPickLoop(
        UIDocument uiDoc,
        View activeView,
        Dictionary<ElementId, List<ElementId>> instancesByType,
        Dictionary<long, SceneTypeDto> selectedTypes,
        List<ElementId> markedElementIds)
    {
        var doc = uiDoc.Document;

        // Override vert pour marquer les elements selectionnes
        var greenOverride = new OverrideGraphicSettings();
        greenOverride.SetProjectionLineColor(new Color(0, 220, 0));
        greenOverride.SetSurfaceForegroundPatternColor(new Color(0, 180, 0));
        greenOverride.SetProjectionLineWeight(5);

        while (true)
        {
            Reference reference;
            try
            {
                reference = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    "Cliquez les elements (ECHAP = valider)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // ECHAP = fin de la boucle, valider
                break;
            }

            var element = doc.GetElement(reference);
            if (element == null) continue;

            var typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId) continue;

            long typeIdValue = ElementIdHelper.GetValue(typeId);

            // Si deja selectionne, ignorer (pas de toggle pour simplifier)
            if (selectedTypes.ContainsKey(typeIdValue)) continue;

            var elementType = doc.GetElement(typeId) as ElementType;
            if (elementType == null) continue;

            selectedTypes[typeIdValue] = CreatePickedTypeDto(element, elementType, typeIdValue);

            // Marquer en vert TOUTES les instances de ce type (FIA-09 : map pre-construite)
            var instanceIds = instancesByType.TryGetValue(typeId, out var ids)
                ? ids
                : new List<ElementId>();

            MarkInstancesGreen(doc, activeView, greenOverride, instanceIds, markedElementIds);

            LogService.Log($"HandlePickElementInView: selected {typeIdValue} ({elementType.Name}), marked {instanceIds.Count} instances, total types={selectedTypes.Count}");
        }
    }

    /// <summary>
    /// Construit le SceneTypeDto d'un type clique (MAINT-10) : detection de la
    /// structure composee et des murs empiles (composite) incluse.
    /// </summary>
    private static SceneTypeDto CreatePickedTypeDto(Element element, ElementType elementType, long typeIdValue)
    {
        bool hasCs = elementType is HostObjAttributes hoa
            && (hoa.GetCompoundStructure() != null || elementType is WallType);
        bool isStackedWall = false;
        if (elementType is WallType wt && wt.GetCompoundStructure() == null
            && element is Wall w)
        {
            var sids = w.GetStackedWallMemberIds();
            isStackedWall = sids != null && sids.Count > 0;
        }

        return new SceneTypeDto
        {
            ElementIdValue = typeIdValue,
            FamilyName = elementType.FamilyName,
            TypeName = elementType.Name,
            CategoryName = element.Category?.Name ?? "Autre",
            HasCompoundStructure = hasCs,
            IsComposite = isStackedWall
        };
    }

    /// <summary>
    /// Applique l'override vert aux instances donnees dans une transaction courte
    /// et les enregistre dans markedElementIds pour le nettoyage final (MAINT-10).
    /// </summary>
    private static void MarkInstancesGreen(
        Document doc,
        View activeView,
        OverrideGraphicSettings greenOverride,
        List<ElementId> instanceIds,
        List<ElementId> markedElementIds)
    {
        using var tx = new Transaction(doc, "Olympe - Marquer selection");
        tx.Start();
        foreach (var id in instanceIds)
        {
            activeView.SetElementOverrides(id, greenOverride);
            markedElementIds.Add(id);
        }
        tx.Commit();
    }

    /// <summary>
    /// Retire les overrides verts de tous les elements marques (MAINT-10).
    /// Invariant du pick : appele dans le finally, donc TOUJOURS execute, que la
    /// boucle se termine par validation (ECHAP) ou par exception.
    /// Retourne null si le nettoyage a reussi, sinon le message d'erreur a
    /// remonter a l'utilisateur (FIA-07).
    /// </summary>
    private static string? CleanupGreenOverrides(Document doc, View activeView, List<ElementId> markedElementIds)
    {
        if (markedElementIds.Count == 0)
            return null;

        try
        {
            using var txClean = new Transaction(doc, "Olympe - Nettoyage");
            txClean.Start();
            var clean = new OverrideGraphicSettings();
            foreach (var id in markedElementIds)
                activeView.SetElementOverrides(id, clean);
            txClean.Commit();
            return null;
        }
        catch (Exception cleanEx)
        {
            // FIA-07 : echec de nettoyage logge et signale a l'utilisateur
            // (la surbrillance verte resterait commitee sans explication).
            LogService.Error("HandlePickElementInView: echec du nettoyage des overrides verts", cleanEx);
            return "La surbrillance verte n'a pas pu être retirée de la vue 3D. " +
                   "Utilisez Annuler (Ctrl+Z) dans Revit pour la retirer.";
        }
    }

    /// <summary>
    /// Construit en un seul passage de collector le dictionnaire
    /// typeId -> ids des instances du document (FIA-09).
    /// </summary>
    private static Dictionary<ElementId, List<ElementId>> BuildInstancesByTypeMap(Document doc)
    {
        var map = new Dictionary<ElementId, List<ElementId>>();
        foreach (var element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
        {
            var typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId) continue;

            if (!map.TryGetValue(typeId, out var list))
            {
                list = new List<ElementId>();
                map[typeId] = list;
            }
            list.Add(element.Id);
        }
        return map;
    }

    /// <summary>
    /// Pipette materiau (B2) : pick d'UN SEUL element dans la vue 3D, sans
    /// surbrillance verte ni boucle — retour immediat apres le clic.
    /// Lit le type de l'element clique et retourne ses materiaux :
    /// couches CompoundStructure (via HandleGetLayersForType, murs empiles inclus)
    /// ou parametres materiaux (via HandleGetMaterialParametersForType).
    /// Les entrees sans materiau resolu (« Par catégorie », « Aucun ») sont ignorees
    /// (id invalide) ; le dedoublonnage contre le groupe cible est fait cote ViewModel.
    /// Retourne null si le pick est annule (ECHAP) — annulation silencieuse.
    /// ARC-05 : le hide/show de la fenetre WPF est gere par le ViewModel appelant.
    /// CRITIQUE : catch Autodesk.Revit.Exceptions.OperationCanceledException (pas System).
    /// </summary>
    private static List<PresetMaterialDto>? HandlePickElementForMaterials(UIApplication uiApp)
    {
        // FIA3-03 : pas de document actif = erreur explicite (pattern GetActiveDocument),
        // pas une pseudo-annulation silencieuse — le chemin exception re-affiche la
        // fenetre et pose le message cote ViewModel.
        var uiDoc = uiApp.ActiveUIDocument
            ?? throw new InvalidOperationException("Aucun document actif.");

        if (uiDoc.ActiveView is not View3D)
            throw new InvalidOperationException("Vue 3D requise pour la sélection par clic.");

        Reference reference;
        try
        {
            reference = uiDoc.Selection.PickObject(
                ObjectType.Element,
                "Cliquez un element pour recuperer ses materiaux (ECHAP = annuler)");
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // ECHAP = annulation silencieuse (pattern AjouterParClic)
            return null;
        }

        var doc = uiDoc.Document;
        var element = doc.GetElement(reference);
        var typeId = element?.GetTypeId() ?? ElementId.InvalidElementId;
        if (element == null || typeId == ElementId.InvalidElementId)
        {
            LogService.Log("HandlePickElementForMaterials: element sans type, aucun materiau");
            return new List<PresetMaterialDto>();
        }

        long typeIdValue = ElementIdHelper.GetValue(typeId);

        // Couches CompoundStructure d'abord (meme dispatch que le panneau central) ;
        // sinon parametres materiaux de la famille chargee.
        var layers = HandleGetLayersForType(uiApp, typeIdValue);
        if (layers.Count > 0)
        {
            var fromLayers = layers
                .Where(l => l.MaterialElementIdValue >= 0)
                .Select(l => new PresetMaterialDto
                {
                    MaterialName = l.MaterialName,
                    MaterialElementIdValue = l.MaterialElementIdValue,
                    ColorArgb = l.ColorArgb ?? System.Drawing.Color.Gray.ToArgb(),
                    AppearanceColorArgb = l.AppearanceColorArgb
                })
                .ToList();
            LogService.Log($"HandlePickElementForMaterials: {fromLayers.Count} materiaux depuis {layers.Count} couches");
            return fromLayers;
        }

        var matParams = HandleGetMaterialParametersForType(uiApp, typeIdValue);
        var fromParams = matParams
            .Where(p => p.CurrentMaterialIdValue >= 0)
            .Select(p => new PresetMaterialDto
            {
                MaterialName = p.CurrentMaterialName,
                MaterialElementIdValue = p.CurrentMaterialIdValue,
                ColorArgb = p.ColorArgb ?? System.Drawing.Color.Gray.ToArgb(),
                AppearanceColorArgb = p.AppearanceColorArgb
            })
            .ToList();
        LogService.Log($"HandlePickElementForMaterials: {fromParams.Count} materiaux depuis {matParams.Count} parametres");
        return fromParams;
    }

    /// <summary>
    /// Selectionne dans la vue Revit tous les elements instances d'un type donne.
    /// Utilise Selection.SetElementIds pour mettre en surbrillance les elements.
    /// </summary>
    private static void HandleHighlightElementsByType(UIApplication uiApp, long typeIdValue)
    {
        var uiDoc = uiApp.ActiveUIDocument;
        if (uiDoc == null) return;

        var doc = uiDoc.Document;
        var typeId = ElementIdHelper.FromValue(typeIdValue);

        // Chercher tous les elements qui sont des instances de ce type
        var elementIds = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e.GetTypeId() == typeId)
            .Select(e => e.Id)
            .ToList();

        uiDoc.Selection.SetElementIds(elementIds);
    }
}
