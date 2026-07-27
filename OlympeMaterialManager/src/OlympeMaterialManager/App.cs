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
        var buttonData = new PushButtonData(
            "ShowMaterialManager",
            "Matériaux",
            assemblyPath,
            commandTypeName)
        {
            ToolTip = "Ouvrir l'éditeur de matériaux Olympe",
            LargeImage = LoadIcon("olympe-icon-32.png"),
            Image = LoadIcon("olympe-icon-16.png")
        };
        panel.AddItem(buttonData);

        return Result.Succeeded;
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

    private static BitmapImage LoadIcon(string fileName)
    {
        var uri = new Uri($"pack://application:,,,/OlympeMaterialManager;component/Resources/{fileName}");
        return new BitmapImage(uri);
    }
}
