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


        var flow=solutions.CurrentSolution!.Workflows.Single(w=>w.Id==vm.WorkflowTabViewModel.SelectedTab!.RootWorkflowId);
        var edge=new WorkflowConnection{Id=Guid.NewGuid().ToString(),SourceNodeId=source.Id,TargetNodeId=node.Id};Require(vm.AddConnectionToWorkflow(edge),"正式控制边不能建立");
        var history=solutions.Session!.History;var host=solutions.Session.Editing.Host;
        var variableEdit=host.BeginVariableEdit();var values=new GlobalVariable("真实阈值数组",new long[]{173,90,37,60,70,80,100,110,111},"Integer[]");var indexVariable=new GlobalVariable("真实索引",1L,"Integer");variableEdit.Configuration.GlobalVariables.Add(values);variableEdit.Configuration.GlobalVariables.Add(indexVariable);host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variableEdit);
        var setup=Open();var setupControl=Control(setup);var imageField=Logical(setupControl).OfType<DeclaredInputFieldsView>().Single().Fields.Single(f=>f.FieldId==BlobTool.ImageInput);var imageCombo=(ComboBox)imageField.InputElement!;
        imageCombo.SelectedItem=imageCombo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ReferenceSetting r&&r.Reference.NodeId==source.Id&&r.Reference.PortId=="output.image"&&r.Reference.MemberFieldPath.SequenceEqual(new[]{"data"}));Pump();Require(setup.TryConfirm(out var error),error);if(setup.IsVisible)setup.Close();Pump();Require(solutions.SaveCurrentDocument(path).FileWritten,"基准方案保存失败");
        var initialRun=vm.RunSelectedImageAsync();PumpUntil(()=>initialRun.IsCompleted);initialRun.GetAwaiter().GetResult();Require(Context().GetNodeResult(source.Id)?.Result is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {IsSuccess:true},"正式上游未产生本次源快照");
        DeclaredFieldAccess Low(DefaultDebugWindow window){Threshold(window);return Logical(Control(window)).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==threshold);}
        ProductionEditorContext Draft(DefaultDebugWindow window)=>(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(window))!;
        void Debug(DefaultDebugWindow window,long expected)
        {
            long? actual=null;var current=Control(window);
            SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler observe=(_,args)=>{if(args.Results is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {Output:BlobOutput output})actual=output.ThresholdMinUsed;};
            current.AddHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observe);
            try{var run=current.RunDebugAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();Require(actual==expected,"真实算法未消费预期阈值："+actual+"/"+expected);Require(Draft(window).DraftNode.RuntimeConfiguration!.ReadCurrentParameter(threshold) is {Value:long value}&&value==expected,"私有当前值与算法不同");}
            finally{current.RemoveHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observe);}
        }
        void Confirm(DefaultDebugWindow window){Require(window.TryConfirm(out var issue),"真实确认失败："+issue);if(window.IsVisible)window.Close();Pump();}
        long Current()=>node.RuntimeConfiguration!.ReadCurrentParameter(threshold)?.Value is long value?value:node.RuntimeConfiguration.Fields.Read(threshold) is ConstantSetting constant?(long)constant.Value!:throw new Exception("没有正式当前值");
        string Identity(ParameterSetting setting)=>setting switch{ReferenceSetting reference=>SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(reference.Reference),VariableSetting variable=>SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(variable.Reference),_=>throw new Exception("不是引用")};
        void SaveCheck(Func<ParameterSetting,bool> predicate,long expected)
        {
            var identity=Identity(node.RuntimeConfiguration!.Fields.Read(threshold));Require(solutions.SaveCurrentDocument(path).FileWritten,"正式保存失败");
            var loaded=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("真实文件重开失败");var loadedFlow=loaded.Workflows.Single(w=>w.Id==flow.Id);var configuration=loadedFlow.Nodes.Single(n=>n.Id==node.Id).RuntimeConfiguration!;
            Require(predicate(configuration.Fields.Read(threshold))&&Identity(configuration.Fields.Read(threshold))==identity&&configuration.ReadCurrentParameter(threshold) is {Value:long actual}&&actual==expected,"保存重开丢完整Selection/身份/当前值");
            var ownEngine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);ownEngine.RegisterWorkflow(loadedFlow);var ownHost=new NodeConfigurationEditHost(loaded);var publication=WorkflowPublishedConfiguration.Capture(ownHost.Capture(),loaded,ownHost);
            using var globals=new WorkflowGlobalRunValues(loaded.Id,publication.GlobalDefinitions,publication.GlobalDefinitions.Select(variable=>new KeyValuePair<string,object?>(variable.Id,variable.Value)));
            var ownExecutor=new WorkflowExecutionEngine(ownEngine,new PluginManager(),SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);
            var replay=ownExecutor.ExecuteWorkflowAsync(new WorkflowRunRequest(loadedFlow.Id,hostInputs:new Dictionary<string,SunEyeVision.Plugin.Contracts.IHostInputPreparer>{{source.Id,new ImageSourceHostInputPreparer(imageFile,1){LocalReadAdmission=(_,_)=>imageFile}}}){Configuration=publication.Configuration,ExecutionOwner=publication.ExecutionOwner,ProducedGlobals=globals,ParameterValues=publication.ParameterValues,ResourcePaths=publication.ResourcePaths});
            PumpUntil(()=>replay.IsCompleted);using(var run=replay.GetAwaiter().GetResult()){Require(run.Success&&run.NodeResults[node.Id].Result is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {Output:BlobOutput output}&&output.ThresholdMinUsed==expected,"重开配置未按完整关系实际执行");}loaded.ExecutionOwner.Retire();
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
        shell.Close();dialogs.Stop();Pump();Console.WriteLine("B08数值引用真实窗口完整链："+_passed+"组通过；不代原厂全域/自然焦点/剪贴板/连续会话/ROI图文。");return 0;
    }
    private static object? _lastChoice;
    private static object Get(object target,string name)=>target.GetType().GetProperty(name)!.GetValue(target)!;
    private static DeclaredReferenceCandidate? Candidate(object item)=>(DeclaredReferenceCandidate?)Get(item,"Candidate");
    private static AppsPopup? _activePopup;
    private static object Session(DeclaredFieldAccess field){_activePopup=Visual(field.View).OfType<AppsPopup>().Single(item=>item.Name=="rt");_activePopup.ApplyTemplate();return _activePopup.DataContext;}
    private static void Subscribe(DeclaredFieldAccess field)
    {if(field.Setting is ReferenceSetting or VariableSetting)return;var toggle=Visual(field.View).OfType<ToggleButton>().Single(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path=="IsSubscribeLeft");toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();}
    private static IEnumerable<object> Tree(System.Collections.IEnumerable nodes){foreach(var node in nodes){yield return node!;foreach(var child in Tree((System.Collections.IEnumerable)Get(node!,"EnumEntrysA")))yield return child;}}
    private static void Choose(object session,bool index,Func<object,bool> predicate)
    {if(_activePopup is {IsLoaded:true}){_activePopup.IsDropDownOpen=true;_activePopup.UpdateLayout();Pump();}((ICommand)Get(session,"DropDownCommand")).Execute(null);Pump();if(index)session.GetType().GetProperty("IsTextDropDownOpen")!.SetValue(session,true);var item=Tree((System.Collections.IEnumerable)Get(session,index?"IndexNodes":"SourceNodes")).Single(predicate);_lastChoice=item;((ICommand)Get(session,index?"IndexClickCommand":"ClickCommand")).Execute(item);Pump();}
    private static void SetIndex(object session,string index)
    {var value=(string)Get(session,"CurValueA");var start=value.LastIndexOf('[');Require(start>=0,"真实引用文本没有索引槽："+value);session.GetType().GetProperty("CurValueA")!.SetValue(session,value[..(start+1)]+index+"]");Require((bool)session.GetType().GetMethod("TryCommitPending")!.Invoke(session,null)!,"索引原文未被实际合同接纳："+index);Pump();}
    private static void SelectVariable(object session,bool index,string id)
    {
        // Complete popup preparation before starting the modal waiter; no queued callback races a pre-dialog Pump.
        if(_activePopup is {IsLoaded:true}){_activePopup.IsDropDownOpen=true;_activePopup.UpdateLayout();Pump();}
        ((ICommand)Get(session,"DropDownCommand")).Execute(null);Pump();if(index)session.GetType().GetProperty("IsTextDropDownOpen")!.SetValue(session,true);
        var manager=Tree((System.Collections.IEnumerable)Get(session,index?"IndexNodes":"SourceNodes")).Single(item=>(bool)Get(item,"VariableManager"));
        Exception? failure=null;var selected=false;VariableSelectionDialog? ownedDialog=null;var deadline=DateTime.UtcNow.AddSeconds(15);var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>{
            if(DateTime.UtcNow>deadline){timer.Stop();failure=new TimeoutException("等待自有真实变量模态窗超过15秒");ownedDialog?.Close();return;}
            var dialog=Application.Current.Windows.OfType<VariableSelectionDialog>().SingleOrDefault(window=>window.IsVisible);if(dialog is null)return;ownedDialog=dialog;timer.Stop();
            try{var item=dialog.BindItems.Single(row=>row.Reference.VariableId==id);((ListView)dialog.FindName("globalVarList")).SelectedItem=item;Visual(dialog).OfType<Button>().Single(button=>Equals(button.Content,dialog.TryFindResource("Confirm"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));selected=true;}
            catch(Exception error){failure=error;ownedDialog.Close();}
        };
        timer.Start();try{((ICommand)Get(session,index?"IndexClickCommand":"ClickCommand")).Execute(manager);}finally{timer.Stop();}Pump();if(failure is not null)throw new Exception("原模态变量选择失败",failure);Require(selected,"原模态变量窗没有完成实际选择");
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
    private static void Case(string name,Action action)
    {
        var file=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"reference-chain-progress.log");
        File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" BEGIN "+name+Environment.NewLine);
        try{action();_passed++;Console.WriteLine("通过："+name);File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" PASS "+name+Environment.NewLine);}
        catch(Exception failure){File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" FAIL "+name+Environment.NewLine+failure+Environment.NewLine);throw;}
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
