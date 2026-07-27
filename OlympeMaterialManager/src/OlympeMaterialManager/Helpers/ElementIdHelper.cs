using Autodesk.Revit.DB;

namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Accesseur centralise pour ElementId (long) per D-04.
/// net48 (Revit 2023/2024) : utilise IntegerValue (int) car le SDK 2023 n'a pas .Value.
/// net8.0 (Revit 2025+) : utilise .Value (long).
/// Les DTOs restent en long pour la compatibilite future.
/// Ce fichier est dans la couche Revit -- import Revit API autorise.
/// </summary>
public static class ElementIdHelper
{
#if REVIT2023_OR_2024
    public static long GetValue(ElementId id) => (long)id.IntegerValue;

    /// <summary>
    /// DON-05 : une valeur persistee hors plage int (id venant d'un document Revit 2025+)
    /// ne doit pas etre tronquee silencieusement en int — on retourne InvalidElementId,
    /// les consommateurs echouent alors proprement (validation ResolveMaterial, GetElement null).
    /// </summary>
    public static ElementId FromValue(long value) =>
        value > int.MaxValue || value < int.MinValue
            ? ElementId.InvalidElementId
            : new ElementId((int)value);
#else
    public static long GetValue(ElementId id) => id.Value;

    public static ElementId FromValue(long value) => new ElementId(value);
#endif
}
