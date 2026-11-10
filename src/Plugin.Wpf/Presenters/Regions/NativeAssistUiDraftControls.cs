using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>Original assist layout with temporarily unavailable source fields.</summary>
public sealed class NativeAssistUiDraftControls : StackPanel, IDisposable
{
    private readonly DeclaredFieldState _mode;
    private readonly DeclaredFieldRow _radio;
    private readonly List<AssistRow> _rows=[];
    private readonly DeclaredFieldRow _information;
    private bool _disposed,_informationOnly;
    public FrameworkElement? GraphicVisibilitySource {get;init;}

    public NativeAssistUiDraftControls(DeclaredFieldRow information)
    {
        _information=information??throw new ArgumentNullException(nameof(information));
        if(information.Parent is not null)throw new ArgumentException("修正信息控件已有宿主。",nameof(information));
        _mode=new DeclaredFieldState(new Port("ui-only-assist-mode",DataType.Integer),new ConstantSetting(0L),_=>{});
        _radio=new DeclaredFieldRow(_mode,"选择方式",integerRadioOptions:new NativeIntegerRadioOptions(new[]{
            new NativeIntegerRadioChoice(0,"按信息"),new NativeIntegerRadioChoice(1,"按点"),
            new NativeIntegerRadioChoice(2,"按坐标")},useFiveSlotTemplate:true));
        try
        {
            Children.Add(_radio);
            information.SetLabel("修正信息");Children.Add(information);
            Add("基准点",1);Add("基准点X",2);Add("基准点Y",2);
            Add("基准角度",3);Add("基准尺度X",3);Add("基准尺度Y",3);
            Add("运行点",1);Add("运行点X",2);Add("运行点Y",2);
            Add("运行角度",3);Add("运行尺度X",3);Add("运行尺度Y",3);
            _mode.Changed+=ProjectVisibility;_mode.Changed+=OnUiChanged;ProjectVisibility();
            IsVisibleChanged+=OnVisibility;Loaded+=OnLoaded;Unloaded+=OnUnloaded;
        }
        catch{Dispose();throw;}
    }

    private void Add(string label,int visibleMode)
    {
        // Keep the declaration in the VM parameter tree, but do not construct a
        // hidden editor. The original window only materializes the selected
        // assist mode; the other templates remain declarations until selected.
        _rows.Add(new AssistRow(label,visibleMode));
    }
    private sealed class AssistRow(string label,int visibleMode)
    {
        internal string Label {get;}=label;
        internal int VisibleMode {get;}=visibleMode;
        internal DeclaredFieldRow? Editor {get;set;}
    }
    private DeclaredFieldRow EnsureEditor(AssistRow row)
    {
        if(row.Editor is not null)return row.Editor;
        var port=new Port("ui-only-assist-"+_rows.IndexOf(row),DataType.Number,Required:false,EditModes:FieldEditModes.Reference);
        row.Editor=new DeclaredFieldRow(port,row.Label,new UnsetSetting(),_=>{},ReferenceFieldOptions.Empty,allowUnboundReference:true)
            {ToolTip="界面试用，来源功能尚未接入。"};
        row.Editor.Changed+=OnUiChanged;
        Children.Add(row.Editor);
        return row.Editor;
    }
    public event Action? Changed;
    public bool HasUiChanges=>!_disposed&&(IsUnimplementedMode||_rows.Any(row=>row.Editor is { } editor&&(editor.HasPendingEdits||editor.Setting is not UnsetSetting)));
    private void OnUiChanged(){if(!_disposed)Changed?.Invoke();}
    public bool IsUnimplementedMode=>!_disposed&&(long)((ConstantSetting)_mode.Accepted).Value! != 0;
    public bool TrySetMode(long mode)=>_radio.TrySetConstant(mode);
    public void SetInformationOnly(bool informationOnly)
    {
        if(_disposed||_informationOnly==informationOnly)return;
        RetireInteractions();_informationOnly=informationOnly;ProjectVisibility();
    }
    public void RetireInteractions()
    {
        if(_disposed)return;
        ParameterCleanup.Run(_rows.Where(row=>row.Editor is not null).Select(row=>(Action)row.Editor!.RetireInteractions).Prepend(_information.RetireInteractions));
    }
    private void OnVisibility(object sender,DependencyPropertyChangedEventArgs args)=>PrepareInteractions((bool)args.NewValue);
    private void OnLoaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,this))PrepareInteractions(IsVisible);}
    private void OnUnloaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,this))PrepareInteractions(false);}
    private void PrepareInteractions(bool visible)
    {
        if(_disposed)return;
        ParameterCleanup.Run(_rows.Where(row=>row.Editor is not null).Select(row=>(Action)(()=>((IParameterVisibility)row.Editor!).PrepareForVisibility(visible&&row.Editor!.Visibility==Visibility.Visible)))
            .Prepend(()=>((IParameterVisibility)_information).PrepareForVisibility(visible&&_information.Visibility==Visibility.Visible)));
    }
    private void ProjectVisibility()
    {
        if(_disposed)return;
        var mode=(long)((ConstantSetting)_mode.Accepted).Value!;
        _radio.Visibility=_informationOnly?Visibility.Collapsed:Visibility.Visible;
        _information.Visibility=_informationOnly||mode==0?Visibility.Visible:Visibility.Collapsed;
        foreach(var row in _rows)
        {
            var active=!_informationOnly&&(row.VisibleMode==mode||(row.VisibleMode==3&&mode is 1 or 2));
            if(active)
            {
                var editor=EnsureEditor(row);
                editor.Visibility=Visibility.Visible;
            }
            else if(row.Editor is not null)row.Editor.Visibility=Visibility.Collapsed;
        }
        PrepareInteractions(IsVisible);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;
        IsVisibleChanged-=OnVisibility;Loaded-=OnLoaded;Unloaded-=OnUnloaded;
        var rows=_rows.ToArray();
        ParameterCleanup.Run(new Action[]{()=>((IParameterVisibility)_information).PrepareForVisibility(false),()=>{if(_mode is {} mode){mode.Changed-=ProjectVisibility;mode.Changed-=OnUiChanged;}}}
            .Concat(rows.Where(row=>row.Editor is not null).Select(row=>(Action)(()=>row.Editor!.Changed-=OnUiChanged)))
            .Append(()=>Children.Clear())
            .Concat(rows.Where(row=>row.Editor is not null).Select(row=>(Action)(()=>row.Editor!.Dispose())))
            .Concat(new Action[]{()=>_radio?.Dispose(),()=>_mode?.Dispose()}));
    }
}
