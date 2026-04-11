using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;
using Olympe.MaterialManager.ViewModels;
using Olympe.MaterialManager.Views;

namespace Olympe.MaterialManager.Commands;

/// <summary>
/// Commande ribbon pour afficher la fenetre principale modeless singleton (D-13).
/// La fenetre est creee une seule fois, puis montree/cachee.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class ShowWindowCommand : ExternalCommand
{
    public override void Execute()
    {
        if (App.MainWindow == null)
        {
            var vm = new MainWindowViewModel(App.EventBridge);
            App.MainWindow = new MainWindow { DataContext = vm };

            // Definir Revit comme fenetre proprietaire pour le Z-order
            var helper = new WindowInteropHelper(App.MainWindow);
            helper.Owner = Application.MainWindowHandle;

            // Intercepter la fermeture pour cacher au lieu de detruire (D-13)
            App.MainWindow.Closing += (sender, e) =>
            {
                if (!App.AllowClose)
                {
                    e.Cancel = true;
                    App.MainWindow.Hide();
                }
            };
        }

        App.MainWindow.Show();
        App.MainWindow.Activate();
    }
}
