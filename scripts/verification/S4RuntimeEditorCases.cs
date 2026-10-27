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
        var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;var plugins=new PluginManager();plugins.LoadPlugins(Path.Combine(product,"plugins"));
        plugins.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(m=>m.Id));PluginLoader.MarkAsLoaded();
        var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"document");Directory.CreateDirectory(folder);
        ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(folder);
        var solutions=ServiceInitializer.SolutionManager;var vm=new MainWindowViewModel();solutions.CreateUntitledSolution();
        if(vm.WorkflowTabViewModel.SelectedTab is null)vm.NewWorkflowCommand.Execute(null);
        var resultHost=new ContentControl();resultHost.SetBinding(ContentControl.ContentProperty,new System.Windows.Data.Binding(nameof(MainWindowViewModel.SelectedNodeDataAnalysisControl)){Source=vm});
        var shell=new Window{Width=620,Height=400,Content=resultHost,Left=-10000,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();
        var node=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(node);
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
        WorkflowContext Context()=>(WorkflowContext)typeof(MainWindowViewModel).GetProperty("CurrentWorkflowContext",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
        var path=Path.Combine(folder,"runtime-editor.solution");
        Require(solutions.SaveCurrentDocument(path).FileWritten,"初始化测试文件位置失败");
        ComboBox? oldInputCombo=null;
        string? sourceNodeId=null;
        var first=Open();
        Case("默认UI工厂创建Runtime节点，真实MainWindow打开中立窗口与58个XML字段",()=>
        {
            Require(node.RuntimeConfiguration?.SettingsType==typeof(BlobSettings)&&first.EditorControl is null
                &&Control(first).GetType()==typeof(RuntimeNodeConfigurationControl),"窗口借用SDK Blob参数或未保留Runtime配置");
            Require(Logical(Control(first)).OfType<DeclaredFieldEditor>().Count()==58,"XML输入及设置字段未进入真实窗口");
            Require(solutions.Session!.Editing.RegisteredScopeCount==1,"实际窗口未登记文档事务");
            // Inspect rendered input controls on their actual page, independently of the
            // user's saved designer SelectedPageIndex.
            Logical(Control(first)).OfType<TabControl>().Single().SelectedIndex=0;
            first.UpdateLayout();Pump();
            var sourceButtons=Visual(Control(first)).OfType<Button>().Where(b=>Equals(b.ToolTip,"选择订阅来源")).ToArray();
            Require(sourceButtons.Length==3&&sourceButtons.All(b=>Visual(b).OfType<Image>().Any(i=>i.Source is BitmapSource bitmap&&bitmap.PixelWidth>0&&i.ActualWidth>0)),
                "来源按钮没有实际显示迁入图标");
            var image=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(f=>f.FieldId==BlobTool.ImageInput);
            Require(image.InputElement is ComboBox combo&&combo.Resources.MergedDictionaries.Any(dictionary=>dictionary.Source?.OriginalString.EndsWith("SourceComboBoxes.xaml")==true)
                &&!Logical(image).OfType<Button>().Any(),"图像输入源未使用VM EnumerationG原ComboBox，或仍有链/清除按钮");
            Require(Visual(image).OfType<Image>().Any(icon=>icon.Source is BitmapSource bitmap&&bitmap.PixelWidth>0&&icon.ActualWidth>0),
                "VM输入源右下角原三角图标未实际渲染");
            Capture(first,"runtime-editor-basic.png");
        });
        Case("VM图像输入源下拉从正式声明选择，未确认引用只写私有节点",()=>
        {
            var source=vm.WorkflowTabViewModel.SelectedTab!.CreateNode("ImageLoad");
            sourceNodeId=source.Id;
            var imageFile=Path.Combine(folder,"input.png");var png=new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(8,8,96,96,PixelFormats.Gray8,null,Enumerable.Repeat((byte)200,64).ToArray(),8)));
            using(var output=File.Create(imageFile))png.Save(output);
            source.Parameters.GetType().GetProperty("FilePath")!.SetValue(source.Parameters,imageFile);vm.AddNodeToWorkflow(source);
            source.IsSelected=true;vm.SelectedNode=source;
            (vm.ActiveInputSource??throw new Exception("正式输入源面板未建立")).AddImage(new SunEyeVision.UI.Shell.Controls.Panels.ImageInfo{Name="Runtime调试输入",FilePath=imageFile,SourceReference=imageFile,IsForRun=true});
            source.IsSelected=false;node.IsSelected=true;vm.SelectedNode=node;Pump();
            var input=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(f=>f.FieldId==BlobTool.ImageInput);
            oldInputCombo=(ComboBox)input.InputElement!;
            var combo=(ComboBox)input.InputElement!;combo.IsDropDownOpen=true;Pump();
            Require(!app.Windows.OfType<Window>().Any(w=>w.Title=="选择绑定来源"),"图像下拉仍打开通用订阅窗口");
            Require(((ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!).DraftNode.ParamSettings.GetSetting(BlobTool.ImageInput) is null,
                "刷新候选擅自写入了来源");
            combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().First(item=>item.Tag is ReferenceSetting reference&&reference.Reference.NodeId==source.Id
                &&reference.Reference.MemberFieldPath.Count==0&&reference.Reference.Type.Resource==ResourceKind.Image);
            combo.IsDropDownOpen=false;Pump();
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            Require(context.DraftNode.ParamSettings.GetSetting(BlobTool.ImageInput)?.SourceNodeId==source.Id
                &&node.ParamSettings.GetSetting(BlobTool.ImageInput) is null,"来源下拉未写草稿或污染正式引用");
        });
        Case("图像下拉拒绝真实自引用并恢复原草稿，刷新不改引用，修正后可继续",()=>
        {
            var input=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(field=>field.FieldId==BlobTool.ImageInput);
            var combo=(ComboBox)input.InputElement!;var priorItem=(ComboBoxItem)combo.SelectedItem;
            var reference=((ReferenceSetting)priorItem.Tag).Reference;
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            var expression=context.DraftNode.ParamSettings.GetSetting(BlobTool.ImageInput)!.ReferenceExpression;
            var invalid=new ComboBoxItem{Content="自引用拒绝检查",Tag=new ReferenceSetting(new OutputReference(node.Id,BlobSchema.Field(5),
                TypeRef.ResourceOf(ResourceKind.Image),scopeId:reference.ScopeId))};
            combo.Items.Add(invalid);combo.SelectedItem=invalid;Pump();
            Require(input.HasInvalidText&&ReferenceEquals(combo.SelectedItem,priorItem)
                &&context.DraftNode.ParamSettings.GetSetting(BlobTool.ImageInput)!.ReferenceExpression==expression,
                "自引用未被生产合同拒绝或污染了原来源");
            combo.Items.Remove(invalid);
            combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is UnsetSetting);combo.SelectedItem=priorItem;Pump();
            Require(!input.HasInvalidText,"修正来源后仍无法继续");
            combo.IsDropDownOpen=true;Pump();combo.IsDropDownOpen=false;
            Require(context.DraftNode.ParamSettings.GetSetting(BlobTool.ImageInput)!.ReferenceExpression==expression
                &&node.ParamSettings.GetSetting(BlobTool.ImageInput) is null,"刷新候选写入了草稿或正式节点");
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
            var combo=oldInputCombo!;
            combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is UnsetSetting);
            Require(context.DraftNode.ParamSettings.GetSetting(BlobTool.ImageInput)?.ReferenceExpression==prior,"旧控件事件写入了新草稿");
        });
        Case("真实工作流发布来源后，窗口执行命令只更新私有调试结果，连续运行可停止",()=>
        {
            var run=vm.RunSelectedImageAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();
            var production=Context().GetNodeResult(node.Id)?.Result??throw new Exception("正式工作流没有Blob结果");
            Require(production.IsSuccess,"正式工作流准备失败："+production.ErrorMessage);
            var source=Context().GetNodeResult(sourceNodeId!)!.Result;
            var table=Logical(vm.SelectedNodeDataAnalysisControl!).OfType<DataGrid>().Single();Require(table.Items.Count==1,"正式结果没有进入实际表格");
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            Threshold(first).Text="255";Pump();
            SunEyeVision.Plugin.SDK.Commands.ToolCommands.Execute.Execute(null,first);
            PumpUntil(()=>context.DebugResults.GetNodeResult(node.Id)?.Result is RuntimeToolResults);
            var preview=(RuntimeToolResults)context.DebugResults.GetNodeResult(node.Id)!.Result!;
            Require(preview.Output is BlobOutput{Count:0}&&Formal()==73,"调试未采用私有阈值或污染正式设置");
            Require(ReferenceEquals(source,Context().GetNodeResult(sourceNodeId!)!.Result)&&ReferenceEquals(production,Context().GetNodeResult(node.Id)!.Result),"调试重跑或替换正式结果");
            Require(table.Items.Count==0,"私有调试结果未刷新实际结果表");Capture(first,"runtime-editor-debug.png");
            var request=preview.ExecutionIdentity!.RequestId;
            SunEyeVision.Plugin.SDK.Commands.ToolCommands.ContinuousExecute.Execute(null,first);
            PumpUntil(()=>context.DebugResults.GetNodeResult(node.Id)?.Result?.ExecutionIdentity?.RequestId is Guid next&&next!=request);
            SunEyeVision.Plugin.SDK.Commands.ToolCommands.ContinuousExecute.Execute(null,first);
            var stopped=context.DebugResults.GetNodeResult(node.Id)!.Result!.ExecutionIdentity!.RequestId;
            var until=DateTime.UtcNow.AddMilliseconds(650);while(DateTime.UtcNow<until){Pump();Thread.Sleep(5);}
            Require(context.DebugResults.GetNodeResult(node.Id)!.Result!.ExecutionIdentity!.RequestId==stopped&&context.DebugOwner.ActiveRuns==0,"连续运行停止后仍执行或未排空");
            Threshold(first).Text="73";Pump();
        });
        Case("输入捕获失败清空私有预览与实际结果表，正式结果保持不变",()=>
        {
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            var formal=Context().GetNodeResult(node.Id)!.Result;
            var ready=Control(first).RunDebugAsync();PumpUntil(()=>ready.IsCompleted);ready.GetAwaiter().GetResult();
            var table=Logical(vm.SelectedNodeDataAnalysisControl!).OfType<DataGrid>().Single();Require(table.Items.Count==1,"有效调试未恢复结果表");
            var prior=context.DraftNode.ParamSettings.GetSetting(BlobTool.ImageInput)!.Clone();
            var input=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(f=>f.FieldId==BlobTool.ImageInput);
            var sourceCombo=(ComboBox)input.InputElement!;
            sourceCombo.SelectedItem=sourceCombo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is UnsetSetting);
            var failed=Control(first).RunDebugAsync();PumpUntil(()=>failed.IsCompleted);
            try{failed.GetAwaiter().GetResult();throw new Exception("缺少来源的调试竟然成功");}catch(InvalidOperationException){}
            Require(context.DebugResults.GetNodeResult(node.Id) is null&&table.Items.Count==0&&ReferenceEquals(formal,Context().GetNodeResult(node.Id)!.Result),"失败未清空私有预览或污染正式结果");
            context.DraftNode.ParamSettings.SetSetting(prior);Control(first).BindDraft(context.DraftNode);Pump();
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
        Console.WriteLine($"S4 Runtime窗口：{_passed}组通过；SDK入口={(sdkPresent?"存在":"缺席")}，Wpf={(wpfPresent?"存在":"缺席")}；真实打开/编辑/保存/确定取消/调试命令与连续停止；未验证本地ROI、全部VM外观或人工验收。");return 0;
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var found in Logical(child))yield return found;}
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
