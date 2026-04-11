using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Olympe.MaterialManager.Converters;

/// <summary>
/// Convertisseur bool -> Visibility standard.
/// true = Visible, false = Collapsed.
/// Utilise pour l'affichage conditionnel des panneaux couches/parametres.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}
