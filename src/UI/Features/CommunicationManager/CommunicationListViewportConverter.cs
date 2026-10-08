using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.UI.Features.CommunicationManager;

/// <summary>Limit a content-sized event list to the remaining space above its add button.</summary>
public sealed class CommunicationListViewportConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double available || double.IsNaN(available))
            return 0d;

        // The add button is 28 high with a 4-pixel top gap.
        return Math.Max(0d, available - 32d);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}
