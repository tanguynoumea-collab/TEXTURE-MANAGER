using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Handler ExternalEvent avec dispatch par enum (D-09, D-10).
/// Point de passage unique entre les ViewModels et l'API Revit.
/// Thread-safe : les requetes sont protegees par lock.
/// </summary>
public class RevitEventBridge : IExternalEventHandler
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
    /// IExternalEventHandler.Execute — appele par Revit quand l'ExternalEvent est leve.
    /// </summary>
    public void Execute(UIApplication app)
    {
        ProcessRequest(app);
    }

    /// <summary>
    /// IExternalEventHandler.GetName — nom affiche dans Revit.
    /// </summary>
    public string GetName() => "Olympe MaterialManager Bridge";

    /// <summary>
    /// Traite la requete sur le thread Revit.
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
    /// Supporte toutes les familles systeme : WallType, FloorType, RoofType, CeilingType
    /// via la classe de base HostObjAttributes qui expose GetCompoundStructure().
    /// Gere les cas sans noyau (Revit 2026) et les materiaux par categorie (InvalidElementId).
    /// Utilise UnitUtils pour la conversion pieds -> mm et LayerFunctionMapper pour les noms francais.
    /// </summary>
    private static List<LayerDto> HandleGetLayersForType(UIApplication uiApp, long typeIdValue)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null) return new List<LayerDto>();

        var elementId = ElementIdHelper.FromValue(typeIdValue);
        var element = doc.GetElement(elementId);

        // HostObjAttributes est la classe de base commune a WallType, FloorType, RoofType, CeilingType
        // Tous ces types exposent CompoundStructure de maniere generique
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

                // D-18 : tentative de lecture du chemin thumbnail (best-effort)
                // Le ThumbnailFile peut etre null, relatif ou pointer vers un fichier inexistant
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
                    && hoa.GetCompoundStructure() != null;

                string catName = element.Category?.Name ?? "Autre";

                pickedTypes.Add(new SceneTypeDto
                {
                    ElementIdValue = typeIdValue,
                    FamilyName = elementType.FamilyName,
                    TypeName = elementType.Name,
                    CategoryName = catName,
                    HasCompoundStructure = hasCs
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
