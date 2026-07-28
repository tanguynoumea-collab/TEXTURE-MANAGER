using System.Globalization;
using System.Windows.Data;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Converters;

/// <summary>
/// Convertisseur PreviewMode ↔ bool pour le sélecteur segmenté (B10-UI).
/// ConverterParameter = nom du mode du segment ("UniformColor" ou
/// "Realistic"). ConvertBack : true → le mode du segment ; false → DoNothing
/// (un RadioButton ne se décoche pas par clic, aucun état intermédiaire).
/// </summary>
public class PreviewModeToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is PreviewMode mode &&
               mode == PreviewModeStore.Parse(parameter as string);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true)
            return PreviewModeStore.Parse(parameter as string);
        return Binding.DoNothing;
    }
}
