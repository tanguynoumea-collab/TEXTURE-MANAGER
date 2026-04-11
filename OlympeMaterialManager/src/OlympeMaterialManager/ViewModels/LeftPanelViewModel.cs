using CommunityToolkit.Mvvm.ComponentModel;

namespace Olympe.MaterialManager.ViewModels;

/// <summary>
/// ViewModel du panneau gauche (Familles / Types).
/// Shell -- la logique TreeView sera ajoutee en Phase 2.
/// </summary>
public partial class LeftPanelViewModel : ObservableObject
{
    [ObservableProperty]
    private string _panelTitle = "Familles / Types";
}
