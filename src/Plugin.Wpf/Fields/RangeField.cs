using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls;
namespace SunEyeVision.Plugin.Wpf.Fields;

internal sealed class RangeField : Grid
{
 private readonly DeclaredFieldEditor _minimum,_maximum;
 private readonly Popup _popup;
 private readonly RangeSlider _slider;
 private bool _refreshing;
 public RangeField(DeclaredFieldEditor minimum,DeclaredFieldEditor maximum,FieldLayout? layout)
 {
  _minimum=minimum;_maximum=maximum;maximum.HideLabel();
  minimum.UsePairedSlider();maximum.UsePairedSlider();
  minimum.SetLabel(minimum.FieldId); // caller supplies the human range label after composition.
  // The left editor contains the 89 px label; only the two input regions share the remaining width.
  ColumnDefinitions.Add(new());ColumnDefinitions.Add(new(){Width=GridLength.Auto});ColumnDefinitions.Add(new());
  SizeChanged+=(_,_)=>{var inputWidth=Math.Max(0,(ActualWidth-89-26)/2);ColumnDefinitions[0].Width=new GridLength(inputWidth+89);ColumnDefinitions[2].Width=new GridLength(inputWidth);};
  Children.Add(minimum);var separator=new TextBlock{Text="—",Margin=new Thickness(5,0,5,0),VerticalAlignment=VerticalAlignment.Center};SetColumn(separator,1);Children.Add(separator);SetColumn(maximum,2);Children.Add(maximum);
  _slider=new RangeSlider{Minimum=layout?.Minimum??0,Maximum=layout?.Maximum??9999999,Height=24,MinWidth=200};
  _popup=new Popup{PlacementTarget=this,Placement=PlacementMode.Bottom,StaysOpen=true,AllowsTransparency=true,
   Child=new Border{Background=System.Windows.Media.Brushes.White,BorderBrush=System.Windows.Media.Brushes.Gray,BorderThickness=new Thickness(1),Padding=new Thickness(8),Child=_slider}};
  GotKeyboardFocus+=(_,_)=>Refresh();LostKeyboardFocus+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{if(!IsKeyboardFocusWithin&&!_slider.IsKeyboardFocusWithin)_popup.IsOpen=false;}));
  PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){_popup.IsOpen=false;e.Handled=true;}};
  _slider.PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){_popup.IsOpen=false;e.Handled=true;}};
  Unloaded+=(_,_)=>_popup.IsOpen=false;
  minimum.Changed+=Refresh;maximum.Changed+=Refresh;
  _slider.MinValueChanged+=(_,e)=>Apply(minimum,e.NewValue);_slider.MaxValueChanged+=(_,e)=>Apply(maximum,e.NewValue);
 }
 private void Apply(DeclaredFieldEditor field,double value)
 {
  if(_refreshing||field.HasInvalidText)return;
  if(field.Setting is not ConstantSetting current)return;
  _refreshing=true;try{field.TrySetConstant(current.Value is long?(object)checked((long)Math.Round(value)):value);}finally{_refreshing=false;}
 }
 private void Refresh()
 {
  if(_refreshing)return;
  if(_minimum.HasInvalidText||_maximum.HasInvalidText||_minimum.Setting is not ConstantSetting low||_maximum.Setting is not ConstantSetting high)
  {_popup.IsOpen=false;return;}
  _refreshing=true;try{_slider.MinValue=Convert.ToDouble(low.Value,CultureInfo.InvariantCulture);_slider.MaxValue=Convert.ToDouble(high.Value,CultureInfo.InvariantCulture);}finally{_refreshing=false;}
  _popup.Width=ActualWidth;_popup.IsOpen=IsKeyboardFocusWithin||_slider.IsKeyboardFocusWithin;
 }
}
