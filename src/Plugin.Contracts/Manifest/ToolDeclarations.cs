namespace SunEyeVision.Plugin.Contracts.Manifest;

/// <summary>
/// 枚举参数的取值与显示名（原厂 <c>EnumEntrys</c>）。
/// 取值是<b>存储值</b>，不是序号；显示名先当本地化 key 查表，查不到按原文显示。
/// </summary>
public sealed record ToolChoice
{
    public ToolChoice(string value, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Value = value;
        Label = label;
    }

    public string Value { get; }
    public string Label { get; }
}

/// <summary>
/// 输入行呈现声明。输入端口本身由 <see cref="ToolInputTemplate"/> 展开，
/// 但参数页需要一行标签与归属，否则输入区组装成空页（原厂由 AlgorithmTab 的输入行提供）。
/// </summary>
public sealed record ToolInputPresentation
{
    public ToolInputPresentation(string portId, string label, string page, string section,
        FieldEditorKind editor = FieldEditorKind.Image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portId);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        if (!Enum.IsDefined(editor)) throw new ArgumentOutOfRangeException(nameof(editor));
        PortId = portId;
        Label = label;
        Page = page;
        Section = section;
        Editor = editor;
    }

    /// <summary>被呈现的输入端口身份（原厂 AlgorithmTab 的输入行绑定）。</summary>
    public string PortId { get; }
    public string Label { get; }
    public string Page { get; }
    public string Section { get; }
    public FieldEditorKind Editor { get; }
}

/// <summary>
/// 参数分组声明（原厂 <c>Category</c>）。渲染只认 <see cref="Page"/> + <see cref="Section"/>；
/// <see cref="VmCategory"/> 保留原厂 <c>Category/@Name</c> 用于逐字对齐校验（UI 不读）。
/// </summary>
public sealed record ToolSectionDeclaration
{
    public ToolSectionDeclaration(string page, string section, bool noHeader = false,
        bool canTriggerRun = false, string? vmCategory = null,
        // 原厂 SubscribeVisible：本分组内参数是否允许订阅（变量/引用）来源。
        bool allowReference = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        Page = page;
        Section = section;
        NoHeader = noHeader;
        CanTriggerRun = canTriggerRun;
        AllowReference = allowReference;
        VmCategory = string.IsNullOrWhiteSpace(vmCategory) ? null : vmCategory;
    }

    public string Page { get; }
    public string Section { get; }
    /// <summary>无标题分组（原厂 <c>CategoryNoHead</c>）：仍分组但不显示标题行。</summary>
    public bool NoHeader { get; }
    /// <summary>改动本组参数可触发运行（原厂 <c>CanTriggerRun</c>）。</summary>
    public bool CanTriggerRun { get; }
    /// <summary>原厂 SubscribeVisible：本分组参数是否允许订阅来源。</summary>
    public bool AllowReference { get; }
    /// <summary>原厂 <c>Category/@Name</c>，仅用于对齐校验。</summary>
    public string? VmCategory { get; }
}

/// <summary>
/// 条件显隐规则（原厂 XML 的 <c>Triggers/Setters/VisibleOperation</c>）。
/// 源参数取到指定值时，显示目标参数；目标默认隐藏。
/// </summary>
/// <remarks>
/// 语义边界（取证结论，不能丢）：
/// ① 只作用于<b>可见性</b>，参数仍参与运行；
/// ② 目标被隐藏时平台会清掉它的变量订阅（<see cref="ClearsSubscription"/>）；
/// ③ 初始显隐由"默认值 + 首轮触发"计算，不只是目标自身的初始可见位。
/// </remarks>
public sealed record FieldVisibilityRule
{
    public FieldVisibilityRule(string sourceFieldId, IReadOnlyList<string> values,
        IReadOnlyList<string> targetFieldIds, bool clearsSubscription = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldId);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(targetFieldIds);
        if (values.Count == 0) throw new ArgumentException("显隐规则至少需要一个触发值。", nameof(values));
        if (targetFieldIds.Count == 0) throw new ArgumentException("显隐规则至少需要一个目标字段。", nameof(targetFieldIds));
        if (values.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("显隐规则的触发值不能为空。", nameof(values));
        if (targetFieldIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("显隐规则的目标字段不能为空。", nameof(targetFieldIds));
        if (targetFieldIds.Distinct(StringComparer.Ordinal).Count() != targetFieldIds.Count)
            throw new ArgumentException("显隐规则的目标字段重复。", nameof(targetFieldIds));
        SourceFieldId = sourceFieldId;
        Values = Array.AsReadOnly(values.ToArray());
        TargetFieldIds = Array.AsReadOnly(targetFieldIds.ToArray());
        ClearsSubscription = clearsSubscription;
    }

    public string SourceFieldId { get; }
    public IReadOnlyList<string> Values { get; }
    public IReadOnlyList<string> TargetFieldIds { get; }
    /// <summary>目标被隐藏时是否清掉它的变量订阅（原厂行为为 true）。</summary>
    public bool ClearsSubscription { get; }
}

/// <summary>
/// 原厂 <c>DisEnableOperation</c>：控制器取指定值时**禁用**目标字段（与显隐规则对称）。
/// 典型用法是两个互斥开关——打开一个就禁用另一个，但两者都不隐藏。
/// </summary>
public sealed record FieldEnableRule
{
    public FieldEnableRule(string sourceFieldId, IReadOnlyList<string> values,
        IReadOnlyList<string> targetFieldIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldId);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(targetFieldIds);
        if (values.Count == 0) throw new ArgumentException("禁用规则至少需要一个触发值。", nameof(values));
        if (targetFieldIds.Count == 0) throw new ArgumentException("禁用规则至少需要一个目标字段。", nameof(targetFieldIds));
        if (values.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("禁用规则的触发值不能为空。", nameof(values));
        if (targetFieldIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("禁用规则的目标字段不能为空。", nameof(targetFieldIds));
        if (targetFieldIds.Distinct(StringComparer.Ordinal).Count() != targetFieldIds.Count)
            throw new ArgumentException("禁用规则的目标字段重复。", nameof(targetFieldIds));
        SourceFieldId = sourceFieldId;
        Values = Array.AsReadOnly(values.ToArray());
        TargetFieldIds = Array.AsReadOnly(targetFieldIds.ToArray());
    }

    public string SourceFieldId { get; }
    public IReadOnlyList<string> Values { get; }
    public IReadOnlyList<string> TargetFieldIds { get; }
}

/// <summary>
/// 成对编辑声明（原厂 <c>IntegerBettween</c>）：一个区间控件承载两个参数，
/// 但存储与下发仍是两个独立键；容器本身不参与运行。
/// </summary>
public sealed record ToolPairGroupDeclaration
{
    public ToolPairGroupDeclaration(string containerId, string minFieldId, string maxFieldId,
        string constraint = "min<=max", string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(minFieldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(maxFieldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(constraint);
        if (string.Equals(minFieldId, maxFieldId, StringComparison.Ordinal))
            throw new ArgumentException("成对参数的下限与上限不能是同一个字段。", nameof(maxFieldId));
        if (string.Equals(containerId, minFieldId, StringComparison.Ordinal)
            || string.Equals(containerId, maxFieldId, StringComparison.Ordinal))
            throw new ArgumentException("成对容器不能与上下限字段同名。", nameof(containerId));
        ContainerId = containerId;
        MinFieldId = minFieldId;
        MaxFieldId = maxFieldId;
        Constraint = constraint;
        Label = string.IsNullOrWhiteSpace(label) ? null : label;
    }

    /// <summary>容器身份；不参与算法下发（原厂 AlgorithmIndex = 0）。</summary>
    public string ContainerId { get; }
    public string MinFieldId { get; }
    public string MaxFieldId { get; }
    /// <summary>交叉约束表达式，默认 <c>min&lt;=max</c>。</summary>
    public string Constraint { get; }
    /// <summary>区间控件显示名（原厂 pairLabel）；为空用下限字段标签。</summary>
    public string? Label { get; }
}
    