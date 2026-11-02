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
        var isolatedFilters=host.BeginEdit(flow.Id,node.Id);var isolatedSettings=(BlobSettings)isolatedFilters.Node.RuntimeConfiguration!.CaptureSettings();isolatedSettings=isolatedSettings with{IsAreaFilterEnabled=false,IsCircularityFilterEnabled=false,IsRectangularityFilterEnabled=false};isolatedFilters.Node.RuntimeConfiguration=isolatedFilters.Node.RuntimeConfiguration.WithSettings(isolatedSettings);host.CommitBatch(new[]{isolatedFilters},null);
        var setup=Open();var setupControl=Control(setup);var imageField=Logical(setupControl).OfType<DeclaredInputFieldsView>().Single().Fields.Single(f=>f.FieldId==BlobTool.ImageInput);var imageCombo=(ComboBox)imageField.InputElement!;
        imageCombo.SelectedItem=imageCombo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ReferenceSetting r&&r.Reference.NodeId==source.Id&&r.Reference.PortId=="output.image"&&r.Reference.MemberFieldPath.SequenceEqual(new[]{"data"}));Pump();Require(setup.TryConfirm(out var error),error);if(setup.IsVisible)setup.Close();Pump();Require(solutions.SaveCurrentDocument(path).FileWritten,"基准方案保存失败");
        var initialRun=vm.RunSelectedImageAsync();PumpUntil(()=>initialRun.IsCompleted);initialRun.GetAwaiter().GetResult();Require(Context().GetNodeResult(source.Id)?.Result is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {IsSuccess:true},"正式上游未产生本次源快照");
        var roiVariables=host.BeginVariableEdit();var roiValues=new GlobalVariable("ROI真实宽度序列",new double[]{2,4,6,2,4,6,2,4,6},"Double[]");roiVariables.Configuration.GlobalVariables.Add(roiValues);host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),roiVariables);
        var multiVariables=host.BeginVariableEdit();var rectangleSeries=new[]{new[]{1d,5d},new[]{1d,5d},new[]{2d,2d},new[]{2d,2d},new[]{0d,0d}}.Select((v,i)=>new GlobalVariable("ROI矩形叶"+i,v,"Double[]")).ToArray();
        var annulusSeries=new[]{new[]{1d,5d},new[]{1d,5d},new[]{1d,1d},new[]{2d,2d},new[]{0d,0d},new[]{360d,360d}}.Select((v,i)=>new GlobalVariable("ROI圆环叶"+i,v,"Double[]")).ToArray();foreach(var v in rectangleSeries.Concat(annulusSeries))multiVariables.Configuration.GlobalVariables.Add(v);host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),multiVariables);
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
            var replay=ownExecutor.ExecuteWorkflowAsync(new WorkflowRunRequest(loadedFlow.Id,hostInputs:new Dictionary<string,SunEyeVision.Plugin.Contracts.IHostInputPreparer>{{source.Id,new ImageSourceHostInputPreparer(imageFile,1){LocalReadAdmission=(_,_)=>imageFile}}}){Configuration=publication.Configuration,ExecutionOwner=publication.ExecutionOwner,ProducedGlobals=globals,ParameterValues=publication.ParameterValues,ResourcePaths=publication.ResourcePaths});
            PumpUntil(()=>replay.IsCompleted);using(var run=replay.GetAwaiter().GetResult()){Require(run.Success&&run.NodeResults[node.Id].Result is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {Output:BlobOutput output}&&output.ThresholdMinUsed==expected&&(expectedReplayCount is null||output.Count==expectedReplayCount),"重开配置未按完整关系实际执行");}loaded.ExecutionOwner.Retire();
        }
        var scope=Environment.GetEnvironmentVariable("BLOB_ROI_INHERIT_CASE")??"All";
        if(scope is not ("All" or "Model" or "Binding" or "Window" or "Publication"))throw new Exception("未知ROI继承筛选");
        bool Included(string value)=>scope=="All"||scope==value;
        var rectangleMode=BlobRoiInheritance.RectangleParameters;
        var rectangleIds=BlobRoiInheritance.RectangleMembers.Select(member=>BlobRoiInheritance.Leaf(rectangleMode,member)).ToArray();
        Dictionary<string,ParameterSetting> RectangleFields(string selected=BlobRoiInheritance.RectangleParameters)
            =>new(StringComparer.Ordinal){[BlobRoiInheritance.ModeId]=new ConstantSetting(selected),[rectangleIds[0]]=new ConstantSetting(new[]{3d}),[rectangleIds[1]]=new ConstantSetting(new[]{3d}),[rectangleIds[2]]=new ConstantSetting(new[]{4d}),[rectangleIds[3]]=new ConstantSetting(new[]{2d}),[rectangleIds[4]]=new ConstantSetting(new[]{0d})};
        RuntimeToolConfiguration Configure(IReadOnlyDictionary<string,ParameterSetting> changes)=>node.RuntimeConfiguration!.WithFieldSettings(changes);
        void FormalEdit(IReadOnlyDictionary<string,ParameterSetting> changes)
        {var edit=host.BeginEdit(flow.Id,node.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldSettings(changes);host.CommitBatch(new[]{edit},null);Pump();}
        string ConfigurationIdentity(RuntimeToolConfiguration config)=>System.Text.Json.JsonSerializer.Serialize(config);
        ReferenceSetting BadReference()=>new(new OutputReference("missing-roi-producer","output",TypeRef.ScalarOf(ScalarKind.Float64),scopeId:flow.Id));
        var capture=typeof(RuntimeToolConfiguration).GetMethod("CaptureExecutionFields",BindingFlags.Instance|BindingFlags.NonPublic)!;
        IReadOnlyList<KeyValuePair<string,ParameterSetting>> Active(RuntimeToolConfiguration config)=>(IReadOnlyList<KeyValuePair<string,ParameterSetting>>)capture.Invoke(config,null)!;
        if(Included("Binding"))
        {
            Case("实际树资格与核心整数到浮点提升一致，反向仍需显式转换",()=>{
                var type=typeof(DeclaredReferenceCandidate).Assembly.GetType("SunEyeVision.Plugin.Wpf.Adapters.References.DeclaredReferenceTree")!;
                var match=type.GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(m=>m.Name=="Matches"&&m.GetParameters()[0].ParameterType==typeof(DeclaredReferenceCandidate));
                foreach(var sequence in new[]{false,true})
                {
                    TypeRef From(ScalarKind scalar)=>sequence?TypeRef.SequenceOf(TypeRef.ScalarOf(scalar)):TypeRef.ScalarOf(scalar);
                    var from=new Port("source",From(ScalarKind.Int64));var to=new Port("target",From(ScalarKind.Float64));var candidate=new DeclaredReferenceCandidate("typed","node","scope",from,Array.Empty<string>());
                    Require((bool)match.Invoke(null,new object[]{candidate,to,BindingConversion.Automatic,false,ParameterReferenceBehavior.StrictExactlyOne})!,"树排除了核心可提升来源");Require(new ParameterBindingService().CompareValueSource(from,to,BindingConversion.Automatic,out _)!=BindingCompatibility.Incompatible,"核心与树类型不一致");
                    var reverse=new DeclaredReferenceCandidate("typed","node","scope",to,Array.Empty<string>());Require(!(bool)match.Invoke(null,new object[]{reverse,from,BindingConversion.Automatic,false,ParameterReferenceBehavior.StrictExactlyOne})!&&(bool)match.Invoke(null,new object[]{reverse,from,BindingConversion.Float64ToInt64Checked,false,ParameterReferenceBehavior.StrictExactlyOne})!,"浮点到整数未经显式转换被放宽");
                }
                Require(!new ParameterBindingService().ResolveValue(9007199254740993L,DataType.Integer,new Port("target",DataType.Number)).Success,"不可精确表示的整数被静默提升");
            });
            Case("四态可选顶层与必需5/6成员，空活动来源拒绝但模式可编辑",()=>{
                Require(BlobRoiInheritance.Ports.All(port=>!port.Required),"互斥输入被误声明全局必填");Require(BlobRoiInheritance.RectangleType.Fields.Count==5&&BlobRoiInheritance.AnnulusType.Fields.Count==6&&BlobRoiInheritance.RectangleType.Fields.Concat(BlobRoiInheritance.AnnulusType.Fields).All(port=>port.Required),"组合必需成员不完整");
                foreach(var mode in new[]{BlobRoiInheritance.Rectangle,rectangleMode,BlobRoiInheritance.Annulus,BlobRoiInheritance.AnnulusParameters}){var config=Configure(new Dictionary<string,ParameterSetting>{{BlobRoiInheritance.ModeId,new ConstantSetting(mode)}});Require(config.Validate().Any(d=>d.Code=="BLOB_ROI_SOURCE_REQUIRED"),"空活动模式伪成功："+mode);}
                var rejected=false;try{_=Configure(new Dictionary<string,ParameterSetting>{{BlobRoiInheritance.ModeId,new ConstantSetting("invalid")}});}catch(ArgumentException){rejected=true;}Require(rejected,"非法模式被接纳");
            });
            Case("不完整组允许合法单叶提案但最终确认提交拒绝，坏源仍拒绝",()=>{
                var draft=host.BeginEdit(flow.Id,node.Id);draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{BlobRoiInheritance.ModeId,new ConstantSetting(BlobRoiInheritance.AnnulusParameters)}});
                var id=BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters,"center-x");var proposal=new Dictionary<string,ParameterSetting>{{id,new VariableSetting(new VariableReference(solutions.CurrentSolution!.Id,annulusSeries[0].Id,DataType.NumberArray))}};
                host.ValidateRuntimeFieldProposals(draft,proposal);draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration.WithFieldSettings(proposal);Require(draft.Node.RuntimeConfiguration.Validate().Any(d=>d.Code=="BLOB_ROI_SOURCE_REQUIRED"),"不完整组丢失最终诊断");
                var before=ConfigurationIdentity(node.RuntimeConfiguration!);var rejected=false;try{host.Validate(draft);}catch(Exception e)when(e is ArgumentException or InvalidOperationException){Console.WriteLine("不完整组最终诊断："+e.Message);rejected=e.Message.Contains("ROI",StringComparison.OrdinalIgnoreCase);}Require(rejected,"不完整组最终校验被放行");
                rejected=false;try{host.Commit(draft);}catch(Exception e)when(e is ArgumentException or InvalidOperationException){rejected=e.Message.Contains("ROI",StringComparison.OrdinalIgnoreCase);}Require(rejected&&ConfigurationIdentity(node.RuntimeConfiguration!)==before,"不完整组提交写入正式配置");
                rejected=false;try{host.ValidateRuntimeFieldProposals(draft,new Dictionary<string,ParameterSetting>{{id,BadReference()}});}catch(Exception e)when(e is ArgumentException or InvalidOperationException){Console.WriteLine("坏源提案诊断："+e.Message);rejected=true;}Require(rejected,"逐叶编辑放宽了无效来源保护");
            });
            Case("未完整草稿提案仍拒绝活动输出自环且不写正式配置",()=>{
                var draft=host.BeginEdit(flow.Id,node.Id);draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithFieldSettings(RectangleFields());var before=ConfigurationIdentity(node.RuntimeConfiguration!);var proposal=new Dictionary<string,ParameterSetting>{{rectangleIds[2],new ReferenceSetting(new OutputReference(node.Id,BlobSchema.Field(12),DataType.Number,scopeId:flow.Id))}};var rejected=false;try{host.ValidateRuntimeFieldProposals(draft,proposal);}catch(Exception e)when(e is ArgumentException or InvalidOperationException){Console.WriteLine("活动自环提案诊断："+e.Message);rejected=e.Message.Contains("自身")||e.Message.Contains("循环")||e.Message.Contains("自己")||e.Message=="Binding introduces an execution dependency cycle.";}Require(rejected&&ConfigurationIdentity(node.RuntimeConfiguration!)==before,"活动自环提案放行或改变正式配置");
            });
            Case("完整候选提案拒绝跨节点间接环和变量生产者自环",()=>{
                var secondNode=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(secondNode);Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString(),SourceNodeId=source.Id,TargetNodeId=secondNode.Id}),"间接环夹具图像边失败");
                Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString(),SourceNodeId=node.Id,TargetNodeId=secondNode.Id}),"间接环夹具控制前序失败");var secondDraft=host.BeginEdit(flow.Id,secondNode.Id);secondDraft.Node.RuntimeConfiguration=secondDraft.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{threshold,new ReferenceSetting(new OutputReference(node.Id,BlobSchema.Field(11),DataType.Integer,scopeId:flow.Id))}});host.Commit(secondDraft);
                var draft=host.BeginEdit(flow.Id,node.Id);draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithFieldSettings(RectangleFields());var before=ConfigurationIdentity(node.RuntimeConfiguration!);var rejected=false;
                try{host.ValidateRuntimeFieldProposals(draft,new Dictionary<string,ParameterSetting>{{rectangleIds[2],new ReferenceSetting(new OutputReference(secondNode.Id,BlobSchema.Field(12),DataType.Number,scopeId:flow.Id))}});}catch(Exception e)when(e is ArgumentException or InvalidOperationException){Console.WriteLine("跨节点提案诊断："+e);rejected=e.Message.Contains("cycle",StringComparison.OrdinalIgnoreCase)||e.Message.Contains("循环");}Require(rejected,"跨节点间接环未在提案阶段拒绝");
                var variables=host.BeginVariableEdit();var produced=new GlobalVariable("ROI生产者自环",2d,"Double"){ProducerBinding=new ProducerBinding{ReferenceExpression=BindingReferenceSerializer.Serialize(new OutputReference(node.Id,BlobSchema.Field(12),DataType.Number,scopeId:flow.Id))}};variables.Configuration.GlobalVariables.Add(produced);host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variables);draft=host.BeginEdit(flow.Id,node.Id);draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithFieldSettings(RectangleFields());rejected=false;
                try{host.ValidateRuntimeFieldProposals(draft,new Dictionary<string,ParameterSetting>{{rectangleIds[2],new VariableSetting(new VariableReference(solutions.CurrentSolution!.Id,produced.Id,DataType.Number))}});}catch(Exception e)when(e is ArgumentException or InvalidOperationException){Console.WriteLine("变量生产者提案诊断："+e);rejected=e.Message.Contains("cycle",StringComparison.OrdinalIgnoreCase)||e.Message.Contains("循环")||e.Message.Contains("自身");}Require(rejected&&ConfigurationIdentity(node.RuntimeConfiguration!)==before,"变量生产者自环放行或写入正式配置");
            });
            Case("休眠坏来源保值而执行依赖不消费，重新激活即拒绝",()=>{
                var changes=RectangleFields(BlobRoiInheritance.Local);changes[rectangleIds[2]]=BadReference();var config=Configure(changes);var shadow=node.Clone();shadow.RuntimeConfiguration=config;
                Require(BindingRelationCatalog.References(shadow).ContainsKey(rectangleIds[2])&&!Active(config).Any(pair=>pair.Key==rectangleIds[2]),"管理丢引用或执行仍含休眠字段");var old=ConfigurationIdentity(node.RuntimeConfiguration!);var position=history.Position;FormalEdit(changes);
                try{WorkflowExecutionDependencyGraph.Compile(flow);SolutionExecutionDependencyGraph.Compile(solutions.CurrentSolution!);var w=Open();Require(Control(w).TryCapture(out error),error);w.Close();Pump();var active=Open();var inputs=Logical(Control(active)).OfType<DeclaredInputFieldsView>().Single();inputs.WriteUnpresentedInput(BlobRoiInheritance.ModeId,new ConstantSetting(rectangleMode));Require(!Control(active).TryCapture(out error)&&!string.IsNullOrWhiteSpace(error),"坏来源重新激活未拒绝");active.Close();Pump();Require(Identity(node.RuntimeConfiguration!.Fields.Read(rectangleIds[2]))==Identity(changes[rectangleIds[2]]),"重新激活清掉来源");}
                finally{if(history.Position>position)history.Undo();Require(ConfigurationIdentity(node.RuntimeConfiguration!)==old,"正式夹具事务未撤销");}
            });
            Case("休眠自环不进入工作流和方案图，管理仍枚举完整来源",()=>{
                var changes=RectangleFields(BlobRoiInheritance.Local);changes[rectangleIds[2]]=new ReferenceSetting(new OutputReference(node.Id,BlobSchema.Field(12),TypeRef.ScalarOf(ScalarKind.Float64),scopeId:flow.Id));var graphSolution=new SolutionRepository().LoadCandidate(path).Candidate!;var graphFlow=graphSolution.Workflows.Single(f=>f.Id==flow.Id);var graphNode=graphFlow.Nodes.Single(n=>n.Id==node.Id);graphNode.RuntimeConfiguration=Configure(changes);
                try{WorkflowExecutionDependencyGraph.Compile(graphFlow);SolutionExecutionDependencyGraph.Compile(graphSolution);Require(BindingRelationCatalog.References(graphNode).ContainsKey(rectangleIds[2]),"休眠自环从管理引用消失");}finally{graphSolution.ExecutionOwner.Retire();}
            });
            Case("配置复制与JSON保留休眠完整引用，其他工具默认全部活动",()=>{
                var changes=RectangleFields(BlobRoiInheritance.Local);changes[rectangleIds[1]]=BadReference();var config=Configure(changes);var json=ConfigurationIdentity(config);var restored=System.Text.Json.JsonSerializer.Deserialize<RuntimeToolConfiguration>(json)!;Require(ConfigurationIdentity(config.Copy())==json&&ConfigurationIdentity(restored)==json&&Identity(restored.Fields.Read(rectangleIds[1]))==Identity(changes[rectangleIds[1]]),"复制/JSON丢休眠身份");Require(source.RuntimeConfiguration!.BindingTargets.All(port=>source.RuntimeConfiguration.IsExecutionFieldActive(port.Id)),"未实现活动规则的图像源行为改变");
            });
            Case("方案消费者登记按InputKey及名称唯一身份过滤，空scope与未知key边界",()=>{
                var graphSolution=new SolutionRepository().LoadCandidate(path).Candidate!;var graphFlow=graphSolution.Workflows.Single(f=>f.Id==flow.Id);var graphNode=graphFlow.Nodes.Single(n=>n.Id==node.Id);var changes=RectangleFields(BlobRoiInheritance.Local);
                var variable=new GlobalVariable("ROI登记验证",64d,"Double"){ProducerBinding=new ProducerBinding{ReferenceExpression=BindingReferenceSerializer.Serialize(new OutputReference(node.Id,BlobSchema.Field(12),DataType.Number,scopeId:flow.Id))}};
                changes[rectangleIds[2]]=new VariableSetting(new VariableReference(graphSolution.Id,variable.Id,DataType.Number));graphNode.RuntimeConfiguration=Configure(changes);graphSolution.GlobalVariables.Add(variable);
                try
                {
                    foreach(var sample in new[]{(flow.Id,(string?)rectangleIds[2],"ignored"),(flow.Id,(string?)null,rectangleIds[2]),("",(string?)rectangleIds[2],"ignored"),("",(string?)null,rectangleIds[2])})
                    {variable.ConsumerBindings.Clear();variable.ConsumerBindings.Add(new ConsumerBinding{WorkflowId=sample.Item1,NodeId=node.Id,InputKey=sample.Item2,ParameterName=sample.Item3});SolutionExecutionDependencyGraph.Compile(graphSolution,false);Require(variable.ConsumerBindings.Count==1&&variable.ConsumerBindings[0].InputKey==sample.Item2,"过滤删除了登记");}
                    foreach(var sample in new[]{((string?)"unknown-id","ignored"),((string?)null,"ROI宽度")})
                    {variable.ConsumerBindings.Clear();variable.ConsumerBindings.Add(new ConsumerBinding{WorkflowId=flow.Id,NodeId=node.Id,InputKey=sample.Item1,ParameterName=sample.Item2});var rejected=false;try{SolutionExecutionDependencyGraph.Compile(graphSolution,false);}catch(InvalidOperationException e){Console.WriteLine("未知登记保守依赖诊断："+e.Message);rejected=e.Message.StartsWith("解决方案依赖不能指向自身：",StringComparison.Ordinal);}Require(rejected,"未知身份被静默当休眠项过滤");}
                }
                finally{graphSolution.ExecutionOwner.Retire();}
            });
        }
        if(Included("Model"))InheritanceModelCases();
        SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor Input(DefaultDebugWindow w,string id)=>Logical(Control(w)).OfType<DeclaredInputFieldsView>().Single().Fields.Single(editor=>editor.FieldId==id);
        object InputSession(SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor field)
        {_activePopup=Visual(field).OfType<AppsPopup>().Single(p=>p.Name=="rt"&&p.DataContext?.GetType().Name=="ReferenceFieldSession"&&((Port)Get(p.DataContext,"Target")).Id==field.FieldId);_activePopup.ApplyTemplate();return _activePopup.DataContext;}
        void Radio(DefaultDebugWindow w,int index,int selected)
        {
            var ui=Logical(Control(w)).OfType<SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiUiDraftControls>().Single();
            var radio=((System.Collections.IEnumerable)Get(ui,"_radios")).Cast<object>().ElementAt(index);
            var command=(ICommand)Get(radio,"ValueChangedCommand");Require(command.CanExecute(selected),"原radio不可操作："+index+"/"+selected);command.Execute(selected);Pump();
        }
        void SetMode(DefaultDebugWindow w,int? selected){Radio(w,1,selected is null?0:1);if(selected is not null)Radio(w,2,selected.Value);}
        void ReferenceInput(SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor field)
        {if(field.Setting is ReferenceSetting or VariableSetting)return;var toggle=Visual(field).OfType<ToggleButton>().Single(b=>BindingOperations.GetBinding(b,ToggleButton.IsCheckedProperty)?.Path?.Path=="IsSubscribeLeft");toggle.SetCurrentValue(ToggleButton.IsCheckedProperty,true);toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();}
        void Populate(DefaultDebugWindow w)
        {SetMode(w,1);foreach(var pair in RectangleFields().Where(pair=>pair.Key!=BlobRoiInheritance.ModeId))Require(Input(w,pair.Key).TrySetConstant(((double[])((ConstantSetting)pair.Value).Value!)[0]),"真实数值控件拒绝："+pair.Key);}
        TextBox NumericText(SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor field)=>(TextBox)Get(Get(field,"_numeric"),"TextEditor");
        (int Count,double Area,bool Center,bool Corner,bool Second,bool Outside,bool RingEdge,bool FirstHole,bool FirstRingEdge) Observe(DefaultDebugWindow w)
        {
            object? trace=null;
            if(Environment.GetEnvironmentVariable("BLOB_ROI_TRACE_ONLY")=="AllMin")
            {
                var config=Draft(w).DraftNode.RuntimeConfiguration!;var captureDebug=(Func<WorkflowNodeBase,RuntimeDebugInvocation>)Get(Control(w),"_captureDebug");using var frozen=captureDebug(Draft(w).DraftNode);
                var prepare=(Func<Action<string,object?>,(InputValues Inputs,object Settings)>)Get(frozen,"_prepare");var prepared=prepare((_,_)=>{});
                var typed=BlobInput.Read(prepared.Inputs);var actualSettings=(BlobSettings)prepared.Settings;
                var leaves=rectangleIds.Select(id=>{var reader=prepared.Inputs.Read(id);return new{id,Count=reader.Count,Values=Enumerable.Range(0,reader.Count).Select(i=>reader.GetElement(i).GetScalar<double>()).ToArray(),Identity=Identity(config.Fields.Read(id))};}).ToArray();
                trace=new{Kind="独立捕获探针（随后另建真实执行请求）",Mode=BlobRoiInheritance.ReadMode(config.Fields),Leaves=leaves,Variables=solutions.CurrentSolution!.GlobalVariables.Select(v=>new{v.Id,v.Name,v.Value}).ToArray(),SourceIdentities=frozen.SourceIdentities,actualSettings.IsAreaFilterEnabled,actualSettings.FilterAreaMin,actualSettings.FilterAreaMax,actualSettings.IsCircularityFilterEnabled,actualSettings.IsRectangularityFilterEnabled,Shapes=BlobLocalRoi.Items(typed.LocalRoi!,BlobLocalRoi.Inspection).Select(item=>item.Get<RecordValue>("shape").CopyFields().ToDictionary(p=>p.Key,p=>p.Value is RecordValue point?(object)point.CopyFields():p.Value)).ToArray()};
            }
            (int,double,bool,bool,bool,bool,bool,bool,bool)? value=null;var control=Control(w);SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler handler=(_,args)=>{if(args.Results is DeclaredOutputSnapshot{Output:BlobOutput output})value=(output.Count,output.Measurements.Sum(blob=>blob.Area),RegionContains(output.Region,3,3),RegionContains(output.Region,0,0),RegionContains(output.Region,5,5),RegionContains(output.Region,7,0),RegionContains(output.Region,6,5),RegionContains(output.Region,1,1),RegionContains(output.Region,2,1));};
            control.AddHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,handler);try{var run=control.RunDebugAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();if(trace is not null)File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"all-min-trace.json"),System.Text.Json.JsonSerializer.Serialize(new{Trace=trace,Actual=value is null?null:new{value.Value.Item1,value.Value.Item2,value.Value.Item3,value.Value.Item4}},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));Require(value is not null,"私有执行未发布真实Blob结果");return value!.Value;}finally{control.RemoveHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,handler);}
        }
        void Replay(double expectedArea,int expectedCount=1)
        {
            var expected=ConfigurationIdentity(node.RuntimeConfiguration!);Require(solutions.SaveCurrentDocument(path).FileWritten,"继承方案保存失败");
            var loaded=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("重开继承方案失败");var loadedFlow=loaded.Workflows.Single(f=>f.Id==flow.Id);var restored=loadedFlow.Nodes.Single(n=>n.Id==node.Id);Require(ConfigurationIdentity(restored.RuntimeConfiguration!)==expected,"保存重开丢完整输入/变量/索引身份");
            var ownEngine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);ownEngine.RegisterWorkflow(loadedFlow);var ownHost=new NodeConfigurationEditHost(loaded);var publication=WorkflowPublishedConfiguration.Capture(ownHost.Capture(),loaded,ownHost);using var globals=new WorkflowGlobalRunValues(loaded.Id,publication.GlobalDefinitions,publication.GlobalDefinitions.Select(v=>new KeyValuePair<string,object?>(v.Id,v.Value)));var executor=new WorkflowExecutionEngine(ownEngine,new PluginManager(),SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);
            var task=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(loadedFlow.Id,hostInputs:new Dictionary<string,SunEyeVision.Plugin.Contracts.IHostInputPreparer>{{source.Id,new ImageSourceHostInputPreparer(imageFile,1){LocalReadAdmission=(_,_)=>imageFile}}}){Configuration=publication.Configuration,ExecutionOwner=publication.ExecutionOwner,ProducedGlobals=globals,ParameterValues=publication.ParameterValues,ResourcePaths=publication.ResourcePaths});PumpUntil(()=>task.IsCompleted);
            using(var result=task.GetAwaiter().GetResult())
            {
                Require(result.Success,"重开对象实际执行失败");var output=((DeclaredOutputSnapshot)result.NodeResults[node.Id].Result!).Output as BlobOutput??throw new Exception("重开后无Blob结果");
                var imageOutput=((DeclaredOutputSnapshot)result.NodeResults[source.Id].Result!).Output as SunEyeVision.Tool.ImageSource.Runtime.ImageSourceOutput??throw new Exception("重开后无图像源真实输出");
                var image=((RecordValue)imageOutput.ToValues()["output.image"]!).Get<IReadOnlyImage>("data");
                Require(output.Count==expectedCount&&output.Measurements.Sum(m=>m.Area)==expectedArea&&output.Region.SourceFrame==image.Frame,"重开结果裁剪或同轮物理帧错误");
            }
            loaded.ExecutionOwner.Retire();
        }
        void RingFields(DefaultDebugWindow w,string stage)
        {
            var config=Draft(w).DraftNode.RuntimeConfiguration!;var fields=BlobRoiInheritance.AnnulusMembers.Select(member=>BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters,member)).Select(id=>{var field=Input(w,id);var state=Get(field,"_state");return new{Id=id,EditorKind=field.Setting.GetType().Name,EditorIdentity=Identity(field.Setting),DraftKind=config.Fields.Read(id).GetType().Name,DraftIdentity=Identity(config.Fields.Read(id)),ReferenceMode=(bool)Get(state,"IsReferenceMode"),field.HasPendingEdits,field.HasInvalidText,field.Error};}).ToArray();
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"ring-fields.jsonl"),System.Text.Json.JsonSerializer.Serialize(new{Stage=stage,Mode=BlobRoiInheritance.ReadMode(config.Fields),Fields=fields,Errors=config.Validate().Select(d=>new{d.Code,d.Message,d.FieldId}).ToArray()})+Environment.NewLine);
        }
        if(Included("Window"))
        {
            Case("原默认面积10合法过滤9像素继承，关闭筛选恢复同一几何",()=>{
                var w=Open();Populate(w);Require(Input(w,rectangleIds[2]).TrySetConstant(2d),"宽2不能提交");var control=Control(w);var tabs=Logical(control).OfType<TabControl>().Single();tabs.SelectedIndex=1;Pump();var area=Logical(control).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.IsAreaFilterEnabled)));Require(area.TrySetConstant(true),"原面积开关不能开启");Require(((BlobSettings)Draft(w).DraftNode.RuntimeConfiguration!.CaptureSettings()).FilterAreaMin==10,"不是原默认下限10");tabs.SelectedIndex=0;Pump();var rejected=Observe(w);Require(rejected.Count==0&&rejected.Area==0,"默认面积10未过滤9像素区域");tabs.SelectedIndex=1;Pump();Require(area.TrySetConstant(false),"原面积开关不能关闭");tabs.SelectedIndex=0;Pump();var restored=Observe(w);Require(restored.Count==1&&restored.Area==9&&restored.Center&&!restored.Corner,"关闭筛选未恢复原9像素几何");w.Close();Pump();
            });
            Case("真实原四态条件与十一叶，空组合候选拒绝且预览有短Tooltip",()=>{
                var w=Open();var inputs=Logical(Control(w)).OfType<DeclaredInputFieldsView>().Single();Require(rectangleIds.All(id=>inputs.Fields.Count(f=>f.FieldId==id)==1),"原五叶未绑定同一正式输入");var ui=Logical(Control(w)).OfType<SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiUiDraftControls>().Single();Require(Visual(ui).OfType<RadioButton>().Count()>=8,"原四态radio消失");Require(ui.ToolTip?.ToString()?.Contains("预览尚未接入")==true,"未接入预览标识缺失");
                foreach(var selected in new[]{0,1,2,3}){SetMode(w,selected);Require(!Control(w).TryCapture(out error)&&error.Contains("继承来源"),"空模式未拒绝："+selected+"/"+error);Require(Input(w,BlobRoiInheritance.RectangleSource).IsVisible==(selected==0)&&Input(w,BlobRoiInheritance.AnnulusSource).IsVisible==(selected==2),"组合来源条件显隐错误："+selected);var rectPanel=(Panel)Get(ui,"_rectangleParameters");var ringPanel=(Panel)Get(ui,"_circleParameters");Require(rectPanel.IsVisible==(selected==1)&&ringPanel.IsVisible==(selected==3),"参数组条件显隐错误");}
                SetMode(w,0);var sourceSession=InputSession(Input(w,BlobRoiInheritance.RectangleSource));var window=Expanded(sourceSession,false);Require(!Tree((System.Collections.IEnumerable)Get(sourceSession,"SourceNodes")).Any(item=>Candidate(item)is not null),"无正式组合生产者却制造候选");SetMode(w,null);Require(Control(w).TryCapture(out error),error);w.Close();Pump();
            });
            Case("真实Host五叶常量执行敏感裁剪，取消不改正式或文件",()=>{
                var before=ConfigurationIdentity(node.RuntimeConfiguration!);var bytes=File.ReadAllBytes(path);var w=Open();Populate(w);Require(Control(w).TryCapture(out error),error);var output=Observe(w);Require(output.Count==1&&output.Area==15&&output.Center&&!output.Corner,"真实五叶未裁剪成15像素矩形："+output);w.Close();Pump();Require(ConfigurationIdentity(node.RuntimeConfiguration!)==before&&bytes.SequenceEqual(File.ReadAllBytes(path)),"取消改变正式或文件");
            });
            Case("活动非法原文拒绝，切休眠保原文不阻捕获，恢复重新拒绝",()=>{
                var w=Open();Populate(w);var field=Input(w,rectangleIds[2]);var text=NumericText(field);var baseline=text.Text;text.SetCurrentValue(TextBox.TextProperty,"roi-invalid");Pump();Require(!Control(w).TryCapture(out error)&&field.HasPendingEdits,"活动非法原文被忽略");SetMode(w,null);Require(Control(w).TryCapture(out error),error);Require(field.HasPendingEdits&&text.Text=="roi-invalid","休眠捕获丢原文");SetMode(w,1);Require(!Control(w).TryCapture(out error)&&text.Text=="roi-invalid","重新激活丢原文或误接纳");text.SetCurrentValue(TextBox.TextProperty,baseline);Require(field.TryCommitPending()&&Control(w).TryCapture(out error),error);w.Close();Pump();
            });
            Case("真实来源树写宽度并消费，确认历史撤销重做保全身份",()=>{
                var w=Open();Populate(w);var width=Input(w,rectangleIds[2]);ReferenceInput(width);Choose(InputSession(width),false,item=>Candidate(item)?.Reference(BindingSelection.All)is{} r&&r.NodeId==source.Id&&r.PortId=="output.image"&&r.MemberFieldPath.SequenceEqual(new[]{"width"}));var selected=Identity(width.Setting);var output=Observe(w);Require(output.Area==24&&output.Center&&!output.Corner,"正式宽度来源8未实际影响裁剪："+output);var position=history.Position;Confirm(w);Require(history.Position==position+1&&Identity(node.RuntimeConfiguration!.Fields.Read(rectangleIds[2]))==selected,"确认未写完整引用或单次历史");history.Undo();Require(node.RuntimeConfiguration!.Fields.Read(BlobRoiInheritance.ModeId)is UnsetSetting,"Undo未恢复无继承基线");history.Redo();Require(Identity(node.RuntimeConfiguration!.Fields.Read(rectangleIds[2]))==selected,"Redo丢引用身份");
            });
            Case("真实序列All按最小数量消费，固定和动态索引单项执行",()=>{
                var w=Open();Populate(w);var width=Input(w,rectangleIds[2]);ReferenceInput(width);var session=InputSession(width);SelectVariable(session,false,roiValues.Id);Require(width.Setting is VariableSetting variable&&variable.Reference.Type.Shape==ValueShape.Sequence&&variable.Reference.Selection is BindingSelection.AllItems,"没有实际All序列来源");
                var allIdentity=Identity(width.Setting);Require(Observe(w).Area==9&&Identity(width.Setting)==allIdentity,"其他四个单常量使min=1时未消费首个对应项或改掉All身份");
                SetIndex(session,"0");Require(Observe(w).Area==9,"固定0未消费宽2");SetIndex(session,"1");Require(Observe(w).Area==15,"固定1未消费宽4");SelectVariable(session,true,indexVariable.Id);Require(Observe(w).Area==15&&width.Setting is VariableSetting{Reference.Selection:BindingSelection.AtVariable},"动态变量索引未消费单项");
                Choose(session,true,item=>Candidate(item)?.Reference(BindingSelection.All)is{} r&&r.NodeId==source.Id&&r.PortId=="output.image"&&r.MemberFieldPath.SequenceEqual(new[]{"width"}));Require(Observe(w).Area==21&&width.Setting is VariableSetting{Reference.Selection:BindingSelection.AtField},"动态字段索引8未消费宽6");w.Close();Pump();
            });
            Case("真实冻结后变量更替只影响下次裁剪，取消不发布正式",()=>{
                var before=ConfigurationIdentity(node.RuntimeConfiguration!);var w=Open();Populate(w);var width=Input(w,rectangleIds[2]);ReferenceInput(width);var session=InputSession(width);SelectVariable(session,false,roiValues.Id);SetIndex(session,"0");var live=solutions.CurrentSolution!.GlobalVariables.Single(v=>v.Id==roiValues.Id);var original=(double[])live.Value!;var control=Control(w);var originalCapture=(Func<WorkflowNodeBase,RuntimeDebugInvocation>)control.GetType().GetField("_captureDebug",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(control)!;
                control.SetDebugCapture(draft=>{var frozen=originalCapture(draft);var next=(double[])original.Clone();next[0]=6;live.Value=next;return frozen;});
                try{Require(Observe(w).Area==9,"冻结快照被后续变量覆盖");control.SetDebugCapture(originalCapture);Require(Observe(w).Area==21,"下轮未消费新变量");w.Close();Pump();Require(ConfigurationIdentity(node.RuntimeConfiguration!)==before,"私有运行/取消改正式");}finally{control.SetDebugCapture(originalCapture);live.Value=original;if(w.IsVisible)w.Close();Pump();}
            });
            Case("真实五叶All多矩形与不等长min，单项索引及确认重开执行",()=>{
                var w=Open();Populate(w);for(var i=0;i<rectangleIds.Length;i++){var field=Input(w,rectangleIds[i]);ReferenceInput(field);SelectVariable(InputSession(field),false,rectangleSeries[i].Id);}
                var multiple=Observe(w);Require(multiple.Count==2&&multiple.Area==18&&multiple.Corner&&multiple.Second&&!multiple.Center&&!multiple.Outside,"五叶All未形成两块独立9像素矩形及中间空域");var width=Input(w,rectangleIds[2]);var session=InputSession(width);SetIndex(session,"0");var one=Observe(w);Require(one.Count==1&&one.Area==9&&one.Corner&&!one.Second,"单项宽度索引没有使min-count降1");Choose(session,true,item=>(bool)Get(item,"Clear"));Require(Observe(w).Count==2,"清索引后未恢复All两项");
                var live=solutions.CurrentSolution!.GlobalVariables.Single(v=>v.Id==rectangleSeries[1].Id);var original=live.Value;try{live.Value=new[]{1d};Require(Observe(w).Count==1,"不等长叶未按最小数量执行");}finally{live.Value=original;}
                var final=Observe(w);Require(final.Count==2,"恢复数量未恢复两项");Confirm(w);Replay(final.Area,2);
            });
            Case("真实六叶All多圆环及确认保存重开，单常量不广播",()=>{
                var w=Open();SetMode(w,3);var ids=BlobRoiInheritance.AnnulusMembers.Select(member=>BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters,member)).ToArray();
                RingFields(w,"opened");for(var i=0;i<ids.Length;i++){var field=Input(w,ids[i]);ReferenceInput(field);try{SelectVariable(InputSession(field),false,annulusSeries[i].Id);Require(field.Setting is VariableSetting accepted&&accepted.Reference.VariableId==annulusSeries[i].Id,"圆环叶未真实接纳："+ids[i]);}finally{RingFields(w,"selected-"+i+"-"+ids[i]);}}
                RingFields(w,"before-first-run");var multiple=Observe(w);File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"ring-result.json"),System.Text.Json.JsonSerializer.Serialize(new{multiple.Count,multiple.Area,multiple.Center,multiple.Corner,multiple.Second,multiple.Outside,multiple.RingEdge,multiple.FirstHole,multiple.FirstRingEdge}));RingFields(w,"after-first-run");Require(multiple.Count==2&&multiple.Area==12&&multiple.FirstRingEdge&&multiple.RingEdge&&!multiple.FirstHole&&!multiple.Second&&!multiple.Center&&!multiple.Outside,"六叶All没有形成两个有孔实际圆环及中间空域");var first=Input(w,ids[0]);Require(first.TrySetConstant(1d),"无法显式改单常量");RingFields(w,"constant-one");Require(Observe(w).Count==1,"单常量被广播到其他多项叶");ReferenceInput(first);SelectVariable(InputSession(first),false,annulusSeries[0].Id);RingFields(w,"reselected-first");var final=Observe(w);Require(final.Count==2,"重新订阅All未恢复多圆环");Confirm(w);Replay(final.Area,2);
            });
            Case("圆环参数六叶真实窗口逐项配置与来源、确认保存重开执行",()=>{
                var ids=BlobRoiInheritance.AnnulusMembers.Select(member=>BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters,member)).ToArray();var empty=ids.ToDictionary(id=>id,id=>(ParameterSetting)new UnsetSetting());empty[BlobRoiInheritance.ModeId]=new ConstantSetting(BlobRoiInheritance.Local);FormalEdit(empty);var w=Open();SetMode(w,3);var numbers=new[]{3d,3d,1d,3d,0d,360d};
                Require(!Control(w).TryCapture(out error)&&error.Contains("继承来源"),"空圆环模式应可编辑但不能捕获");
                for(var i=0;i<ids.Length;i++){Require(Input(w,ids[i]).TrySetConstant(numbers[i]),"圆环原数值控件不能配置："+ids[i]);if(i<ids.Length-1)Require(!Control(w).TryCapture(out error),"缺叶时提前接纳圆环");}
                Require(Control(w).TryCapture(out error),error);var original=Observe(w);Require(original.Count==1&&original.Area>0&&!original.Center&&!original.Corner,"圆环真实窗口未保内孔");
                var inner=Input(w,ids[2]);ReferenceInput(inner);var innerSession=InputSession(inner);SelectVariable(innerSession,false,roiValues.Id);SetIndex(innerSession,"0");var narrowed=Observe(w);Require(narrowed.Count==1&&narrowed.Area>0&&narrowed.Area<original.Area&&!narrowed.Center,"真实内半径来源2未影响圆环集合");var identity=Identity(inner.Setting);Confirm(w);Require(Identity(node.RuntimeConfiguration!.Fields.Read(ids[2]))==identity,"圆环确认丢来源");Replay(narrowed.Area);
                var old=ConfigurationIdentity(node.RuntimeConfiguration);var bytes=File.ReadAllBytes(path);var cancel=Open();SetMode(cancel,null);cancel.Close();Pump();Require(ConfigurationIdentity(node.RuntimeConfiguration!)==old&&bytes.SequenceEqual(File.ReadAllBytes(path)),"圆环取消改正式或保存文件");
            });
            Case("多项常量两表示保值取消、不取首并允许显式单值修复",()=>{
                var baseline=ConfigurationIdentity(node.RuntimeConfiguration!);var initialPosition=history.Position;try
                {
                    foreach(var sequence in new[]{false,true})
                    {
                        var changes=RectangleFields();var port=new BlobTool().Describe().Inputs.Single(p=>p.Id==rectangleIds[2]);changes[rectangleIds[2]]=new ConstantSetting(sequence?new SequenceValue(port.Type,new object[]{2d,4d}):new double[]{2,4});FormalEdit(changes);var expected=ConfigurationIdentity(node.RuntimeConfiguration!);var bytes=File.ReadAllBytes(path);var w=Open();var field=Input(w,rectangleIds[2]);Require(NumericText(field).Text==""&&field.HasInvalidText,"多项常量被悄悄显示首值或零值");Require(!Control(w).TryCapture(out error),"不可单值显示的常量被伪接纳");w.Close();Pump();Require(ConfigurationIdentity(node.RuntimeConfiguration!)==expected&&bytes.SequenceEqual(File.ReadAllBytes(path)),"打开取消改变原多项常量");
                        var fix=Open();var repaired=Input(fix,rectangleIds[2]);Require(repaired.TrySetConstant(4d)&&Control(fix).TryCapture(out error),error);var observed=Observe(fix);Require(observed.Area==15,"显式单常量修复没有消费4");fix.Close();Pump();Require(ConfigurationIdentity(node.RuntimeConfiguration!)==expected,"取消修复改变原多项常量");history.Undo();
                    }
                }finally{if(history.Position>initialPosition)history.Undo();Require(ConfigurationIdentity(node.RuntimeConfiguration!)==baseline,"多常量夹具未恢复正式事务");}
            });
        }
        if(Included("Publication"))
        {
            Case("真实确认保存重开对象执行矩形裁剪，休眠引用身份和取消保留",()=>{
                var w=Open();Populate(w);var width=Input(w,rectangleIds[2]);ReferenceInput(width);var session=InputSession(width);SelectVariable(session,false,roiValues.Id);SetIndex(session,"0");Require(Observe(w).Area==9,"保存前未实际裁剪");Confirm(w);var expected=ConfigurationIdentity(node.RuntimeConfiguration!);Require(solutions.SaveCurrentDocument(path).FileWritten,"继承方案保存失败");
                var loaded=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("重开继承方案失败");var loadedFlow=loaded.Workflows.Single(f=>f.Id==flow.Id);var restored=loadedFlow.Nodes.Single(n=>n.Id==node.Id);Require(ConfigurationIdentity(restored.RuntimeConfiguration!)==expected,"保存重开丢完整输入/变量/索引身份");
                var ownEngine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);ownEngine.RegisterWorkflow(loadedFlow);var ownHost=new NodeConfigurationEditHost(loaded);var publication=WorkflowPublishedConfiguration.Capture(ownHost.Capture(),loaded,ownHost);using var globals=new WorkflowGlobalRunValues(loaded.Id,publication.GlobalDefinitions,publication.GlobalDefinitions.Select(v=>new KeyValuePair<string,object?>(v.Id,v.Value)));var executor=new WorkflowExecutionEngine(ownEngine,new PluginManager(),SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);
                var task=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(loadedFlow.Id,hostInputs:new Dictionary<string,SunEyeVision.Plugin.Contracts.IHostInputPreparer>{{source.Id,new ImageSourceHostInputPreparer(imageFile,1){LocalReadAdmission=(_,_)=>imageFile}}}){Configuration=publication.Configuration,ExecutionOwner=publication.ExecutionOwner,ProducedGlobals=globals,ParameterValues=publication.ParameterValues,ResourcePaths=publication.ResourcePaths});PumpUntil(()=>task.IsCompleted);
                using(var result=task.GetAwaiter().GetResult()){Require(result.Success&&result.NodeResults[node.Id].Result is DeclaredOutputSnapshot{Output:BlobOutput output}&&output.Count==1&&output.Measurements.Sum(m=>m.Area)==9&&RegionContains(output.Region,3,3)&&!RegionContains(output.Region,0,0),"真实重开对象未消费继承裁剪");}loaded.ExecutionOwner.Retire();
                var bytes=File.ReadAllBytes(path);var cancel=Open();SetMode(cancel,null);cancel.Close();Pump();Require(ConfigurationIdentity(node.RuntimeConfiguration!)==expected&&bytes.SequenceEqual(File.ReadAllBytes(path)),"重开后的取消清除继承正式配置");
            });
        }

        shell.Close();dialogs.Stop();Pump();Console.WriteLine("Blob ROI继承真实生产链："+_passed+"组通过；不代原厂全域/自然焦点/剪贴板/连续会话/ROI图文。");return 0;
    }
    private static object? _lastChoice;
    private static bool RegionContains(IReadOnlyRegion region,int x,int y)
    {
        using var reader=region.AcquireReader();
        for(var index=0;index<reader.RunCount;){var runs=reader.ReadRuns(index,Math.Min(4096,reader.RunCount-index));foreach(var run in runs.Span)if(run.Row==y&&run.FirstColumn<=x&&run.LastColumnInclusive>=x)return true;index+=runs.Length;}
        return false;
    }
    private static void InheritanceModelCases()
    {
        using var resources=new FrozenOutputResources();var image=resources.CreateGray8(64,64,Enumerable.Repeat((byte)200,4096).ToArray());
        var tool=new BlobTool();var settings=new BlobSettings{IsAreaFilterEnabled=false,IsCircularityFilterEnabled=false,IsRectangularityFilterEnabled=false,OutputGrayImage=false,OutputBoundary=false};
        BlobInput Definition(string mode,double[] numbers,bool combination=false)
        {
            var rectangle=mode is BlobRoiInheritance.Rectangle or BlobRoiInheritance.RectangleParameters;
            var members=rectangle?BlobRoiInheritance.RectangleMembers:BlobRoiInheritance.AnnulusMembers;
            var values=new Dictionary<string,object?>{{BlobTool.ImageInput,image},{BlobRoiInheritance.ModeId,mode}};
            if(combination)values.Add(rectangle?BlobRoiInheritance.RectangleSource:BlobRoiInheritance.AnnulusSource,
                new SequenceValue(TypeRef.SequenceOf(rectangle?BlobRoiInheritance.RectangleType:BlobRoiInheritance.AnnulusType),new[]{new RecordValue(rectangle?BlobRoiInheritance.RectangleType:BlobRoiInheritance.AnnulusType,members.Select((id,index)=>new KeyValuePair<string,object?>(id,numbers[index])).ToDictionary(pair=>pair.Key,pair=>pair.Value))}));
            else foreach(var pair in members.Select((id,index)=>(id,index)))values.Add(BlobRoiInheritance.Leaf(mode,pair.id),new[]{numbers[pair.index]});
            return BlobInput.Read(new InputValues(tool.Describe().Inputs,values));
        }
        BlobOutput Run(string mode,double[] numbers,bool combination=false)
        {var session=new BlobSession();try{return session.Execute(settings,Definition(mode,numbers,combination),resources);}finally{session.DisposeAsync().GetAwaiter().GetResult();}}
        Case("本方矩形5叶每项敏感及组合与逐叶同值物化",()=>{
            var baseline=new[]{20d,20d,12d,4d,0d};
            using var original=Run(BlobRoiInheritance.RectangleParameters,baseline);Require(RegionContains(original.Region,25,20)&&!RegionContains(original.Region,20,25),"矩形宽高未消费");
            foreach(var sample in new[]{(0,30d,20,20,false),(1,30d,20,20,false),(2,4d,25,20,false),(3,12d,20,25,true)}){var numbers=(double[])baseline.Clone();numbers[sample.Item1]=sample.Item2;using var result=Run(BlobRoiInheritance.RectangleParameters,numbers);Require(RegionContains(result.Region,sample.Item3,sample.Item4)==sample.Item5,"矩形叶未独立生效："+sample.Item1);}
            foreach(var sign in new[]{1d,-1d}){using var result=Run(BlobRoiInheritance.RectangleParameters,new[]{20d,20d,12d,4d,45*sign});Require(RegionContains(result.Region,24,sign>0?24:16)&&!RegionContains(result.Region,24,sign>0?16:24),"角度未消费");}
            using var combined=Run(BlobRoiInheritance.Rectangle,baseline,true);Require(combined.Measurements.Sum(m=>m.Area)==original.Measurements.Sum(m=>m.Area),"同值组合物化不一致");
        });
        Case("本方圆环6叶半径而非直径与角度范围敏感",()=>{
            var baseline=new[]{20d,20d,3d,10d,0d,90d};
            using var original=Run(BlobRoiInheritance.AnnulusParameters,baseline);Require(RegionContains(original.Region,26,26)&&!RegionContains(original.Region,26,14)&&!RegionContains(original.Region,21,21),"圆环象限/半径基准不对");
            foreach(var sample in new[]{(0,30d,26,26,false),(1,30d,26,26,false),(2,9d,25,25,false),(3,7d,26,26,false),(4,-90d,26,14,true),(5,-90d,26,14,true)})
            {
                var numbers=(double[])baseline.Clone();numbers[sample.Item1]=sample.Item2;using var result=Run(BlobRoiInheritance.AnnulusParameters,numbers);
                if(RegionContains(result.Region,sample.Item3,sample.Item4)!=sample.Item5)
                {
                    var typed=Definition(BlobRoiInheritance.AnnulusParameters,numbers);var shape=BlobLocalRoi.Items(typed.LocalRoi!,BlobLocalRoi.Inspection).Single().Get<RecordValue>("shape");
                    HalconDotNet.HOperatorSet.GenCircle(out var inner,20d,20d,numbers[2]);using(inner)
                    {
                        HalconDotNet.HOperatorSet.GenCircleSector(out var outer,20d,20d,numbers[3],3*Math.PI/2,0d);using(outer)
                        {
                            HalconDotNet.HOperatorSet.Difference(outer,inner,out var difference);using(difference)
                            {
                                HalconDotNet.HOperatorSet.TestRegionPoint(inner,26,26,out var inInner);HalconDotNet.HOperatorSet.TestRegionPoint(difference,26,26,out var inDifference);HalconDotNet.HOperatorSet.TestRegionPoint(difference,25,25,out var inDifference25);
                                HalconDotNet.HOperatorSet.GetRegionRuns(inner,out var rows,out var first,out var last);var nativeRuns=Enumerable.Range(0,rows.Length).Where(i=>rows[i].I is >=24 and <=27).Select(i=>new[]{rows[i].I,first[i].I,last[i].I}).ToArray();
                                var managedRuns=new List<int[]>();using(var read=result.Region.AcquireReader()){for(var i=0;i<read.RunCount;){var runs=read.ReadRuns(i,Math.Min(4096,read.RunCount-i));foreach(var run in runs.Span)if(run.Row is >=24 and <=27)managedRuns.Add(new[]{run.Row,run.FirstColumn,run.LastColumnInclusive});i+=runs.Length;}}
                                var diagnostic=new{sample.Item1,Input=numbers,Inner=shape.Get<double>("inner-radius"),Outer=shape.Get<double>("outer-radius"),Start=shape.Get<double>("start-radians"),End=shape.Get<double>("end-radians"),Managed26=RegionContains(result.Region,26,26),Managed25=RegionContains(result.Region,25,25),ManagedArea=result.TotalArea,DirectInner26=inInner.I,DirectDifference26=inDifference.I,DirectDifference25=inDifference25.I,NativeInnerRuns=nativeRuns,ManagedRuns=managedRuns};
                                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"annulus-diagnostic.json"),System.Text.Json.JsonSerializer.Serialize(diagnostic,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(diagnostic));
                            }
                        }
                    }
                }
                Require(RegionContains(result.Region,sample.Item3,sample.Item4)==sample.Item5,"圆环叶未独立生效："+sample.Item1);
            }
            using var combined=Run(BlobRoiInheritance.Annulus,baseline,true);Require(combined.Measurements.Sum(m=>m.Area)==original.Measurements.Sum(m=>m.Area),"同值环组合物化不一致");
        });
        Case("本方正负跨180小弧大弧与正负整环保内孔",()=>{
            foreach(var sample in new[]{(170d,20d,14,20,26,20),(170d,-340d,26,20,14,20),(0d,360d,26,20,20,20),(0d,-360d,26,20,20,20)}){using var result=Run(BlobRoiInheritance.AnnulusParameters,new[]{20d,20d,3d,10d,sample.Item1,sample.Item2});Require(RegionContains(result.Region,sample.Item3,sample.Item4)&&!RegionContains(result.Region,sample.Item5,sample.Item6),"跨界/整环错误："+sample);}
        });
        Case("活动缺叶及缺组合必需成员不使用旧值或native猜测默认",()=>{
            foreach(var mode in new[]{BlobRoiInheritance.RectangleParameters,BlobRoiInheritance.AnnulusParameters}){var members=mode==BlobRoiInheritance.RectangleParameters?BlobRoiInheritance.RectangleMembers:BlobRoiInheritance.AnnulusMembers;foreach(var missing in members){var values=new Dictionary<string,object?>{{BlobTool.ImageInput,image},{BlobRoiInheritance.ModeId,mode}};foreach(var member in members.Where(member=>member!=missing))values.Add(BlobRoiInheritance.Leaf(mode,member),new[]{1d});var rejected=false;try{_=BlobInput.Read(new InputValues(tool.Describe().Inputs,values));}catch(KeyNotFoundException){rejected=true;}catch(ArgumentException){rejected=true;}Require(rejected,"缺叶被默认："+missing);}}
            var malformed=false;try{_=new RecordValue(BlobRoiInheritance.RectangleType,new Dictionary<string,object?>{{"center-x",1d}});}catch(ArgumentException){malformed=true;}Require(malformed,"组合缺4成员被接纳");
        });
        Case("非法尺寸半径跨度与数值拒绝，未扩大成全图",()=>{
            foreach(var sample in new[]{new[]{20d,20d,0d,4d,0d},new[]{20d,20d,double.NaN,4d,0d},new[]{20d,20d,double.Epsilon,4d,0d}}){var rejected=false;try{using var result=Run(BlobRoiInheritance.RectangleParameters,sample);}catch(ArgumentException){rejected=true;}Require(rejected,"非法矩形被接纳");}
            foreach(var sample in new[]{new[]{20d,20d,10d,3d,0d,90d},new[]{20d,20d,3d,10d,0d,0d},new[]{20d,20d,3d,10d,0d,361d}}){var rejected=false;try{using var result=Run(BlobRoiInheritance.AnnulusParameters,sample);}catch(ArgumentException){rejected=true;}Require(rejected,"非法圆环被接纳");}
        });
        BlobInput MultipleInput(string mode,double[][] numbers,bool sequenceValue=false)
        {
            var members=mode==BlobRoiInheritance.RectangleParameters?BlobRoiInheritance.RectangleMembers:BlobRoiInheritance.AnnulusMembers;
            var values=new Dictionary<string,object?>{{BlobTool.ImageInput,image},{BlobRoiInheritance.ModeId,mode}};
            for(var i=0;i<members.Count;i++){var id=BlobRoiInheritance.Leaf(mode,members[i]);var type=tool.Describe().Inputs.Single(p=>p.Id==id).Type;values.Add(id,sequenceValue?new SequenceValue(type,numbers[i].Cast<object?>()):numbers[i]);}
            return BlobInput.Read(new InputValues(tool.Describe().Inputs,values));
        }
        Case("原生数量分支适配：不等长取min、64上限和第65项排除",()=>{
            foreach(var sequence in new[]{false,true})
            {
                var x=Enumerable.Range(0,65).Select(i=>i==64?60d:2d+i%8*6).ToArray();var y=Enumerable.Range(0,65).Select(i=>i==64?60d:2d+i/8*6).ToArray();var width=Enumerable.Repeat(1d,65).ToArray();var angle=new double[65];
                var typed=MultipleInput(BlobRoiInheritance.RectangleParameters,new[]{x,y,width,width,angle},sequence);Require(BlobLocalRoi.Items(typed.LocalRoi!,BlobLocalRoi.Inspection).Count==64,"临时几何不是64项");var session=new BlobSession();try{using var output=session.Execute(settings,typed,resources);Require(output.Count==64&&!RegionContains(output.Region,60,60),"第65项未排除或64项未实际消费");}finally{session.DisposeAsync().GetAwaiter().GetResult();}
                var limited=MultipleInput(BlobRoiInheritance.RectangleParameters,new[]{x,y.Take(2).ToArray(),width,width,angle},sequence);Require(BlobLocalRoi.Items(limited.LocalRoi!,BlobLocalRoi.Inspection).Count==2,"不等长未取最小数量");
                var singleton=MultipleInput(BlobRoiInheritance.RectangleParameters,new[]{x,y,new[]{1d},width,angle},sequence);Require(BlobLocalRoi.Items(singleton.LocalRoi!,BlobLocalRoi.Inspection).Count==1,"单常量被广播");
            }
        });
        Case("空叶和空组合保护，两种数值序列表示同值物化",()=>{
            var good=new[]{new[]{10d,30d},new[]{10d,30d},new[]{4d,4d},new[]{4d,4d},new[]{0d,0d}};
            var a=MultipleInput(BlobRoiInheritance.RectangleParameters,good);var b=MultipleInput(BlobRoiInheritance.RectangleParameters,good,true);Require(BlobLocalRoi.Same(a.LocalRoi,b.LocalRoi),"double[]与SequenceValue产生不同几何");
            for(var i=0;i<good.Length;i++){var bad=(double[][])good.Clone();bad[i]=Array.Empty<double>();var rejected=false;try{_=MultipleInput(BlobRoiInheritance.RectangleParameters,bad);}catch(ArgumentException e){rejected=e.Message.Contains("没有有效数据");}Require(rejected,"空叶未明确保护："+i);}
            var empty=false;try{_=BlobInput.Read(new InputValues(tool.Describe().Inputs,new Dictionary<string,object?>{{BlobTool.ImageInput,image},{BlobRoiInheritance.ModeId,BlobRoiInheritance.Rectangle},{BlobRoiInheritance.RectangleSource,new SequenceValue(TypeRef.SequenceOf(BlobRoiInheritance.RectangleType),Array.Empty<object>())}}));}catch(ArgumentException e){empty=e.Message.Contains("没有有效数据");}Require(empty,"空组合未保护");
        });
    }
    private static object Get(object target,string name)
    {var property=target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(property is not null)return property.GetValue(target)!;var field=target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(field is not null)return field.GetValue(target)!;throw new Exception("缺少真实成员："+name);}
    private static DeclaredReferenceCandidate? Candidate(object item)=>(DeclaredReferenceCandidate?)Get(item,"Candidate");
    private static AppsPopup? _activePopup;
    private static object Session(DeclaredFieldAccess field){_activePopup=Visual(field.View).OfType<AppsPopup>().Single(item=>item.Name=="rt"&&item.DataContext?.GetType().GetProperty("Target",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(item.DataContext) is Port target&&target.Id==field.FieldId);_activePopup.ApplyTemplate();return _activePopup.DataContext;}
    private static void Subscribe(DeclaredFieldAccess field){if(field.Setting is ReferenceSetting or VariableSetting)return;var right=field.FieldId==BindingId("MaxArea");var candidates=Visual(field.View).OfType<ToggleButton>().ToArray();var matches=candidates.Where(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path==(right?"IsSubscribeRight":"IsSubscribeLeft")).ToArray();Require(matches.Length==1,"原订阅按钮数量不符：field="+field.FieldId+";loaded="+field.View.IsLoaded+";visible="+field.View.IsVisible+";paths="+string.Join(",",candidates.Select(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path)));var toggle=matches[0];toggle.SetCurrentValue(ToggleButton.IsCheckedProperty,true);toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();}
    private static IEnumerable<object> Tree(System.Collections.IEnumerable nodes){foreach(var node in nodes){yield return node!;foreach(var child in Tree((System.Collections.IEnumerable)Get(node!,"EnumEntrysA")))yield return child;}}
    private static ReferenceWindowOwner Root(DefaultDebugWindow window)=>(ReferenceWindowOwner)typeof(RuntimeNodeConfigurationControl).GetField("_referenceWindowOwner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(((ContentControl)window.FindName("ContentHost")).Content)!;
    private static SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow Expanded(object session,bool index){var owner=(DefaultDebugWindow)Window.GetWindow(_activePopup!)!;((ICommand)Get(session,"PopSubscribeWindowCommand")).Execute(null);Pump();var root=Root(owner);var window=owner.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Single(w=>w.IsVisible);Require(ReferenceEquals(window.Owner,owner)&&ReferenceEquals(window.DataContext,root)&&root.CurrentRequest is {IsValid:true} request&&request.Target.Id==((Port)Get(session,"Target")).Id,"展开窗/Owner/目标不是正式同一Root");var english=SunEyeVision.Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("Language")=="English";Require(window.Width==(english?320:240),"非模态窗语言宽度不符");if(index){var text=(string)Get(session,"CurValueA");((ICommand)Get(session,"FocusedCommand")).Execute(new SunEyeVision.Plugin.Wpf.Interop.Windows.TextBoxFocusEventArgs{Text=text,IsFocus=true,SelectionStart=text.LastIndexOf('[')+1});Pump();Require(root.CurrentRequest is{IsValid:true,IsIndex:true,HasSelection:true}&&window.IsSubscribeIndex,"索引真实焦点适配未切原索引树");}var none=english?"None":"无";foreach(var nodes in new[]{root.SourceNodes,root.IndexNodes}){var clear=Tree((System.Collections.IEnumerable)nodes).Single(item=>(bool)Get(item,"Clear"));Require((string)Get(clear,"Name")==none,"当前语言原树无项未使用正式None资源");}var shownTree=ExpandedTree(window,index);var shownClear=Tree((System.Collections.IEnumerable)(index?root.IndexNodes:root.SourceNodes)).Single(item=>(bool)Get(item,"Clear"));var clearContainer=Container(shownTree,shownClear);Require(Visual(clearContainer).OfType<TextBlock>().Any(text=>text.Text==none),"实际原树容器未渲染本语言None文本");return window;}
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
            try
            {
                var target=(Port)Get(session,"Target");
                var pages=Math.Max(1,(dialog.ItemsTotalCount+dialog.PageCount-1)/dialog.PageCount);var visited=0;
                while(!dialog.BindItems.Any(row=>row.Reference.VariableId==id))
                {
                    File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"variable-pages.jsonl"),System.Text.Json.JsonSerializer.Serialize(new{Target=target.Id,Expected=id,Page=dialog.CurrentPageNum,PageSize=dialog.PageCount,Candidates=dialog.BindItems.Select(row=>row.Reference.VariableId).ToArray()})+Environment.NewLine);
                    Require(++visited<pages,"全部实际分页中未找到变量："+id);var before=dialog.CurrentPageNum;
                    var next=Visual(dialog).OfType<Button>().Single(button=>button.Name=="PART_ButtonNextPage");Require(next.IsVisible&&next.IsEnabled,"原分页下一页不可操作");next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();Require(dialog.CurrentPageNum==before+1,"原分页按钮未真正切换候选页");
                }
                var trace=new{Target=target.Id,TargetType=target.Type.ToString(),IndexMode=index,ExpectedVariable=id,Raw=(string)Get(session,"CurValueA"),Candidates=dialog.BindItems.Select(row=>new{row.Name,Id=row.Reference.VariableId,Identity=BindingReferenceSerializer.Serialize(row.Reference)}).ToArray(),DocumentVariables=ServiceInitializer.SolutionManager.CurrentSolution!.GlobalVariables.Select(v=>new{v.Id,v.Name,v.Type}).ToArray()};
                File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"variable-candidates.jsonl"),System.Text.Json.JsonSerializer.Serialize(trace)+Environment.NewLine);
                var item=dialog.BindItems.Single(row=>row.Reference.VariableId==id);((ListView)dialog.FindName("globalVarList")).SelectedItem=item;Visual(dialog).OfType<Button>().Single(button=>Equals(button.Content,dialog.TryFindResource("Confirm"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));selected=true;
            }
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
        if(Environment.GetEnvironmentVariable("BLOB_ROI_TRACE_ONLY")=="AllMin"&&!name.StartsWith("真实序列All按",StringComparison.Ordinal))return;
        if(Environment.GetEnvironmentVariable("BLOB_ROI_TRACE_ONLY")=="RingCandidates"&&!name.StartsWith("真实五叶All多矩形",StringComparison.Ordinal)&&!name.StartsWith("真实六叶All多圆环",StringComparison.Ordinal))return;
        var file=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"roi-inheritance-progress.log");
        File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" BEGIN "+name+Environment.NewLine);
        try{action();_passed++;Console.WriteLine("通过："+name);File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" PASS "+name+Environment.NewLine);}
        catch(Exception failure){File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" FAIL "+name+Environment.NewLine+failure+Environment.NewLine);throw;}
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
