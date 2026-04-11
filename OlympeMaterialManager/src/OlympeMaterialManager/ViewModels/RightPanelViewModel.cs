using CommunityToolkit.Mvvm.ComponentModel;

namespace Olympe.MaterialManager.ViewModels;

/// <summary>
/// ViewModel du panneau droit (Materiaux Preset).
/// Shell -- la logique sera ajoutee en Phase 3.
/// </summary>
public partial class RightPanelViewModel : ObservableObject
{
    [ObservableProperty]
    private string _panelTitle = "Materiaux Preset";
}
