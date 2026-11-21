using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 模板训练窗口的样本区域编辑器。复用平台已验证的 NativeRoiEditHost + RoiDocumentStore +
/// NativeRoiUiDraftControls（与 Blob/PixelCount 同一体系），只在工具层装配最小输入视图：
/// 图像字段呈现、样本文档字段声明但不呈现、本地绘制模式。绘制/列表/可见/删除/缩放/撤销重做
/// 全部由平台宿主提供，工具层不另写一套 ROI 交互。
/// 样本文档用 inspection 组=正样本、mask 组=背景，与 ContourMatchTrainingRois 训练域一致。
/// </summary>
internal sealed class ContourMatchTrainingRoiEditor : IDisposable
{
    private const string ImageFieldId = "template.image";
    private const string ModeFieldId = "template.roi-mode";
    private const string LocalMode = "Local";
    private const string RectangleMode = "RectangleArea";
    private const string AnnulusMode = "AnnulusArea";
    private const string RectangleSource = "template.rect-source";
    private const string AnnulusSource = "template.annulus-source";
    private const string AreaTypeFieldId = "template.area-type";
    private const string MaskImageFieldId = "template.mask-image";
    private const string OutputMaskFieldId = "template.output-mask";
    private const string MaskSourceFieldId = "template.mask-source";
    private const string MaskInheritedFieldId = "template.mask-inherited";

    private readonly DeclaredInputFieldsView _inputs;
    private readonly NativeRoiUiDraftControls _controls;
    private readonly RoiDocumentStore _store;
    private readonly NativeRoiEditHost _host;
    private readonly FieldSettingsDraft _draft;
    private bool _disposed;

    public ContourMatchTrainingRoiEditor(RecordValue initialDocument, NativeRoiFrameSource? frames,
        Func<string?>? drawBlockReason, IReadOnlyList<string>? availableShapes,
        Action<FrameworkElement> attachEditor, Action<NativeRoiUiDraftControls>? observe = null)
    {
        RoiDocumentValue.Validate(initialDocument);
        var values = new Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
        {
            [ContourMatchTrainingRois.InputId] = new ConstantSetting(initialDocument),
            [ModeFieldId] = new ConstantSetting(LocalMode),
            [AreaTypeFieldId] = new ConstantSetting(0L)
        };
        var schema = BuildSchema();
        _draft = new FieldSettingsDraft(schema, new FieldSettings(schema.Inputs, values));
        _inputs = new DeclaredInputFieldsView(schema,
            port => _draft.CaptureFields().Read(port.Id),
            (port, setting) => _draft.ApplyFields(new Dictionary<string, ParameterSetting> { [port.Id] = setting }, _ => { }),
            ReferenceFieldOptions.Empty,
            _ => "");

        _controls = NativeRoiGroupComposer.Compose(_inputs, new RoiGroupComposerSpec
        {
            ModeFieldId = ModeFieldId,
            LocalModeValue = LocalMode,
            InheritedModes = new[] { RectangleMode, AnnulusMode },
            RectangleSourceFieldId = RectangleSource,
            AnnulusSourceFieldId = AnnulusSource,
            RectangleParameterRows = Members("center-x", "center-y", "width", "height", "angle-degrees"),
            AnnulusParameterRows = Members("center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees"),
            AreaTypeFieldId = AreaTypeFieldId,
            AreaTypeLabel = "区域类型",
            AreaTypeChoices = new[] { new NativeIntegerRadioChoice(0, "图形"), new NativeIntegerRadioChoice(1, "图像") },
            MaskImageFieldId = MaskImageFieldId,
            MaskImageLabel = "输入掩膜",
            OutputMaskFieldId = OutputMaskFieldId,
            OutputMaskLabel = "输出掩膜",
            MaskSourceFieldId = MaskSourceFieldId,
            MaskSourceLabel = "屏蔽区",
            MaskInheritedFieldId = MaskInheritedFieldId,
            AttachAnchorFieldId = ImageFieldId,
            AttachSection = "样本区域",
            ShowAreaType = false,
            ShowMaskImage = false,
            ShowOutputMask = false,
            ShowAnnulusSource = false,
            ShowMaskGroup = true
        }, (_, element, _) => attachEditor(element), observe);

        _store = new RoiDocumentStore(_inputs, ContourMatchTrainingRois.InputId, Groups, ImageFieldId,
            Inheritance, MaskInheritance, new[] { "正样本", "背景样本" });
        _host = new NativeRoiEditHost(_inputs, _controls, frames, _store, drawBlockReason, availableShapes);
    }

    /// <summary>当前宿主是否已捕获到有效的样本文档。</summary>
    public bool TryCapture(out RecordValue document, out string error)
    {
        if (!_host.TryCapture(out error)) { document = RoiDocumentValue.Empty; return false; }
        var setting = _inputs.ReadUnpresentedInput(ContourMatchTrainingRois.InputId);
        if (setting is not ConstantSetting { Value: RecordValue value }) { error = "样本文档无效。"; document = RoiDocumentValue.Empty; return false; }
        try { ContourMatchTrainingRois.Validate(value); }
        catch (Exception failure) { error = failure.Message; document = RoiDocumentValue.Empty; return false; }
        document = value;
        error = "";
        return true;
    }

    public bool HasPending => _host.HasPending;

    private static IReadOnlyList<RoiParameterRow> Members(params string[] members)
        => members.Select(member => new RoiParameterRow(member, member)).ToArray();

    private static ToolSchema BuildSchema()
    {
        var ports = new List<Port>
        {
            new(ImageFieldId, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
            new(ContourMatchTrainingRois.InputId, RoiDocumentValue.Declaration, EditModes: FieldEditModes.Constant),
            new(ModeFieldId, DataType.Text, EditModes: FieldEditModes.Constant),
            new(AreaTypeFieldId, DataType.Integer, EditModes: FieldEditModes.Constant),
            new(RectangleSource, TypeRef.SequenceOf(RoiDocumentValue.ShapeType), Required: false, EditModes: FieldEditModes.Reference),
            new(AnnulusSource, TypeRef.SequenceOf(RoiDocumentValue.ShapeType), Required: false, EditModes: FieldEditModes.Reference),
            new(MaskImageFieldId, TypeRef.ResourceOf(ResourceKind.Image), Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
            new(OutputMaskFieldId, DataType.Boolean, Required: false, EditModes: FieldEditModes.Constant),
            new(MaskSourceFieldId, TypeRef.SequenceOf(RoiDocumentValue.PointType), Required: false, EditModes: FieldEditModes.Reference),
            new(MaskInheritedFieldId, DataType.Boolean, Required: false, EditModes: FieldEditModes.Constant)
        };
        foreach (var member in new[] { "center-x", "center-y", "width", "height", "angle-degrees" })
            ports.Add(new Port(RectangleSource + "." + member, DataType.NumberArray, Required: false, EditModes: FieldEditModes.Reference));
        foreach (var member in new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" })
            ports.Add(new Port(AnnulusSource + "." + member, DataType.NumberArray, Required: false, EditModes: FieldEditModes.Reference));

        var presentations = new List<FieldPresentation>
        {
            new(ImageFieldId, "输入图像", "样本区域", 0, FieldEditorKind.Image)
            { Layout = new FieldLayout("样本区域", "样本区域", AllowReference: true) }
        };
        return new ToolSchema(ports, Array.Empty<Port>(), new ToolPresentation(presentations), ImageFieldId);
    }

    private static IReadOnlyList<NativeRoiEditGroup> Groups { get; } = Array.AsReadOnly(new[]
    {
        new NativeRoiEditGroup(RoiDocumentValue.Inspection, "正样本", false, ContourMatchTrainingRois.MaximumPositiveCount, RoiDocumentValue.FullDomain),
        new NativeRoiEditGroup(RoiDocumentValue.Mask, "背景样本", true, ContourMatchTrainingRois.MaximumBackgroundCount, RoiDocumentValue.None)
    });

    private static RoiInheritanceSpec Inheritance { get; } = new(
        ModeFieldId, LocalMode,
        new(RectangleMode, "RectangleParameters", RectangleSource,
            new[] { "center-x", "center-y", "width", "height", "angle-degrees" }, "rectangle"),
        new(AnnulusMode, "AnnulusParameters", AnnulusSource,
            new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" }, "annulus"),
        RoiDocumentValue.MaximumInheritedCount);

    private static MaskInheritanceSpec MaskInheritance { get; } = new(
        MaskInheritedFieldId, MaskSourceFieldId, RoiDocumentValue.MaximumInheritedCount);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _host.Dispose();
        _controls.Dispose();
        _inputs.Dispose();
    }
}
