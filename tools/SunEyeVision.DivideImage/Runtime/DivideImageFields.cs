using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.DivideImage.Runtime;

/// <summary>
/// 划片拆分的稳定设置身份（原件 <c>IMVSDivideImageModu</c> 的四个算法参数）。
/// 身份只在声明与绑定中使用，不依赖 CLR 反射。
/// </summary>
public static class DivideImageFields
{
    /// <summary>原厂参数 <c>NumX</c>：X 方向划片数量，DefaultValue=1。</summary>
    public const string Columns = "proc.num-x";
    /// <summary>原厂参数 <c>NumY</c>：Y 方向划片数量，DefaultValue=1。</summary>
    public const string Rows = "proc.num-y";
    /// <summary>原厂参数 <c>OverlaprateX</c>：X 方向重叠率（百分比），DefaultValue=0。</summary>
    public const string OverlapX = "proc.overlap-rate-x";
    /// <summary>原厂参数 <c>OverlaprateY</c>：Y 方向重叠率（百分比），DefaultValue=0。</summary>
    public const string OverlapY = "proc.overlap-rate-y";

    /// <summary>原件 AlgorithmTab 只取证到 <c>Tab_Basic Params</c> 承载这四个数字控件。</summary>
    public const string Page = "基本参数";
    public const string Section = "划片参数";

    /// <summary>重叠率下界（无重叠）。</summary>
    public const int MinimumOverlapPercent = 0;
    /// <summary>
    /// 重叠率上界取 99：100% 时所有分片完全重合、失去划片意义。
    /// 原厂是否允许 100 未取证（见交接单未确认项），因此按显式上限拒绝而不是静默截断。
    /// </summary>
    public const int MaximumOverlapPercent = 99;

    /// <summary>
    /// 分片总数保护上限。原厂是否有分片数量上限未取证；此处只是防止把笔误（例如把 40 输成 4000）
    /// 变成上千万个结果行的产品侧保护，命中即报错，不做静默截断。
    /// </summary>
    public const int MaximumTileCount = 65536;

    private static readonly TypeRef Integer = DataType.Integer;

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(Columns, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(Rows, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(OverlapX, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(OverlapY, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault)
    }, Array.Empty<Port>(), new ToolPresentation(new[]
    {
        new FieldPresentation(Columns, "X方向划片数量", Section, 0, FieldEditorKind.Scalar)
        {
            Layout = new(Page, Section, Minimum: 1, DecimalPlaces: 0, AllowReference: false)
        },
        new FieldPresentation(Rows, "Y方向划片数量", Section, 1, FieldEditorKind.Scalar)
        {
            Layout = new(Page, Section, Minimum: 1, DecimalPlaces: 0, AllowReference: false)
        },
        new FieldPresentation(OverlapX, "X方向重叠率 (%)", Section, 2, FieldEditorKind.Scalar)
        {
            Layout = new(Page, Section, Minimum: MinimumOverlapPercent, Maximum: MaximumOverlapPercent,
                DecimalPlaces: 0, AllowReference: false)
        },
        new FieldPresentation(OverlapY, "Y方向重叠率 (%)", Section, 3, FieldEditorKind.Scalar)
        {
            Layout = new(Page, Section, Minimum: MinimumOverlapPercent, Maximum: MaximumOverlapPercent,
                DecimalPlaces: 0, AllowReference: false)
        }
    }));

    public static InputValues Read(DivideImageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(Schema.Inputs, new Dictionary<string, object?>
        {
            [Columns] = settings.Columns,
            [Rows] = settings.Rows,
            [OverlapX] = settings.OverlapX,
            [OverlapY] = settings.OverlapY
        });
    }

    public static DivideImageSettings Apply(DivideImageSettings original, IReadOnlyDictionary<string, object?> changes)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(changes);
        var values = Read(original).CopyValues().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var pair in changes)
        {
            if (!Schema.Inputs.Any(port => port.Id == pair.Key)) throw new ArgumentException("未声明的划片拆分字段：" + pair.Key);
            values[pair.Key] = pair.Value;
        }
        return Materialize(new InputValues(Schema.Inputs, values));
    }

    public static DivideImageSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var candidate = new InputValues(Schema.Inputs, values.CopyValues());
        var result = new DivideImageSettings
        {
            Columns = candidate.Get<long>(Columns),
            Rows = candidate.Get<long>(Rows),
            OverlapX = candidate.Get<long>(OverlapX),
            OverlapY = candidate.Get<long>(OverlapY)
        };
        var errors = result.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return result.Freeze();
    }
}
