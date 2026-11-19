using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DropCut.App.Shared.Converters;

/// <summary>
/// Converts a boolean (e.g. IsRtl) to XAML FlowDirection (RightToLeft when true, LeftToRight when false).
/// </summary>
public class BoolToFlowDirectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isRtl && isRtl)
        {
            return FlowDirection.RightToLeft;
        }

        return FlowDirection.LeftToRight;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is FlowDirection direction)
        {
            return direction == FlowDirection.RightToLeft;
        }

        return false;
    }
}
