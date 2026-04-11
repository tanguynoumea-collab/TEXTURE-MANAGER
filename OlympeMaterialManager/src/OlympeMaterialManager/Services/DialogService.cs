namespace Olympe.MaterialManager.Services;

/// <summary>
/// Service pour les dialogues systeme (D-04).
/// Utilise OpenFolderDialog sur .NET 8+ et FolderBrowserDialog sur net48.
/// </summary>
public static class DialogService
{
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
