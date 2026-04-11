using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Handler ExternalEvent avec dispatch par enum (D-09, D-10).
/// Point de passage unique entre les ViewModels et l'API Revit.
/// Thread-safe : les requetes sont protegees par lock.
/// </summary>
public class RevitEventBridge
{
    private volatile RevitRequestType _requestType = RevitRequestType.None;
    private volatile object? _requestData;
    private Action<object?>? _resultCallback;
    private readonly object _lock = new();

    /// <summary>
    /// Envoie une requete au thread Revit via ExternalEvent.
    /// Appele depuis le thread UI (ViewModel).
    /// </summary>
    public void MakeRequest(RevitRequestType type, object? data, Action<object?> callback)
    {
        lock (_lock)
        {
            _requestType = type;
            _requestData = data;
            _resultCallback = callback;
        }
        App.RevitEvent.Raise();
    }

    /// <summary>
    /// Traite la requete sur le thread Revit.
    /// Appele par le callback ExternalEvent dans App.cs.
    /// </summary>
    public void ProcessRequest(UIApplication uiApp)
    {
        RevitRequestType type;
        object? data;
        Action<object?>? callback;

        lock (_lock)
        {
            type = _requestType;
            data = _requestData;
            callback = _resultCallback;
            _requestType = RevitRequestType.None;
            _requestData = null;
            _resultCallback = null;
        }

        if (type == RevitRequestType.None || callback == null)
            return;

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
            }
        }
        catch (Exception ex)
        {
            result = ex;
        }

        // Marshaller le resultat vers le thread UI WPF
        System.Windows.Application.Current.Dispatcher.Invoke(() => callback(result));
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
                bool hasCs = et is HostObjAttributes hoa && hoa.GetCompoundStructure() != null;
                result.Add(new SceneTypeDto
                {
                    ElementIdValue = ElementIdHelper.GetValue(et.Id),
                    FamilyName = et.FamilyName,
                    TypeName = et.Name,
                    CategoryName = categoryName,
                    HasCompoundStructure = hasCs
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
    /// Gere les cas sans noyau (Revit 2026) et les materiaux par categorie (InvalidElementId).
    /// Utilise UnitUtils pour la conversion pieds -> mm et LayerFunctionMapper pour les noms francais.
    /// </summary>
    private static List<LayerDto> HandleGetLayersForType(UIApplication uiApp, long typeIdValue)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null) return new List<LayerDto>();

        var elementId = ElementIdHelper.FromValue(typeIdValue);
        var element = doc.GetElement(elementId);

        if (element is HostObjAttributes hostAttrs)
        {
            var cs = hostAttrs.GetCompoundStructure();
            if (cs == null) return new List<LayerDto>();

            var layers = cs.GetLayers();
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

        return new List<LayerDto>();
    }

    /// <summary>
    /// Retourne les parametres de type Material pour un element (D-14, D-16).
    /// Detecte les parametres via SpecTypeId.Reference.Material (pas de correspondance par nom).
    /// </summary>
    private static List<MaterialParamDto> HandleGetMaterialParametersForType(UIApplication uiApp, long typeIdValue)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null) return new List<MaterialParamDto>();

        var elementId = ElementIdHelper.FromValue(typeIdValue);
        var element = doc.GetElement(elementId);
        if (element == null) return new List<MaterialParamDto>();

        var result = new List<MaterialParamDto>();

        foreach (Parameter param in element.Parameters)
        {
            if (param.StorageType != StorageType.ElementId) continue;
            if (param.Definition.GetDataType() != SpecTypeId.Reference.Material) continue;

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

        return result;
    }
}
