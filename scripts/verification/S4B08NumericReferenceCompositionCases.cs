using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
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

internal static partial class S2DocumentEditingCases
{
    private static int _passed;
    private sealed class ThrowAfterDispose(IDisposable inner):IDisposable
    {
        public void Dispose(){inner.Dispose();throw new InvalidOperationException("测试：命令租约清理故障");}
    }
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
        var resultView=new ImageResultControl{DataContext=vm.ImageResult};resultView.Initialize(vm);
        var shell=new Window{Width=1000,Height=850,Content=resultView,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();
        var node=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(node);
        node.DebugWindowStyle=DebugWindowStyle.Custom;
        var threshold=BindingId("LowThreshold");
        const string modeId="field:8ca20000000040008000000000000001";
        NativeRuntimeEditorWindow Open(){Step("Open before");var opened=OpenFor(node);Step("Open after");return opened;}
        NativeRuntimeEditorWindow OpenFor(WorkflowNodeBase target)
        {
            vm.OpenDebugWindowCommand.Execute(target);Pump();
            var window=app.Windows.OfType<NativeRuntimeEditorWindow>().SingleOrDefault(w=>w.IsVisible)??throw new Exception("实际打开命令没有创建Runtime窗口");
            window.UpdateLayout();Pump();return window;
        }
        RuntimeNodeConfigurationControl Control(NativeRuntimeEditorWindow window)=>window.EditorControl;
        TextBox Threshold(NativeRuntimeEditorWindow window)
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
            png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(8,9,96,96,PixelFormats.Gray8,null,Enumerable.Repeat((byte)200,72).ToArray(),8)));
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
        var imageChoice=imageCombo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ReferenceSetting r&&r.Reference.NodeId==source.Id&&r.Reference.PortId=="output.image"&&r.Reference.MemberFieldPath.SequenceEqual(new[]{"data"}));Require(Equals(imageChoice.Content,source.Name+".图像"),"图像来源未显示声明组合父名称");imageCombo.SelectedItem=imageChoice;Pump();Require(setup.TryConfirm(out var error),error);if(setup.IsVisible)setup.Close();Pump();Require(solutions.SaveCurrentDocument(path).FileWritten,"基准方案保存失败");
        var initialRun=vm.RunSelectedImageAsync();PumpUntil(()=>initialRun.IsCompleted);initialRun.GetAwaiter().GetResult();Require(Context().GetNodeResult(source.Id)?.Result is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {IsSuccess:true},"正式上游未产生本次源快照");
        DeclaredFieldAccess Low(NativeRuntimeEditorWindow window){Threshold(window);return Logical(Control(window)).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==threshold);}
        ProductionEditorContext Draft(NativeRuntimeEditorWindow window)=>(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(window))!;
        void Debug(NativeRuntimeEditorWindow window,long expected)
        {
            long? actual=null;var current=Control(window);
            SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler observe=(_,args)=>{if(args.Results is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {Output:BlobOutput output})actual=output.ThresholdMinUsed;};
            current.AddHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observe);
            try{var run=current.RunDebugAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();Require(actual==expected,"真实算法未消费预期阈值："+actual+"/"+expected);Require(Draft(window).DraftNode.RuntimeConfiguration!.ReadCurrentParameter(threshold) is {Value:long value}&&value==expected,"私有当前值与算法不同");}
            finally{current.RemoveHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observe);}
        }
        void Confirm(NativeRuntimeEditorWindow window){Step("Confirm before");Require(window.TryConfirm(out var issue),"真实确认失败："+issue);Step("Confirm after");if(window.IsVisible){Step("Close before");window.Close();Step("Close after");}Step("Confirm Pump before");Pump();Step("Confirm Pump after");}
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
            var replay=ownExecutor.ExecuteWorkflowAsync(new WorkflowRunRequest(loadedFlow.Id,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{source.Id,new ImageSourceRunInput(imageFile,1){LocalReadAdmission=_=>imageFile}}}){Configuration=publication.Configuration,ExecutionOwner=publication.ExecutionOwner,ProducedGlobals=globals,ParameterValues=publication.ParameterValues,ResourcePaths=publication.ResourcePaths});
            PumpUntil(()=>replay.IsCompleted);using(var run=replay.GetAwaiter().GetResult()){Require(run.Success&&run.NodeResults[node.Id].Result is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {Output:BlobOutput output}&&output.ThresholdMinUsed==expected,"重开配置未按完整关系实际执行");}loaded.ExecutionOwner.Retire();
        }
        var variableSetup=host.BeginVariableEdit();var areaValues=new GlobalVariable("混合面积上下端",new long[]{20,30,20,20,20,20,20,20,20},"Integer[]");var floatValues=new GlobalVariable("混合浮点上下端",new double[]{.1,1},"Double[]");variableSetup.Configuration.GlobalVariables.Add(areaValues);variableSetup.Configuration.GlobalVariables.Add(floatValues);host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variableSetup);
        string Stamp(RuntimeToolConfiguration configuration)=>System.Text.Json.JsonSerializer.Serialize(configuration);
        void FormalEdit(Action<NodeConfigurationEditDraft> write){var edit=host.BeginEdit(flow.Id,node.Id);write(edit);host.Commit(edit);Pump();}
        DeclaredFieldAccess Field(NativeRuntimeEditorWindow window,string id){Threshold(window);foreach(var advanced in Logical(Control(window)).OfType<NativeAdvancedPresenter>().ToArray()){advanced.NativeView.SetCurrentValue(ToggleButton.IsCheckedProperty,true);Require(advanced.IsExpanded,"原高级Checked链未展开");}window.UpdateLayout();Pump();return Logical(Control(window)).OfType<DeclaredFieldsView>().Single().Fields.Single(field=>field.FieldId==id);}
        void Constant(NativeRuntimeEditorWindow window,string id,object value){var field=Field(window,id);Require(field.TrySetConstant(value),id+":"+field.Error);}
        void UseConstant(DeclaredFieldAccess field)
        {
            var toggles=Visual(field.View).OfType<ToggleButton>().Where(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path is "IsSubscribeLeft" or "IsSubscribeRight").ToArray();
            var path=field.FieldId==SettingId(nameof(BlobSettings.FilterAreaMax))||field.FieldId==SettingId(nameof(BlobSettings.FilterCircularityMax))||field.FieldId==SettingId(nameof(BlobSettings.FilterRectangularityMax))?"IsSubscribeRight":"IsSubscribeLeft";
            var toggle=toggles.Single(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path==path);toggle.SetCurrentValue(ToggleButton.IsCheckedProperty,false);toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();
        }
        void Variable(NativeRuntimeEditorWindow window,string id,string variableId,string index)
        {var field=Field(window,id);Subscribe(field);var session=Session(field);SelectVariable(session,false,variableId);SetIndex(session,index);Require(field.Setting is VariableSetting selected&&selected.Reference.VariableId==variableId,"原变量选择未实际接纳："+id);}
        RecordValue Rect(double x,double y,double width,double height)=>new(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","rectangle"},{"center",BlobLocalRoi.Point(x,y)},{"width",width},{"height",height},{"angle-degrees",0d}});
        var local=BlobLocalRoi.Root(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{BlobLocalRoi.Item(Guid.NewGuid(),"混合检测",Rect(3,3,4,4))}),BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{BlobLocalRoi.Item(Guid.NewGuid(),"混合屏蔽",Rect(0,7,.5,.5))}));
        void SetLocal(NativeRuntimeEditorWindow window)
        {var inputs=Logical(Control(window)).OfType<DeclaredInputFieldsView>().Single();inputs.WriteUnpresentedInput(BlobLocalRoi.InputId,new ConstantSetting(local));Require(BlobLocalRoi.Same(Draft(window).DraftNode.RuntimeConfiguration!.Fields.Get<RecordValue>(BlobLocalRoi.InputId),local),"正式输入提案未进入本地ROI草稿");}
        (long Threshold,int Count,double Area) Observe(NativeRuntimeEditorWindow window)
        {
            (long Threshold,int Count,double Area)? observed=null;var control=Control(window);SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler listener=(_,args)=>{if(args.Results is DeclaredOutputSnapshot {Output:BlobOutput output})observed=(output.ThresholdMinUsed??-1,output.Count,output.TotalArea);};control.AddHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,listener);
            try{var task=control.RunDebugAsync();PumpUntil(()=>task.IsCompleted);task.GetAwaiter().GetResult();Require(observed is not null,"实际Blob调用未发布成功结果");return observed!.Value;}finally{control.RemoveHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,listener);}
        }
        void Expect(NativeRuntimeEditorWindow window,long expected,double area)
        {var actual=Observe(window);Require(actual.Threshold==expected&&actual.Count==1&&actual.Area==area,"实际Blob消费/筛选/区域不符："+actual+" expected="+expected+"/"+area);}
        void Mix(NativeRuntimeEditorWindow window)
        {

            Step("Mix 1");
            Constant(window,SettingId(nameof(BlobSettings.IsAreaFilterEnabled)),true);Constant(window,SettingId(nameof(BlobSettings.IsCircularityFilterEnabled)),true);Constant(window,SettingId(nameof(BlobSettings.IsRectangularityFilterEnabled)),true);
            Step("Mix 2");
            Variable(window,threshold,values.Id,"0");Variable(window,SettingId(nameof(BlobSettings.FilterAreaMin)),areaValues.Id,"0");Variable(window,SettingId(nameof(BlobSettings.FilterAreaMax)),areaValues.Id,"1");
            Step("Mix 3");
            var lowerSession=Session(Field(window,SettingId(nameof(BlobSettings.FilterAreaMin))));Choose(lowerSession,true,item=>Candidate(item)?.Reference(BindingSelection.All) is {} reference&&reference.ScopeId==flow.Id&&reference.NodeId==source.Id&&reference.PortId=="output.image"&&reference.Storage is null&&reference.Type==source.RuntimeConfiguration!.Schema.Outputs.Single(port=>port.Id=="output.image").Type&&reference.Selection is BindingSelection.AllItems&&reference.MemberFieldPath.SequenceEqual(new[]{"width"}));
            Step("Mix 4");
            var upperSession=Session(Field(window,SettingId(nameof(BlobSettings.FilterAreaMax))));SelectVariable(upperSession,true,indexVariable.Id);
            Step("Mix 5");
            Require(Field(window,SettingId(nameof(BlobSettings.FilterAreaMin))).Setting is VariableSetting {Reference.Selection:BindingSelection.AtField fieldIndex}&&fieldIndex.Reference.PortId=="output.image"&&fieldIndex.Reference.MemberFieldPath.SequenceEqual(new[]{"width"})&&Field(window,SettingId(nameof(BlobSettings.FilterAreaMax))).Setting is VariableSetting {Reference.Selection:BindingSelection.AtVariable variableIndex}&&variableIndex.Reference.VariableId==indexVariable.Id,"真实双端动态索引身份未接纳");
            Step("Mix 6");
            Constant(window,SettingId(nameof(BlobSettings.FilterCircularityMin)),.1);Variable(window,SettingId(nameof(BlobSettings.FilterCircularityMax)),floatValues.Id,"1");Constant(window,SettingId(nameof(BlobSettings.FilterRectangularityMin)),.1);Constant(window,SettingId(nameof(BlobSettings.FilterRectangularityMax)),1d);SetLocal(window);
            Step("Mix 7");
            var editor=(DeclaredSettingsView)Get(Control(window).Content,"Editor");editor.SelectedPageIndex=2;window.UpdateLayout();Pump();var fields=(DeclaredFieldsView)editor.Content;
            Step("Mix 8");
            foreach(var pair in new[]{(Id:BlobResultDisplay.Field(BlobResultDisplay.Inspection,"width"),Value:(object)3d),(Id:BlobResultDisplay.Field(BlobResultDisplay.Text,"content"),Value:(object)"Area={0}")}){var field=fields.Fields.Single(item=>item.FieldId==pair.Id);Require(field.TrySetConstant(pair.Value),field.Error);}
            Step("Mix 9");
            Require(Control(window).TryCapture(out var error),error);
        }
        void Replay(long expected,double area)
        {
            var expectedConfiguration=Stamp(node.RuntimeConfiguration!);Require(solutions.SaveCurrentDocument(path).FileWritten,"混合正式方案保存失败");var loaded=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("混合文件重开失败");var reopened=loaded.Workflows.Single(item=>item.Id==flow.Id);var target=reopened.Nodes.Single(item=>item.Id==node.Id);
            Require(Stamp(target.RuntimeConfiguration!)==expectedConfiguration&&Stamp(target.RuntimeConfiguration!.Copy())==expectedConfiguration,"重开/配置Copy丢完整关系/当前值/ROI/样式");
            var engine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);engine.RegisterWorkflow(reopened);var reopenedHost=new NodeConfigurationEditHost(loaded);var publication=WorkflowPublishedConfiguration.Capture(reopenedHost.Capture(),loaded,reopenedHost);using var globals=new WorkflowGlobalRunValues(loaded.Id,publication.GlobalDefinitions,publication.GlobalDefinitions.Select(variable=>new KeyValuePair<string,object?>(variable.Id,variable.Value)));var executor=new WorkflowExecutionEngine(engine,new PluginManager(),SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);
            try{var task=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(reopened.Id,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{source.Id,new ImageSourceRunInput(imageFile,1){LocalReadAdmission=_=>imageFile}}}){Configuration=publication.Configuration,ExecutionOwner=publication.ExecutionOwner,ProducedGlobals=globals,ParameterValues=publication.ParameterValues,ResourcePaths=publication.ResourcePaths});PumpUntil(()=>task.IsCompleted);using var run=task.GetAwaiter().GetResult();Require(run.Success&&run.NodeResults[node.Id].Result is DeclaredOutputSnapshot {Output:BlobOutput output}&&output.ThresholdMinUsed==expected&&output.Count==1&&output.TotalArea==area,"真实重开引擎未消费完整混合设置");}finally{loaded.ExecutionOwner.Retire();}
        }
        void VerifyPublishedMixed(string expectedText="Area=25",double? expectedX=null,double roiWidth=4)
        {
            vm.SelectedNode=node;node.IsSelected=true;var execution=vm.RunSelectedImageAsync();PumpUntil(()=>execution.IsCompleted);execution.GetAwaiter().GetResult();Pump();
            var workspace=(ResultWorkspaceShell)resultView.FindName("Workspace");workspace.IsDoubleImage=false;Pump();workspace.SelectedOutputItem=workspace.OutputItems.Cast<object>().Single(item=>Get(item,"Reference") is OutputReference reference&&reference.NodeId==node.Id&&reference.PortId==BlobSchema.Field(5));Pump();
            var presenter=Get(resultView,"_workspacePresenter");var pane=Get(presenter,"_output");object? scene=null;PumpUntil(()=>{try{scene=Get(pane,"Display");return scene is not null;}catch(NullReferenceException){return false;}});
            var prepared=Get(Get(scene!,"Layer"),"_prepared");var items=(object[])Get(prepared,"Items");var rectangles=items.OfType<SunEyeVision.Plugin.Wpf.Native.Display.Contracts.IRectangle>().ToArray();
            var geometry=((IToolResultGeometryOutput)Get(Get(vm,"_resultPublicationSnapshot"),"Output")).ResultGeometry;
            var receipt=((SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultViewport)Get(Get(presenter,"OutputPane"),"Viewport")).CurrentFrame!;
            Require(geometry.SourceFrame==receipt.Frame&&geometry.Coordinates==receipt.Coordinates,"混合显示不是同一物理帧/坐标域");
            var appearance=node.RuntimeConfiguration!.CaptureResultDisplay()!;var matched=new HashSet<object>(ReferenceEqualityComparer.Instance);
            string ColorOf(ResultDisplayColor color)=>Color.FromArgb(color.Alpha,color.Red,color.Green,color.Blue).ToString();
            void Match(Func<SunEyeVision.Plugin.Wpf.Native.Display.Contracts.IRectangle,bool> predicate,ResultGraphicAppearance style,bool passed,string fill)
            {
                var choices=rectangles.Where(rect=>predicate(rect)&&rect.Stroke==ColorOf(passed?style.Ok:style.Ng)&&rect.Fill==fill&&rect.StrokeThickness==style.Width&&Math.Abs(rect.Opacity-style.Opacity)<1e-9).ToArray();
                Require(choices.Length==1&&matched.Add(choices[0]),"混合场景缺失、重复或串用某个结果/区域矩形及样式");
            }
            using(var displayed=((SunEyeVision.Plugin.Wpf.Presenters.Results.IResultPublication)Get(vm,"CurrentResultPublication")).Borrow())
            {
                var rows=displayed.Values!.Read(BlobSchema.BlobsPort);
                for(var rowIndex=0;rowIndex<rows.Count;rowIndex++)
                {
                    var row=rows.GetElement(rowIndex);var corners=row.GetField("rotated-bounds");
                    Match(rect=>{
                        var angle=rect.Angle*Math.PI/180;var cs=Math.Cos(angle);var sn=Math.Sin(angle);
                        return corners.Count==4&&Enumerable.Range(0,4).All(index=>{var point=((PointGeometry)corners.GetElement(index).GetGeometry()).Point;var dx=point.X-rect.CenterPoint.X;var dy=point.Y-rect.CenterPoint.Y;return Math.Abs(Math.Abs(dx*cs+dy*sn)-rect.Width/2)<1e-6&&Math.Abs(Math.Abs(-dx*sn+dy*cs)-rect.Height/2)<1e-6;});
                    },appearance.Graphic,row.GetField("judgment-passed").GetScalar<bool>(),Colors.Transparent.ToString());
                }
                var regionRectangles=0;
                foreach(var group in geometry.Groups)foreach(var shape in group.Shapes.Where(shape=>shape.Get<string>("kind")=="rectangle"))
                {
                    regionRectangles++;var center=shape.Get<PointGeometry>("center").Point;
                    var style=group.Role==ResultGeometryRole.Inspection?appearance.Inspection!:group.Role==ResultGeometryRole.Mask?appearance.Mask!:throw new Exception("夹具出现未声明的矩形角色");
                    if(group.Role==ResultGeometryRole.Inspection)Require(shape.Get<double>("width")==roiWidth&&style.Width==3,"检测区域或正式样式不符敏感夹具");
                    Match(rect=>Math.Abs(rect.CenterPoint.X-center.X)<1e-8&&Math.Abs(rect.CenterPoint.Y-center.Y)<1e-8&&rect.Width==shape.Get<double>("width")&&rect.Height==shape.Get<double>("height")&&Math.Abs(Math.IEEERemainder(rect.Angle-shape.Get<double>("angle-degrees"),360))<1e-8,
                        style,geometry.Passed,group.Role==ResultGeometryRole.Mask?"#FFFF5400":Colors.Transparent.ToString());
                }
                Require(regionRectangles==2&&rectangles.Length==rows.Count+regionRectangles&&matched.Count==rectangles.Length,"结果/检测/屏蔽矩形未逐项完整对应");
            }
            var words=items.Where(item=>item.GetType().Name.Contains("MultiText",StringComparison.Ordinal)).SelectMany(item=>((System.Collections.IEnumerable)Get(item,"Texts")).Cast<object>()).ToArray();Require(words.Length==1&&Equals(Get(words[0],"Content"),expectedText),"混合正式文本未消费同次结果");if(expectedX is {} x)Require(((Point)Get(words[0],"Position")).X==x,"混合文本位置未消费同次来源");Capture((FrameworkElement)resultView,"mixed-publication-scene.png");
        }
        if(Environment.GetEnvironmentVariable("S4_C05_NUMBER_MODELS_ONLY")=="1")
        {
            VerifyC05NumberModels(Open,Low,()=>node.RuntimeConfiguration!,FormalEdit,Variable,UseConstant,Debug,Confirm,
                ()=>history.Position,()=>history.Undo(),()=>history.Redo(),SaveCheck,threshold,values.Id,path);
            foreach(var window in app.Windows.OfType<NativeRuntimeEditorWindow>().ToArray())window.Close();shell.Close();app.Shutdown();dialogs.Stop();Pump();Console.WriteLine("C05真实单值模型生产链："+_passed+"组通过；CurrentValues仅普通参数适配，非VM native等价。");return 0;
        }
        Case("真实Blob窗使用唯一公共原底栏并执行返回结果",()=>{
            var window=Open();var control=Control(window);var frames=Visual(window).OfType<SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.NativeToolEditorFrame>().ToArray();Require(frames.Length==1,"Blob窗未使用唯一公共底栏");var frame=frames[0];
            var tabs=Logical(control).OfType<TabControl>().Single();tabs.SelectedIndex=0;window.UpdateLayout();Pump();Require(frame.ResetVisibility==Visibility.Collapsed,"非运行参数页出现重置");tabs.SelectedIndex=1;window.UpdateLayout();Pump();Require(frame.ResetVisibility==Visibility.Visible&&!frame.ResetAvailable,"运行页未呈现受保护的原重置入口");var reset=(Button)Get(frame,"ResetButton");Require(ToolTipService.GetShowOnDisabled(reset)&&reset.ToolTip?.ToString()?.Contains("尚未接入")==true,"禁用重置的未接入提示不可见");
            window.Content=null;Pump();window.Content=frame;window.UpdateLayout();Pump();var footer=(ItemsControl)Get(frame,"BottomCommands");Require(footer.Items.Count==4,"底栏重挂载后未恢复原命令");
            Button ButtonNamed(string label)=>Visual(footer).OfType<Button>().Single(button=>Equals(button.Content,label));
            var continuous=ButtonNamed("连续执行");var execute=ButtonNamed("执行");var confirm=ButtonNamed("确定");Require(!continuous.IsEnabled&&Equals(continuous.ToolTip,"连续执行尚未接入")&&execute.IsEnabled&&confirm.IsEnabled,"原底栏可用状态或保护提示错误");
            void Click(Button button)=>typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(button,null);
            var completed=0;SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler observed=(_,args)=>{if(args.Results is DeclaredOutputSnapshot {IsSuccess:true})completed++;};window.ToolExecutionCompleted+=observed;
            EventHandler faultyObserver=(_,_)=>throw new InvalidOperationException("测试：状态观察者故障");window.Changed+=faultyObserver;
            try{Click(execute);Require(!execute.IsEnabled&&!confirm.IsEnabled,"执行中命令未受保护");PumpUntil(()=>completed==1&&execute.IsEnabled);Require(completed==1&&Draft(window).DebugResults.GetNodeResult(node.Id)?.Result is DeclaredOutputSnapshot {IsSuccess:true},"原底栏实际按钮未将结果传播到正式宿主调试缓存");}finally{window.Changed-=faultyObserver;window.ToolExecutionCompleted-=observed;}
            System.ComponentModel.CancelEventHandler cancelClose=(_,args)=>args.Cancel=true;window.Closing+=cancelClose;
            try{Require(!window.TryConfirm(out var closeError)&&closeError.Contains("参数已提交")&&window.IsVisible,"确认提交后取消关闭未准确报告");}finally{window.Closing-=cancelClose;}
            Click(confirm);Pump();Require(!window.IsVisible,"原底栏实际确认按钮未关闭窗口");
        });
        Case("原底栏执行中关闭退役草稿并拒绝迟到结果",()=>{
            var window=Open();var control=Control(window);var context=Draft(window);var frame=Visual(window).OfType<SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.NativeToolEditorFrame>().Single();var footer=(ItemsControl)Get(frame,"BottomCommands");
            var execute=Visual(footer).OfType<Button>().Single(button=>Equals(button.Content,"执行"));var delivered=0;SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler observed=(_,_)=>delivered++;window.ToolExecutionCompleted+=observed;
            typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(execute,null);Require((bool)Get(control,"_running"),"执行未进入运行态");
            var leaseField=typeof(NativeRuntimeEditorWindow).GetField("_commandLease",BindingFlags.Instance|BindingFlags.NonPublic)!;leaseField.SetValue(window,new ThrowAfterDispose((IDisposable)leaseField.GetValue(window)!));window.Close();Pump();
            PumpUntil(()=>!(bool)Get(control,"_running")&&context.DebugOwner.ActiveRuns==0);Require(!context.IsValid&&delivered==0,"关闭后旧草稿或迟到结果仍可发布");
        });
        Case("整窗未消费解绑读取种入137而非来源173或默认100，取消不发布",()=>{
            FormalEdit(edit=>edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{threshold,137L}}));var before=Stamp(node.RuntimeConfiguration!);var bytes=File.ReadAllBytes(path);var position=history.Position;var window=Open();Variable(window,threshold,values.Id,"0");Require(Draft(window).DraftNode.RuntimeConfiguration!.ReadCurrentParameter(threshold) is {Value:137L},"未消费时种入值不是137");UseConstant(Low(window));Require(Low(window).Setting is ConstantSetting {Value:137L},"未消费解绑读了来源/默认/错误backup");Debug(window,137);window.Close();Pump();Require(Stamp(node.RuntimeConfiguration!)==before&&bytes.SequenceEqual(File.ReadAllBytes(path))&&history.Position==position,"取消私有解绑发布了正式配置/历史/文件");
        });
        Case("消费173后改索引99失败仍区分已接纳值，解绑确认历史重开不回退137",()=>{
            var window=Open();Variable(window,threshold,values.Id,"0");Debug(window,173);var session=Session(Low(window));SetIndex(session,"99");var before=Stamp(node.RuntimeConfiguration!);var completed=0;SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler observe=(_,args)=>{if(args.Results is DeclaredOutputSnapshot {IsSuccess:true})completed++;};Control(window).AddHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observe);var rejected=false;
            try{var task=Control(window).RunDebugAsync();PumpUntil(()=>task.IsCompleted);task.GetAwaiter().GetResult();}catch(Exception error){Console.WriteLine("索引失败诊断："+error);rejected=error.ToString().Contains("变量索引超出本次序列范围",StringComparison.Ordinal);}finally{Control(window).RemoveHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observe);}
            Require(rejected&&completed==0&&Stamp(node.RuntimeConfiguration!)==before&&Draft(window).DraftNode.RuntimeConfiguration!.ReadCurrentParameter(threshold) is {Value:173L},"索引读取失败误写当前值/发布结果/正式");UseConstant(Low(window));Require(Low(window).Setting is ConstantSetting {Value:173L},"消费后解绑未取173");Confirm(window);Require(node.RuntimeConfiguration!.Fields.Read(threshold) is ConstantSetting {Value:173L},"真实确认未存解绑常量");history.Undo();Pump();Require(node.RuntimeConfiguration!.Fields.Read(threshold) is ConstantSetting {Value:137L},"Undo重放执行或丢旧137");history.Redo();Pump();window=Open();Debug(window,173);window.Close();Pump();
        });
        Case("同一真实窗口阈值范围ROI图文混合私有消费与取消完整隔离",()=>{
            var before=Stamp(node.RuntimeConfiguration!);var bytes=File.ReadAllBytes(path);var position=history.Position;var window=Open();Mix(window);Expect(window,173,25);var draft=Draft(window).DraftNode.RuntimeConfiguration!;Require(draft.ReadCurrentParameter(threshold) is {Value:173L}&&draft.ReadCurrentParameter(SettingId(nameof(BlobSettings.FilterAreaMin))) is {Value:20L}&&draft.ReadCurrentParameter(SettingId(nameof(BlobSettings.FilterAreaMax))) is {Value:30L},"同批参数当前值未实际接纳");window.Close();Pump();Require(Stamp(node.RuntimeConfiguration!)==before&&bytes.SequenceEqual(File.ReadAllBytes(path))&&history.Position==position,"混合私有运行/取消污染正式");
        });
        Case("混合真实确认UndoRedo完整Copy及保存重开实际执行",()=>{
                        void Trace(string phase,string json){var root=System.Text.Json.Nodes.JsonNode.Parse(json)!;File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-history.jsonl"),System.Text.Json.JsonSerializer.Serialize(new{Phase=phase,HistoryPosition=history.Position,Fields=root["Fields"],CurrentValues=root["CurrentValues"],Order=root["ParameterBindingOrder"]})+Environment.NewLine);}
            var before=Stamp(node.RuntimeConfiguration!);Trace("before",before);var window=Open();Mix(window);Expect(window,173,25);Confirm(window);var accepted=Stamp(node.RuntimeConfiguration!);Trace("accepted",accepted);Require(accepted!=before,"混合确认未写正式");Replay(173,25);VerifyPublishedMixed();Trace("after-run",Stamp(node.RuntimeConfiguration!));history.Undo();Pump();Trace("undo",Stamp(node.RuntimeConfiguration!));Require(Stamp(node.RuntimeConfiguration!)==before,"混合Undo丢完整配置");history.Redo();Pump();Trace("redo",Stamp(node.RuntimeConfiguration!));
            var acceptedRoot=System.Text.Json.Nodes.JsonNode.Parse(accepted)!;var redoRoot=System.Text.Json.Nodes.JsonNode.Parse(Stamp(node.RuntimeConfiguration!))!;
            Require(acceptedRoot["Fields"]!.ToJsonString()==redoRoot["Fields"]!.ToJsonString()&&acceptedRoot["ParameterBindingOrder"]!.ToJsonString()==redoRoot["ParameterBindingOrder"]!.ToJsonString(),"混合Redo丢完整字段/引用Selection/绑定顺序");
            var opening=System.Text.Json.JsonSerializer.Deserialize<RuntimeToolConfiguration>(before)!;var consumed=System.Text.Json.JsonSerializer.Deserialize<RuntimeToolConfiguration>(accepted)!;var sensitive=0;
            foreach(var port in consumed.SettingsSchema.Inputs.Where(port=>consumed.Fields.Read(port.Id) is ReferenceSetting or VariableSetting))
            {
                var live=opening.ReadCurrentParameter(port.Id)?.Value;var replayed=node.RuntimeConfiguration!.ReadCurrentParameter(port.Id)?.Value;
                Require(Equals(live,replayed),"Redo当前值未遵守live种入合同："+port.Id);if(!Equals(live,consumed.ReadCurrentParameter(port.Id)?.Value))sensitive++;
            }
            Require(sensitive>0,"历史当前值样本没有与已消费值分离");Replay(173,25);var position=history.Position;VerifyPublishedMixed();Trace("after-reconsume",Stamp(node.RuntimeConfiguration!));Require(history.Position==position,"重新消费被错误写入编辑历史");
            foreach(var port in consumed.SettingsSchema.Inputs.Where(port=>consumed.Fields.Read(port.Id) is ReferenceSetting or VariableSetting))Require(Equals(node.RuntimeConfiguration!.ReadCurrentParameter(port.Id)?.Value,consumed.ReadCurrentParameter(port.Id)?.Value),"真实重跑未接纳本次来源："+port.Id);
        });
        Case("同窗逐叶继承及真实Int64图像宽度来源与范围图文实际消费",()=>{
            var window=Open();Mix(window);((DeclaredSettingsView)Get(Control(window).Content,"Editor")).SelectedPageIndex=0;window.UpdateLayout();Pump();var ui=Logical(Control(window)).OfType<SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiUiDraftControls>().Single();var radios=((System.Collections.IEnumerable)Get(ui,"_radios")).Cast<object>().ToArray();((ICommand)Get(radios[1],"ValueChangedCommand")).Execute(1);((ICommand)Get(radios[2],"ValueChangedCommand")).Execute(1);Pump();var inputs=Logical(Control(window)).OfType<DeclaredInputFieldsView>().Single();var ids=BlobRoiInheritance.RectangleMembers.Select(member=>BlobRoiInheritance.Leaf(BlobRoiInheritance.RectangleParameters,member)).ToArray();var scalar=new[]{3d,3d,4d,4d,0d};for(var i=0;i<ids.Length;i++)Require(inputs.Fields.Single(field=>field.FieldId==ids[i]).TrySetConstant(scalar[i]),"逐叶不能构成不完整草稿："+ids[i]);
            var width=inputs.Fields.Single(field=>field.FieldId==ids[2]);var toggle=Visual(width).OfType<ToggleButton>().Single(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path=="IsSubscribeLeft");toggle.SetCurrentValue(ToggleButton.IsCheckedProperty,true);toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));_activePopup=Visual(width).OfType<AppsPopup>().Single(item=>item.Name=="rt");_activePopup.ApplyTemplate();Choose(_activePopup.DataContext,false,item=>Candidate(item)?.Reference(BindingSelection.All) is {} reference&&reference.ScopeId==flow.Id&&reference.NodeId==source.Id&&reference.PortId=="output.image"&&reference.Storage is null&&reference.Type==source.RuntimeConfiguration!.Schema.Outputs.Single(port=>port.Id=="output.image").Type&&reference.Selection is BindingSelection.AllItems&&reference.MemberFieldPath.SequenceEqual(new[]{"width"}));Constant(window,SettingId(nameof(BlobSettings.FilterAreaMax)),50L);Expect(window,173,40);Confirm(window);Replay(173,40);
        });
        Case("合法未失焦和非法pending经整窗原子捕获，不手工失焦",()=>{
            var window=Open();var control=Control(window);var low=Low(window);UseConstant(low);low=Low(window);var text=(TextBox)low.InputElement!;var lost=0;KeyboardFocusChangedEventHandler focus=(_,_)=>lost++;text.LostKeyboardFocus+=focus;var formal=Stamp(node.RuntimeConfiguration!);var bytes=File.ReadAllBytes(path);
            try{text.SetCurrentValue(TextBox.TextProperty,"bad");var area=Field(window,SettingId(nameof(BlobSettings.FilterAreaMin)));UseConstant(area);((TextBox)area.InputElement!).SetCurrentValue(TextBox.TextProperty,"21");var before=Stamp(Draft(window).DraftNode.RuntimeConfiguration!);Require(!control.TryCapture(out _)&&Stamp(Draft(window).DraftNode.RuntimeConfiguration!)==before&&Stamp(node.RuntimeConfiguration!)==formal&&bytes.SequenceEqual(File.ReadAllBytes(path)),"非法pending捕获发生半写或正式发布");text.SetCurrentValue(TextBox.TextProperty,"155");Require(control.TryCapture(out var error),error);Require(lost==0,"夹具产生了系统失焦，不能称未失焦");Confirm(window);}finally{text.LostKeyboardFocus-=focus;}Replay(155,40);
        });
        Case("混合关系断边保值拒绝，Undo恢复且旧窗来源回调不写新owner",()=>{
            var window=Open();var low=Low(window);Subscribe(low);var oldSession=Session(low);Choose(oldSession,false,item=>Candidate(item)?.Reference(BindingSelection.All) is {} reference&&reference.ScopeId==flow.Id&&reference.NodeId==source.Id&&reference.PortId=="output.image"&&reference.Storage is null&&reference.Type==source.RuntimeConfiguration!.Schema.Outputs.Single(port=>port.Id=="output.image").Type&&reference.Selection is BindingSelection.AllItems&&reference.MemberFieldPath.SequenceEqual(new[]{"width"}));var oldChoice=_lastChoice;Confirm(window);var relation=Identity(node.RuntimeConfiguration!.Fields.Read(threshold));vm.CurrentCommandManager!.Execute(new SunEyeVision.UI.Features.WorkflowUI.Commands.BatchDeleteNodesAndConnectionsCommand(flow.Nodes,flow.Connections,new(),new(){edge}));Pump();window=Open();Require(!Control(window).TryCapture(out _)&&Identity(node.RuntimeConfiguration!.Fields.Read(threshold))==relation,"失效来源被清除或误放行");window.Close();Pump();history.Undo();Pump();
            Require(Context().GetNodeResult(source.Id)?.Result is null,"撤销连线不应复活旧来源结果");vm.SelectedNode=source;source.IsSelected=true;var rerun=vm.RunSelectedImageAsync();PumpUntil(()=>rerun.IsCompleted);rerun.GetAwaiter().GetResult();Require(Context().GetNodeResult(source.Id)?.Result is DeclaredOutputSnapshot {IsSuccess:true},"恢复连线后未能重新生成来源结果");vm.SelectedNode=node;node.IsSelected=true;Pump();
            window=Open();var newDraft=Draft(window);var accepted=Stamp(newDraft.DraftNode.RuntimeConfiguration!);((ICommand)Get(oldSession,"ClickCommand")).Execute(oldChoice);Pump();Require(newDraft.IsValid&&Stamp(newDraft.DraftNode.RuntimeConfiguration!)==accepted,"同ID新owner被旧来源回调覆盖");Debug(window,8);window.Close();Pump();
        });
        Case("同一真实窗口数值ROI与文本多映射位置引用确认历史重开执行",()=>{
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 1"+Environment.NewLine);
            var before=Stamp(node.RuntimeConfiguration!);var window=Open();Mix(window);
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 2"+Environment.NewLine);
            var roiEditor=(DeclaredSettingsView)Get(Control(window).Content,"Editor");roiEditor.SelectedPageIndex=0;window.UpdateLayout();Pump();var roiUi=Logical(Control(window)).OfType<SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiUiDraftControls>().Single();var radios=((System.Collections.IEnumerable)Get(roiUi,"_radios")).Cast<object>().ToArray();((ICommand)Get(radios[1],"ValueChangedCommand")).Execute(1);((ICommand)Get(radios[2],"ValueChangedCommand")).Execute(1);Pump();
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 3"+Environment.NewLine);
            var inputs=Logical(Control(window)).OfType<DeclaredInputFieldsView>().Single();var ids=BlobRoiInheritance.RectangleMembers.Select(member=>BlobRoiInheritance.Leaf(BlobRoiInheritance.RectangleParameters,member)).ToArray();var scalar=new[]{3d,3d,4d,4d,0d};for(var i=0;i<ids.Length;i++)Require(inputs.Fields.Single(field=>field.FieldId==ids[i]).TrySetConstant(scalar[i]),"逐叶ROI设置失败");
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 4"+Environment.NewLine);
            var width=inputs.Fields.Single(field=>field.FieldId==ids[2]);var widthToggle=Visual(width).OfType<ToggleButton>().Single(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path=="IsSubscribeLeft");widthToggle.SetCurrentValue(ToggleButton.IsCheckedProperty,true);widthToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));_activePopup=Visual(width).OfType<AppsPopup>().Single(value=>value.Name=="rt");_activePopup.ApplyTemplate();Choose(_activePopup.DataContext,false,value=>Candidate(value)?.Reference(BindingSelection.All) is {} reference&&reference.ScopeId==flow.Id&&reference.NodeId==source.Id&&reference.PortId=="output.image"&&reference.MemberFieldPath.SequenceEqual(new[]{"width"}));Constant(window,SettingId(nameof(BlobSettings.FilterAreaMax)),50L);
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 5"+Environment.NewLine);
            var editor=(DeclaredSettingsView)Get(Control(window).Content,"Editor");editor.SelectedPageIndex=2;window.UpdateLayout();Pump();var fields=(DeclaredFieldsView)editor.Content;
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 6"+Environment.NewLine);
            var contentId=BlobResultDisplay.Field(BlobResultDisplay.Text,"content");var xId=BlobResultDisplay.Field(BlobResultDisplay.Text,"x");
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 7"+Environment.NewLine);
            var text=fields.Fields.Single(field=>field.FieldId==contentId);var presenter=Get(text.View,"_textPresenter");var textSession=Get(presenter,"_session");var raw=Visual(text.View).OfType<TextBox>().Single();
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 8"+Environment.NewLine);
            const string proposed="宽:{};面积:{面积}";raw.SetCurrentValue(TextBox.TextProperty,proposed);raw.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();Keyboard.Focus(raw);Pump();var caret=proposed.IndexOf("{}",StringComparison.Ordinal)+1;raw.SelectionStart=caret-1;raw.SelectionStart=caret;Pump();
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 9"+Environment.NewLine);
            PumpUntil(()=>Tree((System.Collections.IEnumerable)Get(textSession,"SourceNodes")).Any(item=>Candidate(item) is {} c&&c.NodeId==source.Id&&c.Output.Id=="output.image"&&c.MemberPath.SequenceEqual(new[]{"width"})));
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 10"+Environment.NewLine);
            var selected=Tree((System.Collections.IEnumerable)Get(textSession,"SourceNodes")).Single(item=>Candidate(item) is {} c&&c.NodeId==source.Id&&c.Output.Id=="output.image"&&c.MemberPath.SequenceEqual(new[]{"width"}));
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 11"+Environment.NewLine);
            var textPopup=Visual(text.View).OfType<AppsPopup>().Single();textPopup.ApplyTemplate();var part=(Popup)textPopup.Template.FindName("PART_Popup",textPopup)!;Require(part.IsOpen&&part.Child is not null,"原文本来源树未打开");
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 12"+Environment.NewLine);
            var tree=Visual(part.Child).OfType<SunEyeVision.Plugin.Wpf.Controls.References.Primitives.SubTreeView>().Single();var native=(TreeView)tree.FindName("treeView");tree.UpdateLayout();Pump();
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 13"+Environment.NewLine);
            var item=(TreeViewItem?)tree.GetType().GetMethod("FindTreeViewItem",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{native,selected})??throw new Exception("文本来源容器未生成");item.IsSelected=true;Pump();item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=System.Windows.Controls.Control.MouseDoubleClickEvent});Pump();
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 14"+Environment.NewLine);
            Require(text.Setting is DisplayExpressionSetting expression&&expression.Slots.Any(slot=>slot.Reference?.NodeId==source.Id)&&expression.Slots.Any(slot=>slot.Self is not null),"同窗未接纳前驱与Self映射");
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 15"+Environment.NewLine);
            var position=fields.Fields.Single(field=>field.FieldId==xId);var popup=Logical(position.View).Concat(Visual(position.View)).OfType<AppsPopup>().First(value=>value.IsVisible);popup.IsDropDownOpen=true;popup.UpdateLayout();Pump();var positionSession=popup.DataContext;((ICommand)Get(positionSession,"DropDownCommand")).Execute(null);Pump();
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 16"+Environment.NewLine);
            var positionChoice=Tree((System.Collections.IEnumerable)Get(positionSession,"SourceNodes")).Single(value=>Candidate(value)?.Reference(BindingSelection.All) is {} reference&&reference.NodeId==source.Id&&reference.PortId=="output.image"&&reference.MemberFieldPath.SequenceEqual(new[]{"width"}));((ICommand)Get(positionSession,"ClickCommand")).Execute(positionChoice);popup.IsDropDownOpen=false;Pump();
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 17"+Environment.NewLine);
            Require(position.Setting is ReferenceSetting positionReference&&positionReference.Reference.NodeId==source.Id,"同窗位置引用未接纳");Expect(window,173,40);Confirm(window);
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 18"+Environment.NewLine);
            var accepted=Stamp(node.RuntimeConfiguration!);Require(node.RuntimeConfiguration!.Fields.Read(contentId) is DisplayExpressionSetting&&node.RuntimeConfiguration.Fields.Read(xId) is ReferenceSetting,"混合确认丢失显示引用");
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" STEP 19"+Environment.NewLine);
            Replay(173,40);VerifyPublishedMixed("宽:8;面积:40",8,8);history.Undo();Pump();var beforeRoot=System.Text.Json.Nodes.JsonNode.Parse(before)!;var undoRoot=System.Text.Json.Nodes.JsonNode.Parse(Stamp(node.RuntimeConfiguration!))!;Require(beforeRoot["Fields"]!.ToJsonString()==undoRoot["Fields"]!.ToJsonString()&&beforeRoot["ParameterBindingOrder"]!.ToJsonString()==undoRoot["ParameterBindingOrder"]!.ToJsonString(),"混合Undo未恢复正式字段和绑定顺序");history.Redo();Pump();Require(node.RuntimeConfiguration!.Fields.Read(contentId) is DisplayExpressionSetting&&node.RuntimeConfiguration.Fields.Read(xId) is ReferenceSetting,"混合Redo丢显示引用");Replay(173,40);VerifyPublishedMixed("宽:8;面积:40",8,8);
        });
        Case("混合引用真实方案新会话重开后执行与图文显示",()=>{
            var targetId=node.Id;var sourceId=source.Id;var workflowId=flow.Id;var previous=solutions.Session;var previousOwner=solutions.CurrentSolution!.ExecutionOwner;
            Require(solutions.SaveCurrentDocument(path).FileWritten,"重开前混合方案未保存");
            var reopened=solutions.OpenSolution(path)??throw new Exception("混合正式方案真实重开失败");Pump();
            Require(!ReferenceEquals(previous,solutions.Session)&&reopened.ExecutionOwner.Id!=previousOwner.Id,"重开未建立新会话和运行owner");
            node=vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.OfType<SunEyeVision.UI.Shared.WorkflowNode>().Single(value=>value.Id==targetId);source=vm.WorkflowTabViewModel.SelectedTab.WorkflowNodes.OfType<SunEyeVision.UI.Shared.WorkflowNode>().Single(value=>value.Id==sourceId);flow=reopened.Workflows.Single(value=>value.Id==workflowId);host=solutions.Session!.Editing.Host;history=solutions.Session.History;
            var content=node.RuntimeConfiguration!.Fields.Read(BlobResultDisplay.Field(BlobResultDisplay.Text,"content"));var x=node.RuntimeConfiguration.Fields.Read(BlobResultDisplay.Field(BlobResultDisplay.Text,"x"));
            Require(content is DisplayExpressionSetting expression&&expression.Slots.Any(slot=>slot.Reference?.NodeId==sourceId)&&expression.Slots.Any(slot=>slot.Self is not null)&&x is ReferenceSetting position&&position.Reference.NodeId==sourceId,"新会话丢失真实表达式或位置来源身份");
            VerifyPublishedMixed("宽:8;面积:40",8,8);var snapshot=(DeclaredOutputSnapshot)Get(vm,"_resultPublicationSnapshot");Require(snapshot.Identity.DocumentSessionId==reopened.ExecutionOwner.Id,"重开后显示仍借用旧owner");
        });
        Case("持久引用无CurrentValues真实窗切常量保关系，非法取消不伪造默认",()=>{
            var missing=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);missing.DebugWindowStyle=DebugWindowStyle.Custom;var configuration=RuntimeToolConfiguration.Create(BlobTool.Id).WithFieldSettings(new Dictionary<string,ParameterSetting>{{threshold,new VariableSetting(new VariableReference(solutions.CurrentSolution!.Id,values.Id,TypeRef.SequenceOf((TypeRef)DataType.Integer),new BindingSelection.AtConstant(0)))}});
            var json=System.Text.Json.Nodes.JsonNode.Parse(Stamp(configuration))!;json["CurrentValues"]!.AsObject().Remove(threshold);missing.RuntimeConfiguration=System.Text.Json.JsonSerializer.Deserialize<RuntimeToolConfiguration>(json.ToJsonString())!;Require(missing.RuntimeConfiguration.ReadCurrentParameter(threshold) is null,"无值夹具错误种入了默认");vm.AddNodeToWorkflow(missing);Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=source.Id,TargetNodeId=missing.Id}),"无值节点图像边失败");var file=Path.Combine(folder,"missing-current.solution");var missingSave=solutions.SaveCurrentDocument(file);Console.WriteLine("MISSING_SAVE "+System.Text.Json.JsonSerializer.Serialize(new{Result=missingSave,SessionPath=solutions.Session!.FilePath,Target=file,Dirty=solutions.Session.IsDirty,Configuration=Stamp(missing.RuntimeConfiguration!)}));Require(missingSave.FileWritten,"持久空当前值保存失败："+missingSave.Error);var previousSession=solutions.Session;var missingId=missing.Id;var reopened=solutions.OpenSolution(file)??throw new Exception("无CurrentValues文件真实新会话重开失败");Pump();Console.WriteLine("REOPEN_UI "+System.Text.Json.JsonSerializer.Serialize(vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.Select(item=>new{item.Id,Type=item.GetType().FullName})));missing=vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.OfType<SunEyeVision.UI.Shared.WorkflowNode>().Single(item=>item.Id==missingId);Require(Stamp(missing.RuntimeConfiguration!)==Stamp(reopened.Workflows.Single(workflow=>workflow.Id==flow.Id).Nodes.Single(item=>item.Id==missingId).RuntimeConfiguration!),"真实重开UI节点未绑定新方案模型");Require(!ReferenceEquals(previousSession,solutions.Session)&&missing.RuntimeConfiguration!.ReadCurrentParameter(threshold) is null&&missing.RuntimeConfiguration.Fields.Read(threshold) is VariableSetting,"新DocumentSession错误补当前值/丢引用");var bytes=File.ReadAllBytes(file);var formal=Stamp(missing.RuntimeConfiguration!);
            var window=OpenFor(missing);var field=Low(window);Require(field.Setting is VariableSetting,"打开无值窗丢关系");UseConstant(field);Require(field.Setting is VariableSetting&&!Control(window).TryCapture(out _),"无值切常量伪造了默认/提前清引用");var raw=(TextBox)field.InputElement!;raw.SetCurrentValue(TextBox.TextProperty,"invalid");Require(!Control(window).TryCapture(out _)&&field.Setting is VariableSetting,"非法新常量覆盖持久引用");window.Close();Pump();Require(Stamp(missing.RuntimeConfiguration!)==formal&&bytes.SequenceEqual(File.ReadAllBytes(file)),"无值非法取消写正式/文件");
            window=OpenFor(missing);field=Low(window);UseConstant(field);raw=(TextBox)field.InputElement!;raw.SetCurrentValue(TextBox.TextProperty,"149");Require(Control(window).TryCapture(out var issue),issue);Confirm(window);Require(missing.RuntimeConfiguration!.Fields.Read(threshold) is ConstantSetting {Value:149L},"显式合法值未解除无当前值引用");
        });
        shell.Close();dialogs.Stop();Pump();Console.WriteLine("B08已支持完整数值引用混合窗口："+_passed+"组通过；不代原厂全域/自然焦点/剪贴板/连续会话；本地ROI通过正式输入提案而非物理绘制。");return 0;
    }
    private static object? _lastChoice;
    private static object Get(object target,string name){for(var type=target.GetType();type is not null;type=type.BaseType){var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;var property=type.GetProperty(name,flags);if(property is not null)return property.GetValue(target)!;var field=type.GetField(name,flags);if(field is not null)return field.GetValue(target)!;}throw new MissingMemberException(target.GetType().FullName,name);}
    private static DeclaredReferenceCandidate? Candidate(object item)=>(DeclaredReferenceCandidate?)Get(item,"Candidate");
    private static AppsPopup? _activePopup;
    private static readonly Dictionary<object,DeclaredFieldAccess> SessionFields=new();
    private static readonly Dictionary<object,ParameterSetting> SessionSources=new();
    private static object Session(DeclaredFieldAccess field){_activePopup=Visual(field.View).OfType<AppsPopup>().Single(item=>item.Name=="rt"&&item.DataContext?.GetType().Name=="ReferenceFieldSession"&&((Port)Get(item.DataContext,"Target")).Id==field.FieldId);_activePopup.ApplyTemplate();var session=_activePopup.DataContext;SessionFields[session]=field;return session;}
    private static void SetSelection(object session,BindingSelection selection)
    {
        var field=SessionFields[session];
        var source=SessionSources.GetValueOrDefault(session)??field.Setting;
        ParameterSetting proposal=source switch
        {
            VariableSetting v=>v with {Reference=new VariableReference(v.Reference.SolutionId,v.Reference.VariableId,v.Reference.Type,selection,v.Reference.Storage)},
            ReferenceSetting r=>r with {Reference=new OutputReference(r.Reference.NodeId,r.Reference.PortId,r.Reference.Type,r.Reference.MemberFieldPath,selection,r.Reference.ScopeId,r.Reference.Storage)},
            _=>throw new InvalidOperationException("索引命令需要现有引用。")
        };
        var window=(NativeRuntimeEditorWindow)(Window.GetWindow(field.View)??throw new InvalidOperationException("字段没有所属窗口。"));
        var editor=(DeclaredSettingsView)Get(window.EditorControl.Content,"Editor");
        Require(editor.TrySetFields(new Dictionary<string,ParameterSetting>{{field.FieldId,proposal}}),"非视觉索引提案被拒绝："+field.Error);Pump();
    }
    private static bool Right(DeclaredFieldAccess field)=>new[]{nameof(BlobSettings.FilterAreaMax),nameof(BlobSettings.FilterCircularityMax),nameof(BlobSettings.FilterRectangularityMax)}.Any(name=>BlobParameterDefinition.Instance.SettingsFieldId(name)==field.FieldId);
    private static void Subscribe(DeclaredFieldAccess field)
        {
        if(field.Setting is ReferenceSetting or VariableSetting)return;
        var toggles=Visual(field.View).OfType<ToggleButton>().ToArray();var expected=Right(field)?"IsSubscribeRight":"IsSubscribeLeft";
        Console.WriteLine("SUBSCRIBE_TRACE "+System.Text.Json.JsonSerializer.Serialize(new{field.FieldId,View=field.View.GetType().FullName,field.View.IsLoaded,field.View.IsVisible,Expected=expected,Toggles=toggles.Select(button=>new{button.Name,button.IsLoaded,button.IsVisible,Path=BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path,Data=button.DataContext?.GetType().FullName,Target=button.Tag?.ToString()}),Tree=Visual(field.View).Select(item=>item.GetType().FullName)}));
        var toggle=toggles.Single(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path==expected);toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();
    }
    private static IEnumerable<object> Tree(System.Collections.IEnumerable nodes){foreach(var node in nodes){yield return node!;foreach(var child in Tree((System.Collections.IEnumerable)Get(node!,"EnumEntrysA")))yield return child;}}
    private static void Choose(object session,bool index,Func<object,bool> predicate)
    {if(_activePopup is {IsLoaded:true}){_activePopup.IsDropDownOpen=true;_activePopup.UpdateLayout();Pump();}((ICommand)Get(session,"DropDownCommand")).Execute(null);Pump();var nodes=Tree((System.Collections.IEnumerable)Get(session,index?"IndexNodes":"SourceNodes")).ToArray();var matches=nodes.Where(predicate).ToArray();var selected=matches.Select(item=>Candidate(item)?.Reference(BindingSelection.All)).FirstOrDefault(reference=>reference is not null);var alternatives=nodes.Select(item=>Candidate(item)?.Reference(BindingSelection.All)).Where(reference=>reference is not null&&(selected is null||reference.NodeId==selected.NodeId&&reference.MemberFieldPath.SequenceEqual(selected.MemberFieldPath))).Select(reference=>BindingReferenceSerializer.Serialize(reference!)).ToArray();File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"source-candidates.jsonl"),System.Text.Json.JsonSerializer.Serialize(new{Target=((Port)Get(session,"Target")).Id,Index=index,Matches=matches.Length,Alternatives=alternatives})+Environment.NewLine);var item=matches.Single();_lastChoice=item;if(index)SetSelection(session,(bool)Get(item,"Clear")?BindingSelection.All:new BindingSelection.AtField(Candidate(item)!.Reference(BindingSelection.All)));else ((ICommand)Get(session,"ClickCommand")).Execute(item);Pump();}
    private static void SetIndex(object session,string index)
    {SetSelection(session,new BindingSelection.AtConstant(int.Parse(index,System.Globalization.CultureInfo.InvariantCulture)));}
    private static void SelectVariable(object session,bool index,string id)
    {
        if(index){var solution=SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.CurrentSolution!;SetSelection(session,new BindingSelection.AtVariable(new VariableReference(solution.Id,id,DataType.Integer,BindingSelection.All,NumericStorage.Int64)));return;}
        // Complete popup preparation before starting the modal waiter; no queued callback races a pre-dialog Pump.
        if(_activePopup is {IsLoaded:true}){_activePopup.IsDropDownOpen=true;_activePopup.UpdateLayout();Pump();}
        ((ICommand)Get(session,"DropDownCommand")).Execute(null);Pump();
        var manager=Tree((System.Collections.IEnumerable)Get(session,index?"IndexNodes":"SourceNodes")).Single(item=>(bool)Get(item,"VariableManager"));
        Exception? failure=null;var selected=false;VariableSelectionDialog? ownedDialog=null;var deadline=DateTime.UtcNow.AddSeconds(15);var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>{
            if(DateTime.UtcNow>deadline){timer.Stop();failure=new TimeoutException("等待自有真实变量模态窗超过15秒");ownedDialog?.Close();return;}
            var dialog=Application.Current.Windows.OfType<VariableSelectionDialog>().SingleOrDefault(window=>window.IsVisible);if(dialog is null)return;ownedDialog=dialog;timer.Stop();
            try{var item=dialog.BindItems.Single(row=>row.Reference.VariableId==id);SessionSources[session]=new VariableSetting(item.Reference);((ListView)dialog.FindName("globalVarList")).SelectedItem=item;Visual(dialog).OfType<Button>().Single(button=>Equals(button.Content,dialog.TryFindResource("Confirm"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));selected=true;}
            catch(Exception error){failure=error;ownedDialog.Close();}
        };
        Step("Variable modal before "+id);timer.Start();try{((ICommand)Get(session,index?"IndexClickCommand":"ClickCommand")).Execute(manager);}finally{timer.Stop();Step("Variable modal returned "+id);}Step("Variable Pump before");Pump();Step("Variable Pump after");if(failure is not null)throw new Exception("原模态变量选择失败",failure);Require(selected,"原模态变量窗没有完成实际选择");
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
    private static void Step(string phase)=>File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mixed-window-steps.log"),DateTime.UtcNow.ToString("O")+" "+phase+Environment.NewLine);
    private static void Case(string name,Action action)
    {
        var filter=Environment.GetEnvironmentVariable("B08_NUMERIC_COMPOSITION_CASE");if(!string.IsNullOrEmpty(filter)&&!filter.Split('|').Any(part=>name.Contains(part,StringComparison.Ordinal)))return;
        var file=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"reference-chain-progress.log");
        File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" BEGIN "+name+Environment.NewLine);
        try{action();_passed++;Console.WriteLine("通过："+name);File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" PASS "+name+Environment.NewLine);}
        catch(Exception failure){File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" FAIL "+name+Environment.NewLine+failure+Environment.NewLine);throw;}
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
