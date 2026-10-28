using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
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
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetCapture();
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
 struct NativePoint {public int X,Y;}
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]
 [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
 static extern bool GetCursorPos(out NativePoint point);
 static Window owner=null!;static StackPanel host=null!;static int passed,failed;
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static void Test(string name,Action run){var filter=Environment.GetEnvironmentVariable("O06_WINDOW_CASE_FILTER");if(!string.IsNullOrEmpty(filter)&&!name.Contains(filter,StringComparison.Ordinal))return;try{run();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e);}}
 static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
 sealed class ColorLifetimeDraft : IToolSettingsDraft
 {
  public static IReadOnlyList<NativeGraphicDisplayOptions> Graphics {get;}=new[]{new NativeGraphicDisplayOptions("shape","visible","rgba","ng","opacity","width")};
  public static ToolSettingsViewOptions Options()=>new(GraphicDisplays:Graphics);
  private Dictionary<string,object?> _values=new(){["visible"]=true,["rgba"]=PresentationDeclarations.Color(10,20,30,200),["ng"]=PresentationDeclarations.Color(255,0,0),["opacity"]=1d,["width"]=3d};
  public int Writes {get;private set;}
  public ToolSchema Schema {get;}=CreateSchema();
  static ToolSchema CreateSchema()
  {
   var ports=new[]{new Port("visible",DataType.Boolean),new Port("rgba",PresentationDeclarations.Rgba),new Port("ng",PresentationDeclarations.Rgba),new Port("opacity",DataType.Number),new Port("width",DataType.Number)};
   var fields=ports.Select((port,i)=>new FieldPresentation(port.Id,new[]{"Blob","OK颜色","NG颜色","透明度","粗细"}[i],"图像显示",i,port.Type.Shape==ValueShape.Record?FieldEditorKind.Record:FieldEditorKind.Scalar)
   {Layout=new("结果显示","图像显示",DialogGroup:"shape",DialogTitle:"Blob",DialogPrimary:i==0,Minimum:i==3?0:i==4?1:null,Maximum:i==3?1:i==4?10:null,Increment:i==3?.01:1)});
   return new(ports,Array.Empty<Port>(),new ToolPresentation(fields));
  }
  public object CaptureSettings()=>ReadFields();
  public InputValues ReadFields()=>new(Schema.Inputs,_values);
  public void ApplyFields(IReadOnlyDictionary<string,object?> changes,Action<object> acceptCandidate)
  {
   var next=new Dictionary<string,object?>(_values);foreach(var item in changes)next[item.Key]=item.Value;
   var prepared=new InputValues(Schema.Inputs,next);acceptCandidate(prepared);_values=prepared.CopyValues().ToDictionary(item=>item.Key,item=>item.Value);Writes++;
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
 {
        var seen=new HashSet<DependencyObject>();var pending=new Stack<DependencyObject>();pending.Push(root);
        while(pending.Count>0)
        {
            var node=pending.Pop();if(!seen.Add(node))continue;if(node is T found)yield return found;
            foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())pending.Push(child);
            if(node is SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView list)
                foreach(var row in list.Items.OfType<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>())if(row.View is {} view)pending.Push(view);
        }
    }
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
 static Native.Input.Apps.PcUI.Controls.PopupButton OpenStyle(FrameworkElement scope)
 {
  var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(scope).Single(item=>Equals(item.Tag,"NativeGraphicStylePopup"));
  popup.Focus();popup.IsDropDownOpen=true;Pump();Require(popup.IsDropDownOpen,"原样式弹层未打开");return popup;
 }
 static Native.Input.HikPcUI.Controls.PopupButton Palette(Native.Input.HikPcUI.ListView.ColorPickerControlView picker)
  =>((Grid)picker.Content).Children.OfType<Native.Input.HikPcUI.Controls.PopupButton>().Single();
 static void StylePopup(FrameworkElement scope,Action<FrameworkElement> edit)
 {
  var count=Application.Current.Windows.Count;var popup=OpenStyle(scope);
  Require(Application.Current.Windows.Count==count,"样式编辑创建独立Window");
  var surface=(FrameworkElement)((Popup)popup.Template.FindName("PART_Popup",popup)).Child;surface.UpdateLayout();Pump();
  try{edit(surface);}finally{popup.IsDropDownOpen=false;Pump();}
 }
 static KeyEventArgs SendKey(UIElement target,Key key,bool preview=false)
 {
  var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target)!,Environment.TickCount,key){RoutedEvent=preview?Keyboard.PreviewKeyDownEvent:Keyboard.KeyDownEvent};
  target.RaiseEvent(args);Pump();return args;
 }
 sealed class ResultOwnerProbe(bool fail=false):IDisposable
 {
  public int Releases;public void Dispose(){Releases++;if(fail)throw new InvalidOperationException("owner释放异常");}
 }
 static void PublicationThrows<T>(Action action) where T:Exception
 {var caught=false;try{action();}catch(T){caught=true;}Require(caught,"未发生预期异常："+typeof(T).Name);}
 static void VerifyResultPublication()
 {
  var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("value",DataType.Integer),new Port("optional",DataType.Text,Required:false)});
  var run=new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"result-fixture",Guid.NewGuid());
  InputValues Values(long value)=>new(schema.Outputs,new Dictionary<string,object?>{{"value",value}});
  ResultPublicationStamp Stamp()=>new(Guid.NewGuid(),run,7,schema.Fingerprint);
  Test("结果publication共享借阅与owner退休最后释放",()=>
  {
   var release=new ResultOwnerProbe();var values=Values(11);var publication=new ResultPublication(schema,Stamp(),ResultViewState.Success,values,release,()=>true);
   var first=publication.Borrow();var second=publication.Borrow();
   Require(first.Stamp==publication.Stamp&&second.Stamp==publication.Stamp&&ReferenceEquals(first.Values,values)&&ReferenceEquals(second.Values,values),"借阅不是同publication/重新复制了冻结值");
   publication.Dispose();Require(!publication.IsCurrent()&&release.Releases==0,"退休提前释放借阅");
   PublicationThrows<ObjectDisposedException>(()=>{using var rejected=publication.Borrow();});
   Require(first.Values!.Get<long>("value")==11&&second.Values!.Get<long>("value")==11,"退休破坏现有借阅");
   first.Dispose();first.Dispose();Require(release.Releases==0,"首借阅释放共享owner");
   PublicationThrows<ObjectDisposedException>(()=>{_=first.Values;});
   second.Dispose();publication.Dispose();Require(release.Releases==1,"owner未恰好释放一次");
  });
  Test("结果publication最终释放异常不重复归还",()=>
  {
   var release=new ResultOwnerProbe(true);var publication=new ResultPublication(schema,Stamp(),ResultViewState.Success,Values(12),release,()=>true);
   var lease=publication.Borrow();publication.Dispose();Require(release.Releases==0,"有借阅时释放owner");
   PublicationThrows<InvalidOperationException>(lease.Dispose);lease.Dispose();publication.Dispose();
   Require(release.Releases==1&&!publication.IsCurrent(),"释放异常后重放或复活");
   PublicationThrows<ObjectDisposedException>(()=>{_=lease.Values;});
   var immediate=new ResultOwnerProbe(true);var plain=new ResultPublication(schema,Stamp(),ResultViewState.Success,Values(13),immediate,()=>true);
   PublicationThrows<InvalidOperationException>(plain.Dispose);plain.Dispose();Require(immediate.Releases==1,"零借阅释放异常重放");
  });
  Test("结果publication错声明拒绝不转移owner",()=>
  {
   var release=new ResultOwnerProbe();var wrong=new InputValues(new[]{new Port("value",DataType.Text)},new Dictionary<string,object?>{{"value","wrong"}});
   PublicationThrows<ArgumentException>(()=>{using var bad=new ResultPublication(schema,Stamp(),ResultViewState.Success,wrong,release,()=>true);});
   var full=new ToolSchema(new[]{new Port("input",DataType.Integer)},schema.Outputs);
   PublicationThrows<ArgumentException>(()=>{using var bad=new ResultPublication(full,new(Guid.NewGuid(),run,7,full.Fingerprint),ResultViewState.Success,Values(1),release,()=>true);});
   PublicationThrows<ArgumentException>(()=>{using var bad=new ResultPublication(schema,new(Guid.NewGuid(),run,7,"wrong"),ResultViewState.Success,Values(1),release,()=>true);});
   Require(release.Releases==0,"失败构造接管owner");release.Dispose();Require(release.Releases==1,"调用方不能释放owner");
  });
  Test("结果publication完整状态及元数据冻结",()=>
  {
   var issues=new List<Diagnostic>{new("original","原诊断")};var names=new Dictionary<string,string>{{"value","原名称"}};var displayed=new HashSet<string>{"value"};
   using var pub=new ResultPublication(schema,Stamp(),ResultViewState.Success,Values(14),null,()=>true,issues,names,displayed);
   issues.Clear();names["value"]="改名";displayed.Add("optional");
   Require(pub.Diagnostics.Count==1&&pub.DisplayNames["value"]=="原名称"&&!pub.DisplayedPorts.Contains("optional"),"元数据容器未冻结");
   foreach(var state in Enum.GetValues<ResultViewState>())
   {
    var declaration=state==ResultViewState.Declaration;var success=state is ResultViewState.Success or ResultViewState.PartialSuccess;
    using var item=new ResultPublication(schema,declaration?new(Guid.NewGuid(),null,null,schema.Fingerprint):Stamp(),state,success?Values(15):null,null,()=>true);
    using var borrow=item.Borrow();Require(item.State==state&&(borrow.Values is not null)==success,"状态/值语义合并："+state);
   }
   PublicationThrows<ArgumentException>(()=>{using var bad=new ResultPublication(schema,Stamp(),ResultViewState.Failed,Values(1),null,()=>true);});
   PublicationThrows<ArgumentException>(()=>{using var bad=new ResultPublication(schema,Stamp(),ResultViewState.Success,null,null,()=>true);});
  });
  Test("结果publication当前性重入退休不会复活",()=>
  {
   var release=new ResultOwnerProbe();var calls=0;ResultPublication? publication=null;
   publication=new(schema,Stamp(),ResultViewState.Success,Values(16),release,()=>{calls++;publication!.Dispose();return true;});
   var lease=publication.Borrow();Require(!publication.IsCurrent()&&calls==1&&release.Releases==0,"重入退休返回当前或提前释放");
   Require(lease.Values!.Get<long>("value")==16&&!publication.IsCurrent()&&calls==1,"借阅失效或重复当前性外调");
   lease.Dispose();publication.Dispose();Require(release.Releases==1,"重入退休释放错误");
  });
  Test("结果publication同run不同发布替换真实行和选择",()=>
  {
   var a=Stamp();var b=Stamp();var active=a.PublicationId;
   using var first=new ResultPublication(schema,a,ResultViewState.Success,Values(21),null,()=>active==a.PublicationId);
   using var second=new ResultPublication(schema,b,ResultViewState.Success,Values(22),null,()=>active==b.PublicationId);
   var projection=new ResultProjectionDescriptor(ResultProjectionKind.StandardTable,ResultCardinality.One,new[]{new ResultColumnDescriptor("value","值",new("value"))});
   using var view=new DeclaredResultTable(schema,projection);view.Height=160;host.Children.Add(view);
   try
   {
    ResultSelection? selected=null;view.SelectionHighlightChanged+=(_,value)=>selected=value;
    view.Present(first);owner.UpdateLayout();Pump();var grid=(DataGrid)view.Content;grid.SelectedIndex=0;Pump();
    Require(selected?.Publication==a,"初次选择无publication");var old=grid.Items[0];var command=view.CurResultScrollCommand;
    object? Cell(object row)=>row.GetType().GetProperty("Item")!.GetValue(row,new object[]{"value"});
    Require(Equals(Cell(old),21L),"首次行不是实际值21");
    active=b.PublicationId;view.Present(second);Pump();
    Require(grid.Items.Count==1&&!ReferenceEquals(old,grid.Items[0])&&grid.SelectedItem is null&&selected?.Publication==b&&selected.Targets.Count==0,"同run不同snapshot未换行/清选择");
    Require(Equals(Cell(grid.Items[0]),22L),"新publication仍投影旧值");
    command.Execute(10000d);Require(grid.Items.Count==1,"旧命令改新结果");grid.SelectedIndex=0;Pump();Require(selected?.Publication==b,"新行发旧发布身份");
   }
   finally{host.Children.Remove(view);}
  });
 }
 sealed class ResultRegionProbe(IReadOnlyRegion inner):IReadOnlyRegion
 {
  public int Acquired,Released,Active;public bool FailFrame;public Action? BeforeFrame;
  public CoordinateFrame Frame=>inner.Frame;public ImageFrameAddress SourceFrame=>inner.SourceFrame;
  public IRegionReadLease AcquireReader(){var lease=inner.AcquireReader();Acquired++;Active++;return new Reader(this,lease);}
  sealed class Reader(ResultRegionProbe probe,IRegionReadLease lease):IRegionReadLease
  {
   bool closed;
   public CoordinateFrame Frame {get{var callback=probe.BeforeFrame;probe.BeforeFrame=null;callback?.Invoke();if(probe.FailFrame)throw new InvalidOperationException("预期来源读取异常");return lease.Frame;}}
   public ImageFrameAddress SourceFrame=>lease.SourceFrame;public bool IsEmpty=>lease.IsEmpty;public int RunCount=>lease.RunCount;
   public ReadOnlyMemory<RegionRun> ReadRuns(int offset,int count)=>lease.ReadRuns(offset,count);
   public void Dispose(){if(closed)return;closed=true;try{lease.Dispose();}finally{probe.Active--;probe.Released++;}}
  }
 }
 static void VerifyPropertyPublication()
 {
  var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("value",DataType.Integer)});
  var projection=new ResultProjectionDescriptor(ResultProjectionKind.PropertyList,ResultCardinality.One);
  var run=new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"tree-fixture",Guid.NewGuid());
  ResultPublication Create(ResultOwnerProbe release)=>new(schema,new(Guid.NewGuid(),run,1,schema.Fingerprint),ResultViewState.Success,
   new InputValues(schema.Outputs,new Dictionary<string,object?>{{"value",18L}}),release,()=>true);
  Test("原属性树publication路径通知重入退休不发旧选择",()=>
  {
   var release=new ResultOwnerProbe();using var pub=Create(release);
   using var view=new SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.DeclaredPropertyList(schema,projection);
   var inner=(SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.PropertyListView)view.View;
   Show(inner);view.Present(pub);Pump();
   var root=inner.ItemsSource!.Cast<SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.DeclaredPropertyList.Row>().Single();var row=root.Children.Single();
   var descriptor=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.PropertyListView.SelectedItemPathProperty,inner.GetType())!;
   var retired=false;EventHandler change=(_,_)=>{if(!retired&&inner.SelectedItemPath is not null){retired=true;view.Clear();}};
   var events=new List<ResultSelection>();view.SelectionHighlightChanged+=(_,value)=>events.Add(value);descriptor.AddValueChanged(inner,change);
   try{var command=inner.SelectedCommand!;Require(command.CanExecute(row),"真实声明行不能选路径");command.Execute(row);}
   finally{descriptor.RemoveValueChanged(inner,change);}
   Require(retired&&!inner.ItemsSource!.Cast<object>().Any()&&events.Count==1&&events[0].ElementIdentity is null&&events[0].Publication==pub.Stamp,"退休后仍发旧路径选择或未清根");
   pub.Dispose();Require(release.Releases==1,"属性树退休未归还最后借阅");
  });
  Test("原属性树publication观察异常仍逐项清理与归还借阅",()=>
  {
   var release=new ResultOwnerProbe();using var pub=Create(release);
   using var view=new SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.DeclaredPropertyList(schema,projection);
   var inner=(SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.PropertyListView)view.View;
   Show(inner);view.Present(pub);Pump();
   var descriptor=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.PropertyListView.SelectedCommandProperty,inner.GetType())!;
   EventHandler fail=(_,_)=>{if(inner.SelectedCommand is null)throw new InvalidOperationException("预期DP观察异常");};
   descriptor.AddValueChanged(inner,fail);pub.Dispose();Require(release.Releases==0,"owner退休提前释放树借阅");
   try{PublicationThrows<AggregateException>(view.Clear);}finally{descriptor.RemoveValueChanged(inner,fail);}
   Require(inner.SelectedCommand is null&&inner.CopyPathCommand is null&&inner.SelectedItem is null&&inner.SelectedItemPath is null&&inner.ErrorInfo is null
    &&!inner.ItemsSource!.Cast<object>().Any()&&release.Releases==1,"首观察异常截断剩余清理或泄漏SDK owner");
  });
  Test("原属性树publication分页观察异常不丢完整页或后续入口",()=>
  {
   var pagedSchema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("values",TypeRef.SequenceOf(DataType.Integer))});
   using var pub=new ResultPublication(pagedSchema,new(Guid.NewGuid(),run,1,pagedSchema.Fingerprint),ResultViewState.Success,
    new InputValues(pagedSchema.Outputs,new Dictionary<string,object?>{{"values",Enumerable.Range(0,300).Select(i=>(long)i).ToArray()}}),null,()=>true);
   using var view=new SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.DeclaredPropertyList(pagedSchema,projection);
   var inner=(SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.PropertyListView)view.View;
   Show(inner);view.Present(pub);Pump();
   var root=inner.ItemsSource!.Cast<SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.DeclaredPropertyList.Row>().Single();
   var sequence=root.Children.Single();var notices=0;
   System.Collections.Specialized.NotifyCollectionChangedEventHandler fail=(_,_)=>{notices++;throw new InvalidOperationException("预期分页观察异常");};
   var children=(System.Collections.Specialized.INotifyCollectionChanged)sequence.Children;children.CollectionChanged+=fail;
   try{PublicationThrows<AggregateException>(()=>sequence.IsExpanded=true);}finally{children.CollectionChanged-=fail;}
   Require(notices==1&&sequence.Children.Count==129,"异常通知留下半页或缺More");
   var oldMore=sequence.Children.Last();oldMore.IsExpanded=true;
   Require(sequence.Children.Count==257&&sequence.Children[127].DisplayValue=="127"&&sequence.Children[255].DisplayValue=="255","第二页偏移不正确或丢失");
   var nextMore=sequence.Children.Last();nextMore.IsExpanded=true;
   Require(sequence.Children.Count==300&&sequence.Children.Last().DisplayValue=="299","最后一页不能继续");
   oldMore.IsExpanded=false;oldMore.IsExpanded=true;Require(sequence.Children.Count==300,"旧More仍改变已完成页");
   view.Clear();view.Present(pub);var replacement=inner.ItemsSource!.Cast<SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.DeclaredPropertyList.Row>().Single().Children.Single();
   oldMore.IsExpanded=false;oldMore.IsExpanded=true;Require(replacement.Children.Count==0,"退休More驱动新根");
  });
 }
 static void VerifyResultOrigins()
 {
  Test("结果来源实际图像Domain坐标正负向",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(64,32,new byte[2048]);
   var run=new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"frame-fixture",Guid.NewGuid());
   void Check(CoordinateFrame coordinates,bool accepted)
   {
    var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("id",DataType.Integer),new Port("point",TypeRef.GeometryOf(GeometryKind.Point,coordinates)),new Port("image",TypeRef.ResourceOf(ResourceKind.Image))});
    var projection=new ResultProjectionDescriptor(ResultProjectionKind.StandardTable,ResultCardinality.One,new[]{new ResultColumnDescriptor("id","标识",new("id"),Geometry:new("point"),GeometrySource:new("image"))});
    var values=new InputValues(schema.Outputs,new Dictionary<string,object?>{{"id",17L},{"point",new PointGeometry(coordinates,new(10,12))},{"image",image}});
    using var pub=new ResultPublication(schema,new(Guid.NewGuid(),run,1,schema.Fingerprint),ResultViewState.Success,values,null,()=>true);
    using var view=new DeclaredResultTable(schema,projection);view.Height=140;Show(view);
    if(!accepted){PublicationThrows<ArgumentException>(()=>view.Present(pub));Require(((DataGrid)view.Content).Items.Count==0,"错误Domain候选装入表");return;}
    ResultSelection? selected=null;view.SelectionHighlightChanged+=(_,value)=>selected=value;
    view.Present(pub);owner.UpdateLayout();Pump();var grid=(DataGrid)view.Content;grid.SelectedIndex=0;Pump();
    Require(selected is not null&&selected.Targets.Count==1&&selected.Targets[0].ImageFrame==image.Frame&&selected.Targets[0].Coordinates==image.EffectiveDomain.Frame,"来源被硬编码默认逻辑帧或丢失实际Domain");
   }
   Require(image.EffectiveDomain.Frame!=RoiDeclarations.ImageFrame,"fixture未覆盖非默认实际Domain");
   Check(image.EffectiveDomain.Frame,true);Check(RoiDeclarations.ImageFrame,false);
  });
  Test("结果来源同尺寸双物理帧分组和租约异常及重入",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var a=resources.CreateGray8(64,32,new byte[2048]);var b=resources.CreateGray8(64,32,new byte[2048]);
   Require(a.Frame!=b.Frame&&a.Width==b.Width&&a.Height==b.Height,"没有同尺寸不同物理帧");
   var ra=new ResultRegionProbe(resources.CreateRegion(RoiDeclarations.ImageFrame,a.Frame,new[]{new RegionRun(5,4,20)}));
   var rb=new ResultRegionProbe(resources.CreateRegion(RoiDeclarations.ImageFrame,b.Frame,new[]{new RegionRun(7,6,22)}));
   var point=TypeRef.GeometryOf(GeometryKind.Point,RoiDeclarations.ImageFrame);var polygon=TypeRef.SequenceOf(point,CountConstraint.Exactly(4));
   var rowType=TypeRef.RecordOf("design.result-frame-row",new[]{new Port("id",DataType.Integer),new Port("point",point),new Port("polygon",polygon),new Port("source-a",TypeRef.ResourceOf(ResourceKind.Region)),new Port("source-b",TypeRef.ResourceOf(ResourceKind.Region))});
   var rowsType=TypeRef.SequenceOf(rowType);var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("rows",rowsType)});
   ResultFieldPath P(string member)=>new("rows",new[]{member});
   var projection=new ResultProjectionDescriptor(ResultProjectionKind.StandardTable,ResultCardinality.Many,new[]{
    new ResultColumnDescriptor("a","点",P("id"),Geometry:P("point"),GeometrySource:P("source-a")),
    new ResultColumnDescriptor("b","多边形",P("id"),Geometry:P("polygon"),GeometrySource:P("source-b"))},new("rows"),"id");
   var row=new RecordValue(rowType,new Dictionary<string,object?>{{"id",17L},{"point",new PointGeometry(RoiDeclarations.ImageFrame,new(10,12))},
    {"polygon",new SequenceValue(polygon,new object?[]{new PointGeometry(RoiDeclarations.ImageFrame,new(4,5)),new PointGeometry(RoiDeclarations.ImageFrame,new(20,5)),new PointGeometry(RoiDeclarations.ImageFrame,new(20,18)),new PointGeometry(RoiDeclarations.ImageFrame,new(4,18))})},{"source-a",ra},{"source-b",rb}});
   var values=new InputValues(schema.Outputs,new Dictionary<string,object?>{{"rows",new SequenceValue(rowsType,new[]{row})}});
   var run=new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"multi-fixture",Guid.NewGuid());
   using var pub=new ResultPublication(schema,new(Guid.NewGuid(),run,1,schema.Fingerprint),ResultViewState.Success,values,null,()=>true);
   using var view=new DeclaredResultTable(schema,projection);view.Height=150;Show(view);view.Present(pub);owner.UpdateLayout();Pump();
   ResultSelection? selected=null;view.SelectionHighlightChanged+=(_,value)=>selected=value;var grid=(DataGrid)view.Content;grid.SelectedIndex=0;Pump();
   Require(selected?.ElementIdentity is 17L&&selected.Targets.Count==2,"多物理source未分组");
   Require(selected!.Targets.Single(target=>target.ImageFrame==a.Frame).Geometry.Single() is PointGeometry&&selected.Targets.Single(target=>target.ImageFrame==b.Frame).Geometry.Single() is PolygonGeometry,"按尺寸或全局帧覆盖来源");
   Require(ra.Active==0&&rb.Active==0&&ra.Acquired>0&&ra.Acquired==ra.Released&&rb.Acquired==rb.Released,"来源读取租约未归还");
   var old=grid.Items[0];ra.FailFrame=true;
   try{PublicationThrows<InvalidOperationException>(()=>view.Present(pub));}finally{ra.FailFrame=false;}
   Require(ReferenceEquals(old,grid.Items[0])&&ra.Active==0&&ra.Acquired==ra.Released,"读取异常半装结果或漏lease");
   ra.BeforeFrame=()=>view.Clear();view.Present(pub);Require(grid.Items.Count==0&&ra.Active==0&&ra.Acquired==ra.Released,"来源回调Clear后旧结果回装");
   var emptyType=TypeRef.SequenceOf(point,CountConstraint.Any);
   var emptySchema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("metric",DataType.Integer),new Port("geometry",emptyType),new Port("source",TypeRef.ResourceOf(ResourceKind.Region))});
   var emptyProjection=new ResultProjectionDescriptor(ResultProjectionKind.StandardTable,ResultCardinality.One,new[]{new ResultColumnDescriptor("metric","值",new("metric"),Geometry:new("geometry"),GeometrySource:new("source"))});
   var emptyValues=new InputValues(emptySchema.Outputs,new Dictionary<string,object?>{{"metric",1L},{"geometry",new SequenceValue(emptyType,Array.Empty<object?>())},{"source",ra}});
   using var emptyPublication=new ResultPublication(emptySchema,new(Guid.NewGuid(),run,1,emptySchema.Fingerprint),ResultViewState.Success,emptyValues,null,()=>true);
   using var emptyView=new DeclaredResultTable(emptySchema,emptyProjection);emptyView.Height=150;Show(emptyView);
   var beforeEmpty=ra.Acquired;emptyView.Present(emptyPublication);owner.UpdateLayout();Pump();ResultSelection? emptySelection=null;
   emptyView.SelectionHighlightChanged+=(_,value)=>emptySelection=value;((DataGrid)emptyView.Content).SelectedIndex=0;Pump();
   Require(((DataGrid)emptyView.Content).Items.Count==1&&emptySelection is {Targets.Count:0}&&ra.Acquired==beforeEmpty,"合法空几何未保行/错误读取物理来源");
   var before=ra.Acquired+rb.Acquired;
   var plainProjection=new ResultProjectionDescriptor(ResultProjectionKind.StandardTable,ResultCardinality.Many,new[]{new ResultColumnDescriptor("plain","点",P("id"),Geometry:P("point"))},new("rows"),"id");
   using var plain=new DeclaredResultTable(schema,plainProjection);plain.Height=150;Show(plain);plain.Present(pub);owner.UpdateLayout();Pump();
   ResultSelection? noProof=null;plain.SelectionHighlightChanged+=(_,value)=>noProof=value;((DataGrid)plain.Content).SelectedIndex=0;Pump();
   Require(noProof?.ElementIdentity is 17L&&noProof.Targets.Count==0&&ra.Acquired+rb.Acquired==before,"无source纯geometry猜测了来源");
  });
 }
 static void VerifyNativeResultTable()
 {
  Test("原结果表十列500增量、有效性、旧命令和失败准备",()=>
  {
   var metrics=new[]{"area","perimeter","x","y","angle","long-axis","short-axis","circularity","rectangularity"};
   var members=new List<Port>{new("row-id",DataType.Integer),new("number",DataType.Integer),new("valid",DataType.Boolean)};
   members.AddRange(metrics.Select(id=>new Port(id,DataType.Number)));
   var rowType=TypeRef.RecordOf("design.native-result-row",members);
   var rowsType=TypeRef.SequenceOf(rowType,CountConstraint.Any);
   var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("rows",rowsType)});
   SunEyeVision.Plugin.Contracts.Results.Presentation.ResultFieldPath Path(string id)=>new("rows",new[]{id});
   var columns=new List<SunEyeVision.Plugin.Contracts.Results.Presentation.ResultColumnDescriptor>{new("number","序号",Path("number"))};
   columns.AddRange(metrics.Select(id=>new SunEyeVision.Plugin.Contracts.Results.Presentation.ResultColumnDescriptor(id,id,Path(id),DecimalPlaces:2,ValidWhen:Path("valid"))));
   var projection=new SunEyeVision.Plugin.Contracts.Results.Presentation.ResultProjectionDescriptor(
    SunEyeVision.Plugin.Contracts.Results.Presentation.ResultProjectionKind.StandardTable,
    SunEyeVision.Plugin.Contracts.Results.Presentation.ResultCardinality.Many,columns,new("rows"),"row-id");
   InputValues Values(bool duplicate=false)=>new(schema.Outputs,new Dictionary<string,object?>{{"rows",new SequenceValue(rowsType,Enumerable.Range(0,1205).Select(index=>
   {
    var data=new Dictionary<string,object?>{{"row-id",duplicate?1L:10000L+index},{"number",(long)index},{"valid",index!=17}};
    foreach(var metric in metrics)data[metric]=index+1d;
    return new RecordValue(rowType,data);
   }))}});
   using var view=new SunEyeVision.Plugin.Wpf.Presenters.Results.DeclaredResultTable(schema,projection);
   var run=new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"table-fixture",Guid.NewGuid());
   ResultPublication Publication(InputValues values)=>new(schema,new(Guid.NewGuid(),run,1,schema.Fingerprint),ResultViewState.Success,values,null,()=>true);
   using var firstPublication=Publication(Values());
   view.Height=320;view.Width=700;view.Present(firstPublication);host.Children.Add(view);
   try
   {
   owner.UpdateLayout();Pump();
   var grid=Visuals<DataGrid>(view).Single();
   Require(grid.Columns.Count==10&&grid.Items.Count==500&&grid.MinRowHeight==38,"未使用完整原十列/38行高或初批500");
   Require(!grid.CanUserSortColumns&&!grid.CanUserReorderColumns&&grid.ClipboardCopyMode==DataGridClipboardCopyMode.None,"原列表只读交互丢失");
   var invalid=grid.Items[17];var indexer=invalid.GetType().GetProperty("Item")!;
   Require(metrics.All(key=>indexer.GetValue(invalid,new object[]{key}) is null),"无效指标仍显示数值");
   var stale=view.CurResultScrollCommand;
   using var secondPublication=Publication(Values());view.Present(secondPublication);stale.Execute(401d);Require(grid.Items.Count==500,"旧滚动命令分页新publication");
   var current=view.CurResultScrollCommand;
   grid.ApplyTemplate();grid.UpdateLayout();Pump();
   var scroll=Visuals<ScrollViewer>(grid).Single(item=>item.Name=="DG_ScrollViewer");
   scroll.ScrollToVerticalOffset(401);grid.UpdateLayout();Pump();
   Require(scroll.VerticalOffset>=401&&grid.Items.Count==1000,$"原滚动未追加第二批：offset={scroll.VerticalOffset},rows={grid.Items.Count},logical={scroll.CanContentScroll}");
   scroll.ScrollToVerticalOffset(901);grid.UpdateLayout();Pump();
   Require(scroll.VerticalOffset>=901&&grid.Items.Count==1205,"原滚动第三批未完整到1205");
   var last=grid.Items[1204];Require((long)last.GetType().GetProperty("Item")!.GetValue(last,new object[]{"number"})! ==1204L,"尾行内容不对应");
   using var badPublication=Publication(Values(true));
   var rejected=false;try{view.Present(badPublication);}catch(ArgumentException){rejected=true;}
   Require(rejected&&grid.Items.Count==1205,"非法候选半清当前结果");
   System.Collections.Specialized.NotifyCollectionChangedEventHandler bad=(_,_)=>throw new InvalidOperationException("观察异常");
   var source=(System.Collections.Specialized.INotifyCollectionChanged)grid.ItemsSource;source.CollectionChanged+=bad;
   try{view.Clear();}catch(AggregateException){}finally{source.CollectionChanged-=bad;}
   Require(grid.Items.Count==0,"观察者异常留下旧批");
   current.Execute(901d);Require(grid.Items.Count==0,"清空后旧命令复活行");
   }
   finally{host.Children.Remove(view);}
  });
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
   using var fields=Create(_=>writes++,new ReferenceFieldOptions(_=>{if(refreshFails)throw new InvalidOperationException("来源刷新失败");return new(new[]{candidate},Array.Empty<DeclaredReferenceCandidate>());},EditableIndex:true));
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
  var alternate=new DeclaredReferenceCandidate("另一数值来源","source",null,new Port("values2",TypeRef.SequenceOf(DataType.Integer)),Array.Empty<string>());
  FieldPresentation presentation=new("expanded","数量","参数",0,FieldEditorKind.Scalar){Layout=new("参数","参数",Minimum:0,Maximum:1000)};
  DeclaredFieldEditor Create(Action<ParameterSetting> write,Action<ReferenceExpansionRequest>? expand=null)=>new(
   new("expanded",DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"数量",
   new ReferenceSetting(sequence.Reference(BindingSelection.All)),write,new ReferenceFieldOptions(_=>new(new[]{sequence,alternate},new[]{index}),EditableIndex:true,Expand:expand),
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
 static SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiSession CreateRoiSession(IReadOnlyImage image,Func<IReadOnlyList<RoiEditItem>> read,Func<ImageFrameAddress,RoiEditProposal.Replace,bool> accept,Func<bool> current)
 {
  var scope=Guid.NewGuid();long revision=0;
  SunEyeVision.Plugin.Wpf.Adapters.Regions.RoiEditSnapshot Snapshot()=>new(new(scope,revision,image.Frame),read());
  return new(image,Snapshot,(expected,change)=>
  {
   if(expected.ScopeId!=scope||expected.Revision!=revision||expected.Frame!=image.Frame)return RoiEditAcceptance.Reject(Snapshot(),"过期候选");
   if(change is not RoiEditProposal.Replace replace)throw new InvalidOperationException("替换helper不得接纳其他提案");
   if(!accept(expected.Frame,replace))return RoiEditAcceptance.Reject(Snapshot(),"宿主拒绝");revision++;return new(true,Snapshot(),new[]{replace.Target},new RoiEditSelection.Keep());
  },current,()=>new(false,false),"detect");
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
    var index=rows.FindIndex(row=>row.Group==change.Target.Group&&row.Id==change.Target.Id);Require(index>=0,"目标ID丢失");
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
   Require(oldImage.Active==0&&newImage.Active==1&&viewport.NativeView.ROIList.Cast<object>().SequenceEqual(newSession.Items),"旧Acquire覆盖或泄漏新帧/稳定投影对象");
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
  internal readonly List<RoiEditItem> Rows=rows.ToList();internal long Revision;internal int Writes,Reads,Attempts;
  internal bool Alive=true,Reject;internal Action? OnRead,BeforeAccept;
  internal Guid ScopeId=Guid.NewGuid();internal Func<RoiEditAcceptance,RoiEditAcceptance>? AfterAccept;
  internal RoiEditStamp Stamp=>new(ScopeId,Revision,image.Frame);
  internal RoiEditSnapshot Read(){Reads++;OnRead?.Invoke();return new(Stamp,Rows.ToArray());}
  internal RoiEditAcceptance Accept(RoiEditStamp expected,RoiEditProposal change)
  {
   Attempts++;var before=BeforeAccept;BeforeAccept=null;before?.Invoke();
   if(expected!=Stamp||Reject)return RoiEditAcceptance.Reject(new(Stamp,Rows.ToArray()),"宿主拒绝或旧根");
   RoiEditTarget target;
   switch(change)
   {
    case RoiEditProposal.Create create:
     target=new(create.Group,Guid.NewGuid());Rows.Add(new(create.Group,target.Id,"新区域",create.Geometry));break;
    case RoiEditProposal.Replace replace:
     var index=Rows.FindIndex(row=>row.Group==replace.Target.Group&&row.Id==replace.Target.Id);Require(index>=0,"提交身份丢失");
     target=replace.Target;Rows[index]=Rows[index] with{Geometry=replace.Geometry};break;
    case RoiEditProposal.Delete delete:
     Rows.RemoveAll(row=>delete.Targets.Contains(new(row.Group,row.Id)));Revision++;Writes++;
     return new(true,new(Stamp,Rows.ToArray()),delete.Targets,new RoiEditSelection.Clear());
    default:return RoiEditAcceptance.Reject(new(Stamp,Rows.ToArray()),"该检查宿主不提供历史");
   }
   Revision++;Writes++;var result=new RoiEditAcceptance(true,new(Stamp,Rows.ToArray()),new[]{target},change is RoiEditProposal.Create?new RoiEditSelection.Select(target):new RoiEditSelection.Keep());return AfterAccept?.Invoke(result)??result;
  }
  internal NativeRoiSession Open()=>new(image,Read,Accept,()=>Alive,()=>new(false,false),"detect");
 }
 static TextBox RoiText(Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat field)
 {
  var box=(ComboBox)field.FindName("CBox");box.ApplyTemplate();
  return (TextBox)((Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl)box.Template.FindName("T1",box)).FindName("ValueText");
 }
 static void VerifyNativeRoiCreationContract()
 {
  RecordValue Shape()=>RoiGeometryEditing.Create("rectangle",new(10,20),new(110,100));
  Test("原绘制准备读取期间替换或移除接收器不会遗留旧请求",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]));using var session=model.Open();
   var oldCalls=0;var newCalls=0;Action<RoiDrawRequest> old=_=>oldCalls++;Action<RoiDrawRequest> next=_=>newCalls++;session.DrawRequested+=old;
   model.OnRead=()=>{model.OnRead=null;session.DrawRequested-=old;session.DrawRequested+=next;};
   Require(session.BeginDraw("rectangle") is not null&&oldCalls==0&&newCalls==1,"请求交给已退订视口");session.CancelDraw();
   model.OnRead=()=>{model.OnRead=null;session.DrawRequested-=next;};
   Require(session.BeginDraw("rectangle") is null&&session.ActiveDraw is null&&model.Attempts==0,"无接收器仍留下活动请求");
  });
  Test("原绘制请求显式类型与组，不把原按钮类型丢弃",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]));using var session=model.Open();
   var requests=new List<RoiDrawRequest>();session.DrawRequested+=requests.Add;session.SetDrawingGroup("mask");
   var kinds=new[]{Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.SectorRing,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Sector,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Polygon};
   foreach(var kind in kinds)session.ValueChangedCommand.Execute(kind);
   Require(requests.Select(request=>request.Kind).SequenceEqual(new[]{"rectangle","annulus","sector","polygon"})&&requests.All(request=>request.Group=="mask"&&request.Stamp==session.Stamp)&&requests.Select(request=>request.Id).Distinct().Count()==4&&model.Writes==0,"原类型/组/版本丢失或开始即写宿主");
   session.CancelDraw();Require(session.ActiveDraw is null,"取消未使请求失效");
  });
  Test("原绘制Create空选择由宿主分配ID，首个集合通知已选中新对象且只接纳一次",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]));using var session=model.Open();
   session.SetDrawingGroup("mask");session.DrawRequested+=_=>{};var request=session.BeginDraw("rectangle")!;var observed=false;
   ((System.Collections.Specialized.INotifyCollectionChanged)session.Items).CollectionChanged+=(_,_)=>{if(session.Items.Count>0){Require(session.CurROIData is {Group:"mask"} selected&&session.Items.Contains(selected),"首个集合通知看见未选中/不完整对象");observed=true;}};
   Require(session.CompleteDraw(request,Shape())&&observed&&model.Writes==1&&model.Attempts==1&&session.Items.Count==1&&session.CurROIData!.Id==model.Rows[0].Id,"创建未完整安装");
   Require(!session.CompleteDraw(request,Shape())&&model.Attempts==1,"重复完成二次写入");
  });
  Test("原绘制过期版本、取消和其他会话请求不进入宿主",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,240,new byte[76800]);var model=new RoiHost(image);using var a=model.Open();using var b=model.Open();a.DrawRequested+=_=>{};b.DrawRequested+=_=>{};
   var old=a.BeginDraw("rectangle")!;Require(!b.CompleteDraw(old,Shape()),"跨Session请求可写");model.Revision++;
   Require(!a.CompleteDraw(old,Shape())&&model.Attempts==0&&a.Stamp.Revision==1,"过期请求进入宿主或未恢复当前快照");
   var next=a.BeginDraw("rectangle")!;a.CancelPending();Require(!a.CompleteDraw(next,Shape())&&model.Attempts==0,"取消请求可写");
  });
  Test("原绘制读取回调取消与切组使旧候选失效",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]));using var session=model.Open();session.DrawRequested+=_=>{};
   var request=session.BeginDraw("rectangle")!;model.OnRead=()=>session.CancelDraw();Require(!session.CompleteDraw(request,Shape())&&model.Attempts==0,"读取中取消仍提交");
   model.OnRead=()=>session.SetDrawingGroup("mask");Require(session.BeginDraw("rectangle") is null&&session.ActiveDraw is null,"读取中换组仍发旧请求");model.OnRead=null;
   var fresh=session.BeginDraw("rectangle")!;Require(fresh.Group=="mask","新请求未使用显式当前组");session.CancelDraw();
  });
  Test("原绘制非法及部分圆环候选在宿主写入前拒绝",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]));using var session=model.Open();session.DrawRequested+=_=>{};
   var request=session.BeginDraw("annulus")!;var values=RoiGeometryEditing.Values(RingShape());values["end-radians"]=Math.PI;
   var rejected=false;try{session.CompleteDraw(request,RoiGeometryEditing.Checked(values));}catch(NotSupportedException){rejected=true;}
   Require(rejected&&model.Attempts==0&&model.Rows.Count==0&&session.ActiveDraw is null,"部分圆环先写后拒或请求未消费");
   request=session.BeginDraw("rectangle")!;rejected=false;try{session.CompleteDraw(request,RingShape());}catch(ArgumentException){rejected=true;}
   Require(rejected&&model.Attempts==0,"类型不符进入宿主");
  });
  Test("原绘制宿主拒绝不生成幽灵对象且请求不能重放",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800])){Reject=true};using var session=model.Open();session.DrawRequested+=_=>{};
   var request=session.BeginDraw("rectangle")!;Require(!session.CompleteDraw(request,Shape())&&session.Items.Count==0&&session.CurROIData is null&&model.Writes==0,"拒绝后留下对象");
   Require(!session.CompleteDraw(request,Shape())&&model.Attempts==1,"拒绝请求可重放");
  });
  Test("原绘制Create回执复用旧ID或错组明确报错且不安装",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]),new RoiEditItem("detect",Guid.NewGuid(),"旧",Shape()));using var session=model.Open();session.DrawRequested+=_=>{};
   model.AfterAccept=result=>result with{Affected=new[]{new RoiEditTarget("detect",model.Rows[0].Id)}};var request=session.BeginDraw("rectangle")!;
   var rejected=false;try{session.CompleteDraw(request,Shape());}catch(InvalidOperationException){rejected=true;}
   Require(rejected&&session.Items.Count==1&&model.Writes==1&&!session.CompleteDraw(request,Shape()),"旧身份回执被安装或二次写入");
  });
  Test("原绘制接纳通知抛错仍保留完整快照和选中，不能重放",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]));using var session=model.Open();session.DrawRequested+=_=>{};
   var request=session.BeginDraw("rectangle")!;var rows=(System.Collections.Specialized.INotifyCollectionChanged)session.Items;
   System.Collections.Specialized.NotifyCollectionChangedEventHandler observer=(_,_)=>throw new InvalidOperationException("预期集合通知失败");rows.CollectionChanged+=observer;
   var threw=false;try{session.CompleteDraw(request,Shape());}catch(InvalidOperationException){threw=true;}finally{rows.CollectionChanged-=observer;}
   Require(threw&&session.Items.Count==1&&session.CurROIData?.Id==model.Rows[0].Id&&model.Writes==1&&!session.CompleteDraw(request,Shape()),"观察者异常伪装未接纳或丢选中/可重放");
  });
  Test("原绘制刷新取消异常在新快照完整安装后汇总",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]));using var session=model.Open();session.DrawRequested+=_=>{};var request=session.BeginDraw("rectangle")!;
   var observed=0;session.DrawCancelled+=_=>{Require(session.Stamp.Revision==1&&session.Items.Count==1,"取消监听看见旧根");throw new InvalidOperationException("预期取消失败");};session.DrawCancelled+=_=>observed++;
   model.Rows.Add(new("detect",Guid.NewGuid(),"外部",Shape()));model.Revision++;var threw=false;try{session.Refresh();}catch(InvalidOperationException){threw=true;}
   Require(threw&&observed==1&&session.Stamp.Revision==1&&session.Items.Count==1&&!session.CompleteDraw(request,Shape()),"取消异常截断安装/后续监听或旧请求可写");
  });
  Test("原绘制接收器立即取消或抛错后不返回失效请求且可重试",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]));using var session=model.Open();
   Action<RoiDrawRequest> cancel=_=>session.CancelDraw();session.DrawRequested+=cancel;Require(session.BeginDraw("rectangle") is null,"返回已取消请求");session.DrawRequested-=cancel;
   Action<RoiDrawRequest> broken=_=>throw new InvalidOperationException("预期开始失败");Action<RoiDrawRequest> cleanup=_=>throw new ArgumentException("预期清理失败");session.DrawRequested+=broken;session.DrawCancelled+=cleanup;
   var threw=false;try{session.BeginDraw("rectangle");}catch(AggregateException error){threw=error.Flatten().InnerExceptions.Count==2;}finally{session.DrawRequested-=broken;session.DrawCancelled-=cleanup;}
   Require(threw&&session.ActiveDraw is null,"开始异常未汇总或请求未清");session.DrawRequested+=_=>{};Require(session.BeginDraw("rectangle") is not null,"开始失败后请求门禁未退");session.CancelDraw();
  });
 }
 static void VerifyNativeRoiDrawingViewport()
 {
  const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
  object Drawing(NativeRoiViewport view)=>typeof(NativeRoiViewport).GetField("_drawing",hidden)!.GetValue(view)!;
  void Step(object drawing,string method,params object[] args)=>drawing.GetType().GetMethod(method,hidden)!.Invoke(drawing,args);
  void Prime(Window window,FrameworkElement element,Point point)
  {
   if(!window.IsVisible||!element.IsLoaded)return;
   var position=element.TranslatePoint(point,window);position=PresentationSource.FromVisual(element)!.CompositionTarget.TransformToDevice.Transform(position);
   SendMessage(new System.Windows.Interop.WindowInteropHelper(window).Handle,0x0200,IntPtr.Zero,new IntPtr(((int)position.Y<<16)|((int)position.X&0xffff)));
  }
  Native.Display.VMBaseControls.Base.ImageView.ROIItemsPage Begin(NativeRoiScene scene,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType type,Point start)
  {
   var window=Window.GetWindow(scene)!;window.Width=1040;scene.Height=500;window.UpdateLayout();var activated=window.Activate();Pump();
   var button=Visuals<ToggleButton>(scene.Editor).Single(item=>Equals(item.CommandParameter,type));
   Require(button.Command?.CanExecute(button.CommandParameter)==true,"原选择按钮不可执行");button.Command!.Execute(button.CommandParameter);
   var view=scene.Viewport.NativeView;view.ApplyTemplate();
   var page=(Native.Display.VMBaseControls.Base.ImageView.ROIItemsPage)view.Template.FindName("mROIView",view);
   // Prime only this owned window's input provider; geometry methods below remain semantic automation.
   var before=$"request={scene.Session?.ActiveDraw?.Kind},page={page.ImageWidth}x{page.ImageHeight},loaded={page.IsLoaded},visible={page.IsVisible},enabled={page.IsEnabled},hit={page.IsHitTestVisible},host={page.SubstituteHost},ratio={page.ControlToImageRadio},size={page.ActualWidth}x{page.ActualHeight},active={window.IsActive},viewport={scene.Viewport.ActualWidth}x{scene.Viewport.ActualHeight}";
   string InputState()
   {
    var cursorOk=GetCursorPos(out var cursor);var cursorError=System.Runtime.InteropServices.Marshal.GetLastWin32Error();
    var foreground=GetForegroundWindow();var thread=GetWindowThreadProcessId(foreground,out var process);var source=PresentationSource.FromVisual(page);var active=Mouse.PrimaryDevice.ActiveSource;
    return $"hwnd={new System.Windows.Interop.WindowInteropHelper(window).Handle},fg={foreground},fgThread={thread},fgPid={process},nativeCapture={GetCapture()},wpfCapture={Mouse.Captured},active={window.IsActive},enabled={window.IsEnabled},sourceDisposed={source?.IsDisposed},pageEnabled={page.IsEnabled},pageHit={page.IsHitTestVisible},cursorOk={cursorOk},cursorError={(cursorOk?0:cursorError)},cursor={cursor.X}/{cursor.Y},mouseActiveSource={active},mouseSourceMatches={ReferenceEquals(active,source)},wpfAssembly={typeof(Mouse).Assembly.Location}";
   }
   var beforePrime=InputState();
   Prime(window,page,new(start.X*page.ControlToImageRadio,start.Y*page.ControlToImageRadio));
   var afterPrime=InputState();
   var startHook=(Delegate)view.GetType().GetProperty("NativeDrawStart",hidden)!.GetValue(view)!;
   var started=(bool)startHook.DynamicInvoke(page,new Point(start.X*page.ControlToImageRadio,start.Y*page.ControlToImageRadio))!;
   if(!started)Console.WriteLine($"CAPTURE activated={activated};beforePrime={beforePrime};afterPrime={afterPrime};afterStart={InputState()};windows="+string.Join(" | ",Application.Current.Windows.Cast<Window>().Select(item=>$"{item.Title}:visible={item.IsVisible},active={item.IsActive},enabled={item.IsEnabled},owner={item.Owner?.Title}")));
   Require(started,"原视口未开始绘制："+before+",capture="+Mouse.Captured+",error="+scene.Session?.Error);
   Require(page.SubstituteHost.Children.OfType<Native.Display.VMBaseControls.Base.ImageView.ROI.ROISubstitute>().Count()==1&&ReferenceEquals(Mouse.Captured,page),"未使用原替身或未持有捕获");return page;
  }
  void Warning(NativeRoiScene scene,object drawing,Action<Native.Display.VMControls.HikMessageBox> inspect)
  {
   var window=Window.GetWindow(scene)!;var page=(Native.Display.VMBaseControls.Base.ImageView.ROIItemsPage)drawing.GetType().GetProperty("Page",hidden)!.GetValue(drawing)!;
   var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};var deadline=DateTime.UtcNow.AddSeconds(8);Exception? error=null;var observed=false;
   EventHandler tick=(_,_)=>
   {
    var dialog=window.OwnedWindows.OfType<Native.Display.VMControls.HikMessageBox>().SingleOrDefault(item=>item.IsVisible&&item.IsLoaded);
    if(dialog is null){if(DateTime.UtcNow>deadline){error=new Exception("原警告窗未在限时内加载");timer.Stop();((IDisposable)drawing).Dispose();}return;}
    timer.Stop();observed=true;
    try{inspect(dialog);}catch(Exception exception){error=exception;}
    finally
    {
     try{if(dialog.IsVisible)((Button)dialog.FindName("OkBtn")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Prime(window,page,new(20*page.ControlToImageRadio,20*page.ControlToImageRadio));}
     catch(Exception exception){error=error is null?exception:new AggregateException(error,exception);}
    }
   };
   timer.Tick+=tick;timer.Start();try{Step(drawing,"Click",1);}finally{timer.Stop();timer.Tick-=tick;}
   if(error is not null)throw error;Require(observed,"超限未经过原模态警告窗");
  }
  void AtLimit(object drawing,double ratio)
  {
   Step(drawing,"Move",new Point(100*ratio,20*ratio),true);Step(drawing,"Click",1);
   Step(drawing,"Move",new Point(80*ratio,100*ratio),true);Step(drawing,"Click",1);Step(drawing,"Move",new Point(20*ratio,90*ratio),true);
  }
  Test("原绘制上限原警告窗真实Owner模板与暂停恢复",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(640,320,new byte[204800]));using var session=model.Open();using var scene=new NativeRoiScene{Session=session};Show(scene);
   scene.Viewport.NativeView.SetValue(Native.Display.VMBaseControls.Base.ImageView.ROI.PolygonROIAttach.CustomMaxPointNumProperty,3);
   var page=Begin(scene,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Polygon,new(20,20));var drawing=Drawing(scene.Viewport);AtLimit(drawing,page.ControlToImageRadio);
   Warning(scene,drawing,dialog=>
   {
    Require(ReferenceEquals(dialog.Owner,owner)&&dialog.Template is not null&&Native.Display.VMControls.HikMessageBox.Alive&&dialog.Message.EndsWith(" 3",StringComparison.Ordinal),$"未使用原Owner/模板/消息/模态计数：owner={ReferenceEquals(dialog.Owner,owner)},template={dialog.Template},alive={Native.Display.VMControls.HikMessageBox.Alive},message={dialog.Message}");
    Require(((Image)dialog.FindName("Icon")).Source is BitmapSource bitmap&&bitmap.PixelWidth>0&&((Button)dialog.FindName("GetInfo")).IsVisible,"原Warning图标或原按钮缺失");
    var ok=(Button)dialog.FindName("OkBtn");var caption=Visuals<TextBlock>(ok).Single(text=>text.Text==ok.Content?.ToString());
    Require(dialog.IsEnabled&&ok.IsVisible&&ok.IsEnabled&&caption.Foreground is SolidColorBrush foreground&&ok.Foreground is SolidColorBrush expected&&foreground.Color==expected.Color,"原确定按钮不可用或文字被宿主隐式样式污染");
    var diagnostic=(string)dialog.GetType().GetMethod("WriteDiagnostic",hidden)!.Invoke(dialog,null)!;
    Require(File.Exists(diagnostic)&&File.ReadAllText(diagnostic).Contains("WriteDiagnostic",StringComparison.Ordinal),"独立原窗诊断目录/文件未创建");
    Require(!ReferenceEquals(Mouse.Captured,page)&&!(bool)scene.Viewport.NativeView.GetType().GetProperty("IsDrawingOfROI",hidden)!.GetValue(scene.Viewport.NativeView)!,"模态期间未暂停原绘制/捕获");
    var polygon=(Native.Display.VMBaseControls.Base.ImageView.ROI.PolygonROISubstitute)drawing.GetType().GetProperty("Substitute",hidden)!.GetValue(drawing)!;var before=polygon.Points.ToArray();
    Step(drawing,"Move",new Point(300,200),true);Step(drawing,"Back");Step(drawing,"Click",2);Require(polygon.Points.SequenceEqual(before)&&model.Attempts==0,"模态期间仍能修改/提交图形");
    var snapshot=new RenderTargetBitmap((int)Math.Ceiling(dialog.ActualWidth),(int)Math.Ceiling(dialog.ActualHeight),96,96,PixelFormats.Pbgra32);snapshot.Render(dialog);
    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(snapshot));using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"native-polygon-limit-warning.png"));encoder.Save(file);
   });
   Require(session.ActiveDraw is not null&&ReferenceEquals(Mouse.Captured,page)&&!Native.Display.VMControls.HikMessageBox.Alive,"模态结束未恢复同请求捕获或漏计数");
   Step(drawing,"Move",new Point(80*page.ControlToImageRadio,100*page.ControlToImageRadio),true);Step(drawing,"Click",1);Step(drawing,"Click",2);
   Require(model.Writes==1&&RoiGeometryEditing.Vertices(model.Rows[0].Geometry).Length==3&&session.ActiveDraw is null,"超限回退后未能完成原三顶点候选");
  });
  foreach(var action in new[]{"取消","版本变化","重绑","Owner关闭"})
  Test("原绘制上限模态期间"+action+"不恢复旧捕获与回写",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[204800]);var model=new RoiHost(image);var next=new RoiHost(image);using var session=model.Open();using var nextSession=next.Open();using var scene=new NativeRoiScene{Session=session};
   Window? separate=null;
   try
   {
    if(action=="Owner关闭"){separate=new Window{Width=1040,Height=600,Content=scene};separate.Show();Pump();}else Show(scene);
    scene.Viewport.NativeView.SetValue(Native.Display.VMBaseControls.Base.ImageView.ROI.PolygonROIAttach.CustomMaxPointNumProperty,3);
    var page=Begin(scene,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Polygon,new(20,20));var drawing=Drawing(scene.Viewport);var canvas=page.SubstituteHost;AtLimit(drawing,page.ControlToImageRadio);
    Warning(scene,drawing,_=>{if(action=="取消")session.CancelDraw();else if(action=="版本变化"){model.Revision++;session.Refresh();}else if(action=="重绑"){scene.Session=nextSession;Require(nextSession.BeginDraw("rectangle") is not null,"新会话未准备绘制");}else{Console.WriteLine("TRACE 自有Owner关闭前");separate!.Close();Console.WriteLine("TRACE 自有Owner关闭返回");}});
    Require(session.ActiveDraw is null&&model.Attempts==0&&!ReferenceEquals(Mouse.Captured,page)&&!canvas.Children.OfType<Native.Display.VMBaseControls.Base.ImageView.ROI.ROISubstitute>().Any()&&!Native.Display.VMControls.HikMessageBox.Alive,"旧模态复活/写入/漏替身或计数");
    if(action=="重绑"){Require(nextSession.ActiveDraw is not null,"旧模态取消新会话请求");nextSession.CancelDraw();}
   }
   finally{separate?.Close();}
  });
  Test("原绘制上限警告创建未显示就取消或抛错不污染Alive计数",()=>
  {
   var show=typeof(Native.Display.VMControls.HikMessageBox).GetMethod("ShowOwnedWarning",BindingFlags.Static|BindingFlags.NonPublic)!;
   show.Invoke(null,new object[]{owner,"测试",new Action<Native.Display.VMControls.HikMessageBox>(window=>window.Close()),new Func<bool>(()=>false)});
   Require(!Native.Display.VMControls.HikMessageBox.Alive,"未显示关闭减坏计数");
   var threw=false;try{show.Invoke(null,new object[]{owner,"测试",new Action<Native.Display.VMControls.HikMessageBox>(_=>throw new InvalidOperationException("预期创建取消")),new Func<bool>(()=>true)});}catch(TargetInvocationException error)when(error.InnerException is InvalidOperationException){threw=true;}
   Require(threw&&!Native.Display.VMControls.HikMessageBox.Alive,"创建异常漏窗或污染模态计数");
  });
  Test("原绘制上限Closing拒绝及异常与Closed失败都保留清理",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(640,320,new byte[204800]));using var session=model.Open();using var scene=new NativeRoiScene{Session=session};Show(scene);
   scene.Viewport.NativeView.SetValue(Native.Display.VMBaseControls.Base.ImageView.ROI.PolygonROIAttach.CustomMaxPointNumProperty,3);
   var page=Begin(scene,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Polygon,new(20,20));var drawing=Drawing(scene.Viewport);AtLimit(drawing,page.ControlToImageRadio);
   Native.Display.VMControls.HikMessageBox? closed=null;var closedNotifications=0;
   Warning(scene,drawing,dialog=>
   {
    System.ComponentModel.CancelEventHandler? reject=null;reject=(_,args)=>{dialog.Closing-=reject;args.Cancel=true;};dialog.Closing+=reject;((Window)dialog).Close();
    Require(dialog.IsVisible&&Native.Display.VMControls.HikMessageBox.Alive,"一次Closing拒绝伪装已经关闭");
    System.ComponentModel.CancelEventHandler? broken=null;broken=(_,_)=>{dialog.Closing-=broken;throw new InvalidOperationException("预期Closing通知失败");};dialog.Closing+=broken;
    ((Window)dialog).Close();
    Require(dialog.LastCloseError is InvalidOperationException&&dialog.IsVisible&&Native.Display.VMControls.HikMessageBox.Alive,"Closing异常未记录、丢失窗口或提前退休计数");
    closed=dialog;dialog.Closed+=(_,_)=>{closedNotifications++;throw new InvalidOperationException("预期Closed通知失败");};
   });
   Require(session.ActiveDraw is not null&&!Native.Display.VMControls.HikMessageBox.Alive&&closed?.LastCloseError is InvalidOperationException&&!closed.IsVisible&&closedNotifications==1,"关闭通知失败后漏窗/计数或丢绘制");closed.Close();Require(closedNotifications==1,"重复清理重新触发已关闭通知");session.CancelDraw();
  });
  foreach(var type in new[]{Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.SectorRing,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Sector,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Polygon})
  Test("原绘制视口原按钮到替身及完成入口一次宿主创建："+type,()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(640,320,new byte[204800]));
   Window? drawingOwner=null;var drawingHwnd=IntPtr.Zero;Exception? failure=null;
   try
   {
   Require(GetCapture()==IntPtr.Zero&&Mouse.Captured is null,"本例开始前已有捕获，拒绝借用或释放他人捕获。");
   using var session=model.Open();using var scene=new NativeRoiScene{Session=session};
   drawingOwner=new Window{Width=1040,Height=600,Title="原绘制独占验证-"+type,Content=scene};
   drawingOwner.Resources.MergedDictionaries.Add(EditorResources.Create());drawingOwner.Show();
   drawingHwnd=new System.Windows.Interop.WindowInteropHelper(drawingOwner).Handle;Pump();
   var start=type==Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box?new Point(200,180):new Point(20,20);
   var page=Begin(scene,type,start);var drawing=Drawing(scene.Viewport);var ratio=page.ControlToImageRadio;
   void Move(double x,double y)=>Step(drawing,"Move",new Point(x*ratio,y*ratio),true);
   Require(model.Attempts==0&&session.Items.Count==0,"开始绘制提前写入模型");
   if(type==Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Polygon)
   {Move(100,20);Step(drawing,"Click",1);Move(80,100);Step(drawing,"Click",1);Move(20,90);Step(drawing,"Click",1);Step(drawing,"Back");Move(10,70);Step(drawing,"Click",1);Step(drawing,"Click",2);}
   else{Move(type==Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box?40:70,type==Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box?30:20);Step(drawing,"Release");}
   Pump();Require(model.Writes==1&&model.Attempts==1&&session.Items.Count==1&&session.CurROIData?.Id==model.Rows[0].Id,"原完成入口未创建并选中唯一宿主对象");
   Require(!page.SubstituteHost.Children.OfType<Native.Display.VMBaseControls.Base.ImageView.ROI.ROISubstitute>().Any()&&!ReferenceEquals(Mouse.Captured,page)&&session.ActiveDraw is null,"完成仍持有替身/捕获/请求");
   var shape=model.Rows[0].Geometry;
   if(type==Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box)
   {Require(RoiGeometryEditing.Center(shape)==new Point2(120,105)&&Math.Abs(shape.Get<double>("width")-160)<1e-8&&Math.Abs(shape.Get<double>("height")-150)<1e-8,"反向矩形像素坐标错误");}
   else if(type==Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Polygon)
   {Require(RoiGeometryEditing.Vertices(shape).SequenceEqual(new[]{new Point2(20,20),new Point2(100,20),new Point2(80,100),new Point2(10,70)}),"多边形预览尾/右键回退/顺序错误");}
   else Require(shape.Get<double>("inner-radius")==0&&Math.Abs(shape.Get<double>("outer-radius")-50)<1e-8&&shape.Get<double>("start-radians")==0&&Math.Abs(shape.Get<double>("end-radians")-(type==Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Sector?Math.PI/2:2*Math.PI))<1e-8,"原圆环或固定90度扇形语义丢失");
   Step(drawing,"Release");Require(model.Attempts==1,"旧替身完成可重放");
   }
   catch(Exception error){failure=error;}
   finally
   {
    try{drawingOwner?.Close();}catch(Exception cleanup){failure=failure is null?cleanup:new AggregateException(failure,cleanup);}
   }
   if(drawingHwnd!=IntPtr.Zero&&GetCapture()==drawingHwnd)
   {var residue=new InvalidOperationException("本例自有窗口关闭后仍残留原生捕获。");failure=failure is null?residue:new AggregateException(failure,residue);}
   if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
  });
  Test("原绘制视口取消重入Bind新会话后旧清理不释放新帧",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();
   var ia=new TrackedRoiImage(resources.CreateGray8(640,320,new byte[204800]));var ib=new TrackedRoiImage(resources.CreateGray8(640,320,new byte[204800]));var ic=new TrackedRoiImage(resources.CreateGray8(640,320,new byte[204800]));
   var a=new RoiHost(ia);var b=new RoiHost(ib);var c=new RoiHost(ic);using var sa=a.Open();using var sb=b.Open();using var sc=c.Open();
   using var scene=new NativeRoiScene{Session=sa};Show(scene);Pump();Begin(scene,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box,new(30,40));
   sa.DrawCancelled+=_=>scene.Viewport.Bind(sb);scene.Viewport.Bind(sc);Pump();
   Require(scene.Viewport.NativeView.ROIList.Cast<object>().SequenceEqual(sb.Items)&&scene.Viewport.NativeView.ImageDataSource is Native.Dependencies.VMControls.RenderInterface.IImageData current&&current.Buffer.Length==204800&&ia.Active==0&&ib.Active==1&&ic.Active==0,"外层旧Bind清掉或释放重入的新图像/稳定投影对象");
   Require(sb.BeginDraw("rectangle") is not null&&sc.BeginDraw("rectangle") is null,"旧Bind复活或新会话未接收请求");sb.CancelDraw();
  });
  Test("原绘制视口同会话取消重绑后新请求不被外层CancelPending清除",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(640,320,new byte[204800]));using var session=model.Open();
   using var scene=new NativeRoiScene{Session=session};Show(scene);Pump();Begin(scene,Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box,new(30,40));
   Action<RoiDrawRequest>? rebind=null;rebind=_=>{session.DrawCancelled-=rebind;scene.Viewport.Bind(session);Require(session.BeginDraw("rectangle") is not null,"同会话重绑后请求失败");};
   session.DrawCancelled+=rebind;scene.Viewport.Bind(null);Pump();
   Require(session.ActiveDraw is not null&&scene.Viewport.NativeView.ROIList.Cast<object>().SequenceEqual(session.Items),"旧外层取消破坏新请求或投影对象");session.CancelDraw();
  });
 }
 static void VerifyNativeRoiScene()
 {
  Test("独立ROI场景借用会话、卸载重载及最终释放不销毁宿主",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=new TrackedRoiImage(resources.CreateGray8(640,320,new byte[204800]));
   var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"框",RoiGeometryEditing.Create("rectangle",new(100,70),new(380,240))));
   using var session=model.Open();session.Select("detect",model.Rows[0].Id);
   using var scene=new NativeRoiScene{Session=session};Show(scene);Pump();
   Require(image.Active==1&&scene.Viewport.NativeView.ImageDataSource is not null,"场景未借用唯一真实图像帧");
   Show(new TextBlock());Pump();Require(image.Active==0&&session.IsCurrent,"卸载泄漏帧或销毁外部会话");
   Show(scene);Pump();Require(image.Active==1&&ReferenceEquals(scene.Session,session),"重载未恢复同一会话/唯一帧");
   CaptureInline(scene,"native-roi-scene-runtime.png");scene.Dispose();Require(image.Active==0&&session.IsCurrent,"场景释放越权销毁宿主或漏帧");
   using var editor=new NativeRoiEditor();editor.Bind(session);Require(editor.NativeView.IsEnabled,"场景未退编辑器注册");Show(new TextBlock());
  });
  Test("独立ROI场景图像取得期间重绑不回装旧代",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();
   var a=new TrackedRoiImage(resources.CreateGray8(640,320,new byte[204800]));var b=new TrackedRoiImage(resources.CreateGray8(640,320,new byte[204800]));
   RoiHost Model(IReadOnlyImage image)=>new(image,new RoiEditItem("detect",Guid.NewGuid(),"框",RoiGeometryEditing.Create("rectangle",new(20,20),new(120,100))));
   using var first=Model(a).Open();using var second=Model(b).Open();using var scene=new NativeRoiScene();Show(scene);
   a.OnAcquire=()=>scene.Session=second;scene.Session=first;Pump();
   Require(ReferenceEquals(scene.Session,second)&&a.Active==0&&b.Active==1,"旧图像回调覆盖新会话或漏帧");
   scene.Dispose();Require(a.Active==0&&b.Active==0&&first.IsCurrent&&second.IsCurrent,"重绑清理销毁外部会话");Show(new TextBlock());
  });
  Test("独立ROI场景取得图像失败后完全脱离并可再次绑定",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=new TrackedRoiImage(resources.CreateGray8(640,320,new byte[204800]));
   var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"框",RoiGeometryEditing.Create("rectangle",new(20,20),new(120,100))));
   using var session=model.Open();using var scene=new NativeRoiScene();Show(scene);image.OnAcquire=()=>throw new InvalidOperationException("预期图像失败");
   var rejected=false;try{scene.Session=session;}catch(InvalidOperationException){rejected=true;}
   Require(rejected&&scene.Session is null&&image.Active==0&&session.IsCurrent,"失败后未完全脱离或越权释放");
   scene.Session=session;Pump();Require(image.Active==1,"失败清理遗留重复编辑注册");scene.Dispose();Show(new TextBlock());
  });
 }
 static void VerifyNativeSceneCleanupFailure()
 {
  Test("独立ROI场景失败且清空通知抛错仍完整退绑",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=new TrackedRoiImage(resources.CreateGray8(640,320,new byte[204800]));
   var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"框",RoiGeometryEditing.Create("rectangle",new(20,20),new(120,100))));
   using var session=model.Open();using var scene=new NativeRoiScene();Show(scene);
   var descriptor=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(NativeRoiScene.SessionProperty,typeof(NativeRoiScene));
   EventHandler observer=(_,_)=>{if(scene.Session is null)throw new InvalidOperationException("预期清空通知失败");};
   descriptor.AddValueChanged(scene,observer);image.OnAcquire=()=>throw new InvalidOperationException("预期取得帧失败");
   Exception? failure=null;try{scene.Session=session;}catch(Exception error){failure=error;}finally{descriptor.RemoveValueChanged(scene,observer);}
   Require(failure is AggregateException aggregate&&aggregate.Flatten().InnerExceptions.Count>=2&&scene.Session is null&&image.Active==0,"清空通知异常覆盖原错误或中断退绑");
   using var editor=new NativeRoiEditor();editor.Bind(session);Require(session.IsCurrent,"失败清理销毁借用会话");Show(new TextBlock());
  });
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
 static RecordValue PolygonShape()=>RoiGeometryEditing.Create("polygon",new(0,0),new(0,0),new[]{new Point2(40.123456,30.987654),new Point2(40.123456,30.987654),new Point2(240.345678,40.123456),new Point2(220.765432,180.987654)});
 static TextBox PointText(Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint field)
 {
  var button=(ToggleButton)field.FindName("ToBtn");
  var leaf=(Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl)button.Template.FindName("T1",button);
  Require(BindingOperations.GetBinding(leaf,Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl.TextProperty)?.Source==field,"产品未初始化真实Point绑定");
  return (TextBox)leaf.FindName("ValueText");
 }
 static void VerifyNativePolygon()
 {
  Test("原Polygon有序重复点、真实参数栈与同帧图像双向一次接纳",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[640*320]);
   var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"多边形",PolygonShape()));
   using var session=model.Open();using var editor=new NativeRoiEditor();using var viewport=new NativeRoiViewport();
   editor.Bind(session);viewport.Bind(session);var scene=new Grid{Height=500};scene.ColumnDefinitions.Add(new(){Width=new GridLength(420)});scene.ColumnDefinitions.Add(new());
   scene.Children.Add(editor);scene.Children.Add(viewport);Grid.SetColumn(viewport,1);owner.Width=1000;Show(scene);session.Select("detect",model.Rows[0].Id);Pump();
   ((ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView)).IsChecked=true;Pump();
   var stack=Visuals<Native.Composition.Apps.XmlUI.ParamTabControl.PolygonROIStack>(editor).Single();
   var fields=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint>(stack).ToArray();Require(fields.Length==8,"未取得原四顶点八坐标："+fields.Length);
   var selected=(NativePolygonRoiProjection)session.CurROIData!;Require(ReferenceEquals(stack.ROIData,selected),"原参数栈非同一选中对象");
   var before=RoiGeometryEditing.Vertices(model.Rows[0].Geometry);var secondX=PointText(fields.Where(field=>field.IsX).ElementAt(1));
   secondX.Focus();Pump();secondX.Text="80.125";SendKey(secondX,Key.Enter,true);Pump();
   var after=RoiGeometryEditing.Vertices(model.Rows[0].Geometry);
   Require(!Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint>(editor).Any(field=>PointText(field).IsKeyboardFocused),"当前新Point槽仍持焦");
   Require(model.Writes==1&&!secondX.IsKeyboardFocused&&!editor.HasInvalidText,"Polygon Enter未单次失焦："+editor.Error+"；writes="+model.Writes);
   Require(after[0]==before[0]&&after[1].X==80.125&&after[1].Y==before[1].Y&&after[2]==before[2]&&after[3]==before[3],"重复点改错槽或未编辑顶点精度丢失");
   var roi=Visuals<Native.Display.VMBaseControls.Base.ImageView.ROI.NormalizedPolygonROI>(viewport).Single();
   var container=Visuals<Native.Display.VMBaseControls.Base.ImageView.ContainerItem>(viewport).Single(item=>ReferenceEquals(item.DataContext,selected));
   Require(container.ActualWidth>1&&container.ActualHeight>1&&roi.Points.Count==4,"原Polygon图形实际容器缺失");
   Require(Math.Abs(roi.PolygonPoints[1].X-80.125)<1e-8,"参数编辑未到原图像像素点");
   var polygon=(System.Windows.Shapes.Polygon)roi.Template.FindName("mPolygon",roi);
   Require(polygon.Points.SequenceEqual(roi.PolygonPoints)&&polygon.RenderedGeometry.Bounds.Width>0&&polygon.RenderedGeometry.Bounds.Height>0&&polygon.StrokeThickness>0,"实际原Polygon几何未绘制");
   var handles=Visuals<Native.Display.VMBaseControls.Base.ImageView.ROI.MoveArea>(roi).ToArray();Require(handles.Length==4,"原顶点手柄缺失");
   foreach(var handle in handles)
   {var ellipse=(System.Windows.Shapes.Ellipse)handle.Template.FindName("SizeRectX",handle);Require(ellipse.IsVisible&&!ellipse.RenderedGeometry.IsEmpty(),"顶点手柄未实际绘制");}

   roi.PolygonPoints[2]=new Point(260.25,60.5);
   typeof(Native.Display.VMBaseControls.Base.ImageView.ROI.ROIControlBase).GetMethod("NotifyROIParamChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(roi,null);Pump();
   Require(model.Writes==2&&RoiGeometryEditing.Vertices(model.Rows[0].Geometry)[2]==new Point2(260.25,60.5),"原图像点编辑未完整单次接纳");
   CaptureInline(scene,"native-polygon-scene.png");Show(new TextBlock{Text="多边形卸载"});owner.Width=530;
  });
  Test("Polygon旧集合与有序身份失效，实际CollectionView只收到单项完整快照通知",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[640*320]);
   var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"多边形",PolygonShape()));using var session=model.Open();session.Select("detect",model.Rows[0].Id);
   var item=(NativePolygonRoiProjection)session.CurROIData!;var points=item.PolygonPoints;_=CollectionViewSource.GetDefaultView(points);var notifications=0;
   points.CollectionChanged+=(_,args)=>{Require(args.NewItems?.Count==1&&args.OldItems?.Count==1,"范围Replace不受WPF支持");notifications++;};
   points[1]=new Point(.2,.3);Require(notifications==1&&item.HasPending,"有序点未暂存");
   Require(session.TryCommit(item)&&model.Writes==1,"完整顶点未接纳");
   var rejected=false;try{points[1]=new(.4,.5);}catch(InvalidOperationException){rejected=true;}Require(rejected,"接纳后旧集合仍可写入");
   var values=RoiGeometryEditing.Values(model.Rows[0].Geometry);var vertices=RoiGeometryEditing.Vertices(model.Rows[0].Geometry);Array.Reverse(vertices);
   values["vertices"]=new SequenceValue(RoiDeclarations.Shape.Fields.Single(field=>field.Id=="vertices").Type,vertices.Select(point=>(object?)RoiGeometryEditing.Point(point)));
   model.Rows[0]=model.Rows[0] with{Geometry=RoiGeometryEditing.Checked(values)};
   rejected=false;try{session.Refresh();}catch(InvalidOperationException){rejected=true;}Require(rejected,"同中心不同顺序被当作无变化");
  });
  Test("Polygon实际两槽非法与宿主拒绝不半写，纠正后完整接纳且旧控件拒写",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[640*320]);
   var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"多边形",PolygonShape()));using var session=model.Open();using var editor=new NativeRoiEditor();editor.Bind(session);Show(editor);session.Select("detect",model.Rows[0].Id);Pump();
   ((ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView)).IsChecked=true;Pump();
   var fields=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint>(editor).ToArray();var firstY=PointText(fields.Where(field=>!field.IsX).First());var secondX=PointText(fields.Where(field=>field.IsX).ElementAt(1));
   var accepted=RoiGeometryEditing.Vertices(model.Rows[0].Geometry);
   firstY.Focus();Pump();firstY.Text="40000";secondX.Focus();Pump();secondX.Text="99.125";SendKey(secondX,Key.Enter,true);
   Require(model.Writes==0&&firstY.Text=="40000"&&secondX.Text=="99.125"&&editor.HasInvalidText,"非法整组发生半写或原文丢失："+editor.Error);
   model.Reject=true;firstY.Focus();Pump();firstY.Text="45.625";var attempts=model.Attempts;SendKey(firstY,Key.Enter,true);
   Require(model.Writes==0&&model.Attempts==attempts+1&&firstY.Text=="45.625"&&secondX.Text=="99.125"&&accepted.SequenceEqual(RoiGeometryEditing.Vertices(model.Rows[0].Geometry)),"宿主拒绝未保整组原文");
   var item=(NativePolygonRoiProjection)session.CurROIData!;var points=item.PolygonPoints;_=CollectionViewSource.GetDefaultView(points);var observed=false;
   points.CollectionChanged+=(_,args)=>{Require(args.NewItems?.Count==1,"WPF范围通知");if(!observed){observed=true;Require(points[0].Y==45.625/320&&points[1].X==99.125/640,"观察者看到半组候选");}};
   model.Reject=false;SendKey(firstY,Key.Enter,true);Pump();
   var next=RoiGeometryEditing.Vertices(model.Rows[0].Geometry);Require(observed&&model.Writes==1&&next[0].Y==45.625&&next[1].X==99.125&&next[3]==accepted[3],"修正未一次完整接纳："+editor.Error);
   fields[0].StartValue=1234;Require(model.Writes==1,"旧Point控件仍写入");Show(new TextBlock{Text="结束"});
  });
  Test("Polygon损坏真实PointSource绑定阻断Enter且原文留焦",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[640*320]);
   var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"多边形",PolygonShape()));using var session=model.Open();using var editor=new NativeRoiEditor();editor.Bind(session);Show(editor);session.Select("detect",model.Rows[0].Id);Pump();
   ((ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView)).IsChecked=true;Pump();
   var field=Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint>(editor).First();var text=PointText(field);var before=RoiGeometryEditing.Vertices(model.Rows[0].Geometry);
   BindingOperations.SetBinding(field,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint.PointSourceProperty,new Binding("MissingPolygonPoint"){Source=new object()});
   Require(BindingOperations.GetMultiBindingExpression(field,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint.PointSourceProperty) is null,"测试未破坏原PointSource模板绑定");
   text.Focus();Pump();Require(text.IsKeyboardFocused,"损坏绑定用例未取得真实焦点；loaded="+text.IsLoaded+"；member="+Visuals<TextBox>(editor).Contains(text));text.Text="75.125";SendKey(text,Key.Enter,true);
   Require(model.Writes==0&&editor.HasInvalidText&&text.IsKeyboardFocused&&text.Text=="75.125"&&before.SequenceEqual(RoiGeometryEditing.Vertices(model.Rows[0].Geometry)),"坏PointSource未阻断或丢原文："+editor.Error+"；writes="+model.Writes+"；focus="+text.IsKeyboardFocused+"；raw="+text.Text+"；binding="+BindingOperations.GetMultiBindingExpression(field,Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint.PointSourceProperty)?.Status+"；member="+Visuals<TextBox>(editor).Contains(text));
   Show(new TextBlock{Text="结束"});
  });
  Test("原顶点手柄重复坐标按实际容器序号选择",()=>
  {
   var points=new System.Collections.ObjectModel.ObservableCollection<Point>{new(10,10),new(10,10),new(50,60)};
   var page=new Native.Display.VMBaseControls.Base.ImageView.ROI.MoveAreaPage{ItemsSource=points,Width=200,Height=120};Show(page);Pump();
   var second=(Native.Display.VMBaseControls.Base.ImageView.ROI.MoveArea)page.ItemContainerGenerator.ContainerFromIndex(1);
   second.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
   Require(page.SelectedIndex==1,"重复点手柄仍按Hash选中首槽");Show(new TextBlock{Text="结束"});
  });
  Test("Polygon卸载重载保留原Point键入精度并拒绝旧命令",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[640*320]);
   var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"多边形",PolygonShape()));using var session=model.Open();using var editor=new NativeRoiEditor();editor.Bind(session);Show(editor);session.Select("detect",model.Rows[0].Id);Pump();
   ((ToggleButton)editor.NativeView.Template.FindName("expand",editor.NativeView)).IsChecked=true;Pump();
   var text=PointText(Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint>(editor).First());text.Focus();Pump();text.Text="35.125";
   var context=editor.NativeView.DataContext;var command=(ICommand)context.GetType().GetProperty("ROIValueChangedCommand")!.GetValue(context)!;
   Show(new TextBlock{Text="卸载"});command.Execute(null);Require(model.Writes==0,"卸载旧命令写入");Show(editor);Pump();
   var restored=PointText(Visuals<Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint>(editor).First());
   Require(restored.Text=="35.125"&&model.Writes==0,"卸载丢失原文："+restored.Text);
   Require(editor.TryCommitPending()&&model.Writes==1&&RoiGeometryEditing.Vertices(model.Rows[0].Geometry)[0].X==35.125,"重载后无法捕获原文："+editor.Error);
   Show(new TextBlock{Text="结束"});
  });
 }
 static DeclaredFieldsView AdvancedFields(Action<IReadOnlyDictionary<string,ParameterSetting>> write,ReferenceFieldOptions? references=null,ParameterSetting? firstValue=null)
 {
  var rows=new List<(Port,FieldPresentation,ParameterSetting)>();
  foreach(var page in new[]{"甲页","乙页"})
  {
   var prefix=page=="甲页"?"a":"b";
   rows.Add((new Port(prefix+".gate",DataType.Boolean),new FieldPresentation(prefix+".gate","条件","常规",rows.Count,FieldEditorKind.Scalar){Layout=new(page,"常规")},new ConstantSetting(true)));
   rows.Add((new Port(prefix+".value",DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),new FieldPresentation(prefix+".value","精确数值","高级",rows.Count,FieldEditorKind.Scalar){Layout=new(page,"高级",Advanced:true,VisibleWhen:new(prefix+".gate",true),Minimum:0,Maximum:100,DecimalPlaces:2)},prefix=="a"&&firstValue is not null?firstValue:new ConstantSetting(35d)));
   rows.Add((new Port(prefix+".low",DataType.Integer),new FieldPresentation(prefix+".low","下限","高级",rows.Count,FieldEditorKind.Scalar){Layout=new(page,"高级",Advanced:true,PairWith:prefix+".high",Minimum:0,Maximum:100)},new ConstantSetting(10L)));
   rows.Add((new Port(prefix+".high",DataType.Integer),new FieldPresentation(prefix+".high","上限","高级",rows.Count,FieldEditorKind.Scalar){Layout=new(page,"高级",Advanced:true,Minimum:0,Maximum:100)},new ConstantSetting(20L)));
  }
  return new(rows,write,references);
 }
 static NativeAdvancedPresenter Advanced(DeclaredFieldsView fields,int page)=>((Native.Composition.Apps.XmlUI.ParamListView)((TabControl)fields.Content).Items.Cast<TabItem>().ElementAt(page).Content).Items.Cast<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>().Select(row=>row.View).OfType<NativeAdvancedPresenter>().Single();
 static void VerifyNativeAdvanced()
 {
  Test("原高级按钮同级行、条件与跨页独立，配对附件继承展开归属",()=>
  {
   var writes=0;using var fields=AdvancedFields(_=>writes++);Show(fields);Pump();var first=Advanced(fields,0);var second=Advanced(fields,1);var button=first.NativeView;
   Require(button.Height==30&&button.Margin==new Thickness(0,0,40,0)&&button.Padding==new Thickness(6,0,0,0)&&ReferenceEquals(button.Style,first.FindResource("ExpandButtonStyle")),"高级按钮未消费原模板和边距");
   Require(!Find<Expander>(fields).Any()&&!first.IsExpanded&&!second.IsExpanded,"仍使用嵌套Expander或共享初始状态");
   var value=fields.Fields.Single(field=>field.FieldId=="a.value");var range=fields.Fields.Single(field=>field.FieldId=="a.low").View;
   var after=new TextBlock{Text="右端附件"};fields.AddAfterField("a.high",after,new("a.gate",true));
   Require(value.View.Visibility==Visibility.Collapsed&&range.Visibility==Visibility.Collapsed&&after.Visibility==Visibility.Collapsed,"收起时高级行或附件泄漏");
   first.IsExpanded=true;Pump();Require(value.View.IsVisible&&range.IsVisible&&after.IsVisible&&!second.IsExpanded,"展开未显示同级行或串页");
   foreach(var actual in Visuals<Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl>(value.View).Concat(Visuals<Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl>(range)))
   {
    Require(actual.Margin==new Thickness(32,1,32,0)&&actual.ChildLeftWidth==89,"高级行原宿主边距或列宽未生效");
   }
   var cp=(ContentPresenter)button.Template.FindName("cp",button);var icon=(Image)button.Template.FindName("icon",button);
   Require(Equals(cp.Content,first.FindResource("收起"))&&icon.Source.ToString()!.Contains("uparrow"),"原收起文字/图标未切换");
   Require(Visuals<TextBlock>(cp).All(text=>Equals(text.Foreground.ToString(),button.Foreground.ToString())),"原高级按钮生成文字受宿主隐式样式污染");
   var host=(Native.Composition.Apps.XmlUI.ParamListView)((TabItem)((TabControl)fields.Content).Items[0]).Content;
   host.ChildLeftWidth=170;Pump();
   Require(Visuals<Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl>(value.View).Concat(Visuals<Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl>(range)).All(row=>row.ChildLeftWidth==170),"高级行未跟随原宿主170列宽");
   host.ChildLeftWidth=89;Pump();
   fields.Fields.Single(field=>field.FieldId=="a.gate").TrySetConstant(false);Pump();Require(!value.View.IsVisible&&range.IsVisible&&!after.IsVisible,"高级展开覆盖字段自己的显示条件");
   var before=writes;first.IsExpanded=false;second.IsExpanded=true;fields.Fields.Single(field=>field.FieldId=="a.gate").TrySetConstant(true);first.IsExpanded=true;Pump();
   Require(second.IsExpanded&&writes==before+1&&value.View.IsVisible&&after.IsVisible,"展开状态写入参数或跨页共享");CaptureInline(fields,"native-advanced-rows.png");Show(new TextBlock());
  });
  Test("原高级收起保留浮点原文并关闭滑条，不接纳或改精度",()=>
  {
   var writes=0;using var fields=AdvancedFields(_=>writes++);Show(fields);var advanced=Advanced(fields,0);advanced.IsExpanded=true;Pump();
   var access=fields.Fields.Single(field=>field.FieldId=="a.value");var text=(TextBox)access.InputElement!;var original=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(access.View).Single();var box=(ComboBox)original.FindName("CBox");
   text.Focus();box.IsDropDownOpen=true;Pump();text.Text="35.125";Pump();var identity=access.View;Console.WriteLine("高级折叠前 writes="+writes+" raw="+text.Text+" value="+((ConstantSetting)access.Setting).Value+" popup="+box.IsDropDownOpen);
   advanced.IsExpanded=false;Pump();Require(!box.IsDropDownOpen&&writes==0&&access.Setting is ConstantSetting{Value:double accepted}&&accepted==35,"收起提交待编辑值或未关滑条 writes="+writes+" raw="+text.Text+" value="+((ConstantSetting)access.Setting).Value+" popup="+box.IsDropDownOpen);
   advanced.IsExpanded=true;Pump();Require(ReferenceEquals(access.View,identity)&&text.Text=="35.125"&&writes==0,"展开丢失高精度原文或重建编辑器");
   text.Text="bad";advanced.IsExpanded=false;Pump();Require(!fields.TryValidate(out _)&&fields.HasInvalidActiveFields,"收起跳过非法高级字段校验");
   fields.Fields.Single(field=>field.FieldId=="a.gate").TrySetConstant(false);Require(fields.TryValidate(out _),"业务条件失活仍校验隐藏数值");Show(new TextBlock());
  });
  Test("原高级双端滑条收起保留两端待编辑原文且无半写",()=>
  {
   var writes=0;using var fields=AdvancedFields(_=>writes++);Show(fields);var advanced=Advanced(fields,0);advanced.IsExpanded=true;Pump();
   var lower=fields.Fields.Single(field=>field.FieldId=="a.low");var upper=fields.Fields.Single(field=>field.FieldId=="a.high");var low=(TextBox)lower.InputElement!;var high=(TextBox)upper.InputElement!;
   var native=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(lower.View).Single();var box=(ComboBox)native.FindName("CBox");box.IsDropDownOpen=true;Pump();low.Focus();low.Text="17";high.Text="23";
   advanced.IsExpanded=false;Pump();Require(!box.IsDropDownOpen&&writes==0&&lower.Setting is ConstantSetting{Value:10L}&&upper.Setting is ConstantSetting{Value:20L},"双端折叠发生写入或半写");
   advanced.IsExpanded=true;Pump();Require(low.Text=="17"&&high.Text=="23"&&writes==0,"双端折叠丢原文");Show(new TextBlock());
  });
  Test("原高级收起关闭扩展订阅窗且保留完整引用身份",()=>
  {
   var source=new DeclaredReferenceCandidate("浮点来源","source",null,new Port("float",DataType.Number),Array.Empty<string>());var initial=new ReferenceSetting(source.Reference(BindingSelection.All));var writes=0;
   using var fields=AdvancedFields(_=>writes++,new ReferenceFieldOptions(_=>new(new[]{source},Array.Empty<DeclaredReferenceCandidate>())),initial);Show(fields);var advanced=Advanced(fields,0);advanced.IsExpanded=true;Pump();
   var access=fields.Fields.Single(field=>field.FieldId=="a.value");var before=System.Text.Json.JsonSerializer.Serialize(((ReferenceSetting)access.Setting).Reference);
   var native=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(access.View).Single();var session=native.SubscribeDataLeft!;((ICommand)session.GetType().GetProperty("PopSubscribeWindowCommand")!.GetValue(session)!).Execute(null);Pump();
   var window=owner.OwnedWindows.Cast<Window>().OfType<Native.Composition.Apps.XmlUI.ParamSubscribeWindow>().Single();var request=(ReferenceExpansionRequest)window.DataContext;
   advanced.IsExpanded=false;Pump();Require(!window.IsVisible&&!request.IsValid&&writes==0&&before==System.Text.Json.JsonSerializer.Serialize(((ReferenceSetting)access.Setting).Reference),"折叠未关扩展窗或清除了引用");
   advanced.IsExpanded=true;Pump();Require(access.Setting is ReferenceSetting&&writes==0,"展开恢复成其他模式");Show(new TextBlock());
  });
  Test("原高级隐藏捕获被宿主拒绝后保留原文并可修改重试",()=>
  {
   var attempts=0;var reject=true;using var fields=AdvancedFields(_=>{attempts++;if(reject)throw new ArgumentException("宿主拒绝");});Show(fields);var advanced=Advanced(fields,0);advanced.IsExpanded=true;Pump();
   var access=fields.Fields.Single(field=>field.FieldId=="a.value");var text=(TextBox)access.InputElement!;text.Focus();text.Text="35.125";advanced.IsExpanded=false;Pump();
   Require(attempts==0&&!fields.TryValidate(out _)&&attempts==1&&access.Setting is ConstantSetting{Value:35d},"隐藏捕获未被明确拒绝或污染accepted");
   advanced.IsExpanded=true;Pump();Require(text.Text=="35.125","拒绝隐藏捕获后丢原文");reject=false;text.Text="36.25";SendKey(text,Key.Enter,true);Require(attempts==2&&access.Setting is ConstantSetting{Value:36.25d}&&!fields.HasInvalidActiveFields,"拒绝后修改重试未成功");Show(new TextBlock());
  });
  Test("原高级关层状态观察者异常仍隐藏全部目标并保原文",()=>
  {
   var writes=0;using var fields=AdvancedFields(_=>writes++);Show(fields);var advanced=Advanced(fields,0);advanced.IsExpanded=true;Pump();var access=fields.Fields.Single(field=>field.FieldId=="a.value");var text=(TextBox)access.InputElement!;
   var native=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeBase>(access.View).Single();var box=(ComboBox)native.FindName("CBox");text.Focus();box.IsDropDownOpen=true;Pump();text.Text="35.125";
   var popup=(Popup)box.Template.FindName("PART_Popup",box);Require(popup.IsOpen,"实际滑条弹层未打开，不能测试关层异常");var descriptor=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ComboBox.IsDropDownOpenProperty,box.GetType());var invoked=false;EventHandler handler=(_,_)=>{if(!box.IsDropDownOpen){invoked=true;throw new InvalidOperationException("测试关层状态异常");}};descriptor.AddValueChanged(box,handler);
   var reported=false;try{advanced.IsExpanded=false;}catch(Exception){reported=true;}finally{descriptor.RemoveValueChanged(box,handler);}Pump();
   Require(invoked&&reported&&!box.IsDropDownOpen&&!popup.IsOpen&&access.View.Visibility==Visibility.Collapsed&&fields.Fields.Single(field=>field.FieldId=="a.low").View.Visibility==Visibility.Collapsed&&writes==0,"关层异常截断显隐或吞异常 invoked="+invoked+" reported="+reported+" popup="+box.IsDropDownOpen+" valueVisibility="+access.View.Visibility+" rangeVisibility="+fields.Fields.Single(field=>field.FieldId=="a.low").View.Visibility+" writes="+writes+" raw="+text.Text);
   advanced.IsExpanded=true;Pump();Require(text.Text=="35.125"&&writes==0,"关层异常丢原文");Show(new TextBlock());
  });
  Test("原高级显隐通知重入保持最新展开状态且旧按钮释放后失效",()=>
  {
   var writes=0;var fields=AdvancedFields(_=>writes++);Show(fields);var advanced=Advanced(fields,0);advanced.IsExpanded=true;Pump();
   var value=fields.Fields.Single(field=>field.FieldId=="a.value").View;var range=fields.Fields.Single(field=>field.FieldId=="a.low").View;var reentered=false;
   value.IsVisibleChanged+=(_,_)=>{if(!value.IsVisible&&!reentered){reentered=true;advanced.IsExpanded=true;}};
   advanced.IsExpanded=false;Pump();Require(reentered&&advanced.IsExpanded&&value.IsVisible&&range.IsVisible&&writes==0,"旧显隐循环覆盖同步展开新状态");
   var old=advanced.NativeView;fields.Dispose();old.IsChecked=!old.IsChecked;Pump();Require(!old.IsEnabled&&fields.Content is null&&writes==0,"释放后旧按钮复活控件或写入");Show(new TextBlock());
  });
 }
 static void VerifyNativeParameterHost()
 {
  Test("原参数宿主换源枚举与首组通知重入，旧集合不能覆盖新根",()=>
  {
   using var a=new SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow(new("A"),new TextBlock());
   using var b=new SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow(new("B"),new TextBlock());
   using var c=new SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow(new("C"),new TextBlock());
   var list=new Native.Composition.Apps.XmlUI.ParamListView();
   var old=new System.Collections.ObjectModel.ObservableCollection<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>{a,b};
   var current=new System.Collections.ObjectModel.ObservableCollection<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>{c};
   IEnumerable<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow> Reentrant(){list.SetRows(current);yield return a;}
   list.SetRows(Reentrant());Require(list.Items.Count==1&&ReferenceEquals(list.Items[0],c),"枚举旧源覆盖同步换入的新源");
   a.GroupName.PropertyChanged+=(_,args)=>{if(args.PropertyName=="IsFirst"&&a.GroupName.IsFirst)old.Remove(a);};
   list.SetRows(old);Require(old.Count==1&&b.GroupName.IsFirst,"旧组序循环覆盖新首组");
   list.SetRows(current);old.Add(a);Require(list.Items.Count==1&&ReferenceEquals(list.Items[0],c)&&c.GroupName.IsFirst,"旧集合通知污染当前宿主");
   list.SetRows(null);current.Clear();Require(list.ItemsSource is null,"解除来源后旧通知恢复宿主");
  });
  Test("原输入分组无内部滚动，同实例重绑保留而替换和关闭释放",()=>
  {
   var schema=new ToolSchema(new[]{new Port("source",DataType.Integer,Required:false,EditModes:FieldEditModes.Reference)},Array.Empty<Port>(),
    new ToolPresentation(new[]{new FieldPresentation("source","来源","输入",0,FieldEditorKind.Scalar){Layout=new("结果显示","图像输入")}}));
   DeclaredInputFieldsView Create()=>new(schema,_=>new UnsetSetting(),(_,_)=>{},ReferenceFieldOptions.Empty);
   var input=Create();var editor=input.Fields.Single();var row=input.Items.Cast<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>().Single();
   var draft=new ColorLifetimeDraft();using var settings=new DeclaredSettingsView(draft,new(InputSection:"结果显示",InputContent:input,GraphicDisplays:ColorLifetimeDraft.Graphics));Show(settings);Pump();
   var outer=(Native.Composition.Apps.XmlUI.ParamListView)((TabControl)((DeclaredFieldsView)settings.Content).Content).Items.Cast<TabItem>().Single().Content;
   Require(Visuals<ScrollViewer>(input).Count(scroll=>scroll.TemplatedParent is Native.Composition.Apps.XmlUI.ParamListView)==0&&Visuals<ScrollViewer>(outer).Count(scroll=>scroll.TemplatedParent is Native.Composition.Apps.XmlUI.ParamListView)==1,"输入组叠加滚动宿主 input="+Visuals<ScrollViewer>(input).Count()+" outer="+Visuals<ScrollViewer>(outer).Count()+" details="+string.Join(";",Visuals<ScrollViewer>(outer).Select(scroll=>scroll.Name+":"+scroll.TemplatedParent?.GetType().FullName)));
   outer.ChildLeftWidth=170;Pump();Require(input.ChildLeftWidth==170&&Visuals<Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl>(input).Single().ChildLeftWidth==170,"输入组未继承原宿主列宽");
   settings.Bind(draft,new(InputSection:"结果显示",InputContent:input,GraphicDisplays:ColorLifetimeDraft.Graphics));Show(settings);Pump();
   Require(ReferenceEquals(input.Fields.Single(),editor)&&ReferenceEquals(row.View,editor)&&row.ParamItemSelectedCommand.CanExecute(null),"同实例重绑提前释放输入组");
   var next=Create();var nextRow=next.Items.Cast<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>().Single();
   settings.Bind(draft,new(InputSection:"结果显示",InputContent:next,GraphicDisplays:ColorLifetimeDraft.Graphics));Pump();Require(row.View is null&&input.ItemsSource is null&&!row.ParamItemSelectedCommand.CanExecute(null),"替换输入后旧行未失效");
   settings.Dispose();Require(nextRow.View is null&&next.ItemsSource is null&&!nextRow.ParamItemSelectedCommand.CanExecute(null),"关闭未释放输入组");Show(new TextBlock());
  });
  Test("原参数宿主独立分组、行边距与89/170列宽及实例隔离",()=>
  {
   using var field=new DeclaredFieldEditor(new Port("enabled",DataType.Boolean),"启用",new ConstantSetting(true),_=>{});
   using var row=new SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow(new("原分组"),field);
   var list=new Native.Composition.Apps.XmlUI.ParamListView();list.SetRows(new[]{row});Show(list);Pump();
   var nativeRow=Visuals<Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl>(field).Single();
   Require(nativeRow.Margin==new Thickness(32,1,32,0)&&nativeRow.ChildLeftWidth==89&&Math.Abs(nativeRow.ColumnDefinitions[0].ActualWidth-89)<.1,"原行未取正式宿主margin/左宽");
   Require(Visuals<ScrollViewer>(list).Count()==1,"参数页出现双滚动宿主");
   var group=Visuals<GroupItem>(list).Single();Require(((FrameworkElement)group.Template.FindName("rec",group)).Visibility==Visibility.Collapsed,"首组分隔线未隐藏");
   list.ChildLeftWidth=170;Pump();Require(nativeRow.ChildLeftWidth==170&&Math.Abs(nativeRow.ColumnDefinitions[0].ActualWidth-170)<.1,"正式宿主170列宽未传原行");
   var other=new Native.Composition.Apps.XmlUI.ParamListView();other.SetRows(Array.Empty<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>());
   Require(!ReferenceEquals(list.ItemsSource,other.ItemsSource)&&list.Items.Count==1&&other.Items.Count==0,"多个宿主共享CollectionViewSource");
   CaptureInline(list,"native-parameter-host.png");list.SetRows(null);other.SetRows(null);Show(new TextBlock{Text="完成"});
  });
  Test("原参数宿主弹层子错误汇总与监听异常不截断其余行刷新",()=>
  {
   var draft=new ColorLifetimeDraft();var declarations=draft.Schema.Presentation!.Fields.Select(p=>(draft.Schema.Inputs.Single(port=>port.Id==p.FieldId),p,(ParameterSetting)new ConstantSetting(draft.ReadFields().Get<object?>(p.FieldId)))).ToList();
   declarations.Add((new Port("other",DataType.Boolean),new FieldPresentation("other","其他","其他",2,FieldEditorKind.Scalar){Layout=new("结果显示","其他")},new ConstantSetting(true)));
   var writes=0;using var fields=new DeclaredFieldsView(declarations,_=>writes++,graphicDisplays:ColorLifetimeDraft.Graphics);
   var list=(Native.Composition.Apps.XmlUI.ParamListView)((TabControl)fields.Content).Items.Cast<TabItem>().Single().Content;
   var rows=list.Items.Cast<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>().ToArray();var later=0;
   rows[0].PropertyChanged+=(_,args)=>{if(args.PropertyName=="MyItemState")throw new InvalidOperationException("测试正常刷新监听异常");};
   rows[1].PropertyChanged+=(_,args)=>{if(args.PropertyName=="MyItemState")later++;};
   Show(fields);Pump();
   var editor=fields.Fields.Single(field=>field.FieldId=="rgba").View;
   var states=editor.GetType().GetField("_states",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(editor)!;
   var state=states.GetType().GetProperty("Item")!.GetValue(states,new object[]{"rgba"})!;
   var border=(Border)VisualTreeHelper.GetParent(Visuals<Native.Composition.Apps.XmlUI.ParamUserControl>(list).First());
   void Error(string method,string text){var prior=later;var threw=false;try{state.GetType().GetMethod(method)!.Invoke(state,new object[]{text});}catch(TargetInvocationException){threw=true;}Pump();Require(threw&&later>prior,"首行异常截断后续行刷新");}
   Error("SetSourceError","来源错误");Require(rows[0].MyItemState==SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterItemState.ReadError&&((SolidColorBrush)border.BorderBrush).Color==Colors.Yellow,"弹层子来源错误未汇总黄框");
   Error("SetEditError","编辑错误");Require(rows[0].MyItemState==SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterItemState.WriteError&&((SolidColorBrush)border.BorderBrush).Color==Colors.Red,"编辑错误未优先红框");
   Error("SetEditError","");Require(((SolidColorBrush)border.BorderBrush).Color==Colors.Yellow,"清编辑错误未保来源错误");
   Error("SetSourceError","");Require(((SolidColorBrush)border.BorderBrush).Color==Colors.Transparent&&writes==0,"清错误仍留框或写入业务值");Show(new TextBlock());
  });
  Test("原参数宿主配对右端附件、插入预检及观察者异常后完整释放",()=>
  {
   var low=new Port("low",DataType.Integer);var high=new Port("high",DataType.Integer);var writes=0;
   var lower=new FieldPresentation("low","下限","g",0,FieldEditorKind.Scalar){Layout=new("参数","分组",Minimum:0,Maximum:100,DecimalPlaces:0,PairWith:"high")};
   var upper=new FieldPresentation("high","上限","g",1,FieldEditorKind.Scalar){Layout=new("参数","分组",Minimum:0,Maximum:100,DecimalPlaces:0)};
   using var fields=new DeclaredFieldsView(new[]{(low,lower,(ParameterSetting)new ConstantSetting(1L)),(high,upper,(ParameterSetting)new ConstantSetting(10L))},_=>writes++);
   Show(fields);Pump();var list=(Native.Composition.Apps.XmlUI.ParamListView)((TabControl)fields.Content).Items.Cast<TabItem>().Single().Content;
   Require(list.Items.Count==1&&fields.Fields.Count==2&&ReferenceEquals(fields.Fields[0].View,fields.Fields[1].View),"双端重复显示或状态分裂");
   var after=new TextBlock{Text="同一配对行之后"};fields.AddAfterField("high",after);var prefix=new TextBlock{Text="前置输入"};fields.AddToGroup("参数",prefix,0);Pump();
   var rows=list.ItemsSource.Cast<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>().ToArray();Require(rows.Length==3&&ReferenceEquals(rows[0].View,prefix)&&ReferenceEquals(rows[2].View,after),"附件插入顺序不符");
   var rejected=false;try{fields.AddToGroup("参数",after);}catch(ArgumentException){rejected=true;}Require(rejected&&list.Items.Count==3,"重复view未预检");
   rejected=false;try{fields.AddToGroup("参数",new TextBlock(),-1);}catch(ArgumentOutOfRangeException){rejected=true;}Require(rejected&&list.Items.Count==3,"非法索引留下半行");
   var accesses=fields.Fields.ToArray();var notified=false;
   rows[0].PropertyChanged+=(_,args)=>{if(args.PropertyName=="View")throw new InvalidOperationException("测试行释放监听异常");};
   rows[0].PropertyChanged+=(_,args)=>{if(args.PropertyName=="View")notified=true;};
   rejected=false;try{fields.Dispose();}catch(Exception){rejected=true;}
   Require(rejected&&notified&&list.ItemsSource is null&&rows.All(row=>row.View is null)&&accesses.All(access=>!access.TrySetConstant(2L))&&writes==0,"清理异常跳过剩余状态或源解除");
   Show(new TextBlock{Text="完成"});
  });
 }
 static void VerifyNativeIntegerRadio()
 {
  NativeIntegerRadioOptions Options(bool five=false)=>new(new[]{new NativeIntegerRadioChoice(10,"绘制"),new NativeIntegerRadioChoice(30,"继承")},five);
  (Port,FieldPresentation,ParameterSetting) Field(string id,long value,int order=0)=>
   (new Port(id,DataType.Integer,EditModes:FieldEditModes.Constant),new FieldPresentation(id,id,"单选",order,FieldEditorKind.Scalar),new ConstantSetting(value));
  RadioButton[] Buttons(DependencyObject view)=>Visuals<RadioButton>(view).ToArray();
  Test("原整数单选非连续值及程序修改沿唯一long状态不重复写入",()=>
  {
   var writes=0;var reject=false;
   using var fields=new DeclaredFieldsView(new[]{Field("two",10)},values=>{if(reject)throw new ArgumentException("拒绝继承");Require(((ConstantSetting)values["two"]).Value is long,"正式状态不是long");writes++;},integerRadios:new Dictionary<string,NativeIntegerRadioOptions>{{"two",Options()}});
   Show(fields);var access=fields.Fields.Single();var input=access.InputElement!;var buttons=Buttons(input);
   Require(buttons.Length==2&&buttons[0].IsChecked==true&&buttons[1].IsChecked==false&&writes==0,"原两槽初始投影误写或选中错误");
   var command=(System.Windows.Input.ICommand)buttons[0].DataContext.GetType().GetProperty("ValueChangedCommand")!.GetValue(buttons[0].DataContext)!;
   buttons[1].SetCurrentValue(ToggleButton.IsCheckedProperty,true);Pump();
   Require(((ConstantSetting)access.Setting).Value is long value&&value==30&&writes==1,"Checked写位置索引或未接纳");
   Require(access.TrySetConstant(10L)&&ReferenceEquals(input,access.InputElement)&&buttons[0].IsChecked==true&&writes==2,"程序写没有同步同一原视图");
   Require(access.TrySetConstant(10L)&&writes==2,"相同值重复writer");
   reject=true;buttons[1].SetCurrentValue(ToggleButton.IsCheckedProperty,true);Pump();
   Require(((ConstantSetting)access.Setting).Value.Equals(10L)&&buttons[0].IsChecked==true&&buttons[1].IsChecked==false&&access.HasInvalidText&&writes==2,"宿主拒绝没有统一回投影");
   reject=false;Require(access.TrySetConstant(30L)&&!access.HasInvalidText&&writes==3,"拒绝修复未清错误");
   input.IsEnabled=false;command.Execute(10);Require(writes==3,"禁用控件接受旧命令");input.IsEnabled=true;
   Show(new TextBlock{Text="卸载"});command.Execute(10);Require(writes==3,"卸载后旧Checked仍写");
   Show(fields);fields.Dispose();command.Execute(10);Require(writes==3&&!access.TrySetConstant(10L),"释放后旧命令或字段访问仍写");
   Show(new TextBlock{Text="完成"});
  });
  Test("原整数单选未知值同值及超Int32初值始终明确拒绝",()=>
  {
   foreach(var value in new[]{99L,(long)int.MaxValue+1})
   {
    var writes=0;using var fields=new DeclaredFieldsView(new[]{Field("unknown",value)},_=>writes++,integerRadios:new Dictionary<string,NativeIntegerRadioOptions>{{"unknown",Options()}});
    Show(fields);var access=fields.Fields.Single();
    Require(access.HasInvalidText&&Buttons(access.View).All(x=>x.IsChecked!=true),"未知初值被默认选第一项");
    Require(!access.TrySetConstant(value)&&access.HasInvalidText&&writes==0&&((ConstantSetting)access.Setting).Value.Equals(value),"未知同值绕过闭集验证");
    Require(!access.TrySetConstant(0L)&&writes==0,"闭集外值写入宿主");
    Require(access.TrySetConstant(30L)&&!access.HasInvalidText&&writes==1,"未知初值不能显式修复");
    Show(new TextBlock{Text="下一项"});
   }
  });
  Test("原整数单选五槽空槽折叠且负值边界值不是占位符",()=>
  {
   var choices=new NativeIntegerRadioOptions(new[]{new NativeIntegerRadioChoice(-1,"负值"),new NativeIntegerRadioChoice(int.MaxValue,"上界"),new NativeIntegerRadioChoice(int.MinValue,"下界")},true);
   var writes=0;using var fields=new DeclaredFieldsView(new[]{Field("five",-1)},_=>writes++,integerRadios:new Dictionary<string,NativeIntegerRadioOptions>{{"five",choices}});
   Show(fields);var access=fields.Fields.Single();var buttons=Buttons(access.View);
   Require(buttons.Length==5&&buttons[0].IsChecked==true&&buttons.Skip(3).All(x=>x.Content is null&&x.Visibility==Visibility.Collapsed),"原五槽空项没有按Content null折叠");
   foreach(var value in new[]{(long)int.MaxValue,int.MinValue,-1})Require(access.TrySetConstant(value),"Int32有效值不能投影");
   Require(writes==3&&buttons[0].IsChecked==true,"边界转换溢出或负值当空哨兵");
   CaptureInline(fields,"native-integer-radio-five.png");Show(new TextBlock{Text="完成"});
  });
  Test("原整数单选同级标签不重复页面边距并保原图标与完整文字",()=>
  {
   using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeIntegerRadioSample();Show(sample);
   var fields=sample.Fields;var first=fields.Fields.First(x=>x.FieldId=="two");var second=fields.Fields.First(x=>x.FieldId=="five");
   var a=Visuals<TextBlock>(first.View).Single(x=>x.Text=="两项选择"&&x.IsVisible);
   var b=Visuals<TextBlock>(second.View).Single(x=>x.Text=="五槽选择"&&x.IsVisible);
   var ordinary=Visuals<TextBlock>(fields.Fields.Single(x=>x.FieldId=="ordinary").View).Single(x=>x.Text=="普通参数"&&x.IsVisible);
   Require(Math.Abs(a.TransformToAncestor(fields).Transform(new Point()).X-b.TransformToAncestor(fields).Transform(new Point()).X)<.1,"两槽/五槽标签行边距不一致");
   Require(Math.Abs(a.TransformToAncestor(fields).Transform(new Point()).X-ordinary.TransformToAncestor(fields).Transform(new Point()).X)<.1,"单选原行重复计算页面边距");
   var originalRow=Visuals<Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl>(first.InputElement!).Single();
   var expected=Native.Common.Localization.LocalizationResourceDictionary.Instance.CultureName.StartsWith("zh",StringComparison.OrdinalIgnoreCase)?89:165;
   Require(originalRow.ChildLeftWidth==expected,"原两槽语言列宽未生效");
   var page=Visuals<Native.Composition.Apps.XmlUI.ParamListView>(fields).Single();
   var fiveRow=Visuals<Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl>(second.InputElement!).Single();
   page.ChildLeftWidth=137;Pump();Require(fiveRow.ChildLeftWidth==137&&originalRow.ChildLeftWidth==expected,"五槽宿主列宽或两槽本地语言绑定失效");
   page.ChildLeftWidth=151;Pump();Require(fiveRow.ChildLeftWidth==151,"原五槽列宽未动态更新");
   foreach(var button in Buttons(sample).Where(x=>x.IsVisible))
   {
    var icon=Visuals<Image>(button).Single();Require(icon.ActualWidth==24&&icon.ActualHeight==24&&icon.Source is BitmapSource,"原单选图标尺寸或资源不正确");
    var text=Visuals<TextBlock>(button).Single(x=>!string.IsNullOrEmpty(x.Text));
    Require(text.ActualHeight>=text.FontSize&&text.Style is null,"原单选文字被暗色宿主隐式样式污染");
   }
   CaptureInline(sample,"native-integer-radio-independent.png");Show(new TextBlock{Text="完成"});
  });
  Test("原整数单选接纳后通知异常不回滚且继续投影",()=>
  {
   var writes=0;using var fields=new DeclaredFieldsView(new[]{Field("two",10)},_=>writes++,integerRadios:new Dictionary<string,NativeIntegerRadioOptions>{{"two",Options()}});
   Show(fields);var access=fields.Fields.Single();var buttons=Buttons(access.View);
   var context=buttons[0].DataContext;var command=(System.Windows.Input.ICommand)context.GetType().GetProperty("ValueChangedCommand")!.GetValue(context)!;
   Action fail=()=>throw new InvalidOperationException("接纳后观察者异常");access.Changed+=fail;
   var thrown=false;try{command.Execute(30);}catch(Exception error){thrown=error.ToString().Contains("接纳后观察者异常");}finally{access.Changed-=fail;}
   Require(thrown&&writes==1&&((ConstantSetting)access.Setting).Value.Equals(30L)&&buttons[1].IsChecked==true&&!access.HasInvalidText,"通知异常被误当候选拒绝或未完成投影");
   Require(access.TrySetConstant(10L)&&writes==2,"观察者异常后字段不能继续用");Show(new TextBlock{Text="完成"});
  });
  Test("原整数单选写入或通知同步释放不复活旧命令",()=>
  {
   foreach(var inWriter in new[]{false,true})
   {
    var writes=0;DeclaredFieldsView? current=null;
    current=new(new[]{Field("two",10)},_=>{writes++;if(inWriter)current!.Dispose();},integerRadios:new Dictionary<string,NativeIntegerRadioOptions>{{"two",Options()}});
    Show(current);var access=current.Fields.Single();var context=Buttons(access.View)[0].DataContext;
    var command=(System.Windows.Input.ICommand)context.GetType().GetProperty("ValueChangedCommand")!.GetValue(context)!;
    if(!inWriter)access.Changed+=()=>current.Dispose();
    command.Execute(30);command.Execute(10);
    Require(writes==1&&!access.TrySetConstant(10L)&&current.Content is null,"同步释放后旧根仍写或复活");current.Dispose();
    Show(new TextBlock{Text="下一项"});
   }
  });
  Test("原整数单选清理一项异常仍退出所有字段监听和视觉",()=>
  {
   var writes=0;var editor=new DeclaredFieldEditor(new Port("two",DataType.Integer,EditModes:FieldEditModes.Constant),"two",new ConstantSetting(10L),_=>writes++,integerRadioOptions:Options());
   Show(editor);var context=Buttons(editor)[0].DataContext;var command=(System.Windows.Input.ICommand)context.GetType().GetProperty("ValueChangedCommand")!.GetValue(context)!;
   var original=((Decorator)context).Child as FrameworkElement;
   typeof(DeclaredFieldEditor).GetField("_detachImageSource",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(editor,(Action)(()=>throw new InvalidOperationException("测试清理异常")));
   var thrown=false;try{editor.Dispose();}catch(Exception error){thrown=error.ToString().Contains("测试清理异常");}
   command.Execute(30);Require(thrown&&editor.Content is null&&!editor.TrySetConstant(30L)&&writes==0,"清理错误跳过原单选释放或视觉解除");
   Require(((Decorator)context).Child is null&&original is {DataContext:null,IsEnabled:false},"原单选只卸载却未真正执行Dispose");
   editor.Dispose();Show(new TextBlock{Text="完成"});
  });
  Test("原整数单选配置冲突在构造前拒绝不静默转成范围",()=>
  {
   void Reject(Action build){var rejected=false;try{build();}catch(ArgumentException){rejected=true;}Require(rejected,"非法原单选声明未拒绝");}
   Reject(()=>new NativeIntegerRadioOptions(new[]{new NativeIntegerRadioChoice(1,"甲"),new NativeIntegerRadioChoice(1,"乙")}));
   Reject(()=>new NativeIntegerRadioOptions(Array.Empty<NativeIntegerRadioChoice>()));
   var left=Field("left",10);var right=Field("right",30,1);
   left.Item2=new FieldPresentation("left","left","单选",0,FieldEditorKind.Scalar){Layout=new("页","组",PairWith:"right")};
   right.Item2=new FieldPresentation("right","right","单选",1,FieldEditorKind.Scalar){Layout=new("页","组")};
   Reject(()=>new DeclaredFieldsView(new[]{left,right},_=>{},integerRadios:new Dictionary<string,NativeIntegerRadioOptions>{{"right",Options()}}));
   Reject(()=>new DeclaredFieldsView(new[]{Field("two",10)},_=>{},integerRadios:new Dictionary<string,NativeIntegerRadioOptions>{{"missing",Options()}}));
  });
 }
 static void VerifyFieldGroups()
 {
  (Port,FieldPresentation,ParameterSetting) Field(string id,long value,int order=0)=>
    (new Port(id,DataType.Integer),new FieldPresentation(id,id,"组合",order,FieldEditorKind.Scalar),(ParameterSetting)new ConstantSetting(value));
  Dictionary<string,ParameterSetting> Values(params (string Id,object Value)[] values)=>values.ToDictionary(x=>x.Id,x=>(ParameterSetting)new ConstantSetting(x.Value));
  Test("整组字段同值仍验证且重入不写宿主",()=>
  {
   var writes=0;var validations=0;var reject=true;DeclaredFieldsView? fields=null;
   fields=new(new[]{Field("a",10),Field("b",30,1)},_=>writes++,validateComposition:snapshot=>
   {
    validations++;Require(snapshot.Current.Count==2&&snapshot.Changes.Count==1&&snapshot.Proposed.Count==2,"组合快照不完整");
    Require(!fields!.TrySetFields(Values(("b",99L)))&&!fields.Fields[1].TrySetConstant(99L),"校验回调重入提交成功");
    return reject?"纯校验拒绝":null;
   });
   using(fields)
   {
    Show(fields);Require(!fields.TrySetFields(Values(("a",10L)))&&validations==1&&writes==0&&fields.Fields[0].HasInvalidText,"同值跳过校验");
    reject=false;Require(fields.TrySetFields(Values(("a",10L)))&&validations==2&&writes==0&&!fields.Fields[0].HasInvalidText,"同值不能修复或重复writer");
    Require(fields.TrySetFields(Values(("a",20L)))&&validations==3&&writes==1,"变化提案没有一次写入");
    Show(new TextBlock{Text="完成"});
   }
  });
  Test("整组字段数组和调用者字典与校验副本相互隔离",()=>
  {
   var array=new[]{1d,2d};var input=Values(("array",array),("a",20L));var writes=0;
   var declarations=new[]{Field("a",10),(new Port("array",TypeRef.SequenceOf(DataType.Number)),new FieldPresentation("array","数组","组合",1,FieldEditorKind.Sequence),(ParameterSetting)new ConstantSetting(new[]{3d,4d}))};
   using var fields=new DeclaredFieldsView(declarations,changes=>
   {
    writes++;Require(((ConstantSetting)changes["a"]).Value.Equals(20L)&&((double[])((ConstantSetting)changes["array"]).Value!).SequenceEqual(new[]{1d,2d}),"writer读到被校验污染的值");
   },validateComposition:snapshot=>
   {
    array[0]=99;input["a"]=new ConstantSetting(99L);
    ((double[])((ConstantSetting)snapshot.Changes["array"]).Value!)[0]=88;
    ((double[])((ConstantSetting)snapshot.Proposed["array"]).Value!)[1]=77;
    return null;
   });
   Show(fields);Require(fields.TrySetFields(input)&&writes==1,"数组组合未接纳");
   Require(((double[])((ConstantSetting)fields.Fields.Single(x=>x.FieldId=="array").Setting).Value!).SequenceEqual(new[]{1d,2d}),"accepted数组被外部副本改写");
   Require(((ConstantSetting)fields.Fields.Single(x=>x.FieldId=="a").Setting).Value.Equals(20L),"调用者字典重写污染accepted");Show(new TextBlock{Text="完成"});
  });
  Test("整组字段未知身份提前拒绝且接纳通知看到全部最终值",()=>
  {
   var writes=0;var validations=0;var after=0;
   using var fields=new DeclaredFieldsView(new[]{Field("a",10),Field("b",30,1)},_=>writes++,validateComposition:_=>{validations++;return null;});
   Show(fields);var failedIdentity=false;try{fields.TrySetFields(Values(("a",20L),("unknown",40L)));}catch(ArgumentException){failedIdentity=true;}
   Require(failedIdentity&&writes==0&&validations==0&&fields.Fields.All(x=>!x.HasInvalidText),"未知字段留下部分错误/修改");
   Action fail=()=>{Require(((ConstantSetting)fields.Fields[0].Setting).Value.Equals(20L)&&((ConstantSetting)fields.Fields[1].Setting).Value.Equals(40L),"通知看到半安装值");throw new InvalidOperationException("接纳后组观察异常");};
   fields.Fields[0].Changed+=fail;fields.Fields[1].Changed+=()=>after++;
   var thrown=false;try{fields.TrySetFields(Values(("a",20L),("b",40L)));}catch(Exception error){thrown=error.ToString().Contains("接纳后组观察异常");}finally{fields.Fields[0].Changed-=fail;}
   Require(thrown&&writes==1&&after==1&&fields.Fields.All(x=>!x.HasInvalidText),"观察异常误作拒绝或跳过另一字段通知");Show(new TextBlock{Text="完成"});
  });
  Test("整组字段校验失效与生命周期回调重入不安装旧范围",()=>
  {
   foreach(var disposeInWriter in new[]{false,true})
   {
    var writes=0;DeclaredFieldsView? fields=null;fields=new(new[]{Field("a",10)},_=>{writes++;if(disposeInWriter)fields!.Dispose();},
     validateComposition:_=>{if(!disposeInWriter)fields!.Dispose();return null;});
    Show(fields);var old=fields.Fields.Single();Require(!fields.TrySetFields(Values(("a",20L)))&&writes==(disposeInWriter?1:0)&&!old.TrySetConstant(30L),"失效范围继续安装或写入");fields.Dispose();Show(new TextBlock{Text="下一项"});
   }
   var count=0;var nested=true;var active=false;DeclaredFieldsView? current=null;
   current=new(new[]{Field("a",10)},_=>count++,isCurrent:()=>{if(active)nested=current!.TrySetFields(Values(("a",99L)));return true;});
   using(current){Show(current);active=true;Require(current.TrySetFields(Values(("a",20L)))&&!nested&&count==1,"isCurrent入口回调绕过重入门禁");Show(new TextBlock{Text="完成"});}
  });
  Test("整组字段成功同步真实布尔枚举文字颜色与参与数值且保留旁侧原文",()=>
  {
   var ports=new[]{new Port("flag",DataType.Boolean),new Port("choice",DataType.Text,Choices:new TextChoiceConstraint(new[]{"x","y"})),new Port("text",DataType.Text),new Port("color",PresentationDeclarations.Rgba),new Port("number",DataType.Number),new Port("other",DataType.Number)};
   var values=new object[]{false,"x","old",PresentationDeclarations.Color(1,2,3,255),1d,2d};
   using var fields=new DeclaredFieldsView(ports.Select((p,i)=>(p,new FieldPresentation(p.Id,p.Id,"组合",i,FieldEditorKind.Scalar){Layout=new("页","组",Minimum:p.Type==DataType.Number?0:null,Maximum:p.Type==DataType.Number?10:null,DecimalPlaces:2)},(ParameterSetting)new ConstantSetting(values[i]))),_=>{});
   Show(fields);var number=fields.Fields.Single(x=>x.FieldId=="number");var other=fields.Fields.Single(x=>x.FieldId=="other");
   ((TextBox)OriginalNumberInput(number.View).FindName("ValueText")).Text="7.77";
   var untouched=(TextBox)OriginalNumberInput(other.View).FindName("ValueText");untouched.Text="8.88";
   Require(!fields.TrySetFields(Values(("number",.12345)))&&((TextBox)OriginalNumberInput(number.View).FindName("ValueText")).Text=="7.77","精度错误被接受或拒绝丢原文");
   Require(fields.TrySetFields(Values(("flag",true),("choice","y"),("text","new"),("color",PresentationDeclarations.Color(22,33,44,255)),("number",.12))),"多控件组合被拒绝");Pump();
   var flag=fields.Fields.Single(x=>x.FieldId=="flag");Require(((Native.Composition.Apps.XmlUI.ParamTabControl.BooleanControl)flag.InputElement!).IsChecked==true,"布尔没有投影");
   Require(((ComboBox)fields.Fields.Single(x=>x.FieldId=="choice").InputElement!).SelectedValue?.Equals("y")==true,"枚举没有投影");
   Require(((TextBox)fields.Fields.Single(x=>x.FieldId=="text").InputElement!).Text=="new","文本没有投影");
   Require(((NativeColorPresenter)fields.Fields.Single(x=>x.FieldId=="color").InputElement!).SelectedColor==Color.FromArgb(255,22,33,44),"颜色没有投影");
   Require(((TextBox)OriginalNumberInput(number.View).FindName("ValueText")).Text=="0.12"&&ReferenceEquals(untouched,OriginalNumberInput(other.View).FindName("ValueText"))&&untouched.Text=="8.88","参与数值未替换pending或未参与原文被误提交");
   Show(new TextBlock{Text="完成"});
  });
  Test("整组字段Settings重绑阻止旧候选进入新草稿",()=>
  {
   var oldDraft=new ColorLifetimeDraft();var next=new ColorLifetimeDraft();DeclaredSettingsView? view=null;var rebind=false;
   view=new(oldDraft,new(ValidateComposition:_=>{if(rebind)view!.Bind(next,ColorLifetimeDraft.Options());return null;},GraphicDisplays:ColorLifetimeDraft.Graphics));
   using(view){Show(view);rebind=true;Require(!view.TrySetFields(Values(("visible",false)))&&oldDraft.Writes==0&&next.Writes==0,"旧候选写入重绑草稿");Require(view.TryCapture(out _,out _,out _),"新范围不能继续捕获");Show(new TextBlock{Text="完成"});}
  });
  Test("整组接纳重绘后旧字符串和眼睛回调失效且新输入继续工作",()=>
  {
   var writes=0;
   using var fields=new DeclaredFieldsView(new[]{
    (new Port("text",DataType.Text),new FieldPresentation("text","文字","组合",0,FieldEditorKind.Scalar),(ParameterSetting)new ConstantSetting("old")),
    (new Port("eye",DataType.Boolean),new FieldPresentation("eye","显示","组合",1,FieldEditorKind.Scalar){Layout=new("页","组",DialogPrimary:true)},(ParameterSetting)new ConstantSetting(false))},_=>writes++);
   Show(fields);var text=fields.Fields.Single(x=>x.FieldId=="text");var eye=fields.Fields.Single(x=>x.FieldId=="eye");
   var oldText=(TextBox)text.InputElement!;var oldEye=(ToggleButton)eye.InputElement!;
   Require(fields.TrySetFields(Values(("text","accepted"),("eye",true)))&&writes==1,"初次整组未接纳");
   owner.UpdateLayout();Pump();
   oldText.Text="过期输入";oldEye.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
   Require(writes==1&&((ConstantSetting)text.Setting).Value.Equals("accepted")&&((ConstantSetting)eye.Setting).Value.Equals(true),"旧控件回调写入新渲染");
   ((TextBox)text.InputElement!).Text="current";Require(text.TryCommitPending(),"新原文本未提交");((ToggleButton)eye.InputElement!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
   Require(writes==3&&((ConstantSetting)text.Setting).Value.Equals("current")&&((ConstantSetting)eye.Setting).Value.Equals(false),"新控件不能继续编辑");Show(new TextBlock{Text="完成"});
  });
  Test("整组范围接纳后观察异常仍投影规范值且不重新提交旧原文",()=>
  {
   var writes=0;
   using var fields=new DeclaredFieldsView(new[]{
    (new Port("low",DataType.Number),new FieldPresentation("low","下限","范围",0,FieldEditorKind.Scalar){Layout=new("参数","范围",PairWith:"high",Minimum:0,Maximum:100,DecimalPlaces:2)},(ParameterSetting)new ConstantSetting(10d)),
    (new Port("high",DataType.Number),new FieldPresentation("high","上限","范围",1,FieldEditorKind.Scalar){Layout=new("参数","范围",Minimum:0,Maximum:100,DecimalPlaces:2)},(ParameterSetting)new ConstantSetting(20d))},_=>writes++);
   Show(fields);var low=fields.Fields[0];var input=(TextBox)low.InputElement!;input.Focus();input.Text="15.125";
   Action observer=()=>throw new InvalidOperationException("范围接纳后通知异常");low.Changed+=observer;
   var thrown=false;try{low.TryCommitPending();}catch(Exception error){thrown=error.ToString().Contains("范围接纳后通知异常");}finally{low.Changed-=observer;}
   Require(thrown&&writes==1&&((ConstantSetting)low.Setting).Value.Equals(15.12d)&&input.Text=="15.12"&&!low.HasInvalidText,"接纳后异常恢复旧raw或误作拒绝");
   Require(low.TryCommitPending()&&writes==1,"旧raw重新写入宿主");Show(new TextBlock{Text="完成"});
  });
  Test("整组绘制准入回调递归请求立即拒绝且外层可继续或拒绝",()=>
  {
   foreach(var allow in new[]{false,true})
   {
    using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,240,new byte[76800]);var model=new RoiHost(image);
    NativeRoiSession? session=null;var calls=0;var innerAccepted=false;
    session=new(image,model.Read,model.Accept,()=>model.Alive,()=>new(false,false),"detect",canDraw:_=>{calls++;innerAccepted|=session!.BeginDraw("rectangle") is not null;return allow;});
    using(session){session.DrawRequested+=_=>{};var result=session.BeginDraw("rectangle");Require(!innerAccepted&&calls>0&&(result is not null)==allow,"准入递归或外层结果错误");session.CancelDraw();}
   }
  });
  Test("整组继承同根修订取消旧绘制且检测屏蔽独立并保本地身份",()=>
  {
   var width=owner.Width;var height=owner.Height;owner.Width=1100;owner.Height=950;
   using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample{Width=1020,Height=800};
   try
   {
    Show(sample);var local=sample.LocalItems.Single(x=>x.Group=="detect");var old=sample.Session.BeginDraw("rectangle");Require(old is not null,"本地原请求未建立");
    var changes=Values(("detect.mode",1L));changes["detect.source"]=new ReferenceSetting(sample.Reference("detect"));
    Require(sample.Fields.TrySetFields(changes)&&sample.Revision==1&&sample.Session.Stamp.Revision==1&&sample.Session.ActiveDraw is null,"字段与几何修订未统一或旧绘制未取消");
    Require(!sample.Session.CompleteDraw(old!,local.Geometry)&&sample.Session.BeginDraw("rectangle") is null,"继承模式旧完成/新绘制仍被接纳");
    Require(sample.Session.Items.Single(x=>x.Group=="detect") is {Enabled:false} source&&source.Id!=local.Id&&sample.LocalItems.Single(x=>x.Id==local.Id).Geometry==local.Geometry,"继承污染本地或借用本地ID");
    Pump();var inherited=sample.Session.Items.Single(x=>x.Group=="detect");
    var roi=Visuals<Native.Display.VMBaseControls.Base.ImageView.ROI.NormalizedRectROI>(sample.Scene.Viewport).Single(x=>ReferenceEquals(x.DataContext,inherited));
    var container=Visuals<Native.Display.VMBaseControls.Base.ImageView.ContainerItem>(sample.Scene.Viewport).Single(x=>ReferenceEquals(x.DataContext,inherited));
    var outline=(System.Windows.Shapes.Rectangle)roi.Template.FindName("rc",roi);
    Require(roi.IsVisible&&!roi.IsHitTestVisible&&container.ActualWidth>1&&container.ActualHeight>1&&outline.RenderedGeometry.Bounds.Width>1&&outline.RenderedGeometry.Bounds.Height>1,"来源原图形未实际显示或仍命中编辑");
    Require(!Visuals<Native.Display.VMBaseControls.Base.ImageView.ROI.NormalizedRectROI>(sample.Scene.Viewport).Any(x=>x.DataContext is NativeBoxRoiProjection p&&p.Id==local.Id)&&!sample.Session.Select("detect",inherited.Id),"本地图形残留或继承对象可编辑");
    Require(((FrameworkElement)roi.Template.FindName("rect1",roi)).Visibility==Visibility.Collapsed,"继承对象角点未退出");CaptureInline(sample,"native-roi-inheritance-active.png");
    sample.Session.SetDrawingGroup("mask");Require(sample.Session.BeginDraw("annulus") is not null,"检测继承误禁屏蔽本地");sample.Session.CancelDraw();sample.Session.SetDrawingGroup("detect");
    Require(sample.Fields.TrySetFields(Values(("detect.mode",0L)))&&sample.Session.Stamp.Revision==2&&sample.Session.Items.Any(x=>x.Id==local.Id&&x.Enabled)&&sample.Session.Select(local.Group,local.Id),"切回未恢复原本地身份");
    CaptureInline(sample,"native-roi-inheritance.png");
   }
   finally{Show(new TextBlock{Text="完成"});owner.Width=width;owner.Height=height;}
  });
  Test("整组继承错误物理帧整组拒绝且继承只投影一个来源对象",()=>
  {
   using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample{Width=1020,Height=800};Show(sample);
   var before=sample.Session.Stamp;var changes=Values(("detect.mode",1L));changes["detect.source"]=new ReferenceSetting(sample.Reference("detect",true));
   Require(!sample.Fields.TrySetFields(changes)&&sample.Revision==0&&sample.Session.Stamp==before&&((ConstantSetting)sample.Fields.Fields.Single(x=>x.FieldId=="detect.mode").Setting).Value.Equals(0L),"错误帧留下部分接纳");
   var request=sample.Session.BeginDraw("rectangle")!;Require(request is not null&&sample.Session.CompleteDraw(request,RoiGeometryEditing.Create("rectangle",new(30,40),new(130,140))),"未产生第二本地对象");
   var count=sample.LocalItems.Count;changes["detect.source"]=new ReferenceSetting(sample.Reference("detect"));Require(sample.Fields.TrySetFields(changes),"合法来源未修复");
   Require(sample.Session.Items.Count(x=>x.Group=="detect")==1&&sample.LocalItems.Count==count,"多本地对象被复制成多份继承来源");
   Require(sample.Fields.TrySetFields(Values(("detect.mode",0L)))&&sample.Session.Items.Count(x=>x.Group=="detect")==2,"切回本地集合丢失");Show(new TextBlock{Text="完成"});
  });
  Test("整组继承原来源树双击与原单选接纳，空态可编辑但不可捕获",()=>
  {
   using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample{Width=1020,Height=800};Show(sample);
   var source=sample.Fields.Fields.Single(x=>x.FieldId=="detect.source");var mode=sample.Fields.Fields.Single(x=>x.FieldId=="detect.mode");
   bool Choose(string? node)
   {
    var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(source.View).Single(x=>x.Name=="rt");
    popup.ApplyTemplate();popup.Focus();popup.IsDropDownOpen=true;Pump();
    var content=(Popup)popup.Template.FindName("PART_Popup",popup);
    var tree=Visuals<Native.Composition.Apps.XmlUI.ParamTab.SubTreeView>(content.Child).Single();var actual=(TreeView)tree.FindName("treeView");
    TreeViewItem? Find(ItemsControl parent)
    {
     for(var i=0;i<parent.Items.Count;i++)
     {
      if(parent.ItemContainerGenerator.ContainerFromIndex(i) is not TreeViewItem row)continue;
      var data=row.DataContext;
      if(data is not null&&(node is null?data.GetType().GetProperty("Clear")?.GetValue(data) is true:
       data.GetType().GetProperty("Candidate")?.GetValue(data) is DeclaredReferenceCandidate c&&c.NodeId==node))return row;
      row.IsExpanded=true;row.UpdateLayout();Pump();var nested=Find(row);if(nested is not null)return nested;
     }
     return null;
    }
    var leaf=Find(actual)??throw new Exception("原来源树缺正式候选或清除叶");
    leaf.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});Pump();
    return popup.IsDropDownOpen||content.IsOpen;
   }
   var inherit=Visuals<RadioButton>(mode.View).Single(x=>Equals(x.Content,"继承"));inherit.SetCurrentValue(ToggleButton.IsCheckedProperty,true);Pump();
   Require(sample.Revision==1&&!sample.Session.Items.Any(x=>x.Group=="detect")&&sample.Session.Items.Any(x=>x.Group=="mask")&&!sample.TryCapture(out _,out var missing)&&missing.Contains("detect.source"),"空继承态未接纳或回落本地几何/错误捕获");
   Require(Choose("detect-foreign")&&sample.Revision==1&&source.Setting is UnsetSetting&&source.HasInvalidText,"外帧来源未拒绝或强制关闭");
   Require(!Choose("detect-source")&&sample.Revision==2&&source.Setting is ReferenceSetting r&&r.Reference.NodeId=="detect-source"&&!source.HasInvalidText,"原树合法来源未接纳");
   Require(!sample.Session.Items.Single(x=>x.Group=="detect").Enabled&&sample.TryCapture(out _,out _),"完整来源未投影或无法捕获");
   Require(!Choose(null)&&sample.Revision==3&&source.Setting is UnsetSetting&&!sample.Session.Items.Any(x=>x.Group=="detect")&&!sample.TryCapture(out _,out _),"清除未成为明确的未完成编辑态");
   Require(!Choose("detect-source")&&sample.Revision==4&&sample.TryCapture(out _,out _),"清除后来源不能修复");
   Show(new TextBlock{Text="完成"});
  });
  Test("整组来源空串清除拒绝不自动提交已有索引原文",()=>
  {
   var writes=0;var candidate=new DeclaredReferenceCandidate("文本序列","texts",null,new Port("values",TypeRef.SequenceOf(DataType.Text)),Array.Empty<string>());
   using var fields=new DeclaredFieldsView(new[]{(new Port("text",DataType.Text,Required:false,EditModes:FieldEditModes.Reference),new FieldPresentation("text","文本来源","组合",0,FieldEditorKind.Scalar),(ParameterSetting)new ReferenceSetting(candidate.Reference(new BindingSelection.AtConstant(0))))},_=>writes++,
    new ReferenceFieldOptions(_=>new(new[]{candidate},Array.Empty<DeclaredReferenceCandidate>()),EditableIndex:true),validateComposition:group=>group.Proposed["text"] is UnsetSetting?"拒绝清除":null);
   Show(fields);var access=fields.Fields.Single();var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(access.View).Single(x=>x.Name=="rt");
   popup.ApplyTemplate();popup.Focus();popup.IsDropDownOpen=true;Pump();var session=popup.DataContext;var type=session.GetType();
   var current=(string)type.GetProperty("CurValueA")!.GetValue(session)!;var pending=current[..current.LastIndexOf('[')]+"[1]";
   type.GetMethod("ObserveText")!.Invoke(session,new object[]{pending});type.GetProperty("CurValueA")!.SetValue(session,"");
   Require(writes==0&&access.Setting is ReferenceSetting{Reference.Selection:BindingSelection.AtConstant{Index:0}}&&access.HasInvalidText&&popup.IsDropDownOpen
    &&(bool)type.GetProperty("HasPendingText")!.GetValue(session)!&&(string)type.GetProperty("CurValueA")!.GetValue(session)! == pending,"清除拒绝后自动提交旧索引或关层丢原文");
   Require((bool)type.GetMethod("TryCommitPending")!.Invoke(session,null)!&&writes==1&&access.Setting is ReferenceSetting{Reference.Selection:BindingSelection.AtConstant{Index:1}},"保留的索引不能显式提交修复");Show(new TextBlock{Text="完成"});
  });
 }
 static void VerifyInheritedAnnulus()
 {
  RecordValue Shape(double start,double sweep)
  {
   var values=RoiGeometryEditing.Values(RingShape(80,30));values["start-radians"]=start*Math.PI/180;values["end-radians"]=(start+sweep)*Math.PI/180;
   return RoiGeometryEditing.Checked(values);
  }
  Test("原继承环形像素中心半径及正负扫角整圆投影不改变正式几何",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[204800]);
   foreach(var sweep in new[]{210d,-210d,360d,-360d})
   {
    var shape=Shape(170,sweep);var model=new RoiHost(image,new RoiEditItem("mask",Guid.NewGuid(),"继承",shape,Enabled:false));using var session=model.Open();
    var item=(NativeInheritedAnnulusProjection)session.Items.Single();var display=(Native.Dependencies.VMControls.RenderInterface.IAnnularSector)item;
    var center=RoiGeometryEditing.Center(shape);Require(display.CenterPoint==new Point(center.X,center.Y)&&display.InnerRadius==30&&display.OuterRadius==80,"像素字段被归一化或丢内外径");
    Require(Math.Abs(display.StartAngle-(sweep<0?170+sweep:170))<1e-9&&Math.Abs(display.AngleRange-Math.Abs(sweep))<1e-9,"度数扫角转换错误");
    Require(item.Kind=="annulus"&&!session.Select(item.Group,item.Id)&&!session.TryCommit(item)&&!item.DataChangedCommand.CanExecute(null)&&model.Writes==0&&ReferenceEquals(model.Rows[0].Geometry,shape),"只读图形冒充sector或写回几何");
   }
  });
  Test("原继承环形同身份切换更换投影并使旧本地输入永久失效",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[204800]);
   var local=new RoiEditItem("mask",Guid.NewGuid(),"本地",Shape(0,360));var model=new RoiHost(image,local);using var session=model.Open();session.Select(local.Group,local.Id);
   var old=(NativeRingRoiProjection)session.Items.Single();old.Radius=.2;Require(old.HasPending,"未建立旧手势");
   model.Rows[0]=local with{Geometry=Shape(170,210),Enabled=false};var rejected=false;try{session.Refresh();}catch(InvalidOperationException){rejected=true;}
   Require(rejected&&ReferenceEquals(session.Items.Single(),old),"同版本类型变化被静默接受");
   model.Revision++;session.Refresh();var inherited=session.Items.Single();old.Radius=.3;
   Require(inherited is NativeInheritedAnnulusProjection&&inherited.Id==old.Id&&!ReferenceEquals(old,inherited)&&session.CurROIData is null&&!old.HasPending&&!session.TryCommit(old)&&model.Writes==0,"旧投影/选中/候选仍有效");
   model.Rows[0]=local;model.Revision++;session.Refresh();var fresh=session.Items.Single();
   Require(fresh is NativeRingRoiProjection&&!ReferenceEquals(fresh,old)&&session.Select(local.Group,local.Id)&&!session.TryCommit(inherited),"切回复活旧实例或不能本地编辑");
  });
  Test("原继承环形原Shape页面实际绘制大弧隐藏替换及旧集合退订",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[204800]);
   var row=new RoiEditItem("mask",Guid.NewGuid(),"继承",Shape(170,210),Enabled:false);var model=new RoiHost(image,row);using var session=model.Open();using var viewport=new NativeRoiViewport{Width=500,Height=400};viewport.Bind(session);Show(viewport);Pump();
   var source=session.Items.Single();Require(viewport.NativeView.ROIList.Count==0&&viewport.NativeView.ShapeList.Count==1&&ReferenceEquals(viewport.NativeView.ShapeList[0],source)&&!viewport.NativeView.ShapeHitTestVisible,"只读环形未走独立原shape页面");
   var visual=Visuals<Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>(viewport).Single();
   Require(visual.IsVisible&&!visual.IsHitTestVisible&&!visual.RenderedGeometry.Bounds.IsEmpty&&visual.RenderedGeometry.Bounds.Width>20&&visual.RenderedGeometry.Bounds.Height>20&&
    !Visuals<Native.Display.VMBaseControls.Base.ImageView.ROI.CircleROI>(viewport).Any(),"未实际绘制原部分环或残留编辑圆环");
   var geometry=(PathGeometry)visual.RenderedGeometry;Require(geometry.Figures.SelectMany(x=>x.Segments).OfType<ArcSegment>().Count(x=>x.IsLargeArc)==2,"大于180扫角被当小弧");
   CaptureInline(viewport,"native-inherited-annulus-active.png");var old=(System.Collections.ObjectModel.ObservableCollection<object>)viewport.NativeView.ShapeList;
   var before=session.Stamp;viewport.NativeView.Init();viewport.Width=460;Pump();
   Require(session.Stamp==before&&Visuals<Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>(viewport).Count()==1&&Visuals<Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>(viewport).Single().RenderedGeometry.Bounds.Width>20,"同修订重新Init或布局后原图形丢失");
   viewport.NativeView.ImageScale=1.5;Pump();var zoomed=Visuals<Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>(viewport).Single();
   Require(Math.Abs(zoomed.StrokeThickness*viewport.NativeView.ThicknessScale-1)<1e-8,"原样式缩放粗细没有同步");viewport.NativeView.ImageScale=1;Pump();
   model.Rows[0]=row with{Visible=false};model.Revision++;session.Refresh();Pump();Require(viewport.NativeView.ShapeList.Count==0&&!Visuals<Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>(viewport).Any(),"隐藏仍残影");
   old.Add(source);Pump();Require(!Visuals<Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>(viewport).Any(),"旧集合仍订阅原页面");
   model.Rows[0]=row with{Geometry=Shape(-40,-80)};model.Revision++;session.Refresh();Pump();Require(Visuals<Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>(viewport).Count()==1,"替换来源残影或未重绘");
   Show(new TextBlock{Text="卸载"});Require(viewport.NativeView.ShapeList is null&&viewport.NativeView.ROIList is null,"卸载未释放两条图形链");
  });
  Test("原继承环形图形集合回调重绑不会旧清理覆盖新根",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[204800]);
   var first=new RoiHost(image,new RoiEditItem("mask",Guid.NewGuid(),"一",Shape(170,210),Enabled:false));var next=new RoiHost(image,new RoiEditItem("mask",Guid.NewGuid(),"二",Shape(20,80),Enabled:false));
   using var a=first.Open();using var b=next.Open();using var viewport=new NativeRoiViewport{Width=500,Height=400};Show(viewport);
   var changed=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Native.Display.VMBaseControls.ImageViewBox.ShapeListProperty,typeof(Native.Display.VMBaseControls.ImageViewBox));var switched=false;
   EventHandler callback=(_,_)=>{if(!switched&&viewport.NativeView.ShapeList?.Count==1){switched=true;viewport.Bind(b);}};changed.AddValueChanged(viewport.NativeView,callback);
   try{viewport.Bind(a);Pump();Require(switched&&ReferenceEquals(viewport.NativeView.ShapeList?[0],b.Items.Single())&&Visuals<Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>(viewport).Count()==1,"重入后新根未投影或被旧清理覆盖");}
   finally{changed.RemoveValueChanged(viewport.NativeView,callback);Show(new TextBlock{Text="完成"});}
  });
  Test("原继承环形更换投影观察异常后仍安装完整新状态并拒绝旧命令",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,320,new byte[204800]);var row=new RoiEditItem("mask",Guid.NewGuid(),"本地",Shape(0,360));
   var model=new RoiHost(image,row);using var session=model.Open();session.Select(row.Group,row.Id);var old=(NativeRingRoiProjection)session.Items.Single();old.Radius=.2;
   System.Collections.Specialized.NotifyCollectionChangedEventHandler observer=(_,_)=>throw new InvalidOperationException("继承集合观察异常");
   var rows=(System.Collections.Specialized.INotifyCollectionChanged)session.Items;rows.CollectionChanged+=observer;model.Rows[0]=row with{Geometry=Shape(170,210),Enabled=false};model.Revision++;
   var thrown=false;try{session.Refresh();}catch(Exception error){thrown=error.ToString().Contains("继承集合观察异常");}finally{rows.CollectionChanged-=observer;}
   Require(thrown&&session.Items.Single() is NativeInheritedAnnulusProjection&&session.Stamp.Revision==1&&session.CurROIData is null&&!old.HasPending&&!old.DataChangedCommand.CanExecute(null)&&model.Writes==0,"观察异常误回滚或旧命令仍有效");
  });
  Test("原继承环形排队布局刷新后释放不重新挂回图形或图像租约",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=new TrackedRoiImage(resources.CreateGray8(640,320,new byte[204800]));
   var model=new RoiHost(image,new RoiEditItem("mask",Guid.NewGuid(),"继承",Shape(170,210),Enabled:false));using var session=model.Open();using var viewport=new NativeRoiViewport{Width=500,Height=400};viewport.Bind(session);Show(viewport);
   Require(image.Active==1&&viewport.NativeView.ShapeList.Count==1,"未持有实际图像/来源");viewport.NativeView.NeedRefreshShapes=true;viewport.Dispose();Pump();
   Require(image.Active==0&&viewport.NativeView.ImageDataSource is null&&viewport.NativeView.ShapeList is null&&viewport.NativeView.ROIList is null&&session.IsCurrent&&model.Writes==0,"旧排队刷新复活资源或越权销毁宿主");Show(new TextBlock{Text="完成"});
  });
  Test("原继承环形完整编辑器共存本地矩形并可切回原圆环",()=>
  {
   var width=owner.Width;var height=owner.Height;owner.Width=1100;owner.Height=950;
   using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample{Width=1020,Height=800};
   try
   {
    Show(sample);var selected=sample.Session.CurROIData!.Id;
    Require(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"mask.mode",new ConstantSetting(1L)},{"mask.source",new ReferenceSetting(sample.PartialAnnulusReference)}}),"来源组合未接纳");Pump();
    Require(sample.Session.CurROIData?.Id==selected&&Visuals<Native.Display.VMBaseControls.Base.ImageView.ROI.NormalizedRectROI>(sample.Scene.Viewport).Count()==1
     &&Visuals<Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>(sample.Scene.Viewport).Count()==1
     &&!Visuals<Native.Display.VMBaseControls.Base.ImageView.ROI.NormalizedCircleROI>(sample.Scene.Viewport).Any(),"原编辑器与两显示链共存失败");
    CaptureInline(sample,"native-inherited-annulus-composition.png");
    Require(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"mask.mode",new ConstantSetting(0L)}}),"不能切回本地");Pump();
    Require(!Visuals<Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>(sample.Scene.Viewport).Any()&&Visuals<Native.Display.VMBaseControls.Base.ImageView.ROI.NormalizedCircleROI>(sample.Scene.Viewport).Count()==1,"切回残留只读图形或原编辑圆环未恢复");
   }
   finally{Show(new TextBlock{Text="完成"});owner.Width=width;owner.Height=height;}
  });
 }
 static RecordValue ComponentGeometry(SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample sample)
 {
  Require(sample.TryCapture(out var snapshot,out var error),"分量几何捕获失败："+error);
  return snapshot!.Items.Single(row=>row.Group=="detect").Geometry;
 }
 sealed class ComponentBindingHost
 {
  internal readonly SunEyeVision.Plugin.Runtime.ParameterBindingService Service=new();
  internal readonly Guid Document=Guid.NewGuid(),Run=Guid.NewGuid(),ExecutionScope=Guid.NewGuid();
  internal readonly BindingNodeId Consumer=new("components","roi"),Numbers=new("components","numbers"),Index=new("components","index");
  internal ImageFrameAddress Frame=null!;internal double[] Values={200d,150d,100d,80d,0d};internal long IndexValue=2;
  internal bool Alive=true,Unavailable,OldIndexRun,BadSchema,ForeignIndexFrame;internal int Epoch,Calls,Resolutions,ExpireOnCurrent;internal Action? DuringResolve,AfterResolve;
  internal bool Current(){if(ExpireOnCurrent>0&&--ExpireOnCurrent==0)Epoch++;return Alive;}
  internal readonly TypeRef Member=TypeRef.RecordOf("component-numeric-record",new[]{new Port("v",DataType.Number,Storage:NumericStorage.Float32),new Port("other",DataType.Number,Storage:NumericStorage.Float32)});
  internal Port ValuesPort=>new("values",TypeRef.SequenceOf(DataType.Number),Storage:NumericStorage.Float32);
  internal Port RecordsPort=>new("records",TypeRef.SequenceOf(Member));
  internal DeclaredReferenceCandidate Sequence=>new("来源数列",Numbers.NodeId,Numbers.ScopeId,ValuesPort,Array.Empty<string>());
  internal DeclaredReferenceCandidate RecordMember=>new("记录成员",Numbers.NodeId,Numbers.ScopeId,RecordsPort,new[]{"v"});
  internal DeclaredReferenceCandidate IndexCandidate=>new("索引来源",Index.NodeId,Index.ScopeId,new Port("value",DataType.Integer,Storage:NumericStorage.Int32),Array.Empty<string>());
  internal ReferenceCandidates Candidates=>new(new[]{Sequence,RecordMember,new DeclaredReferenceCandidate("不应出现double","double",Consumer.ScopeId,new Port("value",DataType.Number),Array.Empty<string>())},
   new[]{IndexCandidate,new DeclaredReferenceCandidate("不应出现long","long",Consumer.ScopeId,new Port("value",DataType.Integer),Array.Empty<string>())});
  internal OutputReference Reference(BindingSelection selection,bool member=false)=>(member?RecordMember:Sequence).Reference(selection);
  internal ReferenceBindingBatchResult Resolve(IReadOnlyList<(Port Target,ParameterSetting Setting)> fields)
  {
   Calls++;var epoch=Epoch;var numberSchema=new ToolSchema(Array.Empty<Port>(),new[]{ValuesPort,RecordsPort});var indexSchema=new ToolSchema(Array.Empty<Port>(),new[]{IndexCandidate.Output});
   var sources=new[]{new BindingSource(Numbers,ExecutionScope,numberSchema,1),new BindingSource(Index,ExecutionScope,indexSchema,1)};
   var scope=new SunEyeVision.Plugin.Runtime.BindingScopeSnapshot(Consumer,Document,7,sources,sources.Select(source=>source.Node),Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());
   RunIdentity Identity(BindingNodeId node)=>new(Document,Run,ExecutionScope,node.NodeId,Guid.NewGuid());var expected=sources.ToDictionary(source=>source.Node,source=>Identity(source.Node));
   var results=new List<BindingNodeResult>();
   if(!Unavailable)results.Add(new(Numbers,expected[Numbers],BadSchema?2:1,numberSchema.Fingerprint,true,new InputValues(numberSchema.Outputs,new Dictionary<string,object?>{
    ["values"]=Values,["records"]=new SequenceValue(RecordsPort.Type,Values.Select(value=>(object?)new RecordValue(Member,new Dictionary<string,object?>{{"v",value},{"other",value+1}})))})));
   results.Add(new(Index,OldIndexRun?expected[Index] with{RunId=Guid.NewGuid()}:expected[Index],1,indexSchema.Fingerprint,true,new InputValues(indexSchema.Outputs,new Dictionary<string,object?>{{"value",IndexValue}})));
   var snapshot=new SunEyeVision.Plugin.Runtime.BindingRunSnapshot(Consumer,Identity(Consumer),7,results,expected);
   var frames=sources.ToDictionary(source=>source.Node,source=>source.Node==Index&&ForeignIndexFrame?new ImageFrameAddress(Guid.NewGuid(),"foreign-index-frame"):Frame);
   var batch=new ReferenceBindingBatch(Service,scope,snapshot,(plan,run)=>{Require(ReferenceEquals(run,snapshot),"分量解析更换快照");Resolutions++;var result=Service.ResolveBinding(plan,run);var action=DuringResolve;DuringResolve=null;action?.Invoke();return result;},Frame,frames,()=>Alive&&Epoch==epoch);
   var resolved=batch.Resolve(fields);var after=AfterResolve;AfterResolve=null;after?.Invoke();return resolved;
  }
 }
 static void VerifyRoiMenusHistory()
 {
  Test("ROI菜单历史：四形状复制删除后像素粘贴与多目标删除",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(640,480,new byte[640*480]);
   foreach(var shape in new[]{RoiGeometryEditing.Create("rectangle",new(20,30),new(120,150)),RingShape(),SectorShape(),PolygonShape()})
   {
    var id=Guid.NewGuid();var model=new RoiHost(image,new RoiEditItem("detect",id,"原对象",shape));using var session=model.Open();var original=session.Items.Single();session.Select("detect",id);
    var command=original.DataChangedCommand;Require(session.TryCopy(original,out var copy)&&copy is not null&&model.Writes==0,"复制修改宿主");
    Require(session.TryDelete(new[]{original})&&model.Rows.Count==0&&session.CurROIData is null,"删除未清当前选择");
    Require(session.TryPaste(copy!,new(300,200))&&model.Rows.Count==1&&model.Rows[0].Id!=id&&model.Writes==2,"源删除后不能粘贴或身份复用");
    var pasted=model.Rows[0].Geometry;
    if(shape.Get<string>("kind")=="polygon")
    {
     var before=RoiGeometryEditing.Vertices(shape);var after=RoiGeometryEditing.Vertices(pasted);
     Require(after.Length==before.Length&&after.Zip(before).All(pair=>pair.First.X-pair.Second.X==after[0].X-before[0].X&&pair.First.Y-pair.Second.Y==after[0].Y-before[0].Y),"多边形次序或刚性平移损坏");
     Require(Math.Abs((after.Min(p=>p.X)+after.Max(p=>p.X))/2-300)<1e-8&&Math.Abs((after.Min(p=>p.Y)+after.Max(p=>p.Y))/2-200)<1e-8,"没有采用原包围盒中心");
    }
    else Require(RoiGeometryEditing.Center(pasted)==new Point2(300,200),"非像素锚点或重复归一化");
    command.Execute(null);Require(!session.TryDelete(new[]{original})&&!session.TryCopy(original,out _)&&model.Writes==2,"旧实例可以写回");
    var foreign=copy! with{Frame=new(Guid.NewGuid(),"foreign")};Require(!session.TryPaste(foreign,new(1,1))&&model.Writes==2,"跨帧剪贴可写");
   }
   var rows=new[]{new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()),new RoiEditItem("mask",Guid.NewGuid(),"B",PolygonShape())};var many=new RoiHost(image,rows);using var all=many.Open();
   Require(all.TryDelete(all.Items.ToArray())&&many.Writes==1&&all.Items.Count==0,"多对象未一次接纳");
  });
  Test("ROI菜单历史：畸形删除回执中止租约且拒绝重复目标",()=>
  {
   var duplicate=new RoiEditTarget("detect",Guid.NewGuid());var invalid=false;try{_ =new RoiEditProposal.Delete(new[]{duplicate,duplicate});}catch(ArgumentException){invalid=true;}Require(invalid,"重复目标未拒绝");
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]),new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));
   using var session=model.Open();model.AfterAccept=result=>result with{Selection=new RoiEditSelection.Select(new("detect",Guid.NewGuid()))};
   session.DrawRequested+=_=>{};var request=session.BeginDraw("rectangle")!;invalid=false;try{session.CompleteDraw(request,RoiGeometryEditing.Create("rectangle",new(0,0),new(10,10)));}catch(InvalidOperationException){invalid=true;}
   Require(invalid&&!session.IsCurrent&&model.Writes==1,"畸形回执伪报回滚或允许继续编辑");
  });
  Test("ROI菜单历史：原Delete命令接纳且卸载保留命令不得直写集合",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]),new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));using var session=model.Open();
   using var viewport=new NativeRoiViewport{Width=400,Height=250};viewport.Bind(session);Show(viewport);Pump();
   var original=session.Items.Single();var parameters=new object[]{original,viewport.NativeView.ROIList};var native=viewport.NativeView;
   Require(native.CopyRoiCommand.CanExecute(parameters),"原Copy入口未接入");native.CopyRoiCommand.Execute(parameters);
   Require(native.PasteRoiCommand.CanExecute(null),"原Paste仍依赖旧PasteList");native.PasteRoiCommand.Execute(null);Require(model.Writes==1&&session.Items.Count==2,"原Paste不经宿主");
   Require(native.DeleteRoiCommand.CanExecute(parameters),"原Delete入口不可用");native.DeleteRoiCommand.Execute(parameters);Require(model.Writes==2&&session.Items.Count==1,"原Delete未恰一次接纳");
   var retained=native.DeleteRoiCommand;Show(new TextBlock());Pump();retained.Execute(new object[]{session.Items[0],native.ROIList});Require(model.Writes==2,"卸载后原命令落回集合变更");
  });
  Test("ROI菜单历史：整根撤销重做还原字段几何且删除旧实例不复活",()=>
  {
   using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample();Show(sample);Pump();var session=sample.Session;
   var local=session.Items.First(item=>item.Group=="detect");var id=local.Id;var oldCommand=local.DataChangedCommand;
   Require(session.TryDelete(new[]{local})&&sample.LocalItems.All(row=>row.Id!=id),"未删除权威根");var revision=sample.Revision;
   Require(session.TryUndo()&&sample.Revision==revision+1&&sample.LocalItems.Any(row=>row.Id==id),"Undo未新revision恢复定义");var restored=session.Items.Single(row=>row.Id==id);
   Require(!ReferenceEquals(local,restored)&&session.CurROIData==restored&&!session.TryDelete(new[]{local}),"Undo复活旧投影或选择错误");oldCommand.Execute(null);Require(sample.Revision==revision+1,"旧命令写新对象");
   Require(session.TryRedo()&&sample.Revision==revision+2&&sample.LocalItems.All(row=>row.Id!=id),"Redo未恢复同一删除");
   Require(session.TryUndo(),"第二次Undo失败");
   Require(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"area.mode",new ConstantSetting(1L)}})&&session.Items.Count==0,"模式提案失败");revision=sample.Revision;
   Require(session.TryUndo()&&sample.Revision==revision+1&&sample.GraphicMode&&session.Items.Any(row=>row.Id==id),"字段与几何分叉或沿用旧revision");
   Require(session.TryRedo()&&!sample.GraphicMode&&session.Items.Count==0,"Redo未同时恢复字段模式和投影");
   Require(session.TryUndo()&&sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"detect.inherit-way",new ConstantSetting(-1L)}})&&!session.History.CanRedo,"分支编辑未清Redo");
   CaptureInline(sample,"native-roi-menu-history.png");Show(new TextBlock());
  });
  Test("ROI菜单历史：accepted回放清双端raw且不调用普通writer",()=>
  {
   var writes=0;var ownerRevision=0;
   using var fields=new DeclaredFieldsView(new[]{
    (new Port("low",DataType.Number),new FieldPresentation("low","范围下限","范围",0,FieldEditorKind.Scalar){Layout=new("参数","范围",PairWith:"high",Minimum:0,Maximum:100,DecimalPlaces:0)},(ParameterSetting)new ConstantSetting(10d)),
    (new Port("high",DataType.Number),new FieldPresentation("high","范围上限","范围",1,FieldEditorKind.Scalar){Layout=new("参数","范围",Minimum:0,Maximum:100,DecimalPlaces:0)},(ParameterSetting)new ConstantSetting(20d))},_=>writes++);
   Show(fields);Pump();var accepted=fields.CaptureAccepted();var low=(TextBox)fields.Fields[0].InputElement!;var high=(TextBox)fields.Fields[1].InputElement!;low.Focus();low.Text="bad";high.Text="999";
   var observed=0;foreach(var field in fields.Fields)field.Changed+=()=>{Require(ownerRevision==1&&fields.Fields.All(row=>row.Setting is ConstantSetting),"观察了半安装根");observed++;};
   Require(fields.TryReplayAccepted(accepted,()=>{ownerRevision++;return true;},()=>true,out var publish)&&writes==0&&ownerRevision==1,"回放走普通writer或same-value省略根安装");publish();publish();Pump();
   Require(low.Text=="10"&&high.Text=="20"&&!fields.HasInvalidActiveFields&&observed==2&&writes==0,"回放没有清除两个旧raw或通知重复");Show(new TextBlock());
  });
  Test("ROI菜单历史：观察异常保已接纳历史并仍可重做",()=>
  {
   using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample();Show(sample);Pump();
   Require(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"area.mode",new ConstantSetting(1L)}}),"前置模式失败");
   Action fail=()=>throw new InvalidOperationException("history observer");var field=sample.Fields.Fields.First();field.Changed+=fail;var revision=sample.Revision;var threw=false;
   try{sample.Session.TryUndo();}catch(InvalidOperationException error){threw=error.Message=="history observer";}
   field.Changed-=fail;Require(threw&&sample.Revision==revision+1&&sample.GraphicMode&&sample.Session.Items.Count>0&&sample.Session.History.CanRedo,"观察异常回滚已安装历史或漏Session安装");
   Require(sample.Session.TryRedo()&&!sample.GraphicMode,"异常后Redo不可用");Show(new TextBlock());
  });
 }
 static void VerifyRoiMenuBoundaryCases()
 {
  Test("ROI菜单历史：接纳后租约失效仍发布并释放字段门禁",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,240,new byte[76800]);var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));
   using var fields=new DeclaredFieldsView(new[]{(new Port("n",DataType.Number),new FieldPresentation("n","值","参数",0,FieldEditorKind.Scalar),(ParameterSetting)new ConstantSetting(2d))},_=>{});var state=fields.CaptureAccepted();var published=0;
   using var session=new NativeRoiSession(image,model.Read,(stamp,proposal)=>
   {
    var result=model.Accept(stamp,proposal);Require(fields.TryReplayAccepted(state,()=>{model.Alive=false;return true;},()=>true,out var notify),"准备失败");
    return result with{Publish=()=>{published++;notify();}};
   },()=>model.Alive,()=>new(false,false),"detect");
   Require(session.TryDelete(session.Items.ToArray())&&!session.IsCurrent&&published==1,"失效早退漏发布");
   Require(fields.Fields[0].TrySetConstant(3d),"字段提交门禁未释放");
  });
  Test("ROI菜单历史：绘制权限和历史查询同步递归有界",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,240,new byte[76800]);var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));NativeRoiSession? scope=null;RoiGeometryClipboard? copy=null;var drawCalls=0;var historyCalls=0;var nested=false;
   using var session=scope=new(image,model.Read,model.Accept,()=>true,()=>{historyCalls++;nested|=scope!.History.CanUndo;return new(true,false);},"detect",canDraw:_=>{drawCalls++;nested|=scope!.CanPaste(copy);return true;});
   Require(session.TryCopy(session.Items[0],out copy)&&session.CanPaste(copy)&&drawCalls==1&&!nested,"绘制查询递归或改写");
   Require(session.History.CanUndo&&historyCalls==1&&!nested&&model.Writes==0,"历史查询递归或误运行事务");
  });
  Test("ROI菜单历史：当前性回调同步解绑或释放视口不写旧会话",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,240,new byte[76800]);
   foreach(var dispose in new[]{false,true})
   {
    var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));Action? callback=null;
    using var session=new NativeRoiSession(image,model.Read,model.Accept,()=>{var action=callback;callback=null;action?.Invoke();return true;},()=>new(false,false),"detect");
    using var viewport=new NativeRoiViewport{Width=400,Height=250};viewport.Bind(session);Show(viewport);Pump();var parameters=new object[]{session.Items[0],viewport.NativeView.ROIList};
    callback=()=>{if(dispose)viewport.Dispose();else viewport.Bind(null);};
    Require(!viewport.NativeView.DeleteRoiCommand.CanExecute(parameters)&&model.Writes==0,"当前性回调换owner仍可执行");viewport.NativeView.DeleteRoiCommand.Execute(parameters);Require(model.Writes==0,"直接Execute绕过门禁");Show(new TextBlock());
   }
  });
  Test("ROI菜单历史：常量accepted与临时引用模式及备份可往返回放",()=>
  {
   var port=new Port("n",DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
   using var fields=new DeclaredFieldsView(new[]{(port,new FieldPresentation("n","值","参数",0,FieldEditorKind.Scalar){Layout=new("参数","参数",Minimum:0,Maximum:10,DecimalPlaces:0)},(ParameterSetting)new ConstantSetting(2d))},_=>{},ReferenceFieldOptions.Empty);
   var snapshot=fields.CaptureAccepted().ToDictionary(pair=>pair.Key,pair=>pair.Value with{ReferenceMode=true});
   Require(fields.TryReplayAccepted(snapshot,()=>true,()=>true,out var publish),"自身可达的常量引用模式拒绝");publish();var restored=fields.CaptureAccepted()["n"];
   Require(restored.ReferenceMode&&restored.Setting is ConstantSetting{Value:2d}&&restored.ConstantBackup is {Value:2d},"模式或备份丢失");
   snapshot["n"]=restored with{ConstantBackup=new ConstantSetting(50d)};var called=false;var rejected=false;
   try{fields.TryReplayAccepted(snapshot,()=>{called=true;return true;},()=>true,out _);}catch(ArgumentException){rejected=true;}
   Require(rejected&&!called&&fields.CaptureAccepted()["n"].ConstantBackup is {Value:2d},"越界备份先安装后校验");
  });
 }
 static void VerifyRoiReceiptPublication()
 {
  Test("ROI菜单历史：Create与Replace失效或畸形回执均完成发布",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,240,new byte[76800]);
   foreach(var create in new[]{true,false})foreach(var invalidate in new[]{true,false})
   {
    var model=create?new RoiHost(image):new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));var published=0;
    using var session=model.Open();model.AfterAccept=result=>
    {
     if(invalidate)model.Alive=false;
     return result with{Publish=()=>published++,Selection=invalidate?result.Selection:new RoiEditSelection.Select(new("detect",Guid.NewGuid()))};
    };
    var accepted=false;var threw=false;
    try
    {
     if(create){session.DrawRequested+=_=>{};var request=session.BeginDraw("rectangle")!;accepted=session.CompleteDraw(request,RoiGeometryEditing.Create("rectangle",new(0,0),new(10,10)));}
     else{var item=session.Items.Single();session.Select(item.Group,item.Id);item.CenterX+=.1;accepted=session.TryCommit(item);}
    }
    catch(InvalidOperationException){threw=true;}
    Require(published==1&&model.Writes==1&&!session.IsCurrent&&(invalidate?accepted:threw),"部分提案入口漏发布、伪报回滚或仍允许旧租约");
   }
  });
  Test("ROI菜单历史：Create与Replace安装观察异常后仍发布且不回滚",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,240,new byte[76800]);
   foreach(var create in new[]{true,false})
   {
    var model=create?new RoiHost(image):new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));var published=0;
    using var session=model.Open();model.AfterAccept=result=>result with{Publish=()=>published++};
    if(!create){var row=session.Items[0];session.Select(row.Group,row.Id);row.CenterX+=.1;}
    RoiDrawRequest? request=null;if(create){session.DrawRequested+=_=>{};request=session.BeginDraw("rectangle");}
    Action fail=()=>{if(model.Writes==1)throw new InvalidOperationException("receipt observer");};session.Changed+=fail;var threw=false;
    try{if(create)session.CompleteDraw(request!,RoiGeometryEditing.Create("rectangle",new(0,0),new(10,10)));else session.TryCommit(session.Items[0]);}
    catch(InvalidOperationException error){threw=error.Message=="receipt observer";}
    finally{session.Changed-=fail;}
    Require(threw&&published==1&&model.Writes==1&&session.Stamp.Revision==1&&session.IsCurrent,"通知异常漏发布或回滚了已安装快照");
   }
  });
 }
 static bool PumpUntil(Func<bool> completed,int milliseconds=1200)
 {
  if(completed())return true;var watch=System.Diagnostics.Stopwatch.StartNew();var frame=new DispatcherFrame();
  var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(10)};
  EventHandler tick=(_,_)=>{if(completed()||watch.ElapsedMilliseconds>=milliseconds)frame.Continue=false;};timer.Tick+=tick;
  try{timer.Start();Dispatcher.PushFrame(frame);}finally{timer.Stop();timer.Tick-=tick;}
  return completed();
 }
 static void VerifyRoiCancellationBoundaries()
 {
  Test("ROI菜单历史：取消绘制回调换根未刷新时不复制过期几何",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,240,new byte[76800]);
   foreach(var advance in new[]{true,false})
   {
    var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));using var session=model.Open();session.DrawRequested+=_=>{};
    var original=session.Items[0];Require(session.TryCopy(original,out var old)&&old is not null,"前置复制失败");session.BeginDraw("rectangle");
    session.DrawCancelled+=_=>{model.Rows[0]=model.Rows[0] with{Geometry=RoiGeometryEditing.Translate(model.Rows[0].Geometry,20,30)};if(advance)model.Revision++;};
    Require(!session.TryCopy(original,out var latest)&&latest is null&&old.Geometry.Get<PointGeometry>("center").Point!=RoiGeometryEditing.Center(model.Rows[0].Geometry),"复制了取消回调之前的旧几何");
    Require(advance?session.IsCurrent&&session.Stamp==model.Stamp:!session.IsCurrent,"新版本未安装或同版改内容未停止租约");
   }
  });
  Test("ROI菜单历史：所有外部当前性回调后再核调用方租约",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,240,new byte[76800]);
   foreach(var paste in new[]{true,false})foreach(var trigger in Enumerable.Range(1,28))
   {
    var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));var armed=false;var entered=false;var caller=true;var calls=0;var invalidated=false;
    using var session=new NativeRoiSession(image,model.Read,model.Accept,()=>{if(armed&&++calls==trigger&&!entered){caller=false;invalidated=true;}return true;},()=>new(false,false),"detect");
    var item=session.Items[0];session.TryCopy(item,out var clip);model.BeforeAccept=()=>entered=true;armed=true;
    if(paste)session.TryPaste(clip!,new(100,100),()=>caller);else session.TryDelete(new[]{item},()=>caller);
    Require(!invalidated||model.Writes==0,"调用方租约在最后外部回调失效后仍写旧owner："+paste+"/"+trigger);
   }
  });
  Test("ROI菜单历史：历史权限回调使调用方失效后不进入owner",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,240,new byte[76800]);var model=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));
   var caller=true;var checks=0;using var session=new NativeRoiSession(image,model.Read,model.Accept,()=>true,()=>{if(++checks==2)caller=false;return new(true,true);},"detect");
   Require(!session.TryUndo(()=>caller)&&checks==2&&model.Attempts==0,"历史最后权限回调失效仍写旧owner");
  });
  Test("ROI菜单历史：重绑关闭实际原菜单且旧关闭不清新菜单",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var model=new RoiHost(resources.CreateGray8(320,240,new byte[76800]),new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));using var session=model.Open();
   using var viewport=new NativeRoiViewport{Width=400,Height=250};viewport.Bind(session);Show(viewport);Pump();
   var target=Visuals<FrameworkElement>(viewport.NativeView).First(view=>view.ContextMenu is not null);var menu=target.ContextMenu!;menu.PlacementTarget=target;menu.IsOpen=true;Pump();Require(menu.IsOpen,"原菜单未打开");
   var firstClosed=false;RoutedEventHandler firstClose=(_,_)=>firstClosed=true;menu.Closed+=firstClose;
   try{viewport.Bind(session);Require(PumpUntil(()=>firstClosed)&&!menu.IsOpen,"重绑仍保留旧原菜单或Closed未完成");}finally{menu.Closed-=firstClose;}
   target=Visuals<FrameworkElement>(viewport.NativeView).First(view=>view.ContextMenu is not null);menu=target.ContextMenu!;
   var newMenu=new ContextMenu();newMenu.Items.Add(new MenuItem{Header="新会话菜单"});target.ContextMenu=menu;menu.PlacementTarget=target;menu.IsOpen=true;Pump();Require(menu.IsOpen,"当前会话原菜单未打开");var reentered=false;
   RoutedEventHandler next=(_,_)=>{if(reentered)return;reentered=true;viewport.Bind(session);target.ContextMenu=newMenu;newMenu.PlacementTarget=target;newMenu.IsOpen=true;};
   menu.Closed+=next;
   try{viewport.Bind(session);PumpUntil(()=>reentered);Require(reentered&&newMenu.IsOpen&&model.Writes==0,"旧关闭回调清了重绑后新菜单或修改owner：reentered="+reentered+",open="+newMenu.IsOpen+",writes="+model.Writes);}
   finally{menu.Closed-=next;newMenu.IsOpen=false;target.ClearValue(FrameworkElement.ContextMenuProperty);Show(new TextBlock());}
  });
 }
 static void VerifyRoiDefinitionReopen()
 {
  Test("ROI菜单历史：accepted定义重建新会话保身份且不复活临时输入剪贴和历史",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,240,new byte[76800]);var original=new RoiHost(image,new RoiEditItem("detect",Guid.NewGuid(),"A",RingShape()));
   var number=new Port("threshold",DataType.Number);var presentation=new FieldPresentation("threshold","阈值","参数",0,FieldEditorKind.Scalar);var values=new Dictionary<string,ParameterSetting>{{"threshold",new ConstantSetting(2d)}};
   using var first=original.Open();using var fields=new DeclaredFieldsView(new[]{(number,presentation,values["threshold"])},changes=>{foreach(var change in changes)values[change.Key]=change.Value;original.Revision++;});
   fields.Fields[0].Changed+=first.Refresh;Require(fields.Fields[0].TrySetConstant(9d),"字段根前置失败");var old=first.Items[0];first.Select(old.Group,old.Id);Require(first.TryCopy(old,out var clipboard)&&first.TryPaste(clipboard!,new(100,100)),"前置几何失败");
   Show(fields);var raw=(TextBox)fields.Fields[0].InputElement!;raw.Focus();raw.Text="bad";var savedRows=original.Rows.ToArray();var savedFields=fields.CaptureAccepted().ToDictionary(pair=>pair.Key,pair=>pair.Value.Setting);var oldField=fields.Fields[0];var oldCommand=old.DataChangedCommand;
   fields.Dispose();first.Dispose();original.Alive=false;var reopened=new RoiHost(image,savedRows);using var next=reopened.Open();var newWrites=0;
   using var nextFields=new DeclaredFieldsView(new[]{(number,presentation,savedFields["threshold"])},_=>{newWrites++;reopened.Revision++;});Show(nextFields);
   Require(nextFields.Fields[0].Setting is ConstantSetting{Value:9d}&&((TextBox)nextFields.Fields[0].InputElement!).Text=="9"&&!nextFields.HasInvalidActiveFields,"重建复活了raw/错误或丢了accepted定义");
   Require(next.Items.Select(row=>row.Id).SequenceEqual(savedRows.Select(row=>row.Id))&&!ReferenceEquals(old,next.Items.First())&&!next.History.CanUndo&&!next.History.CanRedo&&!next.CanPaste(clipboard),"重建丢身份或复活旧投影/历史/剪贴");
   oldCommand.Execute(null);Require(!oldField.TrySetConstant(20d)&&newWrites==0&&reopened.Writes==0,"旧会话写入重建后根");Show(new TextBlock());
  });
  Test("ROI菜单历史：恢复引用定义重新解析当前运行且来源缺失不复活旧receipt",()=>
  {
   var binding=new ComponentBindingHost();using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample(new(binding.Candidates,binding.Resolve,()=>binding.Alive));binding.Frame=sample.Frame;Show(sample);
   var ids=new[]{"detect.rect.x","detect.rect.y","detect.rect.width","detect.rect.height","detect.rect.angle"};var proposal=new Dictionary<string,ParameterSetting>{{"detect.mode",new ConstantSetting(1L)},{"detect.inherit-way",new ConstantSetting(1L)}};
   for(var i=0;i<ids.Length;i++)proposal[ids[i]]=new ReferenceSetting(binding.Reference(new BindingSelection.AtConstant(i)));
   Require(sample.Fields.TrySetFields(proposal)&&RoiGeometryEditing.Center(ComponentGeometry(sample)).X==200,"前置来源失败");var identity=sample.Session.Items.Single(row=>row.Group=="detect").Id;
   Require(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"area.mode",new ConstantSetting(1L)}}),"前置模式失败");var calls=binding.Calls;binding.Values[0]=260;binding.Epoch++;
   Require(sample.Session.TryUndo()&&binding.Calls==calls+1&&RoiGeometryEditing.Center(ComponentGeometry(sample)).X==260&&sample.Session.Items.Single(row=>row.Group=="detect").Id==identity,"Undo恢复了历史receipt或丢稳定派生身份");
   Require(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"area.mode",new ConstantSetting(1L)}}),"第二前置失败");binding.Unavailable=true;binding.Epoch++;
   Require(sample.Session.TryUndo()&&sample.GraphicMode&&!sample.Session.Items.Any(row=>row.Group=="detect")&&!sample.TryCapture(out _,out _)&&sample.Fields.Fields.Single(field=>field.FieldId==ids[0]).Setting is ReferenceSetting,"未发布来源复活旧几何、删除表达式或冒称可捕获");Show(new TextBlock());
  });
 }
 static void VerifyNativeTextPixels()
 {
  byte[] Pixels(BitmapSource source){var rgb=new FormatConvertedBitmap(source,PixelFormats.Rgb24,null,0);var bytes=new byte[rgb.PixelWidth*rgb.PixelHeight*3];rgb.CopyPixels(bytes,rgb.PixelWidth*3,0);return bytes;}
  (int Count,int X,int Y,int Peak) Ink(BitmapSource source,bool green)
  {
   var bytes=Pixels(source);int count=0,x0=int.MaxValue,y0=int.MaxValue,peak=0;
   for(var i=0;i<bytes.Length/3;i++){int r=bytes[i*3],g=bytes[i*3+1],b=bytes[i*3+2],v=green?g:r;
    if(v<=Math.Max(green?r:g,b)+5)continue;count++;x0=Math.Min(x0,i%source.PixelWidth);y0=Math.Min(y0,i/source.PixelWidth);peak=Math.Max(peak,v);}
   return(count,count==0?0:x0,count==0?0:y0,peak);
  }
  BitmapSource Export(NativeTextViewport view,string name)
  {
   var type=typeof(Native.Display.VMBaseControls.ImageViewBox).Assembly.GetType("SunEyeVision.Plugin.Wpf.Native.Common.NativeImageExport",true)!;
   var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name+".png");
   type.GetMethod("SaveRendered")!.Invoke(null,new object?[]{path,view.NativeView.ImageDataSource,view.NativeView.ShapeList?.Cast<object>().ToArray(),view.NativeView});
   return BitmapDecoder.Create(new Uri(path),BitmapCreateOptions.IgnoreImageCache,BitmapCacheOption.OnLoad).Frames[0];
  }
  BitmapSource Actual(NativeTextViewport view)
  {
   owner.UpdateLayout();Pump();var native=view.NativeView;
   var bitmap=new RenderTargetBitmap((int)Math.Ceiling(native.ActualWidth),(int)Math.Ceiling(native.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(native);return bitmap;
  }
  Test("原文字实际绘字及OKNG透明度XY同帧更新",()=>
  {
   using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();var image=resources.CreateGray8(320,160,new byte[320*160]);
   using var view=new NativeTextViewport{Width=480,Height=240};Require(view.BindImage(image,()=>true),"图像绑定失败");Show(view);view.NativeView.Init();Pump();long revision=0;
   void Present(Color color,double opacity,double x=30,double y=30)
   {Require(view.Present(image.Frame,++revision,new[]{new NativeTextOverlay("Blob",new Point(x,y),24,color,opacity)},()=>true),"文字未接纳");Pump();}
   Present(Colors.Lime,1);var ok=Export(view,"native-text-ok");var ink=Ink(ok,true);
   var actualOk=Ink(Actual(view),true);Require(ok.PixelWidth==320&&ok.PixelHeight==160&&ink.Count>20&&actualOk.Count>20,"原导出或实际视口没有绿色文字");
   Present(Colors.Red,1);var ng=Export(view,"native-text-ng");var actualNg=Actual(view);
   Require(Ink(ng,false).Count>20&&Ink(ng,true).Count==0&&Ink(actualNg,false).Count>20&&Ink(actualNg,true).Count==0,"NG色未实际显示");
   Present(Colors.Lime,0);var actualZero=Actual(view);Require(Pixels(Export(view,"native-text-transparent")).All(value=>value==0)&&Ink(actualZero,true).Count==0&&Ink(actualZero,false).Count==0,"透明文字改变黑底或实际视口仍保留文字");
   Present(Colors.Lime,.5);var half=Ink(Export(view,"native-text-half"),true);var actualHalf=Ink(Actual(view),true);
   Require(half.Count>0&&half.Peak<ink.Peak*.65&&actualHalf.Count>0&&actualHalf.Peak<actualOk.Peak*.65,"透明度未降低真实像素强度");
   Present(Colors.Lime,1,80,50);var moved=Ink(Export(view,"native-text-moved"),true);
   Require(Math.Abs(moved.X-ink.X-50)<=1&&Math.Abs(moved.Y-ink.Y-20)<=1,"XY未按图像像素移动");
   var actualMoved=Ink(Actual(view),true);var page=Visuals<FrameworkElement>(view).Single(item=>item.GetType().FullName=="SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ShapeItemsPage");
   var transform=page.TransformToAncestor(view.NativeView);var ratio=view.NativeView.ControlToImageRadio;
   var start=transform.Transform(new Point(30*ratio,30*ratio));var end=transform.Transform(new Point(80*ratio,50*ratio));
   Require(actualMoved.Count>20&&Math.Abs(actualMoved.X-actualOk.X-(end.X-start.X))<=2&&Math.Abs(actualMoved.Y-actualOk.Y-(end.Y-start.Y))<=2,"实际视口未按当前图像变换更新XY");
   var accepted=view.Revision;view.Width=384;view.Height=192;owner.UpdateLayout();view.NativeView.Init();Pump();
   Require(Ink(Actual(view),true).Count>20&&view.Revision==accepted&&image.CopyPixels().All(value=>value==0),"改尺寸丢字、改发布修订或污染原图");
   Require(view.Present(image.Frame,++revision,Array.Empty<NativeTextOverlay>(),()=>true),"空文字未接纳");Pump();
   Require(Ink(Actual(view),true).Count==0,"清空仍保留旧文字");
  });
 }
 static void VerifyNativeTextExpressions()
 {
  var source=new DeclaredReferenceCandidate("同名","text-source",null,new Port("value",DataType.Number),Array.Empty<string>());
  DeclaredFieldsView Create(Func<SunEyeVision.Plugin.Wpf.Adapters.Text.TextExpressionSnapshot> read,Action<IReadOnlyDictionary<string,ParameterSetting>> write)=>new(new[]{
   (new Port("text",DataType.Text,EditModes:FieldEditModes.Constant),new FieldPresentation("text","内容","原文本",0,FieldEditorKind.Scalar),(ParameterSetting)new ConstantSetting("aa{}bb"))},write,
   textExpressions:new Dictionary<string,SunEyeVision.Plugin.Wpf.Adapters.Text.TextExpressionOptions>{{"text",new(read)}});
  Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox Text(DeclaredFieldsView fields)=>(Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox)fields.Fields.Single().InputElement!;
  (Native.Input.Apps.PcUI.Controls.PopupButton Popup,TreeViewItem Group,TreeViewItem Leaf) Open(DeclaredFieldsView fields)
  {
   var text=Text(fields);text.Focus();Require(text.IsKeyboardFocused,"原文本未取得真实键盘焦点");text.Select(0,0);text.Select(3,0);Pump();
   var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(fields).Single(item=>item.Name=="et");
   Require(popup.IsDropDownOpen,"空{}内光标未打开原Popup");
   var part=(Popup)popup.Template.FindName("PART_Popup",popup);
   var view=Visuals<Native.Composition.Apps.XmlUI.ParamTab.SubTreeView>(part.Child).Single();
   var tree=(TreeView)view.FindName("treeView");tree.UpdateLayout();
   var group=(TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0);group.IsExpanded=true;group.UpdateLayout();Pump();
   return(popup,group,(TreeViewItem)group.ItemContainerGenerator.ContainerFromIndex(0));
  }
  void DoubleClick(TreeViewItem item){item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});Pump();}
  Test("原文本空括号焦点开树且原候选仅替换括号内部",()=>
  {
   var writes=0;var formatted=0;
   using var fields=Create(()=>new(new[]{source},candidate=>{Require(ReferenceEquals(candidate,source),"formatter收到克隆候选");formatted++;return "source.value";},()=>true),_=>writes++);
   Show(fields);var opened=Open(fields);DoubleClick(opened.Group);Require(writes==0,"组双击写入");DoubleClick(opened.Leaf);
   Require(writes==1&&formatted==1&&fields.Fields.Single().Setting is ConstantSetting{Value:"aa{source.value}bb"}&&!opened.Popup.IsDropDownOpen,"原叶未单次插入或前后缀丢失");
   Require(Text(fields).Text=="aa{source.value}bb","接纳未投影真实原文本");
  });
  Test("原文本receipt失败退休旧树且纯文本可恢复",()=>
  {
   var fail=false;var writes=0;using var fields=Create(()=>new(new[]{source},_=>"source.value",()=>fail?throw new InvalidOperationException("receipt失效"):true),_=>writes++);
   Show(fields);var opened=Open(fields);fail=true;DoubleClick(opened.Leaf);
   Require(writes==0&&fields.Fields.Single().HasInvalidText&&!opened.Popup.IsDropDownOpen,"receipt异常未拒绝并关树");
   DoubleClick(opened.Leaf);Require(writes==0,"旧叶仍写入");var text=Text(fields);text.Focus();text.Text="plain";SendKey(text,Key.Enter,true);Pump();
   Require(writes==1&&!fields.Fields.Single().HasInvalidText&&fields.Fields.Single().Setting is ConstantSetting{Value:"plain"},"浏览错误阻断纯文本恢复");
  });
  Test("原文本formatter同叶成功重试清错接纳",()=>
  {
   var fail=true;var writes=0;using var fields=Create(()=>new(new[]{source},_=>fail?throw new InvalidOperationException("格式失败"):"source.value",()=>true),_=>writes++);
   Show(fields);var opened=Open(fields);DoubleClick(opened.Leaf);Require(writes==0&&fields.Fields.Single().HasInvalidText,"失败污染字段");
   fail=false;DoubleClick(opened.Leaf);Require(writes==1&&!fields.Fields.Single().HasInvalidText&&!opened.Popup.IsDropDownOpen,"重试残留来源错误");
  });
  Test("原文本浏览失败后程序接纳返回真实结果且同值不重复写",()=>
  {
   var writes=0;using var fields=Create(()=>throw new InvalidOperationException("来源读取失败"),_=>writes++);Show(fields);
   var text=Text(fields);text.Focus();text.Select(0,0);text.Select(3,0);Pump();var field=fields.Fields.Single();
   Require(field.HasInvalidText,"未实际进入来源错误");
   Require(field.TrySetConstant("aa{}bb")&&!field.HasInvalidText&&writes==0,"同值程序接纳被浏览错误阻断或重复写");
   text.Select(0,0);text.Select(3,0);Pump();Require(field.HasInvalidText,"未再次进入来源错误");
   Require(field.TrySetConstant("plain")&&writes==1&&!field.HasInvalidText&&Text(fields).Text=="plain","程序已接纳却返回失败或未投影");
   Require(field.TryCommitPending()&&writes==1,"程序接纳后捕获重复写");
  });
  Test("原文本清浏览错误的观察异常仍关闭原树并投影接纳",()=>
  {
   var writes=0;using var fields=Create(()=>new(new[]{source},_=>throw new InvalidOperationException("格式失败"),()=>true),_=>writes++);Show(fields);
   var opened=Open(fields);DoubleClick(opened.Leaf);var field=fields.Fields.Single();Require(field.HasInvalidText,"未实际保留浏览错误");
   Action fail=()=>{if(field.Setting is ConstantSetting{Value:"accepted"}&&!field.HasInvalidText)throw new InvalidOperationException("清错通知失败");};
   field.Changed+=fail;var reported=false;
   try{field.TrySetConstant("accepted");}catch(Exception){reported=true;}finally{field.Changed-=fail;}
   Require(reported&&writes==1&&field.Setting is ConstantSetting{Value:"accepted"}&&!field.HasInvalidText&&Text(fields).Text=="accepted"&&!opened.Popup.IsDropDownOpen,"观察异常截断清错关树或接纳投影");
   Require(field.TryCommitPending()&&writes==1,"观察异常后重复提交");
  });
  Test("原文本隐藏迟到绑定不覆盖保留原文",()=>
  {
   var writes=0;using var fields=Create(()=>new(new[]{source},_=>"source.value",()=>true),_=>writes++);Show(fields);
   var text=Text(fields);text.Text="draft";text.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
   fields.Visibility=Visibility.Collapsed;Pump();text.Text="late";text.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
   Require(writes==0,"隐藏绑定写入");fields.Visibility=Visibility.Visible;Pump();Require(Text(fields).Text=="draft","恢复丢失原文");
   Require(fields.Fields.Single().TryCommitPending()&&writes==1&&fields.Fields.Single().Setting is ConstantSetting{Value:"draft"},"捕获接受隐藏迟到原文");
  });
  Test("独立原文本配置初值验证及验证重入不覆盖新配置",()=>
  {
   SunEyeVision.Plugin.Wpf.Adapters.Text.TextExpressionSnapshot Empty()=>new(Array.Empty<DeclaredReferenceCandidate>(),_=>"unused",()=>true);
   using(var invalid=new DeclaredFieldEditor(new("text",DataType.Text,EditModes:FieldEditModes.Constant),"文字",new ConstantSetting("bad"),_=>throw new Exception("不应写入")))
   {
    var rejected=false;try{invalid.UseTextExpressions(new(Empty,text=>text=="bad"?"非法初值":null));}catch(ArgumentException){rejected=true;}
    Require(rejected,"独立入口未验证初值");
   }
   var writes=0;
   using(var disposed=new DeclaredFieldEditor(new("text",DataType.Text,EditModes:FieldEditModes.Constant),"文字",new ConstantSetting("ok"),_=>writes++))
   {
    disposed.UseTextExpressions(new(Empty,_=>{disposed.Dispose();return null;}));
    Require(disposed.Content is null&&!disposed.TrySetConstant("late")&&!disposed.TryCommitPending()&&writes==0,"验证释放后复活旧视图");
   }
   using(var editor=new DeclaredFieldEditor(new("text",DataType.Text,EditModes:FieldEditModes.Constant),"文字",new ConstantSetting("ok"),_=>writes++))
   {
    var next=new SunEyeVision.Plugin.Wpf.Adapters.Text.TextExpressionOptions(Empty,text=>text=="blocked"?"新配置拒绝":null);
    editor.UseTextExpressions(new(Empty,_=>{editor.UseTextExpressions(next);return null;}));Show(editor);
    Require(!editor.TrySetConstant("blocked")&&writes==0,"旧配置覆盖重入新配置");
    Require(editor.TrySetConstant("good")&&writes==1&&editor.Setting is ConstantSetting{Value:"good"},"重入后新配置不可编辑");
   }
  });
  Test("原文本同模板重挂拒绝旧叶并恢复当前编辑",()=>
  {
   var writes=0;using var fields=Create(()=>new(new[]{source},_=>"source.value",()=>true),_=>writes++);Show(fields);
   var old=Text(fields);old.Text="draft";old.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
   var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(fields).Single(item=>item.Name=="et");
   DependencyObject? parent=VisualTreeHelper.GetParent(popup);ContentControl? content=null;
   while(parent is not null){if(parent is ContentControl control&&control.ContentTemplate is not null){content=control;break;}parent=VisualTreeHelper.GetParent(parent);}
   Require(content is not null,"无真实原模板宿主");var template=content!.ContentTemplate;
   content.ContentTemplate=null;content.UpdateLayout();Pump();content.ContentTemplate=template;owner.UpdateLayout();Pump();
   var current=Text(fields);Require(!ReferenceEquals(old,current)&&current.Text=="draft","未重建真实叶或原文丢失");
   old.Text="late";old.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();Require(writes==0&&current.Text=="draft","旧绑定污染新模板");
   Require(fields.Fields.Single().TryCommitPending()&&writes==1&&fields.Fields.Single().Setting is ConstantSetting{Value:"draft"},"捕获丢失原文");
   current=Text(fields);current.Focus();current.Text="current";SendKey(current,Key.Enter,true);Pump();Require(writes==2&&fields.Fields.Single().Setting is ConstantSetting{Value:"current"},"新叶不能继续编辑");
  });
  Test("原文本接纳后观察异常不回滚不重复提交",()=>
  {
   var writes=0;using var fields=Create(()=>new(new[]{source},_=>"source.value",()=>true),_=>writes++);Show(fields);var field=fields.Fields.Single();
   Action fail=()=>throw new InvalidOperationException("接纳后观察异常");field.Changed+=fail;var reported=false;
   try{field.TrySetConstant("accepted");}catch(Exception){reported=true;}finally{field.Changed-=fail;}
   Require(reported&&writes==1&&field.Setting is ConstantSetting{Value:"accepted"}&&Text(fields).Text=="accepted","观察异常回滚接纳或原文");
   Require(field.TryCommitPending()&&writes==1,"已接纳原文重复写入");
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
   VerifyInheritedAnnulus();
   VerifyFieldGroups();
   VerifyNativeIntegerRadio();
   VerifyNativeAdvanced();
   VerifyNativeTextExpressions();
   VerifyNativeTextPixels();
   VerifyNativeParameterHost();
   VerifyResultPublication();
   VerifyPropertyPublication();
   VerifyResultOrigins();
   VerifyNativeResultTable();
   VerifyBetween();
   VerifyExpandedReference();
   VerifyNativeBox();
   VerifyNativeRoiCreationContract();
   VerifyRoiMenusHistory();
   VerifyRoiMenuBoundaryCases();
   VerifyRoiReceiptPublication();
   VerifyRoiCancellationBoundaries();
   VerifyRoiDefinitionReopen();
   VerifyNativeRoiDrawingViewport();
   VerifyNativeRoiScene();
   VerifyNativeSceneCleanupFailure();
   VerifyNativeSector();
   VerifyNativeRing();
   VerifyNativePolygon();
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
    var options=new ReferenceFieldOptions(_=>new(new[]{source},Array.Empty<DeclaredReferenceCandidate>()));var writes=0;
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
        "比例",new ReferenceSetting(source.Reference(BindingSelection.All)),_=>writes++,new SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceFieldOptions(_=>new(new[]{source},Array.Empty<DeclaredReferenceCandidate>())),presentation:presentation);
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
   Test("原完整样式弹层草稿编辑校验及卸载关闭",()=>
   {
    var draft=new ColorLifetimeDraft();var reject=false;
    using var settings=new DeclaredSettingsView(draft,new(GraphicDisplays:ColorLifetimeDraft.Graphics,ValidateComposition:_=>reject?"范围失效":null));Show(settings);
    var fields=(DeclaredFieldsView)settings.Content;var line=fields.Fields.Single(item=>item.FieldId=="width");var rgba=fields.Fields.Single(item=>item.FieldId=="rgba");
    StylePopup(settings,surface=>
    {
     Require(!Visuals<DeclaredFieldEditor>(surface).Any()&&Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeFloat>(surface).Count()==2,"原样式创建了隐藏编辑器或重复数值叶");
     ((TextBox)line.InputElement!).Text="7";Require(line.TryCommitPending()&&((ConstantSetting)line.Setting).Value.Equals(7d),"粗细未更新调用方草稿");
     Require(draft.Writes==1&&draft.ReadFields().Get<double>("width")==7d,"粗细未恰一次写真实draft");
     var picker=(Native.Input.HikPcUI.ListView.ColorPickerControlView)rgba.InputElement!;var palette=Palette(picker);palette.IsDropDownOpen=true;Pump();
     var parent=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(settings).Single(item=>Equals(item.Tag,"NativeGraphicStylePopup"));Require(parent.IsDropDownOpen,"原色板关闭父层");
     Native.Input.HikPcUI.ListView.ColorPickerControlView.SelectColorCommand.Execute(new SolidColorBrush(Color.FromArgb(123,32,64,128)),picker);
     var color=(RecordValue)((ConstantSetting)rgba.Setting).Value!;Require(color.Get<long>("a")==123&&color.Get<long>("b")==128,"颜色未进入同一草稿或丢alpha");
     var accepted=draft.ReadFields().Get<RecordValue>("rgba");Require(draft.Writes==2&&accepted.Get<long>("a")==123&&accepted.Get<long>("b")==128,"颜色未恰一次写真实draft");
     palette.IsDropDownOpen=false;Require(!Visuals<Button>(surface).Any(button=>Equals(button.Content,"确定")||Equals(button.Content,"取消")),"原样式层存在自制保存按钮");
     palette.IsDropDownOpen=true;Pump();SendKey(palette,Key.Escape);Require(parent.IsDropDownOpen&&!palette.IsDropDownOpen,"首个Escape没有只关原子色板");
     SendKey(parent,Key.Escape);Require(!parent.IsDropDownOpen,"第二Escape未关父层");
    });
    StylePopup(settings,_=>
    {
     reject=true;((TextBox)line.InputElement!).Text="9";Require(!line.TryCommitPending()&&line.HasInvalidText&&((ConstantSetting)line.Setting).Value.Equals(7d),"拒绝未保原文和accepted");
     Require(draft.Writes==2&&draft.ReadFields().Get<double>("width")==7d&&((TextBox)line.InputElement!).Text=="9",$"拒绝污染真实草稿或丢原文：writes={draft.Writes},width={draft.ReadFields().Get<double>("width")},raw={((TextBox)line.InputElement!).Text}");
     reject=false;var corrected=(TextBox)line.InputElement!;corrected.Focus();corrected.Text="7";SendKey(corrected,Key.Enter,true);Require(!line.HasInvalidText,"纠正未清错误");
     var reopened=OpenStyle(settings);var invalid=(TextBox)line.InputElement!;invalid.Focus();Pump();
     Require(reopened.IsDropDownOpen&&invalid.IsVisible,"Enter后没有重新打开可编辑原样式叶");
     invalid.Text="bad";Require(line.HasInvalidText&&((ConstantSetting)line.Setting).Value.Equals(7d),$"非法输入未拒绝：invalid={line.HasInvalidText},accepted={((ConstantSetting)line.Setting).Value},raw={invalid.Text},visible={invalid.IsVisible},focus={invalid.IsKeyboardFocused}");invalid.Text="7";
     Require(draft.Writes==2&&draft.ReadFields().Get<double>("width")==7d,"同值纠正或非法输入重复写真实草稿");
    });
    var popup=OpenStyle(settings);Show(new TextBlock{Text="卸载"});Require(!popup.IsDropDownOpen,"卸载原样式未关闭");
    Require(draft.Writes==2,"关层卸载重复提交真实草稿");
   });
   Test("原完整样式绑定错误观察者新原文不被旧提交覆盖",()=>
   {
    var draft=new ColorLifetimeDraft();using var settings=new DeclaredSettingsView(draft,ColorLifetimeDraft.Options());Show(settings);
    var line=((DeclaredFieldsView)settings.Content).Fields.Single(field=>field.FieldId=="width");
    StylePopup(settings,surface=>
    {
     var arrange=Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeFloat>(surface).Single(item=>Equals(item.Tag,"NativeGraphicThickness"));
     arrange.SetBinding(Native.Numeric.HikPcUI.ListView.SliderArrangeBase.StartValueProperty,new Binding("Missing"){Source=new object(),Mode=BindingMode.TwoWay});
     Require(arrange.GetBindingExpression(Native.Numeric.HikPcUI.ListView.SliderArrangeBase.StartValueProperty)!.Status!=BindingStatus.Active,"未实际损坏原第三层绑定");
     var states=line.View.GetType().GetField("_states",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(line.View)!;
     var state=states.GetType().GetProperty("Item")!.GetValue(states,new object[]{"width"})!;
     var changed=state.GetType().GetEvent("Changed")!;var calls=0;var armed=true;
     Action replace=()=>{if(armed&&line.Error.Contains("桥绑定",StringComparison.Ordinal)){armed=false;calls++;((TextBox)line.InputElement!).Text="8";}};
     changed.AddEventHandler(state,replace);
     try
     {
      ((TextBox)line.InputElement!).Text="9";
      Require(!line.TryCommitPending()&&calls==1&&((TextBox)line.InputElement!).Text=="8"&&draft.Writes==0&&draft.ReadFields().Get<double>("width")==3d,"绑定错误通知期间的新原文被旧提案覆盖或写入");
     }
     finally{changed.RemoveEventHandler(state,replace);}
    });
   });
   Test("公共设置视图重绑释放原样式父层和色板",()=>
   {
    var oldDraft=new ColorLifetimeDraft();using var settings=new DeclaredSettingsView(oldDraft,ColorLifetimeDraft.Options());Show(settings);
    var oldFields=(DeclaredFieldsView)settings.Content;var oldField=oldFields.Fields.Single(field=>field.FieldId=="rgba");
    var display=OpenStyle(settings);var oldColor=(Native.Input.HikPcUI.ListView.ColorPickerControlView)oldField.InputElement!;var palette=Palette(oldColor);palette.IsDropDownOpen=true;Pump();
    Require(display.IsDropDownOpen&&palette.IsDropDownOpen,"未实际打开两原浮层");
    var nextDraft=new ColorLifetimeDraft();settings.Bind(nextDraft,ColorLifetimeDraft.Options());Pump();
    Require(!display.IsDropDownOpen&&!palette.IsDropDownOpen,"重绑未关闭原父子浮层");
    Native.Input.HikPcUI.ListView.ColorPickerControlView.SelectColorCommand.Execute(new SolidColorBrush(Colors.Red),oldColor);
    Require(!oldField.TrySetConstant(PresentationDeclarations.Color(255,0,0)),"已释放字段接纳旧回调");
    Require(oldDraft.Writes==0&&nextDraft.Writes==0&&((DeclaredFieldsView)settings.Content).Fields.Count==5,"旧回调污染新草稿或新树不完整");
    settings.Dispose();settings.Dispose();Require(!palette.IsDropDownOpen,"重复关闭恢复原色板");
   });
   Test("公共原色板同叶投影与临时卸载重开",()=>
   {
    var draft=new ColorLifetimeDraft();using var settings=new DeclaredSettingsView(draft,ColorLifetimeDraft.Options());Show(settings);
    var field=((DeclaredFieldsView)settings.Content).Fields.Single(item=>item.FieldId=="rgba");var display=OpenStyle(settings);
    var picker=(Native.Input.HikPcUI.ListView.ColorPickerControlView)field.InputElement!;var palette=Palette(picker);palette.IsDropDownOpen=true;Pump();
    Require(field.TrySetConstant(PresentationDeclarations.Color(32,64,128,123)),"同state色板提案未接纳");Pump();
    Require(ReferenceEquals(picker,field.InputElement)&&picker.CurrentColor.Color==Color.FromArgb(123,32,64,128),"原色板未沿唯一叶投影accepted");
    var accepted=draft.ReadFields().Get<RecordValue>("rgba");Require(draft.Writes==1&&accepted.Get<long>("r")==32&&accepted.Get<long>("g")==64&&accepted.Get<long>("b")==128&&accepted.Get<long>("a")==123,"程序色彩未恰一次写真实owner");
    Show(new TextBlock{Text="暂时卸载"});Require(!display.IsDropDownOpen&&!palette.IsDropDownOpen,"卸载未关闭原父子层");
    var writes=draft.Writes;Native.Input.HikPcUI.ListView.ColorPickerControlView.SelectColorCommand.Execute(new SolidColorBrush(Colors.Red),picker);Pump();
    Require(draft.Writes==writes,"卸载旧色板命令写入草稿");Show(settings);OpenStyle(settings);Pump();
    Require(picker.CurrentColor.Color==Color.FromArgb(123,32,64,128),"重开显示未接纳的颜色");palette.IsDropDownOpen=true;Pump();Require(palette.IsDropDownOpen,"临时卸载永久释放原色板");
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
   Test("图像模式原EnumerationG与同根ROI切换保留对象并拒绝隐藏选择",()=>
   {
    using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample();Show(sample);
    var local=sample.LocalItems.ToArray();var frame=sample.Frame;
    Require(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"area.mode",new ConstantSetting(1L)}}),"图像模式切换拒绝");Pump();
    var image=sample.Fields.Fields.Single(field=>field.FieldId=="mask.image");var combo=(ComboBox)image.InputElement!;
    Require(combo.IsVisible&&sample.Session.Items.Count==0&&sample.Scene.ParameterHost.Visibility==Visibility.Collapsed,"原图像分支未显示或几何未隐藏");
    combo.SelectedIndex=1;Pump();Require(image.Setting is ReferenceSetting,"原图像选择未接纳");
    var selected=((ReferenceSetting)image.Setting).Reference;combo=(ComboBox)image.InputElement!;
    Require(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"area.mode",new ConstantSetting(0L)}}),"图形模式切回失败");Pump();
    var revision=sample.Revision;combo.SelectedIndex=0;Pump();
    Require(!combo.IsVisible&&image.Setting is ReferenceSetting retained&&retained.Reference.NodeId==selected.NodeId&&retained.Reference.PortId==selected.PortId&&sample.Revision==revision,"隐藏旧图像事件写入字段");
    Require(sample.Session.Items.Count==2&&sample.LocalItems.SequenceEqual(local)&&sample.Frame==frame,"模式切换破坏原对象或帧");
    CaptureInline(sample,"native-inheritance-image-mode-restored.png");
   });
   Test("图像模式父条件与自身条件相与且附件和分组同步显隐",()=>
   {
    var values=new[]{
     (new Port("mode",DataType.Integer),new FieldPresentation("mode","模式","模式",0,FieldEditorKind.Scalar),(ParameterSetting)new ConstantSetting(0L)),
     (new Port("enabled",DataType.Boolean),new FieldPresentation("enabled","启用","模式",1,FieldEditorKind.Scalar),(ParameterSetting)new ConstantSetting(true)),
     (new Port("value",DataType.Integer),new FieldPresentation("value","参数","图形",2,FieldEditorKind.Scalar){Layout=new("模式","图形",VisibleWhen:new("enabled",true))},(ParameterSetting)new ConstantSetting(10L))};
    using var fields=new DeclaredFieldsView(values,_=>{},parentConditions:new Dictionary<string,IReadOnlyList<FieldCondition>>{["value"]=new[]{new FieldCondition("mode",0L)}});
    var attachment=new TextBlock{Text="图形附件"};fields.AddAfterField("value",attachment);Show(fields);
    Require(attachment.IsVisible,"初始附件隐藏");
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"mode",new ConstantSetting(1L)}}),"父模式拒绝");Pump();
    Require(!attachment.IsVisible,"父条件未传给附件");
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"enabled",new ConstantSetting(false)},{"mode",new ConstantSetting(0L)}}),"分支恢复拒绝");Pump();
    Require(!attachment.IsVisible,"自身条件被父条件覆盖");
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"enabled",new ConstantSetting(true)}}),"启用拒绝");Pump();Require(attachment.IsVisible,"条件恢复后附件未显示");
   });
   Test("图像模式隐藏先暂停引用失焦写入且显式离线捕获仍有效",()=>
   {
    var writes=0;var source=new DeclaredReferenceCandidate("文本序列","texts",null,new Port("values",TypeRef.SequenceOf(DataType.Text)),Array.Empty<string>());
    using var fields=new DeclaredFieldsView(new[]{
     (new Port("mode",DataType.Integer),new FieldPresentation("mode","模式","组合",0,FieldEditorKind.Scalar),(ParameterSetting)new ConstantSetting(0L)),
     (new Port("text",DataType.Text,Required:false,EditModes:FieldEditModes.Reference),new FieldPresentation("text","来源","组合",1,FieldEditorKind.Scalar),(ParameterSetting)new ReferenceSetting(source.Reference(new BindingSelection.AtConstant(0))))},_=>writes++,
     new ReferenceFieldOptions(_=>new(new[]{source},Array.Empty<DeclaredReferenceCandidate>()),EditableIndex:true),parentConditions:new Dictionary<string,IReadOnlyList<FieldCondition>>{["text"]=new[]{new FieldCondition("mode",0L)}});
    Show(fields);var field=fields.Fields.Single(item=>item.FieldId=="text");
    var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(field.View).Single(item=>item.Name=="rt");
    popup.ApplyTemplate();popup.Focus();popup.IsDropDownOpen=true;Pump();var session=popup.DataContext;var type=session.GetType();
    var current=(string)type.GetProperty("CurValueA")!.GetValue(session)!;var pending=current[..current.LastIndexOf('[')]+"[1]";
    type.GetMethod("ObserveText")!.Invoke(session,new object[]{pending});
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"mode",new ConstantSetting(1L)}}),"隐藏拒绝");Pump();
    var before=writes;type.GetProperty("CurValueA")!.SetValue(session,pending);Pump();
    Require(writes==before&&!popup.IsDropDownOpen&&field.Setting is ReferenceSetting{Reference.Selection:BindingSelection.AtConstant{Index:0}},"隐藏失焦写入或弹层未关闭");
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"mode",new ConstantSetting(0L)}}),"恢复拒绝");Pump();
    Require(field.Setting is ReferenceSetting{Reference.Selection:BindingSelection.AtConstant{Index:0}}&&(string)type.GetProperty("CurValueA")!.GetValue(session)! ==pending,"恢复时提交或丢失索引原文");
    Show(new TextBlock{Text="离线捕获"});
    Require(field.TryCommitPending()&&field.Setting is ReferenceSetting{Reference.Selection:BindingSelection.AtConstant{Index:1}},"显式离线捕获被UI暂停错误阻断");
   });
   Test("图像模式配置前置拒绝未知字段和错型且来源函数不执行",()=>
   {
    var calls=0;var declaration=new[]{(new Port("value",DataType.Integer),new FieldPresentation("value","数值","参数",0,FieldEditorKind.Scalar),(ParameterSetting)new ConstantSetting(1L))};
    foreach(var id in new[]{"missing","value"})
    {
     var rejected=false;try{using var fields=new DeclaredFieldsView(declaration,_=>{},imageSources:new Dictionary<string,Func<IReadOnlyList<DeclaredReferenceCandidate>>>{[id]=()=>{calls++;return Array.Empty<DeclaredReferenceCandidate>();}});}catch(ArgumentException){rejected=true;}
     Require(rejected&&calls==0,"非法图像字段配置已执行来源函数");
    }
    var failed=false;try{using var fields=new DeclaredFieldsView(declaration,_=>{},parentConditions:new Dictionary<string,IReadOnlyList<FieldCondition>>{["value"]=new[]{new FieldCondition("missing",0L)}});}catch(ArgumentException){failed=true;}Require(failed,"未知父条件被静默接受");
   });
   Test("四方式原Radio进入未完成分支，五个ButtonSelecter同帧完整后才投影",()=>
   {
    using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample{Width=1020,Height=1100};Show(sample);
    var local=sample.LocalItems.ToArray();var fields=sample.Fields;
    var creation=fields.Fields.Single(field=>field.FieldId=="detect.mode");
    Visuals<RadioButton>(creation.View).Single(radio=>Equals(radio.Content,"继承")).IsChecked=true;Pump();
    var way=fields.Fields.Single(field=>field.FieldId=="detect.inherit-way");var radios=Visuals<RadioButton>(way.View).ToArray();
    Require(radios.Length==5&&radios.Count(radio=>radio.IsVisible)==4,"原四方式没有五槽填四项/空末槽");
    radios[1].SetCurrentValue(ToggleButton.IsCheckedProperty,true);Pump();
    Require(((ConstantSetting)way.Setting).Value.Equals(1L)&&!sample.TryCapture(out _,out var missing)&&missing.Contains("detect.rect.x")&&!sample.Session.Items.Any(item=>item.Group=="detect"),"原Checked未进入可编辑未完成分支");
    var ids=new[]{"detect.rect.x","detect.rect.y","detect.rect.width","detect.rect.height","detect.rect.angle"};
    foreach(var id in ids)
    {
     var field=fields.Fields.Single(item=>item.FieldId==id);
     Require(field.View.IsVisible&&Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(field.View).Any(popup=>popup.Name=="rt")&&!Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeFloat>(field.View).Any(),"参数继承槽误用数值条或未显示："+id);
     if(id==ids[0])
     {
      var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(field.View).Single(item=>item.Name=="rt");popup.ApplyTemplate();popup.Focus();popup.IsDropDownOpen=true;Pump();
      var content=(Popup)popup.Template.FindName("PART_Popup",popup);var tree=Visuals<Native.Composition.Apps.XmlUI.ParamTab.SubTreeView>(content.Child).Single();var actualTree=(TreeView)tree.FindName("treeView");
      var group=(TreeViewItem)actualTree.ItemContainerGenerator.ContainerFromIndex(1);group.IsExpanded=true;group.UpdateLayout();Pump();
      var leaf=(TreeViewItem)group.ItemContainerGenerator.ContainerFromIndex(0);leaf.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});Pump();
      Require(field.Setting is ReferenceSetting selected&&selected.Reference.NodeId==sample.ComponentReference(id).NodeId,"原分量来源树双击未接纳");
     }
     else Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{id,new ReferenceSetting(sample.ComponentReference(id))}}),"分量来源拒绝："+field.Error);
     if(id!=ids[^1])Require(!sample.Session.Items.Any(item=>item.Group=="detect")&&!sample.TryCapture(out _,out _),"未齐分量产生半组几何");
    }
    Require(sample.TryCapture(out var captured,out _)&&captured!.Stamp==sample.Session.Stamp,"完整分量不能同修订捕获");
    var expected=local.Single(item=>item.Group=="detect").Geometry;var actual=captured!.Items.Single(item=>item.Group=="detect").Geometry;
    var expectedCenter=RoiGeometryEditing.Center(expected);
    Require(RoiGeometryEditing.Center(actual)==new Point2((double)(float)expectedCenter.X,(double)(float)expectedCenter.Y)&&new[]{"width","height","angle-degrees"}.All(key=>actual.Get<double>(key)==(double)(float)expected.Get<double>(key)),"矩形五分量输出字段/Float32映射错误");
    var inherited=sample.Session.Items.Single(item=>item.Group=="detect");var idBefore=inherited.Id;
    Require(!inherited.Enabled&&inherited is NativeBoxRoiProjection&&sample.LocalItems.SequenceEqual(local),"参数继承错误可编辑或污染本地");
    var revision=sample.Revision;
    Require(!fields.TrySetFields(new Dictionary<string,ParameterSetting>{{ids[0],new ReferenceSetting(sample.ComponentReference(ids[0],true))}})&&sample.Revision==revision&&sample.Session.Items.Single(item=>item.Group=="detect").Id==idBefore,"跨帧分量被接纳或破坏稳定ID");
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{ids[0],new ReferenceSetting(sample.ComponentReference(ids[0]))}}),"同值修复失败");
    radios[0].IsChecked=true;Pump();Require(!sample.Session.Items.Any(item=>item.Group=="detect")&&!sample.TryCapture(out _,out _),"空整体来源回落到分量或本地");
    radios[1].IsChecked=true;Pump();Require(sample.Session.Items.Single(item=>item.Group=="detect").Id==idBefore,"切回参数分支丢稳定身份");
    CaptureInline(sample,"native-four-way-rectangle-components.png");
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"detect.mode",new ConstantSetting(0L)}})&&sample.Session.CurROIData?.Id==local.Single(item=>item.Group=="detect").Id,"返回本地未恢复稳定对象选中");
   });
   Test("四方式圆环整体及六分量、未选择值与清除恢复保持分支状态",()=>
   {
    using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample{Width=1020,Height=1100};Show(sample);var fields=sample.Fields;
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"detect.mode",new ConstantSetting(1L)}}),"继承模式不可配置");
    Pump();var way=fields.Fields.Single(field=>field.FieldId=="detect.inherit-way");var areaRadio=Visuals<RadioButton>(way.View).ElementAt(2);
    Require(areaRadio.IsLoaded&&areaRadio.IsVisible,"圆环原项尚未完成布局加载");areaRadio.SetCurrentValue(ToggleButton.IsCheckedProperty,true);Pump();
    Require(((ConstantSetting)way.Setting).Value.Equals(2L),"原圆环整体Checked未接纳");
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"detect.annulus-source",new ReferenceSetting(sample.DetectionAnnulusReference)}})&&sample.TryCapture(out _,out _),"整体圆环不能捕获");
    var whole=sample.Session.Items.Single(item=>item.Group=="detect");Require(whole is NativeInheritedAnnulusProjection,"整体部分圆环未走原只读显示链");
    Require(sample.TryCapture(out var wholeSnapshot,out _),"整体圆环捕获失败");var expected=wholeSnapshot!.Items.Single(item=>item.Group=="detect").Geometry;
    Visuals<RadioButton>(way.View).ElementAt(3).SetCurrentValue(ToggleButton.IsCheckedProperty,true);Pump();Require(((ConstantSetting)way.Setting).Value.Equals(3L)&&!sample.TryCapture(out _,out _),"原圆环参数Checked未进入未完成分支");
    var changes=new Dictionary<string,ParameterSetting>();
    var ids=new[]{"detect.annulus.x","detect.annulus.y","detect.annulus.inner","detect.annulus.outer","detect.annulus.start","detect.annulus.sweep"};
    foreach(var id in ids)changes[id]=new ReferenceSetting(sample.ComponentReference(id));
    var before=sample.Revision;Require(fields.TrySetFields(changes)&&sample.Revision==before+1&&sample.TryCapture(out _,out _),"六分量未一次完整接纳");
    var components=sample.Session.Items.Single(item=>item.Group=="detect");Require(sample.TryCapture(out var snapshot,out _),"完整圆环捕获失败");var geometry=snapshot!.Items.Single(item=>item.Group=="detect").Geometry;
    Require(!components.Enabled&&geometry.Get<string>("kind")=="annulus"&&components.Id!=whole.Id,"两方式混用投影身份");
    var expectedCenter=RoiGeometryEditing.Center(expected);var start=(double)(float)expected.Get<double>("start-radians");var sweep=(double)(float)(expected.Get<double>("end-radians")-expected.Get<double>("start-radians"));
    Require(RoiGeometryEditing.Center(geometry)==new Point2((double)(float)expectedCenter.X,(double)(float)expectedCenter.Y)&&new[]{"inner-radius","outer-radius"}.All(key=>geometry.Get<double>(key)==(double)(float)expected.Get<double>(key))&&geometry.Get<double>("start-radians")==start&&geometry.Get<double>("end-radians")==start+sweep&&Math.Abs(start)>1,"圆环六分量或Float32非零起角+sweep映射错误");
    before=sample.Revision;
    Require(!fields.TrySetFields(new Dictionary<string,ParameterSetting>{{ids[2],new ReferenceSetting(sample.ComponentReference(ids[3]))},{ids[3],new ReferenceSetting(sample.ComponentReference(ids[2]))}})&&sample.Revision==before,"非法完整半径组未原子拒绝");
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{ids[2],new ReferenceSetting(sample.ComponentReference(ids[2]))},{ids[3],new ReferenceSetting(sample.ComponentReference(ids[3]))}}),"合法半径不能修复");
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"detect.inherit-way",new ConstantSetting(-1L)}})&&!sample.TryCapture(out _,out var error)&&error.Contains("继承方式")&&!sample.Session.Items.Any(item=>item.Group=="detect"),"原未选择值未作为中间态或错误捕获");
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"detect.inherit-way",new ConstantSetting(3L)},{ids[0],new UnsetSetting()}})&&!sample.TryCapture(out _,out _),"清除分量未成为未完成态");
    Require(fields.TrySetFields(new Dictionary<string,ParameterSetting>{{ids[0],new ReferenceSetting(sample.ComponentReference(ids[0]))}})&&sample.Session.Items.Single(item=>item.Group=="detect").Id==components.Id,"分量恢复丢稳定ID");
    CaptureInline(sample,"native-four-way-annulus-components.png");
    foreach(var radio in Visuals<RadioButton>(way.View).Where(item=>item.Content is not null))
    {
     Require(radio.IsEnabled,"原四选项意外禁用");
     var texts=Visuals<TextBlock>(radio).Where(item=>!string.IsNullOrEmpty(item.Text)).ToArray();
     Require(texts.Length>0&&radio.Foreground is SolidColorBrush originalForeground&&originalForeground.Color==Color.FromRgb(102,102,102),"原四选文字缺失或前景错误");
     foreach(var text in texts)
      Require(text.Foreground is SolidColorBrush rendered&&radio.Foreground is SolidColorBrush original&&rendered.Color==original.Color,$"宿主暗色文字污染原四选重绘：{text.Text}, brush={text.Foreground}, source={DependencyPropertyHelper.GetValueSource(text,TextBlock.ForegroundProperty).BaseValueSource}, local={text.ReadLocalValue(TextBlock.ForegroundProperty)}, style={DependencyPropertyHelper.GetValueSource(text,FrameworkElement.StyleProperty).BaseValueSource}, styleLocal={text.ReadLocalValue(FrameworkElement.StyleProperty)}, template={text.TemplatedParent?.GetType().Name}, parent={VisualTreeHelper.GetParent(text)?.GetType().Name}");
    }
   });
   Test("四方式接纳后观察异常仍安装显隐并能继续当前根",()=>
   {
    using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample();Show(sample);var local=sample.Session.CurROIData!;
    Action broken=()=>throw new InvalidOperationException("来源样例观察异常");sample.Session.Changed+=broken;var threw=false;
    try{sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"area.mode",new ConstantSetting(1L)}});}catch(InvalidOperationException error){threw=error.Message.Contains("来源样例观察异常");}
    finally{sample.Session.Changed-=broken;}
    Require(threw&&sample.Revision==1&&sample.Session.Stamp.Revision==1&&sample.Session.Items.Count==0&&sample.Scene.ParameterHost.Visibility==Visibility.Collapsed,"观察异常导致已接纳模式与显隐分裂");
    Require(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"area.mode",new ConstantSetting(0L)}})&&sample.Session.CurROIData?.Id==local.Id&&sample.Scene.ParameterHost.Visibility==Visibility.Visible,"观察异常后不能恢复本地选中");
   });
   Test("订阅双通道快照冻结成员存储且来源索引节点互不混用",()=>
   {
    var sequence=new DeclaredReferenceCandidate("浮点序列","producer",null,new Port("values",TypeRef.SequenceOf(DataType.Number),Storage:NumericStorage.Float32),Array.Empty<string>());
    var index=new DeclaredReferenceCandidate("整数索引","producer",null,new Port("index",DataType.Integer,Storage:NumericStorage.Int32),Array.Empty<string>());
    var sources=new List<DeclaredReferenceCandidate>{sequence};var indices=new List<DeclaredReferenceCandidate>{index};var snapshot=new ReferenceCandidates(sources,indices);sources.Clear();indices.Clear();
    Require(snapshot.Sources.Count==1&&snapshot.Indices.Count==1&&snapshot.Sources[0].SelectedStorage==NumericStorage.Float32,"快照未冻结来源或存储");
    var record=TypeRef.RecordOf("component-storage",new[]{new Port("value",DataType.Number,Storage:NumericStorage.Float32)});var path=new[]{"value"};
    var member=new DeclaredReferenceCandidate("成员","record",null,new Port("record",TypeRef.SequenceOf(record)),path);var frozen=new ReferenceCandidates(new[]{member},Array.Empty<DeclaredReferenceCandidate>());path[0]="missing";
    Require(frozen.Sources[0].SelectedType==TypeRef.SequenceOf(DataType.Number)&&frozen.Sources[0].SelectedStorage==NumericStorage.Float32&&frozen.Sources[0].Reference(BindingSelection.All).Storage is null,"成员存储未投影或污染根引用");
    var writes=0;using var field=new DeclaredFieldEditor(new("value",DataType.Number,EditModes:FieldEditModes.Reference),"值",new ReferenceSetting(sequence.Reference(BindingSelection.All)),_=>writes++,new ReferenceFieldOptions(_=>snapshot,EditableIndex:true));Show(field);
    var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(field).Single(item=>item.Name=="rt");var session=popup.DataContext;var type=session.GetType();((ICommand)type.GetProperty("DropDownCommand")!.GetValue(session)!).Execute(null);
    object Leaf(string property)=>((System.Collections.IEnumerable)type.GetProperty(property)!.GetValue(session)!).Cast<object>().SelectMany(root=>((System.Collections.IEnumerable)root.GetType().GetProperty("EnumEntrysA")!.GetValue(root)!).Cast<object>()).Single();
    var sourceNode=Leaf("SourceNodes");var indexNode=Leaf("IndexNodes");
    ((ICommand)type.GetProperty("ClickCommand")!.GetValue(session)!).Execute(indexNode);((ICommand)type.GetProperty("IndexClickCommand")!.GetValue(session)!).Execute(sourceNode);Require(writes==0,"跨通道节点被接纳");
    ((ICommand)type.GetProperty("IndexClickCommand")!.GetValue(session)!).Execute(indexNode);Require(writes==1&&field.Setting is ReferenceSetting{Reference.Selection:BindingSelection.AtField},"独立索引节点未接纳");
   });
   Test("订阅双通道刷新重入和失败不复活旧节点且accepted保留",()=>
   {
    var source=new DeclaredReferenceCandidate("来源","producer",null,new Port("value",DataType.Number),Array.Empty<string>());var fail=false;var reenter=false;var calls=0;ICommand? refresh=null;
    var writes=0;using var field=new DeclaredFieldEditor(new("value",DataType.Number,Required:false,EditModes:FieldEditModes.Reference),"值",new ReferenceSetting(source.Reference(BindingSelection.All)),_=>writes++,new ReferenceFieldOptions(_=>{calls++;if(reenter)refresh!.Execute(null);if(fail)throw new InvalidOperationException("目录失败");return new(new[]{source},Array.Empty<DeclaredReferenceCandidate>());}));Show(field);
    var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(field).Single(item=>item.Name=="rt");var session=popup.DataContext;var type=session.GetType();refresh=(ICommand)type.GetProperty("DropDownCommand")!.GetValue(session)!;refresh.Execute(null);
    var roots=((System.Collections.IEnumerable)type.GetProperty("SourceNodes")!.GetValue(session)!).Cast<object>().ToArray();var clear=roots[0];fail=true;refresh.Execute(null);
    ((ICommand)type.GetProperty("ClickCommand")!.GetValue(session)!).Execute(clear);Require(writes==0&&field.Setting is ReferenceSetting&&field.HasInvalidText,"刷新失败旧清除叶仍写入");
    fail=false;reenter=true;var prior=calls;refresh.Execute(null);Require(calls==prior+1&&!field.HasInvalidText&&writes==0,"provider递归刷新或恢复擅自写入");
   });
   Test("订阅原ButtonSelecter同槽Float32常量Enter单写失焦并能切回来源",()=>
   {
    var source=new DeclaredReferenceCandidate("来源","producer",null,new Port("value",DataType.Number,Storage:NumericStorage.Float32),Array.Empty<string>());var writes=0;
    using var fields=new DeclaredFieldsView(new[]{(new Port("value",DataType.Number,Required:false,Storage:NumericStorage.Float32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),new FieldPresentation("value","值","参数",0,FieldEditorKind.Scalar),(ParameterSetting)new UnsetSetting())},_=>writes++,new ReferenceFieldOptions(_=>new(new[]{source},Array.Empty<DeclaredReferenceCandidate>())),inlineConstants:new HashSet<string>{"value"});Show(fields);var field=fields.Fields.Single();
    var text=Visuals<Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox>(field.View).Single();Require(!text.IsReadOnly&&!Visuals<Native.Numeric.HikPcUI.ListView.SliderArrangeFloat>(field.View).Any(),"同槽常量未使用原可编辑文本");
    text.Focus();text.Text="0.1";SendKey(text,Key.Enter,true);Pump();Require(writes==1&&field.Setting is ConstantSetting{Value:double value}&&value==(double).1f&&!text.IsKeyboardFocusWithin,"Float32常量未单次接纳/失焦");
    text.Focus();text.Text="NaN";SendKey(text,Key.Enter,true);Require(writes==1&&text.Text=="NaN"&&text.IsKeyboardFocusWithin&&field.HasInvalidText,"非法常量丢原文或提交");
    text.Text="0.2";SendKey(text,Key.Enter,true);Require(writes==2&&!field.HasInvalidText,"纠正常量失败");
    var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(field.View).Single(item=>item.Name=="rt");var session=popup.DataContext;var type=session.GetType();((ICommand)type.GetProperty("DropDownCommand")!.GetValue(session)!).Execute(null);
    var root=((System.Collections.IEnumerable)type.GetProperty("SourceNodes")!.GetValue(session)!).Cast<object>().Last();var leaf=((System.Collections.IEnumerable)root.GetType().GetProperty("EnumEntrysA")!.GetValue(root)!).Cast<object>().Single();((ICommand)type.GetProperty("ClickCommand")!.GetValue(session)!).Execute(leaf);Require(writes==3&&field.Setting is ReferenceSetting,"常量态不能重新选择来源");
    text.Focus();text.Text="0.3";SendKey(text,Key.Enter,true);Require(writes==4&&field.Setting is ConstantSetting,"引用不能改为常量");text.Focus();text.Text="";Pump();Require(writes==5&&field.Setting is UnsetSetting,"常量清空未接纳Unset");
   });
   Test("订阅常量拒绝及接纳后观察异常不倒退accepted原文",()=>
   {
    var reject=true;var writes=0;using var fields=new DeclaredFieldsView(new[]{(new Port("value",DataType.Number,Required:false,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),new FieldPresentation("value","值","参数",0,FieldEditorKind.Scalar),(ParameterSetting)new ConstantSetting(1d))},_=>writes++,ReferenceFieldOptions.Empty,validateComposition:_=>reject?"拒绝常量":null,inlineConstants:new HashSet<string>{"value"});Show(fields);var field=fields.Fields.Single();var text=Visuals<Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox>(field.View).Single();text.Focus();text.Text="1.23456789";SendKey(text,Key.Enter,true);Require(writes==0&&text.Text=="1.23456789"&&field.Setting is ConstantSetting{Value:1d},"拒绝丢原文或忽略宿主");
    reject=false;Action broken=()=>throw new InvalidOperationException("常量观察异常");field.Changed+=broken;var threw=false;
    try{field.TryCommitPending();}catch(InvalidOperationException error){threw=error.Message.Contains("常量观察异常");}finally{field.Changed-=broken;}
    Pump();Require(threw&&writes==1&&field.Setting is ConstantSetting{Value:1.23456789d}&&text.Text=="1.23456789"&&!field.HasInvalidText,"接纳后异常恢复了旧原文或接受模型");
    Require(field.TryCommitPending()&&writes==1,"同值捕获重复写入");
   });
   Test("订阅双通道安装后观察异常不污染目录且新叶仍有效",()=>
   {
    var source=new DeclaredReferenceCandidate("来源","producer",null,new Port("value",TypeRef.SequenceOf(DataType.Number)),Array.Empty<string>());var index=new DeclaredReferenceCandidate("索引","index",null,new Port("index",DataType.Integer),Array.Empty<string>());var writes=0;
    using var field=new DeclaredFieldEditor(new("value",DataType.Number,EditModes:FieldEditModes.Reference),"值",new ReferenceSetting(source.Reference(BindingSelection.All)),_=>writes++,new ReferenceFieldOptions(_=>new(new[]{source},new[]{index}),EditableIndex:true));Show(field);
    var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(field).Single(item=>item.Name=="rt");var session=popup.DataContext;var type=session.GetType();var refresh=(ICommand)type.GetProperty("DropDownCommand")!.GetValue(session)!;refresh.Execute(null);
    object Leaf(string property)=>((System.Collections.IEnumerable)type.GetProperty(property)!.GetValue(session)!).Cast<object>().SelectMany(root=>((System.Collections.IEnumerable)root.GetType().GetProperty("EnumEntrysA")!.GetValue(root)!).Cast<object>()).Single();
    var oldSource=Leaf("SourceNodes");var oldIndex=Leaf("IndexNodes");var seen=new HashSet<string>();var once=true;
    System.ComponentModel.PropertyChangedEventHandler broken=(_,args)=>{seen.Add(args.PropertyName!);if(once&&args.PropertyName=="SelectedSourceNode"){once=false;throw new InvalidOperationException("刷新观察异常");}};
    var notifications=(System.ComponentModel.INotifyPropertyChanged)session;notifications.PropertyChanged+=broken;var threw=false;
    try{refresh.Execute(null);}catch(InvalidOperationException error){threw=error.Message.Contains("刷新观察异常");}finally{notifications.PropertyChanged-=broken;}
    Require(threw&&!field.HasInvalidText&&writes==0&&seen.Contains("IndexNodes")&&seen.Contains("CurValueA"),"观察异常误报目录失败或中断后续通知");
    ((ICommand)type.GetProperty("ClickCommand")!.GetValue(session)!).Execute(oldSource);((ICommand)type.GetProperty("IndexClickCommand")!.GetValue(session)!).Execute(oldIndex);Require(writes==0,"旧节点在刷新后复活");
    ((ICommand)type.GetProperty("IndexClickCommand")!.GetValue(session)!).Execute(Leaf("IndexNodes"));Require(writes==1&&field.Setting is ReferenceSetting{Reference.Selection:BindingSelection.AtField},"已安装新索引因通知异常失效");
   });
   Test("订阅同槽声明初值及程序写与键盘共用范围精度",()=>
   {
    var port=new Port("value",DataType.Number,Required:false,Storage:NumericStorage.Float32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
    FieldPresentation Presentation(int? precision,double? maximum=1)=>new("value","值","参数",0,FieldEditorKind.Scalar){Layout=new("参数","参数",Minimum:0,Maximum:maximum,DecimalPlaces:precision)};
    DeclaredFieldsView Create(FieldPresentation presentation,ParameterSetting initial,Action<IReadOnlyDictionary<string,ParameterSetting>> writer)=>new(new[]{(port,presentation,initial)},writer,ReferenceFieldOptions.Empty,inlineConstants:new HashSet<string>{"value"});
    foreach(var invalid in new[]{new ConstantSetting(2d),new ConstantSetting((double).123f)})
    {var rejected=false;try{using var bad=Create(Presentation(2),invalid,_=>throw new Exception("非法初值不得写入"));}catch(ArgumentException){rejected=true;}Require(rejected,"非法范围或精度初值未拒绝");}
    var writes=0;using(var fields=Create(Presentation(2),new ConstantSetting((double).1f),_=>writes++))
    {Show(fields);var field=fields.Fields.Single();Require(!field.TrySetConstant((double).123f)&&!field.TrySetConstant(2d)&&writes==0,"程序写绕过声明");var text=Visuals<Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox>(field.View).Single();text.Focus();text.Text="0.123";SendKey(text,Key.Enter,true);Require(field.HasInvalidText&&text.Text=="0.123"&&writes==0,"键盘绕过精度或丢原文");text.Text="0.2";SendKey(text,Key.Enter,true);Require(writes==1&&!field.HasInvalidText,"规范Float32小数被尾数误拒");}
    using(var fields=Create(Presentation(null),new UnsetSetting(),_=>writes++))
    {Show(fields);var field=fields.Fields.Single();Require(field.TrySetConstant((double)1e-20f)&&field.TrySetConstant((double)float.Epsilon),"不限固定小数位错误截断合法微小Float32");}
    var invalidLayout=false;try{using var bad=Create(Presentation(16),new UnsetSetting(),_=>{});}catch(ArgumentException){invalidLayout=true;}Require(invalidLayout,"空初值绕过非法精度声明");
    var unconfigured=false;try{using var bad=new DeclaredFieldEditor(port,"值",new ConstantSetting((double).1f),_=>{},presentation:Presentation(null));}catch(ArgumentException){unconfigured=true;}Require(unconfigured,"普通独立数值控件静默回退不限位数声明");
   });
   Test("分量序列五槽同快照固定动态索引及原文本双向捕获",()=>
   {
    var binding=new ComponentBindingHost();using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample(new(binding.Candidates,binding.Resolve,()=>binding.Alive));binding.Frame=sample.Frame;Show(sample);
    var ids=new[]{"detect.rect.x","detect.rect.y","detect.rect.width","detect.rect.height","detect.rect.angle"};
    var proposal=new Dictionary<string,ParameterSetting>{{"detect.mode",new ConstantSetting(1L)},{"detect.inherit-way",new ConstantSetting(1L)}};
    for(var i=0;i<ids.Length;i++)proposal[ids[i]]=new ReferenceSetting(binding.Reference(new BindingSelection.AtConstant(i),i==0));
    Require(sample.Fields.TrySetFields(proposal)&&sample.Revision==1&&binding.Calls==1&&binding.Resolutions==5,"五槽未一次解析接纳");var item=sample.Session.Items.Single(row=>row.Group=="detect");var stable=item.Id;
    Require(RoiGeometryEditing.Center(ComponentGeometry(sample)).X==200&&ComponentGeometry(sample).Get<double>("width")==100,"正式序列及记录成员解析未驱动原ROI");
    var calls=binding.Calls;Require(sample.TryCapture(out var captured,out _)&&binding.Calls==calls&&captured!.Stamp==sample.Session.Stamp,"普通Read/捕获重复创建分量运行快照");
    var width=sample.Fields.Fields.Single(field=>field.FieldId==ids[2]);var text=Visuals<Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox>(width.View).Single();text.Focus();Pump();var raw=text.Text;var bracket=raw.LastIndexOf('[');Require(bracket>=0&&raw.EndsWith("]",StringComparison.Ordinal),"实际来源文本无索引槽");text.Text=raw[..(bracket+1)]+"3]";SendKey(text,Key.Enter,true);Pump();
    Require(sample.Revision==2&&binding.Calls==calls+1&&ComponentGeometry(sample).Get<double>("width")==80&&!text.IsKeyboardFocusWithin,"原文本固定索引未驱动同批几何或Enter未失焦");
    var popup=Visuals<Native.Input.Apps.PcUI.Controls.PopupButton>(width.View).Single(view=>view.Name=="rt");var session=popup.DataContext;var type=session.GetType();((ICommand)type.GetProperty("DropDownCommand")!.GetValue(session)!).Execute(null);
    object[] Leaves(string name)=>((System.Collections.IEnumerable)type.GetProperty(name)!.GetValue(session)!).Cast<object>().SelectMany(root=>((System.Collections.IEnumerable)root.GetType().GetProperty("EnumEntrysA")!.GetValue(root)!).Cast<object>()).ToArray();
    Require(Leaves("SourceNodes").Length==2&&Leaves("IndexNodes").Length==1,"VM来源Float32/索引Int32过滤未落实");
    ((ICommand)type.GetProperty("IndexClickCommand")!.GetValue(session)!).Execute(Leaves("IndexNodes").Single());Pump();
    Require(sample.Revision==3&&sample.Session.Items.Single(row=>row.Group=="detect").Id==stable&&ComponentGeometry(sample).Get<double>("width")==100&&width.Setting is ReferenceSetting{Reference.Selection:BindingSelection.AtField},"动态索引未沿原入口驱动同一稳定对象");
    CaptureInline(sample,"native-component-sequence-binding.png");
   });
   Test("分量序列空与多项不取首且未运行可编辑并显式刷新修订",()=>
   {
    var binding=new ComponentBindingHost{Unavailable=true};using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample(new(binding.Candidates,binding.Resolve,()=>binding.Alive));binding.Frame=sample.Frame;Show(sample);var local=sample.LocalItems.ToArray();
    var ids=new[]{"detect.rect.x","detect.rect.y","detect.rect.width","detect.rect.height","detect.rect.angle"};var proposal=new Dictionary<string,ParameterSetting>{{"detect.mode",new ConstantSetting(1L)},{"detect.inherit-way",new ConstantSetting(1L)}};
    foreach(var id in ids)proposal[id]=new ReferenceSetting(binding.Reference(BindingSelection.All));
    Require(sample.Fields.TrySetFields(proposal)&&!sample.TryCapture(out _,out _)&&!sample.Session.Items.Any(row=>row.Group=="detect")&&sample.LocalItems.SequenceEqual(local),"未运行来源无法配置或回落了本地ROI");
    binding.Unavailable=false;binding.Values=Array.Empty<double>();var revision=sample.Revision;Require(!sample.RefreshComponentReferences()&&sample.Revision==revision+1&&!sample.TryCapture(out _,out _),"空[]取了默认项或未增展示修订");
    binding.Values=new[]{20d,30d};Require(!sample.RefreshComponentReferences()&&!sample.Session.Items.Any(row=>row.Group=="detect"),"多项[]静默取首");
    proposal.Clear();var values=new[]{200d,150d,100d,80d,0d};for(var i=1;i<ids.Length;i++)proposal[ids[i]]=new ConstantSetting(values[i]);binding.Values=new[]{200d};
    Require(sample.Fields.TrySetFields(proposal)&&sample.TryCapture(out _,out _)&&RoiGeometryEditing.Center(ComponentGeometry(sample)).X==200,"恰一项[]未成功消费");
    var stable=sample.Session.Items.Single(row=>row.Group=="detect").Id;revision=sample.Revision;binding.Values=new[]{210d};Require(sample.RefreshComponentReferences()&&sample.Revision==revision+1&&sample.Session.Stamp.Revision==sample.Revision&&sample.Session.Items.Single(row=>row.Group=="detect").Id==stable,"新结果更新未沿owner新修订或丢稳定ID");
   });
   Test("分量序列六槽身份错帧旧运行及解析重入均不安装旧值",()=>
   {
    var binding=new ComponentBindingHost{Values=new[]{200d,150d,40d,80d,2d,1d}};using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample(new(binding.Candidates,binding.Resolve,()=>binding.Alive));binding.Frame=sample.Frame;Show(sample);
    var ids=new[]{"detect.annulus.x","detect.annulus.y","detect.annulus.inner","detect.annulus.outer","detect.annulus.start","detect.annulus.sweep"};var proposal=new Dictionary<string,ParameterSetting>{{"detect.mode",new ConstantSetting(1L)},{"detect.inherit-way",new ConstantSetting(3L)}};
    for(var i=0;i<ids.Length;i++)proposal[ids[i]]=new ReferenceSetting(binding.Reference(new BindingSelection.AtConstant(i)));
    Require(sample.Fields.TrySetFields(proposal)&&binding.Calls==1&&binding.Resolutions==6&&sample.TryCapture(out _,out _),"六槽未同快照捕获");var item=sample.Session.Items.Single(row=>row.Group=="detect");Require(ComponentGeometry(sample).Get<double>("end-radians")==3,"圆环分量映射错误");
    var dynamic=new Dictionary<string,ParameterSetting>{{ids[0],new ReferenceSetting(binding.Reference(new BindingSelection.AtField(binding.IndexCandidate.Reference(BindingSelection.All))))}};
    foreach(var fault in new[]{"frame","run","schema","generation"})
    {
     binding.ForeignIndexFrame=fault=="frame";binding.OldIndexRun=fault=="run";binding.BadSchema=fault=="schema";if(fault=="generation")binding.DuringResolve=()=>binding.Epoch++;
     var revision=sample.Revision;Require(!sample.Fields.TrySetFields(dynamic)&&sample.Revision==revision&&sample.Session.Items.Single(row=>row.Group=="detect").Id==item.Id,"错帧/旧运行/schema/重入仍安装旧组合："+fault);
    }
    binding.ForeignIndexFrame=false;binding.OldIndexRun=false;binding.BadSchema=false;binding.IndexValue=-1;
    Require(sample.Fields.TrySetFields(dynamic)&&!sample.TryCapture(out _,out _)&&!sample.Session.Items.Any(row=>row.Group=="detect"),"负动态索引回落零或产生半投影");
    binding.IndexValue=100;Require(!sample.RefreshComponentReferences()&&!sample.TryCapture(out _,out _),"越界动态索引取默认项");binding.IndexValue=0;Require(sample.RefreshComponentReferences()&&sample.TryCapture(out _,out _)&&sample.Session.Items.Single(row=>row.Group=="detect").Id==item.Id,"修复索引未恢复原表达式及ID");
    var revision2=sample.Revision;Require(!sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{ids[2],new ConstantSetting(100d)}})&&sample.Revision==revision2,"非法完整圆环几何被接纳");
   });
   Test("分量序列成功回执后失效及刷新取消重入观察异常保持根事实",()=>
   {
    var binding=new ComponentBindingHost();using var sample=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample(new(binding.Candidates,binding.Resolve,binding.Current));binding.Frame=sample.Frame;Show(sample);
    var ids=new[]{"detect.rect.x","detect.rect.y","detect.rect.width","detect.rect.height","detect.rect.angle"};var proposal=new Dictionary<string,ParameterSetting>{{"detect.mode",new ConstantSetting(1L)},{"detect.inherit-way",new ConstantSetting(1L)}};for(var i=0;i<ids.Length;i++)proposal[ids[i]]=new ReferenceSetting(binding.Reference(new BindingSelection.AtConstant(i)));
    binding.AfterResolve=()=>binding.ExpireOnCurrent=2;Require(!sample.Fields.TrySetFields(proposal)&&sample.Revision==0&&sample.Session.Items.Single(row=>row.Group=="detect").Enabled,"成功回执返回后writer仍安装过期来源");
    Require(sample.Fields.TrySetFields(proposal)&&sample.TryCapture(out _,out _),"旧批拒绝后新批不能修复");binding.Epoch++;
    Require(!sample.TryCapture(out _,out _)&&sample.Fields.Fields.Single(field=>field.FieldId==ids[0]).Setting is ReferenceSetting,"捕获漏查已过期批次或删除了表达式");Require(sample.RefreshComponentReferences()&&sample.TryCapture(out _,out _),"显式源刷新未安装当前回执");
    var revision=sample.Revision;Action broken=()=>throw new ArgumentException("刷新安装后观察异常");sample.Session.Changed+=broken;var threw=false;
    try{sample.RefreshComponentReferences();}catch(ArgumentException error){threw=error.Message.Contains("刷新安装后观察异常");}finally{sample.Session.Changed-=broken;}
    Require(threw&&sample.Revision==revision+1&&sample.Session.Stamp.Revision==sample.Revision&&sample.TryCapture(out _,out _),"接纳后观察异常被吞掉或错误回滚");
    using var local=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample();Show(local);var cancelled=0;local.Session.DrawCancelled+=_=>{cancelled++;Require(!local.RefreshComponentReferences(),"取消回调重入刷新");};
    Require(local.Session.BeginDraw("rectangle") is not null&&local.RefreshComponentReferences()&&cancelled==1,"刷新取消阶段未设门禁或未解除门禁");Require(local.RefreshComponentReferences(),"刷新门禁未在finally解除");
   });
   Console.WriteLine($"O06独立窗口检查：{passed}组通过，{failed}组失败。仅自动交互，不代人工外观验收。");return failed==0?0:1;
  }
  finally{foreach(var window in Application.Current.Windows.Cast<Window>().ToArray())window.Close();Application.Current.Shutdown();}
 }
}
