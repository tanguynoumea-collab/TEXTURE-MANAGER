using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour un type Revit dans une scene.
/// Implemente INotifyPropertyChanged pour que le TreeView detecte les changements
/// sur SubTypes (charges en async apres la creation pour les types composites).
/// </summary>
public class SceneTypeDto : INotifyPropertyChanged
{
    private ObservableCollection<SceneTypeDto>? _subTypes;

    public long ElementIdValue { get; set; } = -1;
    public string FamilyName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool HasCompoundStructure { get; set; }

    /// <summary>
    /// Indique que ce type est composite (ex: mur empile) et ne peut pas etre edite directement.
    /// </summary>
    public bool IsComposite { get; set; }

    /// <summary>
    /// Sous-types composant ce type composite (ex: sous-murs d'un mur empile).
    /// Notifie le TreeView quand les sous-types sont charges.
    /// </summary>
    public ObservableCollection<SceneTypeDto>? SubTypes
    {
        get => _subTypes;
        set
        {
            _subTypes = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public override string ToString() => $"{FamilyName} : {TypeName}";
}
