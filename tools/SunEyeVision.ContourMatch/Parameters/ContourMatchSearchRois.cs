using System;
using System.Collections.Generic;
using HalconDotNet;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 轮廓匹配「运行时搜索区域」的本地绘制支路（对齐原厂 IMVSContourMatchModuAlgorithmTab.xml:9-35
/// Tab_ROI Area 的 RoiSelectGroup/InheritWayGroup，与模块输入 InputROI xml:39 共存）。
///
/// 与原厂一致，搜索区域有两条来源，按模式二选一：
/// - Inherited（默认）：使用上游输入端口 ContourMatchSchema.RoiInput（对应原厂 InputROI）。
/// - Local：使用本模块字段 input.roi.local 的 ROI 文档（对应原厂本地绘制 RoiSelecter）。
///
/// 这是**运行时搜索域**，与模板训练样本 ContourMatchTrainingRois（建模用）是两条独立链路，不得混用。
/// 存量兼容：两字段 Required=false，模式默认 Inherited（UseDeclaredDefault，缺项时加载器补默认），
/// 因此只连上游 ROI 的旧方案行为完全不变。
/// </summary>
public static class ContourMatchSearchRois
{
    public const string LocalId = "input.roi.local";
    public const string ModeId = "input.roi.search-source";
    public const string Inherited = "Upstream";
    public const string Local = "Local";
    public const int MaximumLocalInspectionCount = 16;
    public const int MaximumLocalMaskCount = 8;

    public static Port LocalPort { get; } = new(LocalId, RoiDocumentValue.Declaration,
        Required: false, EditModes: FieldEditModes.Constant);

    public static Port ModePort { get; } = new(ModeId, TypeRef.ScalarOf(ScalarKind.Text),
        Required: false, EditModes: FieldEditModes.Constant,
        DefaultPresence: FieldDefaultPresence.UseDeclaredDefault,
        Choices: new(new[] { Inherited, Local }));

    // 继承模式（按矩形/圆环来源）的输入字段；NativeRoiGroupComposer 按声明建行，必须全部在 schema 中。
    private const string RectangleSource = "search.rect-source";
    private const string AnnulusSource = "search.annulus-source";
    private const string MaskSource = "search.mask-source";
    private const string MaskInherited = "search.mask-inherited";
    private static TypeRef ShapeSequence => TypeRef.SequenceOf(RoiDocumentValue.ShapeType);
    private static TypeRef PointSequence => TypeRef.SequenceOf(RoiDocumentValue.PointType);

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        LocalPort, ModePort,
        new Port(RectangleSource, ShapeSequence, Required: false, EditModes: FieldEditModes.Reference),
        new Port(AnnulusSource, ShapeSequence, Required: false, EditModes: FieldEditModes.Reference),
        new Port(MaskSource, PointSequence, Required: false, EditModes: FieldEditModes.Reference),
        new Port(MaskInherited, DataType.Boolean, Required: false, EditModes: FieldEditModes.Constant)
    });

    public static object ModeDefault => Inherited;

    /// <summary>从配置读取搜索区域来源模式；缺项/未设置为 Inherited，保持存量方案行为。</summary>
    public static string ReadMode(FieldSettings fields)
        => fields.Declarations.ContainsKey(ModeId)
            ? fields.Read(ModeId) switch
            {
                ConstantSetting { Value: string mode } => ValidateMode(mode),
                UnsetSetting => Inherited,
                _ => throw new ArgumentException("搜索区域来源必须是固定模式。")
            }
            : Inherited;

    private static string ValidateMode(string mode)
        => mode is Inherited or Local ? mode : throw new ArgumentException("未知的搜索区域来源：" + mode);

    /// <summary>本地 ROI 文档容量校验（结构校验由 RoiDocumentValue 负责）。</summary>
    public static void Validate(RecordValue root)
    {
        RoiDocumentValue.Validate(root);
        var inspection = RoiDocumentValue.Items(root, RoiDocumentValue.Inspection).Count;
        var mask = RoiDocumentValue.Items(root, RoiDocumentValue.Mask).Count;
        if (inspection > MaximumLocalInspectionCount)
            throw new ArgumentException($"本地搜索区域最多 {MaximumLocalInspectionCount} 个。");
        if (mask > MaximumLocalMaskCount)
            throw new ArgumentException($"本地屏蔽区域最多 {MaximumLocalMaskCount} 个。");
    }

    /// <summary>
    /// 解析运行时搜索域：Local 模式把本地 ROI 文档栅格化为 HRegion；Inherited 模式返回 null，
    /// 由调用方回退到上游 ContourMatchSchema.RoiInput。空本地文档（FullDomain/None）视为未配置，
    /// 直接报错，不静默放大到全图（rule-015）。
    /// </summary>
    public static HRegion? ResolveLocal(InputValues values, int imageWidth, int imageHeight)
    {
        if (ReadMode(values) != Local) return null;
        if (!values.Contains(LocalId))
            throw new ContourMatchCapabilityException("CONTOURMATCH_LOCAL_ROI_REQUIRED", "本地搜索区域尚未绘制。");
        var document = values.Get<RecordValue?>(LocalId)
            ?? throw new ContourMatchCapabilityException("CONTOURMATCH_LOCAL_ROI_REQUIRED", "本地搜索区域尚未绘制。");
        Validate(document);
        var region = ContourMatchTrainingRois.BuildDomain(document, imageWidth, imageHeight)
            ?? throw new ContourMatchCapabilityException("CONTOURMATCH_LOCAL_ROI_REQUIRED", "本地搜索区域没有有效图形，请先绘制。");
        return region;
    }

    private static string ReadMode(InputValues values)
        => values.Contains(ModeId)
            ? values.Get<object?>(ModeId) switch
            {
                string mode => ValidateMode(mode),
                null => Inherited,
                _ => throw new ArgumentException("搜索区域来源必须是固定模式。")
            }
            : Inherited;
}
