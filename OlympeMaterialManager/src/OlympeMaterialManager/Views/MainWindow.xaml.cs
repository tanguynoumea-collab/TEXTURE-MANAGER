using System.Windows;

namespace Olympe.MaterialManager.Views;

/// <summary>
/// Code-behind minimal pour MainWindow.
/// Aucune logique metier -- MVVM strict (D-15).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Cycle 4 : la palette courante est superposee aux ressources de CETTE
        // fenetre (un add-in Revit n'a pas d'Application WPF a lui) et retiree
        // a la fermeture.
        Services.ThemeStore.RegisterHost(Resources);
        Closed += (_, _) => Services.ThemeStore.UnregisterHost(Resources);
    }
}
