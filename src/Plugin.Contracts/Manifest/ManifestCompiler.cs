using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Plugin.Contracts.Manifest;

/// <summary>
/// 把 <see cref="ToolManifest"/> 编译成平台契约（Schema / Descriptor）。
/// 纯函数、无副作用、无 IO：同一 manifest 必须产出字节级相同的结果，
/// 这是迁移期“生成物与手写声明等价”快照测试的判据。
/// </summary>
/// <remarks>
/// 对应原件平台侧的“声明解释器”：工具给 XML，平台生成参数页、IO 与显示对象。
/// 本方由编译器在工具定义静态构造期执行一次，运行期零重复构建。
/// </remarks>
public static class ManifestCompiler
{
    public const string ImageInput = "input.image";
    public const string RoiInput = "input.roi";
    public const string FixtureInput = "input.fixture";
    public const string MaskInput = "input.mask";

    /// <summary>组合模板展开为标准输入端口集。端口身份固定，工具不手写。</summary>
    public static IReadOnlyList<Port> InputPorts(ToolInputTemplate template)
    {
        var ports = new List<Port>();
        if (template == ToolInputTemplate.None) return Array.Empty<Port>();
        ports.Add(new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference));
        if (template is ToolInputTemplate.ImageRoi or ToolInputTemplate.ImageRoiFixture or ToolInputTemplate.ImageRoiMask)
            ports.Add(new Port(RoiInput, TypeRef.ResourceOf(ResourceKind.Region),
                Required: false, Nullable: true, EditModes: FieldEditModes.Reference));
        // fixture 与 mask 是互斥的两类模板：掩膜模板不展开定位基准。
        if (template == ToolInputTemplate.ImageRoiFixture)
            ports.Add(new Port(FixtureInput, TypeRef.ResourceOf(ResourceKind.Region),
                Required: false, Nullable: true, EditModes: FieldEditModes.Reference));
        if (template == ToolInputTemplate.ImageRoiMask)
            ports.Add(new Port(MaskInput, TypeRef.ResourceOf(ResourceKind.Image),
                Required: false, Nullable: true, EditModes: FieldEditModes.Reference));
        return ports.AsReadOnly();
    }

    /// <summary>组合模板对应的图像输入端口（工作流图像回退目标）。</summary>
    public static string? WorkflowImageInput(ToolInputTemplate template)
        => template == ToolInputTemplate.None ? null : ImageInput;

    /// <summary>
    /// 参数声明 → 设置 Schema（**只含参数**，不含输入端口）。
    /// 设置读写只针对参数，混入输入端口会让 ReadSettings 要求 input.image。
    /// </summary>
    public static ToolSchema SettingsSchema(ToolManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var (ports, presentations) = BuildSettings(manifest, 0);
        return new ToolSchema(ports, Array.Empty<Port>(), new ToolPresentation(presentations),
            roiGroups: manifest.RoiGroups);
    }

    /// <summary>构建设置端口与呈现；<paramref name="startOrder"/> 供执行 Schema 在输入行之后接续编号。</summary>
    private static (List<Port> Ports, List<FieldPresentation> Presentations) BuildSettings(
        ToolManifest manifest, int startOrder)
    {
        var ports = new List<Port>(manifest.Parameters.Count);
        var presentations = new List<FieldPresentation>(manifest.Parameters.Count);
        var order = startOrder;
        foreach (var parameter in manifest.Parameters)
        {
            var choices = parameter.Choices;
            // 原厂 Integer 节点类型即 int32：整数参数未显式声明存储时按 Int32 落库，
            // 否则数值模型构建会因缺存储精度拒绝该行。
            ports.Add(new Port(parameter.Id, parameter.Type, EditModes: parameter.Modes,
                DefaultPresence: parameter.DefaultPresence,
                Storage: parameter.Storage ?? (parameter.Type == DataType.Integer ? NumericStorage.Int32 : null),
                Choices: choices.Count == 0 ? null : new TextChoiceConstraint(choices.Select(choice => choice.Value))));
            var editor = choices.Count != 0
                ? FieldEditorKind.Choice
                : parameter.Type.Shape switch
                {
                    ValueShape.Record => FieldEditorKind.Record,
                    ValueShape.Sequence => FieldEditorKind.Sequence,
                    _ => FieldEditorKind.Scalar
                };
            // 分组页：有 Section 声明时用它的 Page，否则退化用 Section 名。
            var section = manifest.Sections
                .FirstOrDefault(item => string.Equals(item.Section, parameter.Section, StringComparison.Ordinal));
            var page = section?.Page ?? parameter.Section;
            var pair = manifest.Pairs.FirstOrDefault(
                item => string.Equals(item.MinFieldId, parameter.Id, StringComparison.Ordinal));
            // 原厂 IsAbandon：端口照旧生成（读写与已保存方案不受影响），参数页不渲染该行。
            if (parameter.Abandon)
                continue;
            presentations.Add(new FieldPresentation(parameter.Id, parameter.Label, parameter.Section, order++, editor,
                choices.Select(choice => choice.Value),
                choices.ToDictionary(choice => choice.Value, choice => choice.Label, StringComparer.Ordinal),
                parameter.DisplayType)
            {
                Layout = new FieldLayout(page, parameter.Section,
                    VisibleWhen: Condition(manifest, parameter.Id),
                    PairWith: pair?.MaxFieldId,
                    PairMinimum: pair is null ? null : parameter.Minimum,
                    PairMaximum: pair is null ? null : parameter.Maximum,
                    // 只有成对的下限行才带区间显示名；非成对行留空（布局要求 PairWith 与显示名同时存在）。
                    PairDisplayName: pair is null ? null : pair.Label ?? parameter.Label,
                    Minimum: parameter.Minimum,
                    Maximum: parameter.Maximum,
                    DecimalPlaces: parameter.Type == DataType.Number ? parameter.Decimals : 0,
                    // 枚举参数只能选声明过的取值，不参与引用绑定。
                    // 原厂 SubscribeVisible：分组声明不允许订阅时，组内参数一律不给订阅入口。
                AllowReference: choices.Count == 0 && parameter.Modes.HasFlag(FieldEditModes.Reference)
                    && (section?.AllowReference ?? true),
                    Increment: parameter.Step ?? 1,
                    // 原厂 Expand：折叠区成员标记由声明直给，不按字段名或分组猜。
                    Advanced: parameter.Advanced,
                    // 原厂 DisEnableOperation：禁用而不隐藏。
                    EnabledWhen: DisabledBy(manifest, parameter.Id),
                    // 原厂 BooleanTips：开关右侧常驻小字（开/关两态）。
                    BooleanTips: parameter.TipsOn is null && parameter.TipsOff is null
                        ? null
                        : new BooleanStateTips(parameter.TipsOn ?? string.Empty, parameter.TipsOff ?? string.Empty),
                    // 原厂 Category 级开关与对齐校验用的分类名。
                    VmCategory: section?.VmCategory,
                    NoHeader: section?.NoHeader ?? false,
                    CanTriggerRun: section?.CanTriggerRun ?? false)
            });
        }
        return (ports, presentations);
    }

    /// <summary>
    /// 输入行的模板键（原厂 AlgorithmTab 的输入行同样带类型）：
    /// 图像/区域/记录型走组合槽，其余按端口类型定键。声明期一次算定，渲染期不再推导。
    /// </summary>
    private static string InputRowKey(Port port, FieldEditorKind editor)
        // 引用型资源输入（图像/区域/位姿）走引用行：未设置时由引用态占位，不进组合槽。
        => port.Type.Shape == ValueShape.Resource && (port.EditModes & FieldEditModes.Reference) != 0
            ? DeclaredFieldTemplateKey.Reference
            : InputRowKind(port, editor);

    private static string InputRowKind(Port port, FieldEditorKind editor) => editor switch
    {
        FieldEditorKind.Image => DeclaredFieldTemplateKey.ComposedSlot,
        FieldEditorKind.Region => DeclaredFieldTemplateKey.ComposedSlot,
        FieldEditorKind.Record => DeclaredFieldTemplateKey.ComposedSlot,
        _ => port.Type == DataType.Boolean ? DeclaredFieldTemplateKey.Boolean
            : port.Type == DataType.Text ? DeclaredFieldTemplateKey.String
            : port.Type == DataType.Number ? DeclaredFieldTemplateKey.Float
            : DeclaredFieldTemplateKey.Integer
    };

    /// <summary>
    /// 单值显隐规则 → 布局条件。多值或多规则的情况留给页面装配器（这里只收敛唯一情形），
    /// 避免在声明期猜测组合语义。
    /// </summary>
    private static FieldCondition? Condition(ToolManifest manifest, string fieldId)
    {
        var rules = manifest.Visibility
            .Where(rule => rule.TargetFieldIds.Contains(fieldId, StringComparer.Ordinal))
            .ToArray();
        if (rules.Length != 1 || rules[0].Values.Count != 1) return null;
        var rule = rules[0];
        return new FieldCondition(rule.SourceFieldId, Literal(rule.Values[0]));
    }

    /// <summary>
    /// 禁用条件（原厂 DisEnableOperation）：规则表达「控制器取该值时禁用」，
    /// <see cref="FieldCondition"/> 表达「满足时启用」，因此取反。
    /// </summary>
    private static FieldCondition? DisabledBy(ToolManifest manifest, string fieldId)
    {
        var rules = manifest.EnableRules
            .Where(rule => rule.TargetFieldIds.Contains(fieldId, StringComparer.Ordinal))
            .ToArray();
        if (rules.Length != 1 || rules[0].Values.Count != 1) return null;
        var rule = rules[0];
        return new FieldCondition(rule.SourceFieldId, Literal(rule.Values[0]), Negate: true);
    }

    private static object Literal(string value)
    {
        if (bool.TryParse(value, out var flag)) return flag;
        if (long.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var number)) return number;
        return value;
    }

    /// <summary>
    /// 执行 Schema（<c>Describe()</c>）：组合模板输入 + 设置参数，
    /// 与既有工具“描述 = 输入端口 + 参数”的约定一致。
    /// </summary>
    public static ToolSchema ExecutionSchema(ToolManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var inputs = InputPorts(manifest.Inputs);
        var presentations = new List<FieldPresentation>();
        // 输入行呈现：缺它输入区会组装成空页（原厂由 AlgorithmTab 的输入行提供）。
        // 按端口声明而不是全局一份，因为不同输入可能落在不同分组（如 ROI 与掩膜同组、图像单独一组）。
        var order = 0;
        foreach (var port in inputs)
        {
            var input = manifest.InputPresentations
                .FirstOrDefault(row => string.Equals(row.PortId, port.Id, StringComparison.Ordinal));
            if (input is null) continue;
            presentations.Add(new FieldPresentation(port.Id, input.Label, input.Section, order++, input.Editor,
                null, null, InputRowKey(port, input.Editor))
            {
                Layout = new FieldLayout(input.Page, input.Section,
                    AllowReference: port.EditModes.HasFlag(FieldEditModes.Reference))
            });
        }
        var (settingsPorts, settingsPresentations) = BuildSettings(manifest, presentations.Count);
        // 执行 Schema 不强制参数：参数由节点配置提供，缺省不应阻止执行侧的输入构造。
        // 设置 Schema 保持 Required=true（BooleanTips 等布局校验收这一条）。
        var optionalParameters = settingsPorts.Select(port => new Port(port.Id, port.Type,
            Required: false, Nullable: port.Nullable, Storage: port.Storage,
            EditModes: port.EditModes, DefaultPresence: port.DefaultPresence, Choices: port.Choices));
        // 组合体输入（本地 ROI 文档、ROI 几何、Assist）保留端口身份但不进参数页的行。
        var ports = inputs.Concat(manifest.ExtraInputs).Concat(optionalParameters).ToArray();
        // 原厂 Algorithm.xml（IO 声明）与 AlgorithmTab.xml（界面）是两份：
        // 执行 Schema 声明输入与输出端口，但参数行的呈现归 SettingsSchema，不在这里重复一份。
        var outputs = ResultSchema(manifest).Outputs;
        return new ToolSchema(ports, outputs, new ToolPresentation(presentations),
            WorkflowImageInput(manifest.Inputs), roiGroups: manifest.RoiGroups,
            // 原厂控制输出（条件分支）：声明顺序即命中槽位序号。
            controlOutputs: manifest.ControlOutputs.Count == 0 ? null : manifest.ControlOutputs);
    }

    /// <summary>结果声明 → 结果 Schema：行端口是唯一输出，行类型由字段声明构造。</summary>
    public static ToolSchema ResultSchema(ToolManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var declaration = manifest.Result;
        // 属性列：每个字段就是独立输出端口（原厂 PropertyList），结果树按字段生成。
        if (declaration.Shape == ToolResultShape.PropertyList)
        {
            var outputs = declaration.Fields.Select(ToPort).Concat(manifest.ExtraOutputs).ToArray();
            return new ToolSchema(Array.Empty<Port>(), outputs,
                outputDisplay: declaration.Fields.Select(ToDisplayNode).ToArray());
        }
        // 行表：行端口 + 附加输出（原厂 Output 段的标量/图像/区域输出）；行内字段不是输出端口。
        var rowType = TypeRef.RecordOf(declaration.RowsPortId + ".row",
            declaration.Fields.Select(ToPort));
        var rows = new Port(declaration.RowsPortId!, TypeRef.SequenceOf(rowType));
        // 行端口保持最后一位，与既有工具的输出端口顺序一致。
        return new ToolSchema(Array.Empty<Port>(), manifest.ExtraOutputs.Concat(new[] { rows }).ToArray());
    }

    private static Port ToPort(ToolResultField field)
        => new(field.Id, field.Type, Required: field.Required, Nullable: field.Nullable, Storage: field.Storage);

    private static OutputDisplayNode ToDisplayNode(ToolResultField field)
        => new(field.Id, field.Label,
            field.Children.Count == 0
                ? null
                : field.Children.Select(child => new OutputDisplayNode(child.Id, child.Label)).ToArray(),
            displayInResultPanel: field.ShowInResultPanel);

    /// <summary>
    /// 结果声明 → 显示合同。只给路径，不带渲染实现；
    /// 轮廓点等系统类型由宿主按 <c>IToolResultOutlineDeclaration</c> 统一请求，不在此声明。
    /// </summary>
    public static ResultDisplayDescriptor Descriptor(ToolManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var declaration = manifest.Result;
        // 显示合同只看是否声明了行端口与行槽位，与结果形态无关
        // （原厂 Display.xml 引用哪个端口由声明给出，不由结果形态决定）。
        if (declaration.RowsPortId is null || declaration.Slots is null)
            throw new InvalidOperationException(
                "该结果没有行显示合同；请声明行端口与行槽位，或改用 IToolDisplaySettingsProvider。");
        var rows = new ResultFieldPath(declaration.RowsPortId);
        var slots = declaration.Slots;
        return new ResultDisplayDescriptor(
            rows,
            new ResultFieldPath(declaration.RowsPortId, new[] { slots.Identity }),
            new ResultFieldPath(declaration.RowsPortId, new[] { slots.Rectangle }),
            new ResultFieldPath(declaration.RowsPortId, new[] { slots.GeometrySource }),
            new ResultFieldPath(declaration.RowsPortId, new[] { slots.TextValue }),
            new ResultFieldPath(declaration.RowsPortId, new[] { slots.Passed }),
            innerRectangle: slots.InnerRectangle is { } inner
                ? new ResultFieldPath(declaration.RowsPortId, new[] { inner })
                : null,
            selectedRectangleColor: declaration.SelectedRectangleColor,
            centroid: slots.Centroid is { } centroid
                ? new ResultFieldPath(declaration.RowsPortId, new[] { centroid })
                : null);
    }

    /// <summary>有 ROI 声明的工具返回声明；无 ROI 的工具返回 null（不伪造空声明）。</summary>
    public static RoiEditDeclaration? RoiEdit(ToolManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return manifest.Roi;
    }

    // ---------------- 显示 ----------------

    public const string VisibleRole = "visible";
    public const string OkRole = "ok";
    public const string NgRole = "ng";
    public const string OpacityRole = "opacity";
    public const string WidthRole = "width";
    public const string ContentRole = "content";
    public const string SizeRole = "size";
    public const string PositionXRole = "x";
    public const string PositionYRole = "y";

    /// <summary>显示字段身份：<c>display.&lt;group&gt;.&lt;role&gt;</c>，与既有工具字段命名一致。</summary>
    public static string DisplayField(string group, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return "display." + group + "." + role;
    }

    private static readonly ResultDisplayColor DefaultOk = new(102, 255, 0);
    private static readonly ResultDisplayColor DefaultNg = new(255, 0, 0);

    /// <summary>
    /// 显示声明 → 显示字段 Schema。每个显示项生成一组 `display.&lt;group&gt;.*`：
    /// 图形项给 visible/ok/ng/opacity/width；文本项给 visible/content/ok/ng/size/opacity/x/y；
    /// 轮廓点只需 visible/ok/ng（系统类型负责点集与三态）。
    /// </summary>
    public static ToolSchema DisplaySchema(ToolManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var ports = new List<Port>();
        var presentations = new List<FieldPresentation>();
        var order = 0;
        // 当前显示项的“样式”对话框（原厂 ImageViewConfig 画笔）；文本项无样式框。
        string? dialogGroup = null;
        string? dialogTitle = null;
        // unformatted：文本坐标这类「不限固定小数位」的行（原厂该行不带小数位声明）。
        void Add(string id, string label, string section, TypeRef type, double? minimum = null,
            double? maximum = null, double? increment = null, int? decimals = null,
            FieldEditModes modes = FieldEditModes.Constant, bool primary = false, bool unformatted = false)
        {
            ports.Add(new Port(id, type, EditModes: modes));
            // 显示行的模板键与原厂一致：布尔→Boolean、文本→String、数值→Float、颜色记录→Slot（组合槽）。
            var editor = type.Shape == ValueShape.Record ? FieldEditorKind.Record : FieldEditorKind.Scalar;
            var displayType = type.Shape == ValueShape.Record ? DeclaredFieldTemplateKey.ComposedSlot
                : type == DataType.Boolean ? DeclaredFieldTemplateKey.Boolean
                : type == DataType.Text ? DeclaredFieldTemplateKey.String
                : type == DataType.Number ? DeclaredFieldTemplateKey.Float
                : DeclaredFieldTemplateKey.Integer;
            presentations.Add(new FieldPresentation(id, label, section, order++, editor, null, null, displayType)
            {
                Layout = new FieldLayout("结果显示", section, Minimum: minimum, Maximum: maximum,
                    // 原厂每行都带小数位声明：非数值行（布尔/颜色记录）声明 0，
                    // 平台对 null 有严格限制（只允许订阅同槽数值文本）。
                    DecimalPlaces: unformatted ? null : decimals ?? 0,
                    AllowReference: modes.HasFlag(FieldEditModes.Reference),
                    Increment: increment ?? 1,
                    DialogGroup: dialogGroup, DialogTitle: dialogTitle,
                    DialogPrimary: primary && dialogGroup is not null)
            });
        }

        foreach (var item in manifest.Display.Items)
        {
            var group = item.GroupKey;
            var isText = item.Role == ToolDisplayRole.Text;
            var section = isText ? "文本显示" : "图像显示";
            // 声明里的组键是短键（原厂 Display.xml 的对象名去掉 display. 前缀），
            // 字段 id 与分组身份统一由 DisplayField / 这里补成 display.<组键>。
            dialogGroup = item.StyleDialog ? "display." + group : null;
            dialogTitle = item.StyleDialog ? item.Label : null;
            // 文本组是否带总开关行由声明给出（原厂各工具不同）；图形项与纯可见性项恒带 visible。
            // 文本的总开关行归「图像显示」分组（原厂该行就画在图像显示里），「文本显示」固定七个角色。
            if (!isText || item.ShowVisible)
                Add(DisplayField(group, VisibleRole), item.Label, "图像显示", DataType.Boolean, primary: true);
            // 纯可见性项：只产出眼睛开关，不产出颜色/透明度/线宽（原厂只有 Show 的 ImageViewConfig）。
            if (item.VisibleOnly)
            {
                dialogGroup = null;
                dialogTitle = null;
                continue;
            }
            if (isText)
            {
                Add(DisplayField(group, ContentRole), "内容", "文本显示", DataType.Text,
                    modes: FieldEditModes.Constant | FieldEditModes.DisplayExpression);
                Add(DisplayField(group, OkRole), "OK颜色", section, PresentationDeclarations.Rgba);
                Add(DisplayField(group, NgRole), "NG颜色", section, PresentationDeclarations.Rgba);
                Add(DisplayField(group, SizeRole), "字号", "文本显示", DataType.Number, 6, 72, 1, decimals: 2);
                Add(DisplayField(group, OpacityRole), "透明度", "文本显示", DataType.Number, 0, 1, .1, decimals: 2);
                // 原厂文本坐标两行「不限固定小数位」：声明不带小数位，由订阅文本同槽承载。
                Add(DisplayField(group, PositionXRole), "位置X", "文本显示", DataType.Number,
                    modes: FieldEditModes.Constant | FieldEditModes.Reference, unformatted: true);
                Add(DisplayField(group, PositionYRole), "位置Y", "文本显示", DataType.Number,
                    modes: FieldEditModes.Constant | FieldEditModes.Reference, unformatted: true);
                continue;
            }
            Add(DisplayField(group, OkRole), "OK颜色", section, PresentationDeclarations.Rgba);
            Add(DisplayField(group, NgRole), "NG颜色", section, PresentationDeclarations.Rgba);
            if (item.Role == ToolDisplayRole.Outline) continue;
            Add(DisplayField(group, OpacityRole), "透明度", section, DataType.Number, 0, 1, .01, decimals: 2);
            Add(DisplayField(group, WidthRole), "线宽", section, DataType.Number, 1, 10, 1, decimals: 2);
        }
        return new ToolSchema(ports, Array.Empty<Port>(), new ToolPresentation(presentations));
    }

    /// <summary>显示字段默认值。对应原件 Display.xml 的初始对象（节点可改并持久化）。</summary>
    public static InputValues DisplayDefaults(ToolManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var schema = DisplaySchema(manifest);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in manifest.Display.Items)
        {
            var group = item.GroupKey;
            var ok = item.Ok ?? DefaultOk;
            var ng = item.Ng ?? DefaultNg;
            // 与 DisplaySchema 一致：文本组只有声明了 ShowVisible 才写该值，避免未声明端口。
            if (item.Role != ToolDisplayRole.Text || item.ShowVisible)
                values[DisplayField(group, VisibleRole)] = item.Visible;
            // 纯可见性项只有眼睛开关，没有颜色/透明度/线宽。
            if (item.VisibleOnly) continue;
            values[DisplayField(group, OkRole)] = PresentationDeclarations.Color(ok.Red, ok.Green, ok.Blue, ok.Alpha);
            values[DisplayField(group, NgRole)] = PresentationDeclarations.Color(ng.Red, ng.Green, ng.Blue, ng.Alpha);
            if (item.Role == ToolDisplayRole.Text)
            {
                values[DisplayField(group, ContentRole)] = item.TextContent ?? string.Empty;
                values[DisplayField(group, SizeRole)] = 10d;
                values[DisplayField(group, OpacityRole)] = 1d;
                values[DisplayField(group, PositionXRole)] = 0d;
                values[DisplayField(group, PositionYRole)] = 0d;
                continue;
            }
            if (item.Role == ToolDisplayRole.Outline) continue;
            values[DisplayField(group, OpacityRole)] = item.Opacity;
            values[DisplayField(group, WidthRole)] = item.Width;
        }
        return new InputValues(schema.Inputs, values);
    }

    /// <summary>
    /// 节点显示值 → 纯外观。只读声明过的字段，越界即报错（不静默回退）。
    /// 缺主图形或文本项时直接抛：显示合同必须完整。
    /// </summary>
    public static ResultDisplayAppearance Appearance(ToolManifest manifest, FieldSettings fields)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(fields);
        var items = manifest.Display.Items;

        ToolDisplayItem Item(ToolDisplayRole role)
            => items.FirstOrDefault(item => item.Role == role)
               ?? throw new ArgumentException($"显示声明缺少 {role} 项。");

        ResultDisplayColor Color(string group, string role)
        {
            var value = fields.Get<RecordValue>(DisplayField(group, role));
            return new(checked((byte)value.Get<long>("r")), checked((byte)value.Get<long>("g")),
                checked((byte)value.Get<long>("b")), checked((byte)value.Get<long>("a")));
        }

        double Number(string group, string role, double minimum, double maximum)
        {
            var id = DisplayField(group, role);
            var value = fields.Get<double>(id);
            if (!double.IsFinite(value) || value < minimum || value > maximum)
                throw new ArgumentException($"显示数值超出范围：{id}");
            return value;
        }

        ResultGraphicAppearance? Graphic(ToolDisplayRole role)
        {
            var item = items.FirstOrDefault(candidate => candidate.Role == role);
            if (item is null) return null;
            var group = item.GroupKey;
            var style = new ResultGraphicAppearance(
                fields.Get<bool>(DisplayField(group, VisibleRole)),
                Color(group, OkRole), Color(group, NgRole),
                Number(group, OpacityRole, 0, 1), Number(group, WidthRole, 1, 10),
                Fill: item.Fill ? Color(group, OkRole) : null,
                FixedJudgment: item.FixedJudgment,
                ShowMeasurements: item.ShowMeasurements,
                PointLabel: item.PointLabel);
            return style;
        }

        // 主图形：没有独立声明时用固定样式（原件 PixelCount 的区间行就是固定绿/红、线宽 2）。
        var graphic = Graphic(ToolDisplayRole.Graphic)
            ?? new ResultGraphicAppearance(true, DefaultOk, DefaultNg, 1, 2);

        var textItem = items.FirstOrDefault(item => item.Role == ToolDisplayRole.Text);
        // 原厂 Display.xml 没有文本对象就没有文本显示：Text 项可选，缺省为不可见空文本。
        ResultTextAppearance text;
        if (textItem is null)
        {
            text = new ResultTextAppearance(false, string.Empty, DefaultOk, DefaultNg, 12, 1, 0, 0);
        }
        else
        {
            var textGroup = textItem.GroupKey;
            var contentSetting = fields.Read(DisplayField(textGroup, ContentRole));
            var content = contentSetting switch
            {
                DisplayExpressionSetting expression => expression.Text,
                ConstantSetting { Value: string raw } => raw,
                _ => throw new ArgumentException("文本内容设置无效。")
            };
            ParameterSetting Position(string role)
            {
                var setting = fields.Read(DisplayField(textGroup, role));
                if (setting is ReferenceSetting) return setting;
                if (setting is ConstantSetting { Value: double number } && double.IsFinite(number)) return setting;
                throw new ArgumentException("文本位置必须是有限数或已声明数字来源。");
            }
            var x = Position(PositionXRole);
            var y = Position(PositionYRole);
            text = new ResultTextAppearance(
                // 有总开关行时读字段值；没有时取声明值（原厂 Display.xml 各工具不同）。
                textItem.ShowVisible
                    ? fields.Get<bool>(DisplayField(textGroup, VisibleRole))
                    : textItem.Visible,
                content,
                Color(textGroup, OkRole),
                Color(textGroup, NgRole),
                Number(textGroup, SizeRole, 6, 72),
                Number(textGroup, OpacityRole, 0, 1),
                x is ConstantSetting { Value: double xv } ? xv : 0,
                y is ConstantSetting { Value: double yv } ? yv : 0,
                contentSetting as DisplayExpressionSetting,
                x, y);
        }

        var outlineItem = items.FirstOrDefault(candidate => candidate.Role == ToolDisplayRole.Outline);
        ResultOutlineAppearance? outline = outlineItem is null
            ? null
            : new(fields.Get<bool>(DisplayField(outlineItem.GroupKey, VisibleRole)),
                Color(outlineItem.GroupKey, OkRole), Color(outlineItem.GroupKey, NgRole));

        return new ResultDisplayAppearance(graphic, text,
            Inspection: Graphic(ToolDisplayRole.Inspection),
            Mask: Graphic(ToolDisplayRole.Mask),
            Annulus: Graphic(ToolDisplayRole.Annulus),
            Outline: outline,
            InnerRectangle: Graphic(ToolDisplayRole.InnerRectangle),
            ReferencePoint: Graphic(ToolDisplayRole.ReferencePoint),
            RunPoint: Graphic(ToolDisplayRole.RunPoint),
            Centroids: Graphic(ToolDisplayRole.Centroid));
    }
}
