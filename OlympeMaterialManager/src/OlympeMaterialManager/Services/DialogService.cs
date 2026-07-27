namespace Olympe.MaterialManager.Services;

/// <summary>
/// Service pour les dialogues systeme (D-04) et les boites de message (ARC-01).
/// Utilise OpenFolderDialog sur .NET 8+ et FolderBrowserDialog sur net48.
/// Les ViewModels passent par ce service au lieu d'appeler MessageBox directement.
/// </summary>
public static class DialogService
{
    private const string DefaultTitle = "Olympe MaterialManager";

    /// <summary>
    /// Demande une confirmation Oui/Non. Retourne true si l'utilisateur confirme.
    /// </summary>
    public static bool Confirm(string message, string titre)
    {
        return System.Windows.MessageBox.Show(
                   message, titre,
                   System.Windows.MessageBoxButton.YesNo,
                   System.Windows.MessageBoxImage.Question)
               == System.Windows.MessageBoxResult.Yes;
    }

    /// <summary>
    /// Demande une confirmation Oui/Non/Annuler.
    /// Retourne true (Oui), false (Non) ou null (Annuler).
    /// </summary>
    public static bool? ConfirmWithCancel(string message, string titre)
    {
        var result = System.Windows.MessageBox.Show(
            message, titre,
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Question);
        return result switch
        {
            System.Windows.MessageBoxResult.Yes => true,
            System.Windows.MessageBoxResult.No => false,
            _ => null
        };
    }

    /// <summary>
    /// Affiche un message d'erreur.
    /// </summary>
    public static void ShowError(string message)
    {
        System.Windows.MessageBox.Show(
            message, DefaultTitle,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    /// <summary>
    /// Affiche un message d'information.
    /// </summary>
    public static void ShowInfo(string message)
    {
        System.Windows.MessageBox.Show(
            message, DefaultTitle,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// Affiche un dialogue de selection de dossier.
    /// Retourne le chemin selectionne ou null si annule.
    /// </summary>
    public static string? ShowFolderBrowser(string title)
    {
#if REVIT2025_OR_GREATER
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
#else
        // net48 : utiliser le FolderBrowserDialog WinForms
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = title,
            ShowNewFolderButton = true
        };
        var result = dialog.ShowDialog();
        return result == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
#endif
    }
}
