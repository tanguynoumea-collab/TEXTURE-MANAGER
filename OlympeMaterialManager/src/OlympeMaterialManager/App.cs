using System.Reflection;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Olympe.MaterialManager.Commands;
using Olympe.MaterialManager.Events;
using Olympe.MaterialManager.Views;
using ToolkitExternalEvent = Nice3point.Revit.Toolkit.External.ExternalEvent;

namespace Olympe.MaterialManager;

/// <summary>
/// Point d'entree IExternalApplication via Nice3point ExternalApplication (D-12).
/// Cree le singleton ExternalEvent et le bouton ribbon au demarrage.
/// </summary>
public class App : ExternalApplication
{
    internal static ToolkitExternalEvent RevitEvent { get; private set; } = null!;
    internal static RevitEventBridge EventBridge { get; private set; } = null!;
    internal static MainWindow? MainWindow { get; set; }

    /// <summary>
    /// Flag pour permettre la fermeture reelle lors du shutdown Revit.
    /// </summary>
    internal static bool AllowClose { get; set; }

    public override void OnStartup()
    {
        // Creer le bridge et l'ExternalEvent (D-09)
        EventBridge = new RevitEventBridge();
        RevitEvent = new ToolkitExternalEvent(uiApp =>
        {
            EventBridge.ProcessRequest(uiApp);
        });

        // Creer le panneau ribbon avec bouton (UI-04 : label en francais)
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var commandTypeName = typeof(ShowWindowCommand).FullName!;

        Application.CreateRibbonTab("Olympe");
        var panel = Application.CreateRibbonPanel("Olympe", "Olympe MaterialManager");
        var buttonData = new PushButtonData(
            "ShowMaterialManager",
            "Materiaux",
            assemblyPath,
            commandTypeName);
        panel.AddItem(buttonData);
    }

    public override void OnShutdown()
    {
        if (MainWindow != null)
        {
            AllowClose = true;
            MainWindow.Close();
            MainWindow = null;
        }
        // Note: Nice3point Toolkit ExternalEvent n'est pas IDisposable.
        // Le GC gerera le nettoyage a la fermeture de Revit.
    }
}
