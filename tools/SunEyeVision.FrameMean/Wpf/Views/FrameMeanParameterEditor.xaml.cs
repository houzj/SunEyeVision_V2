using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.FrameMean.Wpf.Design;

namespace SunEyeVision.Tool.FrameMean.Wpf.Views;

/// <summary>One actual parameter composition for designer and runtime; no parallel source model.</summary>
public partial class FrameMeanParameterEditor : UserControl, IToolSettingsView
{
    private event Action? ViewChanged;
    private bool _disposed;
    private long _generation;

    public FrameMeanParameterEditor()
    {
        InitializeComponent();
        if(System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
        {
            var context=FrameMeanDesignData.CreateParameterContext();
            Bind(context.Draft,context.Options);
        }
    }

    public FrameMeanParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options)
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
        // 有图像输入就必须声明输入区落位，否则参数页组装抛「An input content section is required.」。
        Editor.Bind(draft, options with { InputSection = options.InputSection ?? "运行参数" });
        if(generation!=_generation||_disposed)throw new InvalidOperationException("帧均值参数组合已重新绑定或关闭。");
        Editor.Changed+=OnFieldsChanged;
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
    private void OnFieldsChanged()=>ViewChanged?.Invoke();
    public bool TryCapture(out FieldSettings fields, out string error)
        => Editor.TryCapture(out fields, out error);
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_generation++;
        Editor.Changed-=OnFieldsChanged;
        ViewChanged=null;Editor.Dispose();
    }
}
