using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Converters;

/// <summary>
/// Convertisseur multi-valeurs [PreviewMode, ColorArgb (int ou int?),
/// AppearanceColorArgb (int?)] → Brush, pour l'aperçu du visualisateur, le
/// liseré des cartes (B8) et les pastilles des presets. Le changement de mode
/// se propage par le binding sur PreviewModeStore.CurrentMode (INPC) — pas
/// d'abonnement message nécessaire.
/// - Mode Couleur : SolidColorBrush de ColorArgb.
/// - Mode Réaliste (DR2-2) : SolidColorBrush de AppearanceColorArgb (couleur
///   diffuse/albedo de l'asset d'apparence) ; absente → fallback ColorArgb.
/// - Couleur effective null (« Par catégorie » sans apparence) : transparent,
///   jamais un gris menteur.
/// Toutes les brushes retournées sont figées (Freeze).
/// </summary>
public class MaterialPreviewBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var mode = values.Length > 0 && values[0] is PreviewMode m ? m : PreviewMode.UniformColor;
        int? colorArgb = values.Length > 1 && values[1] is int argb ? argb : null;
        int? appearanceArgb = values.Length > 2 && values[2] is int appearance ? appearance : null;

        var effectiveArgb = mode == PreviewMode.Realistic
            ? appearanceArgb ?? colorArgb
            : colorArgb;

        if (effectiveArgb is null)
            return Brushes.Transparent;

        var (a, r, g, b) = ArgbUtils.UnpackArgb(effectiveArgb.Value);
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
