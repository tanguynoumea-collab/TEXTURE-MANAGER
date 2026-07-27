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

                IsVisible = true;
            }
            else if (result is Exception)
            {
                IsVisible = false;
            }
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
        if (result is Exception ex)
        {
            // FIA-05 : signaler l'echec a l'utilisateur puis resynchroniser les champs
            // avec l'etat reel du materiau Revit (la transaction a ete rollback).
            Services.LogService.Error("Echec d'edition du materiau", ex);
            Services.DialogService.ShowError(
                $"Echec de la modification du materiau :\n{ex.Message}");
            FetchMaterialDetails();
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

}
