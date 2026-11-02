using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using Numbers=SunEyeVision.Plugin.Wpf.Native.Numeric.Arranged;

namespace SunEyeVision.Plugin.Wpf.Presenters.Numeric;

/// <summary>Owns one original input; the nonvisual coordinator borrows its state and visual.</summary>
internal sealed class NativeNumberPresenter : Decorator,IDisposable
{
    private readonly NativeNumberCoordinator _coordinator;
    public static bool UsesOriginalModes(Port port,FieldPresentation? presentation,bool numericSequence=false)
        =>numericSequence&&NativeNumericSequenceProjection.Applies(port)||port.Type.Shape==ValueShape.Scalar&&(port.Type.Scalar==ScalarKind.Int64
            ||port.Type.Scalar==ScalarKind.Float64&&presentation?.Layout is {Minimum:not null,Maximum:not null});


    public TextBox? TextEditor=>_coordinator.TextEditor;
    internal long EditVersion=>_coordinator.EditVersion;
    internal void RestoreUiProjectionRaw(NumericPendingEdit raw){_coordinator.RestorePending(raw);}
    internal bool HasPendingEdits=>_coordinator.HasPendingEdits;
    public NativeNumberPresenter(DeclaredFieldState state,FieldPresentation? presentation,ReferenceFieldOptions? references,Func<OutputReference,string>? format,bool numericSequence=false)
    {
        var layout=presentation?.Layout;
        Language=XmlLanguage.GetLanguage("en-US");Height=32;
        FrameworkElement input;
        if(UsesOriginalModes(state.Port,presentation,numericSequence))
        {
            double minimum,maximum;
            if(layout is {Minimum:not null,Maximum:not null}){minimum=layout.Minimum.Value;maximum=layout.Maximum.Value;}
            else if(numericSequence){minimum=-double.MaxValue;maximum=double.MaxValue;}
            else
            {
                var range=NumericStorageRules.IntegerRange(NumericStorageRules.Effective(state.Port.Type,state.Port.Storage)!.Value);
                minimum=range.Minimum;maximum=range.Maximum;
            }
            Numbers.SliderArrangeBase arrange=state.Port.Type.Scalar==ScalarKind.Int64?new Numbers.SliderArrange():new Numbers.SliderArrangeFloat{DecimalDigits=layout?.DecimalPlaces??2};
            arrange.Minimum=minimum;arrange.Maximum=maximum;
            arrange.MinimumLeft=minimum;arrange.MaximumLeft=maximum;
            arrange.MinimumRight=minimum;arrange.MaximumRight=maximum;
            arrange.SliderTickFrequency=layout?.Increment??1;arrange.EndValue=maximum;
            input=arrange;
        }
        else input=new Numbers.NumericControl{Minimum=layout?.Minimum??-double.MaxValue,Maximum=layout?.Maximum??double.MaxValue,Increment=layout?.Increment??1};
        input.SetValue(Control.ForegroundProperty,new SolidColorBrush(Color.FromRgb(102,102,102)));
        input.SetValue(Control.FontSizeProperty,14d);Child=input;
        try{_coordinator=new(state,presentation,this,input,references,format);}
        catch{Child=null;throw;}
    }
    public bool TrySetConstant(object value)=>_coordinator.TrySetConstant(value);
    public bool TryCommitPending(bool retryAccepted=false)=>_coordinator.TryCommitPending(retryAccepted);
    public void ProjectAccepted()=>_coordinator.ProjectAccepted();
    internal void PrepareForVisibility(bool visible)=>_coordinator.PrepareForVisibility(visible);
    public void Dispose(){try{_coordinator.Dispose();}finally{Child=null;}}
}
