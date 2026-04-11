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
        var name = NameTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Le nom ne peut pas etre vide.",
                "Nom requis", MessageBoxButton.OK, MessageBoxImage.Warning);
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
