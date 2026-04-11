using Autodesk.Revit.DB;

namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Mappe les fonctions de couche CompoundStructure vers leurs noms francais (D-13).
/// Note : MaterialFunctionAssignment.Structure correspond au noyau ("Noyau") dans l'API Revit.
/// Ce fichier est dans la couche Revit -- import Revit API autorise.
/// </summary>
public static class LayerFunctionMapper
{
    public static string ToFrench(MaterialFunctionAssignment function)
    {
        return function switch
        {
            MaterialFunctionAssignment.Finish1 => "Finition 1",
            MaterialFunctionAssignment.Finish2 => "Finition 2",
            MaterialFunctionAssignment.Substrate => "Substrat",
            MaterialFunctionAssignment.Structure => "Noyau",       // D-13: Core -> "Noyau" (Structure is the core layer function in Revit API)
            MaterialFunctionAssignment.Membrane => "Membrane",
            MaterialFunctionAssignment.Insulation => "Isolation thermique / Air",
            MaterialFunctionAssignment.StructuralDeck => "Plancher structurel",
            _ => function.ToString()
        };
    }
}
