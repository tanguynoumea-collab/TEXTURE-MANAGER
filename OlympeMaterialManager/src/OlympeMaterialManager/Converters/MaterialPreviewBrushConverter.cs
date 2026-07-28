using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Converters;

/// <summary>
/// Convertisseur multi-valeurs [PreviewMode, ColorArgb (int ou int?), TexturePath]
/// → Brush, pour l'aperçu du visualisateur, le liseré des cartes (B8) et les
/// pastilles des presets. Le changement de mode se propage par le binding sur
/// PreviewModeStore.CurrentMode (INPC) — pas d'abonnement message nécessaire.
/// - Mode Couleur : SolidColorBrush de ColorArgb.
/// - Mode Texture : ImageBrush de la miniature (TextureBrushCache) ; tuilé si
///   ConverterParameter="Tile" (liseré), étiré sinon (aperçu, pastille) ;
///   texture introuvable ou illisible → fallback couleur silencieux.
/// - ColorArgb null (« Par catégorie ») : transparent, jamais un gris menteur.
/// Toutes les brushes retournées sont figées (Freeze).
/// </summary>
public class MaterialPreviewBrushConverter : IMultiValueConverter
{
    /// <summary>Côté du motif tuilé du liseré, en pixels (spec B8 : ~16-24 px).</summary>
    private const double TileSize = 16;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var mode = values.Length > 0 && values[0] is PreviewMode m ? m : PreviewMode.UniformColor;
        int? colorArgb = values.Length > 1 && values[1] is int argb ? argb : null;
        var texturePath = values.Length > 2 ? values[2] as string : null;

        if (mode == PreviewMode.Texture)
        {
            var thumbnail = TextureBrushCache.GetThumbnail(texturePath);
            if (thumbnail != null)
            {
                var imageBrush = new ImageBrush(thumbnail);
                if (parameter is string s && string.Equals(s, "Tile", StringComparison.OrdinalIgnoreCase))
                {
                    imageBrush.TileMode = TileMode.Tile;
                    imageBrush.ViewportUnits = BrushMappingMode.Absolute;
                    imageBrush.Viewport = new Rect(0, 0, TileSize, TileSize);
                    imageBrush.Stretch = Stretch.Fill;
                }
                else
                {
                    imageBrush.Stretch = Stretch.UniformToFill;
                }
                imageBrush.Freeze();
                return imageBrush;
            }
            // Texture introuvable : fallback couleur (chemin nominal, silencieux ici —
            // le visualisateur porte le tooltip explicatif).
        }

        if (colorArgb is null)
            return Brushes.Transparent;

        var (a, r, g, b) = ArgbUtils.UnpackArgb(colorArgb.Value);
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
