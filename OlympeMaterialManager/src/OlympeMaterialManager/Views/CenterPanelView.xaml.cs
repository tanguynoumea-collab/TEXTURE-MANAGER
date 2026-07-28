using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.ViewModels;
using DragEventArgs = System.Windows.DragEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace Olympe.MaterialManager.Views;

/// <summary>
/// Code-behind pour CenterPanelView.
/// Aucune logique metier -- MVVM strict (D-15).
/// B3 : plomberie du drop d'un materiau preset sur une carte (couche ou
/// parametre) — la validation et l'application passent par les methodes
/// publiques de MainWindowViewModel (meme chemin transactionnel que
/// « Appliquer le matériau »). DR5-2 : le drop porte sur toute la selection
/// quand la carte visee en fait partie.
/// </summary>
public partial class CenterPanelView : UserControl
{
    /// <summary>
    /// Carte actuellement marquee comme cible de drop (B3), pour retirer la
    /// bordure accent des que le curseur la quitte.
    /// </summary>
    private ListBoxItem? _dropTargetItem;

    public CenterPanelView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Survol pendant un drag : bordure accent sur la carte cible valide,
    /// curseur « interdit » sinon (donnee absente, carte informative, ou
    /// application deja en cours — garde anti-reentrance, B3).
    /// </summary>
    private void CardList_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var item = FindCardUnderCursor(e);
        bool isBusy = GetMainWindowViewModel()?.IsSetMatBusy ?? true;

        if (isBusy ||
            !e.Data.GetDataPresent(PresetMaterialDto.DragDropFormat) ||
            item == null || !IsValidDropTarget(item.DataContext))
        {
            e.Effects = System.Windows.DragDropEffects.None;
            SetDropTarget(null);
            return;
        }

        // Le drag source (RightPanelView) n'autorise que l'effet Move
        e.Effects = System.Windows.DragDropEffects.Move;
        SetDropTarget(item);
    }

    /// <summary>
    /// Sortie de la zone : retour visuel normal (B3).
    /// </summary>
    private void CardList_DragLeave(object sender, DragEventArgs e)
    {
        SetDropTarget(null);
    }

    /// <summary>
    /// Drop : application immediate via la meme mecanique que
    /// MainWindowViewModel.AppliquerMateriau (B3). DR5-2 : la portee suit la
    /// convention des explorateurs — deposer sur une carte DE la selection
    /// applique a toute la selection, deposer hors selection applique a cette
    /// seule carte sans toucher a la selection. Le choix est delegue a
    /// DropTargetResolver (logique pure, testee).
    /// </summary>
    private void CardList_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var item = _dropTargetItem ?? FindCardUnderCursor(e);
        SetDropTarget(null);

        if (item == null) return;
        if (!e.Data.GetDataPresent(PresetMaterialDto.DragDropFormat)) return;
        if (e.Data.GetData(PresetMaterialDto.DragDropFormat) is not PresetMaterialDto presetMat) return;

        var mainVm = GetMainWindowViewModel();
        if (mainVm == null) return;

        var selection = mainVm.CenterPanelVM.SelectedItems;

        switch (item.DataContext)
        {
            case LayerDto layer:
                mainVm.AppliquerMateriauSurCouches(
                    presetMat,
                    DropTargetResolver.ResolveDropTargets(layer, selection));
                break;
            case MaterialParamDto param:
                mainVm.AppliquerMateriauSurParametres(
                    presetMat,
                    DropTargetResolver.ResolveDropTargets(param, selection));
                break;
        }
    }

    /// <summary>
    /// Deplace la marque « cible de drop » (propriete attachee consommee par le
    /// Trigger de CardItemStyle) d'une carte a l'autre, sans doublon.
    /// </summary>
    private void SetDropTarget(ListBoxItem? item)
    {
        if (ReferenceEquals(_dropTargetItem, item)) return;

        if (_dropTargetItem != null)
            DropTargetIndicator.SetIsDropTarget(_dropTargetItem, false);

        _dropTargetItem = item;

        if (item != null)
            DropTargetIndicator.SetIsDropTarget(item, true);
    }

    /// <summary>
    /// Une carte est une cible valide si elle porte une couche, ou un parametre
    /// materiau reel (les entrees informatives « Aucun paramètre matériau » ont
    /// une definition vide).
    /// </summary>
    private static bool IsValidDropTarget(object? dataContext) => dataContext switch
    {
        LayerDto => true,
        MaterialParamDto param => !string.IsNullOrEmpty(param.ParameterDefinitionName),
        _ => false
    };

    /// <summary>
    /// Remonte l'arbre visuel depuis la source de l'evenement jusqu'a la carte
    /// (ListBoxItem) survolee, ou null hors carte.
    /// </summary>
    private static ListBoxItem? FindCardUnderCursor(DragEventArgs e)
    {
        var current = e.OriginalSource as DependencyObject;
        while (current != null)
        {
            if (current is ListBoxItem item) return item;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>
    /// Retrouve le ViewModel racine (proprietaire du chemin Set Mat et de la
    /// garde IsSetMatBusy) via la fenetre hote.
    /// </summary>
    private MainWindowViewModel? GetMainWindowViewModel()
        => Window.GetWindow(this)?.DataContext as MainWindowViewModel;
}
