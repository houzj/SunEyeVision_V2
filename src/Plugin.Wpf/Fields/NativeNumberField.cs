using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using PcUiNumbers = SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Neutral literal adapter over the original PCUI numeric controls.
/// The caller owns the draft; this view owns only raw editing text and validation.
/// Int64 values that cannot travel through double are handled by the exact text field.
/// </summary>
public sealed class NativeNumberField : Decorator
{
    private readonly Port _port;
    private readonly FieldLayout? _layout;
    private readonly Func<object,bool> _write;
    private readonly PcUiNumbers.SliderArrangeBase? _arrange;
    private PcUiNumbers.NumericControl? _number;
    private ComboBox? _box;
    private Popup? _popup;
    private object _literal;
    private bool _internal;
    private bool _active;
    private bool _edited;
    private bool _showSlider;
    private bool _pairedRange;
    private string _error="";
    public TextBox? TextEditor { get; private set; }
    public FrameworkElement NativeControl { get; }
    public PcUiNumbers.NumericControl? InputControl=>_number;
    public event Action? InputReady;
    public event Action? ValidationChanged;
    public bool ShowSlider
    {
        get=>_showSlider;
        set{_showSlider=value;ApplySliderVisibility();if(!value)ClosePopup();}
    }
    public void UsePairedRange()
    {
        _pairedRange=true;
        ShowSlider=false;
        ApplyInputInsets();
    }

    public NativeNumberField(Port port,FieldLayout? layout,object literal,Func<object,bool> write)
    {
        _port=port;_layout=layout;_literal=literal;_write=write;
        var bounded=layout?.Minimum is not null&&layout.Maximum is not null;
        _showSlider=bounded;
        if(bounded)
        {
            _arrange=port.Type.Scalar==ScalarKind.Int64?new PcUiNumbers.SliderArrange():new PcUiNumbers.SliderArrangeFloat
                {DecimalDigits=layout!.DecimalPlaces};
            // Existing stored values are displayed exactly, even if a newer VM
            // editor range is narrower. Only a new edit must enter that range.
            var stored=Convert.ToDouble(literal,CultureInfo.InvariantCulture);
            _arrange.Minimum=Math.Min(layout!.Minimum!.Value,stored);
            _arrange.Maximum=Math.Max(layout.Maximum!.Value,stored);
            _arrange.MinimumLeft=_arrange.Minimum;_arrange.MaximumLeft=_arrange.Maximum;
            _arrange.EndValue=_arrange.Maximum;_arrange.SliderTickFrequency=layout.Increment;
            _arrange.StartValue=Convert.ToDouble(literal,CultureInfo.InvariantCulture);
            _arrange.IsDynamicValue=true;
            _arrange.OnStartValueChange+=(_,_)=>OnNativeValue();
            NativeControl=_arrange;
        }
        else
        {
            // The same original leaf used inside SliderArrange, without an
            // invented slider range for an unbounded declaration.
            _number=new PcUiNumbers.NumericControl{Minimum=-double.MaxValue,Maximum=double.MaxValue,
                Increment=layout?.Increment??1};
            _number.OnNumericClick+=(_,_)=>OnLeafValue();
            NativeControl=_number;
        }
        NativeControl.SetValue(Control.ForegroundProperty,new SolidColorBrush(Color.FromRgb(102,102,102)));
        NativeControl.SetValue(Control.FontSizeProperty,14d);
        Child=NativeControl;Height=32;Language=XmlLanguage.GetLanguage("en-US");
        Loaded+=(_,_)=>
        {
            _active=true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(AttachInput));
        };
        Unloaded+=(_,_)=>{_active=false;ClosePopup();};
    }

    private void AttachInput()
    {
        if(!_active||!IsLoaded)return;
        NativeControl.ApplyTemplate();
        if(_arrange is not null)
        {
            _box=_arrange.FindName("CBox") as ComboBox
                ??throw new InvalidOperationException("原数值控件缺少CBox名称域入口。");
            _box.ApplyTemplate();
            _popup=_box.Template.FindName("PART_Popup",_box) as Popup;
            _number=_box.Template.FindName("T1",_box) as PcUiNumbers.NumericControl
                ??throw new InvalidOperationException("原数值控件CBox模板缺少T1输入。");
        }
        var input=(TextBox)_number!.FindName("ValueText");
        if(!ReferenceEquals(TextEditor,input))
        {
            if(TextEditor is not null)DetachInput(TextEditor);
            TextEditor=input;
            input.Style=null;input.MinHeight=0;input.Padding=new Thickness(0);
            ApplyInputInsets();
            if(_arrange is not null)
                _number.SetBinding(PcUiNumbers.NumericControl.TextProperty,new Binding("StartValue")
                    {Source=_arrange,Mode=BindingMode.TwoWay,ConverterCulture=CultureInfo.InvariantCulture,StringFormat="{0:R}"});
            // Native arrow/slider display binding remains. Raw text is proposed
            // to the typed draft first; LostFocus must not bypass its validator.
            BindingOperations.SetBinding(input,TextBox.TextProperty,new Binding("Text")
                {Source=_number,Mode=BindingMode.OneWay,ConverterCulture=CultureInfo.InvariantCulture});
            input.TextChanged+=OnTextChanged;input.PreviewKeyDown+=OnKey;
            input.PreviewMouseWheel+=OnWheel;input.LostKeyboardFocus+=OnFocusLost;
        }
        SetLiteral(_literal);
        ApplySliderVisibility();
        InputReady?.Invoke();
    }
    private void ApplyInputInsets()
    {
        // Two declared editors share one parameter row. Reclaim the standalone
        // leaf's extra left inset in this host only, keeping the native border,
        // spinner and font so seven-digit endpoints remain fully visible.
        if(_pairedRange&&TextEditor is not null)
            TextEditor.Margin=new Thickness(2,4,2,3);
    }
    private void DetachInput(TextBox input)
    {
        input.TextChanged-=OnTextChanged;input.PreviewKeyDown-=OnKey;
        input.PreviewMouseWheel-=OnWheel;input.LostKeyboardFocus-=OnFocusLost;
    }
    private string Format(object value)
    {
        if(_port.Type.Scalar==ScalarKind.Int64)return Convert.ToString(value,CultureInfo.InvariantCulture)!;
        var number=Convert.ToDouble(value,CultureInfo.InvariantCulture);var digits=_layout?.DecimalPlaces??2;
        return number.ToString(Math.Round(number,digits)==number?"F"+digits:"R",CultureInfo.InvariantCulture);
    }
    public void SetLiteral(object value)
    {
        _internal=true;
        try
        {
            _literal=value;_edited=false;_error="";
            if(_arrange is not null)_arrange.StartValue=Convert.ToDouble(value,CultureInfo.InvariantCulture);
            else _number!.SetCurrentValue(PcUiNumbers.NumericControl.TextProperty,Format(value));
            TextEditor?.SetCurrentValue(TextBox.TextProperty,Format(value));
        }
        finally{_internal=false;}
    }
    public bool TryValidatePendingEdit(out string error){error=_error;return error.Length==0;}
    private bool Read(string text,out object value,out string error)
    {
        error="";value=0d;
        if(_port.Type.Scalar==ScalarKind.Int64)
        {
            if(!long.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var integer))
            {error="请输入有效的精确整数。";return false;}
            value=integer;
        }
        else
        {
            if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var floating)||!double.IsFinite(floating))
            {error="请输入有限数值。";return false;}
            value=floating;
        }
        try{ValueRules.Freeze(_port,value);}
        catch(Exception e) when(e is ArgumentException or OverflowException)
        {error=e.Message;return false;}
        var number=Convert.ToDouble(value,CultureInfo.InvariantCulture);
        if((_layout?.Minimum is {} minimum&&number<minimum)||(_layout?.Maximum is {} maximum&&number>maximum))
        {error="数值超出允许范围。";return false;}
        return true;
    }
    private void OnTextChanged(object sender,TextChangedEventArgs args)
    {
        if(_internal||!_active)return;
        _edited=true;var previouslyInvalid=_error.Length>0;
        var raw=TextEditor!.Text;var caret=TextEditor.CaretIndex;var selection=TextEditor.SelectionLength;
        if(!Read(raw,out var value,out _error)){ValidationChanged?.Invoke();return;}
        if(!previouslyInvalid&&Convert.ToDouble(value,CultureInfo.InvariantCulture)==Convert.ToDouble(_literal,CultureInfo.InvariantCulture))
            return;
        if(!_write(value)){_error="当前编辑值未被草稿接纳。";ValidationChanged?.Invoke();return;}
        _internal=true;
        try
        {
            _literal=value;
            if(_arrange is not null)_arrange.StartValue=Convert.ToDouble(value,CultureInfo.InvariantCulture);
            else _number!.SetCurrentValue(PcUiNumbers.NumericControl.TextProperty,raw);
            // Keep the original two-way binding and the user's text/caret, even
            // when the native float binding prefers a formatted display value.
            TextEditor.SetCurrentValue(TextBox.TextProperty,raw);
            TextEditor.Select(Math.Min(caret,raw.Length),Math.Min(selection,Math.Max(0,raw.Length-caret)));
        }
        finally{_internal=false;}
        ValidationChanged?.Invoke();
    }
    private void OnNativeValue()
    {
        if(_internal||!_active||TextEditor is null||_error.Length>0)return;
        if(_arrange!.StartValue==Convert.ToDouble(_literal,CultureInfo.InvariantCulture))return;
        PublishNumber(_arrange!.StartValue);
    }
    private void OnLeafValue()
    {
        if(_internal||!_active||TextEditor is null||_error.Length>0)return;
        if(double.TryParse(_number!.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)
            &&value!=Convert.ToDouble(_literal,CultureInfo.InvariantCulture))PublishNumber(value);
    }
    private void PublishNumber(double number)
    {
        var text=number.ToString("R",CultureInfo.InvariantCulture);
        if(!Read(text,out var value,out _error)){ValidationChanged?.Invoke();return;}
        if(!_write(value)){_error="当前编辑值未被草稿接纳。";ValidationChanged?.Invoke();return;}
        SetLiteral(value);_edited=true;ValidationChanged?.Invoke();
    }
    private void OnKey(object sender,KeyEventArgs args)
    {
        if(args.Key==Key.Escape){var opened=_box?.IsDropDownOpen==true;ClosePopup();args.Handled=opened;return;}
        if(args.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
        {
            args.Handled=true;if(_error.Length>0)return;
            var step=_layout?.Increment??1;
            var value=Convert.ToDouble(_literal,CultureInfo.InvariantCulture);
            value=args.Key switch {Key.Home=>_layout?.Minimum??value,Key.End=>_layout?.Maximum??value,
                Key.Up or Key.PageUp=>value+step,_=>value-step};
            value=Math.Round(value,_port.Type.Scalar==ScalarKind.Int64?0:_layout?.DecimalPlaces??2);
            if(_layout?.Minimum is {} min)value=Math.Max(min,value);
            if(_layout?.Maximum is {} max)value=Math.Min(max,value);
            PublishNumber(value);return;
        }
        if(args.Key!=Key.Enter&&args.Key!=Key.Return)return;
        args.Handled=true;
        if(_error.Length>0)return;
        ClosePopup();
        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(TextEditor!),null);
        Keyboard.ClearFocus();
    }
    private void OnWheel(object sender,MouseWheelEventArgs args)
    {
        // Original NumericControl handles a focused wheel. The adapter only
        // observes it after its handler, preserving native arrow/step behavior.
        if(TextEditor?.IsKeyboardFocused!=true)return;
        Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>{if(_active)OnLeafValue();}));
    }
    private void OnFocusLost(object sender,KeyboardFocusChangedEventArgs args)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>
        {
            if(!_active)return;
            if(!IsKeyboardFocusWithin&&_popup?.Child?.IsKeyboardFocusWithin!=true)ClosePopup();
            if(!_edited)SetLiteral(_literal);
        }));
    }
    private void ApplySliderVisibility()
    {
        if(_arrange is PcUiNumbers.SliderArrangeFloat floating)floating.NeedSlider=_showSlider;
        if(_arrange is not null)_arrange.SliderVisibility=_showSlider?Visibility.Visible:Visibility.Collapsed;
    }
    public void ClosePopup()
    {
        if(_box is not null)_box.IsDropDownOpen=false;
        if(_number is not null)_number.IsFocusedP=false;
    }
}
