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
using ColorPicker=SunEyeVision.Plugin.Wpf.Controls.ColorPicker;
using ToggleSwitch=SunEyeVision.Plugin.Wpf.Controls.ToggleSwitch;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
 static Window owner=null!;static StackPanel host=null!;static int passed,failed;
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static void Test(string name,Action run){try{run();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e);}}
 static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
 static double Luminance(Color color){double Linear(byte c){var s=c/255d;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}return .2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);}
 static void Contrast(Brush foreground,Brush background,string label){Require(foreground is SolidColorBrush&&background is SolidColorBrush,label+"不是可核查纯色");var a=Luminance(((SolidColorBrush)foreground).Color);var b=Luminance(((SolidColorBrush)background).Color);var ratio=(Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);Require(ratio>=4.5,label+"文字对比不足："+ratio.ToString("F2",CultureInfo.InvariantCulture));}
 static void LightText(DependencyObject root,Brush background){foreach(var text in Find<TextBlock>(root).Where(t=>t.IsVisible&&t.Opacity>=1&&!string.IsNullOrWhiteSpace(t.Text)))Contrast(text.Foreground,background,text.Text);foreach(var tab in Find<TabItem>(root))Contrast(tab.Foreground,background,"页签"+tab.Header);}
 static IEnumerable<T> Visuals<T>(DependencyObject root) where T:DependencyObject{if(root is T item)yield return item;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var result in Visuals<T>(VisualTreeHelper.GetChild(root,i)))yield return result;}
 static List<string> VisualContrast(DependencyObject root,Brush background){var failures=new List<string>();foreach(var text in Visuals<TextBlock>(root).Where(t=>t.IsVisible&&t.IsEnabled&&t.Opacity>=1&&!string.IsNullOrWhiteSpace(t.Text))){try{Contrast(text.Foreground,background,text.Text);}catch(Exception error){failures.Add(error.Message+"；Foreground="+text.Foreground+"；IsEnabled="+text.IsEnabled);}}return failures;}
 static void AreaRangeVisible(DeclaredFieldsView fields,IReadOnlyList<ConfigurationFieldAccessor> descriptors)
 {
  var low=fields.Fields.Single(f=>f.FieldId==descriptors.Single(d=>d.StoragePath=="FilterAreaMin").Port.Id);var high=fields.Fields.Single(f=>f.FieldId==descriptors.Single(d=>d.StoragePath=="FilterAreaMax").Port.Id);
  var label=(TextBlock)typeof(DeclaredFieldEditor).GetField("_label",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(low)!;Require(label.Text=="面积范围","配对范围标签不是面积范围："+label.Text);
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
  var numeric=Find<SunEyeVision.Plugin.Wpf.Controls.NumericUpDown>(high).Single();Require(!numeric.ShowSlider&&numeric.Padding.Left==2&&numeric.Padding.Right==2,"配对切回常量丢失padding/单滑条抑制");Verify("切订阅再切常量");Require(widthFailures.Count==0,"9999999可视宽度不足："+string.Join("；",widthFailures));
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
 static void Modal(DisplayObjectField display,Action<Window> edit)
 {
  Exception? failure=null;
  Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>
  {var dialog=Application.Current.Windows.Cast<Window>().Single(w=>w!=owner);try{dialog.UpdateLayout();Pump();edit(dialog);}catch(Exception e){failure=e;}finally{if(dialog.IsVisible)dialog.Close();}}));
  display.OpenStyleEditor();if(failure is not null)throw failure;
 }
 static int Main()
 {
  new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Themes/Dark/DarkTheme.xaml"})Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
  owner=new Window{Width=530,Height=700,Title="O06独立真实窗口验证",Content=host=new StackPanel()};owner.Resources.MergedDictionaries.Add(EditorResources.Create());owner.Show();Pump();
  try
  {
   Test("浮点已保存值只聚焦离开保真",()=>
   {var value=.12345;var writes=0;var field=new DeclaredFieldEditor(new("float",DataType.Number,Storage:NumericStorage.Float64),"比例",new ConstantSetting(value),p=>{value=(double)((ConstantSetting)p).Value!;writes++;});Show(field);var text=(TextBox)field.InputElement!;text.Focus();Pump();var button=new Button{Content="离开"};host.Children.Add(button);button.Focus();Pump();Require(value==.12345,"仅焦点变化将0.12345改成"+value.ToString("R")+"；写入"+writes+"次");});
   Test("de-DE浮点输入不被二次误解析",()=>
   {var previous=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");var value=.5;var field=new DeclaredFieldEditor(new("float",DataType.Number,Storage:NumericStorage.Float64),"比例",new ConstantSetting(value),p=>value=(double)((ConstantSetting)p).Value!);Show(field);var text=(TextBox)field.InputElement!;text.Focus();Pump();var button=new Button();host.Children.Add(button);button.Focus();Pump();Require(value==.5&&!field.HasInvalidText,"0.5在de-DE焦点提交后="+value.ToString("R",CultureInfo.InvariantCulture)+"；"+field.Error);}finally{CultureInfo.CurrentCulture=previous;}});
   Test("显示样式确定取消关闭及宿主拒绝",()=>
   {
    var writes=0;var width=3d;var reject=false;var color=PresentationDeclarations.Color(10,20,30,200);
    var visible=new DeclaredFieldEditor(new("visible",DataType.Boolean),"显示",new ConstantSetting(true),_=>{});
    var line=new DeclaredFieldEditor(new("width",DataType.Number,Storage:NumericStorage.Float64),"线宽",new ConstantSetting(width),p=>{if(reject)throw new InvalidOperationException("范围失效");width=(double)((ConstantSetting)p).Value!;writes++;});
    var rgba=new DeclaredFieldEditor(new("color",PresentationDeclarations.Rgba),"颜色",new ConstantSetting(color),p=>{color=(RecordValue)((ConstantSetting)p).Value!;writes++;});
    var display=new DisplayObjectField("Blob",visible,new[]{(rgba,"OK颜色"),(line,"线宽")});Show(display);
    Modal(display,w=>{var edit=Find<DeclaredFieldEditor>(w).Single(e=>e.FieldId=="width");((TextBox)edit.InputElement!).Text="7";Click(Find<Button>(w).Single(b=>Equals(b.Content,"取消")));});Require(width==3&&writes==0,"取消泄漏样式草稿");
    Modal(display,w=>{((TextBox)Find<DeclaredFieldEditor>(w).Single(e=>e.FieldId=="width").InputElement!).Text="8";w.Close();});Require(width==3&&writes==0,"关闭泄漏样式草稿");
    Modal(display,w=>{var field=(ColorField)Find<DeclaredFieldEditor>(w).Single(e=>e.FieldId=="color").InputElement!;field.BeginEdit();var picker=(ColorPicker)field.Template.FindName("PART_ColorPicker",field);picker.SelectedColor=Color.FromArgb(123,32,64,128);field.ApplyEdit();((TextBox)Find<DeclaredFieldEditor>(w).Single(e=>e.FieldId=="width").InputElement!).Text="7";Click(Find<Button>(w).Single(b=>Equals(b.Content,"确定")));Require(!w.IsVisible,"有效确定未关闭");});Require(width==7&&color.Get<long>("a")==123&&color.Get<long>("b")==128,"确定丢失RGBA/线宽");
    reject=true;
    Modal(display,w=>{((TextBox)Find<DeclaredFieldEditor>(w).Single(e=>e.FieldId=="width").InputElement!).Text="9";Click(Find<Button>(w).Single(b=>Equals(b.Content,"确定")));Require(w.IsVisible&&width==7&&color.Get<long>("a")==123,"宿主拒绝仍关闭或半写");});
    reject=false;
    Modal(display,w=>{((TextBox)Find<DeclaredFieldEditor>(w).Single(e=>e.FieldId=="width").InputElement!).Text="bad";Click(Find<Button>(w).Single(b=>Equals(b.Content,"确定")));Require(w.IsVisible&&width==7,"非法文本确认仍写入");});
   });
   Test("正式Blob五显示对象与ROI真实Combo条件",()=>
   {
    var manager=new PluginManager();var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;manager.LoadPlugins(Path.Combine(product,"plugins"));manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(t=>t.Id));
    var parameters=ToolRegistry.CreateParameters("BlobAnalysis");parameters.GetType().GetProperty("FilterAreaMax")!.SetValue(parameters,9999999d);var node=new WorkflowNodeBase("blob","Blob","Blob","BlobAnalysis"){Parameters=parameters};
    var flow=new SunEyeVision.Workflow.Workflow("flow","O06窗口草稿");flow.Nodes.Add(node);var solution=new Solution();solution.Workflows.Add(flow);
    var edits=(DocumentEditSession)Activator.CreateInstance(typeof(DocumentEditSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{solution},null)!;
    var root=new DeclaredToolConfigurationControl(ToolRegistry.CreateToolInstance("BlobAnalysis")!);var query=new DataSourceQueryService();
    void Bind(WorkflowNodeBase draft){root.SetCurrentNode(draft);root.SetParameters(draft.Parameters);root.SetDataProvider(query);}
    using var context=new ProductionEditorContext(edits.Host,flow.Id,node,()=>true,Bind,documentEdits:edits);Bind(context.DraftNode);ToolEditorCommit.AttachDraftContext(root,context);Show(root);
    var fields=Find<DeclaredFieldsView>(root).Single();var tabs=Find<TabControl>(fields).Single();Require(tabs.Items.Count==3,"正式Blob不是三页");tabs.SelectedIndex=2;Pump();Require(Find<DisplayObjectField>(fields).Count()==5,"图像显示没有五组对象");
    var descriptors=((IConfigurationFieldProvider)parameters).GetConfigurationFields();var declaredStyles=descriptors.Where(f=>f.Presentation.Layout?.DialogGroup is not null&&!f.Presentation.Layout.DialogPrimary).Select(f=>f.Port.Id).ToHashSet();var observedStyles=new HashSet<string>();
    foreach(var display in Find<DisplayObjectField>(fields)){var primary=Find<DeclaredFieldEditor>(display).Single();var eye=(Button)primary.InputElement!;var pencil=display.Children.OfType<Button>().Single();var ey=eye.TransformToAncestor(display).Transform(new Point(eye.ActualWidth/2,eye.ActualHeight/2));var pen=pencil.TransformToAncestor(display).Transform(new Point(pencil.ActualWidth/2,pencil.ActualHeight/2));Require(Math.Abs(ey.Y-pen.Y)<=2,"眼笔中心纵差="+Math.Abs(ey.Y-pen.Y));}
    foreach(var editor in fields.Fields){var error=(TextBlock)typeof(DeclaredFieldEditor).GetField("_error",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(editor)!;Require(error.Text.Length>0||error.Visibility==Visibility.Collapsed,"无错误字段仍保留错误空白行");}
    foreach(var source in Find<SourceSelectionField>(fields)){var error=(TextBlock)typeof(SourceSelectionField).GetField("_error",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(source)!;Require(error.Text.Length>0||error.Visibility==Visibility.Collapsed,"无错误来源仍保留错误空白行");}
    foreach(var entry in descriptors.Where(f=>f.StoragePath.StartsWith("ResultPresentation.Judgments")&&f.StoragePath.EndsWith(".Enabled")))Require(!entry.Presentation.DisplayName.Contains("·"),"判定开关仍含冗余后缀："+entry.Presentation.DisplayName);
    foreach(var display in Find<DisplayObjectField>(fields).ToArray())Modal(display,w=>{LightText(w,w.Background);var editors=Find<DeclaredFieldEditor>(w).ToArray();Require(editors.Length>0,"样式弹窗为空");foreach(var field in editors)Require(observedStyles.Add(field.FieldId),"样式字段重复进入弹窗");Click(Find<Button>(w).Single(b=>Equals(b.Content,"取消")));});Require(declaredStyles.SetEquals(observedStyles),"弹窗字段集合与正式DialogGroup声明不符");
    tabs.SelectedIndex=0;Pump();var group=((IConfigurationFieldProvider)parameters).ConfigurationSchema!.Presentation!.Regions.Single(g=>g.Role=="inspection");var mode=fields.Fields.Single(f=>f.FieldId==group.ModeFieldId);var combo=(ComboBox)mode.InputElement!;
    var correction=descriptors.Single(f=>f.StoragePath=="IsPositionCorrectionEnabled");var enabled=fields.Fields.Single(f=>f.FieldId==correction.Port.Id);var sourceCorrection=Find<SourceSelectionField>(fields).Single(s=>s.ToolTip?.ToString()?.StartsWith("位置修正来源")==true);Require(sourceCorrection.Visibility==Visibility.Collapsed,"修正关闭仍显示来源");((ToggleSwitch)enabled.InputElement!).IsChecked=true;Pump();Require(sourceCorrection.Visibility==Visibility.Visible,"修正开启来源未出现");((ToggleSwitch)enabled.InputElement!).IsChecked=false;Pump();Require(sourceCorrection.Visibility==Visibility.Collapsed,"修正关闭来源未收起");
    var geometry=Find<RegionGeometryEditor>(root).First();Require(geometry.Visibility==Visibility.Visible,"本地ROI编辑未显示");combo.SelectedItem=nameof(RegionFieldMode.InheritedGeometry);Pump();Require(geometry.Visibility==Visibility.Collapsed&&!geometry.IsEnabled,"继承几何仍显示/启用本地编辑");
    combo.SelectedItem=nameof(RegionFieldMode.InheritedRegion);Pump();Require(geometry.Visibility==Visibility.Collapsed,"继承像素区域仍显示本地编辑");
    combo.SelectedItem=nameof(RegionFieldMode.LocalGeometry);Pump();Require(geometry.Visibility==Visibility.Visible&&geometry.IsEnabled,"切回本地未恢复编辑");
    combo.SelectedItem=nameof(RegionFieldMode.InheritedRegion);Pump();Require(((IConfigurationFieldProvider)node.Parameters).GetConfigurationFields().Single(f=>f.Port.Id==group.ModeFieldId).Read()?.ToString()==nameof(RegionFieldMode.LocalGeometry),"ROI条件选择直接污染正式参数");Require(((IConfigurationFieldProvider)node.Parameters).GetConfigurationFields().Single(f=>f.Port.Id==group.ModeFieldId).Read()?.ToString()==nameof(RegionFieldMode.LocalGeometry),"外层取消改变正式模式");
    host.Children.Clear();var shell=new SunEyeVision.Plugin.SDK.UI.Windows.DefaultDebugWindow(root);shell.Show();shell.UpdateLayout();Pump();Require(shell.ActualWidth==420&&shell.ActualHeight==500&&root.IsLoaded,"紧凑DefaultDebugWindow实际装载/420×500尺寸不符");
    var closeText=Visuals<TextBlock>(shell).Single(t=>t.Text=="✕");Contrast(closeText.Foreground,new SolidColorBrush(Color.FromRgb(43,43,43)),"暗标题关闭×");var continuous=(Button)shell.FindName("ContinuousRunButton");var caption=Visuals<TextBlock>(continuous).Single(t=>t.Text=="连续执行");var measured=new FormattedText(caption.Text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(caption.FontFamily,caption.FontStyle,caption.FontWeight,caption.FontStretch),caption.FontSize,caption.Foreground,VisualTreeHelper.GetDpi(caption).PixelsPerDip);Require(measured.WidthIncludingTrailingWhitespace<=continuous.ActualWidth-continuous.Padding.Left-continuous.Padding.Right-continuous.BorderThickness.Left-continuous.BorderThickness.Right,"连续执行完整文字超过按钮可用宽度");
    var visualFailures=new List<string>();for(var page=0;page<tabs.Items.Count;page++){tabs.SelectedIndex=page;shell.UpdateLayout();Pump();if(page==1)try{AreaRangeVisible(fields,descriptors);}catch(Exception error){visualFailures.Add("面积范围可视宽度："+error.Message);}LightText(fields,fields.Background);visualFailures.AddRange(VisualContrast(fields,fields.Background).Select(s=>"page="+page+" "+s));var bitmap=new RenderTargetBitmap((int)shell.ActualWidth,(int)shell.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(shell);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"scrollbar-minwidth-window-page-"+page+".png")))encoder.Save(stream);}VerifyPageScroll(fields,tabs,descriptors,shell);File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"visual-contrast-scrollbar-minwidth-failures.txt"),visualFailures);shell.Close();context.Discard();Require(((IConfigurationFieldProvider)node.Parameters).GetConfigurationFields().Single(f=>f.Port.Id==group.ModeFieldId).Read()?.ToString()==nameof(RegionFieldMode.LocalGeometry),"实际外层取消改变正式模式");edits.Close();Require(visualFailures.Count==0,string.Join("；",visualFailures));
   });
   Console.WriteLine($"O06独立窗口检查：{passed}组通过，{failed}组失败。仅自动交互，不代人工外观验收。");return failed==0?0:1;
  }
  finally{foreach(var window in Application.Current.Windows.Cast<Window>().ToArray())window.Close();Application.Current.Shutdown();}
 }
}














