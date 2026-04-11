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
/// Thread-safe : les requetes sont protegees par lock.
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
                case RevitRequestType.GetDocumentInfo:
                    result = HandleGetDocumentInfo(uiApp);
                    break;
                case RevitRequestType.GetFamilyList:
                    result = HandleGetFamilyList(uiApp);
                    break;
                case RevitRequestType.GetTypeList:
                    result = HandleGetTypeList(uiApp, (GetTypeListRequestDto)data!);
                    break;
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
                case RevitRequestType.RenderMaterialPreview:
                    result = HandleRenderMaterialPreview(uiApp, (long)data!);
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

    private static RevitDocInfoDto HandleGetDocumentInfo(UIApplication uiApp)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        return new RevitDocInfoDto
        {
            Title = doc?.Title ?? "(aucun document)",
            PathName = doc?.PathName ?? "",
            IsValid = doc != null
        };
    }

    /// <summary>
    /// Retourne la liste des familles (systeme + chargees) groupees par categorie (D-08).
    /// Familles systeme : Murs, Sols, Toits, Plafonds.
    /// Familles chargees : toutes les familles avec au moins un FamilySymbol.
    /// </summary>
    private static List<FamilyCategoryDto> HandleGetFamilyList(UIApplication uiApp)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null) return new List<FamilyCategoryDto>();

        var result = new List<FamilyCategoryDto>();

        // Familles systeme : Wall, Floor, Roof, Ceiling
        var systemCategories = new[]
        {
            (typeof(WallType), BuiltInCategory.OST_Walls),
            (typeof(FloorType), BuiltInCategory.OST_Floors),
            (typeof(RoofType), BuiltInCategory.OST_Roofs),
            (typeof(CeilingType), BuiltInCategory.OST_Ceilings),
        };

        foreach (var (typeClass, bic) in systemCategories)
        {
            var types = new FilteredElementCollector(doc)
                .OfClass(typeClass)
                .Cast<ElementType>()
                .ToList();

            if (types.Any())
            {
                string categoryName = LabelUtils.GetLabelFor(bic);
                result.Add(new FamilyCategoryDto
                {
                    CategoryName = categoryName,
                    BuiltInCategoryValue = (long)bic,
                    FamilyName = categoryName,
                    FamilyElementIdValue = -1,
                    IsSystemFamily = true
                });
            }
        }

        // Familles chargees
        var families = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(f => f.FamilyCategoryId != ElementId.InvalidElementId)
            .ToList();

        foreach (var family in families)
        {
            var symbolIds = family.GetFamilySymbolIds();
            if (symbolIds.Count == 0) continue;

            string catName = family.FamilyCategory?.Name ?? "Autre";
            result.Add(new FamilyCategoryDto
            {
                CategoryName = catName,
                FamilyName = family.Name,
                FamilyElementIdValue = ElementIdHelper.GetValue(family.Id),
                IsSystemFamily = false
            });
        }

        return result;
    }

    /// <summary>
    /// Retourne la liste des types pour une famille donnee (D-08, open question 3).
    /// Discrimine familles systeme (BuiltInCategory) et chargees (FamilyElementId).
    /// </summary>
    private static List<SceneTypeDto> HandleGetTypeList(UIApplication uiApp, GetTypeListRequestDto request)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null) return new List<SceneTypeDto>();

        var result = new List<SceneTypeDto>();

        if (request.IsSystemFamily)
        {
            var bic = (BuiltInCategory)request.BuiltInCategoryValue;
            Type? systemTypeClass = bic switch
            {
                BuiltInCategory.OST_Walls => typeof(WallType),
                BuiltInCategory.OST_Floors => typeof(FloorType),
                BuiltInCategory.OST_Roofs => typeof(RoofType),
                BuiltInCategory.OST_Ceilings => typeof(CeilingType),
                _ => null
            };

            if (systemTypeClass == null) return result;

            string categoryName = LabelUtils.GetLabelFor(bic);

            var types = new FilteredElementCollector(doc)
                .OfClass(systemTypeClass)
                .OfCategory(bic)
                .Cast<ElementType>()
                .ToList();

            foreach (var et in types)
            {
                // Detecter si c'est un mur empile (stacked wall) : WallType sans CompoundStructure directe
                bool isStackedWall = false;
                if (et is WallType wt && wt.GetCompoundStructure() == null)
                {
                    // Verifier s'il existe une instance Wall de ce type avec des sous-murs empiles
                    var testInstance = new FilteredElementCollector(doc)
                        .OfClass(typeof(Wall))
                        .Cast<Wall>()
                        .FirstOrDefault(w => w.GetTypeId() == et.Id);
                    if (testInstance != null)
                    {
                        var stackedIds = testInstance.GetStackedWallMemberIds();
                        isStackedWall = stackedIds != null && stackedIds.Count > 0;
                    }
                }

                // Detecter CompoundStructure : inclut les murs empiles (WallType sans CS directe mais avec sous-murs)
                bool hasCs = et is HostObjAttributes hoa &&
                    (hoa.GetCompoundStructure() != null || et is WallType);

                result.Add(new SceneTypeDto
                {
                    ElementIdValue = ElementIdHelper.GetValue(et.Id),
                    FamilyName = et.FamilyName,
                    TypeName = et.Name,
                    CategoryName = categoryName,
                    HasCompoundStructure = hasCs,
                    IsComposite = isStackedWall
                });
            }
        }
        else
        {
            // Famille chargee
            var familyId = ElementIdHelper.FromValue(request.FamilyElementIdValue);
            var family = doc.GetElement(familyId) as Family;
            if (family == null) return result;

            string catName = family.FamilyCategory?.Name ?? "Autre";

            foreach (var symbolId in family.GetFamilySymbolIds())
            {
                var symbol = doc.GetElement(symbolId) as FamilySymbol;
                if (symbol == null) continue;

                result.Add(new SceneTypeDto
                {
                    ElementIdValue = ElementIdHelper.GetValue(symbol.Id),
                    FamilyName = family.Name,
                    TypeName = symbol.Name,
                    CategoryName = catName,
                    HasCompoundStructure = false
                });
            }
        }

        return result;
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
                var layer = layers[i];
                var matId = layer.MaterialId;
                string matName = "< Par categorie >";
                long matIdValue = ElementIdHelper.GetValue(matId);

                if (matId != ElementId.InvalidElementId)
                {
                    var mat = doc.GetElement(matId);
                    matName = mat?.Name ?? "< Inconnu >";
                }

                double widthMm = UnitUtils.ConvertFromInternalUnits(
                    layer.Width, UnitTypeId.Millimeters);

                result.Add(new LayerDto
                {
                    LayerIndex = i,
                    Function = LayerFunctionMapper.ToFrench(layer.Function),
                    Width = Math.Round(widthMm, 1),
                    MaterialName = matName,
                    MaterialElementIdValue = matIdValue
                });
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
                            var layer = subLayers[i];
                            var matId = layer.MaterialId;
                            string matName = "< Par categorie >";
                            long matIdValue = ElementIdHelper.GetValue(matId);

                            if (matId != ElementId.InvalidElementId)
                            {
                                var mat = doc.GetElement(matId);
                                matName = mat?.Name ?? "< Inconnu >";
                            }

                            double widthMm = UnitUtils.ConvertFromInternalUnits(
                                layer.Width, UnitTypeId.Millimeters);

                            allLayers.Add(new LayerDto
                            {
                                LayerIndex = globalIndex++,
                                Function = $"[{subName}] {LayerFunctionMapper.ToFrench(layer.Function)}",
                                Width = Math.Round(widthMm, 1),
                                MaterialName = matName,
                                MaterialElementIdValue = matIdValue
                            });
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
            string matName = "< Aucun >";
            long matIdValue = ElementIdHelper.GetValue(matId);

            if (matId != ElementId.InvalidElementId)
            {
                var mat = doc.GetElement(matId);
                matName = mat?.Name ?? "< Inconnu >";
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

            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);

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
            var matId = ElementIdHelper.FromValue(request.MaterialIdValue);

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
    /// Cherche recursivement un chemin de texture bitmap dans un Asset Revit.
    /// Parcourt les proprietes de type Asset (connectes) et String pour trouver
    /// "unifiedbitmap_Bitmap" ou tout chemin finissant par une extension image.
    /// </summary>
    private static string? FindTexturePath(Asset asset)
    {
        if (asset == null) return null;

        for (int i = 0; i < asset.Size; i++)
        {
            var prop = asset.Get(i);
            if (prop == null) continue;

            // Chercher dans les sous-assets connectes (ex: generic_diffuse -> unifiedbitmap)
            if (prop.NumberOfConnectedProperties > 0)
            {
                for (int c = 0; c < prop.NumberOfConnectedProperties; c++)
                {
                    var connectedAsset = prop.GetConnectedProperty(c) as Asset;
                    if (connectedAsset != null)
                    {
                        var found = FindTexturePath(connectedAsset);
                        if (found != null) return found;
                    }
                }
            }

            // Chercher "unifiedbitmap_Bitmap" ou propriete String contenant un chemin image
            if (prop is AssetPropertyString strProp && !string.IsNullOrEmpty(strProp.Value))
            {
                string val = strProp.Value;
                if (strProp.Name == "unifiedbitmap_Bitmap" ||
                    val.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    val.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    val.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                    val.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                    val.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
                {
                    return val;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extrait la valeur ARGB (int) de la couleur d'un materiau Revit.
    /// Si la couleur est invalide, retourne le gris par defaut.
    /// </summary>
    private static int ExtractColorArgb(Material m)
    {
        if (m.Color.IsValid)
            return System.Drawing.Color.FromArgb(255, m.Color.Red, m.Color.Green, m.Color.Blue).ToArgb();
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
            HasAppearanceAsset = material.AppearanceAssetId != ElementId.InvalidElementId,
            Transparency = material.Transparency,       // 0-100
            Shininess = material.Shininess,             // 0-128
            Smoothness = material.Smoothness            // 0-100
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

                var tintToggle = renderAsset.FindByName("common_Tint_toggle")
                    as AssetPropertyBoolean;
                dto.TintEnabled = tintToggle?.Value ?? false;

                var tintColor = renderAsset.FindByName("common_Tint_color")
                    as AssetPropertyDoubleArray4d;
                if (tintColor != null)
                {
                    var values = tintColor.GetValueAsDoubles();
                    if (values.Count >= 3)
                    {
                        byte r = (byte)(values[0] * 255);
                        byte g = (byte)(values[1] * 255);
                        byte b = (byte)(values[2] * 255);
                        dto.TintColorArgb = System.Drawing.Color.FromArgb(255, r, g, b).ToArgb();
                    }
                }

                // Tentative de lecture du chemin texture bitmap (best-effort)
                try
                {
                    var texPath = FindTexturePath(renderAsset);
                    dto.ThumbnailPath = texPath;
                    dto.TexturePath = texPath;
                }
                catch
                {
                    // Best-effort : ignorer les erreurs de lecture
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
                var tintToggle = editableAsset.FindByName("common_Tint_toggle")
                    as AssetPropertyBoolean;
                if (tintToggle != null)
                    tintToggle.Value = request.TintEnabled;

                // Couleur de teinte (RGB en doubles normalises 0.0-1.0)
                if (request.TintEnabled)
                {
                    var tintColor = editableAsset.FindByName("common_Tint_color")
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
    /// Selection multi-elements dans la vue 3D via PickObject en boucle (D-11, D-12, D-13, D-14, D-17).
    /// Affiche un TaskDialog d'instruction, cache la fenetre WPF, boucle sur PickObject
    /// jusqu'a ce que l'utilisateur appuie sur Echap. Retourne List&lt;SceneTypeDto&gt;.
    /// CRITIQUE : catch Autodesk.Revit.Exceptions.OperationCanceledException (pas System).
    /// </summary>
    private static object? HandlePickElementInView(UIApplication uiApp)
    {
        var uiDoc = uiApp.ActiveUIDocument;
        if (uiDoc == null) return null;

        // D-14, SCENE-09 : valider que la vue active est une vue 3D
        if (uiDoc.ActiveView is not View3D)
            throw new InvalidOperationException("Vue 3D requise pour la selection par clic.");

        // Afficher un TaskDialog d'instruction avant le pick
        var td = new TaskDialog("Selection 3D")
        {
            MainInstruction = "Selection d'elements dans la vue 3D",
            MainContent = "Cliquez sur les elements a ajouter a la scene.\nAppuyez sur Echap pour terminer la selection.",
            CommonButtons = TaskDialogCommonButtons.Ok
        };
        td.Show();

        // D-11 : cacher la fenetre WPF avant le pick
        var mainWindow = App.MainWindow;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            mainWindow?.Hide();
        });

        var pickedTypes = new List<SceneTypeDto>();
        var seenTypeIds = new HashSet<long>();

        try
        {
            while (true)
            {
                var reference = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    "Selectionnez un element (Echap pour terminer)");

                var element = uiDoc.Document.GetElement(reference);
                if (element == null) continue;

                // D-12 : extraire le ElementType de l'element selectionne
                var typeId = element.GetTypeId();
                if (typeId == ElementId.InvalidElementId) continue;

                long typeIdValue = ElementIdHelper.GetValue(typeId);

                // Eviter les doublons par type
                if (seenTypeIds.Contains(typeIdValue)) continue;
                seenTypeIds.Add(typeIdValue);

                var elementType = uiDoc.Document.GetElement(typeId) as ElementType;
                if (elementType == null) continue;

                bool hasCs = elementType is HostObjAttributes hoa
                    && (hoa.GetCompoundStructure() != null || elementType is WallType);

                // Detecter mur empile : WallType sans CompoundStructure directe mais avec sous-murs
                bool isStackedWall = false;
                if (elementType is WallType pickedWt && pickedWt.GetCompoundStructure() == null
                    && element is Wall pickedWall)
                {
                    var stackedIds = pickedWall.GetStackedWallMemberIds();
                    isStackedWall = stackedIds != null && stackedIds.Count > 0;
                }

                string catName = element.Category?.Name ?? "Autre";

                pickedTypes.Add(new SceneTypeDto
                {
                    ElementIdValue = typeIdValue,
                    FamilyName = elementType.FamilyName,
                    TypeName = elementType.Name,
                    CategoryName = catName,
                    HasCompoundStructure = hasCs,
                    IsComposite = isStackedWall
                });
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // D-13 : Escape presse -- fin de la boucle de selection
        }
        finally
        {
            // Toujours re-afficher la fenetre (D-11, D-13, Pitfall 6)
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                mainWindow?.Show();
            });
        }

        return pickedTypes;
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
    /// Genere un rendu du materiau en utilisant le moteur de rendu Revit.
    /// Cree un DirectShape sphere temporaire, applique le materiau, exporte la vue, rollback.
    /// Retourne les octets PNG de l'image rendue, ou null si echec.
    /// </summary>
    private static byte[]? HandleRenderMaterialPreview(UIApplication uiApp, long materialIdValue)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null) return null;

        var matId = ElementIdHelper.FromValue(materialIdValue);
        var material = doc.GetElement(matId) as Material;
        if (material == null) return null;

        // Chemin temporaire pour l'export image
        var tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "OlympeMaterialPreview");
        System.IO.Directory.CreateDirectory(tempDir);
        var tempFileBase = System.IO.Path.Combine(tempDir, "preview");

        byte[]? imageBytes = null;

        using (var tx = new Transaction(doc, "Olympe - Rendu materiau"))
        {
            tx.Start();
            try
            {
                // 1. Creer une sphere via DirectShape
                var sphere = CreateSphereDirectShape(doc, matId);
                if (sphere == null)
                {
                    tx.RollBack();
                    return null;
                }

                // 2. Creer une vue 3D temporaire isolant uniquement la sphere
                var view3d = CreateIsolatedView(doc, sphere.Id);
                if (view3d == null)
                {
                    tx.RollBack();
                    return null;
                }

                // Regenerer le document pour que la vue soit a jour
                doc.Regenerate();

                // 3. Exporter la vue en image
                var exportOpts = new ImageExportOptions
                {
                    FilePath = tempFileBase,
                    FitDirection = FitDirectionType.Horizontal,
                    HLRandWFViewsFileType = ImageFileType.PNG,
                    ImageResolution = ImageResolution.DPI_150,
                    PixelSize = 256,
                    ExportRange = ExportRange.SetOfViews,
                    ZoomType = ZoomFitType.FitToPage,
                    ShadowViewsFileType = ImageFileType.PNG
                };
                exportOpts.SetViewsAndSheets(new List<ElementId> { view3d.Id });

                doc.ExportImage(exportOpts);

                // 4. Lire l'image exportee
                // Revit ajoute le nom de la vue au fichier
                var pngFiles = System.IO.Directory.GetFiles(tempDir, "preview*.png");
                if (pngFiles.Length > 0)
                {
                    imageBytes = System.IO.File.ReadAllBytes(pngFiles[0]);
                    // Nettoyer les fichiers temporaires
                    foreach (var f in pngFiles)
                    {
                        try { System.IO.File.Delete(f); } catch { }
                    }
                }
            }
            finally
            {
                // Rollback pour supprimer la sphere et la vue temporaires
                if (tx.HasStarted() && !tx.HasEnded())
                    tx.RollBack();
            }
        }

        return imageBytes;
    }

    /// <summary>
    /// Cree un DirectShape sphere avec le materiau specifie.
    /// </summary>
    private static DirectShape? CreateSphereDirectShape(Document doc, ElementId materialId)
    {
        // Creer un solide sphere via BRep
        var center = XYZ.Zero;
        double radius = 1.0; // 1 pied

        // Utiliser un Frame pour definir le plan de la sphere
        var frame = new Frame(center, XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ);

        // Creer la sphere via revolution d'un demi-cercle
        var profileLoops = new List<CurveLoop>();
        var loop = new CurveLoop();

        // Demi-cercle dans le plan XZ
        var arc = Arc.Create(
            new XYZ(0, 0, -radius),
            new XYZ(0, 0, radius),
            new XYZ(radius, 0, 0));
        var line = Line.CreateBound(
            new XYZ(0, 0, radius),
            new XYZ(0, 0, -radius));

        loop.Append(arc);
        loop.Append(line);
        profileLoops.Add(loop);

        // Revolution autour de l'axe Z
        var solid = GeometryCreationUtilities.CreateRevolvedGeometry(
            frame, profileLoops, 0, 2 * Math.PI);

        // Appliquer le materialId directement sur le solide via Paint
        var ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
        ds.SetShape(new GeometryObject[] { solid });

        // Peindre toutes les faces du DirectShape avec le materiau
        // Document.Paint(ElementId elementId, Face face, ElementId materialId)
        var opt = new Options { ComputeReferences = true };
        var geomElem = ds.get_Geometry(opt);
        if (geomElem != null)
        {
            foreach (var geomObj in geomElem)
            {
                if (geomObj is Solid s)
                {
                    foreach (Face face in s.Faces)
                    {
                        doc.Paint(ds.Id, face, materialId);
                    }
                }
            }
        }

        return ds;
    }

    /// <summary>
    /// Cree une vue 3D temporaire qui isole uniquement l'element specifie.
    /// Configure un fond neutre et un eclairage standard.
    /// </summary>
    private static View3D? CreateIsolatedView(Document doc, ElementId elementToIsolate)
    {
        // Trouver un ViewFamilyType pour les vues 3D
        var viewFamilyType = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);

        if (viewFamilyType == null) return null;

        var view3d = View3D.CreateIsometric(doc, viewFamilyType.Id);
        view3d.Name = "Olympe_TempPreview_" + Guid.NewGuid().ToString("N")[..8];

        // Isoler uniquement la sphere
        view3d.IsolateElementTemporary(elementToIsolate);

        // Configurer le style visuel : realiste
        view3d.DisplayStyle = DisplayStyle.Realistic;

        // Desactiver les annotations
        view3d.AreAnnotationCategoriesHidden = true;

        // Cadrer sur l'element
        var boundingBox = doc.GetElement(elementToIsolate).get_BoundingBox(view3d);
        if (boundingBox != null)
        {
            var min = boundingBox.Min;
            var max = boundingBox.Max;
            var center = (min + max) / 2;
            var diagonal = max - min;
            var dist = diagonal.GetLength() * 2;

            var eyePos = center + new XYZ(dist * 0.5, -dist * 0.3, dist * 0.4);
            var upDir = XYZ.BasisZ;
            var forwardDir = (center - eyePos).Normalize();

            view3d.SetOrientation(new ViewOrientation3D(eyePos, upDir, forwardDir));
        }

        return view3d;
    }

    /// <summary>
    /// Retourne le nom du motif de surface premier plan d'un materiau.
    /// Retourne "< Aucun >" si pas de motif attribue.
    /// </summary>
    private static string GetPatternName(Document doc, Material material)
    {
        var patternId = material.SurfaceForegroundPatternId;
        if (patternId == ElementId.InvalidElementId)
            return "< Aucun >";
        var pattern = doc.GetElement(patternId) as FillPatternElement;
        return pattern?.Name ?? "< Inconnu >";
    }
}
