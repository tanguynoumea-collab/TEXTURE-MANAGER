using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Olympe.MaterialManager.Events;
using Olympe.MaterialManager.Helpers;
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
    private System.Windows.Threading.DispatcherTimer? _clipboardFeedbackTimer;

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

    /// <summary>
    /// Couleur d'apparence ARGB du materiau affiche (DR2-1), pour l'aperçu en
    /// mode Réaliste. Null = pas d'asset ou asset sans couleur → fallback
    /// couleur graphique + indicateur.
    /// </summary>
    [ObservableProperty]
    private int? _appearanceColorArgb;

    /// <summary>
    /// Chemin resolu de la texture bitmap du materiau affiche (DR4-2) :
    /// aperçu ImageBrush en mode Réaliste. Null = pas de bitmap ou introuvable
    /// → fallback couleur d'apparence puis couleur graphique.
    /// </summary>
    [ObservableProperty]
    private string? _texturePath;

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

    /// <summary>
    /// ADK2-02 : vraie pendant qu'une requete OpenMaterialsDialog est en vol —
    /// desactive le bouton « Ouvrir dans Revit » pour eviter le double PostCommand
    /// (InvalidOperationException) en double-clic. Pattern IsSetMatBusy.
    /// </summary>
    [ObservableProperty]
    private bool _isOpenInRevitBusy;

    /// <summary>
    /// DR1-2 : message affiché près du bouton « Ouvrir dans Revit » après le clic —
    /// l'API Revit ne permet pas de présélectionner le matériau dans son
    /// gestionnaire, le nom est donc copié au presse-papiers, et ce texte rend
    /// l'affordance visible à l'écran (le tooltip seul ne suffit pas). Effacé
    /// après ~6 s (pattern du feedback timer de MainWindowViewModel).
    /// </summary>
    [ObservableProperty]
    private string _clipboardFeedback = string.Empty;

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
                ApplyMaterialDetails(dto);
            }
            else if (result is Exception)
            {
                IsVisible = false;
            }
        });
    }

    /// <summary>
    /// Reporte les details recus du bridge dans les proprietes bindees (MAINT-10).
    /// Le flag _isFetching neutralise OnTintEnabledChanged pendant le report
    /// (sinon chaque resynchronisation redeclencherait une edition de teinte).
    /// </summary>
    private void ApplyMaterialDetails(MaterialDetailsDto dto)
    {
        MaterialName = dto.Name;
        Description = dto.Description;
        ColorArgb = dto.ColorArgb;
        PatternName = dto.PatternName;
        HasAppearanceAsset = dto.HasAppearanceAsset;
        AppearanceColorArgb = dto.AppearanceColorArgb;
        TexturePath = dto.TexturePath;

        // Extraire les composantes RGB de la couleur de surface
        (_, ColorR, ColorG, ColorB) = ArgbUtils.UnpackArgb(dto.ColorArgb);

        // Extraire les composantes RGB de la teinte
        _isFetching = true;
        (_, TintR, TintG, TintB) = ArgbUtils.UnpackArgb(dto.TintColorArgb);
        TintColorArgb = dto.TintColorArgb;
        TintEnabled = dto.TintEnabled;
        _isFetching = false;

        IsVisible = true;
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
            ColorArgb = ArgbUtils.PackArgb(ColorR, ColorG, ColorB);
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
            TintColorArgb = ArgbUtils.PackArgb(TintR, TintG, TintB);
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
                $"Échec de la modification du matériau :\n{ex.Message}");
            FetchMaterialDetails();
            return;
        }

        // D-21 : notifier les autres VMs que le materiau a ete modifie
        WeakReferenceMessenger.Default.Send(new MaterialEditedMessage(_currentMaterialIdValue));

        // D-19, MATEDIT-07 : rafraichir la preview et les champs
        FetchMaterialDetails();
    }

    /// <summary>
    /// Ouvre le gestionnaire de materiaux natif de Revit (B9) :
    /// (1) copie le nom du materiau au presse-papiers (palliatif a l'absence de
    /// preselection par API — l'utilisateur le colle dans la recherche Revit),
    /// (2) suspend le Topmost eventuel de la fenetre pour que le dialogue modal
    /// Revit ne s'ouvre pas derriere, (3) poste la commande via le bridge —
    /// le dialogue s'ouvre lorsque Revit reprend le focus.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOuvrirDansRevit))]
    private void OuvrirDansRevit()
    {
        if (_eventBridge == null || _currentMaterialIdValue < 0) return;

        try
        {
            // DR2-4 : copie robuste (retry + dernier recours) — l'erreur
            // CLIPBRD_E_CANT_OPEN a ete observee en session reelle.
            CopyToClipboardWithRetry(MaterialName);
            // DR1-2 : rendre la copie visible a l'ecran (le tooltip seul ne dit
            // rien apres le clic).
            ShowClipboardFeedback(
                "Nom copié — collez-le (Ctrl+V) dans la recherche du gestionnaire de matériaux");
        }
        catch (Exception ex)
        {
            // Presse-papiers verrouille malgre les tentatives : non bloquant,
            // l'ouverture du dialogue reste utile sans la copie. DR2-4 : le nom
            // est affiche en clair pour que l'utilisateur puisse le retaper.
            Services.LogService.Error("Copie du nom de matériau au presse-papiers impossible", ex);
            ShowClipboardFeedback(
                $"Impossible de copier — recherchez : {MaterialName}");
        }

        Services.WindowService.SuspendTopmostUntilReactivated();

        // ADK2-02 : verrouiller le bouton tant que la requete est en vol.
        IsOpenInRevitBusy = true;

        _eventBridge.MakeRequest(RevitRequestType.OpenMaterialsDialog, null, result =>
        {
            IsOpenInRevitBusy = false;

            // Callback null = succes : la commande est postee, Revit ouvrira le dialogue.
            if (result is Exception ex)
            {
                Services.LogService.Error("Echec d'ouverture du gestionnaire de matériaux Revit", ex);
                Services.DialogService.ShowError(
                    $"Échec de l'ouverture du gestionnaire de matériaux Revit :\n{ex.Message}");
            }
        });
    }

    /// <summary>
    /// DR2-4 : copie robuste au presse-papiers. CLIPBRD_E_CANT_OPEN survient
    /// quand une autre application (gestionnaire de presse-papiers, RDP,
    /// Revit lui-même) le verrouille quelques millisecondes : 3 tentatives de
    /// SetText espacées de 100 ms, puis Clipboard.SetDataObject(copy: false)
    /// en dernier recours (pas de flush : tolère des verrouillages que SetText
    /// ne tolère pas). Échec final → l'exception remonte au caller, qui affiche
    /// le nom en clair. Attente courte assumée sur le thread UI (200 ms max).
    /// </summary>
    private static void CopyToClipboardWithRetry(string text)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                return;
            }
            catch (Exception) when (attempt < maxAttempts)
            {
                System.Threading.Thread.Sleep(100);
            }
            catch (Exception)
            {
                // Dernier recours : sans copie persistante a la fermeture.
                System.Windows.Clipboard.SetDataObject(text, false);
                return;
            }
        }
    }

    /// <summary>
    /// DR1-2 : affiche le feedback presse-papiers pres du bouton et programme son
    /// effacement apres 6 s (pattern StartFeedbackTimer de MainWindowViewModel).
    /// </summary>
    private void ShowClipboardFeedback(string message)
    {
        ClipboardFeedback = message;
        _clipboardFeedbackTimer?.Stop();
        _clipboardFeedbackTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(6)
        };
        _clipboardFeedbackTimer.Tick += (_, _) =>
        {
            ClipboardFeedback = string.Empty;
            _clipboardFeedbackTimer.Stop();
        };
        _clipboardFeedbackTimer.Start();
    }

    /// <summary>
    /// Le bouton « Ouvrir dans Revit » n'est actif que si un materiau est selectionne
    /// et qu'aucune requete d'ouverture n'est deja en vol (ADK2-02).
    /// </summary>
    private bool CanOuvrirDansRevit()
        => IsVisible && !IsOpenInRevitBusy && _currentMaterialIdValue >= 0 && _eventBridge != null;

    /// <summary>
    /// Rafraichit l'etat du bouton « Ouvrir dans Revit » quand la requete part/revient (ADK2-02).
    /// </summary>
    partial void OnIsOpenInRevitBusyChanged(bool value)
    {
        OuvrirDansRevitCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsVisibleChanged(bool value)
    {
        OuvrirDansRevitCommand.NotifyCanExecuteChanged();
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
