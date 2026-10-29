using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

namespace SunEyeVision.UI.Converters.Node;

/// <summary>Canvas and toolbox consume the same admitted tool declaration.
/// Resolving an icon must not load a plugin or revive an archived tool catalog.</summary>
public sealed class ToolNodeIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if(value is not string id||ToolRegistry.DirectoryCatalog?.Find(id) is not { } tool)
            return DependencyProperty.UnsetValue;
        return "pack://application:,,,/SunEyeVision.UI;component/Resources/Toolbox/tool-"+tool.Icon+".png";
    }
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)
        =>throw new NotSupportedException();
}
