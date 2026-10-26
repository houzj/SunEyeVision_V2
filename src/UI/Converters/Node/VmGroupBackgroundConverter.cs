using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SunEyeVision.UI.Converters.Node;
/// <summary>GROUP原件背景图的选择、执行和禁用优先级；不重绘原始像素。</summary>
public sealed class VmGroupBackgroundConverter : IMultiValueConverter
{
    private static readonly ConcurrentDictionary<string, ImageBrush> Brushes = new();
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var enabled = values.ElementAtOrDefault(0) is true; var selected = values.ElementAtOrDefault(1) is true;
        var status = values.ElementAtOrDefault(2) as string;
        var file = !enabled ? "group_bg_forbidden.png" : status is "成功" or "OK" ? selected ? "group_ok_selected.png" : "group_ok.png"
            : status is "失败" or "NG" ? selected ? "group_ng_selected.png" : "group_ng.png" : selected ? "group_bg_focus.png" : "group_bg.png";
        return Brushes.GetOrAdd(file, key =>
        {
            var image = new BitmapImage(); image.BeginInit(); image.UriSource = new Uri("pack://application:,,,/SunEyeVision.UI;component/Resources/Workflow/"+key, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad; image.EndInit(); image.Freeze();
            var brush = new ImageBrush(image) { Stretch = Stretch.None }; brush.Freeze(); return brush;
        });
    }
    public object[] ConvertBack(object value, Type[] types, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
