using System.Globalization;
using System.Windows.Data;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Converters;

/// <summary>
/// Convertisseur multi-valeurs [PreviewMode, AppearanceColorArgb (int?)] →
/// texte d'explication du fallback de l'aperçu du visualisateur (DR2-2).
/// En mode Réaliste, si le matériau n'a pas de couleur d'apparence (pas
/// d'asset, ou asset sans couleur exploitable), le fallback vers la couleur
/// graphique est expliqué — jamais muet dans le visualisateur (2.6 du
/// DESIGN_PLAN). Sinon null : rien à dire. Alimente le tooltip du carré
/// d'aperçu ET le petit texte secondaire visible sous le carré (DR1-3 : le
/// fallback ne doit pas exiger un hover pour être compris).
/// </summary>
public class AppearanceFallbackIndicatorConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var mode = values.Length > 0 && values[0] is PreviewMode m ? m : PreviewMode.UniformColor;
        bool hasAppearanceColor = values.Length > 1 && values[1] is int;

        if (mode == PreviewMode.Realistic && !hasAppearanceColor)
            return "Pas d'apparence — couleur graphique";

        return null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
