using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Views;

/// <summary>
/// Dialogue de selection d'un materiau projet pour ajout a un groupe preset (D-10).
/// Code-behind pour le lifecycle du dialogue -- pas de logique metier.
/// </summary>
public partial class AddMaterialDialog : Window
{
    private ICollectionView? _materialsView;

    /// <summary>
    /// Liste de tous les materiaux du projet (definie par le ViewModel appelant).
    /// </summary>
    public List<PresetMaterialDto> AllMaterials { get; set; } = new();

    /// <summary>
    /// Groupes de presets disponibles (defini par le ViewModel appelant).
    /// </summary>
    public ObservableCollection<PresetGroupDto> PresetGroups { get; set; } = new();

    /// <summary>
    /// Materiau selectionne par l'utilisateur (resultat du dialogue).
    /// </summary>
    public PresetMaterialDto? SelectedMaterial { get; set; }

    /// <summary>
    /// Groupe cible selectionne par l'utilisateur (resultat du dialogue).
    /// </summary>
    public PresetGroupDto? SelectedGroup { get; set; }

    public AddMaterialDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initialise le CollectionView avec le filtre de recherche.
    /// Doit etre appele apres avoir defini AllMaterials et PresetGroups.
    /// </summary>
    public void InitializeCollectionView()
    {
        _materialsView = CollectionViewSource.GetDefaultView(AllMaterials);
        _materialsView.Filter = FilterMaterial;
        MaterialList.ItemsSource = _materialsView;
        GroupCombo.ItemsSource = PresetGroups;

        if (PresetGroups.Count > 0)
            GroupCombo.SelectedIndex = 0;
    }

    /// <summary>
    /// Pre-selectionne un groupe dans le ComboBox.
    /// Doit etre appele apres InitializeCollectionView.
    /// </summary>
    public void PreselectGroup(PresetGroupDto group)
    {
        GroupCombo.SelectedItem = group;
    }

    private bool FilterMaterial(object obj)
    {
        if (obj is not PresetMaterialDto mat) return false;
        var filter = SearchBox.Text;
        if (string.IsNullOrWhiteSpace(filter)) return true;
        return mat.MaterialName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _materialsView?.Refresh();
    }

    private void Ajouter_Click(object sender, RoutedEventArgs e)
    {
        SelectedMaterial = MaterialList.SelectedItem as PresetMaterialDto;
        SelectedGroup = GroupCombo.SelectedItem as PresetGroupDto;

        if (SelectedMaterial == null || SelectedGroup == null)
        {
            MessageBox.Show("Selectionnez un materiau et un groupe cible.",
                "Selection requise", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
