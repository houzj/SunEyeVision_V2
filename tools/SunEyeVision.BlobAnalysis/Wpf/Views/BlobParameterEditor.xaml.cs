using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>Real tool view; the document owner controls execution, capture and disposal.</summary>
public partial class BlobParameterEditor : UserControl,IToolSettingsView,IToolSettingsViewReady,IToolSettingsViewInputProjection
{
    private static readonly DependencyPropertyKey SelectedPageIndexPropertyKey=DependencyProperty.RegisterReadOnly(nameof(SelectedPageIndex),typeof(int),typeof(BlobParameterEditor),new PropertyMetadata(0));
    public static readonly DependencyProperty SelectedPageIndexProperty=SelectedPageIndexPropertyKey.DependencyProperty;
    public int SelectedPageIndex=>(int)GetValue(SelectedPageIndexProperty);
    private BlobUiComposition? _composition;
    private BlobUiComposition? Composition=>_composition??Design.BlobDesignData.GetComposition(Editor);
    public Task ReadyTask=>Composition?.ReadyTask??Task.CompletedTask;
    public IReadOnlyDictionary<string,long> CompositionStageMs=>Composition?.StageMs??new Dictionary<string,long>();
    private Action? _uiOnlyChanged;
    private bool _disposed;
    private void SelectedPageChanged(object? sender,EventArgs args)=>SetValue(SelectedPageIndexPropertyKey,Editor.SelectedPageIndex);
    public BlobParameterEditor()
    {InitializeComponent();Editor.SelectedPageChanged+=SelectedPageChanged;SelectedPageChanged(this,EventArgs.Empty);}
    public BlobParameterEditor(IToolSettingsDraft draft,ToolSettingsViewOptions options):this()
    {
        try
        {
            _composition=new(Editor,draft,options);_composition.Changed+=OnUiOnlyChanged;
            SelectedPageChanged(Editor,EventArgs.Empty);
        }
        catch(Exception failure)
        {
            try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    public FrameworkElement View=>this;
    public void RefreshAcceptedInputs()=>Composition?.RefreshAcceptedInputs();
    public bool HasInvalidInput=>Editor.HasInvalidInput||Composition?.IsReady!=true||Composition?.CompositionError is not null;
    public bool HasPendingEdits=>Editor.HasPendingEdits||Composition?.HasPending==true;
    public bool HasPendingEditsOutside(IReadOnlySet<string> excludedFields)=>Editor.HasPendingEditsOutside(excludedFields)
        ||Composition is not {IsReady:true,HasPending:false};
    public long AcceptedRevision=>Editor.AcceptedRevision+(Composition?.RoiRevision??0);
    public IReadOnlyList<string> AcceptedFieldOrder=>Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields()=>Editor.CaptureAcceptedFields();
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId=null)
        =>Editor.ReadNumericModels(scope,fieldId);
    public event Action? Changed {add{Editor.Changed+=value;_uiOnlyChanged+=value;}remove{Editor.Changed-=value;_uiOnlyChanged-=value;}}
    public bool TryCapture(out FieldSettings fields,out string error)
    {
        if(Composition is {} composition&&composition.CompositionError is {} failure){fields=null!;error=failure;return false;}
        if(Composition is {} ready&&!ready.IsReady){fields=null!;error="参数组合仍在组装，请稍候。";return false;}
        return Composition is {} current?current.TryCapture(out fields,out error):Editor.TryCapture(out fields,out error);
    }
    private void OnUiOnlyChanged(){if(!_disposed){_uiOnlyChanged?.Invoke();System.Windows.Input.CommandManager.InvalidateRequerySuggested();}}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Editor.SelectedPageChanged-=SelectedPageChanged;_uiOnlyChanged=null;
        var composition=Composition;if(composition is not null)composition.Changed-=OnUiOnlyChanged;
        try{composition?.Dispose();}finally{Editor.Dispose();}
    }
}
