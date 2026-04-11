using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Olympe.MaterialManager.Services;
using Olympe.MaterialManager.ViewModels;
using Olympe.MaterialManager.Views;

namespace Olympe.MaterialManager.Commands;

/// <summary>
/// Commande ribbon pour afficher la fenetre principale modeless singleton (D-13).
/// La fenetre est creee une seule fois, puis montree/cachee.
/// Au premier lancement, demande a l'utilisateur de choisir un repertoire de projet.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class ShowWindowCommand : IExternalCommand
{
    private static bool _themeLoaded;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        // Charger le theme dans les ressources globales WPF AVANT de creer la fenetre.
        // Dans un add-in Revit, il n'y a pas de App.xaml WPF -- les ressources doivent
        // etre injectees dans Application.Current.Resources.
        if (!_themeLoaded && Application.Current != null)
        {
            var theme = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/OlympeMaterialManager;component/Themes/OlympeTheme.xaml")
            };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            _themeLoaded = true;
        }

        // Premier lancement : demander le repertoire de projet si non defini
        if (!PresetService.IsProjectDirectorySet())
        {
            var result = MessageBox.Show(
                "Bienvenue dans Olympe MaterialManager !\n\n" +
                "Veuillez choisir un repertoire de projet pour stocker vos presets, scenes et parametres.",
                "Olympe MaterialManager - Premier lancement",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.OK)
                return Result.Cancelled;

            var folder = DialogService.ShowFolderBrowser("Choisir le repertoire de projet");
            if (string.IsNullOrEmpty(folder))
                return Result.Cancelled;

            PresetService.SetProjectDirectory(folder!);
        }

        if (App.MainWindow == null)
        {
            var vm = new MainWindowViewModel(App.EventBridge);
            App.MainWindow = new MainWindow { DataContext = vm };

            // Definir Revit comme fenetre proprietaire pour le Z-order
            var helper = new WindowInteropHelper(App.MainWindow);
            helper.Owner = commandData.Application.MainWindowHandle;

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
        return Result.Succeeded;
    }
}
