using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.GrayMatch.Runtime;

/// <summary>
/// 灰度匹配的声明唯一真源（原件 <c>IMVSGrayMatchModuAlgorithm.xml</c> +
/// <c>AlgorithmTab.xml</c> + <c>Display.xml</c>）。
/// 参数由 <see cref="GrayMatchFields"/> 的表生成——那张表同时承载原厂 VM 参数名与绑定名，
/// 因此不再重写一份声明。
/// </summary>
internal static class GrayMatchManifest
{
    public static ToolManifest Value { get; } = Build();

    private static ToolParameterDeclaration Parameter(GrayMatchFieldSpec field) => new(
        field.Id, field.Label, field.Section, GrayMatchFields.PortType(field), field.Default,
        minimum: field.Minimum, maximum: field.Maximum,
        step: field.Increment,
        decimals: field.Type == typeof(string) ? 0 : field.DecimalPlaces,
        // 原厂 Integer 节点即 int32：整数参数必须带存储精度。
        storage: field.Type == typeof(int) ? NumericStorage.Int32 : null,
        modes: FieldEditModes.Constant
            | (field.AllowReference ? FieldEditModes.Reference : FieldEditModes.None),
        propertyName: field.Name,
        displayType: GrayMatchFields.DisplayTypeOf(field),
        advanced: field.Advanced,
        choices: field.Choices.Count == 0
            ? null
            : field.Choices.Select(key => new ToolChoice(key, field.ChoiceLabels[key])).ToArray());

    /// <summary>分组页：按 (Page, Section) 去重，保持表内顺序。</summary>
    private static IReadOnlyList<ToolSectionDeclaration> Sections() => GrayMatchFields.All
        .GroupBy(field => (field.Page, field.Section))
        .Select(group => new ToolSectionDeclaration(group.Key.Page, group.Key.Section,
            // 原厂 Tab_Check Result 标 SubscribeVisible="False"：判定分组不提供订阅入口。
            allowReference: !string.Equals(group.Key.Section, GrayMatchFields.JudgeSection,
                StringComparison.Ordinal)))
        .ToArray();

    /// <summary>联动显隐：枚举控制器选到 <c>Manual</c>，布尔控制器选到真。</summary>
    private static IReadOnlyList<FieldVisibilityRule> Visibility() => GrayMatchFields.All
        .Where(field => field.VisibleWhen is not null)
        .Select(field =>
        {
            var controller = GrayMatchFields.Spec(field.VisibleWhen!);
            return new FieldVisibilityRule(controller.Id,
                new[] { controller.Type.IsEnum ? "Manual" : "true" }, new[] { field.Id });
        })
        .ToArray();

    private static IReadOnlyList<ToolPairGroupDeclaration> Pairs() => GrayMatchFields.All
        .Where(field => field.PairWith is not null)
        .Select(field => new ToolPairGroupDeclaration(
            "pair:" + field.Id, field.Id, GrayMatchFields.Spec(field.PairWith!).Id,
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
        GrayMatchTool.Id, "灰度匹配",
        "按学习灰度模板在搜索区域内查找灰度相似的目标，输出匹配点、角度、分数与匹配框",
        "gray-match", "定位", 35,
        GrayMatchFields.All.Select(Parameter).ToArray(),
        new ToolResultDeclaration(GrayMatchSchema.Rows,
            new[]
            {
                // 行表形态的行成员决定行记录类型：必须与原 RowsType 逐项一致。
                new ToolResultField("element-id", "序号", DataType.Integer),
                new ToolResultField("match-point", "匹配点", GrayMatchSchema.PointType),
                new ToolResultField("center-x", "匹配点X", DataType.Number),
                new ToolResultField("center-y", "匹配点Y", DataType.Number),
                new ToolResultField("angle-degrees", "角度", DataType.Number),
                new ToolResultField("score", "匹配分数", DataType.Number),
                new ToolResultField("box", "匹配框", GrayMatchSchema.BoxType, children: BoxChildren()),
                new ToolResultField("region", "区域", TypeRef.ResourceOf(ResourceKind.Region)),
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
            selectedRectangleColor: new ResultDisplayColor(0, 0, 255),
            shape: ToolResultShape.Rows),
        new ToolDisplayDeclaration(new[]
        {
            // 原厂 Display.xml：匹配结果/匹配位置/检测区域/基准点/运行点 + 文本。
            new ToolDisplayItem(ToolDisplayRole.Graphic, "匹配结果", groupKey: "match",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            new ToolDisplayItem(ToolDisplayRole.Centroid, "匹配位置", groupKey: "location",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0), width: 1),
            new ToolDisplayItem(ToolDisplayRole.Inspection, "检测区域", groupKey: "inspection",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            new ToolDisplayItem(ToolDisplayRole.ReferencePoint, "基准点", groupKey: "reference-point",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0), width: 1),
            new ToolDisplayItem(ToolDisplayRole.RunPoint, "运行点", groupKey: "run-point",
                ok: new ResultDisplayColor(255, 0, 0), ng: new ResultDisplayColor(255, 0, 0), width: 1),
            new ToolDisplayItem(ToolDisplayRole.Text, "文本", groupKey: "text",
                textContent: "匹配分数:{分数}")
        }),
        // 参数页没有独立输入行（由 ROI 组与 Assist 组承载），执行 Schema 仍声明三个输入端口。
        ToolInputTemplate.Image,
        extraInputs: new[]
        {
            new Port(GrayMatchSchema.RoiInput, TypeRef.ResourceOf(ResourceKind.Region),
                Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
            new Port(GrayMatchSchema.FixtureInput, FixtureDeclarations.Info,
                Required: false, Nullable: true,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference)
        },
        nodeDisplayType: "DEFAULT",
        sections: Sections(),
        visibility: Visibility(),
        pairs: Pairs(),
        extraOutputs: new[]
        {
            new Port(GrayMatchSchema.RunTime, DataType.Number, Storage: NumericStorage.Float32),
            new Port(GrayMatchSchema.Status, DataType.Integer, Storage: NumericStorage.Int32),
            new Port(GrayMatchSchema.Count, DataType.Integer, Storage: NumericStorage.Int32),
            new Port(GrayMatchSchema.Scores, DataType.NumberArray, Storage: NumericStorage.Float32),
            new Port(GrayMatchSchema.MatchPoints, TypeRef.SequenceOf(GrayMatchSchema.PointType)),
            new Port(GrayMatchSchema.Angles, DataType.NumberArray, Storage: NumericStorage.Float32),
            new Port(GrayMatchSchema.Boxes, TypeRef.SequenceOf(GrayMatchSchema.BoxType)),
            new Port(GrayMatchSchema.OutputFixture, FixtureDeclarations.Info, Nullable: true)
        },
        manualReferenceSources: new[] { GrayMatchSchema.MatchPoints, GrayMatchSchema.Scores });
}
