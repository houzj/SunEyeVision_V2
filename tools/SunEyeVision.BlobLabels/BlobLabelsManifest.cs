using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobLabels.Runtime;

/// <summary>
/// Blob 标签的声明唯一真源（原件 <c>IMVSBlobFindLabelsModuAlgorithm.xml</c> +
/// <c>AlgorithmTab.xml</c> + <c>Display.xml</c>）。
/// 参数由 <see cref="BlobLabelsFields"/> 的表生成，结果与显示按原厂分量声明。
/// </summary>
internal static class BlobLabelsManifest
{
    public static ToolManifest Value { get; } = Build();

    private static ToolParameterDeclaration Parameter(BlobLabelsFieldSpec field) => new(
        field.Id, field.Label, field.Section, BlobLabelsFields.PortType(field), field.Default,
        minimum: field.Minimum, maximum: field.Maximum,
        step: field.Increment,
        decimals: field.Type == typeof(string) ? 0 : field.DecimalPlaces,
        storage: field.Type == typeof(int) ? NumericStorage.Int32 : null,
        modes: FieldEditModes.Constant
            | (field.AllowReference ? FieldEditModes.Reference : FieldEditModes.None),
        propertyName: field.Name,
        displayType: BlobLabelsFields.DisplayTypeOf(field),
        advanced: field.Advanced,
        choices: field.Choices.Count == 0
            ? null
            : field.Choices.Select(key => new ToolChoice(key, field.ChoiceLabels[key])).ToArray());

    private static IReadOnlyList<ToolSectionDeclaration> Sections() => BlobLabelsFields.All
        // 原厂 ResultShow 判定组在本方三页结构里归「运行参数」页；
        // 先按归一化后的页分组，否则同一判定组会因字段自带页不同而重复声明。
        .Select(field => (
            Page: string.Equals(field.Section, BlobLabelsFields.JudgeSection, StringComparison.Ordinal)
                ? BlobLabelsFields.RunPage
                : field.Page,
            field.Section))
        .Distinct()
        .Select(group => new ToolSectionDeclaration(group.Page, group.Section,
            // 证据 IMVSBlobFindLabelsModuAlgorithmTab.xml：Tab_ROI Area / Tab_Run Params /
            // Tab_Check Result 三个 Category 标 CanTriggerRun="TRUE"；ClassInfo 没有该标记。
            canTriggerRun: !string.Equals(group.Section, BlobLabelsFields.ClassSection,
                StringComparison.Ordinal),
            // Tab_Check Result 另标 SubscribeVisible="False"：判定区不提供订阅来源入口。
            allowReference: !string.Equals(group.Section, BlobLabelsFields.JudgeSection,
                StringComparison.Ordinal)))
        .ToArray();

    /// <summary>联动显隐：VisibleNegate 表示控制器取反时才显示。</summary>
    private static IReadOnlyList<FieldVisibilityRule> Visibility() => BlobLabelsFields.All
        .Where(field => field.VisibleWhen is not null)
        .Select(field =>
        {
            var controller = BlobLabelsFields.Spec(field.VisibleWhen!);
            var value = controller.Type.IsEnum ? "Manual" : (!field.VisibleNegate).ToString();
            return new FieldVisibilityRule(controller.Id, new[] { value }, new[] { field.Id });
        })
        .ToArray();

    private static IReadOnlyList<ToolPairGroupDeclaration> Pairs() => BlobLabelsFields.All
        .Where(field => field.PairWith is not null)
        .Select(field => new ToolPairGroupDeclaration(
            "pair:" + field.Id, field.Id, BlobLabelsFields.Spec(field.PairWith!).Id,
            label: field.PairLabel ?? field.Label))
        .ToArray();

    private static IReadOnlyList<ToolResultField> BoundsChildren() => new[]
    {
        new ToolResultField("origin", "原点", FixtureDeclarations.Point),
        new ToolResultField("width", "宽度", DataType.Number),
        new ToolResultField("height", "高度", DataType.Number)
    };

    private static IReadOnlyList<ToolResultField> RectangleChildren() => new[]
    {
        new ToolResultField("center", "矩形中心", FixtureDeclarations.Point),
        new ToolResultField("width", "矩形宽度", DataType.Number),
        new ToolResultField("height", "矩形高度", DataType.Number),
        new ToolResultField("angle-degrees", "矩形角度", DataType.Number)
    };

    private static ToolManifest Build() => new(
        BlobLabelsTool.Id, "Blob标签",
        "按灰度阈值分割连通域，输出每个 Blob 的面积、周长、质心、形状特征与分类标签",
        "blob-labels", "定位", 40,
        BlobLabelsFields.All.Select(Parameter).ToArray(),
        new ToolResultDeclaration(BlobLabelsSchema.Rows,
            new[]
            {
                // 行表形态的行成员决定行记录类型：与原 RowsType 的 17 个成员逐项一致。
                new ToolResultField("element-id", "序号", DataType.Integer),
                new ToolResultField("label", "标签", DataType.Integer),
                new ToolResultField("area", "面积", DataType.Number),
                new ToolResultField("perimeter", "周长", DataType.Number),
                new ToolResultField("centroid-x", "质心X", DataType.Number),
                new ToolResultField("centroid-y", "质心Y", DataType.Number),
                new ToolResultField("centroid", "质心", BlobLabelsSchema.PointType),
                new ToolResultField("circularity", "圆形度", DataType.Number),
                new ToolResultField("rectangularity", "矩形度", DataType.Number),
                new ToolResultField("bounds", "外接框", BlobLabelsSchema.BoundsType,
                    children: BoundsChildren()),
                new ToolResultField("oriented-bounds", "最小外接矩形", BlobLabelsSchema.RectangleType,
                    children: RectangleChildren()),
                new ToolResultField("long-axis", "长轴", DataType.Number),
                new ToolResultField("short-axis", "短轴", DataType.Number),
                new ToolResultField("contour", "轮廓", TypeRef.SequenceOf(BlobLabelsSchema.PointType),
                    required: false),
                new ToolResultField("region", "区域", TypeRef.ResourceOf(ResourceKind.Region)),
                new ToolResultField("judgment-passed", "判定", DataType.Boolean),
                new ToolResultField("failed-limits", "未通过判据", TypeRef.SequenceOf(DataType.Text),
                    required: false)
            },
            slots: new ToolResultSlots("element-id", "oriented-bounds", "region", "area",
                "judgment-passed", centroid: "centroid"),
            columns: new[]
            {
                new ToolResultColumn("element-id", "序号", 0, key: "element-id"),
                new ToolResultColumn("area", "面积", 2, key: "area",
                    geometryMember: "centroid", geometrySourceMember: "region"),
                new ToolResultColumn("perimeter", "周长", 2, key: "perimeter",
                    geometryMember: "centroid", geometrySourceMember: "region"),
                new ToolResultColumn("centroid-x", "质心X", 2, key: "centroid-x",
                    geometryMember: "centroid", geometrySourceMember: "region"),
                new ToolResultColumn("centroid-y", "质心Y", 2, key: "centroid-y",
                    geometryMember: "centroid", geometrySourceMember: "region"),
                new ToolResultColumn("circularity", "圆形度", 2, key: "circularity",
                    geometryMember: "centroid", geometrySourceMember: "region"),
                new ToolResultColumn("rectangularity", "矩形度", 2, key: "rectangularity",
                    geometryMember: "centroid", geometrySourceMember: "region"),
                new ToolResultColumn("long-axis", "长轴", 2, key: "long-axis",
                    geometryMember: "centroid", geometrySourceMember: "region"),
                new ToolResultColumn("short-axis", "短轴", 2, key: "short-axis",
                    geometryMember: "centroid", geometrySourceMember: "region"),
                // 嵌套列：角度取自最小外接矩形。
                new ToolResultColumn("oriented-bounds", "Box角度", 2, key: "angle",
                    geometryMember: "centroid", geometrySourceMember: "region",
                    memberPath: new[] { "oriented-bounds", "angle-degrees" }),
                new ToolResultColumn("judgment-passed", "判定", key: "judgment")
            },
            kind: ResultProjectionKind.StandardTable,
            selectedRectangleColor: new ResultDisplayColor(0, 0, 255),
            shape: ToolResultShape.Rows),
        new ToolDisplayDeclaration(new[]
        {
            // 原厂 Display.xml：Blob/检测区域/轮廓点/基准点/运行点/屏蔽区多边形/扇环检测区域 + 文本。
            new ToolDisplayItem(ToolDisplayRole.Graphic, "Blob", groupKey: "blob",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            new ToolDisplayItem(ToolDisplayRole.Inspection, "检测区域", groupKey: "inspection",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            // 轮廓点是系统类型：只有可见/OK/NG 三色，不产图形对话框分组。
            new ToolDisplayItem(ToolDisplayRole.Outline, "轮廓点", groupKey: "outline",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0),
                styleDialog: false),
            new ToolDisplayItem(ToolDisplayRole.ReferencePoint, "基准点", groupKey: "reference-point",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0), width: 1),
            new ToolDisplayItem(ToolDisplayRole.RunPoint, "运行点", groupKey: "run-point",
                ok: new ResultDisplayColor(255, 0, 0), ng: new ResultDisplayColor(255, 0, 0), width: 1),
            new ToolDisplayItem(ToolDisplayRole.Mask, "屏蔽区多边形", groupKey: "mask",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0),
                width: 2, opacity: 0.5),
            new ToolDisplayItem(ToolDisplayRole.Annulus, "扇环检测区域", groupKey: "annulus",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            new ToolDisplayItem(ToolDisplayRole.Text, "文本", groupKey: "text",
                textContent: "Blob面积:{面积}")
        }),
        ToolInputTemplate.Image,
        extraInputs: new[]
        {
            // 原厂 InputMaskImageGroup / RoiSelectGroup 的端口：掩膜、矩形 ROI、扇环 ROI、屏蔽区。
            new Port(BlobLabelsSchema.MaskInput, TypeRef.ResourceOf(ResourceKind.Image),
                Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
            new Port(BlobLabelsSchema.InspectionRoiInput, TypeRef.ResourceOf(ResourceKind.Region),
                Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
            new Port(BlobLabelsSchema.AnnulusRoiInput, BlobLabelsRoi.AnnulusType,
                Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
            new Port(BlobLabelsSchema.MaskRegionInput, TypeRef.ResourceOf(ResourceKind.Region),
                Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
            new Port(BlobLabelsSchema.FixtureInput, FixtureDeclarations.Info,
                Required: false, Nullable: true,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference)
        }.Concat(BlobLabelsRoi.Ports).Concat(BlobLabelsAssist.Ports).ToArray(),
        // 原厂 Basic 页以 ImageSourceGroup 提供图像来源行；ROI/掩膜/辅助组由 BlobLabelsRoi 组合，
        // 但 ROI 编辑宿主需要 image 这一行存在才能取到帧来源字段。
        inputPresentations: new[]
        {
            new ToolInputPresentation(BlobLabelsSchema.ImageInput, "输入源", "基本参数", "图像输入")
        },
        nodeDisplayType: "DEFAULT",
        sections: Sections(),
        visibility: Visibility(),
        pairs: Pairs(),
        extraOutputs: new[]
        {
            new Port(BlobLabelsSchema.RunTime, DataType.Number, Storage: NumericStorage.Float32),
            new Port(BlobLabelsSchema.Status, DataType.Integer, Storage: NumericStorage.Int32),
            new Port(BlobLabelsSchema.BlobStatuses, TypeRef.SequenceOf(DataType.Integer),
                Storage: NumericStorage.Int32),
            new Port(BlobLabelsSchema.Count, DataType.Integer, Storage: NumericStorage.Int32),
            new Port(BlobLabelsSchema.Areas, DataType.NumberArray, Storage: NumericStorage.Float32),
            new Port(BlobLabelsSchema.TotalArea, DataType.Number, Storage: NumericStorage.Float32),
            new Port(BlobLabelsSchema.Centroids, TypeRef.SequenceOf(BlobLabelsSchema.PointType)),
            new Port(BlobLabelsSchema.MinimumBoundingRectangles,
                TypeRef.SequenceOf(BlobLabelsSchema.RectangleType)),
            new Port(BlobLabelsSchema.BlobAngles, DataType.NumberArray, Storage: NumericStorage.Float32),
            new Port(BlobLabelsSchema.Perimeters, DataType.NumberArray, Storage: NumericStorage.Float32),
            new Port(BlobLabelsSchema.LongAxes, DataType.NumberArray, Storage: NumericStorage.Float32),
            new Port(BlobLabelsSchema.ShortAxes, DataType.NumberArray, Storage: NumericStorage.Float32),
            new Port(BlobLabelsSchema.Circularities, DataType.NumberArray, Storage: NumericStorage.Float32),
            new Port(BlobLabelsSchema.Rectangularities, DataType.NumberArray, Storage: NumericStorage.Float32),
            new Port(BlobLabelsSchema.Scores, TypeRef.SequenceOf(DataType.Number), Nullable: true,
                Storage: NumericStorage.Float32),
            new Port(BlobLabelsSchema.BinaryImage, BlobLabelsSchema.BinaryImageType, Nullable: true),
            new Port(BlobLabelsSchema.BlobImage, BlobLabelsSchema.BlobImageType, Nullable: true),
            new Port(BlobLabelsSchema.OutputMask, BlobLabelsSchema.BinaryImageType, Nullable: true),
            new Port(BlobLabelsSchema.InspectionRectangles,
                TypeRef.SequenceOf(BlobLabelsSchema.RectangleType), Nullable: true),
            new Port(BlobLabelsSchema.InspectionAnnuli,
                TypeRef.SequenceOf(BlobLabelsSchema.AnnulusType), Nullable: true),
            new Port(BlobLabelsSchema.ContourPoints, BlobLabelsSchema.ContourInfoType, Nullable: true),
            new Port(BlobLabelsSchema.ClassLabels, TypeRef.SequenceOf(DataType.Text), Nullable: true),
            new Port(BlobLabelsSchema.LabelValues, TypeRef.SequenceOf(DataType.Integer), Nullable: true),
            new Port(BlobLabelsSchema.BlindPolygon, TypeRef.SequenceOf(BlobLabelsSchema.PointType),
                Nullable: true),
            new Port(BlobLabelsSchema.BlindPolygonString, TypeRef.SequenceOf(DataType.Text), Nullable: true),
            new Port(BlobLabelsSchema.OutputFixture, FixtureDeclarations.Info, Nullable: true)
        },
        manualReferenceSources: new[]
        {
            BlobLabelsSchema.Count, BlobLabelsSchema.TotalArea,
            BlobLabelsSchema.Areas, BlobLabelsSchema.Perimeters
        },
        // 证据 IMVSBlobFindLabelsModuAlgorithmTab.xml 的 Trigger #3 / #4：
        //   LabelWiseAreaSelectEnable=True → ThresholdAreaList 显示 + SelectByArea 禁用
        //   SelectByArea=True             → RangArea 显示         + LabelWiseAreaSelectEnable 禁用
        // 两者互斥但不隐藏，因此是「禁用规则」而不是显隐规则。
        enableRules: new[]
        {
            new FieldEnableRule(BlobLabelsFields.LabelWiseAreaSelectEnabled, new[] { "true" },
                new[] { BlobLabelsFields.IdOf(nameof(BlobLabelsSettings.AreaFilterEnabled)) }),
            new FieldEnableRule(BlobLabelsFields.IdOf(nameof(BlobLabelsSettings.AreaFilterEnabled)),
                new[] { "true" }, new[] { BlobLabelsFields.LabelWiseAreaSelectEnabled })
        });
}
