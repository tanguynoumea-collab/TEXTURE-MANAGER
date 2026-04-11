using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.ViewModels;

namespace Olympe.MaterialManager.Views;

/// <summary>
/// Code-behind pour RightPanelView.
/// Gere le drag and drop des materiaux entre groupes dans le TreeView.
/// </summary>
public partial class RightPanelView : UserControl
{
    private Point _dragStartPoint;
    private PresetMaterialDto? _draggedMaterial;

    public RightPanelView()
    {
        InitializeComponent();
    }

    private void PresetTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void PresetTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // Trouver l'element sous la souris
        if (e.OriginalSource is not DependencyObject source) return;
        var treeViewItem = FindAncestor<TreeViewItem>(source);
        if (treeViewItem?.DataContext is not PresetMaterialDto material) return;

        _draggedMaterial = material;
        var data = new DataObject("PresetMaterial", material);
        DragDrop.DoDragDrop(treeViewItem, data, DragDropEffects.Move);
    }

    private void PresetTreeView_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("PresetMaterial"))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Verifier qu'on survole un groupe ou un materiau d'un groupe
        var target = GetTargetGroup(e);
        e.Effects = target != null ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void PresetTreeView_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("PresetMaterial") || _draggedMaterial == null) return;

        var targetGroup = GetTargetGroup(e);
        if (targetGroup == null) return;

        var vm = DataContext as RightPanelViewModel;
        if (vm == null) return;

        // Trouver le groupe source
        var sourceGroup = FindGroupContaining(vm, _draggedMaterial);
        if (sourceGroup == null) return;

        vm.MoveMaterial(_draggedMaterial, sourceGroup, targetGroup);
        _draggedMaterial = null;
    }

    /// <summary>
    /// Determine le groupe cible sous le curseur lors du drop.
    /// </summary>
    private PresetGroupDto? GetTargetGroup(DragEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return null;
        var treeViewItem = FindAncestor<TreeViewItem>(source);
        if (treeViewItem == null) return null;

        if (treeViewItem.DataContext is PresetGroupDto group)
            return group;

        if (treeViewItem.DataContext is PresetMaterialDto mat)
        {
            // Trouver le TreeViewItem parent (le groupe)
            var parentItem = FindAncestor<TreeViewItem>(
                VisualTreeHelper.GetParent(treeViewItem));
            return parentItem?.DataContext as PresetGroupDto;
        }

        return null;
    }

    private PresetGroupDto? FindGroupContaining(RightPanelViewModel vm, PresetMaterialDto material)
    {
        foreach (var group in vm.PresetGroups)
        {
            if (group.Materials.Contains(material))
                return group;
        }
        return null;
    }

    /// <summary>
    /// Remonte l'arbre visuel pour trouver un ancetre du type specifie.
    /// </summary>
    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T target) return target;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
