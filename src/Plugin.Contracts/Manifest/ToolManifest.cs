namespace SunEyeVision.Plugin.Contracts.Manifest;

/// <summary>
/// 输入组合模板。对应原件 <c>Combination Style=IMAGE/ROIBOX/POINT</c> 的组合声明：
/// manifest 只选模板，端口集合由编译器按平台标准展开，工具不手写 Port。
/// 原件 IO 是运行时动态图（组合节点+叶节点），静态声明只承担“模板 + 默认标志”。
/// </summary>
public enum ToolInputTemplate
{
    None,
    Image,
    ImageRoi,
    ImageRoiFixture,
    ImageRoiMask
}

/// <summary>
/// 参数可绑定档位。对应原件 XML 的 <c>CanSubscribe/AccessMode</c> 与四类绑定
/// （常量 / 上游输出 / 变量 / 外部通讯全局量），按参数逐项声明，不全局放开。
/// </summary>
[Flags]
public enum ParameterBindingKinds
{
    None = 0,
    Constant = 1,
    /// <summary>上游节点输出（原件 VARIABLE + INDEX_CONSTANT/INDEX_VARIABLE/INDEX_ALL）。</summary>
    NodeOutput = 2,
    /// <summary>方案变量。</summary>
    Variable = 4,
    /// <summary>外部通讯全局量（原件 SUBSCRIBE_VAR_GLOBAL）。</summary>
    ExternalGlobal = 8,
    /// <summary>显示表达式（仅显示字段）。</summary>
    DisplayExpression = 16
}

/// <summary>
/// 一个参数声明。对应原件 <c>Algorithm.xml</c>（Name+DefaultValue）与
/// <c>AlgorithmTab.xml</c>（控件/联动）的合并视图；不含求值、校验、通知实现。
/// </summary>
public sealed record ToolParameterDeclaration
{
    public ToolParameterDeclaration(string id, string label, string section, TypeRef type, object? defaultValue,
        double? minimum = null, double? maximum = null, double? step = null, int decimals = 2,
        string? tipsOn = null, string? tipsOff = null,
        FieldEditModes modes = FieldEditModes.Constant,
        ParameterBindingKinds bindable = ParameterBindingKinds.Constant,
        FieldDefaultPresence defaultPresence = FieldDefaultPresence.UseDeclaredDefault,
        string? propertyName = null,
        IReadOnlyList<ToolChoice>? choices = null,
        NumericStorage? storage = null,
        string? displayType = null,
        // 原厂 IsAbandon：参数仍在配置文件里（算法与已保存方案兼容），但界面不渲染该行。
        bool abandon = false,
        // 原厂 Expand 折叠区成员：标 true 的字段按 (Section, IsAdvanced) 独立成折叠分区。
        bool advanced = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(defaultValue);
        if (minimum is { } low && maximum is { } high && low > high)
            throw new ArgumentException($"参数 {id} 的最小值大于最大值。", nameof(minimum));
        if (step is <= 0) throw new ArgumentException($"参数 {id} 的步长必须为正。", nameof(step));
        if (decimals is < 0 or > 15) throw new ArgumentException($"参数 {id} 的小数位超出范围。", nameof(decimals));
        var declaredChoices = (choices ?? Array.Empty<ToolChoice>()).ToArray();
        if (declaredChoices.Any(choice => choice is null))
            throw new ArgumentException($"参数 {id} 的枚举选项不能包含空项。", nameof(choices));
        if (declaredChoices.Select(choice => choice.Value).Distinct(StringComparer.Ordinal).Count() != declaredChoices.Length)
            throw new ArgumentException($"参数 {id} 的枚举取值重复。", nameof(choices));
        if (declaredChoices.Length != 0 && !declaredChoices.Any(choice => choice.Value == defaultValue?.ToString()))
            throw new ArgumentException($"参数 {id} 的默认值必须是声明过的枚举取值之一。", nameof(choices));
        Id = id;
        Label = label;
        Section = section;
        Type = type;
        DefaultValue = defaultValue;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
        Decimals = decimals;
        TipsOn = tipsOn;
        TipsOff = tipsOff;
        Storage = storage;
        // 参数行模板键（原厂 DisplayType）；为空表示走迁移期推导。
        DisplayType = string.IsNullOrWhiteSpace(displayType) ? null : displayType.Trim();
        Abandon = abandon;
        Advanced = advanced;
        Choices = Array.AsReadOnly(declaredChoices);
        Modes = modes;
        Bindable = bindable;
        DefaultPresence = defaultPresence;
        // 设置对象属性名默认等于字段 Id；显式给出时用于反射读写。
        PropertyName = string.IsNullOrWhiteSpace(propertyName) ? id : propertyName;
    }

    public string Id { get; }
    public string Label { get; }
    public string Section { get; }
    public TypeRef Type { get; }
    public object? DefaultValue { get; }
    public double? Minimum { get; }
    public double? Maximum { get; }
    public double? Step { get; }
    public int Decimals { get; }
    /// <summary>开关打开时的提示文案（原厂 SelecetedTips）。</summary>
    public string? TipsOn { get; }
    /// <summary>开关关闭时的提示文案（原厂 UnSelecetedTips）。</summary>
    public string? TipsOff { get; }
    // 原厂的 Name / DisplayName / AlgorithmIndex 在本方分别由 Port.Id / Label /
    // （无原生参数下发通道）承担，不再单独声明第三个名字，避免只声明不消费。
    /// <summary>枚举选项（原厂 EnumEntrys）；非空时编辑器为 Choice。</summary>
    public IReadOnlyList<ToolChoice> Choices { get; }
    /// <summary>数值存储精度（原厂 Int32/Float32）；为空用类型默认。</summary>
    public NumericStorage? Storage { get; }
    /// <summary>参数行模板键（原厂 DisplayType）；为空走迁移期推导。</summary>
    public string? DisplayType { get; }
    /// <summary>原厂 IsAbandon：参数保留在配置与端口里，参数页不渲染该行。</summary>
    public bool Abandon { get; }
    /// <summary>原厂 Expand 折叠区成员。</summary>
    public bool Advanced { get; }
    public FieldEditModes Modes { get; }
    public ParameterBindingKinds Bindable { get; }
    /// <summary>
    /// 旧方案缺该字段时是否可用声明默认值补齐（原件迁移语义）。
    /// 新增参数必须为 <see cref="FieldDefaultPresence.UseDeclaredDefault"/>，否则旧方案加载即缺字段。
    /// </summary>
    public FieldDefaultPresence DefaultPresence { get; }
    /// <summary>设置对象（TSettings）上的属性名，默认等于 <see cref="Id"/>。</summary>
    public string PropertyName { get; }
}

/// <summary>
/// 工具声明唯一真源。对应原件四份 XML（Algorithm.xml / AlgorithmTab.xml / Display.xml / ToolItemInfo.xml）
/// 的合并视图：编译器由它生成 SettingsSchema、ResultSchema、DisplaySchema、
/// ResultDisplayDescriptor、RoiEditDeclaration 与 RequiredOutputs。
/// </summary>
public sealed record ToolManifest
{
    public ToolManifest(string id, string name, string description, string iconKey, string category, int order,
        IReadOnlyList<ToolParameterDeclaration> parameters,
        ToolResultDeclaration result,
        ToolDisplayDeclaration display,
        ToolInputTemplate inputs = ToolInputTemplate.ImageRoiFixture,
        RoiEditDeclaration? roi = null,
        ToolPanelOverride? panelOverride = null,
        string nodeDisplayType = "DEFAULT",
        IReadOnlyList<ToolSectionDeclaration>? sections = null,
        IReadOnlyList<FieldVisibilityRule>? visibility = null,
        IReadOnlyList<ToolPairGroupDeclaration>? pairs = null,
        IReadOnlyList<ToolInputPresentation>? inputPresentations = null,
        IReadOnlyList<RoiGroupDeclaration>? roiGroups = null,
        IReadOnlyList<Port>? extraInputs = null,
        IReadOnlyList<Port>? extraOutputs = null,
        IReadOnlyList<string>? manualReferenceSources = null,
        // 原厂控制输出（条件分支）：声明顺序即 ToolOutcome.SelectedBranches 的序号。
        IReadOnlyList<ControlOutput>? controlOutputs = null,
        // 原厂 DisEnableOperation：条件成立时禁用目标行（仍可见）。
        IReadOnlyList<FieldEnableRule>? enableRules = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(iconKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(display);
        var duplicated = parameters.Where(item => item is null).ToArray();
        if (duplicated.Length != 0) throw new ArgumentException("参数声明不能包含空项。", nameof(parameters));
        if (parameters.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != parameters.Count)
            throw new ArgumentException("参数声明身份重复。", nameof(parameters));
        var declared = parameters.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var sectionList = (sections ?? Array.Empty<ToolSectionDeclaration>()).ToArray();
        if (sectionList.Any(item => item is null))
            throw new ArgumentException("分组声明不能包含空项。", nameof(sections));
        if (sectionList.Select(item => (item.Page, item.Section)).Distinct().Count() != sectionList.Length)
            throw new ArgumentException("分组声明重复。", nameof(sections));
        var rules = (visibility ?? Array.Empty<FieldVisibilityRule>()).ToArray();
        if (rules.Any(rule => rule is null)) throw new ArgumentException("显隐规则不能包含空项。", nameof(visibility));
        foreach (var rule in rules)
        {
            if (!declared.Contains(rule.SourceFieldId))
                throw new ArgumentException($"显隐规则的源字段未声明：{rule.SourceFieldId}。", nameof(visibility));
            foreach (var target in rule.TargetFieldIds)
            {
                if (!declared.Contains(target))
                    throw new ArgumentException($"显隐规则的目标字段未声明：{target}。", nameof(visibility));
                if (string.Equals(target, rule.SourceFieldId, StringComparison.Ordinal))
                    throw new ArgumentException("显隐规则的目标不能是源字段本身。", nameof(visibility));
            }
        }
        var pairList = (pairs ?? Array.Empty<ToolPairGroupDeclaration>()).ToArray();
        if (pairList.Any(pair => pair is null)) throw new ArgumentException("成对声明不能包含空项。", nameof(pairs));
        foreach (var pair in pairList)
            // 容器只是 UI 聚合身份（原厂 AlgorithmIndex=0，不参与下发），不要求是参数；
            // 上下限必须指向已声明字段。
            foreach (var field in new[] { pair.MinFieldId, pair.MaxFieldId })
                if (!declared.Contains(field))
                    throw new ArgumentException($"成对声明引用了未声明的字段：{field}。", nameof(pairs));
        if (pairList.Select(pair => pair.ContainerId).Distinct(StringComparer.Ordinal).Count() != pairList.Length)
            throw new ArgumentException("成对容器身份重复。", nameof(pairs));
        Id = id;
        Name = name;
        Description = description;
        IconKey = iconKey;
        Category = category;
        Order = order;
        Parameters = Array.AsReadOnly(parameters.ToArray());
        Result = result;
        Display = display;
        Inputs = inputs;
        Roi = roi;
        PanelOverride = panelOverride;
        NodeDisplayType = nodeDisplayType;
        Sections = Array.AsReadOnly(sectionList);
        Visibility = Array.AsReadOnly(rules);
        Pairs = Array.AsReadOnly(pairList);
        var inputRows = (inputPresentations ?? Array.Empty<ToolInputPresentation>()).ToArray();
        if (inputRows.Any(row => row is null))
            throw new ArgumentException("输入行声明不能包含空项。", nameof(inputPresentations));
        if (inputRows.Select(row => row.PortId).Distinct(StringComparer.Ordinal).Count() != inputRows.Length)
            throw new ArgumentException("输入行声明的端口重复。", nameof(inputPresentations));
        InputPresentations = Array.AsReadOnly(inputRows);
        RoiGroups = Array.AsReadOnly((roiGroups ?? Array.Empty<RoiGroupDeclaration>()).ToArray());

        var extras = (extraInputs ?? Array.Empty<Port>()).ToArray();
        if (extras.Any(port => port is null || string.IsNullOrWhiteSpace(port.Id)))
            throw new ArgumentException("组合体输入端口的身份不能为空。", nameof(extraInputs));
        if (extras.Select(port => port.Id).Distinct(StringComparer.Ordinal).Count() != extras.Length)
            throw new ArgumentException("组合体输入端口的身份重复。", nameof(extraInputs));
        ExtraInputs = Array.AsReadOnly(extras);

        var outputs = (extraOutputs ?? Array.Empty<Port>()).ToArray();
        if (outputs.Any(port => port is null || string.IsNullOrWhiteSpace(port.Id)))
            throw new ArgumentException("附加输出端口的身份不能为空。", nameof(extraOutputs));
        if (outputs.Select(port => port.Id).Distinct(StringComparer.Ordinal).Count() != outputs.Length)
            throw new ArgumentException("附加输出端口的身份重复。", nameof(extraOutputs));
        // 行表形态的行端口由结果声明生成，附加输出不得重名；
        // 属性列形态的字段本身就是输出端口，行端口必须由附加输出显式给出。
        if (result.Shape == ToolResultShape.Rows
            && outputs.Any(port => string.Equals(port.Id, result.RowsPortId, StringComparison.Ordinal)))
            throw new ArgumentException("附加输出端口不能与行端口同名。", nameof(extraOutputs));
        if (result.Shape == ToolResultShape.PropertyList && result.RowsPortId is { } rowPort
            && !outputs.Any(port => string.Equals(port.Id, rowPort, StringComparison.Ordinal)))
            throw new ArgumentException("属性列结果的行端口必须作为附加输出声明。", nameof(extraOutputs));
        ExtraOutputs = Array.AsReadOnly(outputs);

        // 手工引用来源只能指向本工具真实产出的端口：行端口或附加输出。
        // 可引用范围：附加输出 + 行端口 + 属性列形态下"字段即输出端口"的那些字段。
        var referenceable = outputs.Select(port => port.Id)
            .Concat(result.Fields.Select(field => field.Id))
            .Concat(result.RowsPortId is null ? Array.Empty<string>() : new[] { result.RowsPortId })
            .ToHashSet(StringComparer.Ordinal);
        var references = (manualReferenceSources ?? Array.Empty<string>()).ToArray();
        if (references.Any(id => !referenceable.Contains(id)))
            throw new ArgumentException("手工引用来源必须是本工具声明的输出端口。", nameof(manualReferenceSources));
        if (references.Distinct(StringComparer.Ordinal).Count() != references.Length)
            throw new ArgumentException("手工引用来源重复。", nameof(manualReferenceSources));
        ManualReferenceSources = Array.AsReadOnly(references);
        var branches = (controlOutputs ?? Array.Empty<ControlOutput>()).ToArray();
        if (branches.Any(output => string.IsNullOrWhiteSpace(output.Id)))
            throw new ArgumentException("控制输出身份不能为空。", nameof(controlOutputs));
        if (branches.Select(output => output.Id).Distinct(StringComparer.Ordinal).Count() != branches.Length)
            throw new ArgumentException("控制输出身份重复。", nameof(controlOutputs));
        ControlOutputs = Array.AsReadOnly(branches);
        EnableRules = Array.AsReadOnly((enableRules ?? Array.Empty<FieldEnableRule>()).ToArray());
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string IconKey { get; }
    public string Category { get; }
    public int Order { get; }
    public IReadOnlyList<ToolParameterDeclaration> Parameters { get; }
    public ToolResultDeclaration Result { get; }
    public ToolDisplayDeclaration Display { get; }
    public ToolInputTemplate Inputs { get; }
    /// <summary>有本地 ROI 文档的工具给出声明；无 ROI 的工具为 null。</summary>
    public RoiEditDeclaration? Roi { get; }
    /// <summary>行级面板替换；页级不走此字段（需独立登记）。</summary>
    public ToolPanelOverride? PanelOverride { get; }
    /// <summary>节点外观类型（原件 ToolItemInfo.xml 的 displayType），取值见 <see cref="ToolCatalogDescriptor"/>。</summary>
    public string NodeDisplayType { get; }
    /// <summary>控制输出（原厂条件分支）：声明顺序即命中槽位序号。</summary>
    public IReadOnlyList<ControlOutput> ControlOutputs { get; }
    /// <summary>原厂 DisEnableOperation 的禁用规则。</summary>
    public IReadOnlyList<FieldEnableRule> EnableRules { get; }
    /// <summary>参数分组（原厂 Category）；渲染只用 Page + Section。</summary>
    public IReadOnlyList<ToolSectionDeclaration> Sections { get; }
    /// <summary>条件显隐规则（原厂 Triggers/VisibleOperation）。</summary>
    public IReadOnlyList<FieldVisibilityRule> Visibility { get; }
    /// <summary>成对编辑（原厂 IntegerBettween）。</summary>
    public IReadOnlyList<ToolPairGroupDeclaration> Pairs { get; }
    /// <summary>输入行呈现（按端口）；未声明的端口不产出输入行。</summary>
    public IReadOnlyList<ToolInputPresentation> InputPresentations { get; }
    /// <summary>参数页里的 ROI 组（原厂 ROISelecter + GroupLinkItem）；无 ROI 的工具为空。</summary>
    public IReadOnlyList<RoiGroupDeclaration> RoiGroups { get; }
    /// <summary>组合体输入端口（本地 ROI 文档、ROI 几何、Assist 等；原厂 Algorithm.xml 的额外 Input）。</summary>
    public IReadOnlyList<Port> ExtraInputs { get; }
    /// <summary>行端口之外的输出端口（原厂 Output 段的标量/图像/区域输出）。</summary>
    public IReadOnlyList<Port> ExtraOutputs { get; }
    /// <summary>可作为手工引用来源的输出端口身份（原厂引用会话的可选项集合）。</summary>
    public IReadOnlyList<string> ManualReferenceSources { get; }
}
