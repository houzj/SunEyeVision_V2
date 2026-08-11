using System;
using System.Globalization;
using System.Windows;
using SunEyeVision.UI.Shell.Controls.Canvas;

namespace SunEyeVision.UI.Converters.Visibility
{
    /// <summary>
    /// CanvasType到WorkflowCanvas可见性的转换器
    /// </summary>
    public class CanvasTypeToWorkflowVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CanvasType canvasType)
            {
                System.Windows.Visibility visibility = canvasType == CanvasType.WorkflowCanvas ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                return visibility;
            }
            
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
