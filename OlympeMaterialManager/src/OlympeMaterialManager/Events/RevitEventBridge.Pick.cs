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
    private static object? HandlePickElementInView(UIApplication uiApp)
    {
        var uiDoc = uiApp.ActiveUIDocument;
        if (uiDoc == null) return null;
        var doc = uiDoc.Document;

        if (uiDoc.ActiveView is not View3D)
            throw new InvalidOperationException("Vue 3D requise pour la selection par clic.");

        var td = new TaskDialog("Selection 3D")
        {
            MainInstruction = "Selection d'elements dans la vue 3D",
            MainContent = "Cliquez sur les elements a ajouter a la scene.\n" +
                          "Toutes les occurences du type seront marquees en vert.\n\n" +
                          "Appuyez sur ECHAP pour valider.",
            CommonButtons = TaskDialogCommonButtons.Ok
        };
        td.Show();

        var selectedTypes = new Dictionary<long, SceneTypeDto>();
        var markedElementIds = new List<ElementId>();
        var activeView = uiDoc.ActiveView;
        string? cleanupError = null;

        // Override vert pour marquer les elements selectionnes
        var greenOverride = new OverrideGraphicSettings();
        greenOverride.SetProjectionLineColor(new Color(0, 220, 0));
        greenOverride.SetSurfaceForegroundPatternColor(new Color(0, 180, 0));
        greenOverride.SetProjectionLineWeight(5);

        // FIA-09 : UN seul FilteredElementCollector pour toute la session de pick.
        // Le dictionnaire typeId -> instances est construit une fois et reutilise a
        // chaque clic (l'ancien code relancait un collector complet par type clique).
        var instancesByType = BuildInstancesByTypeMap(doc);

        try
        {
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

                // Creer le DTO
                var elementType = doc.GetElement(typeId) as ElementType;
                if (elementType == null) continue;

                bool hasCs = elementType is HostObjAttributes hoa
                    && (hoa.GetCompoundStructure() != null || elementType is WallType);
                bool isStackedWall = false;
                if (elementType is WallType wt && wt.GetCompoundStructure() == null
                    && element is Wall w)
                {
                    var sids = w.GetStackedWallMemberIds();
                    isStackedWall = sids != null && sids.Count > 0;
                }

                selectedTypes[typeIdValue] = new SceneTypeDto
                {
                    ElementIdValue = typeIdValue,
                    FamilyName = elementType.FamilyName,
                    TypeName = elementType.Name,
                    CategoryName = element.Category?.Name ?? "Autre",
                    HasCompoundStructure = hasCs,
                    IsComposite = isStackedWall
                };

                // Marquer en vert TOUTES les instances de ce type (FIA-09 : map pre-construite)
                var instanceIds = instancesByType.TryGetValue(typeId, out var ids)
                    ? ids
                    : new List<ElementId>();

                using (var tx = new Transaction(doc, "Olympe - Marquer selection"))
                {
                    tx.Start();
                    foreach (var id in instanceIds)
                    {
                        activeView.SetElementOverrides(id, greenOverride);
                        markedElementIds.Add(id);
                    }
                    tx.Commit();
                }

                LogService.Log($"HandlePickElementInView: selected {typeIdValue} ({elementType.Name}), marked {instanceIds.Count} instances, total types={selectedTypes.Count}");
            }

            LogService.Log($"HandlePickElementInView: validated {selectedTypes.Count} types");
        }
        finally
        {
            // Invariant : les overrides verts sont TOUJOURS nettoyes en sortie de pick,
            // que la boucle se termine par validation ou par exception.
            if (markedElementIds.Count > 0)
            {
                try
                {
                    using var txClean = new Transaction(doc, "Olympe - Nettoyage");
                    txClean.Start();
                    var clean = new OverrideGraphicSettings();
                    foreach (var id in markedElementIds)
                        activeView.SetElementOverrides(id, clean);
                    txClean.Commit();
                }
                catch (Exception cleanEx)
                {
                    // FIA-07 : echec de nettoyage logge et signale a l'utilisateur
                    // (la surbrillance verte resterait commitee sans explication).
                    LogService.Error("HandlePickElementInView: echec du nettoyage des overrides verts", cleanEx);
                    cleanupError =
                        "La surbrillance verte n'a pas pu etre retiree de la vue 3D. " +
                        "Utilisez Annuler (Ctrl+Z) dans Revit pour la retirer.";
                }
            }
        }

        // FIA-07 : remonter l'echec de nettoyage via le callback (ErrorMessage cote VM)
        if (cleanupError != null)
            throw new InvalidOperationException(cleanupError);

        return selectedTypes.Values.ToList();
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
