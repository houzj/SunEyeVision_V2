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
 static void AreaRangeVisible(DeclaredFieldsView fields,IReadOnlyList<ConfigurationFieldAccessor> descriptors)
 {
  var low=fields.Fields.Single(f=>f.FieldId==descriptors.Single(d=>d.StoragePath=="FilterAreaMin").Port.Id);var high=fields.Fields.Single(f=>f.FieldId==descriptors.Single(d=>d.StoragePath=="FilterAreaMax").Port.Id);
  var label=(TextBlock)typeof(DeclaredFieldEditor).GetField("_label",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(low)!;Require(label.Text=="面积范围","配对范围标签不是面积范围："+label.Text);
  var areaSwitch=fields.Fields.Single(f=>f.FieldId==descriptors.Single(d=>d.StoragePath=="IsAreaFilterEnabled").Port.Id).InputElement!;
  var rangeInput=Visuals<NativeNumberField>(low).Single().NativeControl;
  var switchLeft=areaSwitch.TransformToAncestor(fields).Transform(new Point()).X;
  var inputLeft=rangeInput.TransformToAncestor(fields).Transform(new Point()).X;
  Require(Math.Abs(switchLeft-inputLeft)<=2,"原VM开关未与参数输入列左对齐：switch="+switchLeft+",input="+inputLeft);
  var measurements=new List<string>();var widthFailures=new List<string>();
  TextBox Verify(string stage)
  {
   var text=(TextBox)high.InputElement!;Require(text.IsVisible&&double.TryParse(text.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var stored)&&stored==9999999,"未检验真实可见9999999样本；实际="+text.Text+"；可见="+text.IsVisible);text.ApplyTemplate();text.UpdateLayout();
   var view=Visuals<FrameworkElement>(text).Single(v=>v.GetType().Name=="TextBoxView");var presenter=Visuals<ScrollContentPresenter>(text).Single();var available=Math.Min(view.ActualWidth,presenter.ActualWidth);var measured=new FormattedText(text.Text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,text.Foreground,VisualTreeHelper.GetDpi(text).PixelsPerDip);var width=measured.WidthIncludingTrailingWhitespace;var first=text.GetRectFromCharacterIndex(0,false);var last=text.GetRectFromCharacterIndex(text.Text.Length-1,true);var actualTextWidth=last.X-first.X;
   var measurement=$"阶段={stage};范围样本文本={text.Text};标签={label.Text};TextBoxActual={text.ActualWidth:R};HostActual={Visuals<ScrollViewer>(text).Single().ActualWidth:R};TextBoxPadding={text.Padding};TextBoxView={view.ActualWidth:R};ScrollContentPresenter={presenter.ActualWidth:R};文字宽={width:R};实际字符宽={actualTextWidth:R};首字符Rect={first};尾字符TrailingRect={last};TextFormattingMode={TextOptions.GetTextFormattingMode(text)};HorizontalOffset={text.HorizontalOffset:R};TextBoxVerticalScroll={text.VerticalScrollBarVisibility};HostVerticalScroll={Visuals<ScrollViewer>(text).Single().VerticalScrollBarVisibility};ComputedVertical={Visuals<ScrollViewer>(text).Single().ComputedVerticalScrollBarVisibility}";Console.WriteLine(measurement);measurements.Add(measurement);File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"scrollbar-minwidth-visible-width.txt"),measurements);
   if(text.HorizontalOffset!=0||width>available+.1)widthFailures.Add(measurement);return text;
  }
  var initial=Verify("Loaded");initial.Focus();Pump();Keyboard.ClearFocus();Pump();Verify("聚焦失焦");
  Click(Find<ToggleButton>(high).Single(b=>Equals(b.ToolTip,"切换为订阅")));Pump();Click(Find<ToggleButton>(high).Single(b=>Equals(b.ToolTip,"切换为数值编辑")));Pump();
  var numeric=Visuals<NativeNumberField>(high).Single();Require(!numeric.ShowSlider&&numeric.NativeControl is Native.Numeric.HikPcUI.ListView.SliderArrangeBase,"配对切回常量未保留原数值控件或单滑条抑制");Verify("切订阅再切常量");Require(widthFailures.Count==0,"9999999可视宽度不足："+string.Join("；",widthFailures));
 }
 static void Show(UIElement view){host.Children.Clear();host.Children.Add(view);owner.UpdateLayout();Pump();}
 static void VerifyPageScroll(DeclaredFieldsView fields,TabControl tabs,IReadOnlyList<ConfigurationFieldAccessor> descriptors,Window shell)
 {
  tabs.SelectedIndex=2;shell.UpdateLayout();Pump();var scroll=(ScrollViewer)((TabItem)tabs.Items[2]).Content;var id=descriptors.Single(d=>d.StoragePath.StartsWith("ResultPresentation.Texts")&&d.StoragePath.EndsWith(".PositionY")).Port.Id;var bottom=fields.Fields.Single(f=>f.FieldId==id);var before=bottom.TransformToAncestor(scroll).TransformBounds(new Rect(0,0,bottom.ActualWidth,bottom.ActualHeight));
  Require(scroll.ScrollableHeight>0,"结果显示页没有可滚动内容");scroll.ScrollToVerticalOffset(scroll.ScrollableHeight);shell.UpdateLayout();Pump();var after=bottom.TransformToAncestor(scroll).TransformBounds(new Rect(0,0,bottom.ActualWidth,bottom.ActualHeight));Require(scroll.VerticalOffset>0&&Math.Abs(scroll.VerticalOffset-scroll.ScrollableHeight)<=1&&after.Top>=0&&after.Bottom<=scroll.ActualHeight+1&&after.Top<before.Top,"ScrollToVerticalOffset未使文本显示位置Y实际呈现到底部");
  Require(Math.Abs(((ScrollBar)scroll.Template.FindName("PART_VerticalScrollBar",scroll)).ActualWidth-12)<.1,"页滚动条实际宽度不是12px");var measurement=$"结果页ActualScrollbarWidth={((ScrollBar)scroll.Template.FindName("PART_VerticalScrollBar",scroll)).ActualWidth:R};结果页ScrollableHeight={scroll.ScrollableHeight:R};VerticalOffset={scroll.VerticalOffset:R};Viewport={scroll.ViewportWidth:R}x{scroll.ViewportHeight:R};BottomFieldBefore={before};BottomFieldAfter={after};ScrollViewer={scroll.ActualWidth:R}x{scroll.ActualHeight:R}";Console.WriteLine(measurement);File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"scrollbar-minwidth-scroll.txt"),measurement);
  var image=new RenderTargetBitmap((int)shell.ActualWidth,(int)shell.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(shell);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"scrollbar-minwidth-text-bottom.png")))encoder.Save(stream);
  scroll.ScrollToVerticalOffset(0);shell.UpdateLayout();Pump();Require(scroll.VerticalOffset==0,"结果页不能滚回顶部");
 }
 static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject
 {if(root is T item)yield return item;foreach(var child in LogicalTreeHelper.GetChildren(root))if(child is DependencyObject d)foreach(var found in Find<T>(d))yield return found;}
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
 static int Main()
 {
  new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Themes/Dark/DarkTheme.xaml"})Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
  owner=new Window{Width=530,Height=700,Title="O06独立真实窗口验证",Content=host=new StackPanel()};owner.Resources.MergedDictionaries.Add(EditorResources.Create());owner.Show();Pump();
  try
  {
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
    Show(field);var adapter=Visuals<NativeNumberField>(field).Single();
    Require(adapter.NativeControl is Native.Numeric.HikPcUI.ListView.SliderArrangeFloat,"声明字段没有消费原PCUI浮点控件");
    var original=(Native.Numeric.HikPcUI.ListView.SliderArrangeFloat)adapter.NativeControl;
    var text=(TextBox)field.InputElement!;text.Focus();Pump();
    var combo=(ComboBox)original.FindName("CBox");var popup=(Popup)combo.Template.FindName("PART_Popup",combo);
    Require(popup.IsOpen,"原浮点控件获得焦点未展开滑条");
    text.Text="0.12345";text.CaretIndex=4;Pump();
    var slider=Visuals<Slider>(popup.Child).Single(item=>item.Name=="SL_Bat1");
    Require(value==.12345&&text.Text=="0.12345"&&text.CaretIndex==4&&Math.Abs(slider.Value-value)<1e-12,
        "原数值输入未保留精度/原文/光标或未同步滑条");
    slider.Value=.67;Pump();Require(value==.67&&double.Parse(text.Text,CultureInfo.InvariantCulture)==.67,"原滑条没有回填同一字段草稿");
    text.Text="bad";SendKey(text,Key.Enter,true);Require(text.IsKeyboardFocused&&field.HasInvalidText&&value==.67&&text.Text=="bad","非法Enter未留焦保留原文和值");
    text.Text="0.75";SendKey(text,Key.Enter,true);Require(!text.IsKeyboardFocused&&!popup.IsOpen&&!field.HasInvalidText&&value==.75,"有效Enter未失焦/关闭原滑条");
    var count=writes;text.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.MouseWheelEvent});Pump();
    Require(value==.75&&writes==count,"未聚焦滚轮修改了原控件字段");
    text.Focus();Pump();text.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.MouseWheelEvent});Pump();
    Require(Math.Abs(value-.76)<1e-12,"已聚焦原控件滚轮未按0.01写回");
   });
   Test("原样式数值范围保留旧线宽且仅主动编辑写入",()=>
   {
    var value=.25;var writes=0;
    var presentation=new FieldPresentation("legacy-stroke","粗细","样式",0,FieldEditorKind.Scalar)
        {Layout=new("结果显示","样式",Minimum:1,Maximum:10,Increment:1)};
    var field=new DeclaredFieldEditor(new("legacy-stroke",DataType.Number,Storage:NumericStorage.Float64),"粗细",new ConstantSetting(value),
        setting=>{value=(double)((ConstantSetting)setting).Value!;writes++;},presentation:presentation);
    Show(field);var text=(TextBox)field.InputElement!;text.Focus();Pump();SendKey(text,Key.Enter,true);
    Require(value==.25&&writes==0&&!field.HasInvalidText,"打开/聚焦/Enter自动夹紧或写入旧线宽");
    var number=Visuals<NativeNumberField>(field).Single().InputControl!;
    Click((ButtonBase)number.FindName("UpButton"));Pump();
    Require(value==1.25&&writes==1,"原箭头未从旧值按单位步长编辑或重复写入");
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
     ((TextBox)line.InputElement!).Text="7";Require(width==7&&writes>0,"样式未更新调用方草稿");
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
    StylePopup(display,_=>{reject=true;((TextBox)line.InputElement!).Text="9";Require(line.HasInvalidText&&width==7,"草稿写入拒绝未保留错误和值");reject=false;((TextBox)line.InputElement!).Text="7";Require(!line.HasInvalidText,"修正后错误未清除");((TextBox)line.InputElement!).Text="bad";Require(line.HasInvalidText&&width==7,"非法输入写入草稿");((TextBox)line.InputElement!).Text="7";});
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
    var fields=Find<DeclaredFieldsView>(root).Single();var tabs=Find<TabControl>(fields).Single();Require(tabs.Items.Count==3&&fields.Fields.Count==54&&Find<DeclaredFieldEditor>(root).Count()==58,"新Runtime三页或54设置/4输入不完整");
    var metadata=((IToolSettingsFields)ToolRegistry.CreateRuntime("BlobAnalysis")).SettingsSchema.Presentation!.Fields;
    FieldPresentation Metadata(string property)=>metadata.Single(f=>f.DisplayName==SunEyeVision.Tool.BlobAnalysis.Runtime.BlobParameterDefinition.Instance.Label(property));
    var count=Metadata("BlobCountLimit").Layout!;Require(count.Minimum==1&&count.Maximum==20000&&count.Increment==1,"数量XML范围或步进错误");
    foreach(var name in new[]{"FilterCircularityMin","FilterCircularityMax","FilterRectangularityMin","FilterRectangularityMax"})Require(Metadata(name).Layout!.Increment==.01,"浮点步进丢失");
    tabs.SelectedIndex=1;Pump();var threshold=fields.Fields.Single(f=>f.FieldId==Metadata("Threshold").FieldId);
    var text=(TextBox)threshold.InputElement!;text.Text="201";
    Require(((SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings)node.RuntimeConfiguration!.CaptureSettings()).Threshold==128&&((SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings)context.DraftNode.RuntimeConfiguration!.CaptureSettings()).Threshold==201,"数值没有隔离草稿");
    context.Discard();edits.Close();
    // 旧SDK显示对象/ROI组合用例已退出；新Runtime对应完整显示及本地ROI仍未实现，不能按旧成绩算通过。
   });
   Console.WriteLine($"O06独立窗口检查：{passed}组通过，{failed}组失败。仅自动交互，不代人工外观验收。");return failed==0?0:1;
  }
  finally{foreach(var window in Application.Current.Windows.Cast<Window>().ToArray())window.Close();Application.Current.Shutdown();}
 }
}














