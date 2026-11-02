using SunEyeVision.Plugin.Runtime;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using AppsPopup=SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    [STAThread] private static int Main()
    {
        try{return Run();}catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static int Run()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        SunEyeVision.Plugin.SDK.Vision.Hosting.VisionImageFactoryLocator.Current=new SunEyeVision.HalAdapter.HalImageFactory();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
        var dialogs=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        dialogs.Tick+=(_,_)=>{foreach(var dialog in app.Windows.OfType<Window>().Where(w=>w.GetType().Name.Contains("MessageDialog")).ToArray())
            {Console.Error.WriteLine("产品提示："+string.Join(" | ",Logical(dialog).OfType<TextBlock>().Select(t=>t.Text)));dialog.Close();}};dialogs.Start();
        var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;var plugins=new PluginManager();plugins.LoadPlugins();
        plugins.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(m=>m.Id));PluginLoader.MarkAsLoaded();
        var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"document");Directory.CreateDirectory(folder);
        ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(folder);
        var solutions=ServiceInitializer.SolutionManager;var vm=new MainWindowViewModel();solutions.CreateUntitledSolution();
        if(vm.WorkflowTabViewModel.SelectedTab is null)vm.NewWorkflowCommand.Execute(null);
        var resultHost=new ContentControl();resultHost.SetBinding(ContentControl.ContentProperty,new System.Windows.Data.Binding(nameof(MainWindowViewModel.SelectedNodeDataAnalysisControl)){Source=vm});
        var shell=new Window{Width=620,Height=400,Content=resultHost,Left=-10000,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();
        var node=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(node);
        node.DebugWindowStyle=DebugWindowStyle.Custom;
        var threshold=BindingId("LowThreshold");
        const string modeId="field:8ca20000000040008000000000000001";
        DefaultDebugWindow Open()=>OpenFor(node);
        DefaultDebugWindow OpenFor(WorkflowNodeBase target)
        {
            vm.OpenDebugWindowCommand.Execute(target);Pump();
            var window=app.Windows.OfType<DefaultDebugWindow>().SingleOrDefault(w=>w.IsVisible)??throw new Exception("实际打开命令没有创建Runtime窗口");
            window.UpdateLayout();Pump();return window;
        }
        RuntimeNodeConfigurationControl Control(DefaultDebugWindow window)=>(RuntimeNodeConfigurationControl)((ContentControl)window.FindName("ContentHost")).Content;
        TextBox Threshold(DefaultDebugWindow window)
        {
            var control=Control(window);Logical(control).OfType<TabControl>().Single().SelectedIndex=1;window.UpdateLayout();Pump();
            return (TextBox)Logical(control).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==threshold).InputElement!;
        }
        ReferenceSetting? ImageBinding(WorkflowNodeBase value)=>value.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) as ReferenceSetting;
        int Formal()=>(int)((BlobSettings)node.RuntimeConfiguration!.CaptureSettings()).Threshold;
        WorkflowContext Context()=>(WorkflowContext)typeof(MainWindowViewModel).GetProperty("CurrentWorkflowContext",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
        var path=Path.Combine(folder,"runtime-editor.solution");
        Require(solutions.SaveCurrentDocument(path).FileWritten,"初始化测试文件位置失败");
            var source=vm.WorkflowTabViewModel.SelectedTab!.CreateNode("ImageLoad");
            var imageFile=Path.Combine(folder,"input.png");var png=new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(8,8,96,96,PixelFormats.Gray8,null,Enumerable.Repeat((byte)200,64).ToArray(),8)));
            using(var output=File.Create(imageFile))png.Save(output);
            Require(source.RuntimeConfiguration?.ToolId=="ImageLoad","图像源UI工厂未创建实际Runtime配置");
            var sourcePolicy=ImageSourcePolicy.Read(source)??throw new Exception("图像源没有正式字段投影");
            sourcePolicy.FilePaths=new(){imageFile};sourcePolicy.CurrentIndex=0;sourcePolicy.ForRunPaths=new(){imageFile};sourcePolicy.SNInitialValue=1;sourcePolicy.ApplyTo(source);
            vm.AddNodeToWorkflow(source);

        GlobalVariable? uiVariable=null;
        {
            var host=solutions.Session!.Editing.Host;var variables=host.BeginVariableEdit();
            uiVariable=new GlobalVariable("验收阈值",77d,"Double","验收参数");
            variables.Configuration.GlobalVariables.Add(uiVariable);
            host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variables);
        }
        var first=Open();var control=Control(first);
        Logical(control).OfType<TabControl>().Single().SelectedIndex=1;first.UpdateLayout();Pump();
        var fields=Logical(control).OfType<DeclaredFieldsView>().Single();
        var high=fields.Fields.Single(f=>f.FieldId==BindingId("MaxArea"));
        var low=fields.Fields.Single(f=>f.FieldId==BindingId("MinArea"));
        Require(high.Setting is ConstantSetting{Value:999999999L},"面积上限不是声明最大值");
        var hosts=Visual(control).OfType<SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView>().Where(v=>v.IsVisible).ToArray();
        Require(hosts.Length==1,"运行页原生列表宿主数量不唯一");var pageHost=hosts[0];
        var initial=new{first.Width,first.ActualWidth,first.MinWidth,maxWidth=Finite(first.MaxWidth),first.Height,first.ResizeMode,first.SizeToContent,labelWidth=pageHost.ChildLeftWidth};
        var matrix=new List<object>();
        foreach(var label in new[]{pageHost.ChildLeftWidth,89d}.Distinct())
        {
            pageHost.ChildLeftWidth=label;
            foreach(var width in new[]{360d,400d,420d,440d,460d,480d,500d,520d,560d,640d})
            {
                first.Width=width;first.UpdateLayout();Pump();
                var left=low.InputElement as TextBox??throw new Exception("范围低端没有真实文本输入");
                var right=high.InputElement as TextBox??throw new Exception("范围高端没有真实文本输入");
                left.ScrollToHome();right.ScrollToHome();first.UpdateLayout();Pump();
                matrix.Add(new{requestedWindowWidth=width,actualWindowWidth=first.ActualWidth,labelWidth=label,
                    measurement="actual WPF window at current desktop DPI",dpi=WindowDpi(first),
                    left=Endpoint(left,first),right=Endpoint(right,first),ancestors=Ancestors(right).ToArray()});
                if(width is 420 or 520)Capture(first,$"range-label-{label}-window-{width}.png");
            }
        }
        first.Close();Pump();
        var standalone=new List<object>();
        var numeric=new SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.NumericControl
            {FontSize=14,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,
             Minimum=1,Maximum=999999999,MinValue=1,MaxValue=999999999,IsNeedCheckExtremumByXML=true,Text="999999999"};
        var isolated=new Window{Owner=shell,Title="迁入原数值控件独立宽度量测",Width=420,Height=150,Left=-10000,ShowActivated=false,ShowInTaskbar=false,Content=numeric};isolated.Show();
        foreach(var width in new[]{80d,100d,120d,160d})
        {
            numeric.Width=width;isolated.UpdateLayout();Pump();
            var text=(TextBox)numeric.FindName("ValueText");text.ScrollToHome();isolated.UpdateLayout();Pump();
            standalone.Add(new{requestedNumericWidth=width,actualNumericWidth=numeric.ActualWidth,dpi=WindowDpi(isolated),input=Endpoint(text,isolated),ancestors=Ancestors(text).ToArray()});
            Capture(isolated,$"range-standalone-{width}.png");
        }
        isolated.Close();shell.Close();dialogs.Stop();
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"blob-range-layout-report.json"),System.Text.Json.JsonSerializer.Serialize(
            new{measurementOnly=true,productRoot=product,initialWindow=initial,
                dpiBoundary="Actual window DPI recorded; 96/120/144/192 samples are glyph raster metrics, not global Windows DPI changes or per-monitor runs.",
                labelBoundary="89 is the original ParamListView Chinese default replayed temporarily in the same product view; it is not a VM SDK runtime.",matrix,standalone},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("量测完成：真实窗口20组宽度/标签组合、独立原数值4组；仅诊断，不代表截断已修复或视觉验收。");return 0;
    }
    private static object WindowDpi(Visual visual)
    {
        var dpi=VisualTreeHelper.GetDpi(visual);return new{dpiScaleX=dpi.DpiScaleX,dpiScaleY=dpi.DpiScaleY,dpiX=dpi.PixelsPerInchX,dpiY=dpi.PixelsPerInchY,dpi.PixelsPerDip};
    }
    private static double? Finite(double value)=>double.IsFinite(value)?value:null;
    private static object Endpoint(TextBox text,Visual root)
    {
        var dpi=VisualTreeHelper.GetDpi(text);var face=new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch);
        var glyphs=new[]{96d,120d,144d,192d}.Select(sample=>
        {
            var formatted=new FormattedText(text.Text,System.Globalization.CultureInfo.InvariantCulture,text.FlowDirection,face,text.FontSize,Brushes.Black,sample/96);
            return new{sampleDpi=sample,kind="glyph raster metric only",glyphWidthDip=formatted.WidthIncludingTrailingWhitespace,glyphWidthPixels=formatted.WidthIncludingTrailingWhitespace*sample/96};
        }).ToArray();
        var rect=text.GetRectFromCharacterIndex(Math.Max(0,text.Text.Length-1),true);
        return new{text=text.Text,text.ActualWidth,text.ActualHeight,desiredWidth=text.DesiredSize.Width,text.ViewportWidth,text.ExtentWidth,text.HorizontalOffset,
            padding=text.Padding.ToString(),margin=text.Margin.ToString(),border=text.BorderThickness.ToString(),text.FontSize,fontFamily=text.FontFamily.Source,fontWeight=text.FontWeight.ToString(),
            actualDpi=WindowDpi(text),lastCharacterRight=rect.IsEmpty?(double?)null:Finite(rect.Right),textClipped=text.ExtentWidth>text.ViewportWidth+.5,glyphs,
            widthSource=DependencyPropertyHelper.GetValueSource(text,FrameworkElement.WidthProperty).BaseValueSource.ToString(),fontSizeSource=DependencyPropertyHelper.GetValueSource(text,Control.FontSizeProperty).BaseValueSource.ToString()};
    }
    private static IEnumerable<object> Ancestors(FrameworkElement element)
    {
        DependencyObject? current=element;
        while(current is not null)
        {
            if(current is FrameworkElement node)
            {
                yield return new{type=node.GetType().FullName,node.Name,node.ActualWidth,node.ActualHeight,declaredWidth=Finite(node.Width),node.MinWidth,maxWidth=Finite(node.MaxWidth),desiredWidth=node.DesiredSize.Width,
                    margin=node.Margin.ToString(),node.HorizontalAlignment,node.ClipToBounds,node.UseLayoutRounding,node.SnapsToDevicePixels,
                    columns=node is Grid grid?grid.ColumnDefinitions.Select(c=>new{width=c.Width.ToString(),c.ActualWidth,c.MinWidth,maxWidth=Finite(c.MaxWidth)}).ToArray():null};
            }
            current=VisualTreeHelper.GetParent(current);
        }
    }
    private static string SettingId(string name)=>new BlobTool().SettingsSchema.Presentation!.Fields.Single(field=>field.DisplayName==BlobParameterDefinition.Instance.Label(name)).FieldId;
    private static string BindingId(string name)=>((IToolParameterBindingNames)new BlobTool()).ParameterBindingNames.Single(pair=>pair.Value==name).Key;
    private static string ChoiceKey(ComboBox choice,object item)
    {
        Require(choice.SelectedValuePath==nameof(DeclaredChoiceDisplayItem.FormalKey),"Choice没有使用正式键绑定");
        return item is DeclaredChoiceDisplayItem display?display.DisplayId:throw new Exception("模式未使用正式公开展示快照");
    }
    private static bool IsUnavailableBoundary(string error)=>error.Contains("界面试用",StringComparison.Ordinal)||error.Contains("未接入",StringComparison.Ordinal)||error.Contains("未闭合",StringComparison.Ordinal)||error.Contains("未就绪",StringComparison.Ordinal)||error.Contains("未完成",StringComparison.Ordinal);
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        var seen=new HashSet<DependencyObject>();var pending=new Stack<DependencyObject>();pending.Push(root);
        while(pending.Count>0)
        {
            var node=pending.Pop();if(!seen.Add(node))continue;yield return node;
            foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())pending.Push(child);
            if(node is SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView list)
                foreach(var row in list.Items.OfType<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>())if(row.View is {} view)pending.Push(view);
        }
    }
    private static void Pump()
    {var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void PumpUntil(Func<bool> complete)
    {var until=DateTime.UtcNow.AddSeconds(30);while(!complete()){if(DateTime.UtcNow>until)throw new TimeoutException("真实窗口/工作流操作未完成");Pump();Thread.Sleep(5);}Pump();}
    private static void Capture(FrameworkElement view,string name)
    {
        view.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);
        var images=Visual(view).OfType<Image>().Select(i=>new {Source=i.Source?.ToString(),Width=i.ActualWidth,Height=i.ActualHeight,
            PixelWidth=(i.Source as BitmapSource)?.PixelWidth,PixelHeight=(i.Source as BitmapSource)?.PixelHeight});
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name+".images.json"),System.Text.Json.JsonSerializer.Serialize(images));
        bitmap.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name));png.Save(file);
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {yield return root;for(var n=0;n<VisualTreeHelper.GetChildrenCount(root);n++)foreach(var child in Visual(VisualTreeHelper.GetChild(root,n)))yield return child;}
    private static void Case(string name,Action action){action();_passed++;Console.WriteLine("通过："+name);}
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
