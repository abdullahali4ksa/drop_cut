using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DropCut.App.Shared.Converters;

/// <summary>
/// Converts boolean values to XAML Visibility (Visible / Collapsed).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolVal)
        {
            return boolVal ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }

        return false;
    }
}
