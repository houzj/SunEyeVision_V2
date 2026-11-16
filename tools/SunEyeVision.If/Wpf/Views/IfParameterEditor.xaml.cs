using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.If.Wpf.Views;

/// <summary>设计器与运行期使用同一套真实参数声明；不复制条件模型。</summary>
public partial class IfParameterEditor:UserControl,IToolSettingsView
{
    private event Action? ViewChanged;
    private bool _disposed;
    private long _generation;

    public IfParameterEditor()
    {
        InitializeComponent();
        if(System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
        {
            var context=Design.IfDesignData.CreateParameterContext();
            Bind(context.Draft,context.Options);
        }
    }

    public IfParameterEditor(IToolSettingsDraft draft,ToolSettingsViewOptions options)
    {
        InitializeComponent();
        Bind(draft,options);
    }

    public void Bind(IToolSettingsDraft draft,ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        var generation=++_generation;
        Editor.Changed-=OnFieldsChanged;
        Editor.Bind(draft,options);
        if(_disposed||generation!=_generation||!ReferenceEquals(Editor.Draft,draft))
            throw new InvalidOperationException("条件分支参数组合已重新绑定或关闭。");
        Editor.Changed+=OnFieldsChanged;
    }

    private void OnFieldsChanged()=>ViewChanged?.Invoke();

    public FrameworkElement View=>this;
    public bool HasInvalidInput=>Editor.HasInvalidInput;
    public bool HasPendingEdits=>Editor.HasPendingEdits;
    public bool HasPendingEditsOutside(IReadOnlySet<string> excludedFields)=>Editor.HasPendingEditsOutside(excludedFields);
    public long AcceptedRevision=>Editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder=>Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields()=>Editor.CaptureAcceptedFields();
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        return Editor.ReadNumericModels(scope,fieldId);
    }
    public event Action? Changed{add=>ViewChanged+=value;remove=>ViewChanged-=value;}
    public bool TryCapture(out FieldSettings fields,out string error)=>Editor.TryCapture(out fields,out error);
    public void Dispose()
    {
        if(_disposed)return;
        _disposed=true;_generation++;
        Editor.Changed-=OnFieldsChanged;
        ViewChanged=null;
        Editor.Dispose();
    }
}
