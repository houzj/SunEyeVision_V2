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
        var threshold=((IToolSettingsFields)new BlobTool()).SettingsSchema.Presentation!.Fields.Single(f=>f.DisplayName==BlobParameterDefinition.Instance.Label("Threshold")).FieldId;
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
            return (TextBox)Logical(control).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==threshold).InputElement!;
        }
        ReferenceSetting? ImageBinding(WorkflowNodeBase value)=>value.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) as ReferenceSetting;
        int Formal()=>(int)((BlobSettings)node.RuntimeConfiguration!.CaptureSettings()).Threshold;
        WorkflowContext Context()=>(WorkflowContext)typeof(MainWindowViewModel).GetProperty("CurrentWorkflowContext",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
        var path=Path.Combine(folder,"runtime-editor.solution");
        Require(solutions.SaveCurrentDocument(path).FileWritten,"初始化测试文件位置失败");
        ComboBox? oldInputCombo=null;
        string? sourceNodeId=null;
            var source=vm.WorkflowTabViewModel.SelectedTab!.CreateNode("ImageLoad");
            sourceNodeId=source.Id;
            var imageFile=Path.Combine(folder,"input.png");var png=new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(8,8,96,96,PixelFormats.Gray8,null,Enumerable.Repeat((byte)200,64).ToArray(),8)));
            using(var output=File.Create(imageFile))png.Save(output);
            Require(source.RuntimeConfiguration?.ToolId=="ImageLoad","图像源UI工厂未创建实际Runtime配置");
            var sourcePolicy=ImageSourcePolicy.Read(source)??throw new Exception("图像源没有正式字段投影");
            sourcePolicy.FilePaths=new(){imageFile};sourcePolicy.CurrentIndex=0;sourcePolicy.ForRunPaths=new(){imageFile};sourcePolicy.SNInitialValue=1;sourcePolicy.ApplyTo(source);
            vm.AddNodeToWorkflow(source);

        GlobalVariable? uiVariable=null;
        if(Environment.GetEnvironmentVariable("S4_EDITOR_UI_ONLY")=="1")
        {
            var host=solutions.Session!.Editing.Host;var variables=host.BeginVariableEdit();
            uiVariable=new GlobalVariable("验收阈值",77d,"Double","验收参数");
            variables.Configuration.GlobalVariables.Add(uiVariable);
            host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variables);
        }
        var first=Open();
        if(Environment.GetEnvironmentVariable("S4_EDITOR_UI_ONLY")=="1")
        {
            var formalOpening=node.RuntimeConfiguration!;
            var savedOpening=File.ReadAllBytes(path);
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            var tabs=Logical(Control(first)).OfType<TabControl>().Single();
            var input=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(field=>field.FieldId==BlobTool.ImageInput);
            var combo=(ComboBox)input.InputElement!;
            Case("S1真实MainWindow打开基本页与正式来源下拉",()=>
            {
                Require(tabs.SelectedIndex==0&&combo.IsLoaded&&combo.IsVisible,"实际参数窗未从基本页打开或来源控件未加载");
                Require(solutions.Session!.Editing.RegisteredScopeCount==1,"实际窗口未注册唯一编辑范围");
                Require(combo.Items.OfType<ComboBoxItem>().Any(item=>item.Tag is ReferenceSetting reference
                    &&reference.Reference.NodeId==source.Id&&reference.Reference.PortId=="output.image"),"正式图像源输出没有进入真实下拉");
                Capture(first,"runtime-editor-ui-stage.png");
            });
            Case("S1来源选择仅写私有字段且展示中文声明",()=>
            {
                combo.IsDropDownOpen=true;Pump();
                var choice=combo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ReferenceSetting reference
                    &&reference.Reference.NodeId==source.Id&&reference.Reference.PortId=="output.image");
                combo.SelectedItem=choice;combo.IsDropDownOpen=false;Pump();
                Require(context.DraftNode.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is ReferenceSetting selected
                    &&selected.Reference.NodeId==source.Id&&selected.Reference.PortId=="output.image"
                    &&node.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is UnsetSetting,"真实来源选择未写私有草稿或污染正式配置");
                Require(choice.Content?.ToString()?.Contains("图像",StringComparison.Ordinal)==true
                    &&choice.Content?.ToString()?.Contains("output.image",StringComparison.Ordinal)!=true,"来源下拉没有消费正式中文显示声明");
            });
            Case("真实Blob阈值仍为明确S1占位，不能冒充变量功能链已接入",()=>
            {
                Threshold(first);var field=Logical(Control(first)).OfType<DeclaredFieldsView>().Single().Fields.Single(item=>item.FieldId==threshold);
                Require(!field.View.IsLoaded&&!field.View.IsVisible,"原正式阈值字段已经呈现，必须改为真实功能接纳验收，不能沿用占位结论");
                Require(Visual(Control(first)).OfType<FrameworkElement>().Any(element=>element.GetType().Name=="NativeThresholdUiDraftControls"&&element.IsVisible),"可见页没有明确S1阈值组合");
                Require(solutions.Session!.Editing.Host.GetVariableNames(solutions.CurrentSolution!.Id).ContainsKey(uiVariable!.Id)
                    &&node.RuntimeConfiguration!.Fields.Read(threshold) is ConstantSetting
                    &&context.DraftNode.RuntimeConfiguration!.Fields.Read(threshold) is ConstantSetting,"候选准备或页面显示擅自写入变量引用");
                Capture(first,"runtime-editor-variable-boundary.png");tabs.SelectedIndex=0;Pump();
            });
            Case("S1纯UI阻止调试与确定，不取得算法运行租约",()=>
            {
                var before=Context().GetNodeResult(node.Id)?.Result;
                var task=Control(first).RunDebugAsync();PumpUntil(()=>task.IsCompleted);
                var rejected=false;
                try{task.GetAwaiter().GetResult();}
                catch(InvalidOperationException failure) when(failure.Message.Contains("界面试用",StringComparison.Ordinal)){rejected=true;}
                Require(rejected&&context.DebugOwner.ActiveRuns==0&&ReferenceEquals(before,Context().GetNodeResult(node.Id)?.Result),
                    "纯UI调试未明确拒绝、遗留运行租约或修改正式结果");
                Require(!first.TryConfirm(out var error)&&error.Contains("界面试用",StringComparison.Ordinal)
                    &&first.IsVisible&&ReferenceEquals(node.RuntimeConfiguration,formalOpening),"纯UI确定未拒绝或提前关闭/发布");
            });
            Case("S1未接入界面状态阻断保存，磁盘与正式字段保持不变",()=>
            {
                vm.SaveCurrentSolutionCommand.Execute(null);Pump();
                Require(savedOpening.SequenceEqual(File.ReadAllBytes(path))
                    &&ReferenceEquals(node.RuntimeConfiguration,formalOpening),"S1界面试用修改写入文件或正式配置");
            });
            var retiredDraft=context.DraftNode;var retiredConfiguration=retiredDraft.RuntimeConfiguration;
            first.Close();Pump();
            var reopened=Open();var reopenedContext=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(reopened))!;
            Case("S1取消重开回读正式配置且旧来源控件不能写新会话",()=>
            {
                Require(!context.IsValid&&solutions.Session!.Editing.RegisteredScopeCount==1
                    &&reopenedContext.DraftNode.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is UnsetSetting,"旧范围未退役或重开继承了已取消的来源");
                combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().FirstOrDefault(item=>item.Tag is ReferenceSetting reference
                    &&reference.Reference.NodeId==source.Id&&reference.Reference.PortId=="output.gray-image");Pump();
                Require(ReferenceEquals(retiredDraft.RuntimeConfiguration,retiredConfiguration)
                    &&reopenedContext.DraftNode.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is UnsetSetting
                    &&ReferenceEquals(node.RuntimeConfiguration,formalOpening),"退役来源回调写入旧草稿、新范围或正式配置");
                Require(Logical(Control(reopened)).OfType<TabControl>().Single().SelectedIndex==0,"重开未回到基本参数页");
            });
            reopened.Close();Pump();
            Require(solutions.Session!.Editing.RegisteredScopeCount==0,"真实窗口全部关闭后编辑范围未清空");
            shell.Close();dialogs.Stop();
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"ui-stage-report.json"),
                System.Text.Json.JsonSerializer.Serialize(new{stage="S1Ui",passed=_passed,fullFunctionalAcceptance=false,
                    remaining=new[]{"阈值及控件功能执行","正式功能确认与保存重开","实际VS与人工验收"}}));
            Console.WriteLine($"S1真实编辑窗口UI链：{_passed}组通过；fullFunctionalAcceptance=false，旧RuntimeEditor功能用例保留，未运行Blob算法。");
            return 0;
        }
        Case("默认UI工厂创建Runtime节点，真实MainWindow打开中立窗口与58个XML字段",()=>
        {
            Require(node.RuntimeConfiguration?.SettingsType==typeof(BlobSettings)&&first.EditorControl is null
                &&Control(first).GetType()==typeof(RuntimeNodeConfigurationControl),"窗口借用SDK Blob参数或未保留Runtime配置");
            Require(Logical(Control(first)).OfType<DeclaredFieldsView>().Single().Fields.Select(field=>field.FieldId).Distinct().Count()==54&&Logical(Control(first)).OfType<DeclaredInputFieldsView>().Single().Fields.Count==4,"XML输入及设置字段未进入真实窗口");
            Require(solutions.Session!.Editing.RegisteredScopeCount==1,"实际窗口未登记文档事务");
            // The XAML design page may be saved on another tab; a real editor
            // must still open on the image input page.
            Require(Logical(Control(first)).OfType<TabControl>().Single().SelectedIndex==0,
                "实际Blob窗口未从基本参数和输入源开始");
            first.UpdateLayout();Pump();
            var sourceButtons=Visual(Control(first)).OfType<AppsPopup>().Where(b=>b.Name=="rt").ToArray();
            Require(sourceButtons.Length==3&&sourceButtons.All(b=>Visual(b).OfType<Image>().Any(i=>i.Source is BitmapSource bitmap&&bitmap.PixelWidth>0&&i.ActualWidth>0)),
                "来源按钮没有实际显示迁入图标");
            var image=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(f=>f.FieldId==BlobTool.ImageInput);
            Require(image.InputElement is ComboBox combo&&combo.Resources.MergedDictionaries.Any(dictionary=>dictionary.Source?.OriginalString.EndsWith("SourceComboBoxes.xaml")==true)
                &&!Logical(image).OfType<Button>().Any(),"图像输入源未使用VM EnumerationG原ComboBox，或仍有链/清除按钮");
            Require(Visual(image).OfType<Image>().Any(icon=>icon.Source is BitmapSource bitmap&&bitmap.PixelWidth>0&&icon.ActualWidth>0),
                "VM输入源右下角原三角图标未实际渲染");
            Capture(first,"runtime-editor-basic.png");
        });
        Case("捕获回调同步重绑拒绝旧结果，不把旧配置写入新节点",()=>
        {
            var control=Control(first);var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(control)!;
            var replacement=context.DraftNode.Clone();
            var configuration=replacement.RuntimeConfiguration;var bindings=configuration!.Fields;
            var field=typeof(RuntimeNodeConfigurationControl).GetField("_view",BindingFlags.Instance|BindingFlags.NonPublic)!;
            var inner=(IToolSettingsView)field.GetValue(control)!;
            field.SetValue(control,new CaptureRebindView(inner,()=>control.BindDraft(replacement)));
            Require(!control.TryCapture(out var error)&&error.Contains("重新绑定"),"旧捕获没有拒绝同步重绑");
            Require(ReferenceEquals(replacement.RuntimeConfiguration,configuration)&&ReferenceEquals(replacement.RuntimeConfiguration!.Fields,bindings),
                "旧捕获污染新节点配置或绑定");
            control.BindDraft(context.DraftNode);Pump();
        });
        Case("VM图像输入源下拉从正式声明选择，未确认引用只写私有节点",()=>
        {
            source.IsSelected=true;vm.SelectedNode=source;
            var sourcePanel=vm.ActiveInputSource??throw new Exception("正式输入源面板未建立");
            Require(sourcePanel.CapturePolicy().FilePaths.SequenceEqual(new[]{imageFile})&&sourcePanel.CurrentIndex==0,
                "真实图像源面板没有恢复Runtime正式列表和起点");
            source.IsSelected=false;node.IsSelected=true;vm.SelectedNode=node;Pump();
            // The preceding rebind recreates the real view with the user's saved page index.
            // Exercise the source on its visible page; hidden widgets must not accept UI events.
            Logical(Control(first)).OfType<TabControl>().Single().SelectedIndex=0;first.UpdateLayout();Pump();
            var input=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(f=>f.FieldId==BlobTool.ImageInput);
            oldInputCombo=(ComboBox)input.InputElement!;
            var combo=(ComboBox)input.InputElement!;Require(combo.IsVisible&&combo.IsLoaded,"图像来源实际页未显示");combo.IsDropDownOpen=true;Pump();
            Require(!app.Windows.OfType<Window>().Any(w=>w.Title=="选择绑定来源"),"图像下拉仍打开通用订阅窗口");
            Require(ImageBinding(((ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!).DraftNode) is null,
                "刷新候选擅自写入了来源");
            combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().First(item=>item.Tag is ReferenceSetting reference&&reference.Reference.NodeId==source.Id
                &&reference.Reference.MemberFieldPath.Count==0&&reference.Reference.Type.Resource==ResourceKind.Image);
            combo.IsDropDownOpen=false;Pump();
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            Require(ImageBinding(context.DraftNode)?.Reference.NodeId==source.Id
                &&node.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is UnsetSetting,"来源下拉未写草稿或污染正式引用");
        });
        Case("图像下拉拒绝真实自引用并恢复原草稿，刷新不改引用，修正后可继续",()=>
        {
            var input=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(field=>field.FieldId==BlobTool.ImageInput);
            var combo=(ComboBox)input.InputElement!;var priorItem=(ComboBoxItem)combo.SelectedItem;
            var reference=((ReferenceSetting)priorItem.Tag).Reference;
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            var expression=ImageBinding(context.DraftNode)!;
            var invalid=new ComboBoxItem{Content="自引用拒绝检查",Tag=new ReferenceSetting(new OutputReference(node.Id,BlobSchema.Field(5),
                TypeRef.ResourceOf(ResourceKind.Image),scopeId:reference.ScopeId))};
            combo.Items.Add(invalid);combo.SelectedItem=invalid;Pump();
            Require(input.HasInvalidText&&ReferenceEquals(combo.SelectedItem,priorItem)
                &&Equals(ImageBinding(context.DraftNode),expression),
                "自引用未被生产合同拒绝或污染了原来源");
            combo.Items.Remove(invalid);
            Require(!combo.Items.OfType<ComboBoxItem>().Any(item=>item.Tag is UnsetSetting),"必填图像来源不应提供清除项");
            combo.IsDropDownOpen=true;Pump();
            var validItem=combo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ReferenceSetting candidate&&candidate.Reference.NodeId==reference.NodeId
                &&candidate.Reference.PortId==reference.PortId&&candidate.Reference.ScopeId==reference.ScopeId&&candidate.Reference.MemberFieldPath.SequenceEqual(reference.MemberFieldPath));
            validItem.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left)
                {RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});Pump();
            Require(!input.HasInvalidText,"修正来源后仍无法继续");
            combo.IsDropDownOpen=true;Pump();combo.IsDropDownOpen=false;
            Require(Equals(ImageBinding(context.DraftNode),expression)
                &&node.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is UnsetSetting,"刷新候选写入了草稿或正式节点");
        });
        Case("真实未失焦数值在捕获时进入私有Runtime草稿，隐藏窗口保存后可重开",()=>
        {
            var text=Threshold(first);text.Focus();text.Text="73";Pump();
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            Require(Formal()==128&&text.Text=="73","未提交输入污染正式设置或丢失原文");
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
            var prior=ImageBinding(context.DraftNode)!;
            var combo=oldInputCombo!;
            var stale=new ComboBoxItem{Content="迟到旧来源",Tag=new ReferenceSetting(new OutputReference("stale-node",BlobSchema.Field(5),TypeRef.ResourceOf(ResourceKind.Image)))};
            combo.Items.Add(stale);combo.SelectedItem=stale;Pump();
            Require(Equals(ImageBinding(context.DraftNode),prior),"旧控件事件写入了新草稿");
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
            PumpUntil(()=>context.DebugResults.GetNodeResult(node.Id)?.Result is DeclaredOutputSnapshot);
            var preview=(DeclaredOutputSnapshot)context.DebugResults.GetNodeResult(node.Id)!.Result!;
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
            var prior=context.DraftNode.RuntimeConfiguration!;
            Logical(Control(first)).OfType<TabControl>().Single().SelectedIndex=0;first.UpdateLayout();Pump();
            var input=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(f=>f.FieldId==BlobTool.ImageInput);
            var sourceCombo=(ComboBox)input.InputElement!;
            Require(sourceCombo.IsVisible&&sourceCombo.IsLoaded,"解绑来源实际页未显示");
            Require(!sourceCombo.Items.OfType<ComboBoxItem>().Any(item=>item.Tag is UnsetSetting),"必填图像错误提供清除项");
            try{context.DraftNode.RuntimeConfiguration=prior.WithFieldSettings(new Dictionary<string,ParameterSetting>{{BlobTool.ImageInput,new UnsetSetting()}});throw new Exception("必填来源竟然可以清空");}
            catch(ArgumentException){}
            Require(ReferenceEquals(prior,context.DraftNode.RuntimeConfiguration),"拒绝清除污染原配置");
            context.DraftNode.RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettingsFields(prior.Fields.Select(prior.SettingsSchema.Inputs));
            Control(first).BindDraft(context.DraftNode);Pump();
            Require(context.DraftNode.RuntimeConfiguration.Fields.Read(BlobTool.ImageInput) is UnsetSetting,"缺输入的独立配置被补默认来源");
            var failed=Control(first).RunDebugAsync();PumpUntil(()=>failed.IsCompleted);
            try{failed.GetAwaiter().GetResult();throw new Exception("缺少来源的调试竟然成功");}catch(InvalidOperationException){}
            Require(context.DebugResults.GetNodeResult(node.Id) is null&&table.Items.Count==0&&ReferenceEquals(formal,Context().GetNodeResult(node.Id)!.Result),"失败未清空私有预览或污染正式结果");
            context.DraftNode.RuntimeConfiguration=prior;Control(first).BindDraft(context.DraftNode);Pump();
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
    private sealed class CaptureRebindView(IToolSettingsView inner,Action rebind):IToolSettingsView
    {
        private bool _called;
        public FrameworkElement View=>inner.View;
        public bool HasInvalidInput=>inner.HasInvalidInput;
        public bool HasPendingEdits=>inner.HasPendingEdits;
        public long AcceptedRevision=>inner.AcceptedRevision;
        public IReadOnlyList<string> AcceptedFieldOrder=>inner.AcceptedFieldOrder;
        public FieldSettings CaptureAcceptedFields()=>inner.CaptureAcceptedFields();
        public event Action? Changed {add=>inner.Changed+=value;remove=>inner.Changed-=value;}
        public bool TryCapture(out FieldSettings settings,out string error)
        {
            var result=inner.TryCapture(out settings,out error);
            if(!_called){_called=true;rebind();}return result;
        }
        public void Dispose()=>inner.Dispose();
    }
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
