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
        var caseSet=Environment.GetEnvironmentVariable("S4_BLOB_RANGE_CASE_SET")??"Core";if(caseSet is not ("Core" or "Sensitivity" or "Publication" or "All"))throw new Exception("未知范围case set");
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
        var scope=Environment.GetEnvironmentVariable("S4_BLOB_RANGE_SCOPE")??"Area";
        var spec=scope switch{"Area"=>("MinArea","MaxArea","IsAreaFilterEnabled"),"Circularity"=>("MinCircularity","MaxCircularity","IsCircularityFilterEnabled"),"Rectangularity"=>("MinRectangularity","MaxRectangularity","IsRectangularityFilterEnabled"),_=>throw new Exception("未知scope")};
        var integer=scope=="Area";var threshold=BindingId(spec.Item1);var upper=BindingId(spec.Item2);var enableId=SettingId(spec.Item3);
        object Number(double value)=>integer?(object)(long)value:value;
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
        var producer=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(producer);
        Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString(),SourceNodeId=source.Id,TargetNodeId=producer.Id}),"生产者图像边失败");
        var producerEdge=new WorkflowConnection{Id=Guid.NewGuid().ToString(),SourceNodeId=producer.Id,TargetNodeId=node.Id};Require(vm.AddConnectionToWorkflow(producerEdge),"生产者前序边失败");
        var history=solutions.Session!.History;var host=solutions.Session.Editing.Host;
        var variableEdit=host.BeginVariableEdit();
        var values=new GlobalVariable("范围数组",integer?(object)new long[]{12,60,20,30,40,50,70,80,1}:new double[]{.2,.9,.3,.4,.5,.6,.7,.8,.1},integer?"Integer[]":"Double[]");
        var indexVariable=new GlobalVariable("真实索引",1L,"Integer");variableEdit.Configuration.GlobalVariables.Add(values);variableEdit.Configuration.GlobalVariables.Add(indexVariable);host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variableEdit);
        var setup=Open();var setupControl=Control(setup);var imageField=Logical(setupControl).OfType<DeclaredInputFieldsView>().Single().Fields.Single(f=>f.FieldId==BlobTool.ImageInput);var imageCombo=(ComboBox)imageField.InputElement!;
        imageCombo.SelectedItem=imageCombo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ReferenceSetting r&&r.Reference.NodeId==source.Id&&r.Reference.PortId=="output.image"&&r.Reference.MemberFieldPath.SequenceEqual(new[]{"data"}));Pump();Require(setup.TryConfirm(out var error),error);if(setup.IsVisible)setup.Close();Pump();Require(solutions.SaveCurrentDocument(path).FileWritten,"基准方案保存失败");
        var initialRun=vm.RunSelectedImageAsync();PumpUntil(()=>initialRun.IsCompleted);initialRun.GetAwaiter().GetResult();Require(Context().GetNodeResult(source.Id)?.Result is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {IsSuccess:true},"正式上游未产生本次源快照");
        DeclaredFieldAccess Low(DefaultDebugWindow window){Threshold(window);return Logical(Control(window)).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==threshold);}
        ProductionEditorContext Draft(DefaultDebugWindow window)=>(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(window))!;
        DeclaredFieldAccess Field(DefaultDebugWindow w,int end){Threshold(w);return Logical(Control(w)).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==(end==0?threshold:upper));}
        void Enable(DefaultDebugWindow w,bool value){Threshold(w);foreach(var advanced in Logical(Control(w)).OfType<NativeAdvancedPresenter>())advanced.IsExpanded=true;w.UpdateLayout();Pump();var fields=Logical(Control(w)).OfType<DeclaredFieldsView>().Single().Fields;foreach(var other in new[]{"IsAreaFilterEnabled","IsCircularityFilterEnabled","IsRectangularityFilterEnabled"}.Where(name=>name!=spec.Item3)){var f=fields.Single(f=>f.FieldId==SettingId(other));Require(f.TrySetConstant(false)&&f.Setting is ConstantSetting{Value:false},"其他筛选未明确关闭："+other);}Require(fields.Single(f=>f.FieldId==enableId).TrySetConstant(value),"目标开关被拒");w.UpdateLayout();Pump();}
        RuntimeToolConfiguration Config(DefaultDebugWindow w)=>Draft(w).DraftNode.RuntimeConfiguration!;
        double Value(RuntimeToolConfiguration c,string id)=>Convert.ToDouble(c.ReadCurrentParameter(id)?.Value??throw new Exception("无当前值"),System.Globalization.CultureInfo.InvariantCulture);
        void Pair(RuntimeToolConfiguration c,double low,double high)=>Require(Value(c,threshold)==low&&Value(c,upper)==high,"实际双端快照不同："+Value(c,threshold)+"/"+Value(c,upper));
        BlobOutput Producer()=>((DeclaredOutputSnapshot)Context().GetNodeResult(producer.Id)!.Result).Output as BlobOutput??throw new Exception("无真实前序结果");
        string[] ShapeSet(IEnumerable<BlobMeasurement> rows)=>rows.Select(m=>string.Join("/",new[]{m.Area,m.Circularity,m.Rectangularity,m.CentroidX,m.CentroidY}.Select(n=>n.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        void AssertResult(double low,double high,BlobOutput actual,BlobOutput baseline,string label,ImageFrameAddress? baselineFrame=null,ImageFrameAddress? actualFrame=null){double Feature(BlobMeasurement m)=>scope=="Area"?m.Area:scope=="Circularity"?m.Circularity:m.Rectangularity;var expected=baseline.Measurements.Where(m=>Feature(m)>=low&&Feature(m)<=high).ToArray();var wanted=ShapeSet(expected);var found=ShapeSet(actual.Measurements);baselineFrame??=baseline.Region.SourceFrame;actualFrame??=actual.Region.SourceFrame;File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"range-consumption.jsonl"),System.Text.Json.JsonSerializer.Serialize(new{scope,label,expectedRange=new{low,high,enabled=true},baseline=ShapeSet(baseline.Measurements),expected=wanted,actual=found,baselineFrame,actualFrame})+Environment.NewLine);Require(wanted.SequenceEqual(found)&&baselineFrame==actualFrame,"独立目标范围的实际筛选集合/物理帧不符："+label+";expected="+string.Join(";",wanted)+";actual="+string.Join(";",found));}
        BlobOutput Debug(DefaultDebugWindow w,double low,double high){Require(Config(w).Fields.Read(enableId) is ConstantSetting{Value:true},"目标筛选未启用");foreach(var other in new[]{"IsAreaFilterEnabled","IsCircularityFilterEnabled","IsRectangularityFilterEnabled"}.Where(name=>name!=spec.Item3))Require(Config(w).Fields.Read(SettingId(other)) is ConstantSetting{Value:false},"其他正式筛选并未关闭");var baseline=Producer();var frame=baseline.Region.SourceFrame;BlobOutput? result=null;ImageFrameAddress? resultFrame=null;var c=Control(w);SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler observer=(_,args)=>{if(args.Results is DeclaredOutputSnapshot {Output:BlobOutput o,IsSuccess:true}){result=o;resultFrame=o.Region.SourceFrame;}};c.AddHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observer);try{var run=c.RunDebugAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();Require(result is not null,"无真实Blob成功结果");Pair(Config(w),low,high);AssertResult(low,high,result!,baseline,"Debug",frame,resultFrame);return result!;}finally{c.RemoveHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observer);}}
        void RejectRun(DefaultDebugWindow w,string expected){var c=Control(w);var formal=Context().GetNodeResult(node.Id);var success=false;string? diagnostic=null;SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler observer=(_,args)=>{success|=args.Results is DeclaredOutputSnapshot {IsSuccess:true};diagnostic=args.ErrorMessage;};c.AddHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observer);try{var run=c.RunDebugAsync();PumpUntil(()=>run.IsCompleted);try{run.GetAwaiter().GetResult();}catch(Exception error)when(error is ArgumentException or InvalidOperationException){diagnostic=error.Message;}File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"range-rejections.jsonl"),System.Text.Json.JsonSerializer.Serialize(new{scope,expected,diagnostic,success})+Environment.NewLine);Require(!success&&ReferenceEquals(formal,Context().GetNodeResult(node.Id))&&diagnostic is not null&&(expected=="order"?(diagnostic.Contains("筛选下限不能超过上限")||diagnostic.Contains("范围下限不能大于上限")):expected=="index"?diagnostic.Contains("变量索引超出"):diagnostic.Contains("exactly one",StringComparison.OrdinalIgnoreCase)||diagnostic.Contains("exactly-one",StringComparison.OrdinalIgnoreCase)),"未得到对应诊断或发布成功结果："+expected+" / "+diagnostic);}finally{c.RemoveHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observer);}}
        void Confirm(DefaultDebugWindow w){Require(w.TryConfirm(out var error),error);if(w.IsVisible)w.Close();Pump();}
        string Identity(ParameterSetting setting)=>setting switch{ReferenceSetting r=>BindingReferenceSerializer.Serialize(r.Reference),VariableSetting v=>BindingReferenceSerializer.Serialize(v.Reference),ConstantSetting c=>System.Text.Json.JsonSerializer.Serialize(c.Value),_=>"Unset"};
        void SaveReplay(double low,double high){Require(solutions.SaveCurrentDocument(path).FileWritten,"保存失败");var loaded=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("重开失败");var flow2=loaded.Workflows.Single(f=>f.Id==flow.Id);var c=flow2.Nodes.Single(n=>n.Id==node.Id).RuntimeConfiguration!;Pair(c,low,high);foreach(var id in new[]{threshold,upper})Require(Identity(c.Fields.Read(id))==Identity(node.RuntimeConfiguration!.Fields.Read(id)),"重开丢Selection");
            var engine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);engine.RegisterWorkflow(flow2);var h=new NodeConfigurationEditHost(loaded);var publication=WorkflowPublishedConfiguration.Capture(h.Capture(),loaded,h);using var globals=new WorkflowGlobalRunValues(loaded.Id,publication.GlobalDefinitions,publication.GlobalDefinitions.Select(v=>new KeyValuePair<string,object?>(v.Id,v.Value)));var executor=new WorkflowExecutionEngine(engine,new PluginManager(),SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);var task=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow2.Id,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{source.Id,new ImageSourceRunInput(imageFile,1){LocalReadAdmission=_=>imageFile}}}){Configuration=publication.Configuration,ExecutionOwner=publication.ExecutionOwner,ProducedGlobals=globals,ParameterValues=publication.ParameterValues,ResourcePaths=publication.ResourcePaths});PumpUntil(()=>task.IsCompleted);using(var run=task.GetAwaiter().GetResult()){Require(run.Success&&run.NodeResults[node.Id].Result is DeclaredOutputSnapshot{Output:BlobOutput},"重开对象执行失败");AssertResult(low,high,(BlobOutput)((DeclaredOutputSnapshot)run.NodeResults[node.Id].Result).Output!, (BlobOutput)((DeclaredOutputSnapshot)run.NodeResults[producer.Id].Result).Output!,"Reopen");}Pair(c,low,high);loaded.ExecutionOwner.Retire();}
        var lowValue=integer?12d:.2;var highValue=integer?60d:.9;
        Case(scope+"原between两端及非法原文/倒序原子拒绝恢复",()=>{
            var w=Open();Enable(w,true);var lo=Field(w,0);var hi=Field(w,1);Require(ReferenceEquals(lo.View,hi.View)&&lo.View.GetType().Name=="NativeRangePresenter","未复用原双端");Require(lo.TrySetConstant(Number(lowValue))&&hi.TrySetConstant(Number(highValue)),"合法值拒绝");var before=new[]{Identity(lo.Setting),Identity(hi.Setting)};var text=(TextBox)lo.InputElement!;text.SetCurrentValue(TextBox.TextProperty,"invalid-range");Require(!lo.TryCommitPending()&&text.Text=="invalid-range"&&new[]{Identity(lo.Setting),Identity(hi.Setting)}.SequenceEqual(before),"非法原文半写/丢失");text.SetCurrentValue(TextBox.TextProperty,lowValue.ToString(System.Globalization.CultureInfo.InvariantCulture));Require(lo.TryCommitPending(),"原文恢复拒绝");Require(!lo.TrySetConstant(Number(integer?highValue+1:.95))&&new[]{Identity(lo.Setting),Identity(hi.Setting)}.SequenceEqual(before),"倒序被接纳");Require(lo.TrySetConstant(Number(lowValue)),"失败不可恢复");if(integer)Require(!lo.TrySetConstant(12.5),"整数接纳非整数");else Require(lo.TrySetConstant(.235)&&((ConstantSetting)lo.Setting).Value is double precise&&precise==.24,"浮点原两位ToEven不符");Require(lo.TrySetConstant(Number(lowValue)),"恢复失败");Debug(w,lowValue,highValue);Capture(w,"range-"+scope+".png");w.Close();Pump();
        });
        Case(scope+"左右独立订阅与取消/旧回调退役",()=>{var before=node.RuntimeConfiguration;var bytes=File.ReadAllBytes(path);var position=history.Position;var w=Open();Enable(w,true);Require(Field(w,1).TrySetConstant(Number(integer?999999999:1)),"右常量拒绝");var lo=Field(w,0);Subscribe(lo);var old=Session(lo);SelectVariable(old,false,values.Id);SetIndex(old,"0");Require(Field(w,1).Setting is ConstantSetting,"左订阅污染右端");var hi=Field(w,1);Subscribe(hi);SelectVariable(Session(hi),false,values.Id);SetIndex(Session(hi),"1");Debug(w,lowValue,highValue);var stale=Tree((System.Collections.IEnumerable)Get(old,"SourceNodes")).First(item=>Candidate(item) is not null);UseConstant(hi);Require(hi.Setting is ConstantSetting&&lo.Setting is VariableSetting&&Identity(hi.Setting)==Identity(new ConstantSetting(Number(highValue))),"右原按钮未独立采用本轮宿主当前值");UseConstant(lo);Require(lo.Setting is ConstantSetting&&Identity(lo.Setting)==Identity(new ConstantSetting(Number(lowValue))),"左原按钮未采用本轮宿主当前值");var draft=Config(w);var identities=new[]{threshold,upper}.Select(id=>Identity(draft.Fields.Read(id))).ToArray();w.Close();Pump();var successor=Open();var successorConfig=Config(successor);var successorIds=new[]{threshold,upper}.Select(id=>Identity(successorConfig.Fields.Read(id))).ToArray();((ICommand)Get(old,"DropDownCommand")).Execute(null);((ICommand)Get(old,"ClickCommand")).Execute(stale);Require(new[]{threshold,upper}.Select(id=>Identity(successorConfig.Fields.Read(id))).SequenceEqual(successorIds),"旧来源Click污染新窗口");successor.Close();Pump();Require(ReferenceEquals(before,node.RuntimeConfiguration)&&history.Position==position&&bytes.SequenceEqual(File.ReadAllBytes(path))&&new[]{threshold,upper}.Select(id=>Identity(draft.Fields.Read(id))).SequenceEqual(identities),"取消/旧回调污染正式或退役草稿");});
        Case(scope+"双端确认历史复制保存重开执行",()=>{var opening=new[]{threshold,upper,enableId}.Select(id=>Identity(node.RuntimeConfiguration!.Fields.Read(id))).ToArray();var w=Open();Enable(w,true);foreach(var end in new[]{0,1}){var f=Field(w,end);Subscribe(f);SelectVariable(Session(f),false,values.Id);SetIndex(Session(f),end.ToString());}Debug(w,lowValue,highValue);Confirm(w);Pair(node.RuntimeConfiguration!,lowValue,highValue);var copied=node.RuntimeConfiguration!.Copy();Pair(copied,lowValue,highValue);foreach(var id in new[]{threshold,upper})Require(Identity(copied.Fields.Read(id))==Identity(node.RuntimeConfiguration.Fields.Read(id)),"配置复制丢完整双端身份");var fragment=WorkflowFragmentCopy.Prepare(flow,new[]{node.Id},flow.Id,_=>null,(original,_)=>new WorkflowNodeBase("range-copy-"+original.Id,original.Name,original.DispName,original.ToolType));foreach(var id in new[]{threshold,upper})Require(Identity(fragment.Nodes.Single().RuntimeConfiguration!.Fields.Read(id))==Identity(node.RuntimeConfiguration.Fields.Read(id)),"正式片段复制丢双端变量身份");var committed=new[]{threshold,upper,enableId}.Select(id=>Identity(node.RuntimeConfiguration!.Fields.Read(id))).ToArray();SaveReplay(lowValue,highValue);history.Undo();Require(new[]{threshold,upper,enableId}.Select(id=>Identity(node.RuntimeConfiguration!.Fields.Read(id))).SequenceEqual(opening),"Undo丢端点/使能完整状态");Require(node.RuntimeConfiguration!.Fields.Read(threshold) is ConstantSetting&&node.RuntimeConfiguration.Fields.Read(upper) is ConstantSetting,"Undo双端未原子恢复");history.Redo();Require(new[]{threshold,upper,enableId}.Select(id=>Identity(node.RuntimeConfiguration!.Fields.Read(id))).SequenceEqual(committed),"Redo丢端点/使能完整状态");Require(node.RuntimeConfiguration!.Fields.Read(threshold) is VariableSetting&&node.RuntimeConfiguration.Fields.Read(upper) is VariableSetting,"Redo双端未原子恢复");});
        foreach(var end in new[]{0,1})foreach(var mode in new[]{"Fixed","AtField","AtVariable"})Case(scope+"端"+end+mode+"完整索引消费",()=>{var w=Open();Enable(w,true);var f=Field(w,end);Require(Field(w,1-end).TrySetConstant(Number(end==0?(integer?999999999:1):(integer?1:0))),"对端常量拒绝");Subscribe(f);var session=Session(f);SelectVariable(session,false,values.Id);OutputReference? expectedIndex=null;double expected;if(mode=="Fixed"){SetIndex(session,"1");expected=highValue;}else if(mode=="AtField"){Choose(session,true,item=>{var r=Candidate(item)?.Reference(BindingSelection.All);if(r?.NodeId!=source.Id||r.PortId!="output.image"||r.ScopeId!=flow.Id||!r.MemberFieldPath.SequenceEqual(new[]{"width"}))return false;expectedIndex=r;return true;});expected=integer?1:.1;}else{SelectVariable(session,true,indexVariable.Id);expected=highValue;}var selected=((VariableSetting)f.Setting).Reference.Selection;Require(mode switch{"Fixed"=>selected is BindingSelection.AtConstant{Index:1},"AtField"=>selected is BindingSelection.AtField r&&expectedIndex is not null&&BindingReferenceSerializer.Serialize(r.Reference)==BindingReferenceSerializer.Serialize(expectedIndex),_=>selected is BindingSelection.AtVariable r&&BindingReferenceSerializer.Serialize(r.Reference)==BindingReferenceSerializer.Serialize(new VariableReference(solutions.CurrentSolution!.Id,indexVariable.Id,DataType.Integer,BindingSelection.All,NumericStorage.Int64))},"完整索引身份丢失");Debug(w,end==0?expected:(integer?1:0),end==1?expected:(integer?999999999:1));w.Close();Pump();});
        Case(scope+"首项与严格单值政策/越界拒绝恢复",()=>{var before=node.RuntimeConfiguration;var w=Open();Enable(w,true);var f=Field(w,0);Require(Field(w,1).TrySetConstant(Number(integer?999999999:1)),"高端拒绝");Subscribe(f);var session=Session(f);SelectVariable(session,false,values.Id);Choose(session,true,item=>(bool)Get(item,"Clear"));if(integer)Debug(w,12,999999999);else RejectRun(w,"count");SetIndex(session,"99");RejectRun(w,"index");Require(ReferenceEquals(before,node.RuntimeConfiguration),"失败运行污染正式");SetIndex(session,"0");Debug(w,lowValue,integer?999999999:1);w.Close();Pump();});
        Case(scope+"禁用/弹层/隐藏恢复保原文与引用",()=>{var w=Open();Enable(w,true);var lo=Field(w,0);var hi=Field(w,1);Require(lo.TrySetConstant(Number(lowValue))&&hi.TrySetConstant(Number(highValue)),"基线范围失败");var text=(TextBox)lo.InputElement!;text.SetCurrentValue(TextBox.TextProperty,"pending-range");lo.View.IsEnabled=false;Pump();Require(text.Text=="pending-range","禁用丢原文");lo.View.IsEnabled=true;Pump();Require(text.Text=="pending-range"&&!Control(w).TryCapture(out _),"恢复丢原文或误捕获");text.SetCurrentValue(TextBox.TextProperty,lowValue.ToString(System.Globalization.CultureInfo.InvariantCulture));Require(lo.TryCommitPending(),"恢复后不可修复");Enable(w,false);Require(!lo.View.IsVisible,"筛选关闭未隐藏");Enable(w,true);Debug(w,lowValue,highValue);Subscribe(lo);var session=Session(lo);_activePopup!.IsDropDownOpen=true;Pump();lo.View.IsEnabled=false;Pump();Require(!_activePopup.IsDropDownOpen,"禁用未关闭真实Popup");lo.View.IsEnabled=true;Pump();w.Close();Pump();});
        Case(scope+"左/右/双引用解析倒序整体拒绝并可恢复",()=>{foreach(var modes in new[]{"Left","Right","Both"}){var before=node.RuntimeConfiguration;var w=Open();Enable(w,true);var lo=Field(w,0);var hi=Field(w,1);Require(lo.TrySetConstant(Number(lowValue))&&hi.TrySetConstant(Number(highValue)),"正序基线拒绝");if(modes!="Right"){Require(hi.TrySetConstant(Number(lowValue)),"常量上端拒绝");Subscribe(lo);SelectVariable(Session(lo),false,values.Id);SetIndex(Session(lo),"1");}else Require(lo.TrySetConstant(Number(highValue)),"常量下端拒绝");if(modes!="Left"){Subscribe(hi);SelectVariable(Session(hi),false,values.Id);SetIndex(Session(hi),"0");}RejectRun(w,"order");Require(ReferenceEquals(before,node.RuntimeConfiguration),"倒序解析发生正式半写");if(modes!="Right")SetIndex(Session(lo),"0");else Require(lo.TrySetConstant(Number(lowValue)),"恢复下端拒绝");if(modes!="Left")SetIndex(Session(hi),"1");else Require(hi.TrySetConstant(Number(highValue)),"恢复上端拒绝");Debug(w,lowValue,highValue);w.Close();Pump();}});
        Case(scope+"运行捕获后变量变化只影响下一次快照",()=>{var before=node.RuntimeConfiguration;var w=Open();Enable(w,true);foreach(var end in new[]{0,1}){var f=Field(w,end);Subscribe(f);SelectVariable(Session(f),false,values.Id);SetIndex(Session(f),end.ToString());}var live=solutions.CurrentSolution!.GlobalVariables.Single(v=>v.Id==values.Id);var original=live.Value;var c=Control(w);var capture=(Func<WorkflowNodeBase,RuntimeDebugInvocation>)typeof(RuntimeNodeConfigurationControl).GetField("_captureDebug",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(c)!;c.SetDebugCapture(draft=>{var frozen=capture(draft);live.Value=integer?(object)new long[]{15,65,20,30,40,50,70,80,1}:new double[]{.3,.95,.3,.4,.5,.6,.7,.8,.1};return frozen;});try{Debug(w,lowValue,highValue);c.SetDebugCapture(capture);Debug(w,integer?15:.3,integer?65:.95);w.Close();Pump();Require(ReferenceEquals(before,node.RuntimeConfiguration),"快照或取消污染正式");}finally{c.SetDebugCapture(capture);live.Value=original;if(w.IsVisible)w.Close();Pump();}});
        Case(scope+"真实前序左右来源与断边保值退役及Undo恢复",()=>{var w=Open();Enable(w,true);var producerOutput=((DeclaredOutputSnapshot)Context().GetNodeResult(producer.Id)!.Result).Output as BlobOutput??throw new Exception("无真实前序Blob");Require(producerOutput.Count==1,"测试前序应有一个真实Blob");var lower=integer?1:producerOutput.Measurements[0].Circularity;var higher=integer?1:producerOutput.Measurements[0].Rectangularity;foreach(var end in new[]{0,1}){var f=Field(w,end);Subscribe(f);var session=Session(f);Choose(session,false,item=>Candidate(item)?.Reference(BindingSelection.All) is {} r&&r.NodeId==producer.Id&&r.PortId==BlobSchema.Field(integer?11:end==0?23:24));if(!integer)SetIndex(session,"0");}Debug(w,lower,higher);Confirm(w);var identities=new[]{threshold,upper}.Select(id=>Identity(node.RuntimeConfiguration!.Fields.Read(id))).ToArray();vm.CurrentCommandManager!.Execute(new SunEyeVision.UI.Features.WorkflowUI.Commands.BatchDeleteNodesAndConnectionsCommand(flow.Nodes,flow.Connections,new(),new(){producerEdge}));Pump();w=Open();Require(!Control(w).TryCapture(out _),"断边来源仍可捕获");Require(new[]{threshold,upper}.Select(id=>Identity(node.RuntimeConfiguration!.Fields.Read(id))).SequenceEqual(identities),"断边清除了原引用");w.Close();Pump();history.Undo();w=Open();Debug(w,lower,higher);w.Close();Pump();});
        void WriteImage(int size,bool two){var pixels=new byte[size*size];for(var row=0;row<size;row++)for(var col=0;col<size;col++){var white=two?(row>=4&&row<12&&col>=4&&col<16)||(row>=28&&row<44&&col>=35&&col<35+row-27):col<=row;pixels[row*size+col]=white?(byte)200:(byte)0;}var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(size,size,96,96,PixelFormats.Gray8,null,pixels,size)));using var output=File.Create(imageFile);encoder.Save(output);}
        void Rerun(){var run=vm.RunSelectedImageAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();Require(Context().GetNodeResult(producer.Id)?.Result is DeclaredOutputSnapshot{IsSuccess:true},"重跑未产生真实前序结果");}
        Case(scope+"真实多形敏感样本分别仅改变下限与上限",()=>{var original=File.ReadAllBytes(imageFile);DefaultDebugWindow? w=null;try{WriteImage(64,true);Rerun();var baseline=Producer();Require(baseline.Count==2,"敏感样本不是两个真实区域");double Feature(BlobMeasurement m)=>integer?m.Area:scope=="Circularity"?m.Circularity:m.Rectangularity;var ordered=baseline.Measurements.OrderBy(Feature).ToArray();var a=Feature(ordered[0]);var b=Feature(ordered[1]);var cut=integer?Math.Floor((a+b)/2):Math.Round((a+b)/2,2);Require(a<cut&&cut<b,"敏感指标未分离："+a+"/"+b);w=Open();Enable(w,true);var lo=Field(w,0);var hi=Field(w,1);var min=integer?1d:0d;var max=integer?999999999d:1d;Require(lo.TrySetConstant(Number(min))&&hi.TrySetConstant(Number(max)),"敏感基线失败");Require(Debug(w,min,max).Count==2,"宽范围非两对象");Require(lo.TrySetConstant(Number(cut)),"仅改下限失败");var lower=Debug(w,cut,max);Require(lower.Count==1&&lower.Measurements[0].Area==ordered[1].Area,"下限未单独选高指标对象");Confirm(w);SaveReplay(cut,max);w=Open();Enable(w,true);lo=Field(w,0);hi=Field(w,1);Require(lo.TrySetConstant(Number(min))&&hi.TrySetConstant(Number(cut)),"仅改上限失败");var upperResult=Debug(w,min,cut);Require(upperResult.Count==1&&upperResult.Measurements[0].Area==ordered[0].Area&&ordered[0].Area!=ordered[1].Area,"上限未单独选另一对象");Confirm(w);SaveReplay(min,cut);w=Open();Enable(w,true);lo=Field(w,0);hi=Field(w,1);Require(hi.TrySetConstant(Number(max)),"恢复范围失败");Debug(w,min,max);Confirm(w);SaveReplay(min,max);}finally{if(w?.IsVisible==true)w.Close();File.WriteAllBytes(imageFile,original);Rerun();Pump();}});
        Case(scope+"捕获后真实输出publication更替仅影响下一次消费",()=>{var original=File.ReadAllBytes(imageFile);var w=Open();Enable(w,true);Require(Field(w,1).TrySetConstant(Number(integer?999999999:1)),"快照上限失败");var lo=Field(w,0);Subscribe(lo);var session=Session(lo);Choose(session,false,item=>Candidate(item)?.Reference(BindingSelection.All) is {} r&&r.NodeId==(integer?source.Id:producer.Id)&&(integer?r.PortId=="output.image"&&r.MemberFieldPath.SequenceEqual(new[]{"width"}):r.PortId==BlobSchema.Field(23)));if(!integer)SetIndex(session,"0");var old=integer?8d:Producer().Measurements.Single().Circularity;var oldFrame=Producer().Region.SourceFrame;var c=Control(w);var capture=(Func<WorkflowNodeBase,RuntimeDebugInvocation>)typeof(RuntimeNodeConfigurationControl).GetField("_captureDebug",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(c)!;c.SetDebugCapture(draft=>{var frozen=capture(draft);try{WriteImage(16,false);Rerun();Require(Producer().Region.SourceFrame!=oldFrame,"上游未生成新物理帧");return frozen;}catch{frozen.Dispose();throw;}});try{Debug(w,old,integer?999999999:1);c.SetDebugCapture(capture);var next=integer?16d:Producer().Measurements.Single().Circularity;Require(next!=old,"输出样本值没有变化");Debug(w,next,integer?999999999:1);}finally{c.SetDebugCapture(capture);if(w.IsVisible)w.Close();File.WriteAllBytes(imageFile,original);Rerun();Pump();}});
        shell.Close();dialogs.Stop();Pump();Console.WriteLine("Blob范围"+scope+"整链："+_passed+"组通过；不代原厂全域/自然焦点/剪贴板/连续会话/ROI图文。");return 0;
    }
    private static object? _lastChoice;
    private static object Get(object target,string name)=>target.GetType().GetProperty(name)!.GetValue(target)!;
    private static DeclaredReferenceCandidate? Candidate(object item)=>(DeclaredReferenceCandidate?)Get(item,"Candidate");
    private static AppsPopup? _activePopup;
    private static bool Right(DeclaredFieldAccess f)=>f.FieldId is "field:cdce3e942b555fa58489e08eeb39236f" or "field:1a8be75176b350f09f3cd64506c136da" or "field:c38989cfdf9e51799d27e0eeee34e726";
    private static object Session(DeclaredFieldAccess f){var native=Get(f.View,"NativeView");var session=Get(native,Right(f)?"SubscribeDataRight":"SubscribeDataLeft");_activePopup=Visual(f.View).OfType<AppsPopup>().Single(p=>p.Name=="rt"&&ReferenceEquals(p.DataContext,session));_activePopup.ApplyTemplate();return session;}
    private static void UseConstant(DeclaredFieldAccess field){var toggle=Visual(field.View).OfType<ToggleButton>().Single(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path==(Right(field)?"IsSubscribeRight":"IsSubscribeLeft"));Require(toggle.IsChecked==true,"原订阅按钮状态不真");toggle.SetCurrentValue(ToggleButton.IsCheckedProperty,false);toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();}
    private static void Subscribe(DeclaredFieldAccess field)
    {if(field.Setting is ReferenceSetting or VariableSetting)return;var toggle=Visual(field.View).OfType<ToggleButton>().Single(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path==(Right(field)?"IsSubscribeRight":"IsSubscribeLeft"));toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();}
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
        var set=Environment.GetEnvironmentVariable("S4_BLOB_RANGE_CASE_SET")??"Core";var selected=name.Contains("敏感样本")?"Sensitivity":name.Contains("输出publication")?"Publication":"Core";if(set!="All"&&set!=selected)return;
        var file=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"reference-chain-progress.log");
        File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" BEGIN "+name+Environment.NewLine);
        try{action();_passed++;Console.WriteLine("通过："+name);File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" PASS "+name+Environment.NewLine);}
        catch(Exception failure){File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" FAIL "+name+Environment.NewLine+failure+Environment.NewLine);throw;}
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
