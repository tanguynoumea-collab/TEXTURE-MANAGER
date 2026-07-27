namespace Olympe.MaterialManager.Services;

/// <summary>
/// Service d'acces a la visibilite de la fenetre principale pour les ViewModels (ARC-05).
/// Le bridge Revit ne touche jamais a la fenetre WPF : c'est le ViewModel appelant qui
/// cache la fenetre avant un pick 3D et la re-affiche dans son callback.
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
}
