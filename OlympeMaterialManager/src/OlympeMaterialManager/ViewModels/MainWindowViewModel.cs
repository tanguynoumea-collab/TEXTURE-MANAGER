using CommunityToolkit.Mvvm.ComponentModel;

namespace Olympe.MaterialManager.ViewModels;

/// <summary>
/// ViewModel racine coordonnant les trois panneaux de l'interface.
/// Aucune dependance Revit API -- uniquement MVVM pur (D-15, INFRA-07).
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _titre = "Olympe MaterialManager";

    [ObservableProperty]
    private string _documentInfo = "Aucun document";

    public LeftPanelViewModel LeftPanelVM { get; }
    public CenterPanelViewModel CenterPanelVM { get; }
    public RightPanelViewModel RightPanelVM { get; }

    public MainWindowViewModel()
    {
        LeftPanelVM = new LeftPanelViewModel();
        CenterPanelVM = new CenterPanelViewModel();
        RightPanelVM = new RightPanelViewModel();
    }
}
