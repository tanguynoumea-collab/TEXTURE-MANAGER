using System.Windows;

namespace Olympe.MaterialManager.Views;

/// <summary>
/// Dialog simple et reutilisable pour saisir un nom (scene, groupe preset, etc.).
/// Fond sombre (theme Olympe), TextBox + OK/Annuler.
/// </summary>
public partial class CreateNameDialog : Window
{
    /// <summary>
    /// Nom saisi par l'utilisateur (resultat du dialogue).
    /// </summary>
    public string EnteredName { get; private set; } = string.Empty;

    public CreateNameDialog()
    {
        InitializeComponent();
        // Cycle 4 : la palette courante est superposee aux ressources de CETTE
        // fenetre (un add-in Revit n'a pas d'Application WPF a lui) et retiree
        // a la fermeture.
        Services.ThemeStore.RegisterHost(Resources);
        Closed += (_, _) => Services.ThemeStore.UnregisterHost(Resources);
        Loaded += (_, _) => NameTextBox.Focus();
    }

    /// <summary>
    /// Configure le label de l'invite.
    /// </summary>
    public void SetPrompt(string prompt)
    {
        PromptLabel.Text = prompt;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim() ?? string.Empty;

        // SEC-01 : validation canonique (vide, caracteres interdits, noms reserves Windows)
        var error = Services.PresetService.ValidateFileName(name);
        if (error != null)
        {
            MessageBox.Show(error, "Nom invalide",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        EnteredName = name;
        DialogResult = true;
    }

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
