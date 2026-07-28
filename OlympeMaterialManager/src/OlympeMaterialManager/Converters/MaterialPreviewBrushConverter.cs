using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Converters;

/// <summary>
/// Convertisseur multi-valeurs [PreviewMode, ColorArgb (int ou int?),
/// AppearanceColorArgb (int?), TexturePath (string?, optionnel)] → Brush, pour
/// l'aperçu du visualisateur, le liseré des cartes (B8) et les pastilles des
/// presets. Le changement de mode se propage par le binding sur
/// PreviewModeStore.CurrentMode (INPC) — pas d'abonnement message nécessaire.
/// - Mode Couleur : SolidColorBrush de ColorArgb (inchangé, DR4-2).
/// - Mode Réaliste (DR2-2/DR4-2, opportuniste — décision utilisateur : l'image
///   quand elle existe, sinon le comportement actuel) :
///   - ConverterParameter="Image" (carré d'aperçu du visualisateur) : texture
///     résolue → ImageBrush de la miniature (TextureBrushCache) ; sinon couleur
///     d'apparence → couleur graphique.
///   - défaut (liseré, pastilles) : couleur MOYENNE de l'image
///     (TextureAverageColor — décision utilisateur explicite, l'objection
///     « brun » du council est outrepassée par ce choix) ; pas encore calculée
///     ou absente → couleur d'apparence → couleur graphique.
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
        var texturePath = values.Length > 3 ? values[3] as string : null;

        int? effectiveArgb = colorArgb;

        if (mode == PreviewMode.Realistic)
        {
            bool wantsImage = parameter is string s &&
                              string.Equals(s, "Image", StringComparison.OrdinalIgnoreCase);
            if (wantsImage)
            {
                var thumbnail = TextureBrushCache.GetThumbnail(texturePath);
                if (thumbnail != null)
                {
                    var imageBrush = new ImageBrush(thumbnail) { Stretch = Stretch.UniformToFill };
                    imageBrush.Freeze();
                    return imageBrush;
                }
                // Texture introuvable ou illisible : chaîne couleur ci-dessous
                // (le visualisateur porte l'indicateur explicatif).
                effectiveArgb = appearanceArgb ?? colorArgb;
            }
            else
            {
                // Moyenne pas encore calculée (tâche de fond) → couleur
                // d'apparence, mise à jour au prochain rafraîchissement.
                effectiveArgb = TextureAverageColor.TryGetAverageArgb(texturePath)
                                ?? appearanceArgb
                                ?? colorArgb;
            }
        }

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
