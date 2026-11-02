using System.IO;
using System.Reflection;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using Native=SunEyeVision.Plugin.Wpf.Native.Numeric.Arranged;
internal static class S2DocumentEditingCases
{
 static readonly List<object> Rows=new();
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static void Case(string name,Action action){try{action();Rows.Add(new{name,passed=true});Console.WriteLine("通过："+name);}catch(Exception e){Rows.Add(new{name,passed=false,error=e.ToString()});Console.Error.WriteLine("失败："+name+" "+e);}}
 static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
 static IEnumerable<T> Visuals<T>(DependencyObject root)where T:DependencyObject{if(root is T item)yield return item;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Visuals<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
 static object? Get(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(o)??o.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(o);
 static readonly Type Policy=typeof(DeclaredFieldEditor).Assembly.GetType("SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeNumericTextProjection")!;
 static string Format(double value,int digits)=>(string)Policy.GetMethod("Format",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{value,digits})!;
 static double Parse(string text)=>double.Parse(text,NumberStyles.Float,CultureInfo.InvariantCulture);
 [STAThread] static int Main()
 {
  foreach(var culture in new[]{"zh-CN","en-US","de-DE","fr-FR"})foreach(var digits in new[]{0,2,3,9})Case("原普通格式与有限回退 "+culture+"/"+digits,()=>
  {
   var before=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
   foreach(var value in new[]{0d,-0d,.125,-1.25,37d,Math.BitDecrement(double.MaxValue),double.MaxValue,-double.MaxValue,1.234567890123456E300,9007199254740992d})
   {var normalized=Math.Round(value,digits,MidpointRounding.ToEven);var text=Format(value,digits);Require(!text.Contains('E')&&!text.Contains('e')&&double.IsFinite(Parse(text))&&Parse(text)==normalized,"回退不有限精确往返");var original=normalized.ToString("0."+new string('0',digits),CultureInfo.InvariantCulture);if(double.TryParse(original,NumberStyles.Float,CultureInfo.InvariantCulture,out var parsed)&&double.IsFinite(parsed)&&parsed==normalized)Require(text==original,"普通原格式被改变");}
   }finally{CultureInfo.CurrentCulture=before;}
  });
  Case("非有限源禁止投影",()=>{foreach(var value in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity}){var rejected=false;try{Format(value,2);}catch(TargetInvocationException e)when(e.InnerException is ArgumentException){rejected=true;}Require(rejected,"非有限值获文本");}});
  if(Environment.GetEnvironmentVariable("S4_RANGE_LOADED")=="1")
  {
   var app=Application.Current??new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};foreach(var r in new[]{"Shell/Resources/AppResources.xaml","Themes/Dark/DarkTheme.xaml"})app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+r)});
   foreach(var culture in new[]{"zh-CN","en-US","de-DE","fr-FR"})foreach(var digits in new[]{0,2,9})
   {
    Case("Loaded单值及原外层绑定 "+culture+"/"+digits,()=>Scalar(culture,digits));
    Case("Loaded双端及原绑定编辑 "+culture+"/"+digits,()=>Range(culture,digits));
   }
   Case("稳定Track四向极大域及原Thumb实际提交",VerifyThumb);
   app.Shutdown();
  }
  File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"range-roundtrip-report.json"),JsonSerializer.Serialize(Rows,new JsonSerializerOptions{WriteIndented=true}));return Rows.All(x=>(bool)x.GetType().GetProperty("passed")!.GetValue(x)!)?0:1;
 }
 static FieldPresentation Presentation(string id,int digits,string? pair=null)=>new(id,id,"范围",0,FieldEditorKind.Scalar){Layout=new("参数","范围",PairWith:pair,Minimum:-double.MaxValue,Maximum:double.MaxValue,DecimalPlaces:digits)};
 static void VerifyThumb()
 {
  foreach(var orientation in new[]{Orientation.Horizontal,Orientation.Vertical})foreach(var reversed in new[]{false,true})
  {var track=new SunEyeVision.Plugin.Wpf.Presenters.Numeric.StableSliderTrack{Minimum=0,Maximum=double.MaxValue,Value=double.MaxValue/4,Orientation=orientation,IsDirectionReversed=reversed,Thumb=new Thumb{Width=12,Height=12},DecreaseRepeatButton=new RepeatButton(),IncreaseRepeatButton=new RepeatButton()};track.Measure(new Size(240,240));track.Arrange(new Rect(0,0,240,240));var flip=reversed^(orientation==Orientation.Vertical);var first=orientation==Orientation.Horizontal?new Point(6,10):new Point(10,6);var last=orientation==Orientation.Horizontal?new Point(234,10):new Point(10,234);Require(track.ValueFromPoint(first)==(flip?double.MaxValue:0)&&track.ValueFromPoint(last)==(flip?0:double.MaxValue)&&double.IsFinite(track.ValueFromDistance(10000,10000)),"四向极大域映射失败");}
  var writes=0;FieldPresentation P(string id,string? pair=null)=>new(id,id,"范围",0,FieldEditorKind.Scalar){Layout=new("参数","范围",PairWith:pair,Minimum:0,Maximum:double.MaxValue,DecimalPlaces:0)};
  using var fields=new DeclaredFieldsView(new[]{(new Port("low",DataType.Number),P("low","high"),(ParameterSetting)new ConstantSetting(10d)),(new Port("high",DataType.Number),P("high"),(ParameterSetting)new ConstantSetting(20d))},_=>writes++);var window=new Window{Content=fields,Width=420,Height=200};window.Show();Pump();
  try{Require(fields.Fields[1].TrySetConstant(double.MaxValue),"原失败极大上限未接纳："+fields.Fields[1].Error);writes=0;var native=Visuals<Native.SliderArrangeFloat>(fields).Single();var combo=(ComboBox)native.FindName("CBox");combo.IsDropDownOpen=true;Pump();var popup=(Popup)combo.Template.FindName("PART_Popup",combo);var slider=Visuals<Slider>(popup.Child).Single(x=>x.Name=="SL_Bat1");slider.ApplyTemplate();var track=(Track)slider.Template.FindName("PART_Track",slider);Require(track is SunEyeVision.Plugin.Wpf.Presenters.Numeric.StableSliderTrack&&track.ActualWidth>0,"原真实模板缺稳定Track");track.Thumb.RaiseEvent(new DragDeltaEventArgs(100,0){RoutedEvent=Thumb.DragDeltaEvent});Pump();track.Thumb.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});Pump();Require(writes==1&&native.StartValue>20&&double.IsFinite(native.StartValue)&&fields.Fields[0].Setting is ConstantSetting {Value:double current}&&current==native.StartValue,"原Thumb未单次消费有限值");Require(double.IsFinite(native.StartRectClip.Bounds.Width)&&double.IsFinite(native.EndRectClip.Bounds.X),"双轨裁剪溢出");}finally{window.Close();Pump();}
 }
 static void Scalar(string culture,int digits)
 {
  var previous=CultureInfo.CurrentCulture;CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);try{
   var writes=0;using var field=new DeclaredFieldEditor(new Port("value",DataType.Number),"值",new ConstantSetting(20d),_=>writes++,presentation:Presentation("value",digits));var window=new Window{Content=field,Width=420,Height=200};window.Show();Pump();
   try{var native=Visuals<Native.SliderArrangeFloat>(field).Single();var leaf=(Native.NumericControl)((ComboBox)native.FindName("CBox")).Template.FindName("T1",(ComboBox)native.FindName("CBox"));var outer=leaf.GetBindingExpression(Native.NumericControl.TextProperty);var text=(TextBox)field.InputElement!;var inner=text.GetBindingExpression(TextBox.TextProperty);
    Require(field.TrySetConstant(double.MaxValue),"单值极大值拒绝："+field.Error);Require(writes==1&&Parse(text.Text)==double.MaxValue,"单值极值未接纳");
    text.Focus();Pump();text.SelectAll();text.SelectedText=(-double.MaxValue).ToString("F"+digits,CultureInfo.InvariantCulture);Require(field.TryCommitPending(),"单值真实文本替换不能重新提交："+field.Error);Require(writes==2&&field.Setting is ConstantSetting {Value:double d}&&d==-double.MaxValue,"单值替换半写");
    Require(ReferenceEquals(outer,leaf.GetBindingExpression(Native.NumericControl.TextProperty))&&ReferenceEquals(inner,text.GetBindingExpression(TextBox.TextProperty)),"事务替换了绑定");text.Text="Infinity";Require(!field.TryCommitPending()&&writes==2,"Infinity经ConvertBack误接纳");
   }finally{window.Close();Pump();}
  }finally{CultureInfo.CurrentCulture=previous;}
 }
 static void Range(string culture,int digits)
 {
  var previous=CultureInfo.CurrentCulture;CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);try{
   var writes=0;using var fields=new DeclaredFieldsView(new[]{(new Port("low",DataType.Number),Presentation("low",digits,"high"),(ParameterSetting)new ConstantSetting(10d)),(new Port("high",DataType.Number),Presentation("high",digits),(ParameterSetting)new ConstantSetting(20d))},_=>writes++);var window=new Window{Content=fields,Width=420,Height=200};window.Show();Pump();
   try{var native=Visuals<Native.SliderArrangeFloat>(fields).Single();var box=(ComboBox)native.FindName("CBox");var left=(Native.NumericControl)box.Template.FindName("T1",box);var right=(Native.NumericControl)box.Template.FindName("T2",box);var bindings=new[]{left.GetBindingExpression(Native.NumericControl.TextProperty),right.GetBindingExpression(Native.NumericControl.TextProperty)};
    Require(fields.Fields[1].TrySetConstant(double.MaxValue),"极大上限未接纳："+fields.Fields[1].Error);Require(fields.Fields[0].TrySetConstant(-double.MaxValue),"极小下限未接纳："+fields.Fields[0].Error);Require(writes==2,"端点未单次接纳");
    var texts=new[]{(TextBox)fields.Fields[0].InputElement!,(TextBox)fields.Fields[1].InputElement!};Require(Parse(texts[0].Text)==-double.MaxValue&&Parse(texts[1].Text)==double.MaxValue,"双端原投影不有限");
    texts[1].Focus();Pump();texts[1].SelectAll();texts[1].SelectedText=Math.BitDecrement(double.MaxValue).ToString("F"+digits,CultureInfo.InvariantCulture);Require(fields.Fields[1].TryCommitPending(),"真实端点替换不能重新提交："+fields.Fields[1].Error);Require(fields.Fields[1].Setting is ConstantSetting {Value:double d}&&d==Math.BitDecrement(double.MaxValue),"端点重新编辑未保精度");
    Require(ReferenceEquals(bindings[0],left.GetBindingExpression(Native.NumericControl.TextProperty))&&ReferenceEquals(bindings[1],right.GetBindingExpression(Native.NumericControl.TextProperty)),"两端事务替换原绑定");texts[1].Text="Infinity";var before=writes;Require(!fields.TryValidate(out _)&&writes==before,"非法整组半写");
   }finally{window.Close();Pump();}
  }finally{CultureInfo.CurrentCulture=previous;}
 }
}

