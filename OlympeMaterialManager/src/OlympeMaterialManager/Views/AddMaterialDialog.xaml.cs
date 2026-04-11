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
    /// Materiau selectionne par l'utilisateur (resultat du dialogue). Garde pour compatibilite.
    /// </summary>
    public PresetMaterialDto? SelectedMaterial { get; set; }

    /// <summary>
    /// Liste des materiaux selectionnes (multi-selection).
    /// </summary>
    public List<PresetMaterialDto> SelectedMaterials { get; set; } = new();

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
        SelectedGroup = GroupCombo.SelectedItem as PresetGroupDto;

        // Recuperer tous les materiaux selectionnes
        SelectedMaterials.Clear();
        foreach (var item in MaterialList.SelectedItems)
        {
            if (item is PresetMaterialDto mat)
                SelectedMaterials.Add(mat);
        }

        // Compatibilite : garder le premier selectionne
        SelectedMaterial = SelectedMaterials.Count > 0 ? SelectedMaterials[0] : null;

        if (SelectedMaterials.Count == 0 || SelectedGroup == null)
        {
            MessageBox.Show("Selectionnez au moins un materiau et un groupe cible.",
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
