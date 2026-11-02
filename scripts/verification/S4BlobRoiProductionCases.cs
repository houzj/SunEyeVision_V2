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
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
using SunEyeVision.UI.Shell.Controls.Shell;

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
        var scope=Environment.GetEnvironmentVariable("S4_BLOB_ROI_SCOPE")??"All";
        if(scope is not ("All" or "Model" or "Window" or "Frame" or "Reentry"))throw new Exception("未知专项筛选");
        if(scope is "All" or "Model")ModelCases();
        if(scope=="Model"){Console.WriteLine("本方ROI物化："+_passed+"组，非VM像素等价。");app.Shutdown();return 0;}
        var resultView=new ImageResultControl{DataContext=vm.ImageResult};resultView.Initialize(vm);
        var shell=new Window{Width=1000,Height=850,Content=resultView,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();Pump();
        var node=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(node);node.DebugWindowStyle=DebugWindowStyle.Custom;
        var source=vm.WorkflowTabViewModel.SelectedTab.CreateNode("ImageLoad");
        var imageFile=Path.Combine(folder,"input.png");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,64,96,96,PixelFormats.Gray8,null,Enumerable.Repeat((byte)200,4096).ToArray(),64)));using(var file=File.Create(imageFile))png.Save(file);
        var policy=ImageSourcePolicy.Read(source)!;policy.FilePaths=new(){imageFile};policy.CurrentIndex=0;policy.ForRunPaths=new(){imageFile};policy.SNInitialValue=1;policy.ApplyTo(source);vm.AddNodeToWorkflow(source);
        Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=source.Id,TargetNodeId=node.Id}),"真实图像连线失败");
        var inputReference=((ReferenceSetting)node.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput)).Reference;var key=BindingReferenceSerializer.Serialize(inputReference);
        var flowId=vm.WorkflowTabViewModel.SelectedTab.RootWorkflowId;var host=solutions.Session!.Editing.Host;var history=solutions.Session.History;
        vm.SelectedNode=node;node.IsSelected=true;Pump();
        var workspace=(ResultWorkspaceShell)resultView.FindName("Workspace");workspace.IsDoubleImage=false;Pump();
        SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow Open()
        {vm.OpenDebugWindowCommand.Execute(node);Pump();var window=app.Windows.OfType<SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow>().Single(w=>w.IsVisible);window.UpdateLayout();Pump();return window;}
        RuntimeNodeConfigurationControl Control(SunEyeVision.UI.Services.Editing.NativeRuntimeEditorWindow window)=>window.EditorControl;
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
        Case("无正式图像仍保原两组工具箱，不能伪造绘制帧",()=>
        {
            var formal=node.RuntimeConfiguration;var window=Open();var current=Control(window);var privateOwner=Owner(current);var editor=Editor(privateOwner,BlobLocalRoi.Inspection);
            Require(editor.NativeView.IsVisible&&editor.NativeView.IsEnabled,"无图时原形状入口消失");((ICommand)Get(editor.NativeView.DataContext,"ValueChangedCommand")).Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box);Pump();
            Require(((System.Collections.IDictionary)Get(privateOwner,"_sessions")).Count==0&&ReferenceEquals(formal,node.RuntimeConfiguration),"无图时造帧或写正式模型");window.Close();Pump();
        });
        var run=vm.RunSelectedImageAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();Pump();
        workspace.SelectedOutputItem=workspace.OutputItems.Cast<object>().First(item=>(string)Get(item,"Key")!=key);Pump();
        var path=Path.Combine(folder,"roi.solution");Require(solutions.SaveCurrentDocument(path).FileWritten,"建立方案位置失败");var bytes=File.ReadAllBytes(path);
        var displayTrace=new List<object>();
        var initialViewport=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultViewport)Get(Get(Get(resultView,"_workspacePresenter"),"OutputPane"),"Viewport");
        var initialReceipt=initialViewport.CurrentFrame!;var initialRaster=Get(initialViewport,"_frame");
        var initialPixelHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData((byte[])Get(initialRaster,"Buffer")));
        var initialDomain=System.Text.Json.JsonSerializer.Serialize(Get(initialRaster,"Domain"));
        void TraceDisplay(string phase)
        {
            var selected=workspace.SelectedOutputItem;
            var pane=Get(Get(resultView,"_workspacePresenter"),"OutputPane");var viewport=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultViewport)Get(pane,"Viewport");
            displayTrace.Add(new{phase,time=DateTime.UtcNow,selectedInstance=selected is null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(selected),selectedKey=selected is null?null:(string)Get(selected,"Key"),node.SelectedDisplayReference,fileHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))),receipt=viewport.CurrentFrame?.ToString(),stack=phase=="output-dp-notice"?Environment.StackTrace:null});
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"display-lifetime-trace.json"),System.Text.Json.JsonSerializer.Serialize(displayTrace,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        }
        var descriptor=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ResultWorkspaceShell.SelectedOutputItemProperty,typeof(ResultWorkspaceShell));
        EventHandler displayChanged=(_,_)=>TraceDisplay("output-dp-notice");descriptor.AddValueChanged(workspace,displayChanged);TraceDisplay("before-open");
        var selectedBefore=workspace.SelectedOutputItem;var displayBefore=node.SelectedDisplayReference;var first=Open();TraceDisplay("after-open");var control=Control(first);var owner=Owner(control);
        Case("打开取消不切换单图，原工具箱在无匹配帧时保持可见",()=>
        {
            Require(ReferenceEquals(selectedBefore,workspace.SelectedOutputItem)&&node.SelectedDisplayReference==displayBefore,"仅打开窗口改了显示来源");
            var editors=Logical(control).OfType<NativeRoiEditor>().ToArray();Require(editors.Length==2&&editors.All(editor=>editor.Visibility==Visibility.Visible&&editor.NativeView.IsEnabled),"原两组工具箱消失");
            Require(((System.Collections.IDictionary)Get(owner,"_sessions")).Count==0,"打开即借帧");
            var drawContext=editors[0].NativeView.DataContext;((ICommand)Get(drawContext,"ValueChangedCommand")).Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box);Pump();
            Require(((System.Collections.IDictionary)Get(owner,"_sessions")).Count==0,"选错显示图仍能进入绘制");
            first.Close();Pump();TraceDisplay("after-close");Console.WriteLine("关闭比较 instance="+ReferenceEquals(selectedBefore,workspace.SelectedOutputItem)+" key="+(selectedBefore is not null&&workspace.SelectedOutputItem is not null&&(string)Get(selectedBefore,"Key")== (string)Get(workspace.SelectedOutputItem,"Key"))+" model="+(node.SelectedDisplayReference==displayBefore)+" file="+File.ReadAllBytes(path).SequenceEqual(bytes));Require(workspace.SelectedOutputItem is not null&&selectedBefore is not null&&(string)Get(selectedBefore,"Key")== (string)Get(workspace.SelectedOutputItem,"Key")&&node.SelectedDisplayReference==displayBefore&&File.ReadAllBytes(path).SequenceEqual(bytes),"取消改变稳定显示身份/方案");
            var after=initialViewport.CurrentFrame!;var raster=Get(initialViewport,"_frame");
            Require(after.Frame==initialReceipt.Frame&&after.Coordinates==initialReceipt.Coordinates&&Convert.ToHexString(System.Security.Cryptography.SHA256.HashData((byte[])Get(raster,"Buffer")))==initialPixelHash&&System.Text.Json.JsonSerializer.Serialize(Get(raster,"Domain"))==initialDomain,"取消改变实际物理图像/Domain/像素");
        });
        descriptor.RemoveValueChanged(workspace,displayChanged);
        SelectInput();first=Open();control=Control(first);owner=Owner(control);
        if(scope is "All" or "Reentry")
        {
            var presenter=Get(resultView,"_workspacePresenter");var borrow=presenter.GetType().GetMethod("BorrowRoiFrame",Flags)!;
            NativeRoiFrameLease? Borrow(OutputReference reference,Func<bool> proof)
            {try{return (NativeRoiFrameLease?)borrow.Invoke(presenter,new object[]{reference,proof});}catch(TargetInvocationException error){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException!).Throw();throw;}}
            Case("失败/异常借用不抢已有lease，成功新owner不被旧Dispose退役",()=>
            {
                using var old=Borrow(inputReference,()=>true)??throw new Exception("实际输入未能借帧");
                var other=workspace.OutputItems.Cast<object>().First(item=>(string)Get(item,"Key")!=key);var otherReference=(OutputReference)Get(other,"Reference");
                Require(Borrow(otherReference,()=>true) is null&&old.IsCurrent,"不匹配借用抢了旧lease");
                var marker=new InvalidOperationException("proof-marker");var seen=false;try{_=Borrow(inputReference,()=>throw marker);}catch(InvalidOperationException error){seen=ReferenceEquals(error,marker);}Require(seen&&old.IsCurrent,"proof首错或旧lease丢失");
                var count=0;seen=false;try{_=Borrow(inputReference,()=>{if(++count==3)throw marker;return true;});}catch(InvalidOperationException error){seen=ReferenceEquals(error,marker);}Require(seen&&old.IsCurrent,"安装后异常留下无lease owner或破坏旧owner");
                using var newer=Borrow(inputReference,()=>true)??throw new Exception("新owner未取得同帧");Require(!old.IsCurrent&&newer.IsCurrent,"接管身份错误");old.Dispose();Require(newer.IsCurrent,"旧Dispose退役新owner");
            });
            Case("proof同步重入新borrow仅新请求可接纳",()=>
            {
                NativeRoiFrameLease? inner=null;var entered=false;
                var outer=Borrow(inputReference,()=>{if(!entered){entered=true;inner=Borrow(inputReference,()=>true);}return true;});
                Require(outer is null&&inner?.IsCurrent==true,"旧proof调用覆盖或清理新请求");inner!.Dispose();
                var selected=workspace.SelectedOutputItem!;var field=selected.GetType().GetField("<Current>k__BackingField",Flags)!;var original=(Func<bool>)field.GetValue(selected)!;entered=false;inner=null;
                field.SetValue(selected,(Func<bool>)(()=>{if(!entered){entered=true;inner=Borrow(inputReference,()=>true);}return original();}));
                try{outer=Borrow(inputReference,()=>true);Require(outer is null&&inner?.IsCurrent==true,"source.Current重入覆盖新owner");}
                finally{field.SetValue(selected,original);outer?.Dispose();inner?.Dispose();}
            });
            Case("proof中的用户切图保留其真实选择，同帧不同Key不可借",()=>
            {
                var other=workspace.OutputItems.Cast<object>().First(item=>(string)Get(item,"Key")!=key);var otherKey=(string)Get(other,"Key");var changed=false;
                var lease=Borrow(inputReference,()=>{if(!changed){changed=true;workspace.SelectedOutputItem=other;}return true;});
                Require(lease is null&&(string)Get(workspace.SelectedOutputItem!,"Key")==otherKey,"旧borrow覆盖用户选择");
                Require(Borrow(inputReference,()=>true) is null,"同帧不同来源Key误被接纳");SelectInput(true);using var doubleLease=Borrow(inputReference,()=>true)??throw new Exception("双图真实input未能借帧");workspace.IsDoubleImage=false;Pump();Require(!doubleLease.IsCurrent,"单双图切换未退役旧lease");SelectInput();
            });
        }
        if(scope is "All" or "Window" or "Frame")
        {
            Case("单图实际输入借帧，两组四形同一正式owner且同屏",()=>
            {
                foreach(var group in new[]{BlobLocalRoi.Inspection,BlobLocalRoi.Mask})foreach(var kind in new[]{"rectangle","annulus","sector","polygon"})Draw(owner,group,kind);
                var record=((ConstantSetting)((ProductionEditorContext)ToolEditorCommit.GetDraftContext(control)!).DraftNode.RuntimeConfiguration!.Fields.Read(BlobLocalRoi.InputId)).Value as RecordValue;
                Require(record is not null&&BlobLocalRoi.Items(record,BlobLocalRoi.Inspection).Count==4&&BlobLocalRoi.Items(record,BlobLocalRoi.Mask).Count==4,"四形未进唯一纯值定义");
                var active=Session(owner,BlobLocalRoi.Mask);Require(active.Items.Count==8&&active.Items.All(item=>item.Visible)&&active.Items.Count(item=>item.Enabled)==4,"两组未完整显示或非当前组可改");
                var presenter=Get(resultView,"_workspacePresenter");var pane=Get(presenter,"OutputPane");var viewport=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultViewport)Get(pane,"Viewport");
                Require(viewport.CurrentFrame is {} receipt&&receipt.Frame==active.Frame&&receipt.Coordinates==active.Coordinates,"实际单图receipt未绑定同帧同Domain");
                var view=(SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.ImageViewBox)viewport.View;
                var normal=((System.Collections.IEnumerable)view.ROIList).Cast<object>().ToArray();var shapes=((System.Collections.IEnumerable)view.ShapeList).Cast<object>().ToArray();
                Require(active.Items.All(item=>normal.Any(row=>ReferenceEquals(row,item))||shapes.Any(row=>ReferenceEquals(row,item))),"实际原视口遗漏另一组几何");
                Capture(first,"roi-parameters.png");Capture(view,"roi-two-groups.png");Require(control.TryCapture(out var error),"四形不可捕获："+error);
            });
            Case("组内删除与纯值复制粘贴和本地历史不改另一组",()=>
            {
                var editor=Editor(owner,BlobLocalRoi.Mask);Require(editor.PrepareInteraction!(),"不能激活屏蔽组");var session=Session(owner,BlobLocalRoi.Mask);var item=session.Items.First(row=>row.Group==BlobLocalRoi.Mask);
                Require(session.Select(item.Group,item.Id),"正式组对象不能选择");Require(session.TryCopy(item,out var clipboard)&&clipboard is not null,"正式组对象不能复制");
                Require(session.TryPaste(clipboard!,new Point2(35,35)),"同一scope纯值粘贴失败");Require(session.Items.Count(row=>row.Group==BlobLocalRoi.Mask)==5,"粘贴未创建独立对象");
                var pasted=session.CurROIData!;Require(pasted.Id!=item.Id&&session.TryDelete(new[]{pasted}),"删除新对象失败");Require(session.Items.Count(row=>row.Group==BlobLocalRoi.Inspection)==4&&session.Items.Count(row=>row.Group==BlobLocalRoi.Mask)==4,"组操作波及另一组");
                Require(session.TryUndo()&&session.Items.Any(row=>row.Group==BlobLocalRoi.Mask&&row.Id==pasted.Id),"本地撤销未恢复同ID对象");Require(session.TryRedo()&&!session.Items.Any(row=>row.Group==BlobLocalRoi.Mask&&row.Id==pasted.Id),"本地重做未恢复删除");
            });
            Case("真实确认撤销重做复制保存重开保留两组纯值并实际执行",()=>
            {
                var count=history.Position;Require(first.TryConfirm(out var error),error);if(first.IsVisible)first.Close();Pump();Require(history.Position==count+1,"ROI未作为一次正式历史接纳");
                var accepted=node.RuntimeConfiguration!.Fields.Get<RecordValue>(BlobLocalRoi.InputId);history.Undo();Require(node.RuntimeConfiguration!.Fields.Read(BlobLocalRoi.InputId) is UnsetSetting,"撤销未恢复缺省ROI");history.Redo();Require(BlobLocalRoi.Same(node.RuntimeConfiguration!.Fields.Get<RecordValue>(BlobLocalRoi.InputId),accepted),"重做丢ROI");
                Require(BlobLocalRoi.Same(node.RuntimeConfiguration.Copy().Fields.Get<RecordValue>(BlobLocalRoi.InputId),accepted),"复制丢ROI");Require(solutions.SaveCurrentDocument(path).FileWritten,"ROI保存失败");var reopened=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("ROI重开失败");
                var restored=reopened.Workflows.Single(flow=>flow.Id==flowId).Nodes.Single(value=>value.Id==node.Id).RuntimeConfiguration!.Fields.Get<RecordValue>(BlobLocalRoi.InputId);Require(BlobLocalRoi.Same(accepted,restored),"重开不是同一纯值定义");
                var loadedEngine=new WorkflowEngine(new CheckLogger());foreach(var flow in reopened.Workflows)loadedEngine.RegisterWorkflow(flow);
                var executor=new WorkflowExecutionEngine(loadedEngine,plugins,new CheckLogger());
                var request=new WorkflowRunRequest(flowId,null,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{source.Id,new ImageSourceRunInput(imageFile,1){LocalReadAdmission=_=>imageFile}}})
                    {Configuration=WorkflowRunConfiguration.Capture(reopened.Workflows),ExecutionOwner=reopened.ExecutionOwner,ResourcePaths=SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(reopened)};
                var restoredRun=executor.ExecuteWorkflowAsync(request);PumpUntil(()=>restoredRun.IsCompleted);using var restoredResult=restoredRun.GetAwaiter().GetResult();Require(restoredResult.Success&&restoredResult.NodeResults[node.Id].Result is DeclaredOutputSnapshot {IsSuccess:true},"真实重开对象未进入正式引擎执行");
                var execute=vm.RunSelectedImageAsync();PumpUntil(()=>execute.IsCompleted);execute.GetAwaiter().GetResult();
                var context=(WorkflowContext)typeof(MainWindowViewModel).GetProperty("CurrentWorkflowContext",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;Require(context.GetNodeResult(node.Id)?.Result is DeclaredOutputSnapshot {IsSuccess:true},"正式工作流未消费本地ROI");
            });
        }
        else{first.Close();Pump();}
        if(scope is "All" or "Frame")
        {
            descriptor.RemoveValueChanged(workspace,displayChanged);
        SelectInput();first=Open();control=Control(first);owner=Owner(control);
            Case("非法ROI原文在选错图及选回输入后无损恢复，原绑定仍Active",()=>
            {
                var editor=Editor(owner,BlobLocalRoi.Inspection);Require(editor.PrepareInteraction!(),"实际输入不能取得编辑帧");var session=Session(owner,BlobLocalRoi.Inspection);
                var box=session.Items.First(item=>item.Group==BlobLocalRoi.Inspection&&item.Kind=="rectangle");Require(session.Select(box.Group,box.Id),"实际对象不能选择");
                var expand=Visual(editor.NativeView).OfType<ToggleButton>().Single(button=>button.Name=="expand");expand.SetCurrentValue(ToggleButton.IsCheckedProperty,true);first.UpdateLayout();Pump();
                var text=Visual(editor.NativeView).OfType<TextBox>().First(item=>item.Name=="ValueText"&&item.IsVisible);var baseline=text.Text;Require(text.Focus(),"ROI原文未取得实际WPF焦点");text.SetCurrentValue(TextBox.TextProperty,"invalid-roi");Pump();
                Require(editor.HasPendingEdits&&!control.TryCapture(out _),"非法原文误接纳");
                var other=workspace.OutputItems.Cast<object>().First(item=>(string)Get(item,"Key")!=key);workspace.SelectedOutputItem=other;Pump();Require(!control.TryCapture(out _),"切错图丢原文后误通过");
                SelectInput();Require(editor.PrepareInteraction!(),"选回输入不能重借");first.UpdateLayout();Pump();
                text=Visual(editor.NativeView).OfType<TextBox>().First(item=>item.Name=="ValueText"&&item.IsVisible);Require(text.Text=="invalid-roi"&&text.GetBindingExpression(TextBox.TextProperty)?.Status==BindingStatus.Active,"跨receipt恢复丢原文/原绑定");
                Require(text.Focus(),"恢复原文不能聚焦");text.SetCurrentValue(TextBox.TextProperty,baseline);Pump();Require(control.TryCapture(out var error),"原文修正不能确认："+error);
            });
            Case("实际生产publication新帧替换保原文与旧回调退役",()=>
            {
                var editor=Editor(owner,BlobLocalRoi.Inspection);var previous=Session(owner,BlobLocalRoi.Inspection);var oldFrame=previous.Frame;
                var box=previous.Items.First(item=>item.Group==BlobLocalRoi.Inspection&&item.Kind=="rectangle");Require(previous.Select(box.Group,box.Id),"换帧前不能选框");first.UpdateLayout();Pump();
                var text=Visual(editor.NativeView).OfType<TextBox>().First(item=>item.Name=="ValueText"&&item.IsVisible);var baseline=text.Text;Require(text.Focus(),"换帧原文无焦点");text.SetCurrentValue(TextBox.TextProperty,"frame-raw");Pump();
                var document=solutions.CurrentSolution!;var executor=new WorkflowExecutionEngine((WorkflowEngine)Get(vm,"_workflowEngine"),plugins,new CheckLogger());
                var request=new WorkflowRunRequest(flowId,null,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{source.Id,new ImageSourceRunInput(imageFile,1){LocalReadAdmission=_=>imageFile}}})
                    {Configuration=WorkflowRunConfiguration.Capture(document.Workflows),ExecutionOwner=document.ExecutionOwner,ResourcePaths=SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(document)};
                var task=executor.ExecuteWorkflowAsync(request);PumpUntil(()=>task.IsCompleted);using var execution=task.GetAwaiter().GetResult();Require(execution.Success,"外部正式执行失败");
                var context=(WorkflowContext)typeof(MainWindowViewModel).GetProperty("CurrentWorkflowContext",Flags)!.GetValue(vm)!;var results=(SunEyeVision.UI.Services.Workflow.NodeResultManager)Get(vm,"_nodeResultManager");
                results.UpdateNodeResult(source,(DeclaredOutputSnapshot)execution.NodeResults[source.Id].Result!,false,context);results.UpdateNodeResult(node,(DeclaredOutputSnapshot)execution.NodeResults[node.Id].Result!,true,context);Pump();
                Require(!control.TryCapture(out _),"新帧替换丢原文后误通过");SelectInput();Require(editor.PrepareInteraction!(),"新帧不能重借");var current=Session(owner,BlobLocalRoi.Inspection);Require(current.Frame!=oldFrame&&!previous.IsCurrent,"物理帧未更替或旧Session仍有效");
                first.UpdateLayout();Pump();text=Visual(editor.NativeView).OfType<TextBox>().First(item=>item.Name=="ValueText"&&item.IsVisible);Require(text.Text=="frame-raw"&&text.GetBindingExpression(TextBox.TextProperty)?.Status==BindingStatus.Active,"实际新帧未恢复原文/绑定");
                Require(text.Focus(),"新帧文本无焦点");text.SetCurrentValue(TextBox.TextProperty,baseline);Pump();Require(control.TryCapture(out var error),"新帧修正不能接纳："+error);
                var formal=node.RuntimeConfiguration;var position=history.Position;first.Close();Pump();Require(ReferenceEquals(formal,node.RuntimeConfiguration)&&history.Position==position,"取消写入正式ROI");
                var stale=previous.BeginDraw("rectangle");Require(stale is null,"关闭后旧scope还能绘制");
                SelectInput();var nextWindow=Open();var nextControl=Control(nextWindow);var nextOwner=Owner(nextControl);var nextEditor=Editor(nextOwner,BlobLocalRoi.Inspection);Require(nextEditor.PrepareInteraction!(),"后继窗口不能取得实际帧");
                var nextSession=Session(nextOwner,BlobLocalRoi.Inspection);((IDisposable)owner).Dispose();previous.Dispose();Require(nextSession.IsCurrent&&nextControl.TryCapture(out error)&&!(bool)Get(nextOwner,"HasPending"),"旧owner清理影响后继窗口或泄漏旧原文");nextWindow.Close();Pump();
            });
        }
        shell.Close();app.Shutdown();Console.WriteLine("Blob ROI本方整链候选："+_passed+"组；继承/图像mask/输出掩膜/位置未计完成，非自然鼠标验证。");return 0;
    }
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object value,string name)=>value.GetType().GetProperty(name,Flags)?.GetValue(value)??value.GetType().GetField(name,Flags)?.GetValue(value)??throw new Exception("缺真实成员："+name);
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
    static void ModelCases()
    {
        using var resources=new FrozenOutputResources();var image=resources.CreateGray8(64,64,Enumerable.Repeat((byte)200,4096).ToArray());
        var settings=new BlobSettings{IsAreaFilterEnabled=false,IsCircularityFilterEnabled=false,IsRectangularityFilterEnabled=false,OutputBoundary=false,OutputGrayImage=false};
        BlobOutput Run(RecordValue definition,IReadOnlyImage? input=null)=>new BlobSession().Execute(settings,input??image,resources,localRoi:definition);
        Case("矩形正负45度遵循X右Y下合同",()=>{foreach(var sign in new[]{1d,-1d}){using var result=Run(Root(NativeShape("rectangle",45*sign)));Require(Contains(result.Region,24,sign>0?24:16)&&!Contains(result.Region,24,sign>0?16:24),"矩形旋转或宽高方向错误");}});
        Case("非对称多边形row/col与闭合填充",()=>{using var result=Run(Root(NativeShape("polygon")));Require(Contains(result.Region,6,7)&&!Contains(result.Region,14,6),"多边形坐标转置或未填充");});
        Case("环正负扫角跨边界及正负整环和内孔",()=>
        {
            foreach(var pair in new[]{(0d,Math.PI/2,26,26,26,14),(0d,-Math.PI/2,26,14,26,26),(170*Math.PI/180,20*Math.PI/180,14,20,26,20),(170*Math.PI/180,-340*Math.PI/180,26,20,14,20),(0d,2*Math.PI,26,20,20,20),(0d,-2*Math.PI,26,20,20,20)})
            {using var result=Run(Root(NativeShape("annulus",start:pair.Item1,sweep:pair.Item2)));Require(Contains(result.Region,pair.Item3,pair.Item4)&&!Contains(result.Region,pair.Item5,pair.Item6),"环扫角或内孔错误："+pair);}
        });
        Case("扇形正扫角及负扫角明确拒绝",()=>{using var result=Run(Root(NativeShape("sector")));Require(Contains(result.Region,26,26)&&!Contains(result.Region,26,14),"扇形方向错误");var rejected=false;try{_=NativeShape("sector",sweep:-Math.PI/2);}catch(ArgumentException){rejected=true;}Require(rejected,"负sector未遵循既有中立合同拒绝");});
        Case("多屏蔽并集及非矩形实际Domain裁剪",()=>
        {
            var a=RoiGeometryEditing.Create("rectangle",new(5,5),new(13,13));var b=RoiGeometryEditing.Create("rectangle",new(16,5),new(24,13));
            var definition=BlobLocalRoi.Root(BlobLocalRoi.Group(BlobLocalRoi.FullDomain,Array.Empty<RecordValue>()),BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{BlobLocalRoi.Item(Guid.NewGuid(),"A",Stored(a)),BlobLocalRoi.Item(Guid.NewGuid(),"B",Stored(b))}));
            using var mask=Run(definition);Require(!Contains(mask.Region,9,9)&&!Contains(mask.Region,20,9)&&Contains(mask.Region,30,20),"多个屏蔽未并集减去");
            var runs=Enumerable.Range(0,30).Select(row=>new RegionRun(row,0,29-row)).ToArray();var domain=resources.CreateRegion(image.EffectiveDomain.Frame,image.Frame,runs);var cropped=resources.CreateImage(image.Layout,image.Frame,domain,image.CopyPixels());using var clipped=Run(definition,cropped);
            Require(Contains(clipped.Region,4,20)&&!Contains(clipped.Region,20,20)&&!Contains(clipped.Region,9,9),"非矩形Domain被扩大或屏蔽未生效");
        });
        Case("共线与自交多边形不静默扩成全图",()=>
        {
            foreach(var vertices in new[]{new[]{new Point2(4,4),new Point2(8,8),new Point2(12,12)},new[]{new Point2(4,4),new Point2(12,12),new Point2(4,12),new Point2(12,4)}})
            {
                try{using var result=Run(Root(RoiGeometryEditing.Create("polygon",new(0,0),new(0,0),vertices)));Require(!Contains(result.Region,30,30)&&result.TotalArea<100,"退化多边形变全域");}
                catch(HalconDotNet.HOperatorException){/* 本方HALCON明确拒绝亦可；后续有效调用验证作用域已恢复。 */}
                using var valid=Run(Root(NativeShape("rectangle")));Require(valid.TotalArea>0,"退化输入后有效执行/资源作用域不能恢复");
            }
        });
        Case("有限极端角度不溢出、半尺寸不可表示提前拒绝",()=>
        {
            var shape=Stored(NativeShape("rectangle"));var values=shape.CopyFields().ToDictionary(pair=>pair.Key,pair=>pair.Value);values["angle-degrees"]=1e308;var large=new RecordValue(BlobLocalRoi.ShapeType,values);BlobLocalRoi.ValidateShape(large);
            var root=BlobLocalRoi.Root(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{BlobLocalRoi.Item(Guid.NewGuid(),"large",large)}),BlobLocalRoi.Group(BlobLocalRoi.None,Array.Empty<RecordValue>()));using var result=Run(root);Require(result.TotalArea>0,"有限大角度物化失败");values["width"]=double.Epsilon;var rejected=false;try{BlobLocalRoi.ValidateShape(new(BlobLocalRoi.ShapeType,values));}catch(ArgumentException){rejected=true;}Require(rejected,"半尺寸下溢未提前拒绝");
        });
    }
    sealed class CheckLogger:SunEyeVision.Plugin.SDK.Logging.ILogger
    {
        public SunEyeVision.Plugin.SDK.Logging.ILogger ForSource(string source)=>this;
        public void Log(SunEyeVision.Plugin.SDK.Logging.LogLevel level,string message,string? source=null,Exception? exception=null,SunEyeVision.Plugin.SDK.Logging.LogTarget target=SunEyeVision.Plugin.SDK.Logging.LogTarget.All)
        {if(level==SunEyeVision.Plugin.SDK.Logging.LogLevel.Error)Console.Error.WriteLine(message+" "+exception);}
    }
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
            return reference.NodeId==expected.NodeId&&reference.PortId==expected.PortId&&reference.MemberFieldPath.SequenceEqual(expected.MemberFieldPath);
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
    private static void Case(string name,Action action){action();_passed++;Console.WriteLine("通过："+name);}
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
