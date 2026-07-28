using System.Collections.Generic;
using System.Windows;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Views;

/// <summary>
/// Dialog pour choisir un groupe de destination lors du transfert de materiaux.
/// Fond sombre (theme Olympe), ComboBox de groupes + OK/Annuler.
/// </summary>
public partial class ChooseGroupDialog : Window
{
    /// <summary>
    /// Groupe selectionne par l'utilisateur (resultat du dialogue).
    /// </summary>
    public PresetGroupDto? SelectedGroup { get; private set; }

    public ChooseGroupDialog()
    {
        InitializeComponent();
        // Cycle 4 : la palette courante est superposee aux ressources de CETTE
        // fenetre (un add-in Revit n'a pas d'Application WPF a lui) et retiree
        // a la fermeture.
        Services.ThemeStore.RegisterHost(Resources);
        Closed += (_, _) => Services.ThemeStore.UnregisterHost(Resources);
    }

    /// <summary>
    /// Definit la liste des groupes disponibles dans le ComboBox.
    /// </summary>
    public void SetGroups(IEnumerable<PresetGroupDto> groups)
    {
        GroupComboBox.ItemsSource = groups;
    }

    /// <summary>
    /// Pre-selectionne un groupe dans le ComboBox.
    /// </summary>
    public void PreselectGroup(PresetGroupDto group)
    {
        GroupComboBox.SelectedItem = group;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (GroupComboBox.SelectedItem is not PresetGroupDto selected)
        {
            MessageBox.Show("Veuillez sélectionner un groupe.",
                "Sélection requise", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedGroup = selected;
        DialogResult = true;
    }

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
