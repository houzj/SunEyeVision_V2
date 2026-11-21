using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.If.Runtime;

namespace SunEyeVision.Tool.If.Wpf.Views;

/// <summary>设计器与运行期使用同一套真实参数声明；不复制条件模型。</summary>
public partial class IfParameterEditor:UserControl,IToolSettingsView
{
    private IfBranchPanel? _branchPanel;
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
        // 条件分支的输入是参数引用（无图像输入），但宿主仍需一个已声明的分组来挂输入内容区。
        var panel = new IfBranchPanel(draft, options.AcceptCandidate,
            options.ReferenceOptions ?? ReferenceFieldOptions.Empty,
            options.ReadRuntimeModuleIds);
        var suppressed = new HashSet<string>(StringComparer.Ordinal)
        {
            IfFields.BranchText
        };
        for (var index = 0; index < IfFields.ConditionCount; index++)
        {
            suppressed.Add(IfFields.EnabledField(index));
            suppressed.Add(IfFields.SourceField(index));
            suppressed.Add(IfFields.MinimumField(index));
            suppressed.Add(IfFields.MaximumField(index));
        }
        var anchored = (options.AdditionalAnchoredContents ?? Array.Empty<AnchoredSettingsContent>()).ToList();
        anchored.Add(new AnchoredSettingsContent(IfFields.BranchText, panel, InsertBefore: true));
        Editor.Bind(draft, options with
        {
            InputSection = options.InputSection ?? IfFields.ConditionName(0),
            SuppressedFieldRows = suppressed,
            AdditionalAnchoredContents = anchored,
            PageOnlyPages = (options.PageOnlyPages ?? Array.Empty<string>()).Append("基本参数").Distinct(StringComparer.Ordinal).ToArray()
        });
        _branchPanel = panel;
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
        _branchPanel?.Dispose();
        _branchPanel = null;
        ViewChanged=null;
        Editor.Dispose();
    }
}
