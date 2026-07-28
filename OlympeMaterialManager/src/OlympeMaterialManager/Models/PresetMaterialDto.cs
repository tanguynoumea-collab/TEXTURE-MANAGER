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
    /// <summary>
    /// Nom du DataFormat WPF du drag and drop d'un materiau preset (B3) :
    /// source dans RightPanelView (deplacement entre groupes), cible aussi dans
    /// CenterPanelView (application mono-carte). Constante partagee pour que les
    /// deux vues ne puissent pas diverger.
    /// </summary>
    public const string DragDropFormat = "PresetMaterial";

    private string _materialName = string.Empty;
    private long _materialElementIdValue = -1;
    private int _colorArgb;
    private int? _appearanceColorArgb;
    private string? _texturePath;

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

    /// <summary>
    /// Couleur d'apparence ARGB du materiau (DR2-1), pour la pastille en mode
    /// Réaliste. Null = pas d'asset ou asset sans couleur → fallback couleur
    /// graphique. Champ additif dans les fichiers preset : les fichiers
    /// existants sans ce champ restent lisibles.
    /// </summary>
    public int? AppearanceColorArgb
    {
        get => _appearanceColorArgb;
        set { _appearanceColorArgb = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Chemin resolu de la texture bitmap (DR4-1), pour la pastille en mode
    /// Réaliste (couleur moyenne de l'image). Null = pas de bitmap ou
    /// introuvable → fallback couleur d'apparence puis couleur graphique.
    /// Champ additif dans les fichiers preset : les fichiers existants sans ce
    /// champ restent lisibles.
    /// </summary>
    public string? TexturePath
    {
        get => _texturePath;
        set { _texturePath = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
