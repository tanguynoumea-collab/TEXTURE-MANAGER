using System.Globalization;
using System.Windows.Data;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Converters;

/// <summary>
/// Convertisseur multi-valeurs [PreviewMode, TexturePath] → texte d'explication
/// du fallback de l'aperçu du visualisateur (B10-UI). En mode Texture, si la
/// texture est introuvable OU illisible (le cache retourne null), l'échec est
/// expliqué — jamais muet dans le visualisateur (2.6 du DESIGN_PLAN). Sinon
/// null : rien à dire. Alimente le tooltip du carré d'aperçu ET, depuis DR1-3,
/// le petit texte secondaire visible sous le carré (le fallback ne doit pas
/// exiger un hover pour être compris).
/// </summary>
public class TextureFallbackTooltipConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var mode = values.Length > 0 && values[0] is PreviewMode m ? m : PreviewMode.UniformColor;
        var texturePath = values.Length > 1 ? values[1] as string : null;

        if (mode == PreviewMode.Texture && TextureBrushCache.GetThumbnail(texturePath) == null)
            return "Texture introuvable — aperçu couleur";

        return null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
