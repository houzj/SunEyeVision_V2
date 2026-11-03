using SunEyeVision.Plugin.Wpf.Adapters.References;
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
        var result=1;
        var thread=new Thread(()=>
        {
            try{EnterHiddenDesktop();result=Run();}catch(Exception error){Console.Error.WriteLine(error);}
            finally{if(_desktop!=IntPtr.Zero)CloseDesktop(_desktop);}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();return result;
    }
    private static IntPtr _desktop;
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
    private static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]private static extern bool SetThreadDesktop(IntPtr desktop);
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]private static extern bool CloseDesktop(IntPtr desktop);
    private static void EnterHiddenDesktop()
    {
        // Never switch the user's desktop. All owned WPF HWNDs remain on this isolated desktop.
        _desktop=CreateDesktop("SunEyeVision-B08-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);
        if(_desktop==IntPtr.Zero||!SetThreadDesktop(_desktop))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"独立隐藏桌面不可用；不回退到用户桌面。");
        Console.WriteLine("B08隐藏桌面已建立，不切换用户桌面。");
    }
    private static int Run()
    {
        var only=Environment.GetEnvironmentVariable("C07_CASE")??"All";Require(only is "All" or "Targets" or "Lifetime" or "Reopen","未知C07筛选");
        var culture=Environment.GetEnvironmentVariable("C07_CULTURE")??"zh-CN";Require(culture is "zh-CN" or "en-US","未知C07语言");System.Globalization.CultureInfo.CurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo(culture);
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
        SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow Open()=>OpenFor(node);
        SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow OpenFor(WorkflowNodeBase target)
        {
            vm.OpenDebugWindowCommand.Execute(target);Pump();
            var window=app.Windows.OfType<SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow>().SingleOrDefault(w=>w.IsVisible)??throw new Exception("实际打开命令没有创建Runtime窗口");
            window.UpdateLayout();Pump();return window;
        }
        RuntimeNodeConfigurationControl Control(SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow window)=>window.EditorControl;
        TextBox Threshold(SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow window)
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


        var flow=solutions.CurrentSolution!.Workflows.Single(w=>w.Id==vm.WorkflowTabViewModel.SelectedTab!.RootWorkflowId);
        var edge=new WorkflowConnection{Id=Guid.NewGuid().ToString(),SourceNodeId=source.Id,TargetNodeId=node.Id};Require(vm.AddConnectionToWorkflow(edge),"正式控制边不能建立");
        var history=solutions.Session!.History;var host=solutions.Session.Editing.Host;
        var variableEdit=host.BeginVariableEdit();var values=new GlobalVariable("真实阈值数组",new long[]{173,90,37,60,70,80,100,110,111},"Integer[]");var indexVariable=new GlobalVariable("真实索引",1L,"Integer");variableEdit.Configuration.GlobalVariables.Add(values);variableEdit.Configuration.GlobalVariables.Add(indexVariable);host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variableEdit);
        var setup=Open();var setupControl=Control(setup);var imageField=Logical(setupControl).OfType<DeclaredInputFieldsView>().Single().Fields.Single(f=>f.FieldId==BlobTool.ImageInput);var imageCombo=(ComboBox)imageField.InputElement!;
        imageCombo.SelectedItem=imageCombo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ReferenceSetting r&&r.Reference.NodeId==source.Id&&r.Reference.PortId=="output.image"&&r.Reference.MemberFieldPath.SequenceEqual(new[]{"data"}));Pump();Require(setup.TryConfirm(out var error),error);if(setup.IsVisible)setup.Close();Pump();Require(solutions.SaveCurrentDocument(path).FileWritten,"基准方案保存失败");
        var initialRun=vm.RunSelectedImageAsync();PumpUntil(()=>initialRun.IsCompleted);initialRun.GetAwaiter().GetResult();Require(Context().GetNodeResult(source.Id)?.Result is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {IsSuccess:true},"正式上游未产生本次源快照");
        DeclaredFieldAccess Low(SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow window){Threshold(window);return Logical(Control(window)).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==threshold);}
        ProductionEditorContext Draft(SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow window)=>(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(window))!;
        void Debug(SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow window,long expected)
        {
            long? actual=null;var current=Control(window);
            SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler observe=(_,args)=>{if(args.Results is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {Output:BlobOutput output})actual=output.ThresholdMinUsed;};
            current.AddHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observe);
            try{var run=current.RunDebugAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();Require(actual==expected,"真实算法未消费预期阈值："+actual+"/"+expected);Require(Draft(window).DraftNode.RuntimeConfiguration!.ReadCurrentParameter(threshold) is {Value:long value}&&value==expected,"私有当前值与算法不同");}
            finally{current.RemoveHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observe);}
        }
        void Confirm(SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow window){Require(window.TryConfirm(out var issue),"真实确认失败："+issue);if(window.IsVisible)window.Close();Pump();}
        long Current()=>node.RuntimeConfiguration!.ReadCurrentParameter(threshold)?.Value is long value?value:node.RuntimeConfiguration.Fields.Read(threshold) is ConstantSetting constant?(long)constant.Value!:throw new Exception("没有正式当前值");
        string Identity(ParameterSetting setting)=>setting switch{ReferenceSetting reference=>SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(reference.Reference),VariableSetting variable=>SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(variable.Reference),ConstantSetting constant=>System.Text.Json.JsonSerializer.Serialize(constant.Value),UnsetSetting=>"Unset",_=>throw new Exception("不是支持设置")};
        int? expectedReplayCount=null;
        void SaveCheck(Func<ParameterSetting,bool> predicate,long expected)
        {
            var identity=Identity(node.RuntimeConfiguration!.Fields.Read(threshold));Require(solutions.SaveCurrentDocument(path).FileWritten,"正式保存失败");
            var loaded=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("真实文件重开失败");var loadedFlow=loaded.Workflows.Single(w=>w.Id==flow.Id);var configuration=loadedFlow.Nodes.Single(n=>n.Id==node.Id).RuntimeConfiguration!;
            Require(predicate(configuration.Fields.Read(threshold))&&Identity(configuration.Fields.Read(threshold))==identity&&configuration.ReadCurrentParameter(threshold) is {Value:long actual}&&actual==expected,"保存重开丢完整Selection/身份/当前值");
            var ownEngine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);ownEngine.RegisterWorkflow(loadedFlow);var ownHost=new NodeConfigurationEditHost(loaded);var publication=WorkflowPublishedConfiguration.Capture(ownHost.Capture(),loaded,ownHost);
            using var globals=new WorkflowGlobalRunValues(loaded.Id,publication.GlobalDefinitions,publication.GlobalDefinitions.Select(variable=>new KeyValuePair<string,object?>(variable.Id,variable.Value)));
            var ownExecutor=new WorkflowExecutionEngine(ownEngine,new PluginManager(),SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);
            var replay=ownExecutor.ExecuteWorkflowAsync(new WorkflowRunRequest(loadedFlow.Id,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{source.Id,new ImageSourceRunInput(imageFile,1){LocalReadAdmission=_=>imageFile}}}){Configuration=publication.Configuration,ExecutionOwner=publication.ExecutionOwner,ProducedGlobals=globals,ParameterValues=publication.ParameterValues,ResourcePaths=publication.ResourcePaths});
            PumpUntil(()=>replay.IsCompleted);using(var run=replay.GetAwaiter().GetResult()){Require(run.Success&&run.NodeResults[node.Id].Result is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {Output:BlobOutput output}&&output.ThresholdMinUsed==expected&&(expectedReplayCount is null||output.Count==expectedReplayCount),"重开配置未按完整关系实际执行");}loaded.ExecutionOwner.Retire();
        }
        Case("正式前序成员来源真实选择、私有消费与取消不污染",()=>{
            var before=node.RuntimeConfiguration;var bytes=File.ReadAllBytes(path);var position=history.Position;var window=Open();var field=Low(window);Subscribe(field);var session=Session(field);Choose(session,false,item=>Candidate(item)?.Reference(BindingSelection.All) is {} reference&&reference.NodeId==source.Id&&reference.PortId=="output.image"&&reference.MemberFieldPath.SequenceEqual(new[]{"width"}));
            Require(field.Setting is ReferenceSetting selected&&selected.Reference.MemberFieldPath.SequenceEqual(new[]{"width"}),"WPF没有保留正式成员身份");Debug(window,8);Require(ReferenceEquals(before,node.RuntimeConfiguration)&&history.Position==position,"私有运行提前发布");
            var oldContext=Draft(window);var oldNode=oldContext.DraftNode;var oldConfiguration=oldNode.RuntimeConfiguration;var oldIdentity=Identity(oldConfiguration!.Fields.Read(threshold));
            var oldView=(IToolSettingsView)typeof(RuntimeNodeConfigurationControl).GetField("_view",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(Control(window))!;
            long? ReadOldRevision(){try{return oldView.AcceptedRevision;}catch(InvalidOperationException failure)when(failure.Message.Contains("settings draft",StringComparison.OrdinalIgnoreCase)){return null;}}
            window.Close();Pump();var retiredRevision=ReadOldRevision();Require(retiredRevision is null,"关闭后旧视图仍可读活跃草稿Revision");Require(!oldContext.IsValid&&ReferenceEquals(before,node.RuntimeConfiguration)&&bytes.SequenceEqual(File.ReadAllBytes(path)),"取消改正式或磁盘");
            ((ICommand)Get(session,"ClickCommand")).Execute(_lastChoice);Require(ReferenceEquals(before,node.RuntimeConfiguration)&&ReferenceEquals(oldConfiguration,oldNode.RuntimeConfiguration)&&Identity(oldNode.RuntimeConfiguration!.Fields.Read(threshold))==oldIdentity&&ReadOldRevision()==retiredRevision&&history.Position==position,"旧来源回调污染正式/旧草稿/代次/历史");
        });
        Case("成员来源调试确认、历史与真实文件重开",()=>{
            var window=Open();var field=Low(window);Subscribe(field);Choose(Session(field),false,item=>Candidate(item)?.Reference(BindingSelection.All) is {} reference&&reference.NodeId==source.Id&&reference.MemberFieldPath.SequenceEqual(new[]{"width"}));Debug(window,8);Confirm(window);Require(Current()==8&&node.RuntimeConfiguration!.Fields.Read(threshold) is ReferenceSetting,"确认丢关系或值");SaveCheck(setting=>setting is ReferenceSetting r&&r.Reference.NodeId==source.Id&&r.Reference.MemberFieldPath.SequenceEqual(new[]{"width"}),8);
            history.Undo();Require(node.RuntimeConfiguration!.Fields.Read(threshold) is ConstantSetting,"Undo未恢复常量");history.Redo();Require(node.RuntimeConfiguration!.Fields.Read(threshold) is ReferenceSetting,"Redo未恢复关系");Console.WriteLine("历史只恢复配置，重新消费前当前值="+Current());window=Open();Debug(window,8);Confirm(window);SaveCheck(setting=>setting is ReferenceSetting r&&r.Reference.NodeId==source.Id&&r.Reference.MemberFieldPath.SequenceEqual(new[]{"width"}),8);
        });
        foreach(var selection in new[]{"All","Fixed","AtField","AtVariable"})Case("原模态变量来源及"+selection+"索引完整消费确认重开",()=>{
            var window=Open();var field=Low(window);Subscribe(field);var session=Session(field);SelectVariable(session,false,values.Id);
            long expected=173;
            if(selection=="Fixed"){SetIndex(session,"1");expected=90;}
            if(selection=="AtField"){Choose(session,true,item=>Candidate(item)?.Reference(BindingSelection.All) is {} reference&&reference.NodeId==source.Id&&reference.MemberFieldPath.SequenceEqual(new[]{"width"}));expected=111;}
            if(selection=="AtVariable"){SelectVariable(session,true,indexVariable.Id);expected=90;}
            Require(field.Setting is VariableSetting variable&&variable.Reference.VariableId==values.Id,"主变量身份丢失");var actualSelection=((VariableSetting)field.Setting).Reference.Selection;Require(selection switch{"All"=>actualSelection is BindingSelection.AllItems,"Fixed"=>actualSelection is BindingSelection.AtConstant {Index:1},"AtField"=>actualSelection is BindingSelection.AtField at&&at.Reference.NodeId==source.Id&&at.Reference.PortId=="output.image"&&at.Reference.MemberFieldPath.SequenceEqual(new[]{"width"}),_=>actualSelection is BindingSelection.AtVariable at&&at.Reference.VariableId==indexVariable.Id},"WPF完整Selection身份不符："+selection);Debug(window,expected);Confirm(window);Require(Current()==expected,"确认未接纳私有消费值");SaveCheck(setting=>setting is VariableSetting variable&&variable.Reference.VariableId==values.Id,expected);
        });
        Case("实际捕获后变量变化仅影响下一次私有运行",()=>{
            var before=node.RuntimeConfiguration;var window=Open();var field=Low(window);var session=Session(field);SelectVariable(session,false,values.Id);Choose(session,true,item=>(bool)Get(item,"Clear"));Require(((VariableSetting)field.Setting).Reference.Selection is BindingSelection.AllItems,"真实清索引命令未恢复All");
            var live=solutions.CurrentSolution!.GlobalVariables.Single(variable=>variable.Id==values.Id);var original=(long[])live.Value!;var current=Control(window);
            var capture=(Func<WorkflowNodeBase,RuntimeDebugInvocation>)typeof(RuntimeNodeConfigurationControl).GetField("_captureDebug",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(current)!;
            current.SetDebugCapture(draftNode=>{var frozen=capture(draftNode);var next=(long[])original.Clone();next[0]=99;live.Value=next;return frozen;});
            try{Debug(window,173);current.SetDebugCapture(capture);Debug(window,99);window.Close();Pump();Require(ReferenceEquals(before,node.RuntimeConfiguration),"捕获后变化/取消污染正式配置");}
            finally{current.SetDebugCapture(capture);live.Value=original;if(window.IsVisible)window.Close();Pump();}
        });
        Case("固定索引越界失败不发布，合法索引可恢复",()=>{
            var before=node.RuntimeConfiguration;var window=Open();var field=Low(window);var session=Session(field);SetIndex(session,"99");var task=Control(window).RunDebugAsync();PumpUntil(()=>task.IsCompleted);var rejected=false;try{task.GetAwaiter().GetResult();}catch(Exception failure)when(failure is ArgumentException or InvalidOperationException){rejected=failure.ToString().Contains("变量索引超出本次序列范围",StringComparison.Ordinal);Console.WriteLine("合法模型索引99消费诊断："+failure.Message);}Require(rejected&&ReferenceEquals(before,node.RuntimeConfiguration),"未得到正式序列越界诊断或正式半写");field=Low(window);session=Session(field);SetIndex(session,"1");Require(field.Setting is VariableSetting restored&&restored.Reference.VariableId==values.Id&&restored.Reference.Selection is BindingSelection.AtConstant {Index:1},"恢复索引后完整Selection不符");Debug(window,90);window.Close();Pump();Require(ReferenceEquals(before,node.RuntimeConfiguration),"失败后取消污染正式");
        });
        Case("控制断边保留身份且真实窗口拒绝，Undo恢复可执行",()=>{
            var window=Open();var field=Low(window);Choose(Session(field),false,item=>Candidate(item)?.Reference(BindingSelection.All) is {} reference&&reference.NodeId==source.Id&&reference.MemberFieldPath.SequenceEqual(new[]{"width"}));Debug(window,8);Confirm(window);var before=SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(((ReferenceSetting)node.RuntimeConfiguration!.Fields.Read(threshold)).Reference);
            vm.CurrentCommandManager!.Execute(new SunEyeVision.UI.Features.WorkflowUI.Commands.BatchDeleteNodesAndConnectionsCommand(flow.Nodes,flow.Connections,new(),new(){edge}));Pump();window=Open();Require(!Control(window).TryCapture(out _),"断边来源仍可捕获");Require(SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(((ReferenceSetting)node.RuntimeConfiguration!.Fields.Read(threshold)).Reference)==before,"断边丢引用");window.Close();Pump();history.Undo();window=Open();Debug(window,8);window.Close();Pump();
        });
        Case("原非模态窗树组/单选不写，四字段目标与范围两端共享同窗",()=>{var before=node.RuntimeConfiguration;var bytes=File.ReadAllBytes(path);var w=Open();var field=Low(w);Subscribe(field);var session=Session(field);var expanded=Expanded(session,false);var root=Root(w);var tree=ExpandedTree(expanded,false);var group=Tree((System.Collections.IEnumerable)root.SourceNodes).First(item=>((System.Collections.IEnumerable)Get(item,"EnumEntrysA")).Cast<object>().Any());var identity=Identity(field.Setting);DoubleClick(tree,group);Require(Identity(field.Setting)==identity,"树组双击写入字段");var leaf=Tree((System.Collections.IEnumerable)root.SourceNodes).Single(item=>Candidate(item)?.Reference(BindingSelection.All) is {} r&&r.NodeId==source.Id&&r.PortId=="output.image"&&r.MemberFieldPath.SequenceEqual(new[]{"width"}));Container(tree,leaf).IsSelected=true;Pump();Require(Identity(field.Setting)==identity,"原树单选被当作接纳");DoubleClick(tree,leaf);var old=root.CurrentRequest!;var staleNode=Tree((System.Collections.IEnumerable)root.SourceNodes).Single(item=>Candidate(item)?.Reference(BindingSelection.All)is{} r&&r.NodeId==source.Id&&r.MemberFieldPath.SequenceEqual(new[]{"width"}));var staleContainer=Container(tree,staleNode);Capture(expanded,"expanded-source-"+culture+".png");Require(ReferenceEquals(staleContainer.DataContext,staleNode),"没有缓存真实旧容器");
            var fields=Logical(Control(w)).OfType<DeclaredFieldsView>().Single().Fields;foreach(var id in new[]{BindingId("FindNum"),BindingId("MinArea"),BindingId("MaxArea")}){var current=fields.Single(f=>f.FieldId==id);Subscribe(current);var currentSession=Session(current);var same=Expanded(currentSession,false);Require(ReferenceEquals(same,expanded)&&!old.IsValid&&root.CurrentRequest is{IsValid:true} next&&next.Target.Id==id,"切字段重建窗或保旧request/写错端点");var targetRequest=root.CurrentRequest;var targetIdentity=Identity(current.Setting);Require(!root.CurrentRequest!.ClickCommand.CanExecute(staleNode)&&!old.ClickCommand.CanExecute(staleNode),"新旧request错误接纳旧节点");ReplayOldContainer(tree,staleContainer,staleNode,root);old.ClickCommand.Execute(staleNode);Require(Identity(current.Setting)==targetIdentity&&ReferenceEquals(root.CurrentRequest,targetRequest)&&expanded.IsVisible,"旧容器双击写/关新目标");Choose(currentSession,false,item=>Candidate(item)?.Reference(BindingSelection.All) is {} r&&r.NodeId==source.Id&&r.PortId=="output.image"&&r.MemberFieldPath.SequenceEqual(new[]{"width"}));Require(current.Setting is ReferenceSetting value&&value.Reference.NodeId==source.Id&&value.Reference.MemberFieldPath.SequenceEqual(new[]{"width"}),"双击未写当前真实字段");old=root.CurrentRequest!;var newLeaf=Tree((System.Collections.IEnumerable)root.SourceNodes).First(item=>Candidate(item)is not null);staleContainer=Container(ExpandedTree(expanded,false),newLeaf);staleNode=newLeaf;}
            var editorControl=Control(w);BlobOutput? output=null;SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler observed=(_,args)=>{if(args.Results is DeclaredOutputSnapshot{Output:BlobOutput result})output=result;};editorControl.AddHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observed);try{var run=editorControl.RunDebugAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();Require(output is{Count:0}&&Draft(w).DraftNode.RuntimeConfiguration!.ReadCurrentParameter(BindingId("MinArea")) is{Value:8L}&&Draft(w).DraftNode.RuntimeConfiguration!.ReadCurrentParameter(BindingId("MaxArea")) is{Value:8L},"展开两端未实际筛掉64像素区域");}finally{editorControl.RemoveHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observed);}w.Close();Pump();Require(ReferenceEquals(before,node.RuntimeConfiguration)&&bytes.SequenceEqual(File.ReadAllBytes(path)),"切目标/取消污染正式或文件");var saving=Open();Threshold(saving);var ids=new[]{threshold,BindingId("FindNum"),BindingId("MinArea"),BindingId("MaxArea")};foreach(var id in ids){var currentField=Logical(Control(saving)).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==id);Subscribe(currentField);Choose(Session(currentField),false,item=>Candidate(item)?.Reference(BindingSelection.All)is{} r&&r.NodeId==source.Id&&r.PortId=="output.image"&&r.MemberFieldPath.SequenceEqual(new[]{"width"}));}Debug(saving,8);var expectedFields=ids.ToDictionary(id=>id,id=>Identity(Draft(saving).DraftNode.RuntimeConfiguration!.Fields.Read(id)));Confirm(saving);expectedReplayCount=0;try{SaveCheck(value=>value is ReferenceSetting r&&r.Reference.NodeId==source.Id,8);}finally{expectedReplayCount=null;}var reopened=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("四字段重开失败");var restored=reopened.Workflows.Single(f=>f.Id==flow.Id).Nodes.Single(n=>n.Id==node.Id).RuntimeConfiguration!;foreach(var id in ids)Require(Identity(restored.Fields.Read(id))==expectedFields[id]&&restored.ReadCurrentParameter(id)is{Value:8L},"展开窗四字段持久身份/值丢失："+id);reopened.ExecutionOwner.Retire();});
        Case("Owner跟随/反向移动及隐藏关闭退役，不改变偏好或模型",()=>{var before=node.RuntimeConfiguration;var w=Open();var field=Low(w);Subscribe(field);var session=Session(field);var window=Expanded(session,false);var root=Root(w);var request=root.CurrentRequest!;var leaf=Tree((System.Collections.IEnumerable)request.SourceNodes).First(item=>Candidate(item)is not null);var oldContainer=Container(ExpandedTree(window,false),leaf);var oldPosition=history.Position;var oldPreference=root.PreferExpanded;w.Left+=31;w.Top+=17;w.Height+=20;w.UpdateLayout();Pump();Require(Math.Abs(window.Left-(w.Left+w.ActualWidth))<1&&Math.Abs(window.Top-w.Top)<1&&Math.Abs(window.Height-w.ActualHeight)<1,"原窗未跟随Owner");window.Left+=11;window.Top+=7;Pump();Require(Math.Abs(w.Left-(window.Left-w.ActualWidth))<1&&Math.Abs(w.Top-window.Top)<1,"原子窗移动未反向跟随Owner");w.Hide();Pump();Require(!request.IsValid&&!window.IsVisible&&!root.IsExpanded,"隐藏Owner仍留活跃窗/目标");w.Show();Pump();var newer=Expanded(session,false);var live=root.CurrentRequest!;request.ClickCommand.Execute(leaf);ReplayOldContainer(ExpandedTree(window,false),oldContainer,leaf,root);Require(live.IsValid&&newer.IsVisible&&root.PreferExpanded==oldPreference&&ReferenceEquals(before,node.RuntimeConfiguration)&&history.Position==oldPosition,"旧回调或跟随动作污染状态");w.Close();Pump();Require(!root.IsCurrent&&!live.IsValid&&!newer.IsVisible,"关闭Owner未退役");});
        Case("同ID真实保存重开新会话不复用偏好/目标且旧回调不可写",()=>{Require(solutions.SaveCurrentDocument(path).FileWritten,"同ID重开前保存失败");var bytes=File.ReadAllBytes(path);var originalId=solutions.CurrentSolution!.Id;var sessionId=solutions.Session!.Editing.SessionId;var w=Open();var field=Low(w);Subscribe(field);var session=Session(field);var window=Expanded(session,false);var root=Root(w);var dirtyBefore=solutions.HasUnsavedChanges;var historyBefore=solutions.Session!.History.Position;root.PreferExpanded=true;Require(solutions.HasUnsavedChanges==dirtyBefore&&solutions.Session.History.Position==historyBefore,"会话偏好改动了Dirty或历史");var request=root.CurrentRequest!;var leaf=Tree((System.Collections.IEnumerable)request.SourceNodes).First(item=>Candidate(item)is not null);var oldContainer=Container(ExpandedTree(window,false),leaf);solutions.CloseSolution(false);var reopened=solutions.OpenSolution(path)??throw new Exception("真实重开失败");Pump();Require(reopened.Id==originalId&&solutions.Session!.Editing.SessionId!=sessionId&&!root.IsCurrent&&!request.IsValid&&!window.IsVisible,"同ID重开复用旧会话或旧窗");var rerun=vm.RunSelectedImageAsync();PumpUntil(()=>rerun.IsCompleted);rerun.GetAwaiter().GetResult();var target=vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.Single(n=>n.Id==node.Id);var fresh=OpenFor(target);var freshControl=Control(fresh);Logical(freshControl).OfType<TabControl>().Single().SelectedIndex=1;fresh.UpdateLayout();Pump();var freshField=Logical(freshControl).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==threshold);var newRoot=Root(fresh);Require(!newRoot.PreferExpanded&&!newRoot.IsExpanded,"会话临时偏好被错误持久化");Subscribe(freshField);var newSession=Session(freshField);var freshWindow=Expanded(newSession,false);var newRequest=newRoot.CurrentRequest!;var model=((ProductionEditorContext)ToolEditorCommit.GetDraftContext(freshControl)!).DraftNode.RuntimeConfiguration!;var saved=Identity(model.Fields.Read(threshold));request.ClickCommand.Execute(leaf);ReplayOldContainer(ExpandedTree(window,false),oldContainer,leaf,newRoot);root.Close();root.Dispose();Require(newRequest.IsValid&&freshWindow.IsVisible&&Identity(model.Fields.Read(threshold))==saved&&bytes.SequenceEqual(File.ReadAllBytes(path)),"旧同ID回调写/关新窗或改文件");Choose(newSession,false,item=>Candidate(item)?.Reference(BindingSelection.All)is{} r&&r.NodeId==source.Id&&r.PortId=="output.image"&&r.MemberFieldPath.SequenceEqual(new[]{"width"}));Debug(fresh,8);fresh.Close();Pump();});
        shell.Close();dialogs.Stop();Pump();Console.WriteLine("C07非模态展开订阅窗真实生产链："+_passed+"组通过；不代原厂全域/自然焦点/剪贴板/连续会话/ROI图文。");return 0;
    }
    private static object? _lastChoice;
    private static object Get(object target,string name)=>target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(target)!;
    private static DeclaredReferenceCandidate? Candidate(object item)=>(DeclaredReferenceCandidate?)Get(item,"Candidate");
    private static AppsPopup? _activePopup;
    private static object Session(DeclaredFieldAccess field){_activePopup=Visual(field.View).OfType<AppsPopup>().Single(item=>item.Name=="rt"&&item.DataContext?.GetType().GetProperty("Target",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(item.DataContext) is Port target&&target.Id==field.FieldId);_activePopup.ApplyTemplate();return _activePopup.DataContext;}
    private static void Subscribe(DeclaredFieldAccess field){if(field.Setting is ReferenceSetting or VariableSetting)return;var right=field.FieldId==BindingId("MaxArea");var candidates=Visual(field.View).OfType<ToggleButton>().ToArray();var matches=candidates.Where(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path==(right?"IsSubscribeRight":"IsSubscribeLeft")).ToArray();Require(matches.Length==1,"原订阅按钮数量不符：field="+field.FieldId+";loaded="+field.View.IsLoaded+";visible="+field.View.IsVisible+";paths="+string.Join(",",candidates.Select(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path)));var toggle=matches[0];toggle.SetCurrentValue(ToggleButton.IsCheckedProperty,true);toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();}
    private static IEnumerable<object> Tree(System.Collections.IEnumerable nodes){foreach(var node in nodes){yield return node!;foreach(var child in Tree((System.Collections.IEnumerable)Get(node!,"EnumEntrysA")))yield return child;}}
    private static ReferenceWindowOwner Root(SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow window)=>(ReferenceWindowOwner)typeof(RuntimeNodeConfigurationControl).GetField("_referenceWindowOwner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window.EditorControl)!;
    private static SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow Expanded(object session,bool index){var owner=(SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow)Window.GetWindow(_activePopup!)!;((ICommand)Get(session,"PopSubscribeWindowCommand")).Execute(null);Pump();var root=Root(owner);var window=owner.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Single(w=>w.IsVisible);Require(ReferenceEquals(window.Owner,owner)&&ReferenceEquals(window.DataContext,root)&&root.CurrentRequest is {IsValid:true} request&&request.Target.Id==((Port)Get(session,"Target")).Id,"展开窗/Owner/目标不是正式同一Root");var english=SunEyeVision.Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("Language")=="English";Require(window.Width==(english?320:240),"非模态窗语言宽度不符");if(index){var text=(string)Get(session,"CurValueA");((ICommand)Get(session,"FocusedCommand")).Execute(new SunEyeVision.Plugin.Wpf.Interop.Windows.TextBoxFocusEventArgs{Text=text,IsFocus=true,SelectionStart=text.LastIndexOf('[')+1});Pump();Require(root.CurrentRequest is{IsValid:true,IsIndex:true,HasSelection:true}&&window.IsSubscribeIndex,"索引真实焦点适配未切原索引树");}var none=english?"None":"无";foreach(var nodes in new[]{root.SourceNodes,root.IndexNodes}){var clear=Tree((System.Collections.IEnumerable)nodes).Single(item=>(bool)Get(item,"Clear"));Require((string)Get(clear,"Name")==none,"当前语言原树无项未使用正式None资源");}var shownTree=ExpandedTree(window,index);var shownClear=Tree((System.Collections.IEnumerable)(index?root.IndexNodes:root.SourceNodes)).Single(item=>(bool)Get(item,"Clear"));var clearContainer=Container(shownTree,shownClear);Require(Visual(clearContainer).OfType<TextBlock>().Any(text=>text.Text==none),"实际原树容器未渲染本语言None文本");return window;}
    private static SunEyeVision.Plugin.Wpf.Controls.References.Primitives.SubTreeView ExpandedTree(SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow window,bool index)=>(SunEyeVision.Plugin.Wpf.Controls.References.Primitives.SubTreeView)window.FindName(index?"indextreeView":"treeView");
    private static TreeViewItem Container(SunEyeVision.Plugin.Wpf.Controls.References.Primitives.SubTreeView tree,object item){tree.UpdateLayout();Pump();var native=(TreeView)tree.FindName("treeView");return (TreeViewItem?)tree.GetType().GetMethod("FindTreeViewItem",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{native,item})??throw new Exception("原树未生成实际目标容器");}
    private static void FireDoubleClick(TreeViewItem container){container.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=System.Windows.Controls.Control.MouseDoubleClickEvent});Pump();}
    private static void DoubleClick(SunEyeVision.Plugin.Wpf.Controls.References.Primitives.SubTreeView tree,object item){var container=Container(tree,item);container.IsSelected=true;Pump();FireDoubleClick(container);}
    private static void ReplayOldContainer(SunEyeVision.Plugin.Wpf.Controls.References.Primitives.SubTreeView oldTree,TreeViewItem container,object oldNode,ReferenceWindowOwner currentRoot){var nodes=Tree((System.Collections.IEnumerable)currentRoot.SourceNodes).Concat(Tree((System.Collections.IEnumerable)currentRoot.IndexNodes)).ToArray();var dc=container.DataContext;var oldMember=nodes.Any(n=>ReferenceEquals(n,oldNode));var dcMember=nodes.Any(n=>ReferenceEquals(n,dc));var text=dc?.ToString();var kind=ReferenceEquals(dc,oldNode)?"retained-old":dc is null?"null":text?.Contains("DisconnectedItem",StringComparison.Ordinal)==true?"disconnected":"other";var associated=ItemsControl.ItemsControlFromItemContainer(container);var events=0;object? payload=null;void Observe(object sender,SunEyeVision.Plugin.Wpf.Interop.Windows.ObjectEventArgs args){events++;payload=args.Param;}var file=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"old-container-trace.jsonl");void Trace(string phase)=>File.AppendAllText(file,System.Text.Json.JsonSerializer.Serialize(new{phase,kind,dcType=dc?.GetType().FullName,dcText=text,oldMember,dcMember,associatedType=associated?.GetType().FullName,linkedToOldTree=oldTree.IsAncestorOf(container),container.IsLoaded,container.IsVisible,events,payloadType=payload?.GetType().FullName,payloadIsOld=ReferenceEquals(payload,oldNode),target=currentRoot.CurrentRequest?.Target.Id})+Environment.NewLine);Trace("before");Require(!oldMember&&!dcMember,"旧容器/节点被当前树合法复用，必须另验当前节点而非判陈旧");Require(kind is "retained-old" or "null" or "disconnected","旧容器发生未识别DataContext变化");oldTree.OnTreeNodeDoubleClick+=Observe;try{FireDoubleClick(container);Trace("after");}finally{oldTree.OnTreeNodeDoubleClick-=Observe;}}
    private static void Choose(object session,bool index,Func<object,bool> predicate){var window=Expanded(session,index);var root=(ReferenceWindowOwner)window.DataContext;var request=root.CurrentRequest!;var item=Tree((System.Collections.IEnumerable)(index?root.IndexNodes:root.SourceNodes)).Single(predicate);_lastChoice=item;Require(ReferenceEquals(index?request.IndexNodes:request.SourceNodes,index?(System.Collections.IEnumerable)Get(session,"IndexNodes"):(System.Collections.IEnumerable)Get(session,"SourceNodes")),"展开树克隆或另造候选");if(index)Capture(window,"expanded-index-"+System.Globalization.CultureInfo.CurrentUICulture.Name+".png");DoubleClick(ExpandedTree(window,index),item);}
    private static void SetIndex(object session,string index)
    {var value=(string)Get(session,"CurValueA");var start=value.LastIndexOf('[');Require(start>=0,"真实引用文本没有索引槽："+value);session.GetType().GetProperty("CurValueA")!.SetValue(session,value[..(start+1)]+index+"]");Require((bool)session.GetType().GetMethod("TryCommitPending")!.Invoke(session,null)!,"索引原文未被实际合同接纳："+index);Pump();}
    private static void SelectVariable(object session,bool index,string id)
    {
        var expanded=Expanded(session,index);var expandedRoot=(ReferenceWindowOwner)expanded.DataContext;var manager=Tree((System.Collections.IEnumerable)(index?expandedRoot.IndexNodes:expandedRoot.SourceNodes)).Single(item=>(bool)Get(item,"VariableManager"));
        Exception? failure=null;var selected=false;var confirmRequested=false;VariableSelectionDialog? ownedDialog=null;var deadline=DateTime.UtcNow.AddSeconds(15);var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>{
            if(DateTime.UtcNow>deadline){timer.Stop();failure=new TimeoutException("等待自有真实变量模态窗超过15秒");ownedDialog?.Close();return;}
            var dialog=Application.Current.Windows.OfType<VariableSelectionDialog>().SingleOrDefault(window=>window.IsVisible);if(dialog is null||confirmRequested)return;ownedDialog=dialog;confirmRequested=true;
            try{var item=dialog.BindItems.Single(row=>row.Reference.VariableId==id);((ListView)dialog.FindName("globalVarList")).SelectedItem=item;Visual(dialog).OfType<Button>().Single(button=>Equals(button.Content,dialog.TryFindResource("Confirm"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));selected=true;}
            catch(Exception error){failure=error;ownedDialog.Close();}
        };
        timer.Start();try{DoubleClick(ExpandedTree(expanded,index),manager);}finally{timer.Stop();}Pump();if(failure is not null)throw new Exception("原模态变量选择失败",failure);Require(selected,"原模态变量窗没有完成实际选择");
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
            if(node is SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Core.ParamListView list)
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
    private static void Case(string name,Action action)
    {
        var filter=Environment.GetEnvironmentVariable("C07_CASE")??"All";if(filter!="All"&&!(filter=="Targets"?name.StartsWith("原非模态窗树组",StringComparison.Ordinal):filter=="Lifetime"?name.StartsWith("Owner跟随",StringComparison.Ordinal):name.StartsWith("同ID真实",StringComparison.Ordinal)))return;
        var file=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"reference-chain-progress.log");
        File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" BEGIN "+name+Environment.NewLine);
        try{action();_passed++;Console.WriteLine("通过："+name);File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" PASS "+name+Environment.NewLine);}
        catch(Exception failure){File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" FAIL "+name+Environment.NewLine+failure+Environment.NewLine);throw;}
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
