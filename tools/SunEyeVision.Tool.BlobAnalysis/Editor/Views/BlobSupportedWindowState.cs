using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>Read-only capability projection over the real draft; never stores settings or runtime values.</summary>
internal sealed class BlobSupportedWindowState : IDisposable
{
    private readonly IToolSettingsDraft _draft;
    private readonly DeclaredInputFieldsView _inputs;
    private readonly NativeAssistUiDraftControls? _assist;
    private bool _disposed, _refreshing, _correctionEnabled;
    internal BlobSupportedWindowState(IToolSettingsDraft draft,DeclaredInputFieldsView inputs,NativeAssistUiDraftControls? assist)
    {
        _draft=draft;_inputs=inputs;_assist=assist;
        _correctionEnabled = draft.CaptureFields().Read(Id(nameof(BlobSettings.IsPositionCorrectionEnabled))) is ConstantSetting { Value:true };
        foreach(var field in inputs.Fields)field.Changed+=Refresh;
        Refresh();
    }
    private static string Id(string property)=>BlobParameterDefinition.Instance.SettingsFieldId(property);
    internal string? Validate()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if (_assist is { Visibility: Visibility.Visible } assist && assist.CommitActiveSources() is {} sourceError)
            return sourceError;
        return ValidateInputs(_inputs, _draft);
    }
    internal static string? ValidateInputs(DeclaredInputFieldsView inputs, IToolSettingsDraft draft)
    {
        if (BlobImageRoi.IsImageMode(inputs.ReadUnpresentedInput(BlobImageRoi.ModeId)))
        {
            var mask = inputs.Fields.Single(field => field.FieldId == BlobImageRoi.ImageId);
            if (mask.Setting is UnsetSetting) return "请选择屏蔽图像。";
            return mask.TryCommitPending() ? null : mask.Error;
        }
        var roiValues=new Dictionary<string,ParameterSetting>(StringComparer.Ordinal);
        foreach(var port in BlobRoiInheritance.Ports)
        {
            var setting=port.Id==BlobRoiInheritance.ModeId
                ?inputs.ReadUnpresentedInput(port.Id)
                :inputs.Fields.FirstOrDefault(field=>field.FieldId==port.Id)?.Setting??new UnsetSetting();
            if(setting is not UnsetSetting)roiValues.Add(port.Id,setting);
        }
        var roiDiagnostics=BlobRoiInheritance.ValidateConfiguration(new FieldSettings(BlobRoiInheritance.Ports,roiValues));
        if(roiDiagnostics.Count!=0)return roiDiagnostics[0].Message;
        if (BlobMaskInheritance.IsInherited(inputs.ReadUnpresentedInput(BlobMaskInheritance.ModeId)))
        {
            var mask=inputs.Fields.Single(field=>field.FieldId==BlobMaskInheritance.SourceId);
            if (mask.Setting is UnsetSetting) return "请选择继承屏蔽区来源。";
            if (!mask.TryCommitPending()) return mask.Error;
        }
        var correction = draft.CaptureFields().Read(Id(nameof(BlobSettings.IsPositionCorrectionEnabled))) is ConstantSetting { Value:true };
        if (correction && BlobFixtureInputs.LeafIds.Any(id => inputs.ReadInputProjection(id) is UnsetSetting))
            return "请选择完整的位置修正来源。";
        return null;
    }
    // These guards must also run before the lazy Basic page has created its input-owned state.
    internal static string? ValidateSettings(IToolSettingsDraft draft)
    {
        var values=draft.CaptureFields();
        if(!BlobThresholdCapabilities.IsSupported((values.Read(Id(nameof(BlobSettings.SegmentationMode))) as ConstantSetting)?.Value as string))
            return "当前阈值模式尚未就绪；原设置保留，不能确认或执行。";
        // Result judgments and boundary output are formal settings consumed by
        // BlobAlgorithm. Their enabled state is not an unsupported UI capability.
        return null;
    }
    internal void Refresh()
    {
        if (_disposed || _refreshing) return;
        _refreshing = true;
        try
        {
            var enabled = _draft.CaptureFields().Read(Id(nameof(BlobSettings.IsPositionCorrectionEnabled))) is ConstantSetting { Value:true };
            if (_correctionEnabled && !enabled)
            {
                var pose = _assist?.Children.OfType<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldRow>()
                    .SingleOrDefault(field => field.FieldId == BlobTool.PoseInput);
                if (pose is not null && !pose.TryClear())
                    throw new InvalidOperationException("位置修正来源清理未被参数草稿接纳。");
            }
            _correctionEnabled = enabled;
            ProjectSources(_draft,_inputs,()=>!_disposed);
        }
        finally { _refreshing = false; }
    }
    internal void RefreshAcceptedInputs(NativeRoiUiDraftControls roi)
    {
        if (_disposed) return;
        roi.RefreshAcceptedModes(()=>
        {
            _inputs.RefreshInputProjection(BlobTool.ImageInput);
            _inputs.RefreshInputProjection(BlobImageRoi.OutputEnabledId,new ConstantSetting(false));
            _inputs.RefreshInputProjection(BlobImageRoi.ImageId);
        });
        Refresh();
    }
    // Refresh the shared sources without forcing the lazy Basic input page into existence.
    internal static void ProjectSources(IToolSettingsDraft draft,DeclaredInputFieldsView? inputs,
        Func<bool> current)
    {
        if (inputs is null) return;
        try
        {
            var revision=draft.Revision;
            if(!current())return;
            if (inputs.Fields.Any(field => field.FieldId == BlobMaskInheritance.SourceId))
                inputs.RefreshInputProjection(BlobMaskInheritance.SourceId);
            foreach (var field in inputs.Fields.Where(field => BlobRoiInheritance.Ports.Any(port => port.Id == field.FieldId)).ToArray())
            {
                if (!current() || revision != draft.Revision) return;
                inputs.RefreshInputProjection(field.FieldId);
            }
            foreach (var field in inputs.Fields.Where(field => field.FieldId.StartsWith(BlobTool.PoseInput, StringComparison.Ordinal)).ToArray())
            {
                if (!current() || revision != draft.Revision) return;
                // VM's SelectWay is a real persisted radio with default value 0.
                // The projection may be unset while the optional fixture leaves are
                // dormant, but that must not clear the selected radio or make the
                // three choices appear inert after the enable switch is accepted.
                inputs.RefreshInputProjection(field.FieldId,
                    field.FieldId == BlobFixtureInputs.PositionCorrectionModeId ? new ConstantSetting(0L) : null);
                if (field.FieldId is not (BlobTool.PoseInput or BlobFixtureInputs.ReferencePoint or BlobFixtureInputs.RunPoint)) continue;
                var members = BlobFixtureInputs.LeavesFor(field.FieldId).Select(inputs.ReadInputProjection).ToArray();
                field.SetReferenceSummary(field.Setting is UnsetSetting && members.Any(member => member is not UnsetSetting)
                    ? "(" + string.Join(", ", members.Select(Describe)) + ")" : null);
            }
            string Describe(ParameterSetting value)
            {
                try { return inputs.DescribeInputSource(value); }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return "来源不可用"; }
            }
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException) { }
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        foreach(var field in _inputs.Fields)field.Changed-=Refresh;
    }
}
