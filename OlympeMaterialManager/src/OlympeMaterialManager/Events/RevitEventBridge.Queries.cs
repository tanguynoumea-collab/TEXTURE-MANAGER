using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Partie Queries (ARC-02/MAINT-03) : handlers de lecture seule —
/// couches CompoundStructure, parametres materiaux, liste et details des materiaux,
/// sous-types composites. Aucune transaction ici.
/// </summary>
public partial class RevitEventBridge
{
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
        int? colorArgb = null;
        string? texturePath = null;

        if (matId != ElementId.InvalidElementId)
        {
            var mat = doc.GetElement(matId);
            matName = mat?.Name ?? UiLabels.Inconnu;

            // B8/B10-TX : le Material est deja resolu ici — coût nul pour la
            // couleur, marche d'asset mise en cache pour la texture.
            if (mat is Material material)
            {
                colorArgb = ExtractColorArgb(material);
                texturePath = GetMaterialTexturePath(doc, material);
            }
        }

        double widthMm = UnitUtils.ConvertFromInternalUnits(
            layer.Width, UnitTypeId.Millimeters);

        return new LayerDto
        {
            LayerIndex = index,
            Function = functionPrefix + LayerFunctionMapper.ToFrench(layer.Function),
            Width = Math.Round(widthMm, 1),
            MaterialName = matName,
            MaterialElementIdValue = matIdValue,
            ColorArgb = colorArgb,
            TexturePath = texturePath
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
                CurrentMaterialName = "Aucun paramètre matériau",
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
            int? colorArgb = null;
            string? texturePath = null;

            if (matId != ElementId.InvalidElementId)
            {
                var mat = doc.GetElement(matId);
                matName = mat?.Name ?? UiLabels.Inconnu;

                // B8/B10-TX : memes donnees visuelles que les couches (coherence des cartes)
                if (mat is Material material)
                {
                    colorArgb = ExtractColorArgb(material);
                    texturePath = GetMaterialTexturePath(doc, material);
                }
            }

            result.Add(new MaterialParamDto
            {
                ParameterName = param.Definition.Name,
                ParameterDefinitionName = param.Definition.Name,
                CurrentMaterialName = matName,
                CurrentMaterialIdValue = matIdValue,
                ColorArgb = colorArgb,
                TexturePath = texturePath
            });
        }
    }

    /// <summary>
    /// Retourne tous les materiaux du document sous forme de PresetMaterialDto (D-21).
    /// Utilise FilteredElementCollector.OfClass(typeof(Material)) -- filtre natif rapide.
    /// </summary>
    private static List<PresetMaterialDto> HandleGetAllMaterials(UIApplication uiApp)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null) return new List<PresetMaterialDto>();

        // B10-TX : TexturePath rempli aussi ici (pastilles). Coût maîtrisé même
        // sur des centaines de matériaux : le cache par AppearanceAssetId évite
        // de re-marcher les assets partagés, et TexturePathResolver met en cache
        // les sondes disque par chemin brut.
        // DR1-3 : comptage des issues de résolution pour la ligne de synthèse
        // (diagnostic de terrain, toujours écrite dans olympe.log).
        int resolved = 0, noBitmap = 0, unresolved = 0;
        var result = new List<PresetMaterialDto>();

        foreach (var m in new FilteredElementCollector(doc)
                     .OfClass(typeof(Material))
                     .Cast<Material>())
        {
            var texturePath = GetMaterialTexturePath(doc, m, out var status);
            switch (status)
            {
                case TextureResolution.Resolved: resolved++; break;
                case TextureResolution.NoBitmap: noBitmap++; break;
                default: unresolved++; break;
            }

            result.Add(new PresetMaterialDto
            {
                MaterialName = m.Name,
                MaterialElementIdValue = ElementIdHelper.GetValue(m.Id),
                ColorArgb = ExtractColorArgb(m),
                TexturePath = texturePath
            });
        }

        LogService.Info(
            $"Textures: {resolved} résolues / {noBitmap} sans bitmap / " +
            $"{unresolved} non résolues sur {result.Count}");

        return result.OrderBy(m => m.MaterialName).ToList();
    }

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
            throw new InvalidOperationException("Matériau introuvable.");

        var dto = new MaterialDetailsDto
        {
            Name = material.Name,
            ColorArgb = ExtractColorArgb(material),
            PatternName = GetPatternName(doc, material),
            HasAppearanceAsset = material.AppearanceAssetId != ElementId.InvalidElementId,
            // B10-TX : aperçu du visualisateur en mode Texture (null = fallback couleur)
            TexturePath = GetMaterialTexturePath(doc, material)
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
    /// Poste la commande native « Matériaux » de Revit (B9). Aucune transaction :
    /// PostCommand programme l'ouverture du dialogue pour le moment ou Revit
    /// reprend le focus. Garde standard : echec propre si aucun document actif.
    /// </summary>
    private static void HandleOpenMaterialsDialog(UIApplication uiApp)
    {
        _ = GetActiveDocument(uiApp);
        uiApp.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.Materials));
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
