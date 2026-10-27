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
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;

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
 static void VerifyExpandedReference()
 {
  var sequence=new DeclaredReferenceCandidate("数值来源","source",null,new Port("values",TypeRef.SequenceOf(DataType.Integer)),Array.Empty<string>());
  var index=new DeclaredReferenceCandidate("索引来源","source",null,new Port("index",DataType.Integer),Array.Empty<string>());
  FieldPresentation presentation=new("expanded","数量","参数",0,FieldEditorKind.Scalar){Layout=new("参数","参数",Minimum:0,Maximum:1000)};
  DeclaredFieldEditor Create(Action<ParameterSetting> write,Action<ReferenceExpansionRequest>? expand=null)=>new(
   new("expanded",DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"数量",
   new ReferenceSetting(sequence.Reference(BindingSelection.All)),write,new ReferenceFieldOptions(_=>new[]{sequence,index},EditableIndex:true,Expand:expand),
   new ConstantSetting(7L),presentation:presentation);
  object Session(DeclaredFieldEditor field)=>Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(field).Single().SubscribeDataLeft!;
  void Command(object session,string name,object? argument=null)=>((ICommand)session.GetType().GetProperty(name)!.GetValue(session)!).Execute(argument);
  object Leaf(System.Collections.IEnumerable roots)=>roots.Cast<object>().SelectMany(root=>((System.Collections.IEnumerable)root.GetType().GetProperty("EnumEntrysA")!.GetValue(root)!).Cast<object>()).First();
  Test("原扩展订阅窗实际Owner、原树选择、跟随与关闭释放",()=>
  {
   var writes=0;using var field=Create(_=>writes++);Show(field);var session=Session(field);
   Command(session,"PopSubscribeWindowCommand");Pump();
   var window=owner.OwnedWindows.Cast<Window>().OfType<Native.Composition.Apps.XmlUI.ParamSubscribeWindow>().Single();
   Require(window.Owner==owner&&window.IsVisible&&window.Style is not null,"原窗未取得实际Owner或正式样式");
   var request=(ReferenceExpansionRequest)window.DataContext;
   var tree=(Native.Composition.Apps.XmlUI.ParamTab.SubTreeView)window.FindName("treeView");
   Require(ReferenceEquals(tree.ItemSource,request.SourceNodes),"专窗复制了来源树");
   var node=request.SourceNodes.Cast<object>().SelectMany(root=>((System.Collections.IEnumerable)root.GetType().GetProperty("EnumEntrysA")!.GetValue(root)!).Cast<object>()).Last();
   tree.UpdateLayout();var leaf=Visuals<TreeViewItem>(tree).Single(item=>ReferenceEquals(item.DataContext,node));
   leaf.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});Pump();
   Require(writes==1&&request.IsValid&&window.IsVisible&&field.Setting is ReferenceSetting,"选源关闭专窗或脱离字段状态");
   var oldLeft=owner.Left;owner.Left+=10;Pump();Require(Math.Abs(window.Left-owner.Left-owner.ActualWidth)<2,"专窗未跟随Owner");
   window.Left+=12;Pump();Require(Math.Abs(owner.Left-oldLeft-22)<2,"移动专窗未同步Owner");
   var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
   var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"native-expanded-source.png")))png.Save(stream);
   window.Close();Pump();Require(!request.IsValid,"关闭后请求仍有效");
   var before=writes;request.ClickCommand.Execute(node);Require(writes==before,"已关闭请求仍可写入");
   Command(session,"PopSubscribeWindowCommand");Pump();
   var reopened=owner.OwnedWindows.Cast<Window>().OfType<Native.Composition.Apps.XmlUI.ParamSubscribeWindow>().Single();
   var active=(ReferenceExpansionRequest)reopened.DataContext;Show(new TextBlock{Text="卸载"});
   Require(!active.IsValid&&!reopened.IsVisible,"字段卸载未关窗或未失效");
  });
  Test("扩展请求刷新旧节点、模式隔离与失效回调重入",()=>
  {
   var requests=new List<ReferenceExpansionRequest>();var writes=0;
   using var field=Create(_=>writes++,request=>requests.Add(request));Show(field);var session=Session(field);
   Command(session,"PopSubscribeWindowCommand");var first=requests.Single();var stale=Leaf(first.SourceNodes);
   first.RefreshCommand.Execute(null);first.ClickCommand.Execute(stale);Require(writes==0,"刷新后旧树节点仍有效");
   first.ClickCommand.Execute(Leaf(first.SourceNodes));Require(first.IsValid,"来源选择使专窗请求过早失效");
   var focusType=typeof(Native.Dependencies.Apps.UIHelper.Extern.TextBoxFocusEventArgs);
   var focus=Activator.CreateInstance(focusType)!;
   // The original focus command receives the same bracket caret event as its real text control.
   focusType.GetProperty("Text")!.SetValue(focus,"数值来源 []");focusType.GetProperty("IsFocus")!.SetValue(focus,true);
   focusType.GetProperty("SelectionStart")!.SetValue(focus,6);
   Command(session,"FocusedCommand",focus);Require(first.IsIndex,"括号焦点未切换索引树");
   var before=writes;first.ClickCommand.Execute(Leaf(first.SourceNodes));Require(writes==before,"隐藏来源树迟到命令仍写入");
   first.IndexClickCommand.Execute(Leaf(first.IndexNodes));Require(writes==before+1&&field.Setting is ReferenceSetting{Reference.Selection:BindingSelection.AtField},"索引树未通过同一writer接纳");
   focusType.GetProperty("SelectionStart")!.SetValue(focus,1);Command(session,"FocusedCommand",focus);
   Require(!first.HasSelection&&!first.SourceNodes.Cast<object>().Any()&&!first.IndexNodes.Cast<object>().Any(),"光标移出括号未按原行为清空专窗当前内容");
   first.Invalidated+=()=>Command(session,"PopSubscribeWindowCommand");
   Command(session,"PopSubscribeWindowCommand");Require(requests.Count==2&&requests[1].IsValid&&!first.IsValid,"外层展开覆盖失效回调中新请求");
   field.Dispose();Require(!requests[1].IsValid,"释放未使最新借用请求失效");
  });
  Test("原扩展窗默认方式仅在同一Owner共享且不同入口互斥",()=>
  {
   using var a=Create(_=>{});using var b=Create(_=>{});Show(a);host.Children.Add(b);owner.UpdateLayout();Pump();
   var sa=Session(a);var sb=Session(b);Command(sa,"PopSubscribeWindowCommand");Pump();
   var first=owner.OwnedWindows.Cast<Window>().OfType<Native.Composition.Apps.XmlUI.ParamSubscribeWindow>().Single();
   var request=(ReferenceExpansionRequest)first.DataContext;
   var check=Visuals<CheckBox>(first).Single(check=>check.GetBindingExpression(ToggleButton.IsCheckedProperty)?.ParentBinding.Path?.Path=="PreferExpanded");check.SetCurrentValue(ToggleButton.IsCheckedProperty,true);Pump();
   Require(request.PreferExpanded,"原默认方式CheckBox未写UI偏好");
   Command(sb,"DropDownCommand");Pump();
   var second=owner.OwnedWindows.Cast<Window>().OfType<Native.Composition.Apps.XmlUI.ParamSubscribeWindow>().Single();
   Require(!request.IsValid&&second!=first,"跨入口未互斥或未共享Owner默认方式");
   second.Close();Pump();
   using var other=Create(_=>{});var separate=new Window{Width=500,Height=400,Content=other};
   try{separate.Show();Pump();Command(Session(other),"DropDownCommand");Pump();Require(separate.OwnedWindows.Count==0,"UI偏好泄漏至另一Owner");}
   finally{separate.Close();}
   Command(sb,"PopSubscribeWindowCommand");Pump();
   ((ReferenceExpansionRequest)owner.OwnedWindows.Cast<Window>().OfType<Native.Composition.Apps.XmlUI.ParamSubscribeWindow>().Single().DataContext).PreferExpanded=false;
  });
  Test("原扩展窗可独立构造取得模板和双树",()=>
  {
   var window=new Native.Composition.Apps.XmlUI.ParamSubscribeWindow();
   try{window.Show();Pump();Require(window.Style is not null&&window.FindName("treeView") is not null&&window.FindName("indextreeView") is not null,"独立构造缺原资源或双树");}
   finally{window.Close();}
  });
 }
 static SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiSession CreateRoiSession(IReadOnlyImage image,Func<IReadOnlyList<RoiEditItem>> read,Func<ImageFrameAddress,RoiGeometryChange,bool> accept,Func<bool> current)
 {
  var scope=Guid.NewGuid();long revision=0;
  SunEyeVision.Plugin.Wpf.Adapters.Regions.RoiEditSnapshot Snapshot()=>new(new(scope,revision,image.Frame),read());
  return new(image,Snapshot,(expected,change)=>
  {
   if(expected.ScopeId!=scope||expected.Revision!=revision||expected.Frame!=image.Frame)return new(false,Snapshot(),"过期候选");
   if(!accept(expected.Frame,change))return new(false,Snapshot(),"宿主拒绝");revision++;return new(true,Snapshot());
  },current);
 }
 static void VerifyNativeBox()
 {
  Test("原Box分支展开与五参数、实际归一化图形同对象双向及单次接纳",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();
   var image=resources.CreateGray8(320,240,Enumerable.Repeat((byte)100,320*240).ToArray());
   var first=Guid.NewGuid();var second=Guid.NewGuid();
   var shape=SunEyeVision.Plugin.Wpf.Regions.RoiGeometryEditing.Create("rectangle",new(20.123456,30.987654),new(110.543216,160.123458));
   var rows=new List<SunEyeVision.Plugin.Wpf.Regions.RoiEditItem>{new("detect",first,"检测一",shape),new("mask",second,"屏蔽一",SunEyeVision.Plugin.Wpf.Regions.RoiGeometryEditing.Create("rectangle",new(160,80),new(250,180)))};
   var writes=0;var alive=true;var reject=false;
   using var session=CreateRoiSession(image,()=>rows,(frame,change)=>
   {
    Require(frame==image.Frame&&alive,"物理帧身份丢失");if(reject)return false;
    var index=rows.FindIndex(row=>row.Group==change.Group&&row.Id==change.Id);Require(index>=0,"目标ID丢失");
    rows[index]=rows[index] with{Geometry=change.Geometry};writes++;return true;
   },()=>alive);
   using var editor=new SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiEditor();editor.Bind(session);
   using var viewport=new SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiViewport();viewport.Bind(session);
   var scene=new Grid{Height=420};scene.ColumnDefinitions.Add(new(){Width=new GridLength(420)});scene.ColumnDefinitions.Add(new());
   scene.Children.Add(editor);Grid.SetColumn(viewport,1);scene.Children.Add(viewport);owner.Width=1000;Show(scene);
   session.Select("detect",first);Pump();editor.NativeView.ApplyTemplate();
   var expand=(ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView);Require(expand.IsVisible&&expand.IsChecked==false,"原展开行未初始折叠");
   expand.IsChecked=true;Pump();editor.UpdateLayout();Pump();
   var stack=Visuals<Native.Composition.Apps.XmlUI.ParamTabControl.BoxROIStack>(editor).Single();
   Require(ReferenceEquals(stack.ROIData,session.CurROIData),"原Box参数栈未共享选中投影");
   var fields=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(stack).ToArray();Require(fields.Length==5,"未使用五个原Apps.XmlUI数值");
   var combo=(ComboBox)fields[0].FindName("CBox");combo.ApplyTemplate();
   var numeric=(Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl)combo.Template.FindName("T1",combo);
   var text=(TextBox)numeric.FindName("ValueText");Require(text.IsVisible,"原Box文字未真实显示；stack="+stack.IsVisible+"；field="+fields[0].IsVisible);
   var oldY=SunEyeVision.Plugin.Wpf.Regions.RoiGeometryEditing.Center(rows[0].Geometry).Y;var oldWidth=rows[0].Geometry.Get<double>("width");
   text.Focus();Pump();text.Text="bad";SendKey(text,Key.Enter,true);Require(writes==0&&text.IsKeyboardFocused&&editor.HasInvalidText,"非法数值未拒绝或失焦");
   text.Text="80.12";SendKey(text,Key.Enter,true);Require(writes==1&&!text.IsKeyboardFocused&&!editor.HasInvalidText,"合法Box Enter未单次提交失焦："+editor.Error);
   Require(SunEyeVision.Plugin.Wpf.Regions.RoiGeometryEditing.Center(rows[0].Geometry).X==80.12&&SunEyeVision.Plugin.Wpf.Regions.RoiGeometryEditing.Center(rows[0].Geometry).Y==oldY&&rows[0].Geometry.Get<double>("width")==oldWidth,"编辑X量化未编辑精度或图像尺寸转换两次");
   var roi=Visuals<Native.Display.VMBaseControls.Base.ImageView.ROI.NormalizedRectROI>(viewport).Single(item=>ReferenceEquals(item.DataContext,session.CurROIData));
   Require(Math.Abs(roi.RectWidth-oldWidth)<1e-8,"原归一化手柄宽度不是像素");
   Require(roi.Template is not null&&roi.ContainerWidth>1&&roi.ContainerHeight>1&&roi.ContainerPosition.X>0&&viewport.NativeView.Source is BitmapSource,"原图形仅有DTO而未取得实际尺寸/位置或图像源："+roi.ContainerWidth+","+roi.ContainerHeight+","+roi.ContainerPosition);
   var container=Visuals<Native.Display.VMBaseControls.Base.ImageView.ContainerItem>(viewport).Single(item=>ReferenceEquals(item.DataContext,session.CurROIData));
   Require(container.ActualWidth>1&&container.ActualHeight>1&&container.Position.X>0,"实际ROI容器未承接尺寸/位置："+container.ActualWidth+","+container.ActualHeight+","+container.Position+"；native="+roi.ActualWidth+","+roi.ActualHeight+"；bindings="+BindingOperations.IsDataBound(roi,Native.Display.VMBaseControls.Base.ImageView.ROI.RectROI.ContainerWidthProperty)+","+BindingOperations.IsDataBound(roi,Native.Display.VMBaseControls.Base.ImageView.ROI.ROIControlBase.ContainerPositionProperty));
   roi.SetCurrentValue(Native.Display.VMBaseControls.Base.ImageView.ROI.RectROI.RectWidthProperty,120d);
   BindingOperations.GetMultiBindingExpression(roi,Native.Display.VMBaseControls.Base.ImageView.ROI.RectROI.RectWidthProperty)!.UpdateSource();
   typeof(Native.Display.VMBaseControls.Base.ImageView.ROI.ROIControlBase).GetMethod("NotifyROIParamChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(roi,null);Pump();
   Require(writes==2&&rows[0].Geometry.Get<double>("width")==120,"原图形结束通知未恰好写一次同Guid几何");
   CaptureInline(scene,"native-box-scene.png");
   var stale=(SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeBoxRoiProjection)session.CurROIData!;session.Select("mask",second);Pump();
   stale.Width=.8;stale.DataChangedCommand.Execute(stale);Require(writes==2,"旧选中对象回调仍写入");
   var selected=(SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeBoxRoiProjection)session.CurROIData!;reject=true;selected.Width=.4;Require(!session.TryCommit(selected)&&writes==2&&!selected.HasPending,"宿主拒绝未撤整组候选");reject=false;
   var currentField=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).First();
   var currentCombo=(ComboBox)currentField.FindName("CBox");currentCombo.ApplyTemplate();
   var currentNumeric=(Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl)currentCombo.Template.FindName("T1",currentCombo);
   var currentText=(TextBox)currentNumeric.FindName("ValueText");
   currentText.Focus();Pump();currentText.Text="190.25";
   BindingOperations.SetBinding(currentField,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeBase.StartValueProperty,new Binding("MissingGeometry"){Source=new object()});
   Require(BindingOperations.GetBindingExpressionBase(currentField,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeBase.StartValueProperty)?.Status!=BindingStatus.Active,"测试未破坏原外层绑定");
   var missingAccepted=editor.TryCommitPending();
   Require(!missingAccepted&&writes==2&&!selected.HasPending,"缺原外层绑定仍借手工投影接纳：accepted="+missingAccepted+"；writes="+writes+"；pending="+selected.HasPending+"；"+editor.Error);
   alive=false;selected.Width=.5;selected.DataChangedCommand.Execute(selected);Require(writes==2,"过期物理帧仍写入");
   Show(new TextBlock{Text="卸载"});viewport.Dispose();editor.Dispose();session.Dispose();owner.Width=530;
  });
  Test("原Box实际BGR/BGRA行跨度与透明通道不被丢弃",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();
   foreach(var format in new[]{PixelLayout.Bgr24,PixelLayout.Bgra32})
   {
    var bpp=format==PixelLayout.Bgr24?3:4;var stride=2*bpp+4;
    var frame=new ImageFrameAddress(Guid.NewGuid(),"box-color");
    var domain=resources.CreateRegion(new("color",CoordinateDomain.Image,CoordinateUnit.Pixel),frame,new[]{new RegionRun(0,0,1),new RegionRun(1,0,1)});
    var bytes=Enumerable.Repeat((byte)231,stride*2).ToArray();
    var expected=new byte[4*bpp];
    for(var row=0;row<2;row++)for(var x=0;x<2;x++)for(var channel=0;channel<bpp;channel++)
     bytes[row*stride+x*bpp+channel]=expected[(row*2+x)*bpp+channel]=(byte)(10+row*60+x*20+channel*7);
    var image=resources.CreateImage(new(2,2,stride,format),frame,domain,bytes);
    using var session=CreateRoiSession(image,()=>Array.Empty<RoiEditItem>(),(_,_)=>throw new Exception("显示不应写几何"),()=>true);
    using var viewport=new SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiViewport();viewport.Bind(session);Show(viewport);Pump();
    var source=viewport.NativeView.Source as BitmapSource;Require(source is not null&&source.PixelWidth==2&&source.PixelHeight==2,"实际彩色位图缺失");
    Require(source!.Format==(bpp==3?PixelFormats.Bgr24:PixelFormats.Bgra32),"像素格式被替换");
    var actual=new byte[expected.Length];source.CopyPixels(actual,2*bpp,0);Require(actual.SequenceEqual(expected),"行padding进入画面或BGRA通道丢失");
    Require(image.CopyPixels().SequenceEqual(bytes),"显示修改了算法冻结像素");
   }
   Show(new TextBlock{Text="彩色显示卸载"});
  });
  Test("原ROI观察者异常仍释放图像租约且关闭后读取不复活",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();
   var image=new TrackedRoiImage(resources.CreateGray8(8,8,new byte[64]));
   var rows=new[]{new RoiEditItem("detect",Guid.NewGuid(),"一",RoiGeometryEditing.Create("rectangle",new(1,1),new(5,5)))};
   using var session=CreateRoiSession(image,()=>rows,(_,_)=>false,()=>true);
   var viewport=new SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiViewport();viewport.Bind(session);Show(viewport);session.Select("detect",rows[0].Id);Pump();
   Require(image.Active==1,"显示未保持实际帧租约");session.CurROIData!.PropertyChanged+=(_,_)=>throw new InvalidOperationException("几何观察者异常");
   var rejected=false;try{viewport.Dispose();}catch(Exception){rejected=true;}
   Require(rejected&&image.Active==0&&viewport.Child is null&&viewport.NativeView.ImageDataSource is null,"观察者异常跳过视口清理");
   var invalidated=false;session.PropertyChanged+=(_,_)=>throw new InvalidOperationException("会话观察者异常");session.Changed+=()=>invalidated=true;
   ((System.Collections.Specialized.INotifyCollectionChanged)session.Items).CollectionChanged+=(_,_)=>throw new InvalidOperationException("集合观察者异常");
   try{session.Dispose();}catch(Exception){}
   Require(invalidated&&!session.IsCurrent&&session.Items.Count==0,"关闭异常跳过失效通知或保留旧对象");
   SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiSession? closing=null;
   using var readSession=CreateRoiSession(image,()=>{closing?.Dispose();return rows;},(_,_)=>false,()=>true);
   closing=readSession;readSession.Refresh();Require(!readSession.IsCurrent&&readSession.Items.Count==0,"读取回调关闭后重建旧对象");
   Show(new TextBlock{Text="异常释放完成"});
  });
  Test("原ROI取得像素中同步重绑只保留新帧租约",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();
   var oldImage=new TrackedRoiImage(resources.CreateGray8(8,8,new byte[64]));
   var newImage=new TrackedRoiImage(resources.CreateGray8(8,8,Enumerable.Repeat((byte)199,64).ToArray()));
   using var oldSession=CreateRoiSession(oldImage,()=>Array.Empty<RoiEditItem>(),(_,_)=>false,()=>true);
   using var newSession=CreateRoiSession(newImage,()=>Array.Empty<RoiEditItem>(),(_,_)=>false,()=>true);
   using var viewport=new SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiViewport();Show(viewport);
   oldImage.OnAcquire=()=>viewport.Bind(newSession);viewport.Bind(oldSession);Pump();
   Require(oldImage.Active==0&&newImage.Active==1&&ReferenceEquals(viewport.NativeView.ROIList,newSession.Items),"旧Acquire覆盖或泄漏新帧");
   viewport.Dispose();Require(newImage.Active==0,"最终新帧租约未释放");Show(new TextBlock{Text="重绑完成"});
  });
  Test("原Box卸载拒绝旧UI命令且同对象重载保留键入原文",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(100,100,new byte[10000]);
   var row=new RoiEditItem("detect",Guid.NewGuid(),"一",RoiGeometryEditing.Create("rectangle",new(10,10),new(50,50)));var writes=0;
   using var session=CreateRoiSession(image,()=>new[]{row},(_,change)=>{row=row with{Geometry=change.Geometry};writes++;return true;},()=>true);
   using var editor=new SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiEditor();editor.Bind(session);
   editor.Loaded+=(_,args)=>{if(ReferenceEquals(args.OriginalSource,editor))session.Select("detect",row.Id);};
   Show(editor);Pump();
   var expand=(ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView);expand.IsChecked=true;Pump();
   var field=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).First();var combo=(ComboBox)field.FindName("CBox");combo.ApplyTemplate();
   var leaf=(Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl)combo.Template.FindName("T1",combo);var text=(TextBox)leaf.FindName("ValueText");
   text.Focus();Pump();text.Text="35.125";
   var context=editor.NativeView.DataContext;var command=(ICommand)context.GetType().GetProperty("ROIValueChangedCommand")!.GetValue(context)!;
   Show(new TextBlock{Text="离开参数页"});command.Execute(null);Require(writes==0,"卸载后排队原命令继续写入");
   Show(editor);Pump();
   var current=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).First();var currentCombo=(ComboBox)current.FindName("CBox");currentCombo.ApplyTemplate();
   var currentLeaf=(Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl)currentCombo.Template.FindName("T1",currentCombo);var currentText=(TextBox)currentLeaf.FindName("ValueText");
   Require(currentText.Text=="35.125"&&writes==0,"重载丢失未提交原文或提前写入：actual="+currentText.Text+"；old="+text.Text+"；writes="+writes);
   Require(editor.TryCommitPending()&&writes==1,"同对象重载后无法显式捕获原文："+editor.Error);
   Show(new TextBlock{Text="结束"});
  });
 }
 sealed class TrackedRoiImage(IReadOnlyImage source):IReadOnlyImage
 {
  public int Active;public Action? OnAcquire;
  public int Width=>source.Width;public int Height=>source.Height;public ImageLayout Layout=>source.Layout;
  public ImageFrameAddress Frame=>source.Frame;public IReadOnlyRegion EffectiveDomain=>source.EffectiveDomain;
  public byte[] CopyPixels()=>source.CopyPixels();
  public IImagePixelReadLease AcquirePixels()
  {var lease=source.AcquirePixels();Active++;var action=OnAcquire;OnAcquire=null;try{action?.Invoke();return new TrackedLease(this,lease);}catch{Active--;lease.Dispose();throw;}}
  sealed class TrackedLease(TrackedRoiImage owner,IImagePixelReadLease source):IImagePixelReadLease
  {
   bool disposed;public ImageLayout Layout=>source.Layout;public ImageFrameAddress Frame=>source.Frame;
   public IReadOnlyRegion EffectiveDomain=>source.EffectiveDomain;public ReadOnlySpan<byte> GetRow(int row)=>source.GetRow(row);
   public IRegionReadLease AcquireDomainReader()=>source.AcquireDomainReader();
   public void Dispose(){if(disposed)return;disposed=true;try{source.Dispose();}finally{owner.Active--;}}
  }
 }
 static RecordValue SectorShape(double start=170,double sweep=40,double outer=110.123456,double inner=40.234567)
 {
  var values=RoiGeometryEditing.Values(RoiGeometryEditing.Create("annulus",new(220.123456,130.987654),new(330.123456,130.987654)));
  values["kind"]="sector";values["inner-radius"]=inner;values["outer-radius"]=outer;
  values["start-radians"]=start*Math.PI/180;values["end-radians"]=(start+sweep)*Math.PI/180;
  return RoiGeometryEditing.Checked(values);
 }
 sealed class RoiHost(IReadOnlyImage image,params RoiEditItem[] rows)
 {
  internal readonly List<RoiEditItem> Rows=rows.ToList();internal long Revision;internal int Writes,Reads;
  internal bool Alive=true,Reject;internal Action? OnRead,BeforeAccept;
  internal Guid ScopeId=Guid.NewGuid();
  internal RoiEditStamp Stamp=>new(ScopeId,Revision,image.Frame);
  internal RoiEditSnapshot Read(){Reads++;OnRead?.Invoke();return new(Stamp,Rows.ToArray());}
  internal RoiEditAcceptance Accept(RoiEditStamp expected,RoiGeometryChange change)
  {
   var before=BeforeAccept;BeforeAccept=null;before?.Invoke();
   if(expected!=Stamp||Reject)return new(false,new(Stamp,Rows.ToArray()),"宿主拒绝或旧根");
   var index=Rows.FindIndex(row=>row.Group==change.Group&&row.Id==change.Id);Require(index>=0,"提交身份丢失");
   Rows[index]=Rows[index] with{Geometry=change.Geometry};Revision++;Writes++;return new(true,new(Stamp,Rows.ToArray()));
  }
  internal NativeRoiSession Open()=>new(image,Read,Accept,()=>Alive);
 }
 static TextBox RoiText(Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat field)
 {
  var box=(ComboBox)field.FindName("CBox");box.ApplyTemplate();
  return (TextBox)((Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl)box.Template.FindName("T1",box)).FindName("ValueText");
 }
 static void VerifyNativeSector()
 {
  Test("原Sector六项/非方帧换算、跨180扫角、实际原图形和单次提交",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();
   var image=resources.CreateGray8(640,320,Enumerable.Repeat((byte)90,640*320).ToArray());
   var hostModel=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"扇形",SectorShape()));
   using var session=hostModel.Open();using var editor=new NativeRoiEditor();using var viewport=new NativeRoiViewport();
   editor.Bind(session);viewport.Bind(session);var scene=new Grid{Height=500};scene.ColumnDefinitions.Add(new(){Width=new GridLength(420)});scene.ColumnDefinitions.Add(new());
   scene.Children.Add(editor);scene.Children.Add(viewport);Grid.SetColumn(viewport,1);owner.Width=1000;Show(scene);
   session.Select("detect",hostModel.Rows[0].Id);Pump();
   ((ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView)).IsChecked=true;Pump();
   var stack=Visuals<Native.Composition.Apps.XmlUI.ParamTabControl.SectorROIStack>(editor).Single();
   var fields=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(stack).ToArray();Require(fields.Length==6,"未取得原六参数");
   var selected=(NativeSectorRoiProjection)session.CurROIData!;
   Require(ReferenceEquals(stack.ROIData,selected)&&Math.Abs(fields[1].StartValue-130.987654)<1e-8&&Math.Abs(fields[2].StartValue-110.123456)<1e-8,"Y或半径非原W/H尺度");
   var old=hostModel.Rows[0].Geometry;var x=RoiText(fields[0]);x.Focus();Pump();x.Text="320.125";SendKey(x,Key.Enter,true);
   Require(hostModel.Writes==1&&!x.IsKeyboardFocused&&!editor.HasInvalidText,"原扇形Enter未单次失焦提交："+editor.Error);
   var next=hostModel.Rows[0].Geometry;Require(RoiGeometryEditing.Center(next).X==320.125&&RoiGeometryEditing.Center(next).Y==RoiGeometryEditing.Center(old).Y&&next.Get<double>("inner-radius")==old.Get<double>("inner-radius")&&next.Get<double>("start-radians")==old.Get<double>("start-radians"),"单项编辑量化其他分量");
   for(var repeat=0;repeat<3;repeat++)Require(editor.TryCommitPending()&&hostModel.Writes==1,"持焦高精度原文格式化后被误判再次写入");
   var roi=Visuals<Native.Display.VMBaseControls.Base.ImageView.ROI.NormalizedSectorROI>(viewport).Single();
   var container=Visuals<Native.Display.VMBaseControls.Base.ImageView.ContainerItem>(viewport).Single(item=>ReferenceEquals(item.DataContext,selected));
   Require(roi.Template is not null&&container.ActualWidth>1&&container.ActualHeight>1&&container.Position.X>0,"原扇形实际容器未显示");
   Require(Math.Abs(roi.CircleCenterPoint.Y-RoiGeometryEditing.Center(next).Y)<1e-8&&Math.Abs(roi.CircleRadius-next.Get<double>("outer-radius"))<1e-8,"原手柄不属于同帧像素");
   roi.CircleRadius=125.5;
   Require(BindingOperations.GetMultiBindingExpression(roi,Native.Display.VMBaseControls.Base.ImageView.ROI.SectorROI.CircleRadiusProperty)?.Status==BindingStatus.Active,"原手柄CLR赋值覆盖归一化绑定");
   typeof(Native.Display.VMBaseControls.Base.ImageView.ROI.ROIControlBase).GetMethod("NotifyROIParamChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(roi,null);Pump();
   Require(hostModel.Writes==2&&Math.Abs(hostModel.Rows[0].Geometry.Get<double>("outer-radius")-125.5)<1e-8,"原手柄结束未接纳同对象");
   Require(Math.Abs(selected.AngleRange-40)<1e-8&&selected.EndAngle<0,"跨180把Range当end或短弧");
   CaptureInline(scene,"native-sector-scene.png");
   selected.AngleRange=360;Require(session.TryCommit(selected),"实际视口整圆未接纳");Pump();
   CaptureInline(scene,"native-sector-full-circle.png");
   var path=(System.Windows.Shapes.Path)roi.Template.FindName("path",roi);
   Require(path.Data.Bounds.Width>=roi.ContainerWidth*.95&&path.Data.Bounds.Height>=roi.ContainerHeight*.95,
       "整圆退成径向线："+path.Data.Bounds+"；container="+roi.ContainerWidth+","+roi.ContainerHeight);
   var move=(Grid)roi.Template.FindName("move",roi);
   move.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
   if(ReferenceEquals(Mouse.Captured,move))
   {
    roi.CircleRadius=130;Mouse.Capture(null);Pump();
    Require(!selected.HasPending&&!viewport.NativeView.IsDragROI&&hostModel.Writes==3,"外部丢失捕获未撤手势或误提交");
    Console.WriteLine("取证：原扇形实际捕获及外部释放已执行；不代真实指针移动。");
   }
   else
   {
    Require(!viewport.NativeView.IsDragROI&&!selected.HasPending&&hostModel.Writes==3,"捕获失败仍残留拖动或写入：drag="+viewport.NativeView.IsDragROI+"；pending="+selected.HasPending+"；writes="+hostModel.Writes);
    Console.WriteLine("未证：本进程无实际鼠标捕获，仅验证失败拒绝与收尾；真实捕获丢失/指针交互仍需验收。");
   }
   Show(new TextBlock{Text="扇形卸载"});owner.Width=530;
  });
  Test("原Sector内外半径联合候选、零角拒绝与整圆保真",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[640*320]);
   var model=new RoiHost(image,new RoiEditItem("mask",Guid.NewGuid(),"扇形",SectorShape(15,90,50,40)));
   using var session=model.Open();session.Select("mask",model.Rows[0].Id);var item=(NativeSectorRoiProjection)session.CurROIData!;
   item.Radius=80d/640;item.MinRadius=60d/640;Require(session.TryCommit(item)&&model.Writes==1&&model.Rows[0].Geometry.Get<double>("inner-radius")==60,"完整新半径被中间状态拒绝");
   item.MinRadius=90d/640;Require(!session.TryCommit(item)&&model.Writes==1&&!item.HasPending,"零厚度原联动候选未被规范几何拒绝");
   item.AngleRange=0;Require(!session.TryCommit(item)&&model.Writes==1,"退化零扫角通过");
   item.AngleRange=360;Require(session.TryCommit(item)&&model.Writes==2,"整圆被拒绝");
   item.AngleRange=360.01;Require(!session.TryCommit(item)&&model.Writes==2,"原最大360范围被放大");
   var values=RoiGeometryEditing.Values(model.Rows[0].Geometry);values["start-radians"]=1.72d;values["end-radians"]=1.72+2*Math.PI;
   model.Rows[0]=model.Rows[0] with{Geometry=RoiGeometryEditing.Checked(values)};model.Revision++;session.Refresh();
   var before=model.Rows[0].Geometry;item.Radius=100d/640;Require(session.TryCommit(item),"浮点整圆投影越过360");
   Require(model.Rows[0].Geometry.Get<double>("start-radians")==before.Get<double>("start-radians")&&model.Rows[0].Geometry.Get<double>("end-radians")==before.Get<double>("end-radians"),"无关半径编辑改动整圆角度原值");
   var errors=0;session.PropertyChanged+=(_,e)=>{if(e.PropertyName=="Error"&&session.Error.Length>0)errors++;};
   model.Reject=true;item.CenterX=.4;Require(!session.TryCommit(item)&&errors>0&&!item.HasPending,"同版本宿主拒绝未通知错误或撤候选");
  });
  Test("区域版本拒绝同ID重建及接纳前换根，非变化刷新保留手势",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(32,32,new byte[1024]);
   var row=new RoiEditItem("detect",Guid.NewGuid(),"矩形",RoiGeometryEditing.Create("rectangle",new(2,2),new(10,10)));
   var model=new RoiHost(image,row);using var session=model.Open();session.Select(row.Group,row.Id);var item=(NativeBoxRoiProjection)session.CurROIData!;
   item.Width=.5;session.Refresh();Require(item.HasPending&&item.Width==.5,"无变化Refresh丢手势");
   model.Rows.Clear();model.Rows.Add(row with{});model.Revision++;Require(!session.TryCommit(item)&&model.Writes==0&&!item.HasPending,"同ID同几何重建未拒绝旧版本");
   item.Width=.6;model.BeforeAccept=()=>model.Revision++;
   Require(!session.TryCommit(item)&&model.Writes==0&&!item.HasPending,"read后原子accept前换根仍接纳");
   var count=model.Reads;model.OnRead=()=>session.Refresh();session.Refresh();model.OnRead=null;Require(model.Reads==count+1,"快照read回调Refresh递归");
   var notifications=0;var reentry=false;
   ((System.Collections.Specialized.INotifyCollectionChanged)session.Items).CollectionChanged+=(_,_)=>{if(!session.IsCurrent)return;Require(session.Items.Count==2,"观察到半集合");reentry=session.Select(row.Group,row.Id);throw new InvalidOperationException("集合观察者");};
   ((System.Collections.Specialized.INotifyCollectionChanged)session.Items).CollectionChanged+=(_,_)=>notifications++;
   model.Rows.Add(row with{Id=Guid.NewGuid()});model.Revision++;
   try{session.Refresh();}catch(InvalidOperationException){}
   Require(session.Items.Count==2&&!reentry&&notifications==1,"通知重入编辑或首个错误阻断后续集合通知");
  });
  Test("取消通知中关闭不复活选择，接纳后观察者异常不伪装拒绝",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(32,32,new byte[1024]);
   var first=new RoiEditItem("detect",Guid.NewGuid(),"一",RoiGeometryEditing.Create("rectangle",new(2,2),new(8,8)));var second=first with{Id=Guid.NewGuid()};
   var model=new RoiHost(image,first,second);using var session=model.Open();session.Select(first.Group,first.Id);var item=(NativeBoxRoiProjection)session.CurROIData!;
   var errorObserver=false;Action observer=()=>{if(model.Writes>0){errorObserver=true;throw new InvalidOperationException("已接纳后观察者错误");}};session.Changed+=observer;
   item.Width=.4;var threw=false;try{session.TryCommit(item);}catch(InvalidOperationException){threw=true;}
   Require(threw&&errorObserver&&model.Writes==1&&!item.HasPending&&model.Rows[0].Geometry.Get<double>("width")==.4*32,"接纳后错误被当作拒绝或模型回滚");
   session.Changed-=observer;item.PropertyChanged+=(_,_)=>session.Dispose();Require(!session.Select(second.Group,second.Id)&&session.CurROIData is null,"取消回调关闭后重新选择已Detach对象");
  });
 }
 static RecordValue RingShape(double outer=80.123456,double inner=20.234567)
 {
  var values=RoiGeometryEditing.Values(SectorShape(0,360,outer,inner));values["kind"]="annulus";
  values["center"]=RoiGeometryEditing.Point(new(200.123456,100.987654));values["start-radians"]=1.72;values["end-radians"]=1.72+2*Math.PI;
  return RoiGeometryEditing.Checked(values);
 }
 sealed class RebindRoiConverter(IMultiValueConverter inner):IMultiValueConverter
 {
  public Action? BeforeBack,BeforeConvert;
  public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture)
  {var action=BeforeConvert;BeforeConvert=null;action?.Invoke();return inner.Convert(values,targetType,parameter,culture);}
  public object[] ConvertBack(object value,Type[] types,object parameter,CultureInfo culture)
  {var action=BeforeBack;BeforeBack=null;action?.Invoke();return inner.ConvertBack(value,types,parameter,culture);}
 }
 static void VerifyNativeRing()
 {
  Test("原SectorRing四项、非方帧同原双圆图形及单次Enter",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(800,400,new byte[320000]);
   var model=new RoiHost(image,new RoiEditItem("mask",Guid.NewGuid(),"环形",RingShape()));using var session=model.Open();
   using var editor=new NativeRoiEditor();using var viewport=new NativeRoiViewport();editor.Bind(session);viewport.Bind(session);
   var scene=new Grid{Height=500};scene.ColumnDefinitions.Add(new(){Width=new GridLength(420)});scene.ColumnDefinitions.Add(new());scene.Children.Add(editor);scene.Children.Add(viewport);Grid.SetColumn(viewport,1);owner.Width=1000;Show(scene);
   session.Select("mask",model.Rows[0].Id);Pump();((ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView)).IsChecked=true;Pump();
   var stack=Visuals<Native.Composition.Apps.XmlUI.ParamTabControl.SectorRingROIStack>(editor).Single();
   var fields=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(stack).ToArray();var item=(NativeRingRoiProjection)session.CurROIData!;
   Require(fields.Length==4&&ReferenceEquals(stack.ROIData,item),"不是原四项圆环栈与同一投影");
   var ringButton=Visuals<ToggleButton>(editor).Single(button=>Equals(button.CommandParameter,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.SectorRing));
   var ringIcon=(Image)ringButton.Template.FindName("icon",ringButton);
   Require(ReferenceEquals(ringButton.Style,editor.NativeView.FindResource("RingRoiBtnStyle"))&&ringIcon.Source.ToString().Contains("ROI_circle_click.png",StringComparison.Ordinal)&&!ringIcon.Source.ToString().Contains("Geometry_circle",StringComparison.Ordinal),"圆环误用了CircleMes圆形图标/样式");

   foreach(var field in fields)Require(BindingOperations.GetMultiBindingExpression(field,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeBase.StartValueProperty)?.Status==BindingStatus.Active,"圆环原外层不是有效MultiBinding");
   Require(Math.Abs(fields[1].StartValue-100.987654)<1e-8&&Math.Abs(fields[2].StartValue-80.123456)<1e-8&&Math.Abs(item.Radius-80.123456/800)<1e-12,"非方帧半径或Y用错尺度");
   var before=model.Rows[0].Geometry;var y=RoiText(fields[1]);y.Focus();Pump();y.Text="35.125";SendKey(y,Key.Enter,true);
   Require(model.Writes==1&&!y.IsKeyboardFocused&&RoiGeometryEditing.Center(model.Rows[0].Geometry).Y==35.125,"圆环Enter未精确单次提交并失焦："+editor.Error);
   for(var n=0;n<3;n++)Require(editor.TryCommitPending()&&model.Writes==1,"圆环显示舍入重复接纳");
   y.Focus();Pump();y.Text="35.125";SendKey(y,Key.Enter,true);Require(model.Writes==1&&!editor.HasInvalidText&&!y.IsKeyboardFocused&&y.Text=="35.13","圆环no-op成功未清原文/错误或误写："+y.Text);
   Require(model.Rows[0].Geometry.Get<double>("start-radians")==before.Get<double>("start-radians")&&model.Rows[0].Geometry.Get<double>("end-radians")==before.Get<double>("end-radians"),"完整圆环原始角端点被量化/归零");
   var roi=Visuals<Native.Display.VMBaseControls.Base.ImageView.ROI.NormalizedCircleROI>(viewport).Single();
   roi.MinRadius=24;typeof(Native.Display.VMBaseControls.Base.ImageView.ROI.ROIControlBase).GetMethod("NotifyROIParamChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(roi,null);Pump();
   Require(model.Writes==2&&Math.Abs(model.Rows[0].Geometry.Get<double>("inner-radius")-24)<1e-8,"原圆环内径手柄结束未写同一对象");
   Require(BindingOperations.GetMultiBindingExpression(roi,Native.Display.VMBaseControls.Base.ImageView.ROI.CircleROI.MinRadiusProperty)?.Status==BindingStatus.Active,"原Circle CLR赋值覆盖绑定");
   var path=(System.Windows.Shapes.Path)roi.Template.FindName("pt",roi);var geometry=(GeometryGroup)path.Data;var outer=(System.Windows.Media.EllipseGeometry)geometry.Children[0];var inner=(System.Windows.Media.EllipseGeometry)geometry.Children[1];
   var container=Visuals<Native.Display.VMBaseControls.Base.ImageView.ContainerItem>(viewport).Single(value=>ReferenceEquals(value.DataContext,item));
   Require(geometry.FillRule==FillRule.EvenOdd&&geometry.Children.Count==2&&outer.RadiusX>inner.RadiusX&&Math.Abs(inner.RadiusX-24*roi.ControlToImageRadio)<1e-8&&outer.RadiusX==outer.RadiusY,"原双Ellipse几何或尺度错误");
   Require(Math.Abs(container.ActualWidth-2*roi.CircleRadius*roi.ControlToImageRadio)<1e-7&&Math.Abs(container.ActualHeight-container.ActualWidth)<1e-7&&path.ActualWidth>1&&path.RenderedGeometry.Bounds.Width>1,"原圆环实际容器/图形未显示");
   CaptureInline(scene,"native-sector-ring-scene.png");
   var move=(Grid)roi.Template.FindName("move",roi);move.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
   var captured=ReferenceEquals(Mouse.Captured,move);if(captured){roi.MinRadius=30;Mouse.Capture(null);Pump();}
   Require(!viewport.NativeView.IsDragROI&&!item.HasPending&&model.Writes==2,"原圆环捕获失败/外部丢失仍留拖动或写入");
   Console.WriteLine(captured?"圆环：已执行实际捕获丢失收尾；不代真实指针移动。":"圆环：当前进程未获得实际鼠标捕获，仅核失败收尾；物理指针仍未验。");
   var current=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).First();var text=RoiText(current);text.Focus();Pump();text.Text="35.125";
   var oldContext=editor.NativeView.DataContext;var oldCommand=(ICommand)oldContext.GetType().GetProperty("ROIValueChangedCommand")!.GetValue(oldContext)!;
   Show(new TextBlock{Text="离开圆环"});oldCommand.Execute(null);Require(model.Writes==2,"圆环卸载旧命令写入");
   Show(scene);Pump();var restored=RoiText(Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).First());Require(restored.Text=="35.125"&&model.Writes==2,"圆环重载原文丢失或提前写入："+restored.Text);
   Require(editor.TryCommitPending()&&model.Writes==3,"圆环原文重载捕获失败："+editor.Error);Show(new TextBlock());owner.Width=530;
  });
  Test("圆环整组半径候选、互夹退化拒绝及部分环准入",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(800,400,new byte[320000]);
   var model=new RoiHost(image,new RoiEditItem("mask",Guid.NewGuid(),"环",RingShape(50,40)));using var session=model.Open();session.Select("mask",model.Rows[0].Id);using var editor=new NativeRoiEditor();editor.Bind(session);Show(editor);Pump();((ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView)).IsChecked=true;Pump();
   var fields=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).ToArray();var outer=RoiText(fields[2]);var inner=RoiText(fields[3]);outer.Focus();Pump();outer.Text="30";
   var firstResult=editor.TryCommitPending();Require(!firstResult&&model.Writes==0&&outer.Text=="30"&&model.Rows[0].Geometry.Get<double>("outer-radius")==50,"单外径互夹退化未拒绝/未保原文：result="+firstResult+";writes="+model.Writes+";raw="+outer.Text+";focus="+outer.IsKeyboardFocused+";error="+editor.Error+";outer="+model.Rows[0].Geometry.Get<double>("outer-radius")+";inner="+model.Rows[0].Geometry.Get<double>("inner-radius"));
   outer.Text="50";Require(editor.TryCommitPending()&&model.Writes==0&&!editor.HasInvalidText&&outer.Text=="50.00","恢复已接受外径仍写入或未清原文/错误："+outer.Text);var ring=(NativeRingRoiProjection)session.CurROIData!;ring.Radius=80d/800;ring.MinRadius=60d/800;Require(session.TryCommit(ring)&&model.Writes==1&&model.Rows[0].Geometry.Get<double>("inner-radius")==60&&model.Rows[0].Geometry.Get<double>("outer-radius")==80,"联合半径被中间夹紧破坏："+editor.Error);Pump();
   outer=RoiText(Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).ElementAt(2));inner=RoiText(Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).ElementAt(3));
   inner.Focus();Pump();inner.Text="90";Require(!editor.TryCommitPending()&&model.Writes==1,"内径超外径未拒绝零厚度");inner.Text="0";Require(editor.TryCommitPending()&&model.Writes==2&&model.Rows[0].Geometry.Get<double>("inner-radius")==0,"合法内径0未接纳");
   var values=RoiGeometryEditing.Values(RingShape());values["end-radians"]=2.72;var partial=new RoiHost(image,new RoiEditItem("mask",Guid.NewGuid(),"部分环",RoiGeometryEditing.Checked(values)));var rejected=false;try{using var invalid=partial.Open();}catch(NotSupportedException){rejected=true;}Require(rejected,"部分annulus被四项原完整圆环静默接纳");Show(new TextBlock());
  });
  Test("原环四项切Box五项与Sector六项不串身份或旧原文",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(200,100,new byte[20000]);var id=Guid.NewGuid();
   var model=new RoiHost(image,new RoiEditItem("mask",id,"环",RingShape()),new RoiEditItem("detect",id,"框",RoiGeometryEditing.Create("rectangle",new(10,10),new(50,40))),new RoiEditItem("mask",Guid.NewGuid(),"扇",SectorShape()));using var session=model.Open();using var editor=new NativeRoiEditor();editor.Bind(session);Show(editor);session.Select("mask",id);Pump();((ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView)).IsChecked=true;Pump();
   var old=session.CurROIData!;var text=RoiText(Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).First());text.Focus();Pump();text.Text="bad";
   Require(!session.Select("detect",id)&&ReferenceEquals(session.CurROIData,old),"非法环原文未阻止切到同Guid另一组");text.Text="200";Require(session.Select("detect",id),"合法环值不能切组");Pump();Require(Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).Count()==5,"四项切框未得五项");
   Require(session.Select("mask",model.Rows[2].Id),"不能切扇形");Pump();Require(Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).Count()==6,"五项切扇未得六项");
   model.Rows[0]=model.Rows[0] with{Geometry=SectorShape()};model.Revision++;session.Refresh();((NativeRingRoiProjection)old).Radius=.3;Require(!old.HasPending&&!session.TryCommit(old),"同ID换形后旧环对象仍可写");Show(new TextBlock());
  });
  Test("圆环损坏实际外层绑定拒绝且保留原文",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(800,400,new byte[320000]);var geometry=RingShape();var model=new RoiHost(image,new RoiEditItem("mask",Guid.NewGuid(),"环",geometry));using var session=model.Open();session.Select("mask",model.Rows[0].Id);using var editor=new NativeRoiEditor();editor.Bind(session);Show(editor);Pump();((ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView)).IsChecked=true;Pump();
   var field=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).First();BindingOperations.SetBinding(field,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeBase.StartValueProperty,new Binding("Missing"){Source=new object(),Mode=BindingMode.TwoWay});Pump();Require(BindingOperations.GetBindingExpressionBase(field,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeBase.StartValueProperty)?.Status!=BindingStatus.Active,"坏绑定夹具仍有效");var text=RoiText(field);text.Focus();Pump();text.Text="300";
   Require(!editor.TryCommitPending()&&model.Writes==0&&ReferenceEquals(model.Rows[0].Geometry,geometry)&&text.Text=="300","坏圆环绑定仍写入或丢原文");Show(new TextBlock());
  });
  Test("拒绝恢复UpdateTarget重入换根不把旧原文写到新圆环",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(800,400,new byte[320000]);var oldModel=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"扇",SectorShape()));var newModel=new RoiHost(image,new RoiEditItem("mask",Guid.NewGuid(),"环",RingShape()));using var oldSession=oldModel.Open();using var newSession=newModel.Open();oldSession.Select("detect",oldModel.Rows[0].Id);newSession.Select("mask",newModel.Rows[0].Id);using var editor=new NativeRoiEditor();editor.Bind(oldSession);Show(editor);Pump();((ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView)).IsChecked=true;Pump();var field=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).First();var original=BindingOperations.GetMultiBinding(field,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeBase.StartValueProperty)!;var hook=new RebindRoiConverter(original.Converter);var replacement=new MultiBinding{Mode=BindingMode.TwoWay,Converter=hook};foreach(var binding in original.Bindings)replacement.Bindings.Add(binding);BindingOperations.SetBinding(field,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeBase.StartValueProperty,replacement);Pump();hook.BeforeConvert=()=>editor.Bind(newSession);
   typeof(NativeRoiEditor).GetMethod("RestoreRaw",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,new object[]{new string?[]{"300","301","302","303","304","305"},oldSession.CurROIData!});Pump();
   var fields=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).ToArray();Require(fields.Length==4&&oldModel.Writes==0&&newModel.Writes==0&&RoiText(fields[0]).Text!="300"&&Math.Abs(fields[0].StartValue-200.123456)<1e-8,"拒绝恢复污染新根或继续遍历新控件");Show(new TextBlock());
  });
  Test("原绑定更新中同步切换六项到四项不写新会话",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(800,400,new byte[320000]);
   var oldModel=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"扇",SectorShape()));var newModel=new RoiHost(image,new RoiEditItem("mask",Guid.NewGuid(),"环",RingShape()));using var oldSession=oldModel.Open();using var newSession=newModel.Open();oldSession.Select("detect",oldModel.Rows[0].Id);newSession.Select("mask",newModel.Rows[0].Id);
   using var editor=new NativeRoiEditor();editor.Bind(oldSession);Show(editor);Pump();((ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView)).IsChecked=true;Pump();var fields=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).ToArray();var first=fields[0];var original=BindingOperations.GetMultiBinding(first,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeBase.StartValueProperty)!;var hook=new RebindRoiConverter(original.Converter);
   var replacement=new MultiBinding{Mode=BindingMode.TwoWay,Converter=hook};foreach(var binding in original.Bindings)replacement.Bindings.Add(binding);BindingOperations.SetBinding(first,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeBase.StartValueProperty,replacement);Pump();
   var text=RoiText(first);text.Focus();Pump();text.Text="300";hook.BeforeBack=()=>editor.Bind(newSession);
   Require(!editor.TryCommitPending()&&oldModel.Writes==0&&newModel.Writes==0,"绑定重入仍写旧/新根");Pump();var current=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(editor).ToArray();Require(current.Length==4&&Math.Abs(current[0].StartValue-200.123456)<1e-8,"旧六项绑定继续污染新四项");Show(new TextBlock());
  });
 }
 static int Main()
 {
  new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Themes/Dark/DarkTheme.xaml"})Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
  owner=new Window{Width=530,Height=700,Title="O06独立真实窗口验证",Content=host=new StackPanel()};owner.Resources.MergedDictionaries.Add(EditorResources.Create());owner.Show();Pump();
  try
  {
   Test("原数值内部文字不继承页面最小高度且完整落在边框内",()=>
   {
    foreach(var leaf in new FrameworkElement[]{new Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl(),new Native.Numeric.HikPcUI.ListView.NumericControl(),new Native.Numeric.HikPcUI.Controls.NumericUpDown()})
    {
     Show(leaf);Pump();
     var text=Visuals<TextBox>(leaf).Single();text.Text="320.13";Pump();
     Require(text.MinHeight==0&&text.Style is null,"内部编辑框被宿主隐式样式污染："+leaf.GetType().FullName);
     var bounds=text.TransformToAncestor(leaf).TransformBounds(new Rect(text.RenderSize));
     Require(bounds.Top>=0&&bounds.Bottom<=leaf.ActualHeight+.1,"文字编辑区域超出原件："+bounds+" / "+leaf.ActualHeight);
     Require(text.ActualHeight>=text.FontSize*1.2,"内部文字高度不足："+text.ActualHeight);
    }
   });
   VerifyBetween();
   VerifyExpandedReference();
   VerifyNativeBox();
   VerifyNativeSector();
   VerifyNativeRing();
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
