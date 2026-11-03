using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>Owns only this window's unsupported UI branches; supported fields remain borrowed.</summary>
internal sealed class BlobRunUiState : IDisposable
{
    private const string Mode="field:8ca20000000040008000000000000001",Polarity="field:88b45ab61b965761b901670fd587903f",
        Low="field:43bda03261df55b88f963bd18f2ba4d9",Sort="field:8ca20000000040008000000000000004";
    private readonly IToolSettingsDraft _draft;
    private readonly DeclaredSettingsView _editor;
    internal NativeThresholdUiDraftControls Threshold {get;}
    private readonly DeclaredFieldEditor _sort;
    private readonly IReadOnlySet<string> _supportedSort;
    private readonly IReadOnlyList<DeclaredFieldEditor> _created;
    private readonly IReadOnlyList<NativeUiDraftRange> _ranges;
    private readonly Dictionary<DeclaredFieldEditor,ParameterSetting> _baseline=[];
    private bool _attached,_disposed,_projecting;
    private string? _observedMode;
    internal event Action? Changed;
    private bool Current=>!_disposed&&_attached&&ReferenceEquals(_editor.Draft,_draft);
    internal bool IsUnsupportedSelection=>Current&&(!BlobThresholdCapabilities.IsSupported(Threshold.SelectedMode)||_sort.Setting is not ConstantSetting{Value:string key}
        ||!_supportedSort.Contains(key)||!Equals(_sort.Setting,_draft.CaptureFields().Read(Sort)));
    internal bool HasUiChanges=>Current&&(Threshold.HasUiChanges||_sort.HasPendingEdits||_ranges.Any(range=>range.HasChanges)
        ||_baseline.Any(pair=>pair.Key.HasPendingEdits||!Equals(pair.Key.Setting,pair.Value)));
    internal BlobRunUiState(IToolSettingsDraft draft,DeclaredSettingsView editor,NativeThresholdUiDraftControls threshold,
        DeclaredFieldEditor sort,IReadOnlySet<string> supportedSort,IReadOnlyList<DeclaredFieldEditor> created,IReadOnlyList<NativeUiDraftRange> ranges)
    {
        _draft=draft;_editor=editor;Threshold=threshold;_sort=sort;_supportedSort=supportedSort;_created=created.ToArray();_ranges=ranges.ToArray();
        Threshold.Changed+=OnThreshold;_sort.Changed+=OnSort;
        foreach(var field in _created)
        {
            if(!ReferenceEquals(field,sort))_baseline.Add(field,field.Setting);
            field.Changed+=OnChanged;field.AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnRaw));
        }
        foreach(var range in _ranges)range.Changed+=OnChanged;
    }
    internal void Attach(DeclaredFieldsView fieldHost)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(!_attached)
        {
            _attached=true;
            Threshold.ConfigureSingleProjection(()=>
            {
                if(!Current)throw new InvalidOperationException("阈值预览窗口已退役。");
                var fields=fieldHost.Fields;
                var low=fields.Single(field=>field.FieldId==Low);var polarity=_draft.CaptureFields().Read(Polarity);
                return(new ConstantSetting(polarity is ConstantSetting{Value:"BinaryInv"}?"dark":"bright"),low.Setting,
                    low.HasPendingEdits&&(low.InputElement is TextBox text)?text.Text:null);
            });
            var mode=_draft.CaptureFields().Read(Mode) as ConstantSetting;
            Threshold.TrySelectMode(mode?.Value as string is "None"?"BinaryImage":mode?.Value as string??"SingleThreshold");
            _editor.Changed+=OnFormalChanged;
        }
        OnFormalChanged();
    }
    private void OnThreshold()
    {
        if(!Current||_projecting)return;
        if(_observedMode!=Threshold.SelectedMode){_observedMode=Threshold.SelectedMode;RetireInteractions();}
        if(BlobThresholdCapabilities.IsSupported(Threshold.SelectedMode))
        {
            var selected=new ConstantSetting(Threshold.SelectedMode=="BinaryImage"?"None":Threshold.SelectedMode);
            if(!Equals(_draft.CaptureFields().Read(Mode),selected))
                _editor.TrySetFields(new Dictionary<string,ParameterSetting>{{Mode,selected}});
            if(Current&&Equals(_draft.CaptureFields().Read(Mode),selected))Threshold.AcceptModeProjection(Threshold.SelectedMode);
        }
        OnChanged();
    }
    private void OnSort()
    {
        if(!Current||_projecting)return;
        if(_sort.Setting is ConstantSetting{Value:string key}&&_supportedSort.Contains(key)
            &&!Equals(_sort.Setting,_draft.CaptureFields().Read(Sort)))
            _editor.TrySetFields(new Dictionary<string,ParameterSetting>{{Sort,_sort.Setting}});
        OnChanged();
    }
    private void OnFormalChanged()
    {
        if(!Current||_projecting)return;
        if(BlobThresholdCapabilities.IsSupported(Threshold.SelectedMode)
            &&_draft.CaptureFields().Read(Mode) is ConstantSetting{Value:string formalMode}
            &&BlobThresholdCapabilities.IsSupported(formalMode))
        {
            try{_projecting=true;var projected=formalMode=="None"?"BinaryImage":formalMode;Threshold.TrySelectMode(projected);Threshold.AcceptModeProjection(projected);}
            finally{_projecting=false;}
        }
        if(_sort.Setting is ConstantSetting{Value:string key}&&_supportedSort.Contains(key)
            &&_draft.CaptureFields().Read(Sort) is ConstantSetting{Value:string actual}&&actual!=key)
        {try{_projecting=true;_sort.TrySetConstant(actual);}finally{_projecting=false;}}
    }
    private void OnRaw(object sender,TextChangedEventArgs args)=>OnChanged();
    private void OnChanged(){if(Current&&!_projecting)Changed?.Invoke();}
    internal void RetireInteractions()
    {
        if(_disposed)return;
        var actions=new List<Action>{Threshold.RetireInteractions};actions.AddRange(_ranges.Select(range=>(Action)range.RetireInteractions));actions.AddRange(_created.Select(field=>(Action)field.RetireInteractions));
        var errors=new List<Exception>();foreach(var action in actions)try{action();}catch(Exception error){errors.Add(error);}
        if(errors.Count!=0)throw new AggregateException("运行参数预览弹层退役失败。",errors);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;_editor.Changed-=OnFormalChanged;Threshold.Changed-=OnThreshold;_sort.Changed-=OnSort;
        foreach(var range in _ranges)range.Changed-=OnChanged;
        foreach(var field in _created){field.Changed-=OnChanged;field.RemoveHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnRaw));}
    }
}
