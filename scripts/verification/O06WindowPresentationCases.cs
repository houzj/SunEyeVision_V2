using SunEyeVision.Plugin.Wpf.Adapters.References;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Wpf.Controls;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using NumericUpDown=SunEyeVision.Plugin.Wpf.Controls.NumericUpDown;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;
using Native=SunEyeVision.Plugin.Wpf.Native;

internal static class S2DocumentEditingCases
{
 static Window owner=null!;static StackPanel host=null!;static int passed,failed;
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static void Test(string name,Action run){try{run();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e);}}
 static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
 sealed class ColorLifetimeDraft : IToolSettingsDraft
 {
  private Dictionary<string,object?> _values=new(){["visible"]=true,["rgba"]=PresentationDeclarations.Color(10,20,30,200)};
  public int Writes {get;private set;}
  public ToolSchema Schema {get;}=new(new Port[]{new("visible",DataType.Boolean),new("rgba",PresentationDeclarations.Rgba)},Array.Empty<Port>(),
   new ToolPresentation(new[]{
    new FieldPresentation("visible","Blob","图像显示",0,FieldEditorKind.Scalar){Layout=new("结果显示","图像显示",DialogGroup:"shape",DialogTitle:"Blob",DialogPrimary:true)},
    new FieldPresentation("rgba","OK颜色","图像显示",1,FieldEditorKind.Record){Layout=new("结果显示","图像显示",DialogGroup:"shape",DialogTitle:"Blob")}}));
  public object CaptureSettings()=>ReadFields();
  public InputValues ReadFields()=>new(Schema.Inputs,_values);
  public void ApplyFields(IReadOnlyDictionary<string,object?> changes,Action<object> acceptCandidate)
  {
   var next=new Dictionary<string,object?>(_values);
   foreach(var item in changes)next[item.Key]=item.Value;
   var prepared=new InputValues(Schema.Inputs,next);
   acceptCandidate(prepared);
   _values=prepared.CopyValues().ToDictionary(item=>item.Key,item=>item.Value);Writes++;
  }
 }
 sealed class ExportImage : Native.Dependencies.VMControls.RenderInterface.IImageData,Native.Dependencies.VMControls.RenderInterface.IRangeImageData
 {
  public int Width{get;init;} public int Height{get;init;} public string PixelFormat{get;init;}="Rgb24";
  public byte[] Buffer{get;set;}=Array.Empty<byte>();public string MemoryAddress=>"";
  public byte[] RangeBuffer{get;set;}=null!;public uint DetailedPixelFormat{get;set;}
  public int Xoffset{get;set;} public int Yoffset{get;set;} public int Zoffset{get;set;}
  public int Xscale{get;set;} public int Yscale{get;set;} public int Zscale{get;set;}
 }
 sealed class ExportRectangle : Native.Dependencies.VMControls.RenderInterface.IRectangle
 {
  public Point CenterPoint=>new(4,4);public double Width=>4;public double Height=>4;public double Angle=>0;
  public double SkewAngleX=>0;public double SkewAngleY=>0;public string Fill=>"#00000000";
  public string Stroke=>"#FF00FF00";public double StrokeThickness=>1;public double Opacity=>1;public string ToolTip=>"fixture";
 }
 sealed class ViewSession : Native.Common.IImageViewPresentation
 {
  public int Disposals;public Native.Dependencies.VMControls.Interface.IImageViewBoxWAddition? InnerImageViewWAddition=>null;
  public object? ROIViewOut=>null;public object? InputElementOut=>null;public ICommand? MouseWheelCommand=>null;
  public bool DoImageSourceChangedOut(out bool value){value=false;return false;}
  public void ImageSourceChangedOut(){}public bool ShrinkOut()=>false;public bool InitOut()=>false;public bool EnlargeOut()=>false;
  public void ROIListChangedOut(object args){}public void Dispose(){Require(++Disposals==1,"视觉会话重复释放");}
 }
 sealed class ViewProvider : Native.Common.IImageViewExtensionProvider
 {
  public readonly List<ViewSession> Sessions=new();
  public Native.Common.IImageViewPresentation CreatePresentation(Native.Display.VMBaseControls.ImageViewBox view){var session=new ViewSession();Sessions.Add(session);return session;}
  public void PrepareResources(Native.Display.VMBaseControls.ImageViewBox view,bool profile)
  {
   // Minimal fixture templates satisfy the provider contract; this case checks
   // session ownership, not point-cloud/profile rendering capability.
   var key=profile?"WCustomPlotCanvas":"PointCloundCanvas";
   if(!view.Resources.Contains(key))view.Resources.Add(key,new DataTemplate{VisualTree=new FrameworkElementFactory(typeof(Grid))});
  }
 }
 static void NativeImageExports()
 {
  var exporter=typeof(Native.Display.VMBaseControls.ImageViewBox).Assembly.GetType("SunEyeVision.Plugin.Wpf.Native.Common.NativeImageExport",true)!;
  var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"native-export");Directory.CreateDirectory(folder);
  var bytes=new byte[]{255,0,0,0,0,255};var image=new ExportImage{Width=2,Height=1,Buffer=bytes};
  var raw=Path.Combine(folder,"rgb.png");exporter.GetMethod("SaveRaw")!.Invoke(null,new object[]{raw,image});
  var decoded=BitmapDecoder.Create(new Uri(raw),BitmapCreateOptions.IgnoreImageCache,BitmapCacheOption.OnLoad).Frames[0];
  var rgb=new FormatConvertedBitmap(decoded,PixelFormats.Rgb24,null,0);var actual=new byte[6];rgb.CopyPixels(actual,6,0);
  Require(actual.SequenceEqual(bytes)&&ReferenceEquals(image.Buffer,bytes),"原图导出改变像素、通道或借入缓冲引用");
  var depthBytes=new byte[]{0,0,255,127};var depth=new ExportImage{Width=2,Height=1,PixelFormat="Gray16",Buffer=depthBytes,RangeBuffer=depthBytes,DetailedPixelFormat=17825976u};
  var gray=Path.Combine(folder,"gray16.png");exporter.GetMethod("SaveRaw")!.Invoke(null,new object[]{gray,depth});
  var grayFrame=BitmapDecoder.Create(new Uri(gray),BitmapCreateOptions.IgnoreImageCache|BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
  var stored=new byte[4];grayFrame.CopyPixels(stored,4,0);Require(grayFrame.Format==PixelFormats.Gray16&&stored.SequenceEqual(depthBytes)&&ReferenceEquals(depth.RangeBuffer,depthBytes),"16位原图丢失位深/像素或改变输入");
  var ownerView=new Native.Display.VMBaseControls.ImageViewBox();Show(ownerView);
  var rendered=Path.Combine(folder,"rectangle.png");var black=new ExportImage{Width=8,Height=8,Buffer=new byte[8*8*3]};
  exporter.GetMethod("SaveRendered")!.Invoke(null,new object[]{rendered,black,new object[]{new ExportRectangle()},ownerView});
  var frame=BitmapDecoder.Create(new Uri(rendered),BitmapCreateOptions.IgnoreImageCache,BitmapCacheOption.OnLoad).Frames[0];
  var renderedRgb=new FormatConvertedBitmap(frame,PixelFormats.Rgb24,null,0);var pixels=new byte[8*8*3];renderedRgb.CopyPixels(pixels,24,0);
  Require(frame.PixelWidth==8&&frame.PixelHeight==8&&Enumerable.Range(0,64).Any(i=>pixels[i*3+1]>pixels[i*3]&&pixels[i*3+1]>pixels[i*3+2]),"原ShapeItemsPage未按原图尺寸输出绿色图形");
  Require(black.Buffer.All(value=>value==0),"带图形导出污染了原图缓冲");
 }
 static void NativeSessionLifetime()
 {
  var first=new ViewProvider();var second=new ViewProvider();var view=new Native.Display.VMBaseControls.ImageViewBox{ExtensionProvider=first};
  Show(view);Require(first.Sessions.Count==1&&ReferenceEquals(view.ImageBoxAddition,first.Sessions[0]),"加载未连接显式视觉provider");
  view.ExtensionProvider=second;Require(first.Sessions[0].Disposals==1&&second.Sessions.Count==1,"更换provider未释放旧会话或创建新会话");
  Show(new TextBlock{Text="卸载Native视图"});Require(second.Sessions[0].Disposals==1&&view.ImageBoxAddition is null,"卸载后会话残留");
  Show(view);Require(second.Sessions.Count==2&&second.Sessions[1].Disposals==0,"重新加载沿用了已释放会话");
  Show(new TextBlock());Require(second.Sessions[1].Disposals==1,"重载会话未释放");
 }
 static void NativeBitmapHelpers()
 {
  var bitmap=new WriteableBitmap(8,8,96,96,PixelFormats.Bgra32,null);
  Native.Dependencies.WriteableBitmapEx.WriteableBitmapExtensions.Clear(bitmap);
  Native.Dependencies.WriteableBitmapEx.WriteableBitmapExtensions.DrawLineAa(bitmap,1,1,6,6,unchecked((int)0xffffffff),1);
  var pixels=new byte[8*8*4];bitmap.CopyPixels(pixels,32,0);
  Require(Enumerable.Range(0,64).Any(i=>pixels[i*4+3]>0)&&pixels[3]==0,"原位图绘线未产生笔画或越出端点");
 }
 static void NativeScalarTemplate()
 {
  var original=new Native.Numeric.HikPcUI.ListView.SliderArrangeFloat
      {Minimum=0,Maximum=1,SliderTickFrequency=.01,StartValue=.5,DecimalDigits=2};
  Show(original);Pump();
  var numeric=Visuals<Native.Numeric.HikPcUI.ListView.NumericControl>(original).First(control=>control.IsVisible);
  var text=(TextBox)numeric.FindName("ValueText");
  Require(double.Parse(text.Text,CultureInfo.InvariantCulture)==.5,"原浮点模板未显示绑定值");
  original.StartValue=.37;Pump();
  Require(double.Parse(text.Text,CultureInfo.InvariantCulture)==.37,"原滑条值没有同步原数值输入框");
  Click((ButtonBase)numeric.FindName("UpButton"));Pump();
  Require(Math.Abs(original.StartValue-.38)<1e-12,"原浮点箭头没有消费0.01增量");
  original.IsSubscribeLeft=true;Pump();
  Require(original.SliderVisibility==Visibility.Collapsed,"原控件订阅状态未隐藏滑条");
  original.IsSubscribeLeft=false;Pump();
  Require(original.SliderVisibility==Visibility.Visible,"原控件数值状态未恢复滑条");
  Show(new TextBlock());
 }
 static double Luminance(Color color){double Linear(byte c){var s=c/255d;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}return .2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);}
 static void Contrast(Brush foreground,Brush background,string label){Require(foreground is SolidColorBrush&&background is SolidColorBrush,label+"不是可核查纯色");var a=Luminance(((SolidColorBrush)foreground).Color);var b=Luminance(((SolidColorBrush)background).Color);var ratio=(Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);Require(ratio>=4.5,label+"文字对比不足："+ratio.ToString("F2",CultureInfo.InvariantCulture));}
 static void LightText(DependencyObject root,Brush background){foreach(var text in Find<TextBlock>(root).Where(t=>t.IsVisible&&t.Opacity>=1&&!string.IsNullOrWhiteSpace(t.Text)))Contrast(text.Foreground,background,text.Text);foreach(var tab in Find<TabItem>(root))Contrast(tab.Foreground,background,"页签"+tab.Header);}
 static IEnumerable<T> Visuals<T>(DependencyObject root) where T:DependencyObject{if(root is T item)yield return item;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var result in Visuals<T>(VisualTreeHelper.GetChild(root,i)))yield return result;}
 static List<string> VisualContrast(DependencyObject root,Brush background){var failures=new List<string>();foreach(var text in Visuals<TextBlock>(root).Where(t=>t.IsVisible&&t.IsEnabled&&t.Opacity>=1&&!string.IsNullOrWhiteSpace(t.Text))){try{Contrast(text.Foreground,background,text.Text);}catch(Exception error){failures.Add(error.Message+"；Foreground="+text.Foreground+"；IsEnabled="+text.IsEnabled);}}return failures;}
 static void Show(UIElement view){host.Children.Clear();host.Children.Add(view);owner.UpdateLayout();Pump();}
 static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject
 {if(root is T item)yield return item;foreach(var child in LogicalTreeHelper.GetChildren(root))if(child is DependencyObject d)foreach(var found in Find<T>(d))yield return found;}
 static Native.Numeric.HikPcUI.ListView.NumericControl OriginalNumberInput(DependencyObject field)
 {
  var original=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(field).Single();
  var box=(ComboBox)original.FindName("CBox");box.ApplyTemplate();
  return (Native.Numeric.HikPcUI.ListView.NumericControl)box.Template.FindName("T1",box);
 }
 static void CaptureInline(FrameworkElement view,string name)
 {
  owner.UpdateLayout();Pump();
  var image=new RenderTargetBitmap((int)Math.Ceiling(owner.ActualWidth),(int)Math.Ceiling(owner.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(owner);
  var area=view.TransformToAncestor(owner).TransformBounds(new Rect(view.RenderSize));
  var left=Math.Max(0,(int)Math.Floor(area.Left));var top=Math.Max(0,(int)Math.Floor(area.Top));
  var right=Math.Min(image.PixelWidth,(int)Math.Ceiling(area.Right));var bottom=Math.Min(image.PixelHeight,(int)Math.Ceiling(area.Bottom));
  Require(right>left&&bottom>top,"数值控件实际取图为空");
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(new CroppedBitmap(image,new Int32Rect(left,top,right-left,bottom-top))));
  using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name));encoder.Save(file);
 }
 static void Click(ButtonBase button)=>button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
 static void StylePopup(DisplayObjectField display,Action<FrameworkElement> edit)
 {
  var count=Application.Current.Windows.Count;
  display.OpenStyleEditor();Pump();
  Require(display.IsStyleEditorOpen,"样式弹层未打开");
  Require(Application.Current.Windows.Count==count,"样式编辑仍创建独立Window");
  var surface=display.StyleEditorContent;surface.UpdateLayout();Pump();
  try{edit(surface);}finally{display.CloseStyleEditor();Pump();}
 }
 static KeyEventArgs SendKey(UIElement target,Key key,bool preview=false)
 {
  var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target)!,Environment.TickCount,key){RoutedEvent=preview?Keyboard.PreviewKeyDownEvent:Keyboard.KeyDownEvent};
  target.RaiseEvent(args);Pump();return args;
 }
 static void VerifyBetween()
 {
  DeclaredFieldsView Create(Action<IReadOnlyDictionary<string,ParameterSetting>> writer,ReferenceFieldOptions? refs=null,double maximum=1000,double initialLow=10,double initialHigh=20)=>new(new[]{
   (new Port("low",DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),new FieldPresentation("low","面积下限","范围",0,FieldEditorKind.Scalar){Layout=new("参数","范围",PairWith:"high",Minimum:0,Maximum:maximum,DecimalPlaces:0)},(ParameterSetting)new ConstantSetting(initialLow)),
   (new Port("high",DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),new FieldPresentation("high","面积上限","范围",1,FieldEditorKind.Scalar){Layout=new("参数","范围",Minimum:0,Maximum:maximum,DecimalPlaces:0)},(ParameterSetting)new ConstantSetting(initialHigh))},writer,refs);
  Test("原between一份真实双端、联合输入跨旧上限、Enter一次提交及失焦",()=>
  {
   var writes=0;IReadOnlyDictionary<string,ParameterSetting>? accepted=null;
   using var fields=Create(values=>{writes++;accepted=values;});Show(fields);
   var low=fields.Fields[0];var high=fields.Fields[1];
   Require(ReferenceEquals(low.View,high.View)&&!ReferenceEquals(low.InputElement,high.InputElement),"双端未共享一份真实组合或端点混用");
   var native=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(low.View).Single();
   Require(native.SliderType==Native.Numeric.HikPcUI.ListView.SliderArrangeType.between&&native.IsVisibleOfChild==Visibility.Visible,"没有使用原between");
   var a=(TextBox)low.InputElement!;var b=(TextBox)high.InputElement!;a.Focus();a.Text="30";b.Text="40";
   Require(writes==0,"键入提前写入宿主");SendKey(a,Key.Enter,true);
   Require(writes==1&&accepted!.Count==2&&Equals(((ConstantSetting)accepted["low"]).Value,30d)&&Equals(((ConstantSetting)high.Setting).Value,40d)&&!a.IsKeyboardFocused,"未单次完整接纳或Enter未失焦");
   Require(native.StartValue==30&&native.EndValue==40,"原DP未双向同步");CaptureInline(fields,"native-between-accepted.png");
  });
  Test("原between非法另一端与宿主拒绝不半写，保留原文可修正",()=>
  {
   var writes=0;var reject=true;
   using var fields=Create(_=>{if(reject)throw new InvalidOperationException("宿主拒绝范围");writes++;});Show(fields);
   var a=(TextBox)fields.Fields[0].InputElement!;var b=(TextBox)fields.Fields[1].InputElement!;
   a.Focus();a.Text="30";b.Text="bad";SendKey(a,Key.Enter,true);
   Require(writes==0&&a.IsKeyboardFocused&&a.Text=="30"&&b.Text=="bad","非法另一端仍提交或清原文/焦点");
   b.Text="40";SendKey(a,Key.Enter,true);
   Require(writes==0&&a.Text=="30"&&b.Text=="40"&&Equals(((ConstantSetting)fields.Fields[0].Setting).Value,10d),"宿主拒绝丢原文或部分写入");
   var native=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(fields.Fields[0].View).Single();
   Require(native.StartValue==10&&native.EndValue==20,"拒绝未恢复原DP");
   reject=false;SendKey(a,Key.Enter,true);Require(writes==1&&Equals(((ConstantSetting)fields.Fields[1].Setting).Value,40d),"拒绝后原文无法再次提交");
  });
  Test("原between右订阅槽与另一端联合校验，清空不复活且旧访问失效",()=>
  {
   var writes=0;var refreshFails=false;
   var candidate=new DeclaredReferenceCandidate("数值来源","source",null,new Port("values",TypeRef.SequenceOf(DataType.Number)),Array.Empty<string>());
   using var fields=Create(_=>writes++,new ReferenceFieldOptions(_=>{if(refreshFails)throw new InvalidOperationException("来源刷新失败");return new[]{candidate};},EditableIndex:true));
   Show(fields);var a=(TextBox)fields.Fields[0].InputElement!;
   var native=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(fields.Fields[0].View).Single();
   var toggle=Visuals<ToggleButton>(native).Single(button=>button.GetBindingExpression(ToggleButton.IsCheckedProperty)?.ParentBinding.Path?.Path=="IsSubscribeRight");
   toggle.IsChecked=true;Click(toggle);Pump();Require(native.IsSubscribeRight&&!native.IsSubscribeLeft,"右端错误使用左订阅槽");
   var session=native.SubscribeDataRight!;var type=session.GetType();
   void Refresh()=>type.GetMethod("Refresh")!.Invoke(session,null);
   void Select()
   {
    Refresh();var roots=(System.Collections.IEnumerable)type.GetProperty("SourceNodes")!.GetValue(session)!;
    var node=roots.Cast<object>().SelectMany(root=>((System.Collections.IEnumerable)root.GetType().GetProperty("EnumEntrysA")!.GetValue(root)!).Cast<object>()).First();
    ((ICommand)type.GetProperty("ClickCommand")!.GetValue(session)!).Execute(node);Pump();
   }
   a.Text="bad";Select();Require(writes==0&&fields.Fields[1].Setting is ConstantSetting,"选源绕过另一端非法数字");
   a.Text="11";Select();Require(writes==1&&fields.Fields[1].Setting is ReferenceSetting,"修正后未联合选择来源");
   refreshFails=true;Refresh();Select();Require(writes==1,"保留树显式选源绕过来源错误");a.Focus();Pump();a.Text="12";SendKey(a,Key.Enter,true);Require(writes==1,"来源刷新失败仍调用组writer");
   refreshFails=false;Refresh();SendKey(a,Key.Enter,true);Require(writes==2,"来源恢复后不能提交；writes="+writes+"；focus="+a.IsKeyboardFocused+"；low="+a.Text+"；errors="+string.Join("/",fields.Fields.Select(f=>f.Error)));
   a.Text="bad";Require(!(bool)type.GetMethod("TryCommitPending")!.Invoke(session,null)!,"干净引用提交绕过另一端错误");
   type.GetMethod("ObserveText")!.Invoke(session,new object[]{""});a.Text="13";SendKey(a,Key.Enter,true);
   Require(writes==2&&fields.Fields[1].HasInvalidText,"必填清空复活旧来源");
   toggle.IsChecked=false;Click(toggle);Pump();
   Require(writes==3&&!native.IsSubscribeRight&&Equals(((ConstantSetting)fields.Fields[1].Setting).Value,20d),"解绑未通过组接纳恢复真实常量备份");
   var access=fields.Fields[0];fields.Dispose();Require(!access.TrySetConstant(14d)&&!access.TryCommitPending()&&access.InputElement is null,"释放后的范围访问仍有效");
  });
  Test("范围初始逆序拒绝保存、拓扑缺失拒绝且合法独立边界可创建",()=>
  {
   var writes=0;using var invalid=Create(_=>writes++,initialLow:20,initialHigh:10);Show(invalid);
   Require(!invalid.TryValidate(out _)&&writes==0&&Equals(((ConstantSetting)invalid.Fields[0].Setting).Value,20d),"初始逆序被保存或交换值");
   ((TextBox)invalid.Fields[0].InputElement!).Text="5";Require(invalid.TryValidate(out _)&&writes==1,"逆序修正后无法恢复");
   var rejected=false;
   try{using var missing=new DeclaredFieldsView(new[]{(new Port("a",DataType.Number),new FieldPresentation("a","a","g",0,FieldEditorKind.Scalar){Layout=new("p","s",PairWith:"missing")},(ParameterSetting)new ConstantSetting(1d))},_=>{});}
   catch(ArgumentException){rejected=true;}Require(rejected,"缺失配对被静默忽略");
   using var independent=new DeclaredFieldsView(new[]{
    (new Port("a",DataType.Number),new FieldPresentation("a","a","g",0,FieldEditorKind.Scalar){Layout=new("p","s",PairWith:"b",Minimum:0,Maximum:10)},(ParameterSetting)new ConstantSetting(5d)),
    (new Port("b",DataType.Number,Storage:NumericStorage.Float64),new FieldPresentation("b","b","g",1,FieldEditorKind.Scalar){Layout=new("p","s",Minimum:20,Maximum:100)},(ParameterSetting)new ConstantSetting(60d))},_=>writes++);
   Show(independent);Pump();var native=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(independent.Fields[0].View).Single();
   Require(independent.TryValidate(out _)&&writes==1&&native.EndValue==60,"独立端点边界被错拒或旧异步夹紧覆盖当前值");
  });
  Test("稳定Track四向极大域和原Thumb真实拖动保持有限映射",()=>
  {
   foreach(var orientation in new[]{Orientation.Horizontal,Orientation.Vertical})foreach(var reverse in new[]{false,true})
   {
    var track=new SunEyeVision.Plugin.Wpf.Presenters.Numeric.StableSliderTrack{Minimum=0,Maximum=double.MaxValue,Value=double.MaxValue/4,Orientation=orientation,IsDirectionReversed=reverse,
     Thumb=new Thumb{Width=12,Height=12},DecreaseRepeatButton=new RepeatButton(),IncreaseRepeatButton=new RepeatButton()};
    track.Measure(new Size(240,240));track.Arrange(new Rect(0,0,240,240));
    var flip=reverse^(orientation==Orientation.Vertical);var start=orientation==Orientation.Horizontal?new Point(6,10):new Point(10,6);var end=orientation==Orientation.Horizontal?new Point(234,10):new Point(10,234);
    Require(track.ValueFromPoint(start)==(flip?double.MaxValue:0)&&track.ValueFromPoint(end)==(flip?0:double.MaxValue),"四向端点映射不正确");
    Require(double.IsFinite(track.ValueFromDistance(10000,10000)),"超界拖动返回无穷");
   }
   var writes=0;using var fields=Create(_=>writes++,maximum:double.MaxValue);Show(fields);
   var native=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(fields.Fields[0].View).Single();
   Require(fields.Fields[1].TrySetConstant(double.MaxValue),"极大上限未接纳");writes=0;
   var combo=(ComboBox)native.FindName("CBox");combo.IsDropDownOpen=true;Pump();
   var popup=(Popup)combo.Template.FindName("PART_Popup",combo);var slider=Visuals<Slider>(popup.Child).Single(item=>item.Name=="SL_Bat1");slider.ApplyTemplate();
   var track2=(Track)slider.Template.FindName("PART_Track",slider);
   Require(track2 is SunEyeVision.Plugin.Wpf.Presenters.Numeric.StableSliderTrack&&track2.ActualWidth>0,"真实原模板未使用稳定Track");
   track2.Thumb.RaiseEvent(new DragDeltaEventArgs(100,0){RoutedEvent=Thumb.DragDeltaEvent});Pump();
   track2.Thumb.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});Pump();
   Require(writes==1&&native.StartValue>20&&double.IsFinite(native.StartValue)&&Equals(((ConstantSetting)fields.Fields[0].Setting).Value,native.StartValue),"原Thumb实际拖动未提交有限值");
   Require(double.IsFinite(native.StartRectClip.Bounds.Width)&&double.IsFinite(native.EndRectClip.Bounds.X),"原双轨裁剪仍溢出");
   CaptureInline(fields,"native-between-extreme.png");
  });
 }
 static int Main()
 {
  new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Themes/Dark/DarkTheme.xaml"})Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
  owner=new Window{Width=530,Height=700,Title="O06独立真实窗口验证",Content=host=new StackPanel()};owner.Resources.MergedDictionaries.Add(EditorResources.Create());owner.Show();Pump();
  try
  {
   VerifyBetween();
   Test("原位图辅助真实绘制",NativeBitmapHelpers);
   Test("原PCUI浮点模板加载、数值步进与订阅显隐",NativeScalarTemplate);
   Test("Native原图与原ShapeItemsPage导出保真",NativeImageExports);
   Test("Native视图扩展会话更换卸载释放",NativeSessionLifetime);
   Test("浮点已保存值只聚焦离开保真",()=>
   {var value=.12345;var writes=0;var field=new DeclaredFieldEditor(new("float",DataType.Number,Storage:NumericStorage.Float64),"比例",new ConstantSetting(value),p=>{value=(double)((ConstantSetting)p).Value!;writes++;});Show(field);var text=(TextBox)field.InputElement!;text.Focus();Pump();var button=new Button{Content="离开"};host.Children.Add(button);button.Focus();Pump();Require(value==.12345,"仅焦点变化将0.12345改成"+value.ToString("R")+"；写入"+writes+"次");});
   Test("de-DE浮点输入不被二次误解析",()=>
   {var previous=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");var value=.5;var field=new DeclaredFieldEditor(new("float",DataType.Number,Storage:NumericStorage.Float64),"比例",new ConstantSetting(value),p=>value=(double)((ConstantSetting)p).Value!);Show(field);var text=(TextBox)field.InputElement!;text.Focus();Pump();var button=new Button();host.Children.Add(button);button.Focus();Pump();Require(value==.5&&!field.HasInvalidText,"0.5在de-DE焦点提交后="+value.ToString("R",CultureInfo.InvariantCulture)+"；"+field.Error);}finally{CultureInfo.CurrentCulture=previous;}});
   Test("声明字段消费原浮点模板、双向同步和有效Enter失焦",()=>
   {
    var value=.5;var writes=0;
    var presentation=new FieldPresentation("native-ratio","比例","参数",0,FieldEditorKind.Scalar)
        {Layout=new("运行参数","参数",Minimum:0,Maximum:1,Increment:.01)};
    var field=new DeclaredFieldEditor(new("native-ratio",DataType.Number,Storage:NumericStorage.Float64),"比例",new ConstantSetting(value),
        setting=>{value=(double)((ConstantSetting)setting).Value!;writes++;},presentation:presentation);
    Show(field);var original=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeFloat>(field).Single();
    var text=(TextBox)field.InputElement!;text.Focus();Pump();
    var combo=(ComboBox)original.FindName("CBox");var popup=(Popup)combo.Template.FindName("PART_Popup",combo);
    Require(popup.IsOpen,"原浮点控件获得焦点未展开滑条");
    text.Text="0.12345";text.CaretIndex=4;Pump();
    var slider=Visuals<Slider>(popup.Child).Single(item=>item.Name=="SL_Bat1");
    Require(value==.5&&text.Text=="0.12345"&&text.CaretIndex==4,"未提交输入提前写入或丢失原文/光标");
    SendKey(text,Key.Enter,true);Require(value==.12&&Math.Abs(slider.Value-.12)<1e-12,"原浮点声明精度提交未同步滑条");
    text.Focus();Pump();slider.Value=.67;Pump();
    var thumb=(Thumb)slider.Template.FindName("Thumb",slider);thumb.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseUpEvent});Pump();
    Require(value==.67&&double.Parse(text.Text,CultureInfo.InvariantCulture)==.67,"原滑条释放没有回填同一字段草稿");
    text.Text="bad";SendKey(text,Key.Enter,true);Require(text.IsKeyboardFocused&&field.HasInvalidText&&value==.67&&text.Text=="bad","非法Enter未留焦保留原文和值");CaptureInline(field,"native-number-invalid.png");
    text.Text="0.75";SendKey(text,Key.Enter,true);Require(!text.IsKeyboardFocused&&!popup.IsOpen&&!field.HasInvalidText&&value==.75,"有效Enter未失焦/关闭原滑条");CaptureInline(field,"native-number-accepted.png");
    var count=writes;text.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.PreviewMouseWheelEvent});text.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.MouseWheelEvent});Pump();
    Require(value==.75&&writes==count,"未聚焦滚轮修改了原控件字段");
    text.Focus();Pump();text.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.PreviewMouseWheelEvent});text.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.MouseWheelEvent});Pump();
    Require(Math.Abs(value-.76)<1e-12,"已聚焦原控件滚轮未按0.01写回");
   });
   Test("程序数值提案与键入共用范围和精度且不接纳不可精确整数",()=>
   {
    var value=.5;var writes=0;
    var presentation=new FieldPresentation("program-ratio","比例","参数",0,FieldEditorKind.Scalar)
        {Layout=new("运行参数","参数",Minimum:0,Maximum:1,Increment:.01)};
    using var field=new DeclaredFieldEditor(new("program-ratio",DataType.Number,Storage:NumericStorage.Float64),"比例",new ConstantSetting(value),
        setting=>{value=(double)((ConstantSetting)setting).Value!;writes++;},presentation:presentation);
    Show(field);Require(!field.TrySetConstant(2d)&&value==.5&&writes==0,"程序提案绕过声明范围");
    Require(!field.TryCommitPending()&&value==.5&&writes==0,"保存捕获自动清除了拒绝的程序提案");
    Require(field.TrySetConstant(.12345)&&value==.12&&writes==1,"程序提案未走相同浮点精度");
    using var initial=new DeclaredFieldEditor(new("initial-ratio",DataType.Number,Storage:NumericStorage.Float64),"比例",new ConstantSetting(.12345),_=>writes++,presentation:presentation);
    Show(initial);Require(initial.HasInvalidText&&writes==1,"初始超精度值被格式化掩盖");
    var rejected=false;try{using var large=new DeclaredFieldEditor(new("large",DataType.Integer),"大整数",new ConstantSetting(9007199254740993L),_=>writes++);}catch(ArgumentException){rejected=true;}
    Require(rejected&&writes==1,"不可精确初值被舍入显示或写入");
   });
   Test("已存在引用允许退出但禁止重新进入且越界备份不能恢复",()=>
   {
    var source=new DeclaredReferenceCandidate("来源","source",null,new Port("count",DataType.Integer),Array.Empty<string>());
    var options=new ReferenceFieldOptions(_=>new[]{source});var writes=0;
    FieldPresentation Layout(bool allow)=>new("mode-count","数量","参数",0,FieldEditorKind.Scalar){Layout=new("运行参数","参数",Minimum:0,Maximum:255,AllowReference:allow)};
    using var field=new DeclaredFieldEditor(new("mode-count",DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),
        "数量",new ReferenceSetting(source.Reference(BindingSelection.All)),_=>writes++,options,new ConstantSetting(7L),presentation:Layout(false));
    Show(field);var original=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(field).Single();
    var box=(ComboBox)original.FindName("CBox");
    var mode=Visuals<ToggleButton>(field).Single(b=>BindingOperations.GetBinding(b,ToggleButton.IsCheckedProperty)?.Path?.Path=="IsSubscribeLeft");
    mode.IsChecked=false;Click(mode);Pump();Require(!original.IsSubscribeLeft&&writes==1&&mode.Visibility==Visibility.Collapsed,"退出已有引用后仍允许新增订阅");
    mode.IsChecked=true;Click(mode);Require(!original.IsSubscribeLeft&&writes==1,"强制旧切换事件绕过禁止订阅");
    using var invalid=new DeclaredFieldEditor(new("mode-count",DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),
        "数量",new ReferenceSetting(source.Reference(BindingSelection.All)),_=>writes++,options,new ConstantSetting(1000L),presentation:Layout(true));
    Show(invalid);original=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(invalid).Single();
    mode=Visuals<ToggleButton>(invalid).Single(b=>BindingOperations.GetBinding(b,ToggleButton.IsCheckedProperty)?.Path?.Path=="IsSubscribeLeft");
    mode.IsChecked=false;Click(mode);Pump();Require(original.IsSubscribeLeft&&invalid.HasInvalidText&&writes==1,"越界backup先接纳后夹紧");
   });
   Test("原数值两级绑定保留且初始订阅不校验隐藏常量",()=>
   {
    var type=DataType.Number;var writes=0;
    var source=new DeclaredReferenceCandidate("比例来源","source",null,new Port("ratio",type),Array.Empty<string>());
    var presentation=new FieldPresentation("reference-ratio","比例","参数",0,FieldEditorKind.Scalar)
        {Layout=new("运行参数","参数",Minimum:1,Maximum:10,Increment:1)};
    using var field=new DeclaredFieldEditor(new("reference-ratio",type,Storage:NumericStorage.Float64,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),
        "比例",new ReferenceSetting(source.Reference(BindingSelection.All)),_=>writes++,new SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceFieldOptions(_=>new[]{source}),presentation:presentation);
    Show(field);Require(!field.HasInvalidText&&field.TryCommitPending()&&writes==0,"合法初始引用被隐藏数字越界污染");
    var original=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeFloat>(field).Single();
    Require(original.IsSubscribeLeft,"初始引用没有显示原订阅槽");
   });
   Test("原浮点卸载重载保留待编辑原文且离线捕获不依赖延迟回调",()=>
   {
    var value=.5;var writes=0;
    var presentation=new FieldPresentation("pending-ratio","比例","参数",0,FieldEditorKind.Scalar)
        {Layout=new("运行参数","参数",Minimum:0,Maximum:1,Increment:.01)};
    using var field=new DeclaredFieldEditor(new("pending-ratio",DataType.Number,Storage:NumericStorage.Float64),"比例",new ConstantSetting(value),
        setting=>{value=(double)((ConstantSetting)setting).Value!;writes++;},presentation:presentation);
    Show(field);var text=(TextBox)field.InputElement!;
    var leaf=OriginalNumberInput(field);
    var inner=BindingOperations.GetBinding(text,TextBox.TextProperty);var outer=BindingOperations.GetBinding(leaf,Native.Numeric.HikPcUI.ListView.NumericControl.TextProperty);
    Require(inner?.Mode==BindingMode.Default&&((FrameworkPropertyMetadata)TextBox.TextProperty.GetMetadata(text.GetType())).BindsTwoWayByDefault&&inner.UpdateSourceTrigger==UpdateSourceTrigger.LostFocus&&outer?.Mode==BindingMode.TwoWay&&outer.StringFormat=="0.00","没有保留两级原绑定/原浮点格式");
    text.Text="bad";Show(new TextBlock());Require(!field.TryCommitPending()&&value==.5&&writes==0,"卸载捕获丢失非法原文");
    Show(field);Require(((TextBox)field.InputElement!).Text=="bad"&&field.HasInvalidText,"重载未恢复非法原文");
    text=(TextBox)field.InputElement!;text.Text="0.73";Show(new TextBlock());
    Require(field.TryCommitPending()&&value==.73&&writes==1,"卸载后捕获没有提交真实pending原文");
    Show(field);Require(double.Parse(((TextBox)field.InputElement!).Text,CultureInfo.InvariantCulture)==.73&&!field.HasInvalidText,"卸载接纳后重载没有投影新值");
   });
   Test("原样式粗细严格范围、非法Enter留焦与箭头单次提交",()=>
   {
    var value=2d;var writes=0;
    var presentation=new FieldPresentation("stroke","粗细","样式",0,FieldEditorKind.Scalar)
        {Layout=new("结果显示","样式",Minimum:1,Maximum:10,Increment:1)};
    var field=new DeclaredFieldEditor(new("stroke",DataType.Number,Storage:NumericStorage.Float64),"粗细",new ConstantSetting(value),
        setting=>{value=(double)((ConstantSetting)setting).Value!;writes++;},presentation:presentation);
    Show(field);var text=(TextBox)field.InputElement!;text.Focus();Pump();text.Text="0.25";SendKey(text,Key.Enter,true);
    Require(value==2&&writes==0&&field.HasInvalidText&&text.IsKeyboardFocused&&text.Text=="0.25","越界Enter未拒绝并保留原文/焦点");
    text.Text="2";SendKey(text,Key.Enter,true);Require(value==2&&!field.HasInvalidText&&!text.IsKeyboardFocused,"纠正Enter未恢复");
    var original=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeFloat>(field).Single();
    Require(original.Minimum==1&&original.Maximum==10,"编辑器擅自扩大声明范围");
    var number=OriginalNumberInput(field);
    Click((ButtonBase)number.FindName("UpButton"));Pump();
    Require(value==3&&writes==1,"原箭头未按单位步长单次提交");
   });
   Test("样式就地弹层草稿编辑、校验与卸载关闭",()=>
   {
    var writes=0;var width=3d;var reject=false;var color=PresentationDeclarations.Color(10,20,30,200);
    var visible=new DeclaredFieldEditor(new("visible",DataType.Boolean),"显示",new ConstantSetting(true),_=>{});
    var line=new DeclaredFieldEditor(new("width",DataType.Number,Storage:NumericStorage.Float64),"粗细",new ConstantSetting(width),setting=>{if(reject)throw new InvalidOperationException("范围失效");width=(double)((ConstantSetting)setting).Value!;writes++;});
    var rgba=new DeclaredFieldEditor(new("color",PresentationDeclarations.Rgba),"颜色",new ConstantSetting(color),setting=>{color=(RecordValue)((ConstantSetting)setting).Value!;writes++;});
    var display=new DisplayObjectField("Blob",visible,new[]{(rgba,"OK颜色"),(line,"粗细")});Show(display);
    StylePopup(display,surface=>
    {
     Require(Find<DeclaredFieldEditor>(surface).Contains(line),"样式字段仍为第二套临时副本");
     ((TextBox)line.InputElement!).Text="7";line.TryCommitPending();Require(width==7&&writes>0,"样式未更新调用方草稿");
     var field=(NativeColorPresenter)rgba.InputElement!;field.Palette.IsDropDownOpen=true;Pump();Require(display.IsStyleEditorOpen,"嵌套色板打开关闭了样式父层");
     var picker=field.NativeView;
     SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView.SelectColorCommand.Execute(new SolidColorBrush(Color.FromArgb(123,32,64,128)),picker);
     Require(color.Get<long>("a")==123&&color.Get<long>("b")==128,"颜色没有进入窗口草稿或丢失RGBA");field.ClosePalette();
     Require(!Find<Button>(surface).Any(button=>Equals(button.Content,"确定")||Equals(button.Content,"取消")),"样式层仍保留独立保存按钮");
     field.Palette.IsDropDownOpen=true;Pump();SendKey(field.Palette,Key.Escape);
     Require(display.IsStyleEditorOpen&&!field.Palette.IsDropDownOpen,"第一次Escape没有只关闭嵌套色板");
     SendKey(field.Palette,Key.Escape);
     Require(!display.IsStyleEditorOpen,"第二次Escape没有关闭样式父层");
    });
    Require(width==7&&color.Get<long>("a")==123&&!display.IsStyleEditorOpen,"收起样式层丢失草稿或弹层残留");
    StylePopup(display,_=>{reject=true;((TextBox)line.InputElement!).Text="9";line.TryCommitPending();Require(line.HasInvalidText&&width==7,"草稿写入拒绝未保留错误和值");reject=false;var corrected=(TextBox)line.InputElement!;corrected.Focus();corrected.Text="7";SendKey(corrected,Key.Enter,true);Require(!line.HasInvalidText,"修正后错误未清除");((TextBox)line.InputElement!).Text="bad";Require(line.HasInvalidText&&width==7,"非法输入写入草稿");((TextBox)line.InputElement!).Text="7";});
    display.OpenStyleEditor();Pump();Show(new TextBlock{Text="卸载弹层宿主"});Require(!display.IsStyleEditorOpen,"宿主卸载后样式弹层仍打开");
   });
   Test("公共设置视图重绑释放样式父层和原色板",()=>
   {
    var oldDraft=new ColorLifetimeDraft();using var settings=new DeclaredSettingsView(oldDraft);Show(settings);
    var oldFields=(DeclaredFieldsView)settings.Content;
    var oldField=oldFields.Fields.Single(field=>field.FieldId=="rgba");
    var display=Find<DisplayObjectField>(settings).Single();display.OpenStyleEditor();Pump();
    var oldColor=(NativeColorPresenter)oldField.InputElement!;oldColor.Palette.IsDropDownOpen=true;Pump();
    Require(display.IsStyleEditorOpen&&oldColor.Palette.IsDropDownOpen,"未实际打开父样式层及原色板");
    var nextDraft=new ColorLifetimeDraft();settings.Bind(nextDraft);Pump();
    Require(!display.IsStyleEditorOpen&&!oldColor.Palette.IsDropDownOpen,"重绑未关闭父子浮层");
    oldColor.SelectedColor=Colors.Red;
    Require(!oldField.TrySetConstant(PresentationDeclarations.Color(255,0,0)),"已释放字段仍接纳编辑");
    Require(oldDraft.Writes==0&&nextDraft.Writes==0&&((DeclaredFieldsView)settings.Content).Fields.Count==2,"旧回调污染草稿或新字段树不完整");
    settings.Dispose();settings.Dispose();oldColor.Dispose();Require(oldColor.Child is null,"色板释放没有拆实际原视图");
   });
   Test("公共字段重绘释放旧原色板且临时卸载不失效",()=>
   {
    var draft=new ColorLifetimeDraft();using var settings=new DeclaredSettingsView(draft);Show(settings);
    var field=((DeclaredFieldsView)settings.Content).Fields.Single(item=>item.FieldId=="rgba");
    var display=Find<DisplayObjectField>(settings).Single();display.OpenStyleEditor();Pump();
    var oldColor=(NativeColorPresenter)field.InputElement!;oldColor.Palette.IsDropDownOpen=true;Pump();
    Require(field.TrySetConstant(PresentationDeclarations.Color(32,64,128,123)),"字段重绘提案未接纳");Pump();
    var current=(NativeColorPresenter)field.InputElement!;
    Require(!ReferenceEquals(oldColor,current)&&!oldColor.Palette.IsDropDownOpen&&oldColor.Child is null,"Render未释放被替换色板");
    var writes=draft.Writes;oldColor.SelectedColor=Colors.Red;Pump();
    Require(draft.Writes==writes&&current.SelectedColor==Color.FromArgb(123,32,64,128),"旧色板仍影响新字段或草稿");
    Show(new TextBlock{Text="暂时卸载"});Show(settings);display.OpenStyleEditor();Pump();current.Palette.IsDropDownOpen=true;Pump();
    Require(current.Palette.IsDropDownOpen,"临时卸载错误地永久释放当前色板");
   });
   Test("真实数值模板输入同步及Enter失焦",()=>
   {
    var input=new NumericUpDown{Minimum=0,Maximum=255,Value=100,DecimalPlaces=0,ShowSlider=true};Show(input);input.ApplyTemplate();
    var box=input.TextEditor!;box.Focus();Pump();var slider=(Slider)input.Template.FindName("PART_Slider",input);var popup=(Popup)input.Template.FindName("PART_SliderPopup",input);
    box.Text="175";Require(input.Value==175&&slider.Value==175&&box.Text=="175","有效键入未同步滑条或被重排文本");
    box.Text="bad";box.Focus();var invalid=SendKey(box,Key.Enter,true);Require(invalid.Handled&&box.IsKeyboardFocused&&input.Value==175,$"非法Enter未保留输入焦点或改变数值：Handled={invalid.Handled},Focus={box.IsKeyboardFocused},Value={input.Value},Text={box.Text}");
    box.Text="200";var valid=SendKey(box,Key.Enter,true);Require(valid.Handled&&!box.IsKeyboardFocused&&!popup.IsOpen&&input.Value==200,"有效Enter未确认失焦收起滑条");
    var outside=new Button{Content="其他焦点"};host.Children.Add(outside);outside.Focus();Pump();box.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.PreviewMouseWheelEvent});Require(input.Value==200,"无焦点滚轮改变了数值");
    box.Focus();Pump();box.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.PreviewMouseWheelEvent});Require(input.Value==201,"聚焦滚轮未按步长改变数值");
   });
   Test("正式Runtime三页与XML数值布局，旧SDK入口缺席",()=>
   {
    var manager=new PluginManager();var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;manager.LoadPlugins(Path.Combine(product,"plugins"));manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(t=>t.Id));
    Require(!Directory.Exists(Path.Combine(product,"plugins/SunEyeVision.Tool.BlobAnalysis")),"正式目录仍含旧Blob SDK");
    var node=new WorkflowNodeBase("blob","blob","Blob","BlobAnalysis"){RuntimeConfiguration=RuntimeToolConfiguration.Create("BlobAnalysis")};
    var flow=new SunEyeVision.Workflow.Workflow("flow","Runtime窗口");flow.Nodes.Add(node);var solution=new Solution();solution.Workflows.Add(flow);
    var edits=(DocumentEditSession)Activator.CreateInstance(typeof(DocumentEditSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{solution},null)!;
    using var root=new RuntimeNodeConfigurationControl(node);
    using var context=new ProductionEditorContext(edits.Host,flow.Id,node,()=>true,root.BindDraft,documentEdits:edits);root.BindDraft(context.DraftNode);ToolEditorCommit.AttachDraftContext(root,context);Show(root);
    var fields=Find<DeclaredFieldsView>(root).Single();var tabs=Find<TabControl>(fields).Single();Require(tabs.Items.Count==3&&fields.Fields.Count==54&&fields.Fields.Select(field=>field.FieldId).Distinct().Count()==54&&Find<DeclaredInputFieldsView>(root).Single().Fields.Count==4,"新Runtime三页或54设置/4输入不完整");
    var metadata=((IToolSettingsFields)ToolRegistry.CreateRuntime("BlobAnalysis")).SettingsSchema.Presentation!.Fields;
    var pairs=metadata.Where(item=>item.Layout?.PairWith is not null).ToArray();Require(pairs.Length==14,"Blob配对清单变化未复核");
    foreach(var pair in pairs)
    {
     var left=fields.Fields.Single(item=>item.FieldId==pair.FieldId);var right=fields.Fields.Single(item=>item.FieldId==pair.Layout!.PairWith);
     Require(ReferenceEquals(left.View,right.View)&&Find<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(left.View).Single().SliderType==Native.Numeric.HikPcUI.ListView.SliderArrangeType.between,"Blob实际配对未使用一份原between："+pair.FieldId);
    }
    FieldPresentation Metadata(string property)=>metadata.Single(f=>f.DisplayName==SunEyeVision.Tool.BlobAnalysis.Runtime.BlobParameterDefinition.Instance.Label(property));
    var count=Metadata("BlobCountLimit").Layout!;Require(count.Minimum==1&&count.Maximum==20000&&count.Increment==1,"数量XML范围或步进错误");
    foreach(var name in new[]{"FilterCircularityMin","FilterCircularityMax","FilterRectangularityMin","FilterRectangularityMax"})Require(Metadata(name).Layout!.Increment==.01,"浮点步进丢失");
    tabs.SelectedIndex=1;Pump();var threshold=fields.Fields.Single(f=>f.FieldId==Metadata("Threshold").FieldId);
    var text=(TextBox)threshold.InputElement!;text.Text="201";
    Require(((SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings)context.DraftNode.RuntimeConfiguration!.CaptureSettings()).Threshold==128&&text.Text=="201","尚未提交的原文提前写入草稿");
    Require(threshold.TryCommitPending(),"真实原数值不能提交："+threshold.Error);
    Require(((SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings)node.RuntimeConfiguration!.CaptureSettings()).Threshold==128&&((SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings)context.DraftNode.RuntimeConfiguration!.CaptureSettings()).Threshold==201,"数值没有隔离草稿");
    context.Discard();edits.Close();
    // 旧SDK显示对象/ROI组合用例已退出；新Runtime对应完整显示及本地ROI仍未实现，不能按旧成绩算通过。
   });
   Console.WriteLine($"O06独立窗口检查：{passed}组通过，{failed}组失败。仅自动交互，不代人工外观验收。");return failed==0?0:1;
  }
  finally{foreach(var window in Application.Current.Windows.Cast<Window>().ToArray())window.Close();Application.Current.Shutdown();}
 }
}














