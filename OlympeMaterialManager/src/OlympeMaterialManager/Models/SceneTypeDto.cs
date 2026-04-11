using System.Collections.ObjectModel;

namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour un type Revit dans une scene.
/// Contient les informations necessaires a l'affichage et au dispatch vers le panneau centre.
/// POCO pur -- aucune dependance Revit API (D-01).
/// </summary>
public class SceneTypeDto
{
    public long ElementIdValue { get; set; } = -1;
    public string FamilyName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool HasCompoundStructure { get; set; }

    /// <summary>
    /// Indique que ce type est composite (ex: mur empile) et ne peut pas etre edite directement.
    /// L'utilisateur doit selectionner un sous-type dans SubTypes pour editer ses couches.
    /// </summary>
    public bool IsComposite { get; set; }

    /// <summary>
    /// Sous-types composant ce type composite (ex: sous-murs d'un mur empile).
    /// Null ou vide pour les types non-composites.
    /// </summary>
    public ObservableCollection<SceneTypeDto>? SubTypes { get; set; }
}
