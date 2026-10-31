using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Original result-judgment composition; values remain UI-local during S1.</summary>
public sealed class NativeJudgmentUiDraftControls : StackPanel,IDisposable
{
    private readonly List<IDisposable> _owned=[];
    private readonly List<FrameworkElement> _advancedRows=[];
    private readonly NativeAdvancedPresenter _advanced=new();
    internal IEnumerable<FrameworkElement> Rows=>Children.Cast<FrameworkElement>();
    private bool _disposed;
    public NativeJudgmentUiDraftControls()
    {
        AddSwitch("OKWhenNumIsZero",null,0,0,false,false);
        AddSwitch("Blob Number Check","Blob Number Range",1,99999,true,false);
        AddSwitch("Blob Area Check","Blob Area Range",1,999999999,false,false);
        AddSwitch("RunParam_Blob Total Area Check","RunParam_Blob Total Area Range",1,999999999,false,false);
        Children.Add(_advanced);_owned.Add(_advanced);_advanced.Changed+=Project;
        foreach(var (label,range,min,max) in new[]{
            ("Barycenter X Check","Barycenter X Range",-99999d,99999d),("Barycenter Y Check","Barycenter Y Range",-99999d,99999d),
            ("RunParam_BoxAngle Check","RunParam_BoxAngle Range",-180d,180d),("Center X Check","Center X Range",-99999d,99999d),
            ("Center Y Check","Center Y Range",-99999d,99999d),("Box Height Check","RunParam_Box Height Range",1d,99999d),
            ("RunParam_Box Width Check","RunParam_Box Width Range",1d,99999d),("Perimeter Check","Perimeter Range",1d,999999999d),
            ("Major Axis Check","Major Axis Range",1d,999999999d),("Minor Axis Check","Minor Axis Range",1d,999999999d),
            ("Circularity Check","Circularity Range",0d,1d),("Rectangularity Check","Rectangularity Range",0d,1d)})
            AddSwitch(label,range,min,max,false,true);
        Project();
    }
    private void AddSwitch(string label,string? range,double minimum,double maximum,bool integer,bool advanced)
    {
        var id="ui-only-judgment."+_owned.Count;
        var toggle=new DeclaredFieldEditor(new Port(id,DataType.Boolean,EditModes:FieldEditModes.Constant),
            AppLocalizationService.GetLang(label),new ConstantSetting(false),_=>{});
        FrameworkElement view=toggle;
        if(range is not null)
        {
            var pair=new NativeUiDraftRange(toggle,id,AppLocalizationService.GetLang(range),minimum,maximum,minimum,maximum,integer);
            _owned.Add(pair);view=pair;
        }
        else _owned.Add(toggle);
        Children.Add(view);if(advanced)_advancedRows.Add(view);
    }
    private void Project(){foreach(var row in _advancedRows)row.Visibility=_advanced.IsExpanded?Visibility.Visible:Visibility.Collapsed;}
    public void Dispose(){if(_disposed)return;_disposed=true;_advanced.Changed-=Project;Children.Clear();foreach(var item in _owned)item.Dispose();}
}
