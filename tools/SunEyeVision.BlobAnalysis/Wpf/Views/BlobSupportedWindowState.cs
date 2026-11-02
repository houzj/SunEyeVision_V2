using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>Read-only capability projection over the real draft; never stores settings or runtime values.</summary>
internal sealed class BlobSupportedWindowState : IDisposable
{
    private readonly IToolSettingsDraft _draft;
    private readonly DeclaredInputFieldsView _inputs;
    private bool _disposed;
    internal BlobSupportedWindowState(IToolSettingsDraft draft,DeclaredInputFieldsView inputs)
    {
        _draft=draft;_inputs=inputs;
        foreach(var field in inputs.Fields)field.Changed+=Refresh;
        Refresh();
    }
    private static string Id(string property)=>BlobParameterDefinition.Instance.SettingsFieldId(property);
    private IEnumerable<FieldPresentation> JudgmentSwitches=>_draft.Schema.Presentation!.Fields.Where(field=>field.Group=="结果判定"
        &&_draft.Schema.Inputs.Single(port=>port.Id==field.FieldId).Type==TypeRef.ScalarOf(ScalarKind.Boolean));
    internal string? Validate()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var values=_draft.CaptureFields();
        if(values.Read(Id(nameof(BlobSettings.SegmentationMode))) is not ConstantSetting {Value:"SingleThreshold"})
            return "本窗口仅支持已就绪的单阈值；已有其他模式保留原值，不能确认或执行。";
        if(_inputs.Fields.Where(field=>field.FieldId is BlobTool.InspectionInput or BlobTool.MaskInput).Any(field=>field.Setting is not UnsetSetting))
            return "已有像素区域引用，原几何继承尚未实现，当前窗口不能确认或执行；原值保持不变。";
        if(values.Read(Id(nameof(BlobSettings.OutputGrayImage))) is not ConstantSetting {Value:true}
            ||values.Read(Id(nameof(BlobSettings.OutputBoundary))) is not ConstantSetting {Value:false})
            return "已有图像输出设置超出当前窗口支持范围，不能确认或执行；原值保持不变。";
        if(values.Read(Id(nameof(BlobSettings.IsPositionCorrectionEnabled))) is not ConstantSetting {Value:false})
            return "位置修正尚未接入原三方式，请明确关闭正式位置开关后再确认；原值不会自动清除。";
        if(_inputs.Fields.Single(field=>field.FieldId==BlobTool.PoseInput).Setting is not UnsetSetting)
            return "位置修正尚未实现，请在修正信息中清除已有来源后再确认。";
        if(values.Read(Id(nameof(BlobSettings.UpperThreshold))) is ReferenceSetting or VariableSetting)
            return "单阈值下隐藏的高阈值仍有正式绑定，本窗口不能确认或执行；绑定保持原值。";
        var enabled=JudgmentSwitches.Where(field=>values.Read(field.FieldId) is not ConstantSetting {Value:false}).Select(field=>field.DisplayName).ToArray();
        return enabled.Length==0?null:"已有正式结果判定影响运行但原厂判定尚未接通，本窗口拒绝确认或执行："+string.Join("、",enabled)+"。原值保持不变。";
    }
    internal void Refresh()
    {
        if(_disposed)return;
        // This is only a visibility projection. Capture/acceptance always reads the real draft again.
        try
        {
            var revision=_draft.Revision;var values=_draft.CaptureFields();
            var pose=_inputs.Fields.Single(field=>field.FieldId==BlobTool.PoseInput);
            if(_disposed||revision!=_draft.Revision)return;
            if(InformationHost is {} host)host.Visibility=pose.Setting is not UnsetSetting
                ||values.Read(Id(nameof(BlobSettings.IsPositionCorrectionEnabled))) is not ConstantSetting {Value:false}
                ?Visibility.Visible:Visibility.Collapsed;
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException)
        {
            // Do not turn a refresh observer fault into a false rejection of an already accepted edit.
            if(!_disposed&&InformationHost is {} host)host.Visibility=Visibility.Visible;
        }
    }
    internal FrameworkElement? InformationHost {get;set;}    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        foreach(var field in _inputs.Fields)field.Changed-=Refresh;
    }
}
