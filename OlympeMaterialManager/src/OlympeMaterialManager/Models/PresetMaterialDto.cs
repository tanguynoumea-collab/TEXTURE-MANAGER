using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Olympe.MaterialManager.Models;

/// <summary>
/// DTO pour un materiau preset.
/// Implemente INotifyPropertyChanged pour que le TreeView rafraichisse
/// le carre de couleur et le nom apres une edition.
/// </summary>
public class PresetMaterialDto : INotifyPropertyChanged
{
    private string _materialName = string.Empty;
    private long _materialElementIdValue = -1;
    private int _colorArgb;

    public string MaterialName
    {
        get => _materialName;
        set { _materialName = value; OnPropertyChanged(); }
    }

    public long MaterialElementIdValue
    {
        get => _materialElementIdValue;
        set { _materialElementIdValue = value; OnPropertyChanged(); }
    }

    public int ColorArgb
    {
        get => _colorArgb;
        set { _colorArgb = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
