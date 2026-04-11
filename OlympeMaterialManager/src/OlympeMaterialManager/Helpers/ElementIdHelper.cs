using Autodesk.Revit.DB;

namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Accesseur centralise pour ElementId.Value (long) per D-04.
/// Jamais .IntegerValue (deprecie).
/// Ce fichier est dans la couche Revit -- import Revit API autorise.
/// </summary>
public static class ElementIdHelper
{
    public static long GetValue(ElementId id) => id.Value;

    public static ElementId FromValue(long value) => new ElementId(value);
}
