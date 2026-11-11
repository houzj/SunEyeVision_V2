using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>Original assist layout over owner-provided reference rows and a formal mode field.</summary>
public sealed class NativeAssistUiDraftControls : StackPanel, IDisposable
{
    private readonly DeclaredFieldRow _mode;
    private readonly List<AssistRow> _rows=[];
    private readonly DeclaredFieldRow _information;
    private bool _disposed, _projecting, _projectionPending;

    public NativeAssistUiDraftControls(DeclaredFieldRow mode, DeclaredFieldRow information,
        IEnumerable<(int Mode, Func<DeclaredFieldRow> Create)> sources)
    {
        _mode=mode??throw new ArgumentNullException(nameof(mode));
        _information=information??throw new ArgumentNullException(nameof(information));
        if(mode.Parent is not null)throw new ArgumentException("修正方式控件已有宿主。",nameof(mode));
        if(information.Parent is not null)throw new ArgumentException("修正信息控件已有宿主。",nameof(information));
        try
        {
            Children.Add(_mode);
            information.SetLabel("Fixture Info");
            information.SetDescription("Description_CorrectInfo");
            Children.Add(information);
            _rows.AddRange(sources.Select(source => new AssistRow(source.Mode, source.Create)));
            _mode.Changed+=ProjectVisibility;_mode.Changed+=OnUiChanged;ProjectVisibility();
            IsVisibleChanged+=OnVisibility;Loaded+=OnLoaded;Unloaded+=OnUnloaded;
        }
        catch{Dispose();throw;}
    }

    private sealed class AssistRow(int visibleMode,Func<DeclaredFieldRow> create)
    {
        internal int VisibleMode {get;}=visibleMode;
        internal Func<DeclaredFieldRow> Create {get;}=create;
        internal DeclaredFieldRow? Editor {get;set;}
    }
    private DeclaredFieldRow? EnsureEditor(AssistRow row)
    {
        if(row.Editor is not null)return row.Editor;
        var created=row.Create();
        if(_disposed){created.Dispose();return null;}
        row.Editor=created;
        row.Editor.Changed+=OnUiChanged;
        var index = _rows.IndexOf(row);
        var before = _rows.Take(index).Count(item => item.Editor is not null);
        Children.Insert(2 + before, row.Editor);
        return row.Editor;
    }
    public event Action? Changed;
    private void OnUiChanged(){if(!_disposed)Changed?.Invoke();}
    public bool TrySetMode(long mode)=>!_disposed&&_mode.TrySetConstant(mode);
    private IEnumerable<DeclaredFieldRow> ActiveSources => _rows.Select(row => row.Editor).OfType<DeclaredFieldRow>()
        .Prepend(_information).Where(row => row.Visibility == Visibility.Visible && row.IsEnabled);
    public bool HasPendingEdits => !_disposed && ActiveSources.Any(row => row.HasPendingEdits);
    public string? CommitActiveSources()
    {
        if(_disposed)return "位置修正组合已失效。";
        foreach (var row in ActiveSources.ToArray())
        {
            if(_disposed)return "位置修正组合已失效。";
            if (!row.TryCommitPending()) return row.Error;
        }
        return _disposed?"位置修正组合已失效。":null;
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
        _projectionPending=true;
        if(_projecting)return;
        _projecting=true;
        try
        {
            while(_projectionPending&&!_disposed)
            {
                _projectionPending=false;
                var mode=_mode.Setting is ConstantSetting {Value:long value}?value:0L;
                _information.Visibility=mode==0?Visibility.Visible:Visibility.Collapsed;
                foreach(var row in _rows)
                {
                    if(_disposed||_projectionPending)break;
                    var active=row.VisibleMode==mode||(row.VisibleMode==3&&mode is 1 or 2);
                    if(active)
                    {
                        var editor=EnsureEditor(row);
                        if(_disposed||_projectionPending||editor is null)break;
                        editor.Visibility=Visibility.Visible;
                    }
                    else if(row.Editor is not null)row.Editor.Visibility=Visibility.Collapsed;
                }
                if(!_disposed&&!_projectionPending)PrepareInteractions(IsVisible);
            }
        }
        finally{_projecting=false;}
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;
        IsVisibleChanged-=OnVisibility;Loaded-=OnLoaded;Unloaded-=OnUnloaded;
        var rows=_rows.ToArray();
        ParameterCleanup.Run(new Action[]{()=>((IParameterVisibility)_information).PrepareForVisibility(false),()=>{_mode.Changed-=ProjectVisibility;_mode.Changed-=OnUiChanged;}}
            .Concat(rows.Where(row=>row.Editor is not null).Select(row=>(Action)(()=>row.Editor!.Changed-=OnUiChanged)))
            .Append(()=>Children.Clear())
            .Concat(Array.Empty<Action>()));
    }
}
