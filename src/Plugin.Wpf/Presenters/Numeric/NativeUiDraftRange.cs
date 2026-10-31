using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Presenters.Numeric;

/// <summary>Original range composition with local values for the UI implementation stage.</summary>
public sealed class NativeUiDraftRange : Decorator,IDisposable
{
    private readonly DeclaredFieldState _lower,_upper;
    private readonly NativeRangePresenter _range;
    private readonly ParameterSetting _initialLower,_initialUpper;
    private readonly DeclaredFieldEditor _toggle;
    private bool _disposed;
    internal DeclaredFieldEditor Toggle=>_toggle;
    internal NativeRangePresenter Range=>_range;
    public NativeUiDraftRange(DeclaredFieldEditor toggle,string id,string label,double minimum,double maximum,double lower,double upper,bool integer)
    {
        _toggle=toggle;
        var type=integer?(TypeRef)DataType.Integer:DataType.Number;
        var lowPort=new Port(id+".lower",type,Storage:integer?NumericStorage.Int32:NumericStorage.Float64,
            EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
        var highPort=lowPort with{Id=id+".upper"};
        _initialLower=new ConstantSetting(integer?(object)(long)lower:lower);
        _initialUpper=new ConstantSetting(integer?(object)(long)upper:upper);
        _lower=new(lowPort,_initialLower,_=>{});_upper=new(highPort,_initialUpper,_=>{});
        var layout=new FieldLayout("运行参数","高级",Minimum:minimum,Maximum:maximum,DecimalPlaces:integer?0:2,Increment:integer?1:.01);
        var low=new FieldPresentation(lowPort.Id,label,"高级",0,FieldEditorKind.Scalar){Layout=layout};
        var high=new FieldPresentation(highPort.Id,label,"高级",1,FieldEditorKind.Scalar){Layout=layout};
        try{_range=new(_lower,_upper,low,high,_=>{},ReferenceFieldOptions.Empty,null);var panel=new StackPanel();panel.Children.Add(toggle);panel.Children.Add(_range);Child=panel;toggle.Changed+=Project;Project();}
        catch{_lower.Dispose();_upper.Dispose();throw;}
    }
    public bool HasChanges=>_range.HasPendingEdits(0)||_range.HasPendingEdits(1)
        ||_lower.HasInvalidInput||_upper.HasInvalidInput||_lower.IsReferenceMode||_upper.IsReferenceMode
        ||!Equals(_lower.Accepted,_initialLower)||!Equals(_upper.Accepted,_initialUpper);
    private void Project()=>_range.Visibility=_toggle.Setting is ConstantSetting{Value:true}?Visibility.Visible:Visibility.Collapsed;
    public void Dispose(){if(_disposed)return;_disposed=true;_toggle.Changed-=Project;Child=null;_toggle.Dispose();_range.Dispose();_lower.Dispose();_upper.Dispose();}
}
