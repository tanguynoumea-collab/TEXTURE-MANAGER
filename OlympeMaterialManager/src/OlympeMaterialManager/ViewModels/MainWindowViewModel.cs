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
    /// Constructeur principal avec injection du bridge ExternalEvent.
    /// </summary>
    public MainWindowViewModel(RevitEventBridge eventBridge)
    {
        _eventBridge = eventBridge;
        var presetService = new PresetService();
        LeftPanelVM = new LeftPanelViewModel(eventBridge, presetService);
        CenterPanelVM = new CenterPanelViewModel(eventBridge);
        RightPanelVM = new RightPanelViewModel(eventBridge, presetService);

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
    private void OuvrirRepertoire()
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
        var newPath = DialogService.ShowFolderBrowser("Choisir le nouveau repertoire de projet");
        if (string.IsNullOrEmpty(newPath)) return;

        try
        {
            PresetService.MigrateProjectDirectory(newPath!);
            ProjectDirectoryPath = newPath!;

            System.Windows.MessageBox.Show(
                $"Repertoire de projet migre avec succes vers :\n{newPath}",
                "Olympe MaterialManager",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Erreur lors de la migration :\n{ex.Message}",
                "Olympe MaterialManager",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
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

            if (layerIndices == null || layerIndices.Length == 0)
            {
                IsSetMatBusy = false;
                return;
            }

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
        else if (CenterPanelVM.ShowParameters)
        {
            // D-17 : parametres materiaux de familles chargees
            var paramNames = CenterPanelVM.SelectedItems?
                .Cast<MaterialParamDto>()
                .Select(p => p.ParameterDefinitionName)
                .ToArray();

            if (paramNames == null || paramNames.Length == 0)
            {
                IsSetMatBusy = false;
                return;
            }

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
        else
        {
            IsSetMatBusy = false;
        }
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
            System.Windows.MessageBox.Show(
                $"Erreur lors de l'application du materiau :\n{ex.Message}",
                "Olympe MaterialManager",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        else
        {
            // D-19 : retour visuel succes
            SetMatStatusText = "Materiau applique !";

            // D-25 : rafraichir le panneau central pour afficher les nouveaux noms de materiaux
            WeakReferenceMessenger.Default.Send(
                new RefreshLayersMessage(CenterPanelVM.CurrentTypeIdValue));

            // Effacer le feedback apres 2 secondes
            StartFeedbackTimer();
        }

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
