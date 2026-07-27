using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Handler ExternalEvent avec dispatch par enum (D-09, D-10).
/// Point de passage unique entre les ViewModels et l'API Revit.
/// Thread-safe : les requetes sont empilees dans une ConcurrentQueue depuis le
/// thread UI, drainees sur le thread Revit dans Execute(), et les callbacks sont
/// marshalles vers le thread UI WPF via Dispatcher.BeginInvoke.
/// </summary>
public class RevitEventBridge : IExternalEventHandler
{
    private record struct RequestEntry(RevitRequestType Type, object? Data, Action<object?> Callback);
    private readonly System.Collections.Concurrent.ConcurrentQueue<RequestEntry> _queue = new();

    /// <summary>
    /// Envoie une requete au thread Revit via ExternalEvent.
    /// Utilise une file d'attente pour supporter plusieurs requetes simultanees.
    /// </summary>
    public void MakeRequest(RevitRequestType type, object? data, Action<object?> callback)
    {
        LogService.Log($"MakeRequest: {type}, data={data?.GetType().Name ?? "null"}");
        _queue.Enqueue(new RequestEntry(type, data, callback));
        var raiseResult = App.RevitEvent.Raise();
        LogService.Log($"MakeRequest: Raise() returned {raiseResult}, queue size={_queue.Count}");
    }

    /// <summary>
    /// IExternalEventHandler.Execute — appele par Revit quand l'ExternalEvent est leve.
    /// Traite TOUTES les requetes en attente dans la queue.
    /// </summary>
    public void Execute(UIApplication app)
    {
        LogService.Log($"Execute() called by Revit, queue size={_queue.Count}");
        while (_queue.TryDequeue(out var entry))
        {
            ProcessSingleRequest(app, entry.Type, entry.Data, entry.Callback);
        }
    }

    /// <summary>
    /// IExternalEventHandler.GetName — nom affiche dans Revit.
    /// </summary>
    public string GetName() => "Olympe MaterialManager Bridge";

    /// <summary>
    /// Traite une seule requete sur le thread Revit.
    /// </summary>
    private void ProcessSingleRequest(UIApplication uiApp, RevitRequestType type, object? data, Action<object?> callback)
    {
        if (type == RevitRequestType.None)
        {
            LogService.Log("ProcessSingleRequest: skipped (type=None)");
            return;
        }

        LogService.Log($"ProcessSingleRequest: dispatching {type}");
        object? result = null;
        try
        {
            switch (type)
            {
                case RevitRequestType.GetLayersForType:
                    result = HandleGetLayersForType(uiApp, (long)data!);
                    break;
                case RevitRequestType.GetMaterialParametersForType:
                    result = HandleGetMaterialParametersForType(uiApp, (long)data!);
                    break;

                // Phase 3 : preset panel et Set Mat
                case RevitRequestType.GetAllMaterials:
                    result = HandleGetAllMaterials(uiApp);
                    break;
                case RevitRequestType.SetMaterialOnLayers:
                    HandleSetMaterialOnLayers(uiApp, (SetMatRequestDto)data!);
                    break;
                case RevitRequestType.SetMaterialOnParameter:
                    HandleSetMaterialOnParameter(uiApp, (SetMatParamRequestDto)data!);
                    break;
                case RevitRequestType.DuplicateMaterial:
                    result = HandleDuplicateMaterial(uiApp, (DuplicateMaterialRequestDto)data!);
                    break;

                // Phase 4 : edition materiau et pick 3D
                case RevitRequestType.GetMaterialDetails:
                    result = HandleGetMaterialDetails(uiApp, (long)data!);
                    break;
                case RevitRequestType.EditMaterialName:
                    HandleEditMaterialName(uiApp, (EditMaterialNameRequestDto)data!);
                    break;
                case RevitRequestType.EditMaterialDescription:
                    HandleEditMaterialDescription(uiApp, (EditMaterialDescriptionRequestDto)data!);
                    break;
                case RevitRequestType.EditMaterialColor:
                    HandleEditMaterialColor(uiApp, (EditMaterialColorRequestDto)data!);
                    break;
                case RevitRequestType.EditMaterialTint:
                    HandleEditMaterialTint(uiApp, (EditMaterialTintRequestDto)data!);
                    break;
                case RevitRequestType.PickElementInView:
                    result = HandlePickElementInView(uiApp);
                    break;
                case RevitRequestType.HighlightElementsByType:
                    HandleHighlightElementsByType(uiApp, (long)data!);
                    break;
                case RevitRequestType.GetCompositeSubTypes:
                    result = HandleGetCompositeSubTypes(uiApp, (long)data!);
                    break;
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"ProcessRequest: handler {type} threw", ex);
            result = ex;
        }

        LogService.Log($"ProcessRequest: {type} done, result={result?.GetType().Name ?? "null"}");

        // Marshaller le resultat vers le thread UI WPF (BeginInvoke pour eviter deadlock)
        try
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                LogService.Log($"ProcessRequest: BeginInvoke callback for {type}");
                dispatcher.BeginInvoke(new Action(() =>
                {
                    LogService.Log($"Callback: executing for {type}");
                    try
                    {
                        callback(result);
                        LogService.Log($"Callback: completed for {type}");
                    }
                    catch (Exception cbEx)
                    {
                        LogService.Error($"Callback: failed for {type}", cbEx);
                    }
                }));
            }
            else
            {
                LogService.Log($"ProcessRequest: Dispatcher is null! Calling callback directly for {type}");
                callback(result);
            }
        }
        catch (Exception dispEx)
        {
            LogService.Error($"ProcessRequest: Dispatcher.BeginInvoke failed for {type}", dispEx);
            try { callback(result); } catch { }
        }
    }

    /// <summary>
    /// Retourne les couches CompoundStructure pour un type systeme (D-11, D-12, D-13).
    /// Supporte toutes les familles systeme : WallType, FloorType, RoofType, CeilingType
    /// via la classe de base HostObjAttributes qui expose GetCompoundStructure().
    /// Gere les cas sans noyau (Revit 2026) et les materiaux par categorie (InvalidElementId).
    /// Utilise UnitUtils pour la conversion pieds -> mm et LayerFunctionMapper pour les noms francais.
    /// </summary>
    private static List<LayerDto> HandleGetLayersForType(UIApplication uiApp, long typeIdValue)
    {
        LogService.Log($"HandleGetLayersForType: typeIdValue={typeIdValue}");
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null)
        {
            LogService.Log("HandleGetLayersForType: doc is null");
            return new List<LayerDto>();
        }

        var elementId = ElementIdHelper.FromValue(typeIdValue);
        var element = doc.GetElement(elementId);
        LogService.Log($"HandleGetLayersForType: element={element?.GetType().Name ?? "null"}, name={element?.Name ?? "null"}");

        // HostObjAttributes est la classe de base commune a WallType, FloorType, RoofType, CeilingType
        // Tous ces types exposent CompoundStructure de maniere generique
        if (element is HostObjAttributes hostAttrs)
        {
            LogService.Log($"HandleGetLayersForType: is HostObjAttributes, getting CompoundStructure");
            var cs = hostAttrs.GetCompoundStructure();
            if (cs == null)
            {
                LogService.Log("HandleGetLayersForType: CompoundStructure is null");
                return new List<LayerDto>();
            }

            var layers = cs.GetLayers();
            LogService.Log($"HandleGetLayersForType: found {layers.Count} layers");
            var result = new List<LayerDto>(layers.Count);

            for (int i = 0; i < layers.Count; i++)
            {
                result.Add(BuildLayerDto(doc, layers[i], i));
            }

            return result;
        }

        // Support des murs empiles et autres types sans CompoundStructure directe
        // Tenter de trouver une instance de ce type et lire ses couches via l'instance
        LogService.Log($"HandleGetLayersForType: element {element?.GetType().Name} has no direct CompoundStructure, trying instance lookup");

        var instance = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e.GetTypeId() == elementId)
            .FirstOrDefault();

        if (instance != null)
        {
            LogService.Log($"HandleGetLayersForType: found instance {instance.Id}, type={instance.GetType().Name}");

            // Pour les murs empiles, recuperer les sous-murs
            if (instance is Wall wall)
            {
                var stackedIds = wall.GetStackedWallMemberIds();
                if (stackedIds != null && stackedIds.Count > 0)
                {
                    LogService.Log($"HandleGetLayersForType: stacked wall with {stackedIds.Count} sub-walls");
                    var allLayers = new List<LayerDto>();
                    int globalIndex = 0;

                    foreach (var subWallId in stackedIds)
                    {
                        var subWall = doc.GetElement(subWallId) as Wall;
                        if (subWall == null) continue;

                        var subType = doc.GetElement(subWall.GetTypeId()) as WallType;
                        if (subType == null) continue;

                        var subCs = subType.GetCompoundStructure();
                        if (subCs == null) continue;

                        string subName = subType.Name;
                        var subLayers = subCs.GetLayers();
                        LogService.Log($"HandleGetLayersForType: sub-wall '{subName}' has {subLayers.Count} layers");

                        for (int i = 0; i < subLayers.Count; i++)
                        {
                            allLayers.Add(BuildLayerDto(
                                doc, subLayers[i], globalIndex++, $"[{subName}] "));
                        }
                    }

                    if (allLayers.Count > 0)
                        return allLayers;
                }
            }
        }

        LogService.Log("HandleGetLayersForType: no layers found, returning empty list");
        return new List<LayerDto>();
    }

    /// <summary>
    /// Construit le LayerDto d'une couche CompoundStructure (MAINT-04) :
    /// resolution du nom de materiau (par categorie / inconnu), conversion pieds -> mm,
    /// nom de fonction francais optionnellement prefixe (sous-murs des murs empiles).
    /// </summary>
    private static LayerDto BuildLayerDto(Document doc, CompoundStructureLayer layer,
        int index, string? functionPrefix = null)
    {
        var matId = layer.MaterialId;
        string matName = UiLabels.ByCategory;
        long matIdValue = ElementIdHelper.GetValue(matId);

        if (matId != ElementId.InvalidElementId)
        {
            var mat = doc.GetElement(matId);
            matName = mat?.Name ?? UiLabels.Inconnu;
        }

        double widthMm = UnitUtils.ConvertFromInternalUnits(
            layer.Width, UnitTypeId.Millimeters);

        return new LayerDto
        {
            LayerIndex = index,
            Function = functionPrefix + LayerFunctionMapper.ToFrench(layer.Function),
            Width = Math.Round(widthMm, 1),
            MaterialName = matName,
            MaterialElementIdValue = matIdValue
        };
    }

    /// <summary>
    /// Retourne les parametres de type Material pour un element (D-14, D-16).
    /// Detecte les parametres via SpecTypeId.Reference.Material (pas de correspondance par nom).
    /// </summary>
    private static List<MaterialParamDto> HandleGetMaterialParametersForType(UIApplication uiApp, long typeIdValue)
    {
        LogService.Log($"HandleGetMaterialParametersForType: typeIdValue={typeIdValue}");
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null) return new List<MaterialParamDto>();

        var elementId = ElementIdHelper.FromValue(typeIdValue);
        var element = doc.GetElement(elementId);
        if (element == null)
        {
            LogService.Log("HandleGetMaterialParametersForType: element is null");
            return new List<MaterialParamDto>();
        }

        LogService.Log($"HandleGetMaterialParametersForType: element type={element.GetType().Name}, name={element.Name}");

        var result = new List<MaterialParamDto>();
        var seenParamNames = new HashSet<string>();

        // Chercher les parametres Material dans le type
        CollectMaterialParams(doc, element, result, seenParamNames, "Type");

        // Si c'est un ElementType, chercher aussi dans une instance representative
        if (element is ElementType elementType)
        {
            var instance = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Where(e => e.GetTypeId() == elementId)
                .FirstOrDefault();
            if (instance != null)
            {
                LogService.Log($"HandleGetMaterialParametersForType: found instance {instance.Id}, type={instance.GetType().Name}");
                CollectMaterialParams(doc, instance, result, seenParamNames, "Instance");
            }
        }

        // Si aucun parametre materiau trouve, ajouter une entree informative
        if (result.Count == 0)
        {
            result.Add(new MaterialParamDto
            {
                ParameterName = element.Name,
                ParameterDefinitionName = "",
                CurrentMaterialName = "Aucun parametre materiau",
                CurrentMaterialIdValue = -1
            });
        }

        LogService.Log($"HandleGetMaterialParametersForType: found {result.Count} params");
        return result;
    }

    /// <summary>
    /// Extrait les parametres de type Material d'un element.
    /// </summary>
    private static void CollectMaterialParams(Document doc, Element element,
        List<MaterialParamDto> result, HashSet<string> seenNames, string source)
    {
        foreach (Parameter param in element.Parameters)
        {
            if (param.StorageType != StorageType.ElementId) continue;

            // Verifier si c'est un parametre Material
            bool isMaterialParam = false;
            try { isMaterialParam = param.Definition.GetDataType() == SpecTypeId.Reference.Material; }
            catch { continue; }

            if (!isMaterialParam) continue;

            string paramKey = $"{source}:{param.Definition.Name}";
            if (seenNames.Contains(paramKey)) continue;
            seenNames.Add(paramKey);

            var matId = param.AsElementId();
            string matName = UiLabels.Aucun;
            long matIdValue = ElementIdHelper.GetValue(matId);

            if (matId != ElementId.InvalidElementId)
            {
                var mat = doc.GetElement(matId);
                matName = mat?.Name ?? UiLabels.Inconnu;
            }

            result.Add(new MaterialParamDto
            {
                ParameterName = param.Definition.Name,
                ParameterDefinitionName = param.Definition.Name,
                CurrentMaterialName = matName,
                CurrentMaterialIdValue = matIdValue
            });
        }
    }

    // =====================================================================
    // Phase 3 : handlers preset panel et Set Mat
    // =====================================================================

    /// <summary>
    /// Retourne tous les materiaux du document sous forme de PresetMaterialDto (D-21).
    /// Utilise FilteredElementCollector.OfClass(typeof(Material)) -- filtre natif rapide.
    /// </summary>
    private static List<PresetMaterialDto> HandleGetAllMaterials(UIApplication uiApp)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null) return new List<PresetMaterialDto>();

        return new FilteredElementCollector(doc)
            .OfClass(typeof(Material))
            .Cast<Material>()
            .Select(m => new PresetMaterialDto
            {
                MaterialName = m.Name,
                MaterialElementIdValue = ElementIdHelper.GetValue(m.Id),
                ColorArgb = ExtractColorArgb(m)
            })
            .OrderBy(m => m.MaterialName)
            .ToList();
    }

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
            $"Le materiau '{materialName}' n'existe pas dans ce document.");
    }

    /// <summary>
    /// Applique un materiau aux couches CompoundStructure selectionnees (D-16, D-22).
    /// Pattern Get-Modify-Set : GetCompoundStructure retourne une COPIE.
    /// </summary>
    private static void HandleSetMaterialOnLayers(UIApplication uiApp, SetMatRequestDto request)
    {
        var doc = uiApp.ActiveUIDocument!.Document;
        using var tx = new Transaction(doc, "Olympe : Appliquer materiau aux couches");
        tx.Start();

        try
        {
            var typeId = ElementIdHelper.FromValue(request.TargetTypeIdValue);
            var hostAttrs = doc.GetElement(typeId) as HostObjAttributes;
            if (hostAttrs == null)
                throw new InvalidOperationException("Le type selectionne n'est pas un type a couches.");

            // COPY -- must call SetCompoundStructure() to persist
            var cs = hostAttrs.GetCompoundStructure();
            if (cs == null)
                throw new InvalidOperationException("Le type n'a pas de structure composee.");

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
        var doc = uiApp.ActiveUIDocument!.Document;
        using var tx = new Transaction(doc, "Olympe : Appliquer materiau aux parametres");
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
                        $"Le parametre '{paramName}' est introuvable ou en lecture seule.");

                bool success = param.Set(matId);
                if (!success)
                    throw new InvalidOperationException(
                        $"Echec de l'assignation du materiau au parametre '{paramName}'.");
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
        var doc = uiApp.ActiveUIDocument!.Document;
        using var tx = new Transaction(doc, "Olympe : Dupliquer materiau");
        tx.Start();

        try
        {
            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
            var original = doc.GetElement(matId) as Material;
            if (original == null)
                throw new InvalidOperationException("Materiau source introuvable.");

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
                ColorArgb = ExtractColorArgb(duplicate)
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
    /// Extrait la valeur ARGB (int) de la couleur d'un materiau Revit.
    /// Si la couleur est invalide, retourne le gris par defaut.
    /// </summary>
    private static int ExtractColorArgb(Material m)
    {
        if (m.Color.IsValid)
            return ArgbUtils.PackArgb(m.Color.Red, m.Color.Green, m.Color.Blue);
        return System.Drawing.Color.Gray.ToArgb();
    }

    // =====================================================================
    // Phase 4 : handlers edition materiau et pick 3D
    // =====================================================================

    /// <summary>
    /// Retourne les details complets d'un materiau pour le visualisateur (D-16).
    /// Lit les proprietes directes du Material + tint via AppearanceAsset si present.
    /// </summary>
    private static MaterialDetailsDto HandleGetMaterialDetails(UIApplication uiApp, long materialIdValue)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null)
            throw new InvalidOperationException("Aucun document ouvert.");

        var matId = ElementIdHelper.FromValue(materialIdValue);
        var material = doc.GetElement(matId) as Material;
        if (material == null)
            throw new InvalidOperationException("Materiau introuvable.");

        var dto = new MaterialDetailsDto
        {
            Name = material.Name,
            ColorArgb = ExtractColorArgb(material),
            PatternName = GetPatternName(doc, material),
            HasAppearanceAsset = material.AppearanceAssetId != ElementId.InvalidElementId
        };

        // Description via BuiltInParameter (D-08)
        var descParam = material.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);
        dto.Description = descParam?.AsString() ?? string.Empty;

        // Proprietes de teinte (si AppearanceAsset present)
        if (dto.HasAppearanceAsset)
        {
            var assetElem = doc.GetElement(material.AppearanceAssetId) as AppearanceAssetElement;
            if (assetElem != null)
            {
                var renderAsset = assetElem.GetRenderingAsset();

                var tintToggle = renderAsset.FindByName(RevitAssetProps.TintToggle)
                    as AssetPropertyBoolean;
                dto.TintEnabled = tintToggle?.Value ?? false;

                var tintColor = renderAsset.FindByName(RevitAssetProps.TintColor)
                    as AssetPropertyDoubleArray4d;
                if (tintColor != null)
                {
                    var values = tintColor.GetValueAsDoubles();
                    if (values.Count >= 3)
                    {
                        byte r = (byte)(values[0] * 255);
                        byte g = (byte)(values[1] * 255);
                        byte b = (byte)(values[2] * 255);
                        dto.TintColorArgb = ArgbUtils.PackArgb(r, g, b);
                    }
                }
            }
        }

        return dto;
    }

    /// <summary>
    /// Renomme un materiau Revit (D-07, D-10).
    /// Transaction individuelle pour granularite undo.
    /// </summary>
    private static void HandleEditMaterialName(UIApplication uiApp, EditMaterialNameRequestDto request)
    {
        var doc = uiApp.ActiveUIDocument!.Document;
        using var tx = new Transaction(doc, "Olympe : Renommer materiau");
        tx.Start();

        try
        {
            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
            var material = doc.GetElement(matId) as Material;
            if (material == null)
                throw new InvalidOperationException("Materiau introuvable.");

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
        var doc = uiApp.ActiveUIDocument!.Document;
        using var tx = new Transaction(doc, "Olympe : Modifier description materiau");
        tx.Start();

        try
        {
            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
            var material = doc.GetElement(matId) as Material;
            if (material == null)
                throw new InvalidOperationException("Materiau introuvable.");

            var descParam = material.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);
            if (descParam == null || descParam.IsReadOnly)
                throw new InvalidOperationException("Le parametre description est introuvable ou en lecture seule.");

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
        var doc = uiApp.ActiveUIDocument!.Document;
        using var tx = new Transaction(doc, "Olympe : Modifier couleur de surface");
        tx.Start();

        try
        {
            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
            var material = doc.GetElement(matId) as Material;
            if (material == null)
                throw new InvalidOperationException("Materiau introuvable.");

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
        var doc = uiApp.ActiveUIDocument!.Document;
        using var tx = new Transaction(doc, "Olympe : Modifier teinte materiau");
        tx.Start();

        try
        {
            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);
            var material = doc.GetElement(matId) as Material;
            if (material == null)
                throw new InvalidOperationException("Materiau introuvable.");

            var assetElemId = material.AppearanceAssetId;
            if (assetElemId == ElementId.InvalidElementId)
                throw new InvalidOperationException("Teinte non disponible : pas d'AppearanceAsset.");

            var assetElem = doc.GetElement(assetElemId) as AppearanceAssetElement;
            if (assetElem == null)
                throw new InvalidOperationException("AppearanceAssetElement introuvable.");

            using (var scope = new AppearanceAssetEditScope(doc))
            {
                Asset editableAsset = scope.Start(assetElemId);

                // Toggle teinte on/off
                var tintToggle = editableAsset.FindByName(RevitAssetProps.TintToggle)
                    as AssetPropertyBoolean;
                if (tintToggle != null)
                    tintToggle.Value = request.TintEnabled;

                // Couleur de teinte (RGB en doubles normalises 0.0-1.0)
                if (request.TintEnabled)
                {
                    var tintColor = editableAsset.FindByName(RevitAssetProps.TintColor)
                        as AssetPropertyDoubleArray4d;
                    if (tintColor != null)
                    {
                        tintColor.SetValueAsDoubles(new double[]
                        {
                            request.Red / 255.0,
                            request.Green / 255.0,
                            request.Blue / 255.0,
                            1.0 // Alpha
                        });
                    }
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

    /// <summary>
    /// Selection additive dans la vue 3D via PickObject en boucle.
    /// Chaque clic sur un element ajoute son type a la selection ; un clic sur un
    /// type deja selectionne est ignore (pas de deselection).
    /// Toutes les instances du type sont mises en surbrillance dans la vue Revit.
    /// Echap ou Entree (OperationCanceledException) valide la selection courante.
    /// Retourne List&lt;SceneTypeDto&gt; des types actuellement selectionnes.
    /// CRITIQUE : catch Autodesk.Revit.Exceptions.OperationCanceledException (pas System).
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

        var mainWindow = App.MainWindow;
        System.Windows.Application.Current.Dispatcher.Invoke(() => mainWindow?.Hide());

        var selectedTypes = new Dictionary<long, SceneTypeDto>();
        var markedElementIds = new List<ElementId>();
        var activeView = uiDoc.ActiveView;

        // Override vert pour marquer les elements selectionnes
        var greenOverride = new OverrideGraphicSettings();
        greenOverride.SetProjectionLineColor(new Color(0, 220, 0));
        greenOverride.SetSurfaceForegroundPatternColor(new Color(0, 180, 0));
        greenOverride.SetProjectionLineWeight(5);

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

                // Marquer en vert TOUTES les instances de ce type
                var instanceIds = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .Where(e => e.GetTypeId() == typeId)
                    .Select(e => e.Id)
                    .ToList();

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
            // Nettoyer les overrides verts
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
                catch { }
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() => mainWindow?.Show());
        }

        return selectedTypes.Values.ToList();
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

    /// <summary>
    /// Retourne les sous-types d'un type composite (mur empile).
    /// Trouve une instance du type, recupere les sous-murs via GetStackedWallMemberIds(),
    /// et retourne leurs WallTypes comme List&lt;SceneTypeDto&gt;.
    /// </summary>
    private static List<SceneTypeDto> HandleGetCompositeSubTypes(UIApplication uiApp, long typeIdValue)
    {
        LogService.Log($"HandleGetCompositeSubTypes: typeIdValue={typeIdValue}");
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null) return new List<SceneTypeDto>();

        var typeElementId = ElementIdHelper.FromValue(typeIdValue);
        var result = new List<SceneTypeDto>();
        var seenSubTypeIds = new HashSet<long>();

        // Trouver une instance de ce type (Wall) pour acceder aux sous-murs
        var wallInstance = new FilteredElementCollector(doc)
            .OfClass(typeof(Wall))
            .Cast<Wall>()
            .FirstOrDefault(w => w.GetTypeId() == typeElementId);

        if (wallInstance == null)
        {
            LogService.Log("HandleGetCompositeSubTypes: no wall instance found");
            return result;
        }

        var stackedIds = wallInstance.GetStackedWallMemberIds();
        if (stackedIds == null || stackedIds.Count == 0)
        {
            LogService.Log("HandleGetCompositeSubTypes: no stacked wall member ids");
            return result;
        }

        LogService.Log($"HandleGetCompositeSubTypes: found {stackedIds.Count} sub-walls");

        foreach (var subWallId in stackedIds)
        {
            var subWall = doc.GetElement(subWallId) as Wall;
            if (subWall == null) continue;

            var subTypeId = subWall.GetTypeId();
            long subTypeIdValue = ElementIdHelper.GetValue(subTypeId);

            // Eviter les doublons si plusieurs instances du meme sous-type
            if (seenSubTypeIds.Contains(subTypeIdValue)) continue;
            seenSubTypeIds.Add(subTypeIdValue);

            var subType = doc.GetElement(subTypeId) as WallType;
            if (subType == null) continue;

            bool hasCs = subType.GetCompoundStructure() != null;
            string catName = subWall.Category?.Name ?? "Murs";

            result.Add(new SceneTypeDto
            {
                ElementIdValue = subTypeIdValue,
                FamilyName = subType.FamilyName,
                TypeName = subType.Name,
                CategoryName = catName,
                HasCompoundStructure = hasCs,
                IsComposite = false
            });
        }

        LogService.Log($"HandleGetCompositeSubTypes: returning {result.Count} sub-types");
        return result;
    }

    /// <summary>
    /// Retourne le nom du motif de surface premier plan d'un materiau.
    /// Retourne "< Aucun >" si pas de motif attribue.
    /// </summary>
    private static string GetPatternName(Document doc, Material material)
    {
        var patternId = material.SurfaceForegroundPatternId;
        if (patternId == ElementId.InvalidElementId)
            return UiLabels.Aucun;
        var pattern = doc.GetElement(patternId) as FillPatternElement;
        return pattern?.Name ?? UiLabels.Inconnu;
    }
}
