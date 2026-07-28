using System.Windows;

namespace Olympe.MaterialManager.Helpers;

/// <summary>
/// Propriete attachee « cible de drop » (B3) : posee par le code-behind de
/// CenterPanelView sur la carte survolee pendant un drag de materiau preset,
/// consommee par un Trigger de CardItemStyle (bordure accent — meme vocabulaire
/// visuel que la selection, token AccentBrush, aucune couleur en dur).
/// </summary>
public static class DropTargetIndicator
{
    /// <summary>
    /// Propriete attachee booleenne : true tant que la carte est une cible de
    /// drop valide survolee ; remise a false des que le curseur la quitte.
    /// </summary>
    public static readonly DependencyProperty IsDropTargetProperty =
        DependencyProperty.RegisterAttached(
            "IsDropTarget",
            typeof(bool),
            typeof(DropTargetIndicator),
            new PropertyMetadata(false));

    /// <summary>Accesseur XAML de la propriete attachee.</summary>
    public static bool GetIsDropTarget(DependencyObject obj)
        => (bool)obj.GetValue(IsDropTargetProperty);

    /// <summary>Mutateur XAML de la propriete attachee.</summary>
    public static void SetIsDropTarget(DependencyObject obj, bool value)
        => obj.SetValue(IsDropTargetProperty, value);
}
