using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Plugin.Contracts.Manifest;

/// <summary>
/// 显示项角色，与 <see cref="ResultDisplayAppearance"/> 的槽位一一对应。
/// 对应原件 <c>&lt;Type&gt;Display.xml</c> 的对象类型（outlinepoint / rectangle / text …）。
/// </summary>
public enum ToolDisplayRole
{
    /// <summary>主图形（匹配框 / Blob 主体）。</summary>
    Graphic,
    /// <summary>检测区域。</summary>
    Inspection,
    /// <summary>屏蔽区。</summary>
    Mask,
    /// <summary>扇环检测区域。</summary>
    Annulus,
    /// <summary>轮廓点（系统类型 ResultOutlineSet）。</summary>
    Outline,
    /// <summary>最大内接矩形。</summary>
    InnerRectangle,
    /// <summary>匹配点 / 质心。</summary>
    Centroid,
    /// <summary>基准点。</summary>
    ReferencePoint,
    /// <summary>运行点。</summary>
    RunPoint,
    /// <summary>结果文本。</summary>
    Text
}

/// <summary>
/// 一个显示项声明。只给默认值；节点可在自己的 <c>display.*</c> 字段上覆盖
/// （原件：Display.xml 给初始对象，节点参数持有可改副本）。
/// </summary>
public sealed record ToolDisplayItem
{
    public ToolDisplayItem(ToolDisplayRole role, string label, bool visible = true,
        ResultDisplayColor? ok = null, ResultDisplayColor? ng = null,
        double width = 2, double opacity = 1, bool fill = false,
        bool showMeasurements = false, string? pointLabel = null, bool? fixedJudgment = null,
        string? groupKey = null, string? textContent = null, bool styleDialog = true,
        // 纯可见性项（原厂只有 Show 的 ImageViewConfig）：只产出一行眼睛开关，没有颜色/线宽。
        bool visibleOnly = false,
        // 文本组是否带总开关行：原厂各工具不同（PixelCount 有、IntensityMeasure 走独立项）。
        bool showVisible = true)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (width is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(width));
        if (opacity is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(opacity));
        Role = role;
        Label = label;
        Visible = visible;
        Ok = ok;
        Ng = ng;
        Width = width;
        Opacity = opacity;
        Fill = fill;
        ShowMeasurements = showMeasurements;
        PointLabel = pointLabel;
        FixedJudgment = fixedJudgment;
        // 字段组键：默认由角色派生；工具可覆盖以沿用既有字段身份（如 PixelCount 的 display.roi.*）。
        GroupKey = string.IsNullOrWhiteSpace(groupKey) ? DefaultGroupKey(role) : groupKey;
        TextContent = textContent;
        // 图形项默认带“样式”对话框（原厂 ImageViewConfig 的画笔）；文本项只有眼睛，无样式框。
        StyleDialog = styleDialog && role != ToolDisplayRole.Text && !visibleOnly;
        VisibleOnly = visibleOnly;
        ShowVisible = showVisible;
    }

    /// <summary>角色 → 默认字段组键（kebab-case）。</summary>
    public static string DefaultGroupKey(ToolDisplayRole role) => role switch
    {
        ToolDisplayRole.Graphic => "graphic",
        ToolDisplayRole.Inspection => "inspection",
        ToolDisplayRole.Mask => "mask",
        ToolDisplayRole.Annulus => "annulus",
        ToolDisplayRole.Outline => "outline",
        ToolDisplayRole.InnerRectangle => "inner-rectangle",
        ToolDisplayRole.Centroid => "centroid",
        ToolDisplayRole.ReferencePoint => "reference-point",
        ToolDisplayRole.RunPoint => "run-point",
        ToolDisplayRole.Text => "text",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    /// <summary>显示字段组键（`display.&lt;group&gt;.*` 的 group 段）。</summary>
    public string GroupKey { get; }
    /// <summary>文本默认内容（仅 <see cref="ToolDisplayRole.Text"/> 使用）。</summary>
    public string? TextContent { get; }
    /// <summary>是否生成“样式”对话框（原厂 ImageViewConfig 的画笔入口）。</summary>
    public bool StyleDialog { get; }
    /// <summary>纯可见性项：只产出眼睛开关，不产出颜色/透明度/线宽。</summary>
    public bool VisibleOnly { get; }
    /// <summary>本组是否产出 visible（眼睛）行；文本组可声明为无总开关。</summary>
    public bool ShowVisible { get; }

    public ToolDisplayRole Role { get; }
    public string Label { get; }
    public bool Visible { get; }
    /// <summary>为空表示使用系统默认 OK 色（原件 OkColor 默认值）。</summary>
    public ResultDisplayColor? Ok { get; }
    /// <summary>为空表示使用系统默认 NG 色。</summary>
    public ResultDisplayColor? Ng { get; }
    public double Width { get; }
    public double Opacity { get; }
    public bool Fill { get; }
    public bool ShowMeasurements { get; }
    public string? PointLabel { get; }
    public bool? FixedJudgment { get; }
}

/// <summary>
/// 显示声明。结果树“显示哪些项/顺序/分组”属工具声明；
/// “可见/颜色/线宽”的实际值属节点 <c>display.*</c> 字段，两者不得混用。
/// </summary>
public sealed record ToolDisplayDeclaration
{
    public ToolDisplayDeclaration(IReadOnlyList<ToolDisplayItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        // 原厂没有 Display.xml 的工具就没有显示项：空声明合法，显示配置页随之为空。
        if (items.Any(item => item is null)) throw new ArgumentException("显示项不能包含空项。", nameof(items));
        // 原厂 ResultShow 允许多个同角色 ImageViewConfig（如两个检测区），
        // 真正要求唯一的是显示分组身份。
        if (items.Select(item => item.GroupKey).Distinct(StringComparer.Ordinal).Count() != items.Count)
            throw new ArgumentException("显示项分组身份重复。", nameof(items));
        Items = Array.AsReadOnly(items.ToArray());
    }

    public IReadOnlyList<ToolDisplayItem> Items { get; }
}

/// <summary>
/// 行级面板替换声明。对应原件唯一正式开放的扩展粒度（一个 DisplayType key → 一个模板）：
/// 行外壳（左标签、启用、显隐分组）与值读写通道仍归平台。
/// 页级只能等价 <c>TabType=Panal</c> 页签或独立窗口，须单独登记，不得走本声明。
/// </summary>
public sealed record ToolPanelOverride
{
    public ToolPanelOverride(string displayTypeKey, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayTypeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        DisplayTypeKey = displayTypeKey;
        Reason = reason;
    }

    /// <summary>被替换的控件类型键（原件 DisplayType）</summary>
    public string DisplayTypeKey { get; }
    /// <summary>替换原因，必须登记，供架构测试审查。</summary>
    public string Reason { get; }
}
