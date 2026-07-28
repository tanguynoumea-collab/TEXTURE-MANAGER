using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Olympe.MaterialManager.Commands;
using Olympe.MaterialManager.Events;
using Olympe.MaterialManager.Services;
using Olympe.MaterialManager.Views;

namespace Olympe.MaterialManager;

/// <summary>
/// Point d'entree IExternalApplication.
/// Cree le singleton ExternalEvent et le bouton ribbon au demarrage.
/// </summary>
public class App : IExternalApplication
{
    internal static ExternalEvent RevitEvent { get; private set; } = null!;
    internal static RevitEventBridge EventBridge { get; private set; } = null!;
    internal static MainWindow? MainWindow { get; set; }

    /// <summary>
    /// Flag pour permettre la fermeture reelle lors du shutdown Revit.
    /// </summary>
    internal static bool AllowClose { get; set; }

    public Result OnStartup(UIControlledApplication application)
    {
        LogService.Log("=== OnStartup: Olympe MaterialManager demarrage ===");
        LogService.Log($"Log file: {LogService.LogPath}");
        EventBridge = new RevitEventBridge();
        RevitEvent = ExternalEvent.Create(EventBridge);
        LogService.Log("OnStartup: ExternalEvent created");

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var commandTypeName = typeof(ShowWindowCommand).FullName!;

        // Placer dans l'onglet "Complement" (Add-Ins) — pas d'onglet custom
        var panel = application.CreateRibbonPanel("Olympe MaterialManager");

        // Cycle 4 : le logo existe en deux versions. Le ruban sombre de Revit
        // avale un logo a corps fonce, le ruban clair avale un logo a corps clair.
        var suffix = IsRevitThemeDark() ? "-dark" : string.Empty;

        var buttonData = new PushButtonData(
            "ShowMaterialManager",
            "Matériaux",
            assemblyPath,
            commandTypeName)
        {
            ToolTip = "Ouvrir l'éditeur de matériaux Olympe",
            // 64 px en grande icone : Revit met a l'echelle vers le bas et sert
            // le rendu net des ecrans haute densite.
            LargeImage = LoadIcon($"olympe-icon-64{suffix}.png"),
            Image = LoadIcon($"olympe-icon-16{suffix}.png")
        };
        panel.AddItem(buttonData);

        return Result.Succeeded;
    }

    /// <summary>
    /// Thème du ruban Revit. <c>UIThemeManager</c> est present dans les assemblies
    /// referencees pour les deux cibles, mais l'appel reste protege : sur un hote
    /// plus ancien que prevu, l'absence du type ou de la propriete leverait au
    /// chargement de l'add-in. Tout echec retombe silencieusement sur le jeu
    /// clair — l'icone reste visible, seul son contraste est sous-optimal.
    /// </summary>
    private static bool IsRevitThemeDark()
    {
        try
        {
            return UIThemeManager.CurrentTheme == UITheme.Dark;
        }
        catch (Exception ex)
        {
            LogService.Error("Theme Revit indetermine, repli sur le jeu d'icones clair", ex);
            return false;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        if (MainWindow != null)
        {
            AllowClose = true;
            MainWindow.Close();
            MainWindow = null;
        }
        RevitEvent?.Dispose();
        return Result.Succeeded;
    }

    /// <summary>
    /// Charge une icone embarquee. Renvoie null plutot que de lever : une icone
    /// manquante donne un bouton sans image, jamais un add-in qui ne charge pas.
    /// </summary>
    private static BitmapImage? LoadIcon(string fileName)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/OlympeMaterialManager;component/Resources/{fileName}");
            return new BitmapImage(uri);
        }
        catch (Exception ex)
        {
            LogService.Error($"Echec de chargement de l'icone {fileName}", ex);
            return null;
        }
    }
}
