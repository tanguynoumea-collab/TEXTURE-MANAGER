using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Olympe.MaterialManager.Converters;

/// <summary>
/// Convertisseur int ARGB -> System.Windows.Media.Color.
/// Utilise pour la pastille couleur des materiaux preset dans le panneau droit.
/// </summary>
public class ArgbToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int argb)
        {
            var color = System.Drawing.Color.FromArgb(argb);
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
