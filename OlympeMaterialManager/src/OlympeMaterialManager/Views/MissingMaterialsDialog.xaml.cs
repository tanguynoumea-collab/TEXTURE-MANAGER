using System.Windows;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Views;

/// <summary>
/// Dialogue B1 « Matériaux introuvables » : liste les materiaux du preset actif
/// absents du document Revit. Deux issues : « Supprimer du preset » (DialogResult
/// true — action destructive en style discret, le preset est un fichier partage
/// d'equipe) ou « Conserver » (defaut, IsCancel : Echap et fermeture = conserver).
/// </summary>
public partial class MissingMaterialsDialog : Window
{
    public MissingMaterialsDialog()
    {
        InitializeComponent();
        // Cycle 4 : la palette courante est superposee aux ressources de CETTE
        // fenetre (un add-in Revit n'a pas d'Application WPF a lui) et retiree
        // a la fermeture.
        Services.ThemeStore.RegisterHost(Resources);
        Closed += (_, _) => Services.ThemeStore.UnregisterHost(Resources);
    }

    /// <summary>
    /// Renseigne le message (nom du preset) et la liste des materiaux introuvables
    /// (pastille de couleur + nom).
    /// </summary>
    public void SetContent(string presetName, IEnumerable<PresetMaterialDto> missingMaterials)
    {
        MessageText.Text =
            $"Ces matériaux du preset « {presetName} » n'existent pas dans ce document. " +
            "Réimportez votre matériauthèque, ou supprimez-les du preset.";
        MaterialsList.ItemsSource = missingMaterials.ToList();
    }

    private void Supprimer_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Conserver_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
