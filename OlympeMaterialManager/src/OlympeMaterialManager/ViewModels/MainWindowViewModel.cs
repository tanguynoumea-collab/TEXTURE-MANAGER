using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Olympe.MaterialManager.Events;
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

    [ObservableProperty]
    private string _titre = "Olympe MaterialManager";

    [ObservableProperty]
    private string _documentInfo = "Aucun document";

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
        LeftPanelVM = new LeftPanelViewModel(eventBridge);
        CenterPanelVM = new CenterPanelViewModel(eventBridge);
        RightPanelVM = new RightPanelViewModel(eventBridge, presetService);

        // Surveiller SelectedPresetMaterial pour rafraichir CanExecute de commandes dependantes (Plan 03)
        RightPanelVM.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RightPanelViewModel.SelectedPresetMaterial))
                OnSelectedPresetMaterialChanged();
        };
    }

    /// <summary>
    /// Constructeur sans parametre pour le designer WPF.
    /// </summary>
    public MainWindowViewModel() : this(null!)
    {
    }

    /// <summary>
    /// Hook pour reagir au changement de SelectedPresetMaterial dans RightPanelVM.
    /// Plan 03 ajoutera ici le NotifyCanExecuteChanged de AppliquerMateriauCommand.
    /// </summary>
    private void OnSelectedPresetMaterialChanged()
    {
        // Placeholder -- Plan 03 ajoutera : AppliquerMateriauCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Commande pour rafraichir les informations du document via ExternalEvent round-trip.
    /// Prouve le pipeline complet UI -> ExternalEvent -> Revit API -> DTO -> ViewModel.
    /// </summary>
    [RelayCommand]
    private void RafraichirDocument()
    {
        _eventBridge?.MakeRequest(
            RevitRequestType.GetDocumentInfo,
            null,
            result =>
            {
                if (result is RevitDocInfoDto info)
                    DocumentInfo = info.IsValid ? $"Document : {info.Title}" : "Aucun document ouvert";
                else if (result is Exception ex)
                    DocumentInfo = $"Erreur : {ex.Message}";
            });
    }
}
