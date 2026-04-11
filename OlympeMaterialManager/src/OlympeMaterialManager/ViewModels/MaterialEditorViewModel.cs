using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Olympe.MaterialManager.Events;
using Olympe.MaterialManager.Messages;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.ViewModels;

/// <summary>
/// Sub-ViewModel pour la section editeur de materiau dans le panneau droit.
/// Affiche les details d'un materiau selectionne et permet l'edition live
/// via des commandes envoyees au RevitEventBridge (MATEDIT-01 a MATEDIT-08).
/// </summary>
public partial class MaterialEditorViewModel : ObservableObject
{
    private readonly RevitEventBridge? _eventBridge;
    private long _currentMaterialIdValue = -1;
    private bool _isFetching;

    // ---- Proprietes observables ----

    [ObservableProperty]
    private string _materialName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private int _colorArgb;

    [ObservableProperty]
    private byte _colorR;

    [ObservableProperty]
    private byte _colorG;

    [ObservableProperty]
    private byte _colorB;

    [ObservableProperty]
    private string _patternName = string.Empty;

    [ObservableProperty]
    private bool _hasAppearanceAsset;

    [ObservableProperty]
    private bool _tintEnabled;

    [ObservableProperty]
    private int _tintColorArgb;

    [ObservableProperty]
    private byte _tintR;

    [ObservableProperty]
    private byte _tintG;

    [ObservableProperty]
    private byte _tintB;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string? _thumbnailPath;

    /// <summary>
    /// Image de rendu (thumbnail de l'AppearanceAsset) pour la preview sphere.
    /// </summary>
    [ObservableProperty]
    private ImageSource? _renderPreviewImageSource;

    // ---- Parametres pour le rendu sphere 3D ----

    /// <summary>
    /// Transparence du materiau (0 = opaque, 100 = transparent).
    /// </summary>
    [ObservableProperty]
    private int _transparency;

    /// <summary>
    /// Brillance/shininess du materiau (0 = mat, 128 = tres brillant).
    /// Controle la puissance speculaire de la sphere.
    /// </summary>
    [ObservableProperty]
    private int _shininess;

    /// <summary>
    /// Lissite du materiau (0 = rugueux, 100 = lisse).
    /// </summary>
    [ObservableProperty]
    private int _smoothness;

    /// <summary>
    /// Couleur effective pour le rendu sphere : melange couleur de surface + teinte si active.
    /// </summary>
    [ObservableProperty]
    private int _sphereColorArgb;

    /// <summary>
    /// Opacite pour le rendu sphere (derive de Transparency).
    /// 1.0 = opaque, 0.0 = transparent.
    /// </summary>
    [ObservableProperty]
    private double _sphereOpacity = 1.0;

    /// <summary>
    /// Puissance speculaire pour le rendu sphere (derive de Shininess).
    /// </summary>
    [ObservableProperty]
    private double _sphereSpecularPower = 40.0;

    /// <summary>
    /// Image de texture pour le rendu sphere (si disponible).
    /// </summary>
    [ObservableProperty]
    private ImageSource? _sphereTextureImage;

    // ---- Constructeurs ----

    /// <summary>
    /// Constructeur principal avec injection du bridge ExternalEvent.
    /// </summary>
    public MaterialEditorViewModel(RevitEventBridge eventBridge)
    {
        _eventBridge = eventBridge;

        // Ecouter la selection d'un materiau preset (D-20)
        WeakReferenceMessenger.Default.Register<MaterialSelectedMessage>(this, (_, msg) => OnMaterialSelected(msg));

        // Ecouter les editions depuis d'autres sources pour rafraichir si necessaire
        WeakReferenceMessenger.Default.Register<MaterialEditedMessage>(this, (_, msg) => OnMaterialEdited(msg));
    }

    /// <summary>
    /// Constructeur sans parametre pour le designer WPF.
    /// </summary>
    public MaterialEditorViewModel() : this(null!)
    {
    }

    // ---- Handlers de messages ----

    /// <summary>
    /// Reagit a la selection d'un materiau preset dans le TreeView.
    /// </summary>
    private void OnMaterialSelected(MaterialSelectedMessage msg)
    {
        if (msg.Value is null)
        {
            IsVisible = false;
            return;
        }

        _currentMaterialIdValue = msg.Value.MaterialElementIdValue;
        FetchMaterialDetails();
    }

    /// <summary>
    /// Reagit aux notifications d'edition de materiau depuis d'autres sources.
    /// Rafraichit si le materiau modifie est celui actuellement affiche.
    /// </summary>
    private void OnMaterialEdited(MaterialEditedMessage msg)
    {
        if (msg.Value == _currentMaterialIdValue && _currentMaterialIdValue >= 0)
        {
            FetchMaterialDetails();
        }
    }

    // ---- Recuperation des details ----

    /// <summary>
    /// Demande les details complets du materiau au bridge Revit (D-16).
    /// </summary>
    private void FetchMaterialDetails()
    {
        if (_eventBridge == null || _currentMaterialIdValue < 0) return;

        _eventBridge.MakeRequest(RevitRequestType.GetMaterialDetails, _currentMaterialIdValue, result =>
        {
            if (result is MaterialDetailsDto dto)
            {
                MaterialName = dto.Name;
                Description = dto.Description;
                ColorArgb = dto.ColorArgb;
                PatternName = dto.PatternName;
                HasAppearanceAsset = dto.HasAppearanceAsset;
                ThumbnailPath = dto.ThumbnailPath;
                RenderPreviewImageSource = LoadRenderPreview(dto.ThumbnailPath);

                // Extraire les composantes RGB de la couleur de surface
                ColorR = (byte)((dto.ColorArgb >> 16) & 0xFF);
                ColorG = (byte)((dto.ColorArgb >> 8) & 0xFF);
                ColorB = (byte)(dto.ColorArgb & 0xFF);

                // Extraire les composantes RGB de la teinte
                // Utiliser le flag _isFetching pour eviter de declencher OnTintEnabledChanged
                _isFetching = true;
                TintR = (byte)((dto.TintColorArgb >> 16) & 0xFF);
                TintG = (byte)((dto.TintColorArgb >> 8) & 0xFF);
                TintB = (byte)(dto.TintColorArgb & 0xFF);
                TintColorArgb = dto.TintColorArgb;
                TintEnabled = dto.TintEnabled;
                _isFetching = false;

                // Parametres rendu sphere
                Transparency = dto.Transparency;
                Shininess = dto.Shininess;
                Smoothness = dto.Smoothness;

                // Opacite sphere (inverse de la transparence)
                SphereOpacity = 1.0 - (dto.Transparency / 100.0);

                // Puissance speculaire : mapper Shininess 0-128 vers 5-100
                SphereSpecularPower = Math.Max(5, dto.Shininess * 0.8);

                // Couleur effective sphere : si teinte active, melanger couleur + teinte
                if (dto.TintEnabled && dto.TintColorArgb != 0)
                {
                    byte sr = (byte)((dto.ColorArgb >> 16) & 0xFF);
                    byte sg = (byte)((dto.ColorArgb >> 8) & 0xFF);
                    byte sb = (byte)(dto.ColorArgb & 0xFF);
                    byte tr = (byte)((dto.TintColorArgb >> 16) & 0xFF);
                    byte tg = (byte)((dto.TintColorArgb >> 8) & 0xFF);
                    byte tb = (byte)(dto.TintColorArgb & 0xFF);
                    // Blend 50/50 surface + teinte
                    byte mr = (byte)((sr + tr) / 2);
                    byte mg = (byte)((sg + tg) / 2);
                    byte mb = (byte)((sb + tb) / 2);
                    SphereColorArgb = System.Drawing.Color.FromArgb(255, mr, mg, mb).ToArgb();
                }
                else
                {
                    SphereColorArgb = dto.ColorArgb;
                }

                // Texture pour la sphere (fallback si le rendu Revit echoue)
                SphereTextureImage = LoadRenderPreview(dto.TexturePath);

                IsVisible = true;

            }
            else if (result is Exception)
            {
                IsVisible = false;
            }
        });
    }

    /// <summary>
    /// Demande a Revit de generer un rendu sphere du materiau.
    /// Le rendu utilise le moteur Revit natif (meme qualite que le Material Editor).
    /// Retourne un byte[] PNG qui remplace l'image de la sphere WPF.
    /// </summary>
    private void RequestRevitRender()
    {
        if (_eventBridge == null || _currentMaterialIdValue < 0) return;

        _eventBridge.MakeRequest(RevitRequestType.RenderMaterialPreview, _currentMaterialIdValue, result =>
        {
            if (result is byte[] pngBytes && pngBytes.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(pngBytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    RenderPreviewImageSource = bitmap;
                }
                catch
                {
                    // Fallback : garder la sphere WPF 3D
                }
            }
            // Si le rendu echoue, la sphere WPF 3D reste affichee (fallback)
        });
    }

    // ---- Commandes d'edition ----

    /// <summary>
    /// Edite le nom du materiau via Transaction Revit (MATEDIT-02).
    /// Declenche sur LostFocus du TextBox.
    /// </summary>
    [RelayCommand]
    private void EditName()
    {
        if (_currentMaterialIdValue < 0 || _eventBridge == null) return;

        var dto = new EditMaterialNameRequestDto
        {
            MaterialIdValue = _currentMaterialIdValue,
            NewName = MaterialName
        };
        _eventBridge.MakeRequest(RevitRequestType.EditMaterialName, dto, OnEditResult);
    }

    /// <summary>
    /// Edite la description du materiau via Transaction Revit (MATEDIT-03).
    /// Declenche sur LostFocus du TextBox.
    /// </summary>
    [RelayCommand]
    private void EditDescription()
    {
        if (_currentMaterialIdValue < 0 || _eventBridge == null) return;

        var dto = new EditMaterialDescriptionRequestDto
        {
            MaterialIdValue = _currentMaterialIdValue,
            NewDescription = Description
        };
        _eventBridge.MakeRequest(RevitRequestType.EditMaterialDescription, dto, OnEditResult);
    }

    /// <summary>
    /// Edite la couleur de surface via Transaction Revit (MATEDIT-04).
    /// Declenche sur LostFocus des TextBox R/V/B.
    /// </summary>
    [RelayCommand]
    private void EditColor()
    {
        if (_currentMaterialIdValue < 0 || _eventBridge == null) return;

        var dto = new EditMaterialColorRequestDto
        {
            MaterialIdValue = _currentMaterialIdValue,
            Red = ColorR,
            Green = ColorG,
            Blue = ColorB
        };
        _eventBridge.MakeRequest(RevitRequestType.EditMaterialColor, dto, OnEditResult);
    }

    /// <summary>
    /// Ouvre un ColorDialog pour choisir la couleur de surface.
    /// </summary>
    [RelayCommand]
    private void PickSurfaceColor()
    {
        if (_currentMaterialIdValue < 0 || _eventBridge == null) return;

        using var dialog = new System.Windows.Forms.ColorDialog();
        dialog.Color = System.Drawing.Color.FromArgb(255, ColorR, ColorG, ColorB);
        dialog.FullOpen = true;

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ColorR = dialog.Color.R;
            ColorG = dialog.Color.G;
            ColorB = dialog.Color.B;
            // Mettre a jour le ColorArgb pour rafraichir le carre de couleur
            ColorArgb = System.Drawing.Color.FromArgb(255, ColorR, ColorG, ColorB).ToArgb();
            EditColorCommand.Execute(null);
        }
    }

    /// <summary>
    /// Ouvre un ColorDialog pour choisir la couleur de teinte.
    /// </summary>
    [RelayCommand]
    private void PickTintColor()
    {
        if (_currentMaterialIdValue < 0 || _eventBridge == null) return;
        if (!HasAppearanceAsset) return;

        using var dialog = new System.Windows.Forms.ColorDialog();
        dialog.Color = System.Drawing.Color.FromArgb(255, TintR, TintG, TintB);
        dialog.FullOpen = true;

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TintR = dialog.Color.R;
            TintG = dialog.Color.G;
            TintB = dialog.Color.B;
            // Mettre a jour le TintColorArgb pour rafraichir le carre de teinte
            TintColorArgb = System.Drawing.Color.FromArgb(255, TintR, TintG, TintB).ToArgb();
            EditTintCommand.Execute(null);
        }
    }

    /// <summary>
    /// Edite la teinte (activation + couleur) via Transaction Revit (MATEDIT-05).
    /// Declenche sur LostFocus des TextBox R/V/B teinte ou toggle du CheckBox.
    /// </summary>
    [RelayCommand]
    private void EditTint()
    {
        if (_currentMaterialIdValue < 0 || _eventBridge == null) return;
        if (!HasAppearanceAsset) return;

        var dto = new EditMaterialTintRequestDto
        {
            MaterialIdValue = _currentMaterialIdValue,
            TintEnabled = TintEnabled,
            Red = TintR,
            Green = TintG,
            Blue = TintB
        };
        _eventBridge.MakeRequest(RevitRequestType.EditMaterialTint, dto, OnEditResult);
    }

    /// <summary>
    /// Callback apres chaque edition reussie (D-19, D-21, MATEDIT-07).
    /// Envoie MaterialEditedMessage pour rafraichir la liste des presets,
    /// puis rafraichit la preview et tous les champs.
    /// </summary>
    private void OnEditResult(object? result)
    {
        if (result is Exception)
        {
            // Gestion gracieuse : ne rien faire de bloquant
            return;
        }

        // D-21 : notifier les autres VMs que le materiau a ete modifie
        WeakReferenceMessenger.Default.Send(new MaterialEditedMessage(_currentMaterialIdValue));

        // D-19, MATEDIT-07 : rafraichir la preview et les champs
        FetchMaterialDetails();
    }

    /// <summary>
    /// Declenche automatiquement l'edition de la teinte quand le toggle change (MATEDIT-05).
    /// </summary>
    partial void OnTintEnabledChanged(bool value)
    {
        if (!_isFetching && _currentMaterialIdValue >= 0 && HasAppearanceAsset)
        {
            EditTintCommand.Execute(null);
        }
    }

    /// <summary>
    /// Charge l'image de rendu depuis le chemin du thumbnail.
    /// Retourne null si le chemin est invalide ou le fichier introuvable (le XAML affichera le placeholder).
    /// </summary>
    private static ImageSource? LoadRenderPreview(string? thumbnailPath)
    {
        if (string.IsNullOrEmpty(thumbnailPath)) return null;

        try
        {
            string fullPath = thumbnailPath;

            // Resoudre les chemins relatifs via les dossiers standard Autodesk
            if (!Path.IsPathRooted(fullPath))
            {
                // Dossiers standards ou Revit stocke les textures de materiaux
                string[] searchPaths =
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Autodesk", "Shared", "Materials", "Textures"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Autodesk", "Shared", "Materials", "Textures", "1"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Autodesk", "Shared", "Materials", "Textures", "2"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Autodesk", "Shared", "Materials", "Textures", "3"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                        "Autodesk Shared", "Materials", "Textures"),
                };

                foreach (var basePath in searchPaths)
                {
                    var candidate = Path.Combine(basePath, fullPath);
                    if (File.Exists(candidate))
                    {
                        fullPath = candidate;
                        break;
                    }
                }
            }

            if (!File.Exists(fullPath)) return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(fullPath);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
