using System.Globalization;
using System.Reflection;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Plugin.Contracts.Manifest;

/// <summary>
/// 工具基类：把“声明 → 契约”的样板集中到一处。子类只需给出
/// <see cref="Manifest"/> 与 <see cref="CreateSession"/>，不再手写 Schema、
/// Descriptor、Appearance、Catalog 与设置读写。
/// </summary>
/// <remarks>
/// 对应原件平台替工具做的十项（参数页渲染、值读写、校验、订阅、显隐、
/// 动态 IO、显示项、结果树、ROI 全链、持久化）；工具侧只剩声明与算法。
/// 读数按 manifest 的 <c>PropertyName</c> 反射，缺失属性直接报错，
/// 不做默认值兜底（原件缺失字段由方案迁移补，不在这里静默补）。
/// </remarks>
public abstract class ToolDefinition<TSettings, TOutput> :
    IToolDefinition<TSettings, TOutput>, IToolSettingsFields, IToolCatalogProvider,
    IToolResultPresentationProvider, IToolResultDisplayProvider, IToolManualReferenceSources
    where TSettings : IToolSettings<TSettings>, new()
    where TOutput : class, IToolOutput
{
    private readonly Lazy<ToolSchema> _settings;
    private readonly Lazy<ToolSchema> _execution;
    private readonly Lazy<ToolSchema> _result;
    private readonly Lazy<ToolSchema> _display;
    private readonly Lazy<ResultDisplayDescriptor> _descriptor;
    private readonly Lazy<ResultProjectionDescriptor> _projection;
    private readonly Lazy<ToolCatalogDescriptor> _catalog;
    private readonly Lazy<IReadOnlyList<ManualReferenceSource>> _references;
    // 惰性求值：Manifest 由子类属性初始化，基类构造期尚不可用（虚调用陷阱）。
    private readonly Lazy<Dictionary<string, PropertyInfo>> _properties;

    protected ToolDefinition()
    {
        // 扩展点：槽位型参数（如 32 组区间端点）不是 C# 属性，由工具追加端口与呈现，
        // 声明仍集中在一处，不走“工具自建 Schema”的老路。
        _settings = new(() => ExtendSettings(ManifestCompiler.SettingsSchema(Manifest)));
        _execution = new(() => ExtendExecution(ManifestCompiler.ExecutionSchema(Manifest)));
        _result = new(() => ManifestCompiler.ResultSchema(Manifest));
        _display = new(() => ManifestCompiler.DisplaySchema(Manifest));
        _descriptor = new(() => ManifestCompiler.Descriptor(Manifest));
        _projection = new(BuildProjection);
        _catalog = new(() => new ToolCatalogDescriptor(Manifest.Name, Manifest.Description,
            Manifest.IconKey, Manifest.Category, Manifest.Order, Manifest.NodeDisplayType));
        // 手工引用来源只暴露 manifest 里声明过的输出端口，不做隐式推断。
        _references = new(() => Array.AsReadOnly(
            Manifest.ManualReferenceSources.Select(id => new ManualReferenceSource(id)).ToArray()));
        _properties = new(BuildProperties);
    }

    /// <summary>工具声明唯一真源。</summary>
    protected abstract ToolManifest Manifest { get; }

    /// <summary>
    /// 追加非属性型设置端口（槽位、动态子项）。默认不追加；追加的端口必须是可缺省的，
    /// 否则基类反射往返会因缺值失败。
    /// </summary>
    protected virtual ToolSchema ExtendSettings(ToolSchema schema) => schema;

    /// <summary>执行 Schema 的同一扩展点（保持与设置 Schema 的端口一致）。</summary>
    protected virtual ToolSchema ExtendExecution(ToolSchema schema) => schema;

    public abstract IToolSession<TSettings, TOutput> CreateSession();

    public string ToolId => Manifest.Id;
    public ToolSchema SettingsSchema => _settings.Value;
    public ToolSchema ResultSchema => _result.Value;
    public ToolSchema DisplaySettingsSchema => _display.Value;
    public ResultDisplayDescriptor ResultDisplay => _descriptor.Value;
    public ResultProjectionDescriptor ResultProjection => _projection.Value;
    public ToolCatalogDescriptor Catalog => _catalog.Value;
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources => _references.Value;
    public RoiEditDeclaration? RoiEdit => ManifestCompiler.RoiEdit(Manifest);
    public ToolSchema Describe() => _execution.Value;
    public InputValues CreateDisplaySettings() => ManifestCompiler.DisplayDefaults(Manifest);
    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields)
        => ManifestCompiler.Appearance(Manifest, displayFields);

    /// <summary>默认无额外准入；需要跨字段校验的工具覆盖本方法。</summary>
    public virtual IReadOnlyList<Diagnostic> Validate(TSettings settings) => Array.Empty<Diagnostic>();

    /// <summary>默认无运行期资源键；需要原生资源的工具覆盖本方法。</summary>
    public virtual ToolCapabilities Capabilities() => ToolCapabilities.None;

    public virtual InputValues ReadSettings(object settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings is not TSettings typed)
            throw new ArgumentException($"设置类型不是 {typeof(TSettings).FullName}。", nameof(settings));
        var properties = _properties.Value;
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var parameter in Manifest.Parameters)
        {
            // 枚举按取值名落库（与声明里的 Choice 取值同一命名）；
            // C# int 属性按声明的 Int32 存储提升为 long（端口数值统一用 long 承载）。
            var raw = properties[parameter.Id].GetValue(typed);
            values[parameter.Id] = raw switch
            {
                Enum flag => flag.ToString(),
                int integer => (long)integer,
                _ => raw
            };
        }
        return new InputValues(SettingsSchema.Inputs, values);
    }

    public virtual object MaterializeSettings(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var settings = new TSettings();
        var properties = _properties.Value;
        foreach (var parameter in Manifest.Parameters)
        {
            if (!values.Contains(parameter.Id))
                throw new ArgumentException($"缺少设置字段：{parameter.Id}。");
            var property = properties[parameter.Id];
            var raw = values.Get<object?>(parameter.Id);
            property.SetValue(settings, Convert(raw, parameter, property.PropertyType));
        }
        return settings.Freeze();
    }

    private Dictionary<string, PropertyInfo> BuildProperties()
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
        foreach (var parameter in Manifest.Parameters)
        {
            var property = typeof(TSettings).GetProperty(parameter.PropertyName,
                BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"参数 {parameter.Id} 映射的属性 {parameter.PropertyName} 不存在于 {typeof(TSettings).Name}。");
            if (!property.CanRead || !property.CanWrite)
                throw new InvalidOperationException(
                    $"参数 {parameter.Id} 映射的属性 {parameter.PropertyName} 必须可读写。");
            map.Add(parameter.Id, property);
        }
        return map;
    }

    private static object? Convert(object? value, ToolParameterDeclaration parameter, Type target)
    {
        if (value is null)
            throw new ArgumentException($"设置字段 {parameter.Id} 不能为空。");
        if (target.IsInstanceOfType(value)) return value;
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        // 枚举型设置：端口按声明过的取值名（文本）保存，回填时按名字还原。
        if (underlying.IsEnum)
            return value is string name
                ? Enum.Parse(underlying, name, ignoreCase: false)
                : throw new ArgumentException($"设置字段 {parameter.Id} 必须是声明过的枚举取值。");
        if (underlying == typeof(long))
            return value switch
            {
                long l => l,
                int i => i,
                double d when d == Math.Truncate(d) => (long)d,
                _ => throw new ArgumentException($"设置字段 {parameter.Id} 必须是整数。")
            };
        if (underlying == typeof(double))
            return value switch
            {
                double d => d,
                float f => (double)f,
                long l => l,
                int i => i,
                _ => throw new ArgumentException($"设置字段 {parameter.Id} 必须是数值。")
            };
        if (underlying == typeof(bool))
            return value is bool flag
                ? flag
                : throw new ArgumentException($"设置字段 {parameter.Id} 必须是布尔值。");
        if (underlying == typeof(string))
            return value as string ?? throw new ArgumentException($"设置字段 {parameter.Id} 必须是文本。");
        if (underlying.IsEnum)
            return value is string text
                ? Enum.Parse(underlying, text, ignoreCase: false)
                : System.Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        return System.Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture)
               ?? throw new ArgumentException($"设置字段 {parameter.Id} 无法转换。");
    }

    private ResultProjectionDescriptor BuildProjection()
    {
        var declaration = Manifest.Result;
        // 属性列：每列直接引用一个输出端口，单行呈现（原厂 PropertyList）。
        if (declaration.Shape == ToolResultShape.PropertyList)
            return new ResultProjectionDescriptor(declaration.Kind, ResultCardinality.One,
                declaration.Columns.Select(column => new ResultColumnDescriptor(column.Key, column.Label,
                    new ResultFieldPath(column.FieldId), column.Unit, column.DecimalPlaces)).ToArray(),
                rendererKey: declaration.RendererKey);
        // 行端口按声明身份查找：结果 Schema 可能还有附加输出端口（原厂 Output 段）。
        var rowsPort = _result.Value.Outputs
            .FirstOrDefault(port => string.Equals(port.Id, declaration.RowsPortId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"结果 Schema 缺少行端口：{declaration.RowsPortId}。");
        var many = rowsPort.Type.Shape == ValueShape.Sequence;
        // 列可自带叠加几何与几何来源（原厂属性列表的 Geometry/GeometrySource）。
        ResultFieldPath? Member(string? member) => member is null
            ? null
            : new ResultFieldPath(declaration.RowsPortId!, new[] { member });
        var columns = declaration.Columns
            .Select(column => new ResultColumnDescriptor(column.Key, column.Label,
                // 嵌套列（如 oriented-bounds.angle-degrees）按声明路径取值，否则单段。
                new ResultFieldPath(declaration.RowsPortId!, column.MemberPath ?? new[] { column.FieldId }),
                column.Unit, column.DecimalPlaces,
                Geometry: Member(column.GeometryMember),
                GeometrySource: Member(column.GeometrySourceMember)))
            .ToArray();
        return new ResultProjectionDescriptor(declaration.Kind,
            many ? ResultCardinality.Many : ResultCardinality.One,
            columns,
            rows: many ? new ResultFieldPath(declaration.RowsPortId!) : null,
            elementIdentityFieldId: many ? declaration.Slots!.Identity : null,
            rendererKey: declaration.RendererKey);
    }
}
