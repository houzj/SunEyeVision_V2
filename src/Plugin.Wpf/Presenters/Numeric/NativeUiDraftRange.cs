using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Presenters.Numeric;

/// <summary>Original range composition over window-local, non-persisted UI values.</summary>
public sealed class NativeUiDraftRange : Decorator,IDisposable,IParameterVisibility
{
    private readonly DeclaredFieldState _lower,_upper;
    private readonly NativeRangePresenter _range;
    private readonly ParameterSetting _initialLower,_initialUpper,_initialToggle;
    private readonly DeclaredFieldEditor _toggle;
    private bool _disposed;
    internal DeclaredFieldEditor Toggle=>_toggle;
    internal NativeRangePresenter Range=>_range;
    public event Action? Changed;
    public NativeUiDraftRange(DeclaredFieldEditor toggle,string id,string label,double minimum,double maximum,double lower,double upper,bool integer,
        double? lowerMinimum=null,double? upperMinimum=null,double? increment=null)
    {
        _toggle=toggle;_initialToggle=toggle.Setting;
        var type=integer?(TypeRef)DataType.Integer:DataType.Number;
        var lowPort=new Port(id+".lower",type,Storage:integer?NumericStorage.Int32:NumericStorage.Float64,
            EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
        var highPort=lowPort with{Id=id+".upper"};
        _initialLower=new ConstantSetting(integer?(object)(long)lower:lower);
        _initialUpper=new ConstantSetting(integer?(object)(long)upper:upper);
        _lower=new(lowPort,_initialLower,_=>{});_upper=new(highPort,_initialUpper,_=>{});
        var layout=new FieldLayout("运行参数","高级",Minimum:minimum,Maximum:maximum,DecimalPlaces:integer?0:2,Increment:increment??(integer?1:.01));
        var low=new FieldPresentation(lowPort.Id,label,"高级",0,FieldEditorKind.Scalar){Layout=layout with{Minimum=lowerMinimum??minimum}};
        var high=new FieldPresentation(highPort.Id,label,"高级",1,FieldEditorKind.Scalar){Layout=layout with{Minimum=upperMinimum??minimum}};
        try
        {
            _range=new(_lower,_upper,low,high,_=>{},ReferenceFieldOptions.Empty,null);
            var panel=new StackPanel();panel.Children.Add(toggle);panel.Children.Add(_range);Child=panel;
            toggle.Changed+=Project;_lower.Changed+=OnChanged;_upper.Changed+=OnChanged;
            AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnRawChanged));
            IsVisibleChanged+=OnVisible;Project();
        }
        catch{_lower.Dispose();_upper.Dispose();throw;}
    }
    public bool HasPendingEdits=>!_disposed&&(_toggle.HasPendingEdits||_range.HasPendingEdits(0)||_range.HasPendingEdits(1));
    public bool HasInvalidInput=>!_disposed&&(_toggle.HasInvalidText||_lower.HasInvalidInput||_upper.HasInvalidInput);
    public void RetireInteractions()
    {if(_disposed)return;((IParameterVisibility)this).PrepareForVisibility(false);if(IsVisible)((IParameterVisibility)this).PrepareForVisibility(true);}
    public bool HasChanges=>!_disposed&&(_range.HasPendingEdits(0)||_range.HasPendingEdits(1)
        ||_lower.HasInvalidInput||_upper.HasInvalidInput||_lower.IsReferenceMode||_upper.IsReferenceMode
        ||!DeclaredFieldState.SameSetting(_toggle.Setting,_initialToggle)
        ||!DeclaredFieldState.SameSetting(_lower.Accepted,_initialLower)||!DeclaredFieldState.SameSetting(_upper.Accepted,_initialUpper));
    private void OnChanged(){if(!_disposed)Changed?.Invoke();}
    private void OnRawChanged(object sender,TextChangedEventArgs args)=>OnChanged();
    private void Project()
    {
        var visible=_toggle.Setting is ConstantSetting{Value:true};
        ((IParameterVisibility)_range).PrepareForVisibility(visible&&IsVisible);
        _range.Visibility=visible?Visibility.Visible:Visibility.Collapsed;OnChanged();
    }
    private void OnVisible(object sender,DependencyPropertyChangedEventArgs args)=>((IParameterVisibility)this).PrepareForVisibility(IsVisible);
    void IParameterVisibility.PrepareForVisibility(bool visible)
    {ParameterCleanup.Run(new Action[]{()=>((IParameterVisibility)_toggle).PrepareForVisibility(visible),()=>((IParameterVisibility)_range).PrepareForVisibility(visible&&_range.Visibility==Visibility.Visible)});}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;_toggle.Changed-=Project;_lower.Changed-=OnChanged;_upper.Changed-=OnChanged;
        IsVisibleChanged-=OnVisible;RemoveHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnRawChanged));
        ParameterCleanup.Run(new Action[]{()=>Child=null,_toggle.Dispose,_range.Dispose,_lower.Dispose,_upper.Dispose});
    }
}
