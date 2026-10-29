using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
using SunEyeVision.Tool.ImageSource.Runtime;

namespace SunEyeVision.Tool.ImageSource.Wpf.Views;

/// <summary>One actual parameter composition for designer and runtime; no parallel source model.</summary>
public partial class ImageSourceParameterEditor : UserControl, IToolSettingsView
{
    private IImageSourceSettingsContent? _source;
    private event Action? ViewChanged;
    private bool _disposed;
    private long _generation;
    // DeclaredSettingsView.InputSection currently addresses a page identity, not a category caption.
    public const string InputPage = "基本参数";

    public ImageSourceParameterEditor()
    {
        InitializeComponent();
        if(System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
        {
            var context=Design.ImageSourceDesignData.CreateParameterContext();
            Bind(context.Draft,context.Options);
        }
    }

    public ImageSourceParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()=>Bind(draft,options);

    public void Bind(IToolSettingsDraft draft,ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        var generation=++_generation;
        Editor.Changed-=OnFieldsChanged;
        if(_source is not null)_source.Changed-=OnSourceChanged;
        // Attachment and IDisposable ownership are entirely handled by the shared presenter.
        // If Bind fails before attachment, the caller still owns the unaccepted InputContent.
        _source=options.InputContent as IImageSourceSettingsContent;
        var source=_source;
        void EnsureCurrentBinding()
        {
            if(_disposed||generation!=_generation||!ReferenceEquals(_source,source)||!ReferenceEquals(Editor.Draft,draft))
                throw new InvalidOperationException("图像源参数组合已重新绑定或关闭。");
        }
        _source?.BindSourceDraft(draft,()=>
        {
            EnsureCurrentBinding();
            if(!Editor.TryCapture(out var fields,out var error))
                throw new InvalidOperationException(error);
            EnsureCurrentBinding();
        },candidate=>
        {
            EnsureCurrentBinding();
            options.AcceptCandidate?.Invoke(candidate);
        });
        Editor.Bind(draft, options with { InputSection = null, InputAfterField = ImageSourceFields.SourceKind,
            InputCondition = null });
        EnsureCurrentBinding();
        Editor.Changed+=OnFieldsChanged;
        if(_source is not null)_source.Changed+=OnSourceChanged;
    }

    public FrameworkElement View => this;
    public bool HasInvalidInput => Editor.HasInvalidInput;
    public bool HasPendingEdits=>Editor.HasPendingEdits;
    public long AcceptedRevision=>Editor.AcceptedRevision;
    public FieldSettings CaptureAcceptedFields()=>Editor.CaptureAcceptedFields();
    public event Action? Changed { add => ViewChanged += value; remove => ViewChanged -= value; }
    private void OnFieldsChanged(){_source?.RefreshSourceDraft();ViewChanged?.Invoke();}
    private void OnSourceChanged()
    {
        // The strip can also change declared fields such as automatic switching.
        // Re-read the accepted private draft so scalar editors cannot retain stale values.
        Editor.Bind(Editor.Draft??throw new InvalidOperationException("图像源设置草稿已释放。"),Editor.Options);
        ViewChanged?.Invoke();
    }
    public bool TryCapture(out FieldSettings fields, out string error)
        => Editor.TryCapture(out fields, out error);
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_generation++;
        Editor.Changed-=OnFieldsChanged;
        if(_source is not null)_source.Changed-=OnSourceChanged;
        _source=null;ViewChanged=null;Editor.Dispose();
    }
}
