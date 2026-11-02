using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Original zero-target policy and fifteen judgment ranges, window-local only.</summary>
public sealed class NativeJudgmentUiDraftControls : StackPanel,IDisposable,IParameterVisibility
{
    private readonly List<IDisposable> _owned=[];
    private readonly List<NativeUiDraftRange> _ranges=[];
    private readonly List<FrameworkElement> _advancedRows=[];
    private readonly NativeAdvancedPresenter _advanced=new();
    private readonly DeclaredFieldEditor _zero;
    internal IEnumerable<FrameworkElement> Rows=>Children.Cast<FrameworkElement>();
    private bool _disposed;
    public event Action? Changed;
    public bool HasPendingEdits=>!_disposed&&(_zero.HasPendingEdits||_ranges.Any(range=>range.HasPendingEdits));
    public bool HasInvalidInput=>!_disposed&&(_zero.HasInvalidText||_ranges.Any(range=>range.HasInvalidInput));
    public void RetireInteractions()
    {if(_disposed)return;ParameterCleanup.Run(new Action[]{()=>((IParameterVisibility)this).PrepareForVisibility(false),()=>{if(!_disposed&&IsVisible)((IParameterVisibility)this).PrepareForVisibility(true);}});}
    public bool HasUiChanges=>!_disposed&&(_zero.Setting is not ConstantSetting{Value:false}||_zero.HasPendingEdits||_ranges.Any(range=>range.HasChanges));
    public NativeJudgmentUiDraftControls()
    {
        _zero=new(new Port("ui-only-judgment.zero",DataType.Boolean,EditModes:FieldEditModes.Constant),AppLocalizationService.GetLang("OKWhenNumIsZero"),new ConstantSetting(false),_=>{});
        Children.Add(_zero);_owned.Add(_zero);_zero.Changed+=OnChanged;
        AddSwitch("Blob Number Check","Blob Number Range",1,99999,true,false,1,0,1);
        AddSwitch("Blob Area Check","Blob Area Range",1,999999999,false,false,increment:1);
        AddSwitch("RunParam_Blob Total Area Check","RunParam_Blob Total Area Range",1,999999999,false,false,increment:1);
        Children.Add(_advanced);_owned.Add(_advanced);_advanced.Changed+=Project;
        foreach(var (label,range,min,max,inc) in new[]{
            ("Barycenter X Check","Barycenter X Range",-99999d,99999d,1d),("Barycenter Y Check","Barycenter Y Range",-99999d,99999d,1d),
            ("RunParam_BoxAngle Check","RunParam_BoxAngle Range",-180d,180d,.1d),("Center X Check","Center X Range",-99999d,99999d,1d),
            ("Center Y Check","Center Y Range",-99999d,99999d,1d),("Box Height Check","RunParam_Box Height Range",1d,99999d,1d),
            ("RunParam_Box Width Check","RunParam_Box Width Range",1d,99999d,1d),("Perimeter Check","Perimeter Range",1d,999999999d,1d),
            ("Major Axis Check","Major Axis Range",1d,999999999d,1d),("Minor Axis Check","Minor Axis Range",1d,999999999d,1d),
            ("Circularity Check","Circularity Range",0d,1d,.01d),("Rectangularity Check","Rectangularity Range",0d,1d,.01d)})
            AddSwitch(label,range,min,max,false,true,increment:inc);
        IsVisibleChanged+=OnVisible;Project();
    }
    private void AddSwitch(string label,string range,double minimum,double maximum,bool integer,bool advanced,
        double? lowerMinimum=null,double? upperMinimum=null,double? increment=null)
    {
        var id="ui-only-judgment."+_ranges.Count;
        var toggle=new DeclaredFieldEditor(new Port(id,DataType.Boolean,EditModes:FieldEditModes.Constant),AppLocalizationService.GetLang(label),new ConstantSetting(false),_=>{});
        var pair=new NativeUiDraftRange(toggle,id,AppLocalizationService.GetLang(range),minimum,maximum,minimum,maximum,integer,lowerMinimum,upperMinimum,increment);
        _owned.Add(pair);_ranges.Add(pair);pair.Changed+=OnChanged;Children.Add(pair);if(advanced)_advancedRows.Add(pair);
    }
    private void OnChanged(){if(!_disposed)Changed?.Invoke();}
    private void Project()
    {ParameterCleanup.Run(_advancedRows.Select(row=>(Action)(()=>{(row as IParameterVisibility)?.PrepareForVisibility(_advanced.IsExpanded&&IsVisible);row.Visibility=_advanced.IsExpanded?Visibility.Visible:Visibility.Collapsed;})));}
    private void OnVisible(object sender,DependencyPropertyChangedEventArgs args)=>((IParameterVisibility)this).PrepareForVisibility(IsVisible);
    void IParameterVisibility.PrepareForVisibility(bool visible)
    {ParameterCleanup.Run(Children.OfType<IParameterVisibility>().Select(row=>(Action)(()=>row.PrepareForVisibility(visible&&(row as FrameworkElement)?.Visibility==Visibility.Visible))));}
    public void Dispose()
    {if(_disposed)return;_disposed=true;Changed=null;_zero.Changed-=OnChanged;_advanced.Changed-=Project;IsVisibleChanged-=OnVisible;foreach(var range in _ranges)range.Changed-=OnChanged;ParameterCleanup.Run(new Action[]{()=>Children.Clear()}.Concat(_owned.Select(item=>(Action)item.Dispose)));}
}
