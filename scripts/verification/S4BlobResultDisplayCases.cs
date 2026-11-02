using NativeRectangle=SunEyeVision.Plugin.Wpf.Native.Display.Contracts.IRectangle;
using NativeRectShape=SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.Shape.RectShape;
using NativeShapeCanvas=SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.Shape.ShapeCanvas;
using NativeOverlay=SunEyeVision.Plugin.Wpf.Presenters.Display.NativeGeometryOverlay;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using System.IO;
using System.Reflection;
using System.Windows.Data;
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
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
using SunEyeVision.UI.Shell.Controls.Shell;

internal static partial class S2DocumentEditingCases
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
        Console.WriteLine("Display schema fields="+BlobResultDisplay.Schema.Inputs.Count);
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
        var scope=Environment.GetEnvironmentVariable("S4_BLOB_DISPLAY_SCOPE")??"All";
        Require(scope is "All" or "Core" or "Window" or "Roi" or "Text" or "TextSource" or "Render","未知显示专项");
        var resultView=new ImageResultControl{DataContext=vm.ImageResult};resultView.Initialize(vm);
        var shell=new Window{Width=1000,Height=850,Content=resultView,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();Pump();
        var node=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(node);node.DebugWindowStyle=DebugWindowStyle.Custom;
        var source=vm.WorkflowTabViewModel.SelectedTab.CreateNode("ImageLoad");
        var imageFile=Path.Combine(folder,"input.png");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,64,96,96,PixelFormats.Gray8,null,Pixels(),64)));using(var file=File.Create(imageFile))png.Save(file);
        var policy=ImageSourcePolicy.Read(source)!;policy.FilePaths=new(){imageFile};policy.CurrentIndex=0;policy.ForRunPaths=new(){imageFile};policy.SNInitialValue=1;policy.ApplyTo(source);vm.AddNodeToWorkflow(source);
        Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=source.Id,TargetNodeId=node.Id}),"真实图像连线失败");
        var inputReference=((ReferenceSetting)node.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput)).Reference;var key=BindingReferenceSerializer.Serialize(inputReference);
        var flowId=vm.WorkflowTabViewModel.SelectedTab.RootWorkflowId;var host=solutions.Session!.Editing.Host;var history=solutions.Session.History;
        vm.SelectedNode=node;node.IsSelected=true;Pump();
        var workspace=(ResultWorkspaceShell)resultView.FindName("Workspace");workspace.IsDoubleImage=false;Pump();
        NativeRuntimeEditorWindow Open()
        {vm.OpenDebugWindowCommand.Execute(node);Pump();var window=app.Windows.OfType<NativeRuntimeEditorWindow>().Single(w=>w.IsVisible);window.UpdateLayout();Pump();return window;}
        RuntimeNodeConfigurationControl Control(NativeRuntimeEditorWindow window)=>window.EditorControl;
        object Owner(RuntimeNodeConfigurationControl control)=>Get(control.Content,"_roiOwner");
        NativeRoiSession Session(object owner,string group)=>(NativeRoiSession)((System.Collections.IDictionary)Get(owner,"_sessions"))[group]!;
        NativeRoiEditor Editor(object owner,string group)=>(NativeRoiEditor)((System.Collections.IDictionary)Get(owner,"_editors"))[group]!;
        void SelectInput(bool doubleImage=false)
        {
            workspace.IsDoubleImage=doubleImage;Pump();var items=(System.Collections.IEnumerable)(doubleImage?workspace.InputItems:workspace.OutputItems);
            var item=items.Cast<object>().Single(item=>(string)Get(item,"Key")==key);
            if(doubleImage)workspace.SelectedInputItem=item;else workspace.SelectedOutputItem=item;Pump();
        }
        var drawTrace=new List<object>();
        void Draw(object owner,string group,string kind,bool expected=true)
        {
            var editor=Editor(owner,group);var context=editor.NativeView.DataContext;
            var type=kind switch {"rectangle"=>SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box,"annulus"=>SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.SectorRing,"sector"=>SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Sector,_=>SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Polygon};
            ((ICommand)Get(context,"ValueChangedCommand")).Execute(type);Pump();
            var session=Session(owner,group);var request=session.ActiveDraw??throw new Exception("原工具箱未进入实际视口绘制："+Get(owner,"Error"));
            var attachment=(SunEyeVision.Plugin.Wpf.Presenters.Display.INativeRoiEditLease)Get(owner,"_interaction");
            void TraceDraw(string phase)
            {
                drawTrace.Add(new{group,kind,phase,activeDraw=session.ActiveDraw?.Id,attachment.HasPendingGesture,revision=(long)Get(owner,"Revision")});
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"roi-draw-trace.json"),System.Text.Json.JsonSerializer.Serialize(drawTrace,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            }
            TraceDraw("before-actual-completion");
            var completionType=typeof(NativeRoiEditor).Assembly.GetType("SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiDrawCompletion")!;
            var completion=Activator.CreateInstance(completionType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{request,NativeShape(kind)},null)!;
            var completed=(bool)attachment.GetType().GetMethod("CompleteDrawing",Flags)!.Invoke(attachment,new[]{completion})!;Pump();TraceDraw("after-actual-completion");
            Require(completed==expected&&session.ActiveDraw is null&&!attachment.HasPendingGesture,"原附件完成链未接纳/退役手势："+group+"/"+kind+" "+session.Error);
        }

        var flow=solutions.CurrentSolution!.Workflows.Single(flow=>flow.Id==flowId);
        string Display(string group,string role)=>BlobResultDisplay.Field(group,role);
        var displaySchema=node.RuntimeConfiguration!.DisplaySettingsSchema;
        string FieldValue(string property)=>BlobParameterDefinition.Instance.SettingsFieldId(property);
        SunEyeVision.Plugin.Wpf.Presenters.Results.IResultPublication Publication()=>(SunEyeVision.Plugin.Wpf.Presenters.Results.IResultPublication)Get(vm,"CurrentResultPublication");
        var presenter=Get(resultView,"_workspacePresenter");var outputPane=Get(presenter,"_output");
        var viewport=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultViewport)Get(Get(presenter,"OutputPane"),"Viewport");
        void SelectBlob(bool binary=false)
        {
            workspace.IsDoubleImage=false;Pump();var id=BlobSchema.Field(binary?6:5);
            var item=workspace.OutputItems.Cast<object>().Single(item=>Get(item,"Reference") is OutputReference reference&&reference.NodeId==node.Id&&reference.PortId==id);
            workspace.SelectedOutputItem=item;Pump();
        }
        object Scene(){try{PumpUntil(()=>Maybe(outputPane,"Display") is not null);}catch{throw new Exception("真实结果常驻场景未安装");}return Get(outputPane,"Display");}
        object[] Items(object scene)=>(object[])Get(Get(Get(scene,"Layer"),"_prepared"),"Items");
        object[] Geometry(object scene)=>Items(scene).Where(item=>Maybe(item,"Texts") is null&&Maybe(item,"StrokeThickness") is not null).ToArray();
        IEnumerable<object> Words(object scene)=>Items(scene).Where(item=>Maybe(item,"Texts") is not null).SelectMany(item=>((System.Collections.IEnumerable)Get(item,"Texts")).Cast<object>());
        void Run(){var run=vm.RunSelectedImageAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();Pump();SelectBlob();}
        void SetFormal(string id,object value){var edit=host.BeginEdit(flowId,node.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{id,value}});host.Commit(edit);Pump();}
        void DeleteThroughHost(string id)
        {
            foreach(var item in vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes)item.IsSelected=false;foreach(var edge in vm.WorkflowTabViewModel.SelectedTab.WorkflowConnections)edge.IsSelected=false;
            vm.WorkflowTabViewModel.SelectedTab.WorkflowNodes.Single(item=>item.Id==id).IsSelected=true;
            dialogs.Stop();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};var confirmed=false;Exception? failure=null;var deadline=DateTime.UtcNow.AddSeconds(15);
            timer.Tick+=(_,_)=>{var dialog=app.Windows.OfType<Window>().SingleOrDefault(item=>item.GetType().Name=="ThemeMessageDialog"&&item.IsVisible);if(DateTime.UtcNow>deadline){timer.Stop();failure=new TimeoutException("等待自有删除确认框超时");dialog?.Close();return;}if(dialog is null)return;try{Require(((TextBlock)dialog.FindName("TitleText")).Text=="确认删除","出现非删除确认，不执行Yes");timer.Stop();confirmed=true;((Button)dialog.FindName("YesButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));}catch(Exception error){failure=error;dialog.Close();}};
            timer.Start();try{vm.DeleteSelectedNodesCommand.Execute(null);}finally{timer.Stop();dialogs.Start();}Pump();if(failure is not null)throw failure;Require(confirmed&&!solutions.CurrentSolution!.Workflows.Single(flow=>flow.Id==flowId).Nodes.Any(node=>node.Id==id),"未经过主窗真实删除确认或节点仍存在");
        }
        void PrivateDebug(NativeRuntimeEditorWindow window,bool expectedSuccess,Action<DeclaredOutputSnapshot,RuntimeDebugInvocation>? inspect=null)
        {
            var control=Control(window);var capture=(Func<WorkflowNodeBase,RuntimeDebugInvocation>)Get(control,"_captureDebug");RuntimeDebugInvocation? invocation=null;DeclaredOutputSnapshot? output=null;IDisposable? hold=null;Exception? observationFailure=null;var completed=false;var events=0;
            SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventHandler observe=(_,args)=>{events++;try{if(args.Results is DeclaredOutputSnapshot result){output=result;hold=result.Retain();}}catch(Exception error){observationFailure=error;}completed=true;};
            control.SetDebugCapture(draft=>{invocation=capture(draft);return invocation;});control.AddHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observe);
            try
            {
                Require(SunEyeVision.Plugin.SDK.Commands.ToolCommands.Execute.CanExecute(null,control),"真实宿主调试命令未就绪");SunEyeVision.Plugin.SDK.Commands.ToolCommands.Execute.Execute(null,control);PumpUntil(()=>completed&&!(bool)Get(control,"_running"));if(observationFailure is not null)throw observationFailure;
                Require(events==1&&((output?.IsSuccess==true)==expectedSuccess),"真实宿主调试结果不符：events="+events+" status="+output?.Status);
                if(expectedSuccess){Require(invocation is not null&&output is not null,"成功调试未捕获实际快照");inspect?.Invoke(output!,invocation!);}else Require(invocation is null,"缺源调试进入了算法捕获/执行而非在正式准入拒绝");
            }
            finally{control.RemoveHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,observe);if(!(bool)Get(control,"_disposed"))control.SetDebugCapture(capture);hold?.Dispose();}
        }        if(scope is "All" or "Core")
        {
            Case("正式显示声明与算法阶段隔离，常量模式及默认只创建一次",()=>{
                var config=node.RuntimeConfiguration!;Require(config.SettingsSchema.Inputs.Count==55&&config.DisplaySettingsSchema.Inputs.Count==28&&config.EditorSettingsSchema.Inputs.Count==83,"算法/显示编辑声明边界错误");
                Require(displaySchema.Inputs.Where(port=>port.Id!=Display(BlobResultDisplay.Text,"content")&&port.Id!=Display(BlobResultDisplay.Text,"x")&&port.Id!=Display(BlobResultDisplay.Text,"y")).All(port=>port.EditModes==FieldEditModes.Constant),"非文本角色被扩大订阅");
                Require(displaySchema.Inputs.Single(port=>port.Id==Display(BlobResultDisplay.Text,"content")).EditModes==(FieldEditModes.Constant|FieldEditModes.DisplayExpression)&&displaySchema.Inputs.Where(port=>port.Id==Display(BlobResultDisplay.Text,"x")||port.Id==Display(BlobResultDisplay.Text,"y")).All(port=>port.EditModes==(FieldEditModes.Constant|FieldEditModes.Reference)),"文本显示专属能力未声明");
                var execution=((System.Collections.IEnumerable)config.GetType().GetMethod("CaptureExecutionFields",Flags)!.Invoke(config,null)!).Cast<object>().Select(pair=>(string)Get(pair,"Key")).ToArray();Require(!execution.Intersect(displaySchema.Inputs.Select(port=>port.Id)).Any()&&config.ParameterBindingOrder.All(id=>!config.IsDisplayField(id)),"显示进入执行或参数写入顺序");
                var appearance=config.CaptureResultDisplay()!;Require(appearance.Graphic.Ok.Red==102&&appearance.Graphic.Width==2&&appearance.Text.Content=="Blob Area:{0}"&&appearance.Text.X==0&&appearance.Text.FontSize==10,"原feature默认错误");
                var other=RuntimeToolConfiguration.Create("ImageLoad");Require(other.DisplaySettingsSchema.Inputs.Count==0&&other.EditorSettingsSchema.Fingerprint==other.SettingsSchema.Fingerprint,"无显示provider工具行为改变");
            });
            Case("显示只在完整编辑集合接纳，算法WithSettings保持正式样式",()=>{
                var original=node.RuntimeConfiguration!;var changed=original.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),4d}});
                var algorithm=changed.WithSettings(new BlobSettings{Threshold=121});Require(algorithm.CaptureResultDisplay()!.Graphic.Width==4&&((BlobSettings)algorithm.CaptureSettings()).Threshold==121,"算法更新覆盖显示");
                var editor=new SunEyeVision.Plugin.Runtime.ToolSettingsEditSession(new BlobTool(),algorithm.Fields.Select(algorithm.EditorSettingsSchema.Inputs));Require(editor.Schema.Inputs.Count==83&&editor.Schema.Inputs.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal).SetEquals(algorithm.EditorSettingsSchema.Inputs.Select(port=>port.Id)),"编辑集合丢显示");
                var full=algorithm.WithSettingsFields(editor.CaptureValidatedFields());Require(full.Fields.Read(BlobTool.ImageInput) is ReferenceSetting&&full.CaptureResultDisplay()!.Graphic.Width==4,"编辑替换覆盖图像输入/样式");
                var rejected=false;try{_=algorithm.WithSettingsFields(algorithm.Fields.Select(algorithm.SettingsSchema.Inputs));}catch(ArgumentException error){Console.WriteLine("MISSING_DISPLAY_REJECTION "+error);rejected=error.Message.Contains("display.")||error.Message.Contains("required")||error.Message=="文本内容设置无效。";}Require(rejected&&algorithm.Fields.Read(Display(BlobResultDisplay.Text,"content")) is ConstantSetting {Value:"Blob Area:{0}"},"算法only快照误当完整编辑集合或损坏原owner");
                var widthPort=source.RuntimeConfiguration!.Schema.Outputs.Single(port=>port.Id=="output.image");var displayReference=new ReferenceSetting(new OutputReference(source.Id,widthPort.Id,widthPort.Type,new[]{"width"},scopeId:flowId,storage:widthPort.Storage));var positioned=changed.WithFieldSettings(new Dictionary<string,ParameterSetting>{{Display(BlobResultDisplay.Text,"x"),displayReference}});Require(positioned.Fields.Read(Display(BlobResultDisplay.Text,"x")) is ReferenceSetting&&positioned.ParameterBindingOrder.All(id=>!positioned.IsDisplayField(id)),"显示位置未保引用或进入算法顺序");
            });
            Case("保存与复制保显示，加载缺字段不补默认，非法参数仍拒绝",()=>{
                var config=node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),5d},{Display(BlobResultDisplay.Text,"content"),"A={0:F1}"}});
                var json=System.Text.Json.JsonSerializer.Serialize(config);var restored=System.Text.Json.JsonSerializer.Deserialize<RuntimeToolConfiguration>(json)!;Require(Equals(restored.CaptureResultDisplay(),config.CaptureResultDisplay())&&Equals(config.Copy().CaptureResultDisplay(),config.CaptureResultDisplay()),"复制/加载覆盖保存样式");
                var root=System.Text.Json.Nodes.JsonNode.Parse(json)!;root["Fields"]!.AsObject().Remove(Display(BlobResultDisplay.Graphic,"width"));var missing=false;try{_=System.Text.Json.JsonSerializer.Deserialize<RuntimeToolConfiguration>(root.ToJsonString());}catch(Exception error){missing=error.ToString().Contains("display.");}Require(missing,"重开静默补显示默认");
                var invalid=false;try{_=config.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"opacity"),1.1d}});}catch(ArgumentException){invalid=true;}Require(invalid,"非法显示透明度通过");
                invalid=false;try{_=config.WithFieldValues(new Dictionary<string,object?>{{FieldValue(nameof(BlobSettings.FilterAreaMin)),100L},{FieldValue(nameof(BlobSettings.FilterAreaMax)),10L}});}catch(ArgumentException){invalid=true;}Require(invalid,"显示合同绕过算法跨字段校验");
            });
            Case("显示失效签名仅豁免纯样式，混合算法和休眠输入仍失效",()=>{
                var edit=host.BeginEdit(flowId,node.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),3d}});var style=host.Commit(edit);Require(style.InvalidatedNodes.Count==0,"纯样式使执行结果失效");history.Undo();Pump();
                edit=host.BeginEdit(flowId,node.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),3d},{FieldValue(nameof(BlobSettings.Threshold)),120L}});var mixed=host.Commit(edit);Require(mixed.InvalidatedNodes.Any(id=>id.NodeId==node.Id),"算法+样式被错误豁免");history.Undo();Pump();
            });
            Case("受控面积格式和中文面积映射，未知token明确拒绝",()=>{
                BlobResultDisplay.ValidateContent(new string('a',40)+"{0:F2}");Require(string.Format(System.Globalization.CultureInfo.InvariantCulture,"A={0:F1}",25d)=="A=25.0","独立格式oracle错误");
                BlobResultDisplay.ValidateContent("Blob面积:{面积}");foreach(var raw in new[]{"{不存在的来源}","{1}","{0,1000000000}"}){var rejected=false;try{BlobResultDisplay.ValidateContent(raw);}catch(ArgumentException){rejected=true;}Require(rejected,"不支持内容被伪接纳："+raw);}
            });
        }
        if(scope is "All" or "Core")
        {
            Case("显示来源早Retain后装配、交叉raw租约与最终释放计数",()=>{
                var counts=new System.Collections.Concurrent.ConcurrentDictionary<string,int>();var owner=new ToolExecutionOwner();var run=Guid.NewGuid();
                DeclaredOutputSnapshot Make(string id,bool throws=false)
                {
                    counts[id+"-output"]=0;counts[id+"-resource"]=0;
                    var identity=owner.Begin("lifetime/"+id,run,FoundationContract.Major+"."+FoundationContract.Minor);
                    var output=new ScalarOutput(()=>{counts.AddOrUpdate(id+"-output",1,(_,count)=>count+1);if(throws)throw new InvalidOperationException("injected-output-"+id);});
                    return DeclaredOutputSnapshot.Capture(new ToolSchema(Array.Empty<Port>(),new[]{new Port("n",DataType.Number)}),0,identity,id,
                        new ToolOutcome(true,output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),new ActionLease(()=>counts.AddOrUpdate(id+"-resource",1,(_,count)=>count+1)),0,sourceWorkflowId:"lifetime");
                }
                var a=Make("A");var b=Make("B");var target=Make("target");var early=target.Retain();
                using(var set=new ResultDisplaySourceSet(new[]{a,b}))target.AttachDisplaySources(set);
                a.Dispose();b.Dispose();target.Dispose();Require(counts.Values.All(count=>count==0),"早Retain未保后装集合或过早释放");
                using(var reader=target.AcquireReader())Require(reader.DisplaySources is {} sources&&sources.TryGet(new("lifetime","A"),out var snapshot,out var values)&&values!.Read("n").GetScalar<double>()==1,"原owner释放后读者不能读取前驱");
                early.Dispose();Require(counts.Values.All(count=>count==1),"最终租约未各释放恰好一次");
                var first=Make("first");var second=Make("second");using(var set=new ResultDisplaySourceSet(new[]{second}))first.AttachDisplaySources(set);using(var set=new ResultDisplaySourceSet(new[]{first}))second.AttachDisplaySources(set);
                first.Dispose();second.Dispose();Require(counts.Where(pair=>pair.Key.StartsWith("first")||pair.Key.StartsWith("second")).All(pair=>pair.Value==1),"ValueReader间接保display形成交叉循环");
                var badA=Make("badA",true);var badB=Make("badB",true);var badTarget=Make("badTarget",true);
                using(var set=new ResultDisplaySourceSet(new[]{badA,badB}))badTarget.AttachDisplaySources(set);badA.Dispose();badB.Dispose();
                var failures=false;try{badTarget.Dispose();}catch(AggregateException error){failures=error.ToString().Contains("injected-output-badA")&&error.ToString().Contains("injected-output-badB")&&error.ToString().Contains("injected-output-badTarget");}
                Require(failures&&counts.Where(pair=>pair.Key.StartsWith("bad")).All(pair=>pair.Value==1),"异常清理漏来源或丢原始异常");
                var concurrentSource=Make("concurrent-source");var concurrentTarget=Make("concurrent-target");
                using(var set=new ResultDisplaySourceSet(new[]{concurrentSource}))
                {try{Task.WaitAll(Enumerable.Range(0,8).Select(_=>Task.Run(()=>concurrentTarget.AttachDisplaySources(set))).ToArray());}finally{concurrentSource.Dispose();concurrentTarget.Dispose();}}
                Require(counts.Where(pair=>pair.Key.StartsWith("concurrent-")).All(pair=>pair.Value==1),"并发重复装配泄漏或重复释放");
                var raceSource=Make("race-source");var raceTarget=Make("race-target");
                using(var set=new ResultDisplaySourceSet(new[]{raceSource}))using(var barrier=new Barrier(2))
                {try{var attach=Task.Run(()=>{barrier.SignalAndWait();try{raceTarget.AttachDisplaySources(set);}catch(ObjectDisposedException){}});var retire=Task.Run(()=>{barrier.SignalAndWait();raceTarget.Dispose();});Task.WaitAll(attach,retire);}finally{raceTarget.Dispose();raceSource.Dispose();}}
                Require(counts.Where(pair=>pair.Key.StartsWith("race-")).All(pair=>pair.Value==1),"装配/末次释放竞争泄漏或重复释放");
                var partial=Make("partial");IEnumerable<DeclaredOutputSnapshot> FailEnumeration(){yield return partial;throw new InvalidOperationException("injected-source-enumeration");}
                var partialRejected=false;try{using var ignored=new ResultDisplaySourceSet(FailEnumeration());}catch(InvalidOperationException error){partialRejected=error.Message=="injected-source-enumeration";}finally{partial.Dispose();}
                Require(partialRejected&&counts.Where(pair=>pair.Key.StartsWith("partial")).All(pair=>pair.Value==1),"来源集合中途失败泄漏已借读者");owner.Retire();
            });
        }        if(scope is "All" or "Core")
        {
            Case("首次显示准备保护历史、未登记草稿及失败重试",()=>{
                DocumentEditSession Create(Solution solution)=>(DocumentEditSession)Activator.CreateInstance(typeof(DocumentEditSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{solution},null)!;
                var solution=new Solution();var editing=Create(solution);var history=editing.History;var oldHost=editing.Host;
                var held=0;Func<IDisposable> gate=()=>{held++;return new ActionLease(()=>held--);};history.SetMutationLeaseProvider(gate);
                using(var preparation=editing.BeginInitialPresentationMaterialization())
                {
                    var hostBlocked=false;try{_=editing.Host;}catch(InvalidOperationException){hostBlocked=true;}
                    var historyBlocked=false;try{using var ignored=history.BeginCompositeMutation();}catch(InvalidOperationException){historyBlocked=true;}
                    var closeBlocked=false;try{editing.Close();}catch(InvalidOperationException){closeBlocked=true;}
                    var gateBlocked=false;try{history.ReleaseMutationLeaseProvider(gate);}catch(InvalidOperationException){gateBlocked=true;}
                    Require(hostBlocked&&historyBlocked&&closeBlocked&&gateBlocked&&held==1,"准备期间宿主、历史、执行门禁或关闭可以重入");
                }
                Require(held==0,"失败准备未释放执行门禁");
                var retired=false;try{oldHost.Capture();}catch(InvalidOperationException){retired=true;}
                Require(retired&&ReferenceEquals(history,editing.History)&&!ReferenceEquals(oldHost,editing.Host),"失败回滚未退役旧宿主或误替换历史");
                using(var retry=editing.BeginInitialPresentationMaterialization())retry.Complete();
                var used=false;try{using var ignored=editing.BeginInitialPresentationMaterialization();}catch(InvalidOperationException){used=true;}
                Require(used,"成功准备后仍允许再次物化");editing.Close();
                var draftSession=Create(new Solution());var draftHost=draftSession.Host;_ = draftHost.BeginVariableEdit();
                var draftBlocked=false;try{using var ignored=draftSession.BeginInitialPresentationMaterialization();}catch(InvalidOperationException){draftBlocked=true;}
                Require(draftBlocked&&draftHost.Capture() is not null,"未登记草稿被关闭或覆盖");draftSession.Close();
                var usedSolution=new Solution();var usedSession=Create(usedSolution);usedSolution.ExecutionOwner.Begin("test/node",Guid.NewGuid(),FoundationContract.Major+"."+FoundationContract.Minor);
                var runBlocked=false;try{using var ignored=usedSession.BeginInitialPresentationMaterialization();}catch(InvalidOperationException){runBlocked=true;}
                Require(runBlocked,"已运行的会话被当作首次加载");usedSession.Close();usedSolution.ExecutionOwner.Retire();
                var racedSolution=new Solution();var racedSession=Create(racedSolution);var racedHost=racedSession.Host;var racedHistory=racedSession.History;var racedHeld=0;
                racedHistory.SetMutationLeaseProvider(()=>{racedHeld++;racedSolution.ExecutionOwner.Begin("test/race",Guid.NewGuid(),FoundationContract.Major+"."+FoundationContract.Minor);return new ActionLease(()=>racedHeld--);});
                var racedBlocked=false;try{using var ignored=racedSession.BeginInitialPresentationMaterialization();}catch(InvalidOperationException){racedBlocked=true;}
                Require(racedBlocked&&racedHeld==0&&ReferenceEquals(racedHost,racedSession.Host),"取得门禁期间启动的运行未阻止重建或门禁泄漏");racedSession.Close();racedSolution.ExecutionOwner.Retire();
                var managerRoot=Path.Combine(folder,"presentation-guard-manager");var manager=new SolutionManager(managerRoot);
                var document=manager.CreateUntitledSolution();var sourcePath=Path.Combine(managerRoot,"before.solution");Require(manager.SaveCurrentDocument(sourcePath).FileWritten,"无法准备当前方案复制夹具");
                var originalSession=manager.Session!;var managerEditing=originalSession.Editing;_=managerEditing.Host;
                using(var preparation=managerEditing.BeginInitialPresentationMaterialization())
                {
                    var switchBlocked=false;try{manager.SetCurrentSolution(new Solution());}catch(InvalidOperationException){switchBlocked=true;}
                    var closeBlocked=false;try{manager.CloseSolution(false);}catch(InvalidOperationException){closeBlocked=true;}
                    var saveBlocked=false;try{manager.SaveCurrentDocument(Path.Combine(managerRoot,"unexpected.solution"));}catch(InvalidOperationException){saveBlocked=true;}
                    var copyBlocked=false;try{manager.CopySolution(sourcePath,"blocked",targetDirectory:managerRoot);}catch(InvalidOperationException){copyBlocked=true;}
                    Require(switchBlocked&&closeBlocked&&saveBlocked&&copyBlocked&&!File.Exists(Path.Combine(managerRoot,"blocked.solution"))&&ReferenceEquals(manager.Session,originalSession)&&ReferenceEquals(manager.CurrentSolution,document),"准备期间切换、关闭、保存或复制改变了当前方案");
                }
                _=document.ExecutionOwner.Begin("test/still-live",Guid.NewGuid(),FoundationContract.Major+"."+FoundationContract.Minor);
                manager.CloseSolution(false);
            });
            Case("文字只随实际来源节点域退役，不随新轮或无关节点失效",()=>{
                var owner=new ToolExecutionOwner();var run=Guid.NewGuid();
                var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("value",DataType.Integer)});
                DeclaredOutputSnapshot Make(string node,long value)=>DeclaredOutputSnapshot.Capture(schema,0,
                    owner.Begin("domain/"+node,run,FoundationContract.Major+"."+FoundationContract.Minor),node,
                    new ToolOutcome(true,new ValueOutput(new Dictionary<string,object?>{{"value",value}}),BusinessJudgment.Pass,Array.Empty<Diagnostic>()),
                    new ActionLease(()=>{}),0,sourceWorkflowId:"domain");
                using var source=Make("source",32);using var unrelated=Make("unrelated",7);using var target=Make("target",1);
                using(var set=new ResultDisplaySourceSet(new[]{source,unrelated}))target.AttachDisplaySources(set);
                var reference=new OutputReference("source","value",DataType.Integer,scopeId:"domain");
                var expression=new DisplayExpressionSetting("值:{数}",new[]{new DisplayExpressionSlot("数",reference)});
                using var lease=new ResultDisplayTextLease(target,new ResultTextAppearance(true,expression.Text,new(1,2,3),new(4,5,6),10,1,0,0,expression));
                var callbacks=0;lease.SetInvalidatedCallback(()=>System.Threading.Interlocked.Increment(ref callbacks));
                owner.Begin("domain/source",Guid.NewGuid(),FoundationContract.Major+"."+FoundationContract.Minor);
                owner.RetireNode("domain/unrelated");
                Require(lease.IsCurrent&&lease.Evaluate(0).Content=="值:32"&&callbacks==0,"来源新轮或无关节点退役破坏冻结值");
                owner.RetireNode("domain/source");
                Require(!lease.IsCurrent&&callbacks==1,"实际来源退役未使场景失效");
                owner.Begin("domain/source",Guid.NewGuid(),FoundationContract.Major+"."+FoundationContract.Minor);
                var rejected=false;try{lease.Evaluate(0);}catch(InvalidOperationException){rejected=true;}
                Require(rejected&&!lease.IsCurrent&&callbacks==1,"同身份重建复活旧来源域");
                lease.Dispose();owner.RetireNode("domain/source");Require(callbacks==1,"释放后旧回调仍触发");owner.Retire();
            });
            Case("内容解析区分声明null空数组与非法port索引及类型storage",()=>{
                var owner=new ToolExecutionOwner();var run=Guid.NewGuid();var outputs=new List<DeclaredOutputSnapshot>();
                DeclaredOutputSnapshot Make(string id,Port port,object? value,bool present=true)
                {
                    var identity=owner.Begin("typed-text/"+id,run,FoundationContract.Major+"."+FoundationContract.Minor);
                    var output=new ValueOutput(present?new Dictionary<string,object?>{{port.Id,value}}:new Dictionary<string,object?>());
                    var result=DeclaredOutputSnapshot.Capture(new ToolSchema(Array.Empty<Port>(),new[]{port}),0,identity,id,new ToolOutcome(true,output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),new ActionLease(()=>{}),0,sourceWorkflowId:"typed-text");outputs.Add(result);return result;
                }
                var nullable=Make("nullable",new("value",DataType.Number,Nullable:true),null);var scalar=Make("scalar",new("value",DataType.Number),2d);
                var empty=Make("empty",new("value",TypeRef.SequenceOf((TypeRef)DataType.Number)),Array.Empty<double>());
                var array=Make("array",new("value",TypeRef.SequenceOf((TypeRef)DataType.Number)),new[]{10d,20d});
                var missingIndex=Make("index",new("index",DataType.Integer,Required:false),null,false);var integer=Make("integer",new("value",DataType.Integer),3L);
                var validIndex=Make("valid-index",new("index",DataType.Integer),0L);var nullableArray=Make("nullable-array",new("value",TypeRef.SequenceOf((TypeRef)DataType.Number),Nullable:true),null);
                var indexArrayType=TypeRef.SequenceOf((TypeRef)DataType.Integer);var indexArray=Make("index-array",new("value",indexArrayType),new SequenceValue(indexArrayType,new object?[]{0L}));var missingMain=Make("missing-main",new("value",DataType.Number,Required:false),null,false);
                var target=Make("target",new("value",DataType.Number),1d);
                OutputReference Ref(DeclaredOutputSnapshot source)=>new(source.Identity.NodeId,source.Schema.Outputs[0].Id,source.Schema.Outputs[0].Type,scopeId:"typed-text",storage:source.Schema.Outputs[0].Storage);
                using(var sources=new ResultDisplaySourceSet(outputs.Where(output=>!ReferenceEquals(output,target))))target.AttachDisplaySources(sources);
                string Read(DisplayExpressionSetting expression,int row=0)
                {using var lease=new ResultDisplayTextLease(target,new ResultTextAppearance(true,expression.Text,new(1,2,3),new(4,5,6),10,1,0,0,expression));return lease.Evaluate(row).Content;}
                DisplayExpressionSetting Expression(string text,params DisplayExpressionSlot[] slots)=>new(text,slots);
                try
                {
                    Require(Read(Expression("A:{A};B:{B}",new DisplayExpressionSlot("A",Ref(nullable)),new DisplayExpressionSlot("B",Ref(scalar))))=="A:;B:2","合法null混合映射未保有值文本");
                    Require(Read(Expression("缺本轮值:{A}",new DisplayExpressionSlot("A",Ref(missingMain))))=="","已声明主来源缺本轮值未按内容空值处理");
                    Require(Read(Expression("全空:{A}",new DisplayExpressionSlot("A",Ref(nullable))))==""&&Read(Expression("空数组:{A}",new DisplayExpressionSlot("A",Ref(empty))))=="","全无值未返回空串");
                    Require(Read(Expression("A:{A};B:{B}",new DisplayExpressionSlot("A",Ref(empty)),new DisplayExpressionSlot("B",Ref(scalar))))=="A:;B:2","空数组混合映射错误");
                    Require(Read(Expression("{A}",new DisplayExpressionSlot("A",Ref(array))),3)=="10","本方已声明逐行短数组回首策略错误");
                    var nullWithIndex=new OutputReference("nullable-array","value",nullableArray.Schema.Outputs[0].Type,selection:new BindingSelection.AtField(Ref(validIndex)),scopeId:"typed-text");
                    var emptyWithIndex=new OutputReference("empty","value",empty.Schema.Outputs[0].Type,selection:new BindingSelection.AtField(Ref(validIndex)),scopeId:"typed-text");
                    Require(Read(Expression("{A}",new DisplayExpressionSlot("A",nullWithIndex)))==""&&Read(Expression("{A}",new DisplayExpressionSlot("A",emptyWithIndex)))=="","有效动态索引下主来源null/空数组被误判索引失败");
                    var badPort=new OutputReference("scalar","illegal",DataType.Number,scopeId:"typed-text");
                    var badIndex=new OutputReference("index","illegal-index",DataType.Integer,scopeId:"typed-text");
                    var dynamicMissing=new OutputReference("array","value",array.Schema.Outputs[0].Type,selection:new BindingSelection.AtField(Ref(missingIndex)),scopeId:"typed-text");
                    var nullWithMissingIndex=new OutputReference("nullable-array","value",nullableArray.Schema.Outputs[0].Type,selection:new BindingSelection.AtField(Ref(missingIndex)),scopeId:"typed-text");
                    var nestedIndex=new OutputReference("index-array","value",indexArrayType,selection:new BindingSelection.AtField(Ref(missingIndex)),scopeId:"typed-text");
                    var recursiveMissing=new OutputReference("array","value",array.Schema.Outputs[0].Type,selection:new BindingSelection.AtField(nestedIndex),scopeId:"typed-text");
                    var dynamicBad=new OutputReference("array","value",array.Schema.Outputs[0].Type,selection:new BindingSelection.AtField(badIndex),scopeId:"typed-text");
                    var outside=new OutputReference("array","value",array.Schema.Outputs[0].Type,selection:new BindingSelection.AtConstant(9),scopeId:"typed-text");
                    var wrongType=new OutputReference("integer","value",DataType.Number,scopeId:"typed-text");
                    var wrongStorage=new OutputReference("integer","value",DataType.Integer,scopeId:"typed-text",storage:NumericStorage.Int32);
                    foreach(var (name,reference,expectedCode) in new[]{("内容非法port",badPort,"missing-output"),("动态索引非法port",dynamicBad,"missing-output"),("动态索引无本轮值",dynamicMissing,"display-index-missing-value"),("主源null但索引缺值",nullWithMissingIndex,"display-index-missing-value"),("递归索引缺值",recursiveMissing,"display-index-display-index-missing-value"),("显式索引越界",outside,"index-range"),("源类型改变",wrongType,"schema-changed"),("storage改变",wrongStorage,"schema-changed")})
                    {var rejected=false;try{_=Read(Expression("{A}",new DisplayExpressionSlot("A",reference)));}catch(ArgumentException error){var code=Maybe(error,"Code")?.ToString();Console.WriteLine("TEXT_REJECT "+name+" CODE="+code+" "+error.Message);Require(code==expectedCode,name+"因无关诊断拒绝："+error);rejected=true;}Require(rejected,name+"被当普通空值吞掉");}
                }
                finally{var errors=new List<Exception>();foreach(var output in outputs)try{output.Dispose();}catch(Exception error){errors.Add(error);}owner.Retire();if(errors.Count!=0)throw new AggregateException(errors);}
            });
        }
        SunEyeVision.UI.Shared.WorkflowNode? textSourceB=null;
        if(scope=="TextSource")
        {
            textSourceB=vm.WorkflowTabViewModel.SelectedTab!.CreateNode("ImageLoad");
            var secondFile=Path.Combine(folder,"text-source-b.png");var secondPng=new PngBitmapEncoder();
            secondPng.Frames.Add(BitmapFrame.Create(BitmapSource.Create(32,64,96,96,PixelFormats.Gray8,null,new byte[32*64],32)));
            using(var file=File.Create(secondFile))secondPng.Save(file);
            var secondPolicy=ImageSourcePolicy.Read(textSourceB)!;secondPolicy.FilePaths=new(){secondFile};secondPolicy.CurrentIndex=0;secondPolicy.ForRunPaths=new(){secondFile};secondPolicy.ApplyTo(textSourceB);
            vm.AddNodeToWorkflow(textSourceB);
            Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=textSourceB.Id,TargetNodeId=source.Id}),"第二文本来源控制前驱连接失败");
        }
        Run();var path=Path.Combine(folder,"display.solution");Require(solutions.SaveCurrentDocument(path).FileWritten,"正式夹具保存失败");
        if(Environment.GetEnvironmentVariable("S4_NATIVE_EXPORT_ONLY")=="1")
        {
            VerifyNativeExportWhole(viewport,()=>Scene(),()=>Publication(),()=>Run(),path,
                ()=>node.RuntimeConfiguration!.Fields,()=>history.Position,
                ()=>SetFormal(Display(BlobResultDisplay.Text,"visible"),false));
            shell.Close();app.Shutdown();Console.WriteLine("原生完整图像导出："+_passed+"组通过；字体native保护与平台差异另记。");return 0;
        }
        if(scope is "All" or "Render")
        {
            Case("有序矩形角点投原IRectangle，退化与大坐标经两原绘制路径",()=>{
                var receipt=viewport.CurrentFrame!;var originalScene=Scene();using var layer=viewport.CreateGeometryLayer(30);long revision=0;
                Point2[] Corners(double x,double y,double w,double h,double angle)
                {
                    var ux=Math.Cos(angle)*w/2;var uy=Math.Sin(angle)*w/2;var vx=-Math.Sin(angle)*h/2;var vy=Math.Cos(angle)*h/2;
                    return new[]{new Point2(x-ux-vx,y-uy-vy),new Point2(x+ux-vx,y+uy-vy),new Point2(x+ux+vx,y+uy+vy),new Point2(x-ux+vx,y-uy+vy)};
                }
                foreach(var sample in new[]{(X:20d,Y:20d,W:8d,H:4d,A:.7),(X:20d,Y:20d,W:0d,H:6d,A:.3),(X:20d,Y:20d,W:6d,H:0d,A:.3),(X:20d,Y:20d,W:0d,H:0d,A:0d),(X:1e9,Y:1e9,W:20d,H:.04,A:-.7)})
                {
                    var corners=Corners(sample.X,sample.Y,sample.W,sample.H,sample.A);
                    Require(layer.Present(receipt,++revision,new[]{new NativeOverlay(receipt.Frame,new PolygonGeometry(receipt.Coordinates,corners),Colors.Lime,2,.5,RectangleCorners:true,IncludeMeasurements:true)},()=>viewport.CurrentFrame==receipt),"矩形角点被拒");
                    var shapes=(object[])Get(Get(layer,"_prepared"),"Items");Require(shapes.Length==1&&shapes[0] is NativeRectangle,"仍走多边形而非原矩形接口");
                    var rect=(NativeRectangle)shapes[0];var tolerance=1e-5;
                    Require(Math.Abs(rect.CenterPoint.X-sample.X)<tolerance&&Math.Abs(rect.CenterPoint.Y-sample.Y)<tolerance&&Math.Abs(rect.Width-sample.W)<tolerance&&Math.Abs(rect.Height-sample.H)<tolerance&&rect.StrokeThickness==2&&rect.Opacity==.5,"矩形参数或样式未保留");
                    Require(Math.Abs(Math.IEEERemainder(rect.Angle*Math.PI/180-sample.A,Math.PI))<1e-5,"矩形旋转角错误");
                    var high=NativeRectShape.GetRectGeometry(rect,64,64,1,1);Require(high is not null,"原高质量矩形路径拒绝退化");Require(high.IsHitTestVisible&&((ToolTip)high.ToolTip).Content is string tip&&tip==rect.ToolTip&&tip==$"CenterX:{rect.CenterPoint.X},CenterY:{rect.CenterPoint.Y} Width:{rect.Width} Height:{rect.Height} Angle:{rect.Angle}","原Tips格式/冻结参数/命中入口未贯通");
                    var highRotation=((TransformGroup)high.RenderTransform).Children.OfType<RotateTransform>().Single();
                    Require(highRotation.Angle==rect.Angle&&highRotation.CenterX==rect.CenterPoint.X&&highRotation.CenterY==rect.CenterPoint.Y,"原高质量绘制旋转或中心未消费");
                    var canvas=new NativeShapeCanvas();NativeRectShape.AddRectGeometry(rect,64,64,1,1,canvas);Require(VisualTreeHelper.GetChildrenCount(canvas)==1,"原低质量矩形路径未生成绘制对象");
                    var low=(System.Windows.Media.DrawingVisual)VisualTreeHelper.GetChild(canvas,0);
                    foreach(var rawCorner in new[]{new Point(rect.CenterPoint.X-rect.Width/2,rect.CenterPoint.Y-rect.Height/2),new Point(rect.CenterPoint.X+rect.Width/2,rect.CenterPoint.Y-rect.Height/2),new Point(rect.CenterPoint.X+rect.Width/2,rect.CenterPoint.Y+rect.Height/2),new Point(rect.CenterPoint.X-rect.Width/2,rect.CenterPoint.Y+rect.Height/2)})
                    {
                        var highPoint=high.RenderTransform.Transform(rawCorner);var lowPoint=low.Transform.Transform(rawCorner);
                        Require(corners.Any(point=>Math.Abs(point.X-highPoint.X)<tolerance&&Math.Abs(point.Y-highPoint.Y)<tolerance)&&Math.Abs(highPoint.X-lowPoint.X)<tolerance&&Math.Abs(highPoint.Y-lowPoint.Y)<tolerance,"原高低质量旋转后的角点不符实际来源");
                    }
                }
                var invalid=new[]{new Point2(1,1),new Point2(9,1),new Point2(10,5),new Point2(1,5)};
                var rejected=false;try{layer.Present(receipt,++revision,new[]{new NativeOverlay(receipt.Frame,new PolygonGeometry(receipt.Coordinates,invalid),Colors.Red,RectangleCorners:true)},()=>true);}catch(ArgumentException){rejected=true;}
                Require(rejected&&Maybe(layer,"_prepared") is null&&ReferenceEquals(Scene(),originalScene),"非法四点未拒绝或误清其他常驻场景");
                var ambiguous=new[]{new Point2(1e16,1e16),new Point2(1e16+100,1e16),new Point2(1e16+200,1e16),new Point2(1e16+100,1e16)};
                rejected=false;try{layer.Present(receipt,++revision,new[]{new NativeOverlay(receipt.Frame,new PolygonGeometry(receipt.Coordinates,ambiguous),Colors.Red,RectangleCorners:true)},()=>true);}catch(ArgumentException error){rejected=error.Message.Contains("精度");}
                Require(rejected&&ReferenceEquals(Scene(),originalScene),"精度不足的非矩形被静默接纳或误清其他场景");
                Require(layer.Present(receipt,++revision,new[]{new NativeOverlay(receipt.Frame,new PolygonGeometry(receipt.Coordinates,invalid),Colors.Red)},()=>true),"普通多边形被矩形约束误伤");
            });
            Case("同帧双图只在结果视口安装图文，切单图及旧场景释放不误清",()=>{
                var old=Scene();SelectInput(true);
                var choice=workspace.OutputItems.Cast<object>().Single(item=>Get(item,"Reference") is OutputReference reference&&reference.NodeId==node.Id&&reference.PortId==BlobSchema.Field(5));
                workspace.SelectedOutputItem=choice;Pump();var current=Scene();var inputPane=Get(presenter,"_input");
                Require(Maybe(inputPane,"Display") is null,"输入视口安装了结果图文");
                var inputViewport=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultViewport)Get(Get(presenter,"InputPane"),"Viewport");
                Require(inputViewport.CurrentFrame is {} inputReceipt&&inputReceipt.Frame==viewport.CurrentFrame!.Frame,"夹具未形成同物理帧双图");
                Require(Geometry(current).Length==2&&Words(current).Count()==2,"结果视口丢失同次图文");
                if(!ReferenceEquals(old,current)){((IDisposable)old).Dispose();Pump();Require(ReferenceEquals(Scene(),current),"旧场景释放清掉新结果");}
                SelectBlob();Require(Maybe(inputPane,"Display") is null&&Geometry(Scene()).Length==2,"切单图丢结果或复活输入图文");
            });
            Case("真实HALCON单像素细线旋转样本完整图文不被退化清空",()=>{
                var saved=node.RuntimeConfiguration!.Copy();var bytes=File.ReadAllBytes(imageFile);
                try
                {
                    var pixels=new byte[4096];pixels[3*64+3]=200;
                    for(var i=0;i<16;i++){pixels[12*64+5+i]=200;pixels[(5+i)*64+30]=200;}
                    for(var i=0;i<11;i++){pixels[(20+i)*64+45+i]=200;if(i<10)pixels[(20+i)*64+46+i]=200;}
                    for(var y=40;y<56;y++)for(var x=25;x<40;x++)if(Math.Abs(x-32)+Math.Abs(y-48)<=5)pixels[y*64+x]=200;
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,64,96,96,PixelFormats.Gray8,null,pixels,64)));using(var file=File.Create(imageFile))encoder.Save(file);
                    var edit=host.BeginEdit(flowId,node.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{FieldValue(nameof(BlobSettings.IsAreaFilterEnabled)),false},{FieldValue(nameof(BlobSettings.IsCircularityFilterEnabled)),false},{FieldValue(nameof(BlobSettings.IsRectangularityFilterEnabled)),false}});host.Commit(edit);Run();
                    using var values=Publication().Borrow();var rows=values.Values!.Read(BlobSchema.BlobsPort);var rectangles=Geometry(Scene()).OfType<NativeRectangle>().ToArray();
                    Require(rows.Count==5&&rectangles.Length==5&&Words(Scene()).Count()==5,"合法细线/单像素使场景丢项或整帧清空");
                    Require(Enumerable.Range(0,rows.Count).Select(i=>rows.GetElement(i).GetField("area").GetScalar<double>()).Order().SequenceEqual(new[]{1d,16d,16d,21d,61d}),"独立像素面积oracle不符");
                    var evidence=new List<object>();
                    for(var i=0;i<rows.Count;i++)
                    {
                        var points=rows.GetElement(i).GetField("rotated-bounds");var rect=rectangles[i];var angle=rect.Angle*Math.PI/180;var cs=Math.Cos(angle);var sn=Math.Sin(angle);
                        for(var j=0;j<4;j++){var p=((PointGeometry)points.GetElement(j).GetGeometry()).Point;var dx=p.X-rect.CenterPoint.X;var dy=p.Y-rect.CenterPoint.Y;Require(Math.Abs(Math.Abs(dx*cs+dy*sn)-rect.Width/2)<1e-6&&Math.Abs(Math.Abs(-dx*sn+dy*cs)-rect.Height/2)<1e-6,"真实HALCON角点与矩形投影不对应");}
                        evidence.Add(new{Area=rows.GetElement(i).GetField("area").GetScalar<double>(),rect.CenterPoint,rect.Width,rect.Height,rect.Angle});
                    }
                    File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"halcon-rectangle-projections.json"),System.Text.Json.JsonSerializer.Serialize(evidence));
                    Capture(viewport.View,"original-rectangle-thin-results.png");
                }
                finally{File.WriteAllBytes(imageFile,bytes);var restore=host.BeginEdit(flowId,node.Id);restore.Node.RuntimeConfiguration=saved;host.Commit(restore);Run();}
            });
        }
        if((scope is "All" or "Window")&&Environment.GetEnvironmentVariable("S4_BLOB_DISPLAY_CASE")!="多节点纯样式")
        {
            var publication=Publication();var originalScene=Scene();
            Case("同次两个真实Blob原矩形和Area文本进入原图层",()=>{
                using var values=publication.Borrow();var rows=values.Values!.Read(BlobSchema.BlobsPort);Require(rows.Count==2,"敏感图未产生两个真实对象");Require(Enumerable.Range(0,rows.Count).Select(i=>rows.GetElement(i).GetField("area").GetScalar<double>()).Order().SequenceEqual(new[]{25d,64d}),"真实区域面积不符独立像素oracle");
                Require((int)Get(Get(Get(originalScene,"Layer"),"_prepared"),"Count")==4&&Words(originalScene).Select(word=>(string)Get(word,"Content")).Order().SequenceEqual(new[]{"Blob Area:25","Blob Area:64"}),"未实际投影两个边框及两个Area文字");
                Console.WriteLine("Scene styles="+string.Join(";",Items(originalScene).Select(item=>item.GetType().FullName+"/stroke="+Maybe(item,"StrokeThickness"))));
                Require(Items(originalScene).Count(item=>Maybe(item,"StrokeThickness") is not null&&Maybe(item,"Texts") is null)==2,"未产生两条实际几何投影");
                Require(Items(originalScene).Where(item=>Maybe(item,"StrokeThickness") is not null&&Maybe(item,"Texts") is null).All(item=>(double)Get(item,"StrokeThickness")==2),"原边框样式未消费");
                var projected=Geometry(originalScene).OfType<NativeRectangle>().ToArray();
                Require(projected.Length==2&&projected.All(rect=>rect.ToolTip==$"CenterX:{rect.CenterPoint.X},CenterY:{rect.CenterPoint.Y} Width:{rect.Width} Height:{rect.Height} Angle:{rect.Angle}"),"同次各行Tips测量未对应实际矩形");
                viewport.View.UpdateLayout();Pump();
                var native=(SunEyeVision.Plugin.Wpf.Native.Display.ImageView.ImageViewBox)viewport.View;
                var batch=native.ShapeList!.OfType<Tuple<string,object>>().Single(item=>item.Item1=="IRectangle");
                Require(batch.Item2 is List<object> members&&members.Count==2&&members.All(item=>projected.Any(rect=>ReferenceEquals(rect,item))),"生产列表未走原Tuple或复制串换同次矩形");
                var canvas=Visual(viewport.View).OfType<NativeShapeCanvas>().Single(item=>VisualTreeHelper.GetChildrenCount(item)>0);
                foreach(var rect in projected)
                {
                    var angle=rect.Angle*Math.PI/180;var ratio=native.ControlToImageRadio;
                    var point=new Point((rect.CenterPoint.X+Math.Sin(angle)*rect.Height/2)*ratio,(rect.CenterPoint.Y-Math.Cos(angle)*rect.Height/2)*ratio);
                    var hit=VisualTreeHelper.HitTest(canvas,point);Require(hit is not null&&Equals(Maybe(hit.VisualHit,"Tag"),rect.ToolTip),"真实原画布描边命中未携带同次对象Tips");
                    canvas.MyCallback(hit!);var tip=(ToolTip)canvas.ToolTip;
                    var serviceType=typeof(ToolTip).Assembly.GetType("System.Windows.Controls.PopupControlService",true)!;
                    var service=serviceType.GetProperty("Current",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(null);
                    try{serviceType.GetMethod("ShowToolTip",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(service,new object[]{canvas,false});Pump();Require(tip.IsOpen&&ReferenceEquals(tip.PlacementTarget,canvas)&&Equals(tip.Content,rect.ToolTip),"原画布Tips经WPF服务的实际Popup绑定或默认目标未贯通");}
                    finally{serviceType.GetMethod("DismissToolTips",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(service,null);Pump();}
                }
                Require(!Visual(viewport.View).OfType<NativeRectShape>().Any(),"普通Blob结果仍走逐控件分支");Capture(viewport.View,"display-default.png");
            });
            var window=Open();var control=Control(window);var editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);window.UpdateLayout();Pump();
            DeclaredFieldsView Fields()=>(DeclaredFieldsView)((DeclaredSettingsView)Get(control.Content,"Editor")).Content;
            DeclaredFieldAccess Field(string group,string role)=>Fields().Fields.Single(field=>field.FieldId==Display(group,role));
            Case("原九行七文本同组装，七项缺生产值禁用且不伪功能",()=>{
                Require(Fields().Fields.Count(field=>field.FieldId.StartsWith("display.text.",StringComparison.Ordinal))==8,"文本眼睛和七角色缺失");
                var tabs=Visual(editor).OfType<TabControl>().Single();Require(tabs.Items.Cast<object>().Select(o=>(string)Get(o,"Header")).SequenceEqual(new[]{"基本参数","运行参数","结果显示"}),"原三页顺序改变");
                var page=((System.Collections.IDictionary)Get(Fields(),"_groups"))["结果显示"]!;
                var rows=((System.Collections.IEnumerable)Get(page,"Rows")).Cast<object>().ToArray();
                var groupOrder=rows.Select(row=>(string)Get(Get(row,"GroupName"),"NameA")).Distinct().ToArray();
                Console.WriteLine("Result groups="+string.Join(";",groupOrder));
                Require(groupOrder.SequenceEqual(new[]{"结果判定","图像显示","文本显示"}),"原结果页三组顺序改变");
                Console.WriteLine("Result row slots="+string.Join(";",rows.Select(row=>Get(Get(row,"GroupName"),"NameA")+":"+Maybe(Get(row,"View"),"DisplayNameA")+":"+Get(row,"View").GetType().FullName)));
                var unavailable=Get(control.Content,"_displayUi");var contents=((System.Collections.IEnumerable)Get(unavailable,"_contents")).Cast<object>().OfType<FrameworkElement>().ToArray();Require(contents.Length==4&&contents.All(view=>!view.IsEnabled&&view.ToolTip is not null),"无生产值显示项可误编辑");
                var actualRows=rows.Where(row=>Equals(Get(Get(row,"GroupName"),"NameA"),"图像显示")).Select(row=>Get(row,"View")).ToArray();
                var expectedRows=new object[]{Field(BlobResultDisplay.Graphic,"visible").View,contents[0],Field(BlobResultDisplay.Inspection,"visible").View,contents[1],contents[2],contents[3],Field(BlobResultDisplay.Text,"visible").View,Field(BlobResultDisplay.Mask,"visible").View,Field(BlobResultDisplay.Annulus,"visible").View};
                Require(actualRows.Length==9&&actualRows.Zip(expectedRows).All(pair=>ReferenceEquals(pair.First,pair.Second)),"原九行槽位顺序改变（含屏蔽→扇环）");
                Require((string)Get(Field(BlobResultDisplay.Graphic,"visible").View,"DisplayNameA")=="Blob"&&(string)Get(Field(BlobResultDisplay.Text,"visible").View,"DisplayNameA")=="文本"&&(bool)Get(Field(BlobResultDisplay.Text,"visible").View,"PanVisibleA")==false,"未复用原图形/文本眼睛模板");
                Console.WriteLine("Page="+editor.SelectedPageIndex+" tabs="+string.Join(";",Visual(editor).OfType<TabControl>().Select(t=>t.SelectedIndex+":"+string.Join(",",t.Items.Cast<object>().Select(o=>Maybe(o,"Header"))))));
                Console.WriteLine("XY visibility="+Field(BlobResultDisplay.Text,"x").View.IsVisible+" loaded="+Field(BlobResultDisplay.Text,"x").View.IsLoaded+" enabled="+Field(BlobResultDisplay.Text,"x").View.IsEnabled+" parent="+LogicalTreeHelper.GetParent(Field(BlobResultDisplay.Text,"x").View)?.GetType().FullName);Capture(window,"display-layout-diagnostic.png");
                Console.WriteLine("XY input="+Field(BlobResultDisplay.Text,"x").InputElement?.GetType().FullName+" view="+Field(BlobResultDisplay.Text,"x").View.GetType().FullName+" tree="+string.Join(";",Visual(Field(BlobResultDisplay.Text,"x").View).Select(e=>e.GetType().FullName)));
                Require(Visual(Field(BlobResultDisplay.Text,"x").View).OfType<SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox>().Any(),"固定位置未复用原同槽文本");
            });
            Case("有效草稿即时样式不运行，原眼睛双向且不改正式配置",()=>{
                var visible=Field(BlobResultDisplay.Graphic,"visible");var eye=Visual(visible.View).OfType<ToggleButton>().Single(item=>Equals(item.Tag,"NativeGraphicVisibility"));eye.SetCurrentValue(ToggleButton.IsCheckedProperty,false);eye.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();Console.WriteLine("Eye setting="+visible.Setting+" scene="+string.Join(";",Items(Scene()).Select(item=>item.GetType().FullName))+" words="+Words(Scene()).Count());Require(visible.Setting is ConstantSetting{Value:false}&&Words(Scene()).Count()==2&&Items(Scene()).All(item=>Maybe(item,"Texts") is not null),"原眼睛未驱动真实草稿/场景");
                Require(node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Visible&&ReferenceEquals(publication,Publication())&&publication.IsCurrent(),"预览改正式配置或新建运行结果");
                Require(visible.TrySetConstant(true),visible.Error);var width=Field(BlobResultDisplay.Graphic,"width");Require(width.TrySetConstant(4d),width.Error);Pump();Require(Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==4)&&node.RuntimeConfiguration.CaptureResultDisplay()!.Graphic.Width==2,"样式未消费或写正式");
            });
            Case("真实文本七字段完整投影，未知token拒绝且不半发布",()=>{
                var textFields=Fields().Fields.Where(field=>field.FieldId.StartsWith("display.text.",StringComparison.Ordinal)).ToArray();
                var baseline=textFields.ToDictionary(field=>field.FieldId,field=>((ConstantSetting)field.Setting).Value);
                var values=new Dictionary<string,object?>{{"content","A={0:F1}"},{"ok",PresentationDeclarations.Color(1,2,3)},{"ng",PresentationDeclarations.Color(4,5,6)},{"size",14d},{"opacity",.5d},{"x",7d},{"y",9d}};
                foreach(var pair in values)Require(Field(BlobResultDisplay.Text,pair.Key).TrySetConstant(pair.Value!),"文本字段未接纳："+pair.Key);Pump();
                var words=Words(Scene()).ToArray();Require(words.Length==2&&words.Select(word=>(string)Get(word,"Content")).Order().SequenceEqual(new[]{"A=25.0","A=64.0"})
                    &&words.All(word=>(int)Get(word,"FontSize")==14&&(Point)Get(word,"Position")==new Point(7,9)&&(double)Get(word,"Opacity")==.5&&(string)Get(word,"Stroke")=="#FF010203"),"文本七字段未进入真实原投影");
                Require(!Field(BlobResultDisplay.Text,"content").TrySetConstant("Blob面积:{不存在的来源}"),"未知中文token被伪接纳");Pump();Require(Words(Scene()).Select(word=>(string)Get(word,"Content")).Order().SequenceEqual(new[]{"A=25.0","A=64.0"}),"非法文本半发布");
                foreach(var field in textFields)Require(field.TrySetConstant(baseline[field.FieldId]!),field.Error);Pump();Require(publication.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Text.Content=="Blob Area:{0}","文本草稿改正式或运行");
            });
            Case("原样式Popup非法pending关闭重开保原文，恢复后完整捕获",()=>{
                var graphic=Field(BlobResultDisplay.Graphic,"visible").View;
                var popup=Visual(graphic).OfType<AppsPopup>().Single();popup.IsDropDownOpen=true;window.UpdateLayout();Pump();
                var part=(Popup)popup.Template.FindName("PART_Popup",popup)!;Require(part.IsOpen&&part.Child is FrameworkElement,"原样式弹层未打开");
                TextBox PendingText(){var numbers=(System.Collections.IDictionary)Get(graphic,"_numbers");var text=(TextBox)Get(numbers[Display(BlobResultDisplay.Graphic,"opacity")]!,"TextEditor");Require(Visual(part.Child!).Any(item=>ReferenceEquals(item,text))&&text.Name=="ValueText","待提交文本未绑定原透明度数值角色");return text;}
                var text=PendingText();var old=text.Text;text.SetCurrentValue(TextBox.TextProperty,"bad-opacity");Pump();
                Require(control.HasPendingEdits&&!control.TryCapture(out _),"非法原文未阻断真实宿主捕获");
                Require(node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Opacity==1&&publication.IsCurrent(),"非法原文写正式或执行");
                popup.IsDropDownOpen=false;Pump();popup.IsDropDownOpen=true;window.UpdateLayout();Pump();text=PendingText();Require(text.Text=="bad-opacity"&&!control.TryCapture(out _),"关闭重开清掉非法pending");
                text.SetCurrentValue(TextBox.TextProperty,old);Require(Field(BlobResultDisplay.Graphic,"opacity").TryCommitPending(),"显式原字段提交未恢复");popup.IsDropDownOpen=false;Pump();Require(control.TryCapture(out var error),error);
            });
            Case("真实Host确认后同publication保留，UndoRedo只刷新正式样式",()=>{
                var context=ToolEditorCommit.GetDraftContext(control)!;var args=new object?[]{null};Require((bool)context.GetType().GetMethod("TryCommitEditor",Flags)!.Invoke(context,args)!,"正式确认拒绝："+args[0]);Pump();
                Require(node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==4&&publication.IsCurrent()&&ReferenceEquals(publication,Publication()),"正式样式确认使结果失效");editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);Require(Field(BlobResultDisplay.Graphic,"width").TrySetConstant(6d),"同窗Apply新草稿不可编辑");Pump();Console.WriteLine("ApplyNext draft="+((ConstantSetting)Field(BlobResultDisplay.Graphic,"width").Setting).Value+" formal="+node.RuntimeConfiguration.CaptureResultDisplay()!.Graphic.Width+" scene="+string.Join(",",Geometry(Scene()).Select(item=>Get(item,"StrokeThickness"))));Require(Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==6),"同窗Apply新代未预览");window.Close();Pump();history.Undo();Pump();Console.WriteLine("StyleUndo formalWidth="+node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width+" publicationCurrent="+publication.IsCurrent()+" publicationSame="+ReferenceEquals(publication,Publication())+" history="+history.Position+" sceneWidths="+string.Join(",",Geometry(Scene()).Select(item=>Get(item,"StrokeThickness"))));Require(publication.IsCurrent()&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==2),"样式Undo失效publication或未更新");history.Redo();Pump();Require(publication.IsCurrent()&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==4),"样式Redo未刷新");
            });
            window=Open();control=Control(window);editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);window.UpdateLayout();Pump();
            Case("取消读最新正式值，不恢复开窗旧样式",()=>{
                var width=Field(BlobResultDisplay.Graphic,"width");Require(width.TrySetConstant(6d),width.Error);Pump();_=Scene();SetFormal(Display(BlobResultDisplay.Graphic,"width"),3d);window.Close();Pump();Require(publication.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==3&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==3),"取消覆盖较新的正式样式");
            });
            Case("无修改旧窗口token不覆盖打开期间UndoRedo后的正式样式",()=>{
                var prior=node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width;SetFormal(Display(BlobResultDisplay.Graphic,"width"),5d);
                window=Open();control=Control(window);editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);
                history.Undo();Pump();Console.WriteLine("OpenUndo formal="+node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width+" scene="+string.Join(",",Geometry(Scene()).Select(item=>Get(item,"StrokeThickness")))+" draft="+((ConstantSetting)Field(BlobResultDisplay.Graphic,"width").Setting).Value+" contextValid="+Get(ToolEditorCommit.GetDraftContext(control)!,"IsValid")+" pending="+control.HasPendingEdits);Require(publication.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==prior&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==prior),"旧窗口token覆盖Undo正式值");
                history.Redo();Pump();Require(publication.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==5&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==5),"开窗Redo未投影最新正式值");window.Close();Pump();history.Undo();Pump();
            });
            Case("有合法未确认草稿时正式历史变更退役旧token而非写回旧草稿",()=>{
                window=Open();control=Control(window);editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);
                var draftContext=ToolEditorCommit.GetDraftContext(control)!;Require(Field(BlobResultDisplay.Graphic,"width").TrySetConstant(6d),"未接纳未确认样式草稿");Pump();Require(Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==6),"合法草稿未预览");
                history.Redo();Pump();Require(publication.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==5&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==5),"旧未提交token覆盖正式历史");
                Require(Field(BlobResultDisplay.Graphic,"width").Setting is ConstantSetting{Value:6d},"历史刷新丢未确认草稿");var commitArguments=new object?[]{null};var commitAccepted=(bool)draftContext.GetType().GetMethod("TryCommitEditor")!.Invoke(draftContext,commitArguments)!;Console.WriteLine("Stale display commit="+commitAccepted+" error="+commitArguments[0]);Require(!commitAccepted&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==5,"旧草稿覆盖正式历史");window.Close();Pump();history.Undo();Pump();
            });
            Case("纯文字仍严核实际物理Frame与Domain，错图清整景",()=>{
                SetFormal(Display(BlobResultDisplay.Graphic,"visible"),false);Pump();Require(Words(Scene()).Count()==2,"纯文字未实际显示");SelectInput();Pump();Require(Maybe(outputPane,"Display") is null,"文字在不匹配上游坐标域上误显示");SelectBlob();Pump();Require(Words(Scene()).Count()==2,"选回真实Blob图像未恢复文字");SetFormal(Display(BlobResultDisplay.Graphic,"visible"),true);Pump();_=Scene();
            });
            Case("保存重开及复制读取同一正式显示字段，不重置样式",()=>{
                Require(solutions.SaveCurrentDocument(path).FileWritten,"显示保存失败");var copy=new SolutionRepository().LoadCandidate(path).Candidate!;try{var copied=copy.Workflows.Single(flow=>flow.Id==flowId).Nodes.Single(item=>item.Id==node.Id).RuntimeConfiguration!;Require(Equals(copied.CaptureResultDisplay(),node.RuntimeConfiguration!.CaptureResultDisplay()),"保存重开替换正式显示值");Require(Equals(node.Clone().RuntimeConfiguration!.CaptureResultDisplay(),copied.CaptureResultDisplay()),"节点复制丢显示");}finally{copy.ExecutionOwner.Retire();}
            });
            Case("失败BindDraft不发布新显示token，成功绑定真实新草稿后恢复",()=>{
                window=Open();control=Control(window);editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);
                var context=ToolEditorCommit.GetDraftContext(control)!;var scopeOwner=Get(vm,"_displayPreview");var oldToken=Get(scopeOwner,"_token");var invalid=node.Clone();invalid.RuntimeConfiguration=null;var failed=false;
                try{control.BindDraft(invalid);}catch(ArgumentException){failed=true;}Pump();Require(failed&&ReferenceEquals(Get(scopeOwner,"_token"),oldToken),"失败绑定发布新token");
                control.BindDraft((WorkflowNodeBase)Get(context,"DraftNode"));Pump();Require(!ReferenceEquals(Get(scopeOwner,"_token"),oldToken)&&Geometry(Scene()).Length==2,"成功真实草稿绑定未建立新代");window.Close();Pump();
            });
            Case("同schema同物理输入的另一节点不借旧节点publication样式",()=>{
                foreach(var existing in app.Windows.OfType<NativeRuntimeEditorWindow>().Where(item=>item.IsVisible).ToArray())existing.Close();Pump();
                var old=Scene();var before=((WorkflowContext)Get(vm,"CurrentWorkflowContext")).GetNodeResult(node.Id)?.Result;
                var second=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(second);
                Require(before is not null&&ReferenceEquals(before,((WorkflowContext)Get(vm,"CurrentWorkflowContext")).GetNodeResult(node.Id)?.Result)&&before.ExecutionIdentity?.IsCurrent==true&&!history.LastInvalidatedNodes.Any(item=>item.NodeId==node.Id),"新增无关节点错误退役原运行结果");
                vm.SelectedNode=second;Pump();var captured=typeof(MainWindowViewModel).GetMethod("CaptureCurrentResultDisplay",Flags)!.Invoke(vm,new object[]{publication});Require(captured is null&&Maybe(outputPane,"Display") is null,"另一节点借用旧publication");vm.SelectedNode=node;Pump();SelectBlob();_=Scene();Require(Maybe(old,"Disposed") is true,"旧节点scene未退役");
            });
            Case("同尺寸新publication换帧退役旧reader及场景",()=>{
                var old=Scene();var oldPublication=Publication();var oldFrame=viewport.CurrentFrame!.Frame;Run();var current=Scene();Require(!ReferenceEquals(oldPublication,Publication())&&!oldPublication.IsCurrent()&&(bool)Get(old,"Disposed")&&viewport.CurrentFrame!.Frame!=oldFrame&&!ReferenceEquals(old,current),"重跑复用旧运行权威/scene");Capture(viewport.View,"display-rerun.png");
            });
        }
        if(scope is "All" or "Window")
        {
            Case("多节点纯样式事务及UndoRedo保结果，算法改变与Undo不复活旧publication",()=>{
                void Phase(string phase)=>File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"display-progress.log"),DateTime.UtcNow.ToString("O")+" MULTI "+phase+Environment.NewLine);Phase("before-window-close");foreach(var existingWindow in app.Windows.OfType<NativeRuntimeEditorWindow>().ToArray())existingWindow.Close();Pump();Phase("after-window-close");var current=Publication();var peer=vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.FirstOrDefault(item=>item.Id!=node.Id&&item.RuntimeConfiguration?.ToolId==BlobTool.Id);if(peer is null){peer=vm.WorkflowTabViewModel.SelectedTab.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(peer);}
                Phase("peer-ready");var before=node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width;
                var first=host.BeginEdit(flowId,node.Id);var second=host.BeginEdit(flowId,peer.Id);
                first.Node.RuntimeConfiguration=first.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),7d}});
                second.Node.RuntimeConfiguration=second.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),8d}});
                Phase("before-style-commit");var committed=host.CommitBatch(new[]{first,second},null);Phase("after-style-commit");Pump();Require(committed.InvalidatedNodes.Count==0&&current.IsCurrent()&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==7),"多节点纯显示事务失效或未刷新");
                history.Undo();Pump();Require(current.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==before&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==before),"多节点显示Undo错误");history.Redo();Pump();Require(current.IsCurrent()&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==7),"多节点显示Redo错误");
                Phase("before-algorithm-window");var algorithmWindow=Open();var algorithmControl=Control(algorithmWindow);var algorithmEditor=(DeclaredSettingsView)Get(algorithmControl.Content,"Editor");
                var algorithmTabs=Visual(algorithmEditor).OfType<TabControl>().Single();algorithmTabs.SelectedItem=algorithmTabs.Items.Cast<TabItem>().Single(item=>Equals(item.Header,"运行参数"));algorithmWindow.UpdateLayout();Pump();
                var threshold=((DeclaredFieldsView)algorithmEditor.Content).Fields.Single(field=>field.FieldId==SettingId(nameof(BlobSettings.Threshold)));Require(threshold.TrySetConstant(201L),threshold.Error);Require(algorithmControl.TryCapture(out var captureError),captureError);
                var algorithmContext=ToolEditorCommit.GetDraftContext(algorithmControl)!;var commitArgs=new object?[]{null};Require((bool)algorithmContext.GetType().GetMethod("TryCommitEditor")!.Invoke(algorithmContext,commitArgs)!,commitArgs[0]?.ToString()??"真实算法确认失败");Pump();
                var actualResult=((WorkflowContext)Get(vm,"CurrentWorkflowContext")).GetNodeResult(node.Id)?.Result;Console.WriteLine("ProductionAlgorithm cachedResult="+(actualResult is null?"null":"present")+" cachedCurrent="+actualResult?.ExecutionIdentity?.IsCurrent+" publicationCurrent="+current.IsCurrent()+" pane="+(Maybe(outputPane,"Display") is null?"null":"present"));
                Require(!current.IsCurrent(),"正式算法确认后旧publication仍current");Require(Maybe(outputPane,"Display") is null,"正式算法确认后旧图层未撤");algorithmWindow.Close();Pump();
                history.Undo();Pump();Require(!current.IsCurrent()&&Maybe(outputPane,"Display") is null,"算法Undo复活旧运行结果");
            });
        }
        if(scope=="All")
        {
            // Window组创建的无输入临时节点已完成隔离断言；后续真实工作流重开必须移除它。
            foreach(var peer in vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.Where(item=>item.Id!=node.Id&&item.RuntimeConfiguration?.ToolId==BlobTool.Id).ToArray())DeleteThroughHost(peer.Id);
            vm.SelectedNode=node;node.IsSelected=true;Pump();
        }
        if(scope is "All" or "Roi")
        {
            ResultGeometrySnapshot Frozen() => ((IToolResultGeometryOutput)Get(Get(vm,"_resultPublicationSnapshot"),"Output")).ResultGeometry;
            RecordValue Rectangle(double x,double y,double width,double height)=>new(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","rectangle"},{"center",BlobLocalRoi.Point(x,y)},{"width",width},{"height",height},{"angle-degrees",0d}});
            RecordValue Ring(double start,double end,double inner=3)=>new(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","annulus"},{"center",BlobLocalRoi.Point(20,20)},{"inner-radius",inner},{"outer-radius",10d},{"start-radians",start},{"end-radians",end}});
            RecordValue Row(RecordValue shape,bool enabled=true)=>BlobLocalRoi.Item(Guid.NewGuid(),"同次几何",shape,enabled);
            void Roi(RecordValue inspection,RecordValue? mask=null){SetFormal(BlobLocalRoi.InputId,BlobLocalRoi.Root(inspection,mask??BlobLocalRoi.Group(BlobLocalRoi.None,Array.Empty<RecordValue>())));Run();}
            ResultGeometryGroup Group(ResultGeometryRole role)=>Frozen().Groups.Single(group=>group.Role==role);
            bool InspectionRectangle(object item)=>item is NativeRectangle rectangle&&Group(ResultGeometryRole.Inspection).Shapes.Where(shape=>shape.Get<string>("kind")=="rectangle").Any(shape=>{
                var center=shape.Get<PointGeometry>("center").Point;
                return Math.Abs(rectangle.CenterPoint.X-center.X)<1e-8&&Math.Abs(rectangle.CenterPoint.Y-center.Y)<1e-8
                    &&Math.Abs(rectangle.Width-shape.Get<double>("width"))<1e-8&&Math.Abs(rectangle.Height-shape.Get<double>("height"))<1e-8
                    &&Math.Abs(Math.IEEERemainder(rectangle.Angle-shape.Get<double>("angle-degrees"),360))<1e-8;
            });
            Case("几何快照深冻结容器且拒绝错误声明和非有限参数",()=>{
                Run(); // 上一组可能已退役旧结果；必须由真实工作流生成新快照。
                var shapes=new List<RecordValue>{BlobRoiDisplayProjection.Shape(Rectangle(9,9,14,14))};var groups=new List<ResultGeometryGroup>{new(ResultGeometryRole.Inspection,ResultGeometryCoverage.Explicit,shapes)};
                var copy=new ResultGeometrySnapshot(Frozen().SourceFrame,RoiDeclarations.ImageFrame,true,groups);shapes.Clear();groups.Clear();Require(copy.Groups.Count==1&&copy.Groups[0].Shapes.Count==1,"快照共享了可写容器");
                var rejected=false;try{_=new ResultGeometryGroup(ResultGeometryRole.Inspection,ResultGeometryCoverage.Explicit,new[]{Rectangle(9,9,14,14)});}catch(ArgumentException){rejected=true;}Require(rejected,"快照接纳私有/错误形状声明");
                rejected=false;try{var fields=copy.Groups[0].Shapes[0].CopyFields().ToDictionary(pair=>pair.Key,pair=>pair.Value);fields["angle-degrees"]=double.NaN;_=new ResultGeometryGroup(ResultGeometryRole.Inspection,ResultGeometryCoverage.Explicit,new[]{new RecordValue(RoiDeclarations.Shape,fields)});}catch(ArgumentException){rejected=true;}Require(rejected,"快照接纳非有限几何");
            });
            Case("同次全域与无区域不伪造框，空结果仍保覆盖身份",()=>{
                Roi(BlobLocalRoi.Group(BlobLocalRoi.FullDomain,Array.Empty<RecordValue>()));
                Require(Group(ResultGeometryRole.Inspection).Coverage==ResultGeometryCoverage.FullDomain&&Frozen().Groups.All(group=>group.Shapes.Count==0),"全域被伪造名义框");
                Roi(BlobLocalRoi.Group(BlobLocalRoi.None,Array.Empty<RecordValue>()));
                Require(Group(ResultGeometryRole.Inspection).Coverage==ResultGeometryCoverage.None&&Geometry(Scene()).Length==0&&!Words(Scene()).Any(),"无检测区域产生虚假结果或几何");
            });
            Case("实际矩形与disabled按本次冻结值展示，后改草稿不替换结果",()=>{
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(9,9,14,14)),Row(Rectangle(32,32,8,8),false)}));
                var snapshot=Frozen();Require(Group(ResultGeometryRole.Inspection).Shapes.Count==1,"禁用形状伪装本次采用");
                var rectangles=Geometry(Scene()).Where(InspectionRectangle).ToArray();Require(rectangles.Count(item=>(double)Get(item,"Width")==14&&(string)Get(item,"Stroke")=="#FF0000FF")==1,"真实检测框未进入原renderer");
                var window=Open();var control=Control(window);var draft=ToolEditorCommit.GetDraftContext(control)!;
                var draftNode=(WorkflowNodeBase)Get(draft,"DraftNode");draftNode.RuntimeConfiguration=draftNode.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{BlobLocalRoi.InputId,BlobLocalRoi.Empty}});Pump();
                Require(ReferenceEquals(snapshot,Frozen())&&Group(ResultGeometryRole.Inspection).Shapes.Count==1,"显示读了后改草稿");window.Close();Pump();
            });
            Case("多形屏蔽实际名义几何及原橙色Fill，零Blob不丢检测层",()=>{
                var polygon=new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","polygon"},{"vertices",new SequenceValue(BlobLocalRoi.ShapeType.Fields.Single(field=>field.Id=="vertices").Type,new object?[]{BlobLocalRoi.Point(1,1),BlobLocalRoi.Point(3,1),BlobLocalRoi.Point(2,3)})}});
                var sectorFields=Ring(0,Math.PI/2,0).CopyFields().ToDictionary(pair=>pair.Key,pair=>pair.Value);sectorFields["kind"]="sector";var sector=new RecordValue(BlobLocalRoi.ShapeType,sectorFields);
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(50,50,6,6))}),BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(polygon),Row(Rectangle(10,40,4,4)),Row(Ring(0,2*Math.PI)),Row(sector)}));
                Require(Group(ResultGeometryRole.Mask).Shapes.Count==4,"多形屏蔽被收窄");
                var items=Geometry(Scene());Require(items.Length==5&&!Words(Scene()).Any(),"零Blob时实际几何丢失或伪造结果文本");
                Require(items.Count(item=>Equals(Get(item,"Fill"),"#FFFF5400"))==4,"屏蔽填充未走原IShape.Fill");Capture(shell,"roi-multiple-mask-zero-result.png");
            });
            Case("检测多边形与扇形名义投影保实际kind，不伪造矩形",()=>{
                var triangle=new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","polygon"},{"vertices",new SequenceValue(BlobLocalRoi.ShapeType.Fields.Single(field=>field.Id=="vertices").Type,new object?[]{BlobLocalRoi.Point(5,5),BlobLocalRoi.Point(12,5),BlobLocalRoi.Point(5,12)})}});
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(triangle)}));
                var inspectionPolygon=Geometry(Scene()).OfType<SunEyeVision.Plugin.Wpf.Native.Display.Contracts.IPolygon>().Single();
                Require(Group(ResultGeometryRole.Inspection).Shapes.Single().Get<string>("kind")=="polygon"&&inspectionPolygon.Points.SequenceEqual(new[]{new Point(5,5),new Point(12,5),new Point(5,12)})&&inspectionPolygon.Stroke=="#FF0000FF","检测多边形顶点或原角色样式未保留");
                using(var values=Publication().Borrow())Require(Geometry(Scene()).OfType<NativeRectangle>().Count()==values.Values!.Read(BlobSchema.BlobsPort).Count,"Blob结果矩形与检测ROI混淆");
                var fields=Ring(0,Math.PI/2,0).CopyFields().ToDictionary(pair=>pair.Key,pair=>pair.Value);fields["kind"]="sector";
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(new RecordValue(BlobLocalRoi.ShapeType,fields))}));Require(Group(ResultGeometryRole.Annulus).Shapes.Single().Get<string>("kind")=="sector"&&Geometry(Scene()).Any(item=>item.GetType().Name=="Annular"),"扇形kind或原扇环renderer丢失");
            });
            Case("正负扇环及完整环复用原接口，内半径零与孔洞不混",()=>{
                foreach(var values in new[]{(Start:170d,End:150d,Inner:3d),(Start:150d,End:170d,Inner:3d),(Start:0d,End:360d,Inner:0d)})
                {
                    Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Ring(values.Start*Math.PI/180,values.End*Math.PI/180,values.Inner))}));
                    var ring=Geometry(Scene()).Single(item=>item.GetType().Name=="Annular");
                    Require(Math.Abs((double)Get(ring,"StartAngle")-Math.Min(values.Start,values.End))<1e-9&&Math.Abs((double)Get(ring,"AngleRange")-Math.Abs(values.End-values.Start))<1e-9&&(double)Get(ring,"InnerRadius")==values.Inner,"原扇环参数未保持同弧域");
                    Require(Group(ResultGeometryRole.Annulus).Shapes.Count==1&&Group(ResultGeometryRole.Inspection).Shapes.Count==0,"扇环被误画为矩形");
                    var original=SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.Shape.AnnularSectorShape.GetAnnularGeometry((SunEyeVision.Plugin.Wpf.Native.Display.Contracts.IAnnularSector)ring,64,64,1,1);
                    var field=original.GetType().BaseType!.GetField("geometry",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);var drawn=(System.Windows.Media.Geometry)field!.GetValue(original)!;
                    Point At(double radius,double angle)=>new(20+radius*Math.Cos(angle*Math.PI/180),20+radius*Math.Sin(angle*Math.PI/180));
                    if(values.End!=360)Require(drawn.FillContains(At(6,160))&&!drawn.FillContains(At(6,100))&&!drawn.FillContains(At(1,160)),"原renderer正负扇环实际弧域或孔洞不符");
                    else Require(drawn.FillContains(At(6,90))&&drawn.FillContains(At(1,180))&&!drawn.FillContains(At(11,90)),"原renderer整圆/零内半径实际区域不符");Capture(shell,"roi-ring-"+values.Start+"-"+values.End+".png");
                }
            });
            Case("三角色真实WPF眼睛样式确认取消UndoRedo只刷新不重跑",()=>{
                var polygon=new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","polygon"},{"vertices",new SequenceValue(BlobLocalRoi.ShapeType.Fields.Single(field=>field.Id=="vertices").Type,new object?[]{BlobLocalRoi.Point(1,40),BlobLocalRoi.Point(5,40),BlobLocalRoi.Point(2,44)})}});
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(9,9,14,14)),Row(Ring(0,Math.PI))}),BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(polygon)}));
                var publication=Publication();var snapshot=Frozen();
                object[] Members(ResultGeometryRole role)=>Geometry(Scene()).Where(item=>role switch {ResultGeometryRole.Inspection=>InspectionRectangle(item),ResultGeometryRole.Mask=>Equals(Maybe(item,"Fill"),"#FFFF5400"),_=>item.GetType().Name=="Annular"}).ToArray();
                var roles=new[]{(Role:ResultGeometryRole.Inspection,Id:BlobResultDisplay.Inspection),(Role:ResultGeometryRole.Mask,Id:BlobResultDisplay.Mask),(Role:ResultGeometryRole.Annulus,Id:BlobResultDisplay.Annulus)};
                for(var i=0;i<roles.Length;i++)
                {
                    var (role,id)=roles[i];var window=Open();var control=Control(window);var editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);window.UpdateLayout();Pump();
                    DeclaredFieldAccess Field(string name)=>((DeclaredFieldsView)editor.Content).Fields.Single(field=>field.FieldId==Display(id,name));
                    var eye=Visual(Field("visible").View).OfType<ToggleButton>().Single(button=>Equals(button.Tag,"NativeGraphicVisibility"));eye.SetCurrentValue(ToggleButton.IsCheckedProperty,false);eye.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
                    Require(Members(role).Length==0&&publication.IsCurrent()&&ReferenceEquals(snapshot,Frozen()),"原眼睛未隐藏对应角色或引发新执行："+role);
                    Require(Field("visible").TrySetConstant(true),Field("visible").Error);
                    var width=3d+i;var opacity=new[]{.4,.5,.6}[i];var ok=PresentationDeclarations.Color((byte)(11+i),(byte)(21+i),(byte)(31+i));var ng=PresentationDeclarations.Color((byte)(151+i),(byte)(161+i),(byte)(171+i));
                    Require(Field("width").TrySetConstant(width)&&Field("opacity").TrySetConstant(opacity)&&Field("ok").TrySetConstant(ok)&&Field("ng").TrySetConstant(ng),"真实样式字段提案被拒绝："+role);Pump();
                    var expected=System.Windows.Media.Color.FromRgb((byte)(11+i),(byte)(21+i),(byte)(31+i)).ToString();Require(Members(role).Length==1&&Members(role).All(item=>(double)Get(item,"StrokeThickness")==width&&Math.Abs((double)Get(item,"Opacity")-opacity)<1e-12&&Equals(Get(item,"Stroke"),expected)),"草稿样式未投影到实际角色："+role);
                    Require(control.TryCapture(out var error),error);var context=ToolEditorCommit.GetDraftContext(control)!;var args=new object?[]{null};Require((bool)context.GetType().GetMethod("TryCommitEditor")!.Invoke(context,args)!,args[0]?.ToString()??"真实确认失败");window.Close();Pump();
                    Require(publication.IsCurrent()&&ReferenceEquals(snapshot,Frozen()),"确认仅样式却替换结果："+role);history.Undo();Pump();Require(Members(role).Length==1&&Members(role).All(item=>(double)Get(item,"StrokeThickness")==2),"样式Undo未恢复对应角色："+role);history.Redo();Pump();Require(Members(role).All(item=>(double)Get(item,"StrokeThickness")==width&&Equals(Get(item,"Stroke"),expected)),"样式Redo未恢复对应角色："+role);
                    var cancel=Open();var cancelControl=Control(cancel);var cancelEditor=(DeclaredSettingsView)Get(cancelControl.Content,"Editor");SelectResultPage(cancelEditor);var cancelField=((DeclaredFieldsView)cancelEditor.Content).Fields.Single(field=>field.FieldId==Display(id,"width"));Require(cancelField.TrySetConstant(9d),cancelField.Error);Pump();Require(Members(role).All(item=>(double)Get(item,"StrokeThickness")==9),"取消前合法草稿未实际预览");cancel.Close();Pump();Require(Members(role).All(item=>(double)Get(item,"StrokeThickness")==width)&&publication.IsCurrent()&&ReferenceEquals(snapshot,Frozen()),"取消未恢复最新正式样式："+role);
                }
            });
            Case("三角色样式和实际区域保存重开执行，复制不丢名义几何定义",()=>{
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(9,9,14,14))}),BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(10,10,2,2))}));
                SetFormal(Display(BlobResultDisplay.Mask,"width"),4d);var before=node.RuntimeConfiguration!;var formalSnapshot=Frozen();
                Require(solutions.SaveCurrentDocument(path).FileWritten,"同次几何配置保存失败");var bytes=File.ReadAllBytes(path);
                var loaded=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("重开失败");var loadedFlow=loaded.Workflows.Single(f=>f.Id==flowId);var restored=loadedFlow.Nodes.Single(n=>n.Id==node.Id);
                Require(Equals(restored.RuntimeConfiguration!.CaptureResultDisplay(),before.CaptureResultDisplay())&&Equals(before.Copy().CaptureResultDisplay(),before.CaptureResultDisplay()),"三角色样式保存/复制丢失");
                var engine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);engine.RegisterWorkflow(loadedFlow);var ownHost=new NodeConfigurationEditHost(loaded);var published=WorkflowPublishedConfiguration.Capture(ownHost.Capture(),loaded,ownHost);
                using var globals=new WorkflowGlobalRunValues(loaded.Id,published.GlobalDefinitions,published.GlobalDefinitions.Select(v=>new KeyValuePair<string,object?>(v.Id,v.Value)));
                var executor=new WorkflowExecutionEngine(engine,new PluginManager(),SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);
                var task=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(loadedFlow.Id,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{source.Id,new ImageSourceRunInput(imageFile,1){LocalReadAdmission=_=>imageFile}}}){Configuration=published.Configuration,ExecutionOwner=published.ExecutionOwner,ProducedGlobals=globals,ParameterValues=published.ParameterValues,ResourcePaths=published.ResourcePaths});PumpUntil(()=>task.IsCompleted);
                using(var result=task.GetAwaiter().GetResult())
                {
                    Require(result.Success&&result.NodeResults[node.Id].Result is DeclaredOutputSnapshot,"真实重开执行失败："+System.Text.Json.JsonSerializer.Serialize(result.NodeResults.Select(pair=>new{Node=pair.Key,Result=pair.Value.Result?.ToString()})));
                    var output=(BlobOutput)((DeclaredOutputSnapshot)result.NodeResults[node.Id].Result!).Output!;
                    Require(output.ResultGeometry.Groups.Single(group=>group.Role==ResultGeometryRole.Inspection).Shapes.Count==1&&output.ResultGeometry.Groups.Single(group=>group.Role==ResultGeometryRole.Mask).Shapes.Count==1,"重开执行未携带本次实际检测/屏蔽");
                    Require(output.ResultGeometry.SourceFrame==output.Region.SourceFrame&&output.ResultGeometry.Coordinates==output.Region.Frame,"重开名义几何与实际结果资源帧不一致");
                    Require(output.TotalArea==55,"实际屏蔽未裁掉9像素或检测串入另一个Blob");
                }
                loaded.ExecutionOwner.Retire();var window=Open();window.Close();Pump();Require(bytes.SequenceEqual(File.ReadAllBytes(path))&&ReferenceEquals(formalSnapshot,Frozen()),"取消改写文件或替换实际几何");
            });
            Case("三角色总业务NG颜色来自同次状态，独立于逐Blob通过色",()=>{
                var polygon=new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","polygon"},{"vertices",new SequenceValue(BlobLocalRoi.ShapeType.Fields.Single(field=>field.Id=="vertices").Type,new object?[]{BlobLocalRoi.Point(1,40),BlobLocalRoi.Point(5,40),BlobLocalRoi.Point(2,44)})}});
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(9,9,14,14)),Row(Ring(0,Math.PI))}),BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(polygon)}));
                var draft=host.BeginEdit(flowId,node.Id);var settings=(BlobSettings)draft.Node.RuntimeConfiguration!.CaptureSettings();draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration.WithSettings(settings with {Judgments=settings.Judgments.Select(rule=>rule.Key=="blob-count"?rule with {Enabled=true,Minimum=99,Maximum=100}:rule).ToArray()});host.Commit(draft);Run();
                Require(!Frozen().Passed,"同次总判定未实际NG");
                var styles=new[]{(Role:ResultGeometryRole.Inspection,Style:node.RuntimeConfiguration!.CaptureResultDisplay()!.Inspection!),(Role:ResultGeometryRole.Mask,Style:node.RuntimeConfiguration!.CaptureResultDisplay()!.Mask!),(Role:ResultGeometryRole.Annulus,Style:node.RuntimeConfiguration!.CaptureResultDisplay()!.Annulus!)};
                foreach(var (role,style) in styles)
                {
                    var expected=System.Windows.Media.Color.FromArgb(style.Ng.Alpha,style.Ng.Red,style.Ng.Green,style.Ng.Blue).ToString();var members=Geometry(Scene()).Where(item=>role switch{ResultGeometryRole.Inspection=>InspectionRectangle(item),ResultGeometryRole.Mask=>Equals(Maybe(item,"Fill"),"#FFFF5400"),_=>item.GetType().Name=="Annular"}).ToArray();Require(members.Length==1&&members.All(item=>Equals(Get(item,"Stroke"),expected)),"总NG未投影对应角色色："+role);
                }
                using var values=Publication().Borrow();var rows=values.Values!.Read(BlobSchema.BlobsPort);Require(rows.Count>0&&Enumerable.Range(0,rows.Count).All(index=>rows.GetElement(index).GetField("judgment-passed").GetScalar<bool>()),"未形成总NG但逐Blob通过的敏感样本");history.Undo();Pump();Run();
            });
            Case("新几何同物理帧严格验证，错图与新publication退役旧整景",()=>{
                var old=Scene();var oldSnapshot=Frozen();SelectInput();Pump();Require(Maybe(outputPane,"Display") is null&&(bool)Get(old,"Disposed"),"不同坐标域上游图仍显示Blob结果几何或泄漏旧scene租约");SelectBlob();var restored=Scene();Require(!ReferenceEquals(old,restored)&&ReferenceEquals(oldSnapshot,Frozen()),"选回未恢复当前结果");
                Run();Require(!ReferenceEquals(restored,Scene())&&!ReferenceEquals(oldSnapshot,Frozen()),"新publication未退役旧场景");
            });
        }
        if(scope is "All" or "Text")
        {
            if(scope=="All")
            {
                SetFormal(BlobLocalRoi.InputId,BlobLocalRoi.Root(BlobLocalRoi.Group(BlobLocalRoi.FullDomain,Array.Empty<RecordValue>()),BlobLocalRoi.Group(BlobLocalRoi.None,Array.Empty<RecordValue>())));
                Run();
            }
            var textGroup=BlobResultDisplay.Text;var contentId=Display(textGroup,"content");var xId=Display(textGroup,"x");var yId=Display(textGroup,"y");
            var imagePort=source.RuntimeConfiguration!.Schema.Outputs.Single(port=>port.Id=="output.image");
            var widthSource=new OutputReference(source.Id,imagePort.Id,imagePort.Type,new[]{"width"},scopeId:flowId,storage:imagePort.Storage);
            void SetText(params (string Id,ParameterSetting Setting)[] fields)
            {var draft=host.BeginEdit(flowId,node.Id);draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithFieldSettings(fields.ToDictionary(pair=>pair.Id,pair=>pair.Setting));host.ValidateRuntimeFieldProposals(draft,fields.ToDictionary(pair=>pair.Id,pair=>pair.Setting));host.Commit(draft);Pump();}
            Case("文本显示同轮前驱及Self映射、X整数引用且不执行算法",()=>{
                var current=Publication();var snapshot=(DeclaredOutputSnapshot)Get(vm,"_resultPublicationSnapshot");
                using(var reader=snapshot.AcquireReader())Require(reader.DisplaySources is {} sources&&sources.TryGet(new(flowId,source.Id),out var captured,out var values)&&captured!.Identity.RunId==snapshot.Identity.RunId&&values!.Read(imagePort.Id).GetField("width").GetScalar<long>()==64,"日常正式运行未随结果保同轮图像来源");
                var expression=BlobResultDisplay.ParseContent("W:{图像宽度};Blob面积:{面积}",new[]{new DisplayExpressionSlot("图像宽度",widthSource)});
                SetText((contentId,expression),(xId,new ReferenceSetting(widthSource)),(yId,new ConstantSetting(7d)));
                var words=Words(Scene()).ToArray();Require(current.IsCurrent()&&words.Length==2,"仅显示改动失效publication或丢文字：current="+current.IsCurrent()+" words="+words.Length);
                Require(words.Select(word=>(string)Get(word,"Content")).Order().SequenceEqual(new[]{"W:64;Blob面积:25","W:64;Blob面积:64"}),"同轮前驱或中文Self表达式未实际消费");
                Require(words.All(word=>((Point)Get(word,"Position")).X==64&&((Point)Get(word,"Position")).Y==7),"显示阶段数字来源未控制真实文字位置");
            });
            Case("表达式深冻结与JSON重开、每个slot复制重映射不改原配置",()=>{
                var config=node.RuntimeConfiguration!;var expression=(DisplayExpressionSetting)config.Fields.Read(contentId);
                var json=System.Text.Json.JsonSerializer.Serialize(config);var restored=System.Text.Json.JsonSerializer.Deserialize<RuntimeToolConfiguration>(json)!;
                Require(((DisplayExpressionSetting)restored.Fields.Read(contentId)).SameExpression(expression),"表达式序列化丢失原文/引用/自体路径");
                var replacement=Guid.NewGuid().ToString("N");var remapped=config.MapReferences(reference=>reference.NodeId==source.Id?new OutputReference(replacement,reference.PortId,reference.Type,reference.MemberFieldPath,reference.Selection,reference.ScopeId,reference.Storage):reference);
                Require(((DisplayExpressionSetting)remapped.Fields.Read(contentId)).Slots.Where(slot=>slot.Reference is not null).All(slot=>slot.Reference!.NodeId==replacement)
                    &&((ReferenceSetting)remapped.Fields.Read(xId)).Reference.NodeId==replacement&&((DisplayExpressionSetting)config.Fields.Read(contentId)).SameExpression(expression),"嵌套复制映射漏slot或污染原owner");
                Require(solutions.SaveCurrentDocument(path).FileWritten,"表达式正式方案保存失败");var reopened=new SolutionRepository().LoadCandidate(path).Candidate!;
                try{var loaded=reopened.Workflows.Single(flow=>flow.Id==flowId).Nodes.Single(item=>item.Id==node.Id).RuntimeConfiguration!;Require(((DisplayExpressionSetting)loaded.Fields.Read(contentId)).SameExpression(expression)&&BindingReferenceSerializer.Serialize(((ReferenceSetting)loaded.Fields.Read(xId)).Reference)==BindingReferenceSerializer.Serialize(widthSource),"方案重开丢失文本映射或X身份");}finally{reopened.ExecutionOwner.Retire();}
            });
            Case("重复映射按原顺序替换，空字面量与非法来源原子拒绝",()=>{
                SetText((contentId,BlobResultDisplay.ParseContent("{面积}|{面积}")),(xId,new ConstantSetting(0d)),(yId,new ConstantSetting(0d)));
                Require(Words(Scene()).Select(word=>(string)Get(word,"Content")).Order().SequenceEqual(new[]{"25|25","64|64"}),"重复映射未依原source重复替换");
                SetText((contentId,new ConstantSetting("literal-only")));Require(Words(Scene()).Count()==2&&Words(Scene()).All(word=>Equals(Get(word,"Content"),"literal-only")),"不引用面积的纯文字未实际显示");
                SetText((contentId,new ConstantSetting("")));Require(Geometry(Scene()).Length==2&&Words(Scene()).All(word=>Equals(Get(word,"Content"),"")),"空文字误清Blob几何或仍显示旧文字");
                SetText((contentId,BlobResultDisplay.ParseContent("{面积}|{面积}")));
                var before=node.RuntimeConfiguration!;var bad=new OutputReference(source.Id,"不存在的端口",DataType.Number,scopeId:flowId);
                var draft=host.BeginEdit(flowId,node.Id);var rejected=false;try{host.ValidateRuntimeFieldProposals(draft,new Dictionary<string,ParameterSetting>{{xId,new ReferenceSetting(bad)}});}catch(ArgumentException error){rejected=error.Message.Contains("source",StringComparison.OrdinalIgnoreCase)||error.Message.Contains("field",StringComparison.OrdinalIgnoreCase)||error.Message.Contains("来源");}
                Require(rejected&&ReferenceEquals(node.RuntimeConfiguration,before),"非法新来源误接纳或写正式配置");
            });
        }        if(scope is "All" or "Text")
        {
            Case("真实七行文本控件与位置来源选择、非法原文、确认取消和UndoRedo",()=>{
                var contentId=Display(BlobResultDisplay.Text,"content");var xId=Display(BlobResultDisplay.Text,"x");
                var imagePort=source.RuntimeConfiguration!.Schema.Outputs.Single(port=>port.Id=="output.image");
                var widthSource=new OutputReference(source.Id,imagePort.Id,imagePort.Type,new[]{"width"},scopeId:flowId,storage:imagePort.Storage);
                var formal=System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration!);var publication=Publication();var bytes=File.ReadAllBytes(path);
                var window=Open();var control=Control(window);var editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);
                DeclaredFieldAccess Field(string id)=>((DeclaredFieldsView)editor.Content).Fields.Single(field=>field.FieldId==id);
                var text=Field(contentId);Require(text.TrySetConstant("Blob面积:{面积}"),text.Error);Pump();
                Require(text.Setting is DisplayExpressionSetting&&Words(Scene()).Select(word=>(string)Get(word,"Content")).Order().SequenceEqual(new[]{"Blob面积:25","Blob面积:64"}),"原文本控件确认未生成映射或未进入真实场景");
                var position=(DeclaredFieldEditor)Field(xId).View;
                Console.WriteLine("TEXT_POSITION_ROUTE "+System.Text.Json.JsonSerializer.Serialize(new{inline=(bool)Get(position,"_inlineConstantReference"),view=position.GetType().FullName,popups=Visual(position).OfType<AppsPopup>().Count()}));Require((bool)Get(position,"_inlineConstantReference"),"文本坐标未走原ButtonSelecter内联常量/来源组合");
                SelectSource(position,widthSource);Pump();
                Require(Field(xId).Setting is ReferenceSetting reference&&BindingReferenceSerializer.Serialize(reference.Reference)==BindingReferenceSerializer.Serialize(widthSource),"真实位置来源按钮未接纳完整引用身份");
                Require(Words(Scene()).All(word=>((Point)Get(word,"Position")).X==64),"真实位置草稿未进入当前publication文字");
                Require(!text.TrySetConstant("{无此来源}"),"非法手输表达式误接纳");Require(!control.TryCapture(out var error)&&error.Length>0,"非法原文未阻整窗捕获");
                Require(text.TrySetConstant("Blob面积:{面积}"),text.Error);Require(control.TryCapture(out error),error);
                window.Close();Pump();Require(System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration!)==formal&&File.ReadAllBytes(path).SequenceEqual(bytes)&&publication.IsCurrent(),"关闭取消写正式配置/文件或运行结果");
                window=Open();control=Control(window);editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);
                var accepted=((DeclaredFieldsView)editor.Content).Fields.Single(field=>field.FieldId==contentId);Require(accepted.TrySetConstant("已确认:{面积}"),accepted.Error);Require(control.TryCommitEditor(out error),error);window.Close();Pump();
                Require(node.RuntimeConfiguration!.Fields.Read(contentId) is DisplayExpressionSetting saved&&saved.Text=="已确认:{面积}"&&publication.IsCurrent(),"真实确认丢映射或触发算法");
                history.Undo();Pump();Require(System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration!)==formal&&publication.IsCurrent(),"文本确认Undo未恢复正式配置或失效结果");history.Redo();Pump();Require(((DisplayExpressionSetting)node.RuntimeConfiguration!.Fields.Read(contentId)).Text=="已确认:{面积}"&&publication.IsCurrent(),"文本确认Redo丢映射或失效结果");
            });
        }        if(scope is "All" or "Text")
        {
            Case("两真实外源整片复制映射及删除A保B和Undo恢复",()=>{
                var other=vm.WorkflowTabViewModel.SelectedTab!.CreateNode("ImageLoad");var otherFile=Path.Combine(folder,"other-input.png");var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(32,48,96,96,PixelFormats.Gray8,null,new byte[32*48],32)));using(var file=File.Create(otherFile))encoder.Save(file);
                var policy=ImageSourcePolicy.Read(other)!;policy.FilePaths=new(){otherFile};policy.CurrentIndex=0;policy.ForRunPaths=new(){otherFile};policy.ApplyTo(other);vm.AddNodeToWorkflow(other);
                Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=other.Id,TargetNodeId=source.Id}),"第二图像来源的真实前驱控制链失败");Run();
                OutputReference Width(WorkflowNodeBase item){var port=item.RuntimeConfiguration!.Schema.Outputs.Single(port=>port.Id=="output.image");return new(item.Id,port.Id,port.Type,new[]{"width"},scopeId:flowId,storage:port.Storage);}
                var a=Width(source);var b=Width(other);var contentId=Display(BlobResultDisplay.Text,"content");
                var edit=host.BeginEdit(flowId,node.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{contentId,BlobResultDisplay.ParseContent("A:{A};B:{B}",new[]{new DisplayExpressionSlot("A",a),new DisplayExpressionSlot("B",b)})}});host.ValidateRuntimeFieldProposals(edit,new Dictionary<string,ParameterSetting>{{contentId,edit.Node.RuntimeConfiguration.Fields.Read(contentId)}});host.Commit(edit);Pump();
                Require(Words(Scene()).Count()==2&&Words(Scene()).All(word=>Equals(Get(word,"Content"),"A:64;B:32")),"两个真实前驱来源未独立消费");
                var currentFlow=solutions.CurrentSolution!.Workflows.Single(item=>item.Id==flowId);var oldIds=currentFlow.Nodes.Select(item=>item.Id).ToHashSet(StringComparer.Ordinal);
                foreach(var item in vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes)item.IsSelected=false;source.IsSelected=true;node.IsSelected=true;vm.SelectedNode=node;
                Require(vm.CopyCanvasSelection()&&vm.PasteCanvasSelection(new Point(700,400)),"真实主窗整片复制/粘贴入口拒绝");Pump();
                var copies=vm.WorkflowTabViewModel.SelectedTab.WorkflowNodes.Where(item=>!oldIds.Contains(item.Id)).ToArray();
                var copiedTarget=copies.Single(item=>item.ToolType==BlobTool.Id);var copiedSource=copies.Single(item=>item.ToolType=="ImageLoad");
                var copiedExpression=(DisplayExpressionSetting)copiedTarget.RuntimeConfiguration!.Fields.Read(contentId);Require(copiedExpression.Slots.Single(slot=>slot.Token=="A").Reference!.NodeId==copiedSource.Id&&copiedExpression.Slots.Single(slot=>slot.Token=="B").Reference!.NodeId==other.Id,"主窗整片复制漏映射A或误映射未选B");
                var copiedFile=Path.Combine(folder,"copied-input.png");var copiedPixels=new byte[40*64];var originalPixels=Pixels();for(var row=0;row<64;row++)Array.Copy(originalPixels,row*64,copiedPixels,row*40,40);var copiedEncoder=new PngBitmapEncoder();copiedEncoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(40,64,96,96,PixelFormats.Gray8,null,copiedPixels,40)));using(var file=File.Create(copiedFile))copiedEncoder.Save(file);
                var sourceDraft=host.BeginEdit(flowId,copiedSource.Id);var copiedPolicy=ImageSourcePolicy.Read(sourceDraft.Node)!;copiedPolicy.FilePaths=new(){copiedFile};copiedPolicy.CurrentIndex=0;copiedPolicy.ForRunPaths=new(){copiedFile};copiedPolicy.ApplyTo(sourceDraft.Node);host.Commit(sourceDraft);Pump();                node.IsSelected=false;vm.SelectedNode=copiedTarget;copiedTarget.IsSelected=true;Pump();var copyRun=vm.RunSelectedImageAsync();PumpUntil(()=>copyRun.IsCompleted);copyRun.GetAwaiter().GetResult();Pump();
                workspace.SelectedOutputItem=workspace.OutputItems.Cast<object>().Single(item=>Get(item,"Reference") is OutputReference reference&&reference.NodeId==copiedTarget.Id&&reference.PortId==BlobSchema.Field(5));Pump();Require(Words(Scene()).Count()==2&&Words(Scene()).All(word=>Equals(Get(word,"Content"),"A:40;B:32")),"复制后的真实执行/场景未消费映射来源");
                copiedTarget.IsSelected=false;vm.SelectedNode=node;node.IsSelected=true;Pump();SelectBlob();var beforeDelete=Publication();var targetSnapshot=(DeclaredOutputSnapshot)Get(vm,"_resultPublicationSnapshot");using var targetLease=targetSnapshot.Retain();
                var formalStamp=System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration!);var normalContext=(WorkflowContext)Get(vm,"CurrentWorkflowContext");var sourceAIdentity=normalContext.GetNodeResult(source.Id)!.Result!.Identity;var sourceBIdentity=normalContext.GetNodeResult(other.Id)!.Result!.Identity;
                var debugWindow=Open();PrivateDebug(debugWindow,true,(result,invocation)=>{
                    Require(result.Identity.DocumentSessionId!=sourceAIdentity.DocumentSessionId&&invocation.DisplaySourceIdentities.Contains(sourceAIdentity)&&invocation.DisplaySourceIdentities.Contains(sourceBIdentity)&&!invocation.SourceIdentities.Contains(sourceBIdentity),"私有显示专用来源身份未与算法来源区分");
                    using var reader=result.AcquireReader();Require(reader.DisplaySources is {} sources&&sources.TryGet(new(flowId,source.Id),out var frozenA,out var aValues)&&sources.TryGet(new(flowId,other.Id),out var frozenB,out var bValues)&&frozenA!.Identity==sourceAIdentity&&frozenB!.Identity==sourceBIdentity&&aValues!.Read("output.image").GetField("width").GetScalar<long>()==64&&bValues!.Read("output.image").GetField("width").GetScalar<long>()==32,"Execute返回释放算法readers后显示集合失效或读latest");
                    Require(((DeclaredOutputSnapshot)Get(vm,"_resultPublicationSnapshot")).Identity==result.Identity&&Words(Scene()).Count()==2&&Words(Scene()).All(word=>Equals(Get(word,"Content"),"A:64;B:32")),"真实私有调试未进入同身份场景");
                });debugWindow.Close();Pump();Require(System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration!)==formalStamp&&normalContext.GetNodeResult(source.Id)!.Result!.Identity==sourceAIdentity&&normalContext.GetNodeResult(other.Id)!.Result!.Identity==sourceBIdentity,"私有调试写正式配置或隐式重跑来源");SelectBlob();                var before=(DisplayExpressionSetting)node.RuntimeConfiguration!.Fields.Read(contentId);currentFlow=solutions.CurrentSolution!.Workflows.Single(item=>item.Id==flowId);
                Require(targetSnapshot.ExecutionIdentity?.IsCurrent==true,"删除前目标实际运行已失效");DeleteThroughHost(source.Id);
                var missing=(DisplayExpressionSetting)node.RuntimeConfiguration!.Fields.Read(contentId);Require(missing.SameExpression(before)&&missing.Slots.Single(slot=>slot.Token=="B").Reference!.NodeId==other.Id,"删除A清掉文本映射或误写B");Require(!beforeDelete.IsCurrent()&&targetSnapshot.ExecutionIdentity?.IsCurrent==false,"真实上游控制/图像链删除后旧publication或实际运行仍有效");
                vm.SelectedNode=node;node.IsSelected=true;Pump();var invalidWindow=Open();PrivateDebug(invalidWindow,false);invalidWindow.Close();Pump();
                var beforeIndependent=((WorkflowContext)Get(vm,"CurrentWorkflowContext")).GetNodeResult(copiedTarget.Id)?.Result?.Identity;var invalidRun=vm.RunSelectedImageAsync();PumpUntil(()=>invalidRun.IsCompleted);invalidRun.GetAwaiter().GetResult();Pump();var invalidContext=(WorkflowContext)Get(vm,"CurrentWorkflowContext");Require(invalidContext.GetNodeResult(node.Id)?.Result?.IsSuccess!=true&&invalidContext.GetNodeResult(copiedTarget.Id)?.Result?.IsSuccess==true&&invalidContext.GetNodeResult(copiedTarget.Id)!.Result!.Identity!=beforeIndependent&&targetSnapshot.ExecutionIdentity?.IsCurrent==false,"缺源消费者执行了旧值、独立节点未运行或旧结果复活");                history.Undo();Pump();Require(((DisplayExpressionSetting)node.RuntimeConfiguration!.Fields.Read(contentId)).SameExpression(before)&&!beforeDelete.IsCurrent()&&targetSnapshot.ExecutionIdentity?.IsCurrent==false,"撤销删除丢映射或复活旧运行");vm.SelectedNode=node;node.IsSelected=true;Pump();Run();Require(Words(Scene()).Count()==2&&Words(Scene()).All(word=>Equals(Get(word,"Content"),"A:64;B:32")),"删除Undo后的真实新运行未恢复两来源");
                history.Redo();Pump();Require(((DisplayExpressionSetting)node.RuntimeConfiguration!.Fields.Read(contentId)).SameExpression(before),"自动图像源删除Redo丢显示引用");vm.SelectedNode=node;node.IsSelected=true;Pump();var redoRun=vm.RunSelectedImageAsync();PumpUntil(()=>redoRun.IsCompleted);redoRun.GetAwaiter().GetResult();Pump();Require(((WorkflowContext)Get(vm,"CurrentWorkflowContext")).GetNodeResult(node.Id)?.Result?.IsSuccess!=true,"自动图像源删除Redo后消费者仍执行");history.Undo();Pump();vm.SelectedNode=node;node.IsSelected=true;Pump();Run();
                var threshold=FieldValue(nameof(BlobSettings.Threshold));var manual=new ReferenceSetting(b);var manualDraft=host.BeginEdit(flowId,node.Id);manualDraft.Node.RuntimeConfiguration=manualDraft.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{threshold,manual}});host.ValidateRuntimeFieldProposals(manualDraft,new Dictionary<string,ParameterSetting>{{threshold,manual}});host.Commit(manualDraft);Pump();Run();Require(((BlobOutput)((DeclaredOutputSnapshot)Get(vm,"_resultPublicationSnapshot")).Output!).ThresholdMinUsed==32,"手动算法来源B未实际消费32");
                var manualIdentity=BindingReferenceSerializer.Serialize(((ReferenceSetting)node.RuntimeConfiguration!.Fields.Read(threshold)).Reference);var expressionBefore=(DisplayExpressionSetting)node.RuntimeConfiguration.Fields.Read(contentId);
                void CheckManualMissing()
                {
                    Require(BindingReferenceSerializer.Serialize(((ReferenceSetting)node.RuntimeConfiguration!.Fields.Read(threshold)).Reference)==manualIdentity&&((DisplayExpressionSetting)node.RuntimeConfiguration.Fields.Read(contentId)).SameExpression(expressionBefore)&&node.RuntimeConfiguration.ReadCurrentParameter(threshold) is {Value:32L},"手动缺源清关系、改另一slot或篡改已接纳值");
                    vm.SelectedNode=node;node.IsSelected=true;Pump();var missingWindow=Open();PrivateDebug(missingWindow,false);missingWindow.Close();Pump();
                    var prior=((WorkflowContext)Get(vm,"CurrentWorkflowContext")).GetNodeResult(copiedTarget.Id)?.Result?.Identity;var run=vm.RunSelectedImageAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();Pump();var context=(WorkflowContext)Get(vm,"CurrentWorkflowContext");var failed=context.GetNodeResult(node.Id)?.Result;var independent=context.GetNodeResult(copiedTarget.Id)?.Result;
                    Require(failed?.IsSuccess!=true&&independent?.IsSuccess==true&&independent.Identity!=prior,"手动缺源消费者用旧值执行、或独立仅显示失效节点未实际新跑");
                    vm.SelectedNode=copiedTarget;copiedTarget.IsSelected=true;node.IsSelected=false;Pump();workspace.SelectedOutputItem=workspace.OutputItems.Cast<object>().Single(item=>Get(item,"Reference") is OutputReference reference&&reference.NodeId==copiedTarget.Id&&reference.PortId==BlobSchema.Field(5));Pump();Require(((DeclaredOutputSnapshot)Get(vm,"_resultPublicationSnapshot")).Identity==independent!.Identity&&Maybe(outputPane,"Display") is null,"仅显示来源缺失伪造数值或沿用旧场景");
                }
                DeleteThroughHost(other.Id);CheckManualMissing();history.Undo();Pump();vm.SelectedNode=node;node.IsSelected=true;Pump();Run();Require(((BlobOutput)((DeclaredOutputSnapshot)Get(vm,"_resultPublicationSnapshot")).Output!).ThresholdMinUsed==32&&Words(Scene()).All(word=>Equals(Get(word,"Content"),"A:64;B:32")),"手动来源Undo后新执行/双slot未恢复");history.Redo();Pump();CheckManualMissing();history.Undo();Pump();vm.SelectedNode=node;node.IsSelected=true;Pump();Run();
            });            Case("两轮不同图像量测保前轮显示冻结，真正方案重开后执行显示",()=>{
                var contentId=Display(BlobResultDisplay.Text,"content");var xId=Display(BlobResultDisplay.Text,"x");
                var imagePort=source.RuntimeConfiguration!.Schema.Outputs.Single(port=>port.Id=="output.image");
                var widthSource=new OutputReference(source.Id,imagePort.Id,imagePort.Type,new[]{"width"},scopeId:flowId,storage:imagePort.Storage);
                var edit=host.BeginEdit(flowId,node.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{contentId,BlobResultDisplay.ParseContent("W:{宽};A:{面积}",new[]{new DisplayExpressionSlot("宽",widthSource)})},{xId,new ReferenceSetting(widthSource)}});host.Commit(edit);Pump();
                var before=(DeclaredOutputSnapshot)Get(vm,"_resultPublicationSnapshot");using var retained=before.Retain();using var oldText=new ResultDisplayTextLease(before,node.RuntimeConfiguration!.CaptureResultDisplay()!.Text);
                Require(oldText.Evaluate(0).Content.StartsWith("W:64;",StringComparison.Ordinal),"前轮宽度前置失败");
                var pixels=new byte[80*64];var original=Pixels();for(var row=0;row<64;row++)Array.Copy(original,row*64,pixels,row*80,64);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(80,64,96,96,PixelFormats.Gray8,null,pixels,80)));using(var file=File.Create(imageFile))encoder.Save(file);
                Run();var after=(DeclaredOutputSnapshot)Get(vm,"_resultPublicationSnapshot");
                using var recreatedOldText=new ResultDisplayTextLease(before,node.RuntimeConfiguration!.CaptureResultDisplay()!.Text);Require(recreatedOldText.Evaluate(0).Content.StartsWith("W:64;",StringComparison.Ordinal),"新轮后从旧snapshot重新借用丢前驱");
                Require(before.Identity!=after.Identity&&oldText.Evaluate(0).Content.StartsWith("W:64;",StringComparison.Ordinal),"新轮来源覆盖前轮已保留显示值");
                Require(Words(Scene()).Select(word=>(string)Get(word,"Content")).Order().SequenceEqual(new[]{"W:80;A:25","W:80;A:64"})&&Words(Scene()).All(word=>((Point)Get(word,"Position")).X==80),"新轮未消费真实新宽度/位置或形状丢失");
                Require(solutions.SaveCurrentDocument(path).FileWritten,"新表达式方案保存失败");var previousSession=solutions.Session;var targetId=node.Id;var reopened=solutions.OpenSolution(path)??throw new Exception("真实方案重开失败");Pump();Require(!ReferenceEquals(previousSession,solutions.Session),"未进入新文档会话");
                var selected=vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.Single(item=>item.Id==targetId);vm.SelectedNode=selected;selected.IsSelected=true;Pump();
                var tracePath=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"reopen-trace.txt");
                void TraceReopen(string message)=>File.AppendAllText(tracePath,DateTime.UtcNow.ToString("O")+" "+message+Environment.NewLine);
                var manager=(SunEyeVision.UI.Services.Workflow.WorkflowExecutionManager)Get(vm,"_executionManager");
                EventHandler<SunEyeVision.UI.Services.Workflow.WorkflowExecutionEventArgs> onError=(_,args)=>TraceReopen("ERROR "+args.WorkflowId+" "+args.ErrorMessage);
                EventHandler<SunEyeVision.UI.Services.Workflow.WorkflowExecutionEventArgs> onStart=(_,args)=>TraceReopen("START "+args.WorkflowId);
                EventHandler<SunEyeVision.UI.Services.Workflow.WorkflowExecutionEventArgs> onDone=(_,args)=>TraceReopen("DONE "+args.WorkflowId);
                EventHandler<SunEyeVision.UI.Services.Workflow.NodeExecutionResultEventArgs> onNode=(_,args)=>TraceReopen("NODE "+args.Node.Id+" "+args.Result.Status+" "+args.Result.ErrorMessage);
                manager.WorkflowExecutionError+=onError;manager.WorkflowExecutionStarted+=onStart;manager.WorkflowExecutionCompleted+=onDone;manager.NodeExecutionCompleted+=onNode;
                try
                {
                    var active=Maybe(vm,"ActiveInputSource");TraceReopen("BEFORE selected="+vm.SelectedNode?.Id+" active="+(active is null?null:Maybe(active,"NodeId"))+" owner="+solutions.CurrentSolution!.ExecutionOwner.Id+" status="+vm.StatusText);
                    var newHost=solutions.Session!.Editing.Host;var store=(BindingConfigurationStore)Get(newHost,"_store");
                    var definition=store.Capture().CopyConfiguration().Workflows.Single(flow=>flow.Id==flowId).Nodes.Single(item=>item.Id==targetId);
                    var live=solutions.CurrentSolution.Workflows.Single(flow=>flow.Id==flowId).Nodes.Single(item=>item.Id==targetId);
                    var identityProperty=typeof(RuntimeToolConfiguration).GetProperty("ConfigurationIdentity",BindingFlags.Instance|BindingFlags.NonPublic)!;
                    var liveIdentity=(string)identityProperty.GetValue(live.RuntimeConfiguration!)!;var formalIdentity=(string)identityProperty.GetValue(definition.RuntimeConfiguration!)!;
                    File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"reopen-live-config.json"),liveIdentity);
                    File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"reopen-formal-config.json"),formalIdentity);
                    var instances=(System.Collections.IDictionary)Get(newHost,"_nodeInstances");var registered=instances[new ScopedNodeIdentity(flowId,targetId)];
                    TraceReopen("CONFIG same="+(liveIdentity==formalIdentity)+" registered="+ReferenceEquals(live,registered)+" selected="+ReferenceEquals(live,selected));
                    var rerun=vm.RunSelectedImageAsync();PumpUntil(()=>rerun.IsCompleted);rerun.GetAwaiter().GetResult();Pump();
                    var current=Maybe(vm,"CurrentWorkflowContext") as WorkflowContext;
                    foreach(var item in solutions.CurrentSolution.Workflows.Single(flow=>flow.Id==flowId).Nodes)
                    {var output=current?.GetNodeResult(item.Id)?.Result;TraceReopen("CTX "+item.Id+" "+item.Name+" "+output?.Status+" "+output?.ErrorMessage+" "+output?.Identity);}
                    TraceReopen("BEFORE_SELECT snapshot="+(Maybe(vm,"_resultPublicationSnapshot") as DeclaredOutputSnapshot)?.Identity+" status="+vm.StatusText);
                    SelectBlob();TraceReopen("AFTER_SELECT snapshot="+(Maybe(vm,"_resultPublicationSnapshot") as DeclaredOutputSnapshot)?.Identity+" status="+vm.StatusText);
                }
                finally{manager.WorkflowExecutionError-=onError;manager.WorkflowExecutionStarted-=onStart;manager.WorkflowExecutionCompleted-=onDone;manager.NodeExecutionCompleted-=onNode;}
                var loadedSnapshot=(DeclaredOutputSnapshot)Get(vm,"_resultPublicationSnapshot");Require(loadedSnapshot.Identity.DocumentSessionId==solutions.CurrentSolution!.ExecutionOwner.Id&&loadedSnapshot.Identity.DocumentSessionId!=before.Identity.DocumentSessionId,"重开后仍使用旧文档运行身份");
                Require(Words(Scene()).Select(word=>(string)Get(word,"Content")).Order().SequenceEqual(new[]{"W:80;A:25","W:80;A:64"})&&Words(Scene()).All(word=>((Point)Get(word,"Position")).X==80),"真实重开执行未保表达式/位置引用或实际结果");Capture(resultView,"text-reopened.png");
            });
        }        if(scope is "All" or "TextSource")
        {
            if(scope=="All")
            {
                // 上一组真实重开了方案，重新取得当前会话对象与该组需要的64像素图像。
                node=(SunEyeVision.UI.Shared.WorkflowNode)vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.Single(item=>item.Id==node.Id);
                source=(SunEyeVision.UI.Shared.WorkflowNode)vm.WorkflowTabViewModel.SelectedTab.WorkflowNodes.Single(item=>item.Id==source.Id);
                host=solutions.Session!.Editing.Host;history=solutions.Session.History;
                flow=solutions.CurrentSolution!.Workflows.Single(item=>item.Id==flowId);
                var fresh=new PngBitmapEncoder();fresh.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,64,96,96,PixelFormats.Gray8,null,Pixels(),64)));
                using(var file=File.Create(imageFile))fresh.Save(file);
                vm.SelectedNode=node;node.IsSelected=true;Pump();Run();
            }
            Case("原文本花括号来源事件链与旧容器退出（WPF事件注入）",()=>{
                var contentId=Display(BlobResultDisplay.Text,"content");var baseline=System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration!);
                var window=Open();var control=Control(window);var editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);
                var field=((DeclaredFieldsView)editor.Content).Fields.Single(field=>field.FieldId==contentId);var textPresenter=Get(field.View,"_textPresenter");var session=Get(textPresenter,"_session");
                var raw=Visual(field.View).OfType<TextBox>().Single();raw.SetCurrentValue(TextBox.TextProperty,"值:{}");raw.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();raw.SelectionStart=3;raw.SelectionLength=0;
                var focus=Keyboard.Focus(raw);Pump();raw.SelectionStart=2;raw.SelectionStart=3;Pump();
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"text-focus.json"),System.Text.Json.JsonSerializer.Serialize(new{raw.IsKeyboardFocused,raw.IsFocused,raw.Text,raw.SelectionStart,focusType=focus?.GetType().FullName,sourceCount=((System.Collections.IEnumerable)Get(session,"SourceNodes")).Cast<object>().Count()}));
                Require(raw.IsKeyboardFocused,"隔离桌面未取得原文本框WPF键盘焦点；不手工RaiseEvent伪造焦点、不回退用户桌面");
                PumpUntil(()=>((System.Collections.IEnumerable)Get(session,"SourceNodes")).Cast<object>().Any());
                IEnumerable<object> Nodes(System.Collections.IEnumerable nodes){foreach(var item in nodes){yield return item;foreach(var child in Nodes((System.Collections.IEnumerable)Get(item,"EnumEntrysA")))yield return child;}}
                var selected=Nodes((System.Collections.IEnumerable)Get(session,"SourceNodes")).Single(item=>Maybe(item,"Candidate") is SunEyeVision.Plugin.Wpf.Adapters.References.DeclaredReferenceCandidate candidate&&candidate.NodeId==source.Id&&candidate.Output.Id=="output.image"&&candidate.MemberPath.SequenceEqual(new[]{"width"}));
                var popup=Visual(field.View).OfType<AppsPopup>().Single();popup.ApplyTemplate();var part=(Popup)popup.Template.FindName("PART_Popup",popup)!;Require(part.IsOpen&&part.Child is not null,"原文本来源Popup未实际打开");
                var tree=Visual(part.Child).OfType<SunEyeVision.Plugin.Wpf.Controls.References.Primitives.SubTreeView>().Single();var native=(TreeView)tree.FindName("treeView");tree.UpdateLayout();Pump();
                var container=(TreeViewItem?)tree.GetType().GetMethod("FindTreeViewItem",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{native,selected})??throw new Exception("原文本来源目标容器未生成");
                var previousScene=Maybe(outputPane,"Display");
                container.IsSelected=true;Pump();container.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=System.Windows.Controls.Control.MouseDoubleClickEvent});Pump();
                Require(field.Setting is DisplayExpressionSetting expression&&expression.Slots.Single().Reference is {} reference&&reference.NodeId==source.Id&&reference.PortId=="output.image"&&reference.MemberFieldPath.SequenceEqual(new[]{"width"})&&reference.ScopeId==flowId,"原文本树事件未保选择来源身份");
                void TraceScene(string stage)
                {
                    var current=Maybe(outputPane,"Display");var preview=Maybe(vm,"_displayPreview");
                    var contents=current is null?Array.Empty<string>():Words(current).Select(word=>(string)Get(word,"Content")).ToArray();
                    var data=new{stage,sameScene=ReferenceEquals(previousScene,current),contents,setting=System.Text.Json.JsonSerializer.Serialize(field.Setting),pending=Maybe(session,"_pending")?.ToString(),acceptedRevision=Maybe(editor,"AcceptedRevision"),syncedRevision=Maybe(control,"_syncedRevision"),displayRevision=Maybe(vm,"_resultDisplayRevision"),captureError=Maybe(control,"_captureError")?.ToString(),previewAppearance=preview is null?null:Maybe(preview,"Appearance")?.ToString()};
                    File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"text-source-trace.jsonl"),System.Text.Json.JsonSerializer.Serialize(data)+Environment.NewLine);
                }
                TraceScene("after-selection");
                try{PumpUntil(()=>Maybe(outputPane,"Display") is {} current&&Words(current).Count()==2&&Words(current).All(word=>Equals(Get(word,"Content"),"值:64")));}
                catch{TraceScene("timeout");throw;}
                TraceScene("accepted");
                window.Close();Pump();Require(System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration!)==baseline,"来源选择取消写正式配置");
                window=Open();control=Control(window);var context=ToolEditorCommit.GetDraftContext(control)!;var draft=(WorkflowNodeBase)context.GetType().GetProperty("DraftNode")!.GetValue(context)!;var draftBefore=System.Text.Json.JsonSerializer.Serialize(draft.RuntimeConfiguration!);
                container.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=System.Windows.Controls.Control.MouseDoubleClickEvent});Pump();Require(window.IsVisible&&System.Text.Json.JsonSerializer.Serialize(draft.RuntimeConfiguration!)==draftBefore,"旧文本容器写新草稿或关闭新窗");window.Close();Pump();
            });
            if(scope=="TextSource")Case("真实文本树连续选择两来源、编辑确认并重开消费",()=>{
                var second=textSourceB??throw new Exception("缺第二文本来源夹具");
                var contentId=Display(BlobResultDisplay.Text,"content");var window=Open();var control=Control(window);
                var editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);
                var field=((DeclaredFieldsView)editor.Content).Fields.Single(item=>item.FieldId==contentId);
                var textPresenter=Get(field.View,"_textPresenter");var session=Get(textPresenter,"_session");
                var raw=Visual(field.View).OfType<TextBox>().Single();
                IEnumerable<object> Nodes(System.Collections.IEnumerable nodes){foreach(var item in nodes){yield return item;foreach(var child in Nodes((System.Collections.IEnumerable)Get(item,"EnumEntrysA")))yield return child;}}
                void Choose(string proposed,string sourceId)
                {
                    raw.SetCurrentValue(TextBox.TextProperty,proposed);raw.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                    Keyboard.Focus(raw);Pump();var caret=proposed.LastIndexOf("{}",StringComparison.Ordinal)+1;Require(caret>0,"缺待选择的花括号");
                    raw.SelectionStart=caret-1;raw.SelectionStart=caret;Pump();
                    PumpUntil(()=>Nodes((System.Collections.IEnumerable)Get(session,"SourceNodes")).Any(item=>Maybe(item,"Candidate") is SunEyeVision.Plugin.Wpf.Adapters.References.DeclaredReferenceCandidate c&&c.NodeId==sourceId&&c.Output.Id=="output.image"&&c.MemberPath.SequenceEqual(new[]{"width"})));
                    var selected=Nodes((System.Collections.IEnumerable)Get(session,"SourceNodes")).Single(item=>Maybe(item,"Candidate") is SunEyeVision.Plugin.Wpf.Adapters.References.DeclaredReferenceCandidate c&&c.NodeId==sourceId&&c.Output.Id=="output.image"&&c.MemberPath.SequenceEqual(new[]{"width"}));
                    var popup=Visual(field.View).OfType<AppsPopup>().Single();popup.ApplyTemplate();var part=(Popup)popup.Template.FindName("PART_Popup",popup)!;
                    Require(part.IsOpen&&part.Child is not null,"原文本来源Popup未实际打开");
                    var tree=Visual(part.Child).OfType<SunEyeVision.Plugin.Wpf.Controls.References.Primitives.SubTreeView>().Single();var native=(TreeView)tree.FindName("treeView");tree.UpdateLayout();Pump();
                    var item=(TreeViewItem?)tree.GetType().GetMethod("FindTreeViewItem",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{native,selected})??throw new Exception("来源树目标容器未生成");
                    item.IsSelected=true;Pump();item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=System.Windows.Controls.Control.MouseDoubleClickEvent});Pump();
                }
                Choose("前:{};后",source.Id);
                Require(field.Setting is DisplayExpressionSetting first&&first.Slots.Count==1&&first.Slots[0].Reference?.NodeId==source.Id,"第一次真实选择未保存来源A");
                PumpUntil(()=>Maybe(outputPane,"Display") is {} scene&&Words(scene).Count()==2&&Words(scene).All(word=>Equals(Get(word,"Content"),"前:64;后")));
                Choose(raw.Text+";中:{};尾",second.Id);
                Require(field.Setting is DisplayExpressionSetting two&&two.Slots.Count==2&&two.Slots.Any(slot=>slot.Reference?.NodeId==source.Id)&&two.Slots.Any(slot=>slot.Reference?.NodeId==second.Id),"第二次真实选择丢失A或B身份");
                PumpUntil(()=>Maybe(outputPane,"Display") is {} scene&&Words(scene).Count()==2&&Words(scene).All(word=>Equals(Get(word,"Content"),"前:64;后;中:32;尾")));
                raw.SetCurrentValue(TextBox.TextProperty,raw.Text.Replace("前:","前缀:",StringComparison.Ordinal).Replace(";后;",";后缀;",StringComparison.Ordinal));
                raw.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();Require(control.TryCommitEditor(out var error),error);Pump();
                Require(node.RuntimeConfiguration!.Fields.Read(contentId) is DisplayExpressionSetting committed&&committed.Slots.Count==2&&committed.Slots.Any(slot=>slot.Reference?.NodeId==source.Id)&&committed.Slots.Any(slot=>slot.Reference?.NodeId==second.Id),"确认后双来源身份丢失");
                window.Close();Pump();Require(solutions.SaveCurrentDocument(path).FileWritten,"双来源正式保存失败");
                var targetId=node.Id;var oldOwner=solutions.CurrentSolution!.ExecutionOwner.Id;_=solutions.OpenSolution(path)??throw new Exception("双来源方案重开失败");Pump();
                var reopened=vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.Single(item=>item.Id==targetId);vm.SelectedNode=reopened;reopened.IsSelected=true;Pump();
                Require(reopened.RuntimeConfiguration!.Fields.Read(contentId) is DisplayExpressionSetting restored&&restored.Slots.Count==2&&restored.Slots.Any(slot=>slot.Reference?.NodeId==source.Id)&&restored.Slots.Any(slot=>slot.Reference?.NodeId==second.Id),"重开后双来源身份丢失");
                Run();var snapshot=(DeclaredOutputSnapshot)Get(vm,"_resultPublicationSnapshot");Require(snapshot.Identity.DocumentSessionId!=oldOwner&&snapshot.Identity.DocumentSessionId==solutions.CurrentSolution!.ExecutionOwner.Id,"双来源重开执行使用旧会话");
                PumpUntil(()=>Maybe(outputPane,"Display") is {} scene&&Words(scene).Count()==2&&Words(scene).All(word=>Equals(Get(word,"Content"),"前缀:64;后缀;中:32;尾")));
            });
        }        shell.Close();app.Shutdown();Console.WriteLine("Blob正式图文组合："+_passed+"组通过；仅具真实生产值项目开通，非VM像素/语言/物理鼠标等价。");return 0;
    }
    private static void SelectResultPage(DeclaredSettingsView editor)
    {
        editor.UpdateLayout();Pump();var tabs=Visual(editor).OfType<TabControl>().Single();
        var header=tabs.Items.Cast<TabItem>().Single(item=>Equals(item.Header,"结果显示"));
        tabs.SelectedItem=header;editor.UpdateLayout();Pump();
        Require(ReferenceEquals(tabs.SelectedItem,header)&&Equals(header.Header,"结果显示"),"未实际选中结果显示页");
    }
    private sealed class ValueOutput(IReadOnlyDictionary<string,object?> values):IToolOutput,IDisposable
    {public IReadOnlyDictionary<string,object?> ToValues()=>values;public void Dispose(){}}    private sealed class ScalarOutput(Action disposed):IToolOutput,IDisposable
    {public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>{{"n",1d}};public void Dispose()=>disposed();}
    private sealed class ActionLease(Action dispose):IDisposable
    {public void Dispose()=>dispose();}    private static byte[] Pixels(){var pixels=new byte[4096];for(var y=5;y<13;y++)for(var x=5;x<13;x++)pixels[y*64+x]=200;for(var y=30;y<35;y++)for(var x=30;x<35;x++)pixels[y*64+x]=200;return pixels;}
    private sealed class ActionCommand(Action<object?> action):ICommand {public bool CanExecute(object? value)=>true;public void Execute(object? value)=>action(value);public event EventHandler? CanExecuteChanged {add{}remove{}}}
    private sealed class Spy(bool fail):IDisposable {public int Count;public void Dispose(){Count++;if(fail)throw new InvalidOperationException("reader-release-marker");}}
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object? Maybe(object value,string name){for(var type=value.GetType();type is not null;type=type.BaseType){var property=type.GetProperty(name,Flags|BindingFlags.DeclaredOnly);if(property is not null)return property.GetValue(value);var field=type.GetField(name,Flags|BindingFlags.DeclaredOnly);if(field is not null)return field.GetValue(value);}return null;}
    static object Get(object value,string name)=>Maybe(value,name)??throw new Exception("缺真实成员："+name);
    static RecordValue NativeShape(string kind,double angle=0,double start=0,double sweep=Math.PI/2)
    {
        if(kind=="polygon")return RoiGeometryEditing.Create(kind,new(0,0),new(0,0),new[]{new Point2(4,5),new Point2(12,5),new Point2(4,19)});
        var values=RoiGeometryEditing.Values(RoiGeometryEditing.Create(kind=="sector"?"annulus":kind,new(14,18),new(26,22)));
        if(kind=="rectangle"){values["angle-degrees"]=angle;}
        else{values["kind"]=kind;values["center"]=RoiGeometryEditing.Point(new(20,20));values["inner-radius"]=3d;values["outer-radius"]=10d;values["start-radians"]=start;values["end-radians"]=start+sweep;}
        return RoiGeometryEditing.Checked(values);
    }
    static RecordValue Stored(RecordValue shape)=>(RecordValue)typeof(SunEyeVision.Tool.BlobAnalysis.Wpf.Views.BlobParameterEditor).Assembly.GetType("SunEyeVision.Tool.BlobAnalysis.Wpf.Views.BlobLocalRoiProjection")!.GetMethod("FromNative",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object?[]{shape,null})!;
    static RecordValue Root(RecordValue shape)=>BlobLocalRoi.Root(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{BlobLocalRoi.Item(Guid.NewGuid(),"test",Stored(shape))}),BlobLocalRoi.Group(BlobLocalRoi.None,Array.Empty<RecordValue>()));
    static bool Contains(IReadOnlyRegion region,int x,int y)
    {using var read=region.AcquireReader();for(var i=0;i<read.RunCount;){var runs=read.ReadRuns(i,Math.Min(4096,read.RunCount-i));foreach(var run in runs.Span)if(run.Row==y&&x>=run.FirstColumn&&x<=run.LastColumnInclusive)return true;i+=runs.Length;}return false;}
    private static bool SelectRadio(FrameworkElement radio,long value)=>(bool)radio.GetType().GetMethod("TrySetConstant")!.Invoke(radio,new object[]{value})!;
    private static void SelectSource(DeclaredFieldEditor field,OutputReference? expected)
    {
        var popup=Logical(field).Concat(Visual(field)).OfType<AppsPopup>().First(item=>item.IsVisible);popup.IsDropDownOpen=true;popup.UpdateLayout();Pump();
        var session=popup.DataContext;((ICommand)session.GetType().GetProperty("DropDownCommand")!.GetValue(session)!).Execute(null);Pump();
        var roots=(System.Collections.IEnumerable)session.GetType().GetProperty("SourceNodes")!.GetValue(session)!;
        IEnumerable<object> Walk(System.Collections.IEnumerable nodes)
        {foreach(var node in nodes){yield return node;foreach(var child in Walk((System.Collections.IEnumerable)node.GetType().GetProperty("EnumEntrysA")!.GetValue(node)!))yield return child;}}
        var item=Walk(roots).Single(node=>
        {
            if(expected is null)return (bool)node.GetType().GetProperty("Clear")!.GetValue(node)!;
            var candidate=node.GetType().GetProperty("Candidate")!.GetValue(node);if(candidate is null)return false;
            var reference=(OutputReference)candidate.GetType().GetMethod("Reference")!.Invoke(candidate,new object[]{BindingSelection.All})!;
            return BindingReferenceSerializer.Serialize(reference)==BindingReferenceSerializer.Serialize(expected);
        });        ((ICommand)session.GetType().GetProperty("ClickCommand")!.GetValue(session)!).Execute(item);popup.IsDropDownOpen=false;Pump();
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
    private static void Case(string name,Action action){var filter=Environment.GetEnvironmentVariable("S4_BLOB_DISPLAY_CASE");if(!string.IsNullOrEmpty(filter)&&!name.Contains(filter,StringComparison.Ordinal))return;var log=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"display-progress.log");File.AppendAllText(log,DateTime.UtcNow.ToString("O")+" BEGIN "+name+Environment.NewLine);try{action();_passed++;Console.WriteLine("通过："+name);File.AppendAllText(log,DateTime.UtcNow.ToString("O")+" PASS "+name+Environment.NewLine);}catch(Exception error){File.AppendAllText(log,DateTime.UtcNow.ToString("O")+" FAIL "+name+Environment.NewLine+error+Environment.NewLine);throw;}}
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
