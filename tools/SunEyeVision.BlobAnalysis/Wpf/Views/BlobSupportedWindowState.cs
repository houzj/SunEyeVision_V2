using System.Windows;
using System.Windows.Controls;
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
    private bool _disposed;
    private readonly FrameworkElement _pointVisibility;
    internal BlobSupportedWindowState(IToolSettingsDraft draft,DeclaredInputFieldsView inputs,FrameworkElement pointVisibility)
    {
        _draft=draft;_inputs=inputs;_pointVisibility=pointVisibility;
        foreach(var field in inputs.Fields)field.Changed+=Refresh;
        Refresh();
    }
    private static string Id(string property)=>BlobParameterDefinition.Instance.SettingsFieldId(property);
    internal string? Validate()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(ValidateSettings(_draft) is {} thresholdError)return thresholdError;
        return ValidateInputs(_inputs);
    }
    internal static string? ValidateInputs(DeclaredInputFieldsView inputs)
    {
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
        if(inputs.Fields.Where(field=>field.FieldId is BlobTool.InspectionInput or BlobTool.MaskInput).Any(field=>field.Setting is not UnsetSetting))
            return "已有像素区域引用，原几何继承尚未实现，当前窗口不能确认或执行；原值保持不变。";
        if(inputs.Fields.Single(field=>field.FieldId==BlobTool.PoseInput).Setting is not UnsetSetting)
            return "位置修正尚未实现，请在修正信息中清除已有来源后再确认。";
        return null;
    }
    // These guards must also run before the lazy Basic page has created its input-owned state.
    internal static string? ValidateSettings(IToolSettingsDraft draft)
    {
        var values=draft.CaptureFields();
        if(!BlobThresholdCapabilities.IsSupported((values.Read(Id(nameof(BlobSettings.SegmentationMode))) as ConstantSetting)?.Value as string))
            return "当前阈值模式尚未就绪；原设置保留，不能确认或执行。";
        if(values.Read(Id(nameof(BlobSettings.SegmentationMode))) is not ConstantSetting{Value:"DoubleThreshold"}
            &&values.Read(Id(nameof(BlobSettings.UpperThreshold))) is ReferenceSetting or VariableSetting)
            return "单阈值下隐藏的高阈值仍有正式绑定，本窗口不能确认或执行；绑定保持原值。";
        var mode=Enum.Parse<BlobSegmentationMode>((string)((ConstantSetting)values.Read(Id(nameof(BlobSettings.SegmentationMode)))).Value!);
        foreach(var property in new[]{nameof(BlobSettings.Threshold),nameof(BlobSettings.FixedSoftLowerThreshold),nameof(BlobSettings.FixedSoftUpperThreshold),nameof(BlobSettings.ThresholdSoftness),
            nameof(BlobSettings.RelativeLeftRatio),nameof(BlobSettings.RelativeRightRatio),nameof(BlobSettings.RelativeLowerRatio),nameof(BlobSettings.RelativeUpperRatio)})
            if(!BlobThresholdCapabilities.IsThresholdFieldActive(mode,property)&&values.Read(Id(property)) is ReferenceSetting or VariableSetting)
                return "当前模式隐藏的阈值字段仍有正式绑定，尚未接入休眠引用消费策略；绑定保持原值："+BlobParameterDefinition.Instance.Label(property);
        if(values.Read(Id(nameof(BlobSettings.OutputBoundary))) is not ConstantSetting {Value:false})
            return "已有图像输出设置超出当前窗口支持范围，不能确认或执行；原值保持不变。";
        if(values.Read(Id(nameof(BlobSettings.IsPositionCorrectionEnabled))) is not ConstantSetting {Value:false})
            return "位置修正尚未接入原三方式，请明确关闭正式位置开关后再确认；原值不会自动清除。";
        var judgmentSwitches=draft.Schema.Presentation!.Fields.Where(field=>field.Group=="结果判定"
            &&draft.Schema.Inputs.Single(port=>port.Id==field.FieldId).Type==TypeRef.ScalarOf(ScalarKind.Boolean));
        var enabled=judgmentSwitches.Where(field=>values.Read(field.FieldId) is not ConstantSetting{Value:false}).Select(field=>field.DisplayName).ToArray();
        return enabled.Length==0?null:"已有正式结果判定影响运行但原厂判定尚未接通，本窗口拒绝确认或执行："+string.Join("、",enabled)+"。原值保持不变。";
    }
    internal void Refresh()
    {
        if(!_disposed)ProjectAssist(_draft,_inputs,InformationHost as NativeAssistUiDraftControls,_pointVisibility,()=>!_disposed);
    }
    // One logical projection also serves Result-first, without constructing the lazy Basic inputs.
    // Last accepted parameter values and effective IsVisible are not the UI's activity contract.
    internal static void ProjectAssist(IToolSettingsDraft draft,DeclaredInputFieldsView? inputs,NativeAssistUiDraftControls? assist,
        FrameworkElement pointVisibility,Func<bool> current)
    {
        try
        {
            var revision=draft.Revision;var values=draft.CaptureFields();
            var hasPose=inputs?.Fields.Single(field=>field.FieldId==BlobTool.PoseInput).Setting is {} pose&&pose is not UnsetSetting;
            var enabled=values.Read(Id(nameof(BlobSettings.IsPositionCorrectionEnabled))) is not ConstantSetting{Value:false};
            var graphic=assist?.GraphicVisibilitySource?.Visibility is not (Visibility.Collapsed or Visibility.Hidden);
            if(!current()||revision!=draft.Revision)return;
            assist?.SetInformationOnly(graphic&&!enabled&&hasPose);
            if(!current()||revision!=draft.Revision)return;
            if(assist is not null)assist.Visibility=graphic&&(enabled||hasPose)?Visibility.Visible:Visibility.Collapsed;
            if(current()&&revision==draft.Revision)pointVisibility.Visibility=graphic&&enabled?Visibility.Visible:Visibility.Collapsed;
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException)
        {
            // Keep an existing input's clear route available on a projection fault. Admission still re-reads the real fields.
            if(current()&&assist is not null)assist.Visibility=Visibility.Visible;
        }
    }
    internal FrameworkElement? InformationHost {get;set;}    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        foreach(var field in _inputs.Fields)field.Changed-=Refresh;
    }
}
