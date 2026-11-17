using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.UI.Converters.Node;

/// <summary>
/// 节点外框几何：按节点显示类型返回原厂对应的 PathGeometry。
/// <para>对应原件 diagramitemtemplate.xaml 中 `TargetDisplayType → Path.Data` 的映射。</para>
/// <para>几何直接内联解析，不从资源字典查找：节点模板位于 UserControl 资源作用域，
/// 用 Application.TryFindResource 取不到，会导致永远回退成矩形。</para>
/// </summary>
public sealed class NodeShapeConverter : IValueConverter
{
    /// <summary>标准矩形：110×30，圆角 r=4（原件 NormalModulePathStyle）。</summary>
    private const string Standard =
        "M106,0 A4,4 0 0 1 110,4 L110,26 A4,4 0 0 1 106,30 L4,30 A4,4 0 0 1 0,26 L0,4 A4,4 0 0 1 4,0 Z";

    /// <summary>图像类胶囊：110×30，圆角 r=15（原件 ImageModulePath）。</summary>
    private const string Image =
        "M15,0 L95,0 A15,15 0 0 1 110,15 A15,15 0 0 1 95,30 L15,30 A15,15 0 0 1 0,15 A15,15 0 0 1 15,0 Z";

    /// <summary>条件分支六边形：110×40（原件 IfBranchPath）。</summary>
    private const string IfBranch =
        "M0,12 A2,2 0 0 1 2,10 L54,1 A2,2 0 0 1 56,1 L108,10 A2,2 0 0 1 110,12 L110,28 A2,2 0 0 1 108,30 L56,39 A2,2 0 0 1 54,39 L2,30 A2,2 0 0 1 0,28 Z";

    /// <summary>功能方块：40×40，圆角 r=2（原件 FunctionModulePath）。</summary>
    private const string Function =
        "M0,2 A2,2 0 0 1 2,0 L38,0 A2,2 0 0 1 40,2 L40,38 A2,2 0 0 1 38,40 L2,40 A2,2 0 0 1 0,38 Z";

    /// <summary>定时圆形：40×40（原件 TimerModulePath）。</summary>
    private const string Timer =
        "M20,0 A20,20 0 0 1 40,20 A20,20 0 0 1 20,40 A20,20 0 0 1 0,20 A20,20 0 0 1 20,0 Z";

    /// <summary>壳形：110×30（原件 ShellModulePath）。</summary>
    private const string Shell =
        "M9.5,0 L108,0 A2,2 0 0 1 110,2 L110,20.5 A1,3 0 0 1 109,23.5 L103.5,29 A3,1 0 0 1 100.5,30 L2,30 A2,2 0 0 1 0,28 L0,9.5 A1,3 0 0 1 1,6.5 L6.5,1 A3,1 0 0 1 9.5,0 Z";

    /// <summary>通讯数据：110×30 左右尖角（原件 CommuncationDataPath）。</summary>
    private const string Communication =
        "M2,0 L100.5,0 A3,1.5 0 0 1 103.5,1.5 L109,13.5 A2,3 0 0 1 109,16.5 L103.5,28.5 A3,1.5 0 0 1 100.5,30 L2,30 A3,1.5 0 0 1 1,28.5 L6.5,16.5 A2,3 0 0 1 6.5,13.5 L1,1.5 A3,1.5 0 0 1 2,0 Z";

    private static readonly Dictionary<string, Geometry> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Resolve(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Geometry Resolve(string? displayType)
    {
        var key = (displayType ?? string.Empty).ToUpperInvariant() switch
        {
            "IMAGE" => nameof(Image),
            "IFBRANCH" => nameof(IfBranch),
            "FUNCTION" => nameof(Function),
            "TIMER" => nameof(Timer),
            "SHELL" => nameof(Shell),
            "COMMUNCATION" => nameof(Communication),
            _ => nameof(Standard)
        };

        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
        }

        var data = key switch
        {
            nameof(Image) => Image,
            nameof(IfBranch) => IfBranch,
            nameof(Function) => Function,
            nameof(Timer) => Timer,
            nameof(Shell) => Shell,
            nameof(Communication) => Communication,
            _ => Standard
        };

        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        lock (Cache) { Cache[key] = geometry; }
        return geometry;
    }
}
