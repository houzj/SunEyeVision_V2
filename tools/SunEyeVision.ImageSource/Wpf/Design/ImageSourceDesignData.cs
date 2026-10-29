using System.ComponentModel;
using SunEyeVision.Plugin.Contracts;
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
        var draft = new DesignDraft();
        if(camera)draft.ApplyFields(new Dictionary<string,ParameterSetting>{[ImageSourceFields.SourceKind]=new ConstantSetting("camera")},_=>{});
        return (draft, new(InputSection: Views.ImageSourceParameterEditor.InputPage, InputContent: new DesignSourceContent()));
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
        {if(_draft is not null){Input.SourceStrip.Session=new ReadOnlySourceSession(_draft);Input.IsCamera=_draft.CaptureFields().Get<string>(ImageSourceFields.SourceKind)=="camera";}}
        public void Dispose(){Input.SourceStrip.Session=null;_draft=null;}
    }
    private sealed class ReadOnlySourceSession : IImageSourceStripSession
    {
        private readonly IToolSettingsDraft _draft;
        public ReadOnlySourceSession(IToolSettingsDraft draft) => _draft = draft;
        private FieldSettings Values => _draft.CaptureFields();
        public IReadOnlyList<ImageSourceStripItem> Items
        {
            get
            {
                var values = Values;
                var selected = values.Get<SequenceValue>(ImageSourceFields.Selected)
                    .Select(item => (string)item!).ToHashSet(StringComparer.Ordinal);
                return Array.AsReadOnly(values.Get<SequenceValue>(ImageSourceFields.Paths)
                    .Select((item, index) => new ImageSourceStripItem(
                        index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + (string)item!,
                        System.IO.Path.GetFileName((string)item!), null, selected.Contains((string)item!)))
                    .ToArray());
            }
        }
        public long Version => 0;
        public int DisplayIndex => ConfiguredStartIndex;
        public int ConfiguredStartIndex => checked((int)Values.Get<long>(ImageSourceFields.Start));
        public bool IsReadOnly => true;
        public bool IsSelectMode => Values.Get<string>(ImageSourceFields.RunMode) == "selected";
        public bool AutoSwitchEnabled => Values.Get<bool>(ImageSourceFields.AutoSwitch);
        public bool IsImporting => false;
        public string? Status => null;
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public Task RequestAsync(ImageSourceStripRequest request, CancellationToken cancellationToken)
            => Task.FromException(new InvalidOperationException("设计态图像源条只读，不导入文件或执行工具。"));
    }

    private sealed class DesignDraft : IToolSettingsDraft
    {
        internal ImageSourceTool Tool {get;}=new();
        private readonly FieldSettingsDraft _draft;
        public DesignDraft()
        {
            var declaration=(IToolSettingsFields)Tool;
            _draft=new(declaration.SettingsSchema,new FieldSettings(declaration.SettingsSchema.Inputs,
                declaration.ReadSettings(new ImageSourceSettings()).CopyValues().ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value),StringComparer.Ordinal)));
        }
        public ToolSchema Schema=>_draft.Schema;
        public long Revision=>_draft.Revision;
        public FieldSettings CaptureFields()=>_draft.CaptureFields();
        public FieldSettings CaptureValidatedFields()
        {
            var fields=_draft.CaptureValidatedFields();
            if(fields.Capture().Values.Any(value=>value is ReferenceSetting))return fields;
            var settings=((IToolSettingsFields)Tool).MaterializeSettings(fields.Materialize(new Dictionary<string,object?>()));
            var errors=((IToolDefinition)Tool).Validate(settings);
            if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors.Select(error=>error.Message)));
            return fields;
        }
        public void ApplyFields(IReadOnlyDictionary<string,ParameterSetting> changes,Action<FieldSettings> acceptCandidate)=>_draft.ApplyFields(changes,acceptCandidate);
    }
}
