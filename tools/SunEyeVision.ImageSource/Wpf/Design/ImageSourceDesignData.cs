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
        if(camera)draft.ApplyFields(new Dictionary<string,object?>{[ImageSourceFields.SourceKind]="camera"},_=>{});
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
        public void BindSourceDraft(IToolSettingsDraft draft,Action prepareEdit,Action<object> acceptCandidate)
        {_draft=draft;RefreshSourceDraft();}
        public void RefreshSourceDraft()
        {if(_draft is not null){Input.SourceStrip.Session=new ReadOnlySourceSession(_draft);Input.IsCamera=_draft.ReadFields().Get<string>(ImageSourceFields.SourceKind)=="camera";}}
        public void Dispose(){Input.SourceStrip.Session=null;_draft=null;}
    }
    private sealed class ReadOnlySourceSession : IImageSourceStripSession
    {
        private readonly IToolSettingsDraft _draft;
        public ReadOnlySourceSession(IToolSettingsDraft draft) => _draft = draft;
        private InputValues Values => _draft.ReadFields();
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
        private readonly IToolDefinition _tool = new ImageSourceTool();
        private IToolSettingsFields Fields => (IToolSettingsFields)_tool;
        private object _settings;
        private bool _applying;
        public DesignDraft() => _settings = _tool.FreezeSettings(_tool.CreateSettings());
        public ToolSchema Schema => Fields.SettingsSchema;
        public InputValues ReadFields() => Fields.ReadSettings(_settings);
        public object CaptureSettings() => _tool.FreezeSettings(_settings);

        public void ApplyFields(IReadOnlyDictionary<string, object?> changes, Action<object> acceptCandidate)
        {
            ArgumentNullException.ThrowIfNull(changes);
            ArgumentNullException.ThrowIfNull(acceptCandidate);
            if (_applying) throw new InvalidOperationException("设计参数接纳正在进行。");
            _applying = true;
            try
            {
                var candidate = Fields.ApplySettings(_settings, changes);
                var errors = _tool.Validate(candidate);
                if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
                var frozen = _tool.FreezeSettings(candidate);
                acceptCandidate(frozen);
                _settings = _tool.FreezeSettings(frozen);
            }
            finally { _applying = false; }
        }
    }
}
