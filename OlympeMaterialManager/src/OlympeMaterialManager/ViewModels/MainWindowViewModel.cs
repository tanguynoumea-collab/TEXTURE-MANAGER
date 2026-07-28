using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Olympe.MaterialManager.Events;
using Olympe.MaterialManager.Messages;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.ViewModels;

/// <summary>
/// ViewModel racine coordonnant les trois panneaux de l'interface.
/// Aucune dependance Revit API -- uniquement MVVM pur (D-15, INFRA-07).
/// Communication avec Revit via RevitEventBridge (Olympe.MaterialManager.Events).
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly RevitEventBridge? _eventBridge;
    private DispatcherTimer? _feedbackTimer;

    /// <summary>
    /// Nombre de cibles de l'application en cours, et leur libelle au pluriel
    /// (« couches » / « paramètres »), memorises pour que le feedback de succes
    /// annonce la portee reelle de l'operation (DR5-2).
    /// </summary>
    private int _pendingTargetCount;
    private string _pendingTargetLabel = string.Empty;

    [ObservableProperty]
    private string _titre = "Olympe MaterialManager";

    /// <summary>
    /// Chemin du repertoire de projet affiche dans la barre d'en-tete.
    /// </summary>
    [ObservableProperty]
    private string _projectDirectoryPath = string.Empty;

    /// <summary>
    /// Texte de retour visuel apres Set Mat ("Materiau applique !" ou message d'erreur) (D-19).
    /// </summary>
    [ObservableProperty]
    private string _setMatStatusText = string.Empty;

    /// <summary>
    /// Empeche le double-clic pendant une operation Revit en cours.
    /// </summary>
    [ObservableProperty]
    private bool _isSetMatBusy;

    public LeftPanelViewModel LeftPanelVM { get; }
    public CenterPanelViewModel CenterPanelVM { get; }
    public RightPanelViewModel RightPanelVM { get; }

    /// <summary>
    /// Point unique de verite du jeu de couleurs (cycle 4). Expose au XAML pour
    /// le glyphe et le libelle du bouton de bascule (icone de DESTINATION).
    /// </summary>
    public ThemeStore ThemeStore { get; }

    /// <summary>
    /// Constructeur principal avec injection du bridge ExternalEvent.
    /// </summary>
    public MainWindowViewModel(RevitEventBridge eventBridge)
    {
        _eventBridge = eventBridge;
        var presetService = new PresetService();
        // B10-S : point unique de vérité du mode d'aperçu, partagé par les panneaux.
        var previewModeStore = new PreviewModeStore(presetService);
        // Cycle 4 : le theme est charge et APPLIQUE ici, avant la construction de
        // la fenetre — elle s'ouvre donc directement au jeu de couleurs persiste.
        ThemeStore = new ThemeStore(presetService);
        LeftPanelVM = new LeftPanelViewModel(eventBridge, presetService);
        CenterPanelVM = new CenterPanelViewModel(eventBridge, previewModeStore);
        RightPanelVM = new RightPanelViewModel(eventBridge, presetService, previewModeStore);

        // Initialiser le chemin du repertoire de projet
        ProjectDirectoryPath = PresetService.GetProjectDirectory() ?? string.Empty;

        // Surveiller SelectedPresetMaterial pour rafraichir CanExecute (D-15)
        RightPanelVM.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RightPanelViewModel.SelectedPresetMaterial))
                AppliquerMateriauCommand.NotifyCanExecuteChanged();
        };

        // Surveiller CenterPanelVM pour rafraichir CanExecute quand selection/mode change (D-15, Pitfall 5)
        CenterPanelVM.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CenterPanelViewModel.SelectedItems)
                or nameof(CenterPanelViewModel.ShowLayers)
                or nameof(CenterPanelViewModel.ShowParameters))
                AppliquerMateriauCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>
    /// Constructeur sans parametre pour le designer WPF.
    /// </summary>
    public MainWindowViewModel() : this(null!)
    {
    }

    /// <summary>
    /// Rafraichit l'etat du bouton Set Mat quand IsSetMatBusy change.
    /// </summary>
    partial void OnIsSetMatBusyChanged(bool value)
    {
        AppliquerMateriauCommand.NotifyCanExecuteChanged();
    }

    // ------------------------------------------------------------------
    //  Repertoire de projet : Ouvrir / Migrer
    // ------------------------------------------------------------------

    /// <summary>
    /// Ouvre le repertoire de projet dans l'Explorateur Windows.
    /// </summary>
    [RelayCommand]
    private static void OuvrirRepertoire()
    {
        var path = PresetService.GetProjectDirectory();
        if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
        {
            Process.Start("explorer.exe", path);
        }
    }

    /// <summary>
    /// Ouvre un dialogue de choix de dossier, puis migre tous les fichiers
    /// vers le nouveau repertoire de projet.
    /// </summary>
    [RelayCommand]
    private void MigrerRepertoire()
    {
        var newPath = DialogService.ShowFolderBrowser("Choisir le nouveau répertoire de projet");
        if (string.IsNullOrEmpty(newPath)) return;

        try
        {
            PresetService.MigrateProjectDirectory(newPath!);
            ProjectDirectoryPath = newPath!;

            DialogService.ShowInfo(
                $"Répertoire de projet migré avec succès vers :\n{newPath}");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(
                $"Erreur lors de la migration :\n{ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    //  Bascule du jeu de couleurs (cycle 4)
    // ------------------------------------------------------------------

    /// <summary>
    /// Bascule entre le jeu sombre et le jeu clair. Le store applique la palette
    /// a toutes les fenetres ouvertes, persiste le choix et diffuse le message.
    /// </summary>
    [RelayCommand]
    private void BasculerTheme()
    {
        ThemeStore.CurrentTheme =
            ThemeStore.CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
    }

    // ------------------------------------------------------------------
    //  AppliquerMateriauCommand (D-14, D-15, D-16, D-17, D-18, D-19, D-24, D-25)
    // ------------------------------------------------------------------

    /// <summary>
    /// CanExecute : actif si pas en cours, preset selectionne, et items selectionnes en mode couches ou parametres.
    /// </summary>
    private bool CanAppliquerMateriau()
        => !IsSetMatBusy
           && RightPanelVM.SelectedPresetMaterial != null
           && CenterPanelVM.SelectedItems?.Count > 0
           && (CenterPanelVM.ShowLayers || CenterPanelVM.ShowParameters);

    /// <summary>
    /// Applique le materiau preset selectionne aux couches ou parametres selectionnes (D-24).
    /// Dispatch vers SetMaterialOnLayers (types a couches) ou SetMaterialOnParameter (familles chargees).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAppliquerMateriau))]
    private void AppliquerMateriau()
    {
        var presetMat = RightPanelVM.SelectedPresetMaterial;
        if (presetMat == null) return;

        IsSetMatBusy = true;

        if (CenterPanelVM.ShowLayers)
        {
            // D-16 : couches CompoundStructure
            var layerIndices = CenterPanelVM.SelectedItems?
                .Cast<LayerDto>()
                .Select(l => l.LayerIndex)
                .ToArray();

            EnvoyerSetMatCouches(presetMat, layerIndices ?? []);
        }
        else if (CenterPanelVM.ShowParameters)
        {
            // D-17 : parametres materiaux de familles chargees
            var paramNames = CenterPanelVM.SelectedItems?
                .Cast<MaterialParamDto>()
                .Select(p => p.ParameterDefinitionName)
                .ToArray();

            EnvoyerSetMatParametres(presetMat, paramNames ?? []);
        }
        else
        {
            IsSetMatBusy = false;
        }
    }

    /// <summary>
    /// Application par drag and drop d'un preset sur une ou plusieurs couches
    /// (B3, DR5-2). Chemin transactionnel identique a AppliquerMateriau : une
    /// seule requete bridge donc une seule transaction Revit, quel que soit le
    /// nombre de couches. Garde anti-reentrance : ignore le drop si une
    /// application est deja en cours.
    /// </summary>
    public void AppliquerMateriauSurCouches(PresetMaterialDto presetMat, IReadOnlyList<LayerDto> layers)
    {
        if (_eventBridge == null) return;
        if (layers.Count == 0) return;
        if (IsSetMatBusy)
        {
            // FIA3-06 : drop ignore pendant une application en cours — feedback
            // au lieu d'un silence (OnSetMatResult/le timer effacera le texte).
            SetMatStatusText = "Application en cours…";
            return;
        }

        IsSetMatBusy = true;
        EnvoyerSetMatCouches(presetMat, layers.Select(l => l.LayerIndex).ToArray());
    }

    /// <summary>
    /// Application par drag and drop d'un preset sur un ou plusieurs parametres
    /// materiaux (B3, DR5-2). Meme mecanique que AppliquerMateriau, en un seul
    /// batch. Les cartes informatives (« Aucun paramètre matériau », definition
    /// vide) sont ecartees ; s'il ne reste rien, le drop est sans effet.
    /// </summary>
    public void AppliquerMateriauSurParametres(PresetMaterialDto presetMat, IReadOnlyList<MaterialParamDto> parametres)
    {
        if (_eventBridge == null) return;

        var paramNames = parametres
            .Select(p => p.ParameterDefinitionName)
            .Where(nom => !string.IsNullOrEmpty(nom))
            .ToArray();

        if (paramNames.Length == 0) return;
        if (IsSetMatBusy)
        {
            // FIA3-06 : drop ignore pendant une application en cours — feedback
            // au lieu d'un silence (OnSetMatResult/le timer effacera le texte).
            SetMatStatusText = "Application en cours…";
            return;
        }

        IsSetMatBusy = true;
        EnvoyerSetMatParametres(presetMat, paramNames);
    }

    /// <summary>
    /// Envoi unique de la requete Set Mat sur un lot de couches — point de
    /// passage commun du bouton « Appliquer le matériau » et du drag and drop
    /// (DR5-2). Un lot vide relache simplement la garde.
    /// </summary>
    private void EnvoyerSetMatCouches(PresetMaterialDto presetMat, int[] layerIndices)
    {
        if (layerIndices.Length == 0)
        {
            IsSetMatBusy = false;
            return;
        }

        _pendingTargetCount = layerIndices.Length;
        _pendingTargetLabel = "couches";

        var request = new SetMatRequestDto
        {
            TargetTypeIdValue = CenterPanelVM.CurrentTypeIdValue,
            LayerIndices = layerIndices,
            MaterialIdValue = presetMat.MaterialElementIdValue,
            // DON-04 : le nom est la cle logique de validation cote handler
            MaterialName = presetMat.MaterialName
        };

        _eventBridge?.MakeRequest(RevitRequestType.SetMaterialOnLayers, request, OnSetMatResult);
    }

    /// <summary>
    /// Envoi unique de la requete Set Mat sur un lot de parametres materiaux —
    /// point de passage commun du bouton « Appliquer le matériau » et du drag
    /// and drop (DR5-2). Un lot vide relache simplement la garde.
    /// </summary>
    private void EnvoyerSetMatParametres(PresetMaterialDto presetMat, string[] paramNames)
    {
        if (paramNames.Length == 0)
        {
            IsSetMatBusy = false;
            return;
        }

        _pendingTargetCount = paramNames.Length;
        _pendingTargetLabel = "paramètres";

        var request = new SetMatParamRequestDto
        {
            TargetTypeIdValue = CenterPanelVM.CurrentTypeIdValue,
            MaterialIdValue = presetMat.MaterialElementIdValue,
            ParameterDefinitionNames = paramNames,
            // DON-04 : le nom est la cle logique de validation cote handler
            MaterialName = presetMat.MaterialName
        };

        _eventBridge?.MakeRequest(RevitRequestType.SetMaterialOnParameter, request, OnSetMatResult);
    }

    /// <summary>
    /// Callback apres execution de Set Mat par le bridge Revit (D-18, D-19, D-25).
    /// Gere erreur (MessageBox francais + rollback) et succes (feedback + refresh).
    /// </summary>
    private void OnSetMatResult(object? result)
    {
        IsSetMatBusy = false;

        if (result is Exception ex)
        {
            // D-18 : erreur avec message francais
            SetMatStatusText = $"Erreur : {ex.Message}";
            DialogService.ShowError(
                $"Erreur lors de l'application du matériau :\n{ex.Message}");
        }
        else
        {
            // D-19 : retour visuel succes. DR5-2 : au-dela d'une cible, le texte
            // annonce la portee reelle — l'utilisateur voit que le drop a couvert
            // toute sa selection et pas seulement la carte survolee.
            SetMatStatusText = _pendingTargetCount > 1
                ? $"Matériau appliqué à {_pendingTargetCount} {_pendingTargetLabel} !"
                : "Matériau appliqué !";

            // D-25 : rafraichir le panneau central pour afficher les nouveaux noms de materiaux
            WeakReferenceMessenger.Default.Send(
                new RefreshLayersMessage(CenterPanelVM.CurrentTypeIdValue));

            // Effacer le feedback apres 2 secondes
            StartFeedbackTimer();
        }

        _pendingTargetCount = 0;
        _pendingTargetLabel = string.Empty;

        AppliquerMateriauCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Lance un timer de 2 secondes pour effacer le texte de feedback (D-19).
    /// </summary>
    private void StartFeedbackTimer()
    {
        _feedbackTimer?.Stop();
        _feedbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _feedbackTimer.Tick += (_, _) =>
        {
            SetMatStatusText = string.Empty;
            _feedbackTimer.Stop();
        };
        _feedbackTimer.Start();
    }
}
