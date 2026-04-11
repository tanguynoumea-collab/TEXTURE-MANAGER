using CommunityToolkit.Mvvm.ComponentModel;

namespace Olympe.MaterialManager.ViewModels;

/// <summary>
/// ViewModel du panneau central (Couches / Parametres).
/// Shell -- la logique sera ajoutee en Phase 2.
/// </summary>
public partial class CenterPanelViewModel : ObservableObject
{
    [ObservableProperty]
    private string _panelTitle = "Couches / Parametres";
}
