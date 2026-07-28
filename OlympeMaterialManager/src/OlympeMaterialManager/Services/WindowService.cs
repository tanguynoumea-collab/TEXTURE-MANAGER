using System.Windows;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Services;

/// <summary>
/// Service d'acces a la visibilite de la fenetre principale pour les ViewModels (ARC-05).
/// Le bridge Revit ne touche jamais a la fenetre WPF : c'est le ViewModel appelant qui
/// cache la fenetre avant un pick 3D et la re-affiche dans son callback.
/// Gere aussi la persistance taille/position de la fenetre principale (UI-M9).
/// A appeler uniquement depuis le thread UI WPF.
/// </summary>
public static class WindowService
{
    /// <summary>
    /// Cache la fenetre principale si elle existe.
    /// </summary>
    public static void HideMainWindow() => App.MainWindow?.Hide();

    /// <summary>
    /// Re-affiche la fenetre principale si elle existe.
    /// </summary>
    public static void ShowMainWindow() => App.MainWindow?.Show();

    /// <summary>
    /// Suspend temporairement le Topmost de la fenetre principale (B9) : un
    /// dialogue modal Revit (ex. gestionnaire de materiaux) s'ouvrirait derriere
    /// une fenetre toujours-au-premier-plan. Le Topmost est retabli des que la
    /// fenetre principale est re-activee. No-op si Topmost n'est pas actif.
    /// </summary>
    public static void SuspendTopmostUntilReactivated()
    {
        var window = App.MainWindow;
        if (window is not { Topmost: true }) return;

        window.Topmost = false;

        void RestoreTopmost(object? sender, EventArgs e)
        {
            window.Activated -= RestoreTopmost;
            window.Topmost = true;
        }

        window.Activated += RestoreTopmost;
    }

    /// <summary>
    /// Restaure taille/position de la fenetre depuis les settings (UI-M9).
    /// Garde ecran : la fenetre est ramenee dans les limites de l'ecran virtuel
    /// (multi-moniteurs) et ne descend jamais sous MinWidth/MinHeight.
    /// Ne fait rien si aucune valeur n'a encore ete persistee.
    /// </summary>
    public static void RestoreWindowPlacement(Window window, AppSettingsDto settings)
    {
        if (settings.WindowWidth is not double width ||
            settings.WindowHeight is not double height ||
            settings.WindowLeft is not double left ||
            settings.WindowTop is not double top)
        {
            return;
        }

        if (double.IsNaN(width) || double.IsNaN(height) ||
            double.IsNaN(left) || double.IsNaN(top))
        {
            return;
        }

        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;
        var screenWidth = SystemParameters.VirtualScreenWidth;
        var screenHeight = SystemParameters.VirtualScreenHeight;

        // MinWidth/MinHeight respectes, et jamais plus grand que l'ecran virtuel
        width = Math.Min(Math.Max(width, window.MinWidth), screenWidth);
        height = Math.Min(Math.Max(height, window.MinHeight), screenHeight);

        // Garde : ne pas restaurer hors des limites de l'ecran virtuel
        left = Math.Min(Math.Max(left, screenLeft), screenLeft + screenWidth - width);
        top = Math.Min(Math.Max(top, screenTop), screenTop + screenHeight - height);

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = width;
        window.Height = height;
        window.Left = left;
        window.Top = top;
    }

    /// <summary>
    /// Reporte taille/position courantes de la fenetre dans les settings (UI-M9).
    /// Fenetre maximisee/minimisee : ce sont les RestoreBounds qui sont persistes.
    /// </summary>
    public static void SaveWindowPlacement(Window window, AppSettingsDto settings)
    {
        double width, height, left, top;

        if (window.WindowState == WindowState.Normal)
        {
            width = window.Width;
            height = window.Height;
            left = window.Left;
            top = window.Top;
        }
        else
        {
            var bounds = window.RestoreBounds;
            if (bounds.IsEmpty) return;
            width = bounds.Width;
            height = bounds.Height;
            left = bounds.Left;
            top = bounds.Top;
        }

        if (double.IsNaN(width) || double.IsNaN(height) ||
            double.IsNaN(left) || double.IsNaN(top))
        {
            return;
        }

        settings.WindowWidth = width;
        settings.WindowHeight = height;
        settings.WindowLeft = left;
        settings.WindowTop = top;
    }
}
