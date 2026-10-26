using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Plugin.Wpf.Fields;
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
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
        var dialogs=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        dialogs.Tick+=(_,_)=>{foreach(var dialog in app.Windows.OfType<Window>().Where(w=>w.GetType().Name.Contains("MessageDialog")).ToArray())
            {Console.Error.WriteLine("产品提示："+string.Join(" | ",Logical(dialog).OfType<TextBlock>().Select(t=>t.Text)));dialog.Close();}};dialogs.Start();
        var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;var plugins=new PluginManager();plugins.LoadPlugins(Path.Combine(product,"plugins"));
        plugins.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(m=>m.Id));PluginLoader.MarkAsLoaded();
        var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"document");Directory.CreateDirectory(folder);
        ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(folder);
        var solutions=ServiceInitializer.SolutionManager;var vm=new MainWindowViewModel();solutions.CreateUntitledSolution();
        if(vm.WorkflowTabViewModel.SelectedTab is null)vm.NewWorkflowCommand.Execute(null);
        var shell=new Window{Width=100,Height=100,Left=-10000,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();
        var baseNode=SunEyeVision.Workflow.WorkflowNodeFactory.CreateAlgorithmNode(BlobTool.Id,"runtime-editor-node","Runtime Blob","Blob")??throw new Exception("Runtime节点创建失败");
        var node=SunEyeVision.UI.Shared.WorkflowNode.FromBase(baseNode);vm.AddNodeToWorkflow(node);
        node.DebugWindowStyle=DebugWindowStyle.Custom;
        var threshold=((IToolSettingsFields)new BlobTool()).SettingsSchema.Presentation!.Fields.Single(f=>f.DisplayName=="阈值").FieldId;
        DefaultDebugWindow Open()
        {
            vm.OpenDebugWindowCommand.Execute(node);Pump();
            var window=app.Windows.OfType<DefaultDebugWindow>().SingleOrDefault(w=>w.IsVisible)??throw new Exception("实际打开命令没有创建Runtime窗口");
            window.UpdateLayout();Pump();return window;
        }
        RuntimeNodeConfigurationControl Control(DefaultDebugWindow window)=>(RuntimeNodeConfigurationControl)((ContentControl)window.FindName("ContentHost")).Content;
        TextBox Threshold(DefaultDebugWindow window)
        {
            var control=Control(window);Logical(control).OfType<TabControl>().Single().SelectedIndex=1;window.UpdateLayout();Pump();
            return (TextBox)Logical(control).OfType<DeclaredFieldEditor>().Single(f=>f.FieldId==threshold).InputElement!;
        }
        int Formal()=>(int)((BlobSettings)node.RuntimeConfiguration!.CaptureSettings()).Threshold;
        var path=Path.Combine(folder,"runtime-editor.solution");
        Require(solutions.SaveCurrentDocument(path).FileWritten,"初始化测试文件位置失败");
        DeclaredFieldEditor? oldInput=null;
        var first=Open();
        Case("真实MainWindow命令打开中立Runtime窗口，UI投影保留实际设置与58个XML字段",()=>
        {
            Require(node.RuntimeConfiguration?.SettingsType==typeof(BlobSettings)&&first.EditorControl is null
                &&Control(first).GetType()==typeof(RuntimeNodeConfigurationControl),"窗口借用SDK Blob参数或未保留Runtime配置");
            Require(Logical(Control(first)).OfType<DeclaredFieldEditor>().Count()==58,"XML输入及设置字段未进入真实窗口");
            Require(solutions.Session!.Editing.RegisteredScopeCount==1,"实际窗口未登记文档事务");
            var sourceButtons=Visual(Control(first)).OfType<Button>().Where(b=>Equals(b.ToolTip,"选择订阅来源")).ToArray();
            Require(sourceButtons.Length==4&&sourceButtons.All(b=>Visual(b).OfType<Image>().Any(i=>i.Source is BitmapSource bitmap&&bitmap.PixelWidth>0&&i.ActualWidth>0)),
                "来源按钮没有实际显示迁入图标");
            Capture(first,"runtime-editor-basic.png");
        });
        Case("实际来源弹窗从声明选择图像，未确认引用只写私有节点",()=>
        {
            var source=vm.WorkflowTabViewModel.SelectedTab!.CreateNode("ImageLoad");
            var imageFile=Path.Combine(folder,"input.png");var png=new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(8,8,96,96,PixelFormats.Gray8,null,Enumerable.Repeat((byte)200,64).ToArray(),8)));
            using(var output=File.Create(imageFile))png.Save(output);
            source.Parameters.GetType().GetProperty("FilePath")!.SetValue(source.Parameters,imageFile);vm.AddNodeToWorkflow(source);
            var input=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(f=>f.FieldId==BlobTool.ImageInput);
            oldInput=input;
            var pick=Logical(input).OfType<Button>().Single(b=>Equals(b.ToolTip,"选择订阅来源"));
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>
            {
                var window=app.Windows.OfType<Window>().Single(w=>w.Title=="选择绑定来源");
                var picker=Logical(window).OfType<ReferenceSelectionControl>().Single();
                picker.SelectedCandidate=Logical(picker).OfType<ListBox>().Single().Items.OfType<DeclaredReferenceCandidate>()
                    .First(c=>c.NodeId==source.Id&&c.MemberPath.Count==0&&c.Output.Type.Resource==ResourceKind.Image);
                Logical(window).OfType<Button>().Single(b=>Equals(b.Content,"确定")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            pick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            Require(context.DraftNode.ParamSettings.GetSetting(BlobTool.ImageInput)?.SourceNodeId==source.Id
                &&node.ParamSettings.GetSetting(BlobTool.ImageInput) is null,"来源弹窗未写草稿或污染正式引用");
        });
        Case("真实未失焦数值进入私有Runtime草稿，隐藏窗口后文档保存接受它并可重开",()=>
        {
            var text=Threshold(first);text.Focus();text.Text="73";Pump();
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            Require(Formal()==128&&((BlobSettings)context.DraftNode.RuntimeConfiguration!.CaptureSettings()).Threshold==73,"未确认输入污染正式设置或未更新草稿");
            Capture(first,"runtime-editor-run.png");
            first.Hide();vm.SaveCurrentSolutionCommand.Execute(null);Pump();
            Require(Formal()==73,"文档保存没有接受Runtime草稿");
            var restored=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("保存重开失败");
            Require(((BlobSettings)restored.Workflows.SelectMany(w=>w.Nodes).Single(n=>n.Id==node.Id).RuntimeConfiguration!.CaptureSettings()).Threshold==73,"重开覆盖用户值");
            first.Show();Pump();
        });
        Case("保存重绑后旧来源控件的迟到清除事件不能改新草稿",()=>
        {
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            var prior=context.DraftNode.ParamSettings.GetSetting(BlobTool.ImageInput)!.ReferenceExpression;
            Logical(oldInput!).OfType<Button>().Single(b=>Equals(b.Content,"×")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(context.DraftNode.ParamSettings.GetSetting(BlobTool.ImageInput)?.ReferenceExpression==prior,"旧控件事件写入了新草稿");
        });
        Case("非法输入隐藏后仍阻断保存及确定，取消关闭保留正式配置并退出范围",()=>
        {
            var before=File.ReadAllBytes(path);var text=Threshold(first);text.Focus();text.Text="invalid";Pump();first.Hide();
            vm.SaveCurrentSolutionCommand.Execute(null);Pump();Require(before.SequenceEqual(File.ReadAllBytes(path)),"非法隐藏输入仍保存或污染文件");
            Require(!first.TryConfirm(out _)&&Formal()==73,"非法输入确定成功或污染正式配置");
            first.Close();Pump();Require(solutions.Session!.Editing.RegisteredScopeCount==0&&Formal()==73,"取消未退出或污染正式值");
        });
        Case("重新打开读正式值，关闭丢弃新草稿，确定发布并释放范围",()=>
        {
            var window=Open();var text=Threshold(window);Require(text.Text.StartsWith("73"),"重开没有正式值");text.Text="99";Pump();window.Close();Pump();Require(Formal()==73,"关闭提交了未确认草稿");
            window=Open();Threshold(window).Text="99";Pump();Require(window.TryConfirm(out var error),error);Pump();
            Require(Formal()==99&&solutions.Session!.Editing.RegisteredScopeCount==0,"确定未发布强类型设置或遗留编辑范围");
        });
        shell.Close();dialogs.Stop();
        var sdkPresent=File.Exists(Path.Combine(product,"plugins/SunEyeVision.Tool.BlobAnalysis/SunEyeVision.Tool.BlobAnalysis.dll"));
        var wpfPresent=File.Exists(Path.Combine(product,"SunEyeVision.Tool.BlobAnalysis.Wpf.dll"));
        if(!sdkPresent)Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="SunEyeVision.Tool.BlobAnalysis"),"SDK入口缺席时仍加载了旧Blob");
        if(!wpfPresent)Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="SunEyeVision.Tool.BlobAnalysis.Wpf"),"Wpf缺席时仍加载了专属视图");
        Console.WriteLine($"S4 Runtime窗口：{_passed}组通过；SDK入口={(sdkPresent?"存在":"缺席")}，Wpf={(wpfPresent?"存在":"缺席")}；真实打开/编辑/文档保存/确定取消；未验证调试运行、本地ROI、全部VM外观或人工验收。");return 0;
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var found in Logical(child))yield return found;}
    private static void Pump()
    {var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
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
