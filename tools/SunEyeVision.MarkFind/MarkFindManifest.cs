using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.MarkFind.Runtime;

/// <summary>
/// 图形定位的声明唯一真源（原件 <c>IMVSMarkFindModuAlgorithm.xml</c> +
/// <c>AlgorithmTab.xml</c> + <c>Display.xml</c>）。
/// 参数由 <see cref="MarkFindFields"/> 的表生成——那张表同时承载原厂 VM 参数名与绑定名，
/// 因此不再重写一份声明。
/// </summary>
internal static class MarkFindManifest
{
    /// <summary>
    /// 原厂 <c>IMVSMarkFindModuAlgorithmTab.xml</c> 标 <c>IsAbandon="True"</c> 的 7 个参数
    /// （RangScale / RangScaleX / RangScaleY / ScaleXLimitEnable / RangeScaleXLimit /
    /// ScaleYLimitEnable / RangeScaleYLimit）；本方成对字段展开后是 12 个。
    /// 保留端口与取值（算法与已保存方案兼容），参数页不渲染。
    /// 静态字段必须声明在 <see cref="Value"/> 之前：C# 按文本顺序初始化。
    /// </summary>
    private static readonly HashSet<string> Abandoned = new(StringComparer.Ordinal)
    {
        nameof(MarkFindSettings.ScaleStart), nameof(MarkFindSettings.ScaleEnd),
        nameof(MarkFindSettings.ScaleXStart), nameof(MarkFindSettings.ScaleXEnd),
        nameof(MarkFindSettings.ScaleYStart), nameof(MarkFindSettings.ScaleYEnd),
        nameof(MarkFindSettings.ScaleXLimitEnabled), nameof(MarkFindSettings.ScaleXLimitLow),
        nameof(MarkFindSettings.ScaleXLimitHigh),
        nameof(MarkFindSettings.ScaleYLimitEnabled), nameof(MarkFindSettings.ScaleYLimitLow),
        nameof(MarkFindSettings.ScaleYLimitHigh)
    };

    public static ToolManifest Value { get; } = Build();

    private static ToolParameterDeclaration Parameter(MarkFindFieldSpec field) => new(
        field.Id, field.Label, field.Section, MarkFindFields.PortType(field), field.Default,
        minimum: field.Minimum, maximum: field.Maximum,
        step: field.Increment,
        decimals: field.Type == typeof(string) ? 0 : field.DecimalPlaces,
        // 原厂 Integer 节点即 int32：整数参数必须带存储精度。
        storage: field.Type == typeof(int) ? NumericStorage.Int32 : null,
        modes: FieldEditModes.Constant
            | (field.AllowReference ? FieldEditModes.Reference : FieldEditModes.None),
        propertyName: field.Name,
        displayType: MarkFindFields.DisplayTypeOf(field),
        abandon: Abandoned.Contains(field.Name),
        advanced: field.Advanced,
        choices: field.Choices.Count == 0
            ? null
            : field.Choices.Select(key => new ToolChoice(key, field.ChoiceLabels[key])).ToArray());

    /// <summary>分组页：按 (Page, Section) 去重，保持表内顺序。</summary>
    /// <summary>
    /// 分组声明。证据 <c>IMVSMarkFindModuAlgorithmTab.xml</c>：
    /// <list type="bullet">
    /// <item><c>Category Name="Tab_Run Params" CanTriggerRun="TRUE"</c></item>
    /// <item><c>Category Name="Tab_Check Result" CanTriggerRun="TRUE" SubscribeVisible="False"</c></item>
    /// </list>
    /// 两处 CanTriggerRun 分别对应运行参数区与结果判断区；判定区另不提供订阅来源入口。
    /// 基本参数页的 ROI/Assist 由特征模型面板承载，没有独立参数分组，故不在此声明。
    /// </summary>
    private static IReadOnlyList<ToolSectionDeclaration> Sections() => MarkFindFields.All
        .GroupBy(field => (field.Page, field.Section))
        .Select(group =>
        {
            var judge = string.Equals(group.Key.Section, MarkFindFields.JudgeSection,
                StringComparison.Ordinal);
            var run = string.Equals(group.Key.Section, MarkFindFields.RunSection, StringComparison.Ordinal);
            return new ToolSectionDeclaration(group.Key.Page, group.Key.Section,
                canTriggerRun: run || judge,
                allowReference: !judge);
        })
        .ToArray();

    /// <summary>联动显隐：两个枚举控制器选到 <c>Manual</c>，布尔控制器选到真。</summary>
    private static IReadOnlyList<FieldVisibilityRule> Visibility() => MarkFindFields.All
        .Where(field => field.VisibleWhen is not null)
        .Select(field =>
        {
            var controller = MarkFindFields.Spec(field.VisibleWhen!);
            return new FieldVisibilityRule(controller.Id,
                new[] { controller.Type.IsEnum ? "Manual" : "true" }, new[] { field.Id });
        })
        .ToArray();

    private static IReadOnlyList<ToolPairGroupDeclaration> Pairs() => MarkFindFields.All
        .Where(field => field.PairWith is not null)
        .Select(field => new ToolPairGroupDeclaration(
            "pair:" + field.Id, field.Id, MarkFindFields.Spec(field.PairWith!).Id,
            label: field.PairLabel ?? field.Label))
        .ToArray();

    private static IReadOnlyList<ToolResultField> BoxChildren() => new[]
    {
        new ToolResultField("center", "矩形中心", FixtureDeclarations.Point),
        new ToolResultField("width", "矩形宽度", DataType.Number),
        new ToolResultField("height", "矩形高度", DataType.Number),
        new ToolResultField("angle-degrees", "矩形角度", DataType.Number)
    };

    private static ToolManifest Build() => new(
        MarkFindTool.Id, "图形定位",
        "按学习模板在搜索区域内定位标记，输出匹配点、角度、分数与匹配框",
        "graph-locate", "定位", 30,
        MarkFindFields.All.Select(Parameter).ToArray(),
        new ToolResultDeclaration(MarkFindSchema.Rows,
            new[]
            {
                // 行表形态的行成员决定行记录类型：必须与原 RowsType 的 12 个成员逐项一致，
                // 否则已保存方案与消费方的行结构会变。
                new ToolResultField("element-id", "序号", DataType.Integer),
                new ToolResultField("match-point", "匹配点", MarkFindSchema.PointType),
                new ToolResultField("center-x", "匹配点X", DataType.Number),
                new ToolResultField("center-y", "匹配点Y", DataType.Number),
                new ToolResultField("angle-degrees", "角度", DataType.Number),
                new ToolResultField("scale", "缩放", DataType.Number, nullable: true),
                new ToolResultField("score", "匹配分数", DataType.Number),
                new ToolResultField("box", "匹配框", MarkFindSchema.BoxType, children: BoxChildren()),
                new ToolResultField("region", "区域", TypeRef.ResourceOf(ResourceKind.Region)),
                new ToolResultField("outline", "轮廓", TypeRef.SequenceOf(MarkFindSchema.PointType),
                    required: false),
                new ToolResultField("judgment-passed", "判定", DataType.Boolean),
                new ToolResultField("failed-limits", "未通过判据", TypeRef.SequenceOf(DataType.Text),
                    required: false)
            },
            slots: new ToolResultSlots("element-id", "box", "region", "score", "judgment-passed",
                centroid: "match-point"),
            columns: new[]
            {
                new ToolResultColumn("element-id", "序号", 0, key: "element-id"),
                new ToolResultColumn("center-x", "匹配点X", 2, key: "center-x",
                    geometryMember: "match-point", geometrySourceMember: "region"),
                new ToolResultColumn("center-y", "匹配点Y", 2, key: "center-y",
                    geometryMember: "match-point", geometrySourceMember: "region"),
                new ToolResultColumn("angle-degrees", "角度", 2, key: "angle",
                    geometryMember: "match-point", geometrySourceMember: "region"),
                new ToolResultColumn("score", "匹配分数", 2, key: "score",
                    geometryMember: "match-point", geometrySourceMember: "region"),
                new ToolResultColumn("judgment-passed", "判定", key: "judgment")
            },
            kind: ResultProjectionKind.StandardTable,
            // 选中行的高亮色（原厂属性列表选中框为蓝）。
            selectedRectangleColor: new ResultDisplayColor(0, 0, 255),
            shape: ToolResultShape.Rows),
        new ToolDisplayDeclaration(new[]
        {
            // 原厂 Display.xml：匹配结果/匹配位置/传感器区域/轮廓/基准点/运行点/盲区 + 文本。
            new ToolDisplayItem(ToolDisplayRole.Inspection, "匹配结果", groupKey: "match",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            // 证据 IMVSMarkFindModuAlgorithmTab.xml ResultShow：MatchLocation 的 DisplayName 是
            // "Match Point"；对应 IMVSMarkFindModuDisplay.xml 的 Feature Match Points
            // （type=point，ok=#66ff00，ng=#ff0000，linewidth=2）。这就是匹配点，不是普通图形组。
            new ToolDisplayItem(ToolDisplayRole.Centroid, "匹配位置", groupKey: "location",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            new ToolDisplayItem(ToolDisplayRole.Inspection, "传感器区域", groupKey: "inspection",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            // 轮廓在编辑期显示装配里是一个完整图形组（五个角色），因此按检测区声明。
            // 原厂轮廓是系统类型（ResultOutlineSet）：只有可见/OK/NG 三色，没有透明度与线宽，
            // 因此不产图形对话框分组（不进 display.outline 的五角色集合）。
            new ToolDisplayItem(ToolDisplayRole.Outline, "匹配轮廓点", groupKey: "outline",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0),
                styleDialog: false),
            new ToolDisplayItem(ToolDisplayRole.ReferencePoint, "基准点", groupKey: "reference-point",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0), width: 1),
            new ToolDisplayItem(ToolDisplayRole.RunPoint, "运行点", groupKey: "run-point",
                ok: new ResultDisplayColor(255, 0, 0), ng: new ResultDisplayColor(255, 0, 0), width: 1),
            // 证据 IMVSMarkFindModuDisplay.xml：ExternROI type=polygon，ok=#0000ff、linewidth=2、opacity=0.5。
            new ToolDisplayItem(ToolDisplayRole.Mask, "盲区", groupKey: "mask",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0),
                width: 2, opacity: 0.5),
            new ToolDisplayItem(ToolDisplayRole.Text, "文本", groupKey: "text")
        }),
        // 参数页没有独立输入行（由 ROI 组与 Assist 组承载），但执行 Schema 必须声明三个输入端口。
        ToolInputTemplate.Image,
        extraInputs: new[]
        {
            new Port(MarkFindSchema.RoiInput, TypeRef.ResourceOf(ResourceKind.Region),
                Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
            new Port(MarkFindSchema.FixtureInput, FixtureDeclarations.Info,
                Required: false, Nullable: true,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference)
        },
        nodeDisplayType: "DEFAULT",
        sections: Sections(),
        visibility: Visibility(),
        pairs: Pairs(),
        extraOutputs: new[]
        {
            new Port(MarkFindSchema.RunTime, DataType.Number, Storage: NumericStorage.Float32),
            new Port(MarkFindSchema.Status, DataType.Integer, Storage: NumericStorage.Int32),
            new Port(MarkFindSchema.Count, DataType.Integer, Storage: NumericStorage.Int32),
            new Port(MarkFindSchema.Scores, DataType.NumberArray, Storage: NumericStorage.Float32),
            new Port(MarkFindSchema.MatchPoints, TypeRef.SequenceOf(MarkFindSchema.PointType)),
            new Port(MarkFindSchema.Angles, DataType.NumberArray, Storage: NumericStorage.Float32),
            new Port(MarkFindSchema.Scales, DataType.NumberArray, Nullable: true, Storage: NumericStorage.Float32),
            new Port(MarkFindSchema.Boxes, TypeRef.SequenceOf(MarkFindSchema.BoxType)),
            new Port(MarkFindSchema.OutlinePoints, MarkFindSchema.OutlineSetType, Nullable: true),
            new Port(MarkFindSchema.OutputFixture, FixtureDeclarations.Info, Nullable: true)
        },
        manualReferenceSources: new[] { MarkFindSchema.OutlinePoints });
}
