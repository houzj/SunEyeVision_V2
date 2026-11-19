using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.ImageSource.Runtime;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;

namespace SunEyeVision.Tool.ImageSource.Wpf.Design;

/// <summary>Design-only editor state over the real Runtime factory and field operations.</summary>
public static class ImageSourceDesignData
{
    public static (IToolSettingsDraft Draft, ToolSettingsViewOptions Options) CreateParameterContext(bool camera=false)
    {
        var (draft, options) = SunEyeVision.Plugin.Wpf.Presenters.Design.DesignTimeSample.Create(new ImageSourceTool());
        if(camera)draft.ApplyFields(new Dictionary<string,ParameterSetting>{[ImageSourceFields.SourceKind]=new ConstantSetting("camera")},_=>{});
        return (draft, options with { InputContent = new DesignSourceContent() });
    }

    private sealed class DesignSourceContent : System.Windows.Controls.UserControl,IImageSourceSettingsContent,IDisposable
    {
        private IToolSettingsDraft? _draft;
        private ImageSourceInputView Input {get;}=new();
        public event Action? Changed {add{} remove{}}
        public DesignSourceContent()
        {
            Content=Input;
            Input.CameraPicker.IsEnabled=false;Input.OpenCameraButton.IsEnabled=false;
            Input.CameraPicker.Items.Add(new System.Windows.Controls.ComboBoxItem{Content="未选择相机"});
            Input.CameraPicker.SelectedIndex=0;Input.CameraStatus.Text="设计态：未连接设备";
        }
        public void BindSourceDraft(IToolSettingsDraft draft,Action prepareEdit,Action<FieldSettings> acceptCandidate)
        {_draft=draft;RefreshSourceDraft();}
        public void RefreshSourceDraft()
        {if(_draft is not null)Input.IsCamera=_draft.CaptureFields().Get<string>(ImageSourceFields.SourceKind)=="camera";}
        public void Dispose(){_draft=null;}
    }
}
