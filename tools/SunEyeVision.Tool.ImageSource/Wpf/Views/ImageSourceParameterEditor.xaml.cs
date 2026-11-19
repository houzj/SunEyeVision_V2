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

    public ImageSourceParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        InitializeComponent();
        Bind(draft, options);
    }

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
        var conditions=options.ParentConditions?.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal)
            ??new Dictionary<string,IReadOnlyList<FieldCondition>>(StringComparer.Ordinal);
        conditions[ImageSourceFields.ShowImageName]=[new(ImageSourceFields.SourceKind,"local")];
        conditions[ImageSourceFields.StopAtLast]=[new(ImageSourceFields.AutoSwitch,true)];
        conditions[ImageSourceFields.FolderPath]=[new(ImageSourceFields.SourceKind,"local")];
        conditions[ImageSourceFields.RefreshFolder]=[new(ImageSourceFields.SourceKind,"local")];
        conditions[ImageSourceFields.TriggerFolderRun]=[new(ImageSourceFields.SourceKind,"local")];
        conditions[ImageSourceFields.ClearStitchValue]=[new(ImageSourceFields.StitchEnabled,true)];
        Editor.Bind(draft, options with { InputSection = null, InputAfterField = ImageSourceFields.SourceKind,
            InputCondition = new FieldCondition(ImageSourceFields.SourceKind,"camera"), ParentConditions=conditions });
        EnsureCurrentBinding();
        Editor.Changed+=OnFieldsChanged;
        if(_source is not null)_source.Changed+=OnSourceChanged;
    }

    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId=null)
    { ObjectDisposedException.ThrowIf(_disposed,this); return Editor.ReadNumericModels(scope,fieldId); }
    public FrameworkElement View => this;
    public bool HasInvalidInput => Editor.HasInvalidInput;
    public bool HasPendingEdits=>Editor.HasPendingEdits;
    public bool HasPendingEditsOutside(IReadOnlySet<string> excludedFields)=>Editor.HasPendingEditsOutside(excludedFields);
    public long AcceptedRevision=>Editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder=>Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields()=>Editor.CaptureAcceptedFields();
    public event Action? Changed { add => ViewChanged += value; remove => ViewChanged -= value; }
    private void OnFieldsChanged(){_source?.RefreshSourceDraft();ViewChanged?.Invoke();}
    private void OnSourceChanged()
    {
        // Camera identity belongs to the input slot, not a materialized parameter row.
        // The shared draft revision lets the host accept that edit without rebuilding the page.
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
