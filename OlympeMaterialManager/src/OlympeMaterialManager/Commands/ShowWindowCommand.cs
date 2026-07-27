using System.IO;
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
                "Veuillez choisir un répertoire de projet pour stocker vos presets, scènes et paramètres.",
                "Olympe MaterialManager - Premier lancement",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.OK)
                return Result.Cancelled;

            var folder = DialogService.ShowFolderBrowser("Choisir le répertoire de projet");
            if (string.IsNullOrEmpty(folder))
                return Result.Cancelled;

            PresetService.SetProjectDirectory(folder!);
        }

        // FIA-01 : valider l'accessibilite du repertoire projet AVANT de construire
        // les ViewModels (repertoire OneDrive/reseau deconnecte, droits insuffisants...).
        // En cas d'echec, proposer de re-choisir un repertoire au lieu de propager l'exception.
        while (!IsProjectDirectoryAccessible(out var currentDir))
        {
            var retry = MessageBox.Show(
                $"Le répertoire de projet est inaccessible :\n{currentDir}\n\n" +
                "Il est peut-être déconnecté (réseau, OneDrive) ou vous n'avez pas les droits d'écriture.\n\n" +
                "Voulez-vous choisir un autre répertoire de projet ?",
                "Olympe MaterialManager - Répertoire inaccessible",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (retry != MessageBoxResult.Yes)
                return Result.Cancelled;

            var newFolder = DialogService.ShowFolderBrowser("Choisir le répertoire de projet");
            if (string.IsNullOrEmpty(newFolder))
                return Result.Cancelled;

            try
            {
                PresetService.SetProjectDirectory(newFolder!);
            }
            catch (Exception ex)
            {
                // Repertoire choisi lui-meme inaccessible : la boucle re-validera
                LogService.Error($"Echec de definition du repertoire de projet : {newFolder}", ex);
            }
        }

        if (App.MainWindow == null)
        {
            var vm = new MainWindowViewModel(App.EventBridge);
            App.MainWindow = new MainWindow { DataContext = vm };

            // UI-M9 : restaurer taille/position persistees (garde ecran dans WindowService)
            try
            {
                WindowService.RestoreWindowPlacement(App.MainWindow, new PresetService().LoadSettings());
            }
            catch (Exception ex)
            {
                LogService.Error("Echec de restauration de la position de la fenetre", ex);
            }

            // FIA-03 : dernier filet — toute exception WPF non geree est loggee et
            // neutralisee (e.Handled = true) pour ne jamais faire tomber le process
            // Revit hote. Enregistre une seule fois, a la creation de la fenetre.
            App.MainWindow.Dispatcher.UnhandledException += (_, args) =>
            {
                LogService.Error("Exception WPF non geree (dernier filet FIA-03)", args.Exception);
                MessageBox.Show(
                    "Une erreur inattendue s'est produite :\n" + args.Exception.Message +
                    "\n\nL'opération a été annulée. Détails dans le journal :\n" + LogService.LogPath,
                    "Olympe MaterialManager - Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            // Definir Revit comme fenetre proprietaire pour le Z-order
            var helper = new WindowInteropHelper(App.MainWindow);
            helper.Owner = commandData.Application.MainWindowHandle;

            // Intercepter la fermeture pour cacher au lieu de detruire (D-13)
            App.MainWindow.Closing += (sender, e) =>
            {
                // UI-M9 : persister taille/position a chaque fermeture (croix ou arret Revit)
                SaveWindowPlacementSafe(App.MainWindow);

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

    /// <summary>
    /// Persiste taille/position de la fenetre dans settings.json (UI-M9).
    /// Jamais d'exception propagee : appele depuis le handler Closing (FIA-03).
    /// </summary>
    private static void SaveWindowPlacementSafe(Window window)
    {
        try
        {
            var service = new PresetService();
            var settings = service.LoadSettings();
            WindowService.SaveWindowPlacement(window, settings);
            service.SaveSettings(settings);
        }
        catch (Exception ex)
        {
            LogService.Error("Echec de sauvegarde de la position de la fenetre", ex);
        }
    }

    /// <summary>
    /// Verifie que le repertoire de projet existe et est accessible en ecriture
    /// (ecriture puis suppression immediate d'un fichier temporaire). FIA-01.
    /// Si aucun repertoire n'est defini, le service retombe sur %APPDATA% : considere accessible.
    /// </summary>
    private static bool IsProjectDirectoryAccessible(out string directory)
    {
        var dir = PresetService.GetProjectDirectory();
        if (string.IsNullOrEmpty(dir))
        {
            directory = string.Empty;
            return true;
        }

        directory = dir!;
        try
        {
            if (!Directory.Exists(dir)) return false;
            var probe = Path.Combine(dir!, ".olympe-write-probe-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
