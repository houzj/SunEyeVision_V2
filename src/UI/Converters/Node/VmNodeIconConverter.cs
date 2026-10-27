using System.Globalization;
using System.Text.Json;
using System.Windows.Data;
namespace SunEyeVision.UI.Converters.Node;
/// <summary>节点与VM目录共享同一显式工具映射和原始图标。</summary>
public sealed class VmNodeIconConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<string,string> Icons=ReadIcons();
    private static IReadOnlyDictionary<string,string> ReadIcons()
    {
        using var stream=typeof(VmNodeIconConverter).Assembly.GetManifestResourceStream("SunEyeVision.UI.VmToolCatalog.json")!;
        using var json=JsonDocument.Parse(stream);
        return json.RootElement.EnumerateArray().SelectMany(g=>g.GetProperty("Items").EnumerateArray())
            .Where(t=>t.GetProperty("ToolId").ValueKind==JsonValueKind.String)
            .ToDictionary(t=>t.GetProperty("ToolId").GetString()!,t=>t.GetProperty("Icon").GetString()!,StringComparer.Ordinal);
    }
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>
        "pack://application:,,,/SunEyeVision.UI;component/Resources/Toolbox/"+
        (value is string id&&Icons.TryGetValue(id,out var icon)?icon:"tool-subroutine.png");
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}