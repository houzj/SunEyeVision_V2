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
        var scope=Environment.GetEnvironmentVariable("S4_BLOB_PREVIEW_SCOPE")??"All";
        Require(scope is "All" or "Core" or "Window" or "Lifecycle","未知预览专项");
        var resultView=new ImageResultControl{DataContext=vm.ImageResult};resultView.Initialize(vm);
        var shell=new Window{Width=1000,Height=850,Content=resultView,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();Pump();
        var node=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(node);node.DebugWindowStyle=DebugWindowStyle.Custom;
        var source=vm.WorkflowTabViewModel.SelectedTab.CreateNode("ImageLoad");
        var imageFile=Path.Combine(folder,"input.png");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,64,96,96,PixelFormats.Gray8,null,Enumerable.Repeat((byte)200,4096).ToArray(),64)));using(var file=File.Create(imageFile))png.Save(file);
        var policy=ImageSourcePolicy.Read(source)!;policy.FilePaths=new(){imageFile};policy.CurrentIndex=0;policy.ForRunPaths=new(){imageFile};policy.SNInitialValue=1;policy.ApplyTo(source);vm.AddNodeToWorkflow(source);
        Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=source.Id,TargetNodeId=node.Id}),"真实图像连线失败");
        var inputReference=((ReferenceSetting)node.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput)).Reference;var key=SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Serialize(inputReference);
        var flowId=vm.WorkflowTabViewModel.SelectedTab.RootWorkflowId;var host=solutions.Session!.Editing.Host;var history=solutions.Session.History;
        vm.SelectedNode=node;node.IsSelected=true;Pump();
        var workspace=(ResultWorkspaceShell)resultView.FindName("Workspace");workspace.IsDoubleImage=false;Pump();
        DefaultDebugWindow Open()
        {vm.OpenDebugWindowCommand.Execute(node);Pump();var window=app.Windows.OfType<DefaultDebugWindow>().Single(w=>w.IsVisible);window.UpdateLayout();Pump();return window;}
        RuntimeNodeConfigurationControl Control(DefaultDebugWindow window)=>(RuntimeNodeConfigurationControl)((ContentControl)window.FindName("ContentHost")).Content;
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
        var variables=host.BeginVariableEdit();var widthVariable=new GlobalVariable("预览宽度",new double[]{12,20},"Double[]");var indexVariable=new GlobalVariable("预览索引",0L,"Integer");var unrelated=new GlobalVariable("无关变量",1d,"Double");variables.Configuration.GlobalVariables.AddRange(new[]{widthVariable,indexVariable,unrelated});host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variables);
        var createdWidth=widthVariable;var liveWidth=solutions.CurrentSolution!.GlobalVariables.Single(variable=>variable.Id==widthVariable.Id);var createdEvents=0;var liveEvents=0;
        System.ComponentModel.PropertyChangedEventHandler createdNotice=(_,_)=>createdEvents++;System.ComponentModel.PropertyChangedEventHandler liveNotice=(_,_)=>liveEvents++;
        createdWidth.PropertyChanged+=createdNotice;liveWidth.PropertyChanged+=liveNotice;var liveBefore=liveWidth.CaptureValue();var createdBefore=createdWidth.Value;createdWidth.Value=new double[]{77,88};
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"variable-object-diagnostic.json"),System.Text.Json.JsonSerializer.Serialize(new{SameObject=ReferenceEquals(createdWidth,liveWidth),CreatedEvents=createdEvents,LiveEvents=liveEvents,CreatedRevision=createdWidth.ValueRevision,LiveRevisionBefore=liveBefore.Revision,LiveRevisionAfter=liveWidth.ValueRevision,CreatedValue=createdWidth.Value,DocumentValue=liveWidth.Value}));
        createdWidth.Value=createdBefore;createdWidth.PropertyChanged-=createdNotice;liveWidth.PropertyChanged-=liveNotice;
        widthVariable=liveWidth;indexVariable=solutions.CurrentSolution.GlobalVariables.Single(variable=>variable.Id==indexVariable.Id);unrelated=solutions.CurrentSolution.GlobalVariables.Single(variable=>variable.Id==unrelated.Id);
        var ids=BlobRoiInheritance.RectangleMembers.Select(member=>BlobRoiInheritance.Leaf(BlobRoiInheritance.RectangleParameters,member)).ToArray();
        var edit=host.BeginEdit(flowId,node.Id);var fields=new Dictionary<string,ParameterSetting>{{BlobRoiInheritance.ModeId,new ConstantSetting(BlobRoiInheritance.RectangleParameters)}};var numbers=new[]{20d,20d,12d,4d,30d};for(var i=0;i<ids.Length;i++)fields[ids[i]]=new ConstantSetting(new[]{numbers[i]});edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldSettings(fields);host.Commit(edit);
        var run=vm.RunSelectedImageAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();Pump();SelectInput();
        var path=Path.Combine(folder,"preview.solution");Require(solutions.SaveCurrentDocument(path).FileWritten,"预览夹具保存失败");
        var context=(WorkflowContext)typeof(MainWindowViewModel).GetProperty("CurrentWorkflowContext",Flags)!.GetValue(vm)!;
        DeclaredOutputSnapshot Published(string id)=>context.GetNodeResult(id)?.Result as DeclaredOutputSnapshot??throw new Exception("缺少真实publication："+id);
        var requested=ids.Concat(new[]{BlobTool.ImageInput,BlobRoiInheritance.ModeId}).ToArray();
        RuntimeInputPreviewLease CaptureInputs(Workflow targetFlow,WorkflowNodeBase target,Func<string,DeclaredOutputSnapshot?>? read=null,Action<IReadOnlyCollection<string>>? observer=null)
            =>RuntimeDebugInvocation.CaptureInputs(targetFlow,target,new ToolExecutionOwner(),host.Capture().Version,read??(id=>Published(id)),CancellationToken.None,publishedOwner:solutions.CurrentSolution!.ExecutionOwner,solutionId:solutions.CurrentSolution.Id,variables:solutions.CurrentSolution.GlobalVariables,inputFieldIds:requested,observeVariables:observer);
        var selection=new VariableSetting(new VariableReference(solutions.CurrentSolution!.Id,widthVariable.Id,DataType.NumberArray,new BindingSelection.AtVariable(new VariableReference(solutions.CurrentSolution.Id,indexVariable.Id,DataType.Integer))));
        if(scope is "All" or "Core")
        {
            Case("只解析目标活动输入，不运行算法不读取无关坏参数",()=>{
                var privateNode=node.Clone();privateNode.RuntimeConfiguration=privateNode.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{ids[2],selection},{BindingId("LowThreshold"),new ReferenceSetting(new OutputReference("missing","output",DataType.Integer,scopeId:flowId))}});
                var formal=System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration);var position=history.Position;var publication=Published(node.Id);using var lease=CaptureInputs(flow,privateNode);
                Require(lease.IsCurrent&&lease.Inputs.Read(ids[2]).Count==1&&lease.Inputs.Read(ids[2]).GetElement(0).GetScalar<double>()==12,"目标输入未按冻结索引解析");Require(ReferenceEquals(publication,Published(node.Id))&&history.Position==position&&System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration)==formal,"只读解析运行算法或写配置历史");
                privateNode.RuntimeConfiguration=privateNode.RuntimeConfiguration.WithFieldSettings(new Dictionary<string,ParameterSetting>{{BlobLocalRoi.InputId,new ConstantSetting(BlobLocalRoi.Empty)}});Require(lease.IsCurrent,"无关本地mask配置替换误退役目标快照");privateNode.RuntimeConfiguration=privateNode.RuntimeConfiguration.WithFieldSettings(new Dictionary<string,ParameterSetting>{{ids[0],new ConstantSetting(new[]{25d})}});Require(!lease.IsCurrent,"相关端口变化未退役");
            });
            Case("捕获中变量值修订重入拒绝旧值新revision拼接并可恢复",()=>{
                var privateNode=node.Clone();privateNode.RuntimeConfiguration=privateNode.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{ids[2],selection}});var changed=false;var rejected=false;try{using var lease=CaptureInputs(flow,privateNode,id=>{if(!changed){changed=true;var before=widthVariable.CaptureValue();widthVariable.Value=new double[]{16,20};File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"capture-variable-order.json"),System.Text.Json.JsonSerializer.Serialize(new{Stage="readPublished callback after CaptureDefinitions",SameDocumentObject=ReferenceEquals(widthVariable,solutions.CurrentSolution!.GlobalVariables.Single(variable=>variable.Id==widthVariable.Id)),BeforeRevision=before.Revision,AfterRevision=widthVariable.ValueRevision,BeforeValue=before.Value,AfterValue=widthVariable.Value}));}return Published(id);});}catch(InvalidOperationException error){Console.WriteLine("变量重入诊断："+error.Message);rejected=error.Message.Contains("变化");}Require(changed&&rejected,"捕获期间变值没有拒绝");using var recovered=CaptureInputs(flow,privateNode);Require(recovered.IsCurrent&&recovered.Inputs.Read(ids[2]).GetElement(0).GetScalar<double>()==16,"捕获失败后未恢复");widthVariable.Value=new double[]{12,20};
            });
            Case("捕获中来源配置改变拒绝，资源清理后可再次取得快照",()=>{
                var copy=new SolutionRepository().LoadCandidate(path).Candidate!;try{var copyFlow=copy.Workflows.Single(item=>item.Id==flowId);var copyNode=copyFlow.Nodes.Single(item=>item.Id==node.Id);var copySource=copyFlow.Nodes.Single(item=>item.Id==source.Id);var changed=false;var rejected=false;try{using var lease=CaptureInputs(copyFlow,copyNode,id=>{if(!changed){changed=true;copySource.RuntimeConfiguration=copySource.RuntimeConfiguration!.Copy();}return Published(id);});}catch(InvalidOperationException error){Console.WriteLine("来源重入诊断："+error.Message);rejected=error.Message.Contains("变化");}Require(rejected,"publication取证期间来源配置变化未拒绝");using var restored=CaptureInputs(copyFlow,copyNode);Require(restored.IsCurrent,"失败后快照reader不可恢复");}finally{copy.ExecutionOwner.Retire();}
            });
            Case("监听安装期间变值按安装后的原子快照取值",()=>{
                var privateNode=node.Clone();privateNode.RuntimeConfiguration=privateNode.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{ids[2],selection}});var installed=false;using var lease=CaptureInputs(flow,privateNode,observer:requestedIds=>{Require(requestedIds.Contains(widthVariable.Id)&&requestedIds.Contains(indexVariable.Id)&&!requestedIds.Contains(unrelated.Id),"通知依赖集合不精确");installed=true;indexVariable.Value=1L;});Require(installed&&lease.IsCurrent&&lease.Inputs.Read(ids[2]).GetElement(0).GetScalar<double>()==20,"监听安装期读取了旧值或新旧revision拼接");indexVariable.Value=0L;
            });
            Case("关闭回调结构异常仍逐项释放reader且幂等",()=>{
                var lease=CaptureInputs(flow,node.Clone());var release=(Action)Get(lease,"_release");var invocation=(RuntimeDebugInvocation)release.Target!;var readers=(List<IDisposable>)Get(invocation,"_readers");var first=new Spy(true);var last=new Spy(false);readers.Add(first);readers.Add(last);
                typeof(ToolExecutionIdentity).GetField("_attempt",Flags)!.SetValue(invocation.Identity,null);var threw=false;try{lease.Dispose();}catch(AggregateException error){Console.WriteLine("清理故障注入："+error);threw=true;}Require(threw&&first.Count==1&&last.Count==1&&readers.Count==0,"CompleteCallbacks故障跳过reader释放");lease.Dispose();Require(first.Count==1&&last.Count==1,"重复Dispose二次释放");
            });
        }
        if(scope is "All" or "Window" or "Lifecycle")
        {
            var window=Open();var control=Control(window);var owner=Owner(control);var formal=System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration);var position=history.Position;var bytes=File.ReadAllBytes(path);var previousPublication=Published(node.Id);
            var viewport=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultViewport)Get(Get(Get(resultView,"_workspacePresenter"),"OutputPane"),"Viewport");
            object Layer(){try{PumpUntil(()=>Maybe(owner,"_previewLayer") is not null);}catch{throw new Exception("预览未就绪："+Get(owner,"PreviewError"));}return Get(owner,"_previewLayer");}
            DeclaredFieldEditor Width()=>Logical(control).OfType<SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredInputFieldsView>().Single().Fields.Single(field=>field.FieldId==ids[2]);
            void Apply(DeclaredFieldEditor field,ParameterSetting setting){Require((bool)field.GetType().GetMethod("TryApplySetting",Flags)!.Invoke(field,new[]{setting})!,"正式字段接纳失败："+field.Error);Pump();}
            object Prepared(object layer)=>Get(layer,"_prepared");
            object[] Shapes(object layer)=>(object[])Get(Prepared(layer),"Items");
            Case("真实单图只读原矩形可见，打开不执行不写字段历史文件",()=>{
                var layer=Layer();Require(Shapes(layer).Length==1&&Shapes(layer)[0].GetType().Name.Contains("Rectangle"),"未走原矩形renderer");Require(viewport.CurrentFrame!.Frame==((NativeRoiFrameLease)Get(owner,"_frame")).Image.Frame,"实际视口帧不匹配");Require(formal==System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration)&&position==history.Position&&ReferenceEquals(previousPublication,Published(node.Id))&&bytes.SequenceEqual(File.ReadAllBytes(path)),"打开预览有副作用");Capture(viewport.View,"preview-rectangle.png");
            });
            Case("同尺寸异物理帧的receipt明确拒绝，不以尺寸映射",()=>{
                var active=Layer();var frame=(NativeRoiFrameLease)Get(owner,"_frame");using var rejected=frame.CreateReadOnlyLayer();var rows=((NativeRoiSession)Get(active,"_projection")).Items.Select(item=>new RoiEditItem(item.Group,item.Id,"测试只读",(RecordValue)Get(item,"AcceptedGeometry"),false,true)).ToArray();var wrong=frame.Receipt with{Frame=new ImageFrameAddress(Guid.NewGuid(),"fault-same-size")};var seen=false;try{rejected.Present(wrong,1,rows,()=>true);}catch(ArgumentException error){seen=error.Message.Contains("同一实际帧");}Require(seen&&ReferenceEquals(active,Layer())&&frame.IsCurrent,"相同64×64错误帧被绘制或影响真实scope");
            });
            Case("独立只读检测与实际mask附件同屏，不可删除复制或作为历史目标",()=>{
                var layer=Layer();Draw(owner,BlobLocalRoi.Mask,"rectangle");Require(ReferenceEquals(layer,Layer()),"mask创建误退役检测预览");var projection=(NativeRoiSession)Get(layer,"_projection");Require(projection.Items.Count==1&&projection.Items.All(item=>item.Visible&&!item.Enabled)&&!projection.CanCopy(projection.Items[0])&&!projection.CanDelete(projection.Items),"只读形状可编辑/复制/删除");var mask=Session(owner,BlobLocalRoi.Mask);Require(mask.Items.Any(item=>item.Group==BlobLocalRoi.Mask&&item.Enabled)&&((System.Collections.IEnumerable)Get(viewport.View,"ROIList")).Cast<object>().Any(item=>mask.Items.Any(roi=>ReferenceEquals(roi,item))),"mask原编辑层未实际同屏");Require((int)Get(layer,"Order")<50,"只读层覆盖mask层顺序");Capture(viewport.View,"preview-with-mask.png");
            });
            Apply(Width(),selection);var activeLayer=Layer();
            Case("无关变量不刷新，相关值和动态索引更新实际原图形",()=>{
                unrelated.Value=2d;Pump();Require(ReferenceEquals(activeLayer,Layer()),"无关变量清了检测层");widthVariable.Value=new double[]{16,20};Pump();var next=Layer();Require(!ReferenceEquals(activeLayer,next)&&(double)Get(Shapes(next)[0],"Width")==16,"相关值未刷新实际矩形");indexVariable.Value=1L;Pump();Require((double)Get(Shapes(Layer())[0],"Width")==20,"动态索引未刷新");
            });
            Case("mask非法原文和活动绘制在只读源刷新后保持",()=>{
                var editor=Editor(owner,BlobLocalRoi.Mask);var maskSession=Session(owner,BlobLocalRoi.Mask);File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mask-before-expand.json"),System.Text.Json.JsonSerializer.Serialize(new{Selected=maskSession.CurROIData?.Id,Rows=maskSession.Items.Select(item=>new{item.Id,item.Group,item.Enabled,item.Visible}),TextBoxes=Visual(editor.NativeView).OfType<TextBox>().Select(item=>new{item.Name,item.IsVisible,item.Visibility,item.Text})}));Require(editor.PrepareInteraction!(),"mask实际帧不能激活");var selected=maskSession.Items.First(item=>item.Group==BlobLocalRoi.Mask&&item.Enabled);Require(maskSession.Select(selected.Group,selected.Id),"mask真实形状不能选择");var expand=Visual(editor.NativeView).OfType<ToggleButton>().Single(button=>button.Name=="expand");expand.SetCurrentValue(ToggleButton.IsCheckedProperty,true);window.UpdateLayout();Pump();var text=Visual(editor.NativeView).OfType<TextBox>().First(item=>item.Name=="ValueText"&&item.IsVisible);var accepted=text.Text;Require(text.Focus()&&text.IsKeyboardFocused,"原mask数字文本未获得真实WPF键盘焦点");text.SetCurrentValue(TextBox.TextProperty,"mask-pending");Pump();File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"mask-pending-events.json"),System.Text.Json.JsonSerializer.Serialize(new{text.Text,text.IsKeyboardFocused,FocusIsText=ReferenceEquals(Keyboard.FocusedElement,text),Binding=text.GetBindingExpression(TextBox.TextProperty)?.Status.ToString(),editor.HasPendingEdits,Inputs=((System.Collections.IEnumerable)Get(editor,"_inputs")).Cast<object>().Select(input=>new{Raw=Maybe(input,"Raw"),Baseline=Maybe(input,"Baseline"),SameText=ReferenceEquals(Get(input,"Text"),text)})}));Require(editor.HasPendingEdits,"未产生真实mask待提交原文");widthVariable.Value=new double[]{18,22};Pump();_=Layer();Require(text.Text=="mask-pending"&&editor.HasPendingEdits&&text.GetBindingExpression(TextBox.TextProperty)?.Status==BindingStatus.Active,"只读刷新丢失mask原文/绑定");text.SetCurrentValue(TextBox.TextProperty,accepted);Require(editor.TryCommitPending(),editor.Error);
                ((ICommand)Get(editor.NativeView.DataContext,"ValueChangedCommand")).Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box);Pump();var session=Session(owner,BlobLocalRoi.Mask);var draw=session.ActiveDraw??throw new Exception("mask未进入真实绘制");var attachment=(SunEyeVision.Plugin.Wpf.Presenters.Display.INativeRoiEditLease)Get(owner,"_interaction");widthVariable.Value=new double[]{20,24};Pump();_=Layer();Require(ReferenceEquals(session.ActiveDraw,draw)&&attachment.HasPendingGesture,"只读刷新取消mask手势");attachment.CancelGesture();Pump();
            });
            Case("空值与坏索引清本层，恢复后重新预览且不重写来源",()=>{
                var before=SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Serialize(((VariableSetting)Width().Setting).Reference);indexVariable.Value=9L;Pump();Require(Maybe(owner,"_previewLayer") is null&&((string)Get(owner,"PreviewError")).Length>0,"越界索引没有清预览");indexVariable.Value=0L;Pump();_=Layer();widthVariable.Value=Array.Empty<double>();Pump();Require(Maybe(owner,"_previewLayer") is null,"空序列沿用旧预览");widthVariable.Value=new double[]{12,20};Pump();_=Layer();Require(SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Serialize(((VariableSetting)Width().Setting).Reference)==before,"恢复改写了来源身份");
            });
            Case("圆环六叶原只读renderer保内外半径和负扫角，缩放不改mask附件",()=>{
                var ui=Logical(control).OfType<SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiUiDraftControls>().Single();var radio=((System.Collections.IEnumerable)Get(ui,"_radios")).Cast<object>().ElementAt(2);((ICommand)Get(radio,"ValueChangedCommand")).Execute(3);Pump();var ringIds=BlobRoiInheritance.AnnulusMembers.Select(member=>BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters,member)).ToArray();var values=new[]{20d,20d,3d,10d,170d,-20d};var all=Logical(control).OfType<SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredInputFieldsView>().Single();for(var i=0;i<ringIds.Length;i++)Require(all.Fields.Single(field=>field.FieldId==ringIds[i]).TrySetConstant(values[i]),"圆环六叶不能逐项输入");Pump();var layer=Layer();var ring=Shapes(layer).Single();var captured=(NativeRoiInputLease)Get(owner,"_previewInputs");var resolved=ringIds.Select(id=>new{Id=id,Values=Enumerable.Range(0,captured.Inputs.Read(id).Count).Select(index=>captured.Inputs.Read(id).GetElement(index).GetScalar<double>()).ToArray()}).ToArray();var materialized=BlobRoiInheritance.Materialize(captured.Inputs,BlobLocalRoi.Empty)!;var projected=(NativeRoiSession)Get(layer,"_projection");File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"annulus-three-levels.json"),System.Text.Json.JsonSerializer.Serialize(new{Mode=captured.Inputs.Get<string>(BlobRoiInheritance.ModeId),Resolved=resolved,Stored=BlobLocalRoi.Items(materialized,BlobLocalRoi.Inspection).Select(item=>item.Get<RecordValue>("shape").CopyFields()),Projected=projected.Items.Select(item=>((RecordValue)Get(item,"AcceptedGeometry")).CopyFields()),Renderer=new{Type=ring.GetType().FullName,Start=Get(ring,"StartAngle"),Range=Get(ring,"AngleRange"),Inner=Get(ring,"InnerRadius"),Outer=Get(ring,"OuterRadius")}}));Require(ring is SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeInheritedAnnulusProjection&&Math.Abs((double)Get(ring,"StartAngle")-150)<1e-10&&Math.Abs((double)Get(ring,"AngleRange")-20)<1e-10&&(double)Get(ring,"InnerRadius")==3&&(double)Get(ring,"OuterRadius")==10,"圆环原renderer角度/半径未保留");var nativeRing=(SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface.IAnnularSector)ring;var originalShape=SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape.GetAnnularGeometry(nativeRing,64,64,1,1);var arc=(PathGeometry)Get(originalShape,"geometry");Point At(double radius,double degrees)=>new(20+radius*Math.Cos(degrees*Math.PI/180),20+radius*Math.Sin(degrees*Math.PI/180));Require(arc.FillContains(At(6,160))&&!arc.FillContains(At(6,0))&&!arc.FillContains(new Point(20,20))&&!arc.FillContains(At(11,160))&&!originalShape.IsHitTestVisible,"原renderer未保留负扫对应弧域/内孔或变为可命中");Require(projected.Items.Single().GetType()==typeof(NativeInheritedAnnulusProjection)&&((RecordValue)Get(projected.Items.Single(),"AcceptedGeometry")).Get<double>("start-radians")==BlobLocalRoi.Items(materialized,BlobLocalRoi.Inspection).Single().Get<RecordValue>("shape").Get<double>("start-radians"),"原始record被正扫显示改写");var attachment=Get(owner,"_interaction");var view=(SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.ImageViewBox)viewport.View;view.UpdateLayout();Pump();var actualRing=Visual(view).OfType<SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>().Single(shape=>shape.IsVisible);var beforeScale=view.ImageScale;var beforeBounds=actualRing.TransformToAncestor(view).TransformBounds(actualRing.RenderedGeometry.Bounds);var beforeRatio=view.ControlToImageRadio;view.ChangeSize(new Point(view.ActualWidth/2,view.ActualHeight/2),1.2);view.UpdateLayout();Pump();actualRing=Visual(view).OfType<SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>().Single(shape=>shape.IsVisible);var afterBounds=actualRing.TransformToAncestor(view).TransformBounds(actualRing.RenderedGeometry.Bounds);File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"original-zoom.json"),System.Text.Json.JsonSerializer.Serialize(new{BeforeScale=beforeScale,AfterScale=view.ImageScale,BeforeRatio=beforeRatio,AfterRatio=view.ControlToImageRadio,BeforeBounds=beforeBounds.ToString(),AfterBounds=afterBounds.ToString(),view.ThicknessScale,actualRing.IsHitTestVisible}));Require(Math.Abs(view.ImageScale/beforeScale-1.2)<1e-10&&afterBounds.Width>beforeBounds.Width*1.1&&!actualRing.IsHitTestVisible,"原ChangeSize未实际缩放原环图形或变为可命中");Require(ReferenceEquals(attachment,Get(owner,"_interaction"))&&ReferenceEquals(layer,Layer()),"缩放替换mask附件或检测层");Capture(view,"preview-annulus-with-mask.png");Require(all.Fields.Single(field=>field.FieldId==ringIds[5]).TrySetConstant(360d),"整环不可输入");Pump();Require((double)Get(Shapes(Layer()).Single(),"AngleRange")==360,"整环被丢成非完整环");((ICommand)Get(radio,"ValueChangedCommand")).Execute(1);Pump();_=Layer();
            });
            Case("正式History删除和Undo恢复变量，旧对象通知不得安装旧层",()=>{
                var before=Width().Setting;var editVariables=host.BeginVariableEdit();editVariables.Configuration.GlobalVariables.RemoveAll(variable=>variable.Id==widthVariable.Id);host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),editVariables);Pump();Require(Maybe(owner,"_previewLayer") is null,"正式删除没有清层");history.Undo();Pump();_=Layer();var restored=solutions.CurrentSolution!.GlobalVariables.Single(variable=>variable.Id==widthVariable.Id);Require(Width().Setting is VariableSetting&&SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Serialize(((VariableSetting)Width().Setting).Reference)==SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Serialize(((VariableSetting)before).Reference),"Undo改来源");var current=Layer();if(!ReferenceEquals(restored,widthVariable)){widthVariable.Value=new double[]{99};Pump();Require(ReferenceEquals(current,Layer()),"旧对象迟到通知重建了新层");}widthVariable=restored;
            });
            Case("正式History索引删除Undo及同ID提案经SetValue更新正式对象",()=>{
                var oldIndex=indexVariable;var variableEdit=host.BeginVariableEdit();variableEdit.Configuration.GlobalVariables.RemoveAll(variable=>variable.Id==oldIndex.Id);host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variableEdit);Pump();Require(Maybe(owner,"_previewLayer") is null,"索引变量删除没有清层");history.Undo();Pump();_=Layer();indexVariable=solutions.CurrentSolution!.GlobalVariables.Single(variable=>variable.Id==oldIndex.Id);var stable=Layer();if(!ReferenceEquals(oldIndex,indexVariable)){oldIndex.Value=9L;Pump();Require(ReferenceEquals(stable,Layer()),"旧索引对象迟到通知影响新层");}
                var oldWidth=widthVariable;variableEdit=host.BeginVariableEdit();var at=variableEdit.Configuration.GlobalVariables.FindIndex(variable=>variable.Id==oldWidth.Id);variableEdit.Configuration.GlobalVariables[at]=new GlobalVariable("同ID替换",oldWidth.Value,"Double[]"){Id=oldWidth.Id};variableEdit.SetValue(oldWidth.Id,new double[]{14,24});host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variableEdit);Pump();widthVariable=solutions.CurrentSolution.GlobalVariables.Single(variable=>variable.Id==oldWidth.Id);Require((double)Get(Shapes(Layer()).Single(),"Width")==14,"正式同ID替换未刷新");File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"same-id-variable.json"),System.Text.Json.JsonSerializer.Serialize(new{PreservedFormalInstance=ReferenceEquals(oldWidth,widthVariable),widthVariable.ValueRevision,widthVariable.Value}));history.Undo();Pump();widthVariable=solutions.CurrentSolution.GlobalVariables.Single(variable=>variable.Id==oldWidth.Id);_=Layer();
            });
            Case("同引用新publication与同尺寸新物理帧退役旧层并恢复",()=>{
                var oldLayer=Layer();var oldInputs=(NativeRoiInputLease)Get(owner,"_previewInputs");var oldFrame=((NativeRoiFrameLease)Get(owner,"_frame")).Image.Frame;var oldPublication=Published(source.Id);var executor=new WorkflowExecutionEngine((WorkflowEngine)Get(vm,"_workflowEngine"),plugins,SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);var document=solutions.CurrentSolution!;var request=new WorkflowRunRequest(flowId,null,hostInputs:new Dictionary<string,SunEyeVision.Plugin.Contracts.IHostInputPreparer>{{source.Id,new ImageSourceHostInputPreparer(imageFile,1){LocalReadAdmission=(_,_)=>imageFile}}}){Configuration=WorkflowRunConfiguration.Capture(document.Workflows),ExecutionOwner=document.ExecutionOwner,ResourcePaths=SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(document)};var task=executor.ExecuteWorkflowAsync(request);PumpUntil(()=>task.IsCompleted);using var execution=task.GetAwaiter().GetResult();Require(execution.Success,"新帧前置执行失败");var results=(SunEyeVision.UI.Services.Workflow.NodeResultManager)Get(vm,"_nodeResultManager");results.UpdateNodeResult(source,(DeclaredOutputSnapshot)execution.NodeResults[source.Id].Result!,false,context);results.UpdateNodeResult(node,(DeclaredOutputSnapshot)execution.NodeResults[node.Id].Result!,true,context);Pump();Require(!ReferenceEquals(oldPublication,Published(source.Id))&&Maybe(oldLayer,"_owner") is null&&!oldInputs.IsCurrent,"新publication未退役旧层");SelectInput();_=Layer();var nextFrame=((NativeRoiFrameLease)Get(owner,"_frame")).Image;Require(nextFrame.Frame!=oldFrame&&nextFrame.Width==64&&nextFrame.Height==64,"同尺寸物理帧没有真实更替");oldInputs.Dispose();Require(Maybe(owner,"_previewLayer") is not null,"旧输入lease清掉新层");
            });
            Case("选错图清只读层但不选图，选回输入恢复同帧",()=>{
                var wrong=workspace.OutputItems.Cast<object>().First(item=>(string)Get(item,"Key")!=key);var oldLayer=Layer();var oldFrame=(NativeRoiFrameLease)Get(owner,"_frame");var oldInputs=(NativeRoiInputLease)Get(owner,"_previewInputs");var changes=0;var frameChanges=0;var notices=new List<object>();var dp=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ResultWorkspaceShell.SelectedOutputItemProperty,typeof(ResultWorkspaceShell));EventHandler changed=(_,_)=>{changes++;notices.Add(new{Phase="selected-dp",Key=workspace.SelectedOutputItem is null?null:Get(workspace.SelectedOutputItem,"Key"),Receipt=viewport.CurrentFrame?.ToString()});};var frames=(NativeRoiFrameSource)Get(control,"_roiFrames");Action frameChanged=()=>frameChanges++;dp.AddValueChanged(workspace,changed);frames.Changed+=frameChanged;
                try{notices.Add(new{Phase="before",ExpectedWrongKey=Get(wrong,"Key"),SelectedKey=Get(workspace.SelectedOutputItem!,"Key"),Receipt=viewport.CurrentFrame?.ToString()});workspace.SelectedOutputItem=wrong;Pump();notices.Add(new{Phase="after",ActualKey=workspace.SelectedOutputItem is null?null:Get(workspace.SelectedOutputItem,"Key"),SameWrapper=ReferenceEquals(workspace.SelectedOutputItem,wrong),Receipt=viewport.CurrentFrame?.ToString(),LayerIsNull=Maybe(owner,"_previewLayer") is null,OldLayerOwned=Maybe(oldLayer,"_owner") is not null,Prepared=Maybe(oldLayer,"_prepared") is not null,FrameCurrent=oldFrame.IsCurrent,InputCurrent=oldInputs.IsCurrent,changes,frameChanges,VisibleShapes=(Maybe(viewport.View,"ShapeList") as System.Collections.IEnumerable)?.Cast<object>().Select(item=>item.GetType().Name).ToArray(),PreviewError=Get(owner,"PreviewError")});File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"wrong-image-trace.json"),System.Text.Json.JsonSerializer.Serialize(notices));Require(Maybe(owner,"_previewLayer") is null&&Maybe(oldInputs,"_release") is null&&Maybe(oldFrame,"_release") is null&&workspace.SelectedOutputItem is not null&&(string)Get(workspace.SelectedOutputItem,"Key")==((string)Get(wrong,"Key")),"错图未释放本次预览/frame或改变实际选择");}finally{dp.RemoveValueChanged(workspace,changed);frames.Changed-=frameChanged;}SelectInput();Pump();_=Layer();
            });
            Case("单双图切换正确换pane，无关pane选择不打断mask手势",()=>{
                var old=Layer();SelectInput(true);_=Layer();Require(Maybe(old,"_owner") is null,"进入双图未退役输出层");var frame=(NativeRoiFrameLease)Get(owner,"_frame");var inputViewport=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultViewport)Get(Get(Get(resultView,"_workspacePresenter"),"InputPane"),"Viewport");Require(frame.Receipt.OwnerId==inputViewport.CurrentFrame!.OwnerId,"双图借的不是实际输入pane");var editor=Editor(owner,BlobLocalRoi.Mask);Require(editor.PrepareInteraction!(),"双图mask不能激活");((ICommand)Get(editor.NativeView.DataContext,"ValueChangedCommand")).Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box);Pump();var session=Session(owner,BlobLocalRoi.Mask);var draw=session.ActiveDraw??throw new Exception("双图mask绘制未开始");var stable=Layer();var right=workspace.OutputItems.Cast<object>().First(item=>(string)Get(item,"Key")!=key);workspace.SelectedOutputItem=right;Pump();Require(frame.IsCurrent&&ReferenceEquals(stable,Layer())&&ReferenceEquals(draw,session.ActiveDraw),"无关右输出选择退役左输入mask");((SunEyeVision.Plugin.Wpf.Presenters.Display.INativeRoiEditLease)Get(owner,"_interaction")).CancelGesture();SelectInput();_=Layer();Require(Maybe(stable,"_owner") is null,"回单图未退役输入层");frame=(NativeRoiFrameLease)Get(owner,"_frame");editor=Editor(owner,BlobLocalRoi.Mask);Require(editor.PrepareInteraction!(),"单图mask不能激活");((ICommand)Get(editor.NativeView.DataContext,"ValueChangedCommand")).Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box);Pump();session=Session(owner,BlobLocalRoi.Mask);draw=session.ActiveDraw??throw new Exception("单图mask绘制未开始");stable=Layer();workspace.SelectedInputItem=workspace.InputItems.Cast<object>().Single(item=>(string)Get(item,"Key")==key);Pump();Require(frame.IsCurrent&&ReferenceEquals(stable,Layer())&&ReferenceEquals(draw,session.ActiveDraw),"单图隐藏input选择打断实际output mask");workspace.SelectedInputItem=null;Pump();Require(frame.IsCurrent&&ReferenceEquals(draw,session.ActiveDraw),"隐藏input清空打断实际output mask");((SunEyeVision.Plugin.Wpf.Presenters.Display.INativeRoiEditLease)Get(owner,"_interaction")).CancelGesture();
            });
            Case("隐藏释放frame与输入reader，显示恢复mask原文和两层",()=>{
                var editor=Editor(owner,BlobLocalRoi.Mask);Require(editor.PrepareInteraction!(),"mask不能激活");var mask=Session(owner,BlobLocalRoi.Mask);var row=mask.Items.First(item=>item.Group==BlobLocalRoi.Mask&&item.Enabled);Require(mask.Select(row.Group,row.Id),"mask不能选择");var expand=Visual(editor.NativeView).OfType<ToggleButton>().Single(button=>button.Name=="expand");expand.SetCurrentValue(ToggleButton.IsCheckedProperty,true);window.UpdateLayout();Pump();var text=Visual(editor.NativeView).OfType<TextBox>().First(item=>item.Name=="ValueText"&&item.IsVisible);var baseline=text.Text;Require(text.Focus()&&text.IsKeyboardFocused,"隐藏前原mask文本不能聚焦");text.SetCurrentValue(TextBox.TextProperty,"hidden-mask-pending");Pump();Require(editor.HasPendingEdits,"隐藏前未产生pending");var oldLayer=Layer();var frame=(NativeRoiFrameLease)Get(owner,"_frame");var inputs=(NativeRoiInputLease)Get(owner,"_previewInputs");window.Hide();Pump();Require(Maybe(owner,"_frame") is null&&Maybe(owner,"_previewInputs") is null&&Maybe(oldLayer,"_owner") is null&&Maybe(frame,"_release") is null&&Maybe(inputs,"_release") is null&&(bool)Get(owner,"HasPending"),"隐藏未释放读者或丢mask原文");Require(editor.PrepareInteraction!()==false&&Maybe(owner,"_frame") is null,"隐藏期间隐式重借帧");window.Show();window.UpdateLayout();Pump();_=Layer();editor=Editor(owner,BlobLocalRoi.Mask);mask=Session(owner,BlobLocalRoi.Mask);Require(mask.Items.Any(item=>item.Group==BlobLocalRoi.Mask&&item.Visible)&&Maybe(owner,"_interaction") is not null,"显示未恢复同屏mask附件");text=Visual(editor.NativeView).OfType<TextBox>().First(item=>item.Name=="ValueText"&&item.IsVisible);Require(text.Text=="hidden-mask-pending"&&text.GetBindingExpression(TextBox.TextProperty)?.Status==BindingStatus.Active&&!control.TryCapture(out _),"显示后原文/绑定/确认保护丢失");Require(text.Focus(),"恢复原文不能聚焦");text.SetCurrentValue(TextBox.TextProperty,baseline);Require(editor.TryCommitPending(),editor.Error);
            });
            Case("切图在实际mask绘制中先记录再取消，恢复后仍拒绝未完成手势",()=>{
                var editor=Editor(owner,BlobLocalRoi.Mask);Require(editor.PrepareInteraction!(),"mask不能激活");((ICommand)Get(editor.NativeView.DataContext,"ValueChangedCommand")).Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box);Pump();var session=Session(owner,BlobLocalRoi.Mask);var request=session.ActiveDraw??throw new Exception("切图前没有真实绘制请求");var retired=(NativeRoiFrameLease)Get(owner,"_frame");var wrong=workspace.OutputItems.Cast<object>().First(item=>(string)Get(item,"Key")!=key);workspace.SelectedOutputItem=wrong;Pump();Require(session.ActiveDraw is null&&Maybe(owner,"_frame") is null&&(bool)Get(owner,"HasPending"),"切图先取消丢失未完成手势");SelectInput();_=Layer();Require(!control.TryCapture(out _)&&(bool)Get(owner,"HasPending"),"选回后未完成手势被误确认");window.Close();Pump();window=Open();control=Control(window);owner=Owner(control);_=Layer();Require(!(bool)Get(owner,"HasPending"),"主动取消关窗后旧手势污染新owner");retired.Dispose();Require(Maybe(owner,"_previewLayer") is not null,"旧帧释放损坏新owner");Draw(owner,BlobLocalRoi.Mask,"rectangle");
            });
            Case("失效Prepare及Start迟到回调先记录，显式取消不记退役",()=>{
                foreach(var phase in new[]{"PrepareDrawing","StartDrawing","NativeCancel"})
                {
                    var editor=Editor(owner,BlobLocalRoi.Mask);Require(editor.PrepareInteraction!(),"mask不能激活");((ICommand)Get(editor.NativeView.DataContext,"ValueChangedCommand")).Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box);Pump();var session=Session(owner,BlobLocalRoi.Mask);var request=session.ActiveDraw??throw new Exception("迟到回调前无绘制请求");var attachment=Get(owner,"_interaction");var proof=attachment.GetType().GetField("_current",Flags)!;proof.SetValue(attachment,(Func<bool>)(()=>false));
                    if(phase=="PrepareDrawing")attachment.GetType().GetMethod(phase,Flags)!.Invoke(attachment,new object[]{session,request});
                    else if(phase=="StartDrawing"){var page=Visual(viewport.View).OfType<SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROIItemsPage>().Single();attachment.GetType().GetMethod(phase,Flags)!.Invoke(attachment,new object[]{page,new Point(10,10)});}
                    else ((Action)Get(viewport.View,"NativeDrawCancel"))();
                    Require(session.ActiveDraw is null&&(bool)Get(owner,"HasPending")&&!control.TryCapture(out _),"失效自动取消抹掉未完成状态："+phase);window.Close();Pump();window=Open();control=Control(window);owner=Owner(control);_=Layer();Draw(owner,BlobLocalRoi.Mask,"rectangle");
                }
                var currentEditor=Editor(owner,BlobLocalRoi.Mask);((ICommand)Get(currentEditor.NativeView.DataContext,"ValueChangedCommand")).Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box);Pump();((SunEyeVision.Plugin.Wpf.Presenters.Display.INativeRoiEditLease)Get(owner,"_interaction")).CancelGesture();Pump();Require(!(bool)Get(owner,"HasPending"),"显式用户取消被误记退役");
            });
            Case("原End提交command重入新gesture后抛错保留新暂存及正常替换取消",()=>{
                var editor=Editor(owner,BlobLocalRoi.Mask);Require(editor.PrepareInteraction!(),"mask不能激活");var session=Session(owner,BlobLocalRoi.Mask);var row=session.Items.First(item=>item.Group==BlobLocalRoi.Mask&&item.Enabled);Require(session.Select(row.Group,row.Id),"mask不能选中");var native=Visual(viewport.View).OfType<SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI.RectROI>().Single(item=>ReferenceEquals(item.DataContext,row));var handle=(FrameworkElement)Get(native,"move");var baseType=typeof(SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI.ROIControlBase);var prepare=baseType.GetMethod("PrepareRoiGesture",Flags)!;object Prepare()=>prepare.Invoke(native,new object?[]{handle,(Action)(()=>{}),null})??throw new Exception("原gesture未安装");var previous=Prepare();row.CenterX+=0.02;Require(row.HasPending,"原暂存未产生");_=Prepare();Require(!row.HasPending,"正常替换旧gesture未取消旧暂存");((SunEyeVision.Plugin.Wpf.Presenters.Display.INativeRoiEditLease)Get(owner,"_interaction")).CancelGesture();previous=Prepare();row.CenterX+=0.02;var command=native.DataChangedCommand;Require(command is not null,"原ROI提交命令未绑定");previous.GetType().GetMethod("End",Flags)!.Invoke(previous,new object[]{true});Require(!row.HasPending&&!(bool)Get(owner,"HasPending"),"有效原End没有正常提交暂存");previous=Prepare();row.CenterX+=0.02;object? newer=null;var called=false;native.SetCurrentValue(baseType.GetField("DataChangedCommandProperty")!.GetValue(null) as DependencyProperty??throw new Exception("原命令DP缺失"),new ActionCommand(_=>{called=true;newer=Prepare();row.CenterX+=0.03;throw new InvalidOperationException("gesture-command-marker");}));var failed=false;try{previous.GetType().GetMethod("End",Flags)!.Invoke(previous,new object[]{true});}catch(Exception error){Console.WriteLine("原End命令异常注入："+error);failed=error.ToString().Contains("gesture-command-marker");}finally{native.SetCurrentValue(SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI.ROIControlBase.DataChangedCommandProperty,command);}Require(called&&failed&&newer is not null&&ReferenceEquals(Maybe(native,"ActiveRoiGesture"),newer)&&row.HasPending,"旧End清理覆盖重入新gesture或吞原命令异常");((SunEyeVision.Plugin.Wpf.Presenters.Display.INativeRoiEditLease)Get(owner,"_interaction")).CancelGesture();Pump();Require(!row.HasPending&&!(bool)Get(owner,"HasPending"),"正常主动取消未恢复");
            });
            Case("lost-capture proof同步安装新gesture不清新暂存",()=>{
                var editor=Editor(owner,BlobLocalRoi.Mask);Require(editor.PrepareInteraction!(),"mask不能激活");var session=Session(owner,BlobLocalRoi.Mask);var row=session.Items.First(item=>item.Group==BlobLocalRoi.Mask&&item.Enabled);Require(session.Select(row.Group,row.Id),"mask不能选中");var native=Visual(viewport.View).OfType<SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI.RectROI>().Single(item=>ReferenceEquals(item.DataContext,row));var handle=(FrameworkElement)Get(native,"move");var prepare=typeof(SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI.ROIControlBase).GetMethod("PrepareRoiGesture",Flags)!;Require(prepare.Invoke(native,new object?[]{handle,(Action)(()=>{}),null}) is not null,"旧原gesture未安装");var attachment=Get(owner,"_interaction");var proof=attachment.GetType().GetField("_current",Flags)!;var original=(Func<bool>)proof.GetValue(attachment)!;object? newer=null;var entered=false;proof.SetValue(attachment,(Func<bool>)(()=>{if(!entered){entered=true;proof.SetValue(attachment,original);newer=prepare.Invoke(native,new object?[]{handle,(Action)(()=>{}),null});row.CenterX+=0.03;}return false;}));handle.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.LostMouseCaptureEvent});Require(entered&&newer is not null&&ReferenceEquals(Maybe(native,"ActiveRoiGesture"),newer)&&row.HasPending,"旧lost-capture取消同步安装的新gesture/暂存");((SunEyeVision.Plugin.Wpf.Presenters.Display.INativeRoiEditLease)attachment).CancelGesture();Pump();Require(!row.HasPending&&!(bool)Get(owner,"HasPending"),"有效用户取消误记退役");
            });
            Case("迟到lost-capture在清原投影暂存前记录，旧回调不损新窗",()=>{
                var editor=Editor(owner,BlobLocalRoi.Mask);Require(editor.PrepareInteraction!(),"mask不能激活");var session=Session(owner,BlobLocalRoi.Mask);var row=session.Items.First(item=>item.Group==BlobLocalRoi.Mask&&item.Enabled);Require(session.Select(row.Group,row.Id),"mask不能选中");var native=Visual(viewport.View).OfType<SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI.RectROI>().Single(item=>ReferenceEquals(item.DataContext,row));var handle=(FrameworkElement)Get(native,"move");var gesture=typeof(SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI.ROIControlBase).GetMethod("PrepareRoiGesture",Flags)!.Invoke(native,new object?[]{handle,(Action)(()=>{}),null});Require(gesture is not null&&ReferenceEquals(Get(native,"InteractionHandle"),handle),"原gesture入口未安装实际handle");row.CenterX+=0.02;Require(row.HasPending,"原投影暂存未产生");var attachment=Get(owner,"_interaction");attachment.GetType().GetField("_current",Flags)!.SetValue(attachment,(Func<bool>)(()=>false));var lost=new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.LostMouseCaptureEvent};handle.RaiseEvent(lost);Require(!row.HasPending&&(bool)Get(owner,"HasPending")&&!control.TryCapture(out _),"失效lost-capture清掉暂存而未保留拒绝状态");window.Close();Pump();window=Open();control=Control(window);owner=Owner(control);var current=Layer();handle.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.LostMouseCaptureEvent});Pump();Require(ReferenceEquals(current,Layer())&&!(bool)Get(owner,"HasPending"),"旧capture事件损坏新owner");
            });
            Case("取消关闭只清本层，旧lease释放不损新窗口owner",()=>{
                var oldLayer=Layer();var oldInputs=(NativeRoiInputLease)Get(owner,"_previewInputs");var original=(Func<bool>)Get(oldInputs,"_current");DefaultDebugWindow? newer=null;object? newOwner=null;var entered=false;typeof(NativeRoiInputLease).GetField("_current",Flags)!.SetValue(oldInputs,(Func<bool>)(()=>{if(!entered){entered=true;window.Close();Pump();newer=Open();newOwner=Owner(Control(newer));PumpUntil(()=>Maybe(newOwner,"_previewLayer") is not null);}return original();}));Require(!oldInputs.IsCurrent&&entered,"proof同步关闭未退役旧输入");Require(Maybe(oldLayer,"_owner") is null&&formal==System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration)&&position==history.Position&&bytes.SequenceEqual(File.ReadAllBytes(path)),"取消写正式/历史/文件或留下层");var installed=Get(newOwner!,"_previewLayer");oldInputs.Dispose();Require(ReferenceEquals(installed,Maybe(newOwner!,"_previewLayer")),"旧Dispose破坏新owner");newer!.Close();Pump();
            });
        }
        shell.Close();app.Shutdown();Console.WriteLine("检测继承只读预览："+_passed+"组通过；本方宿主适配，非VM视觉/时序等价。");return 0;
    }
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
    private static void Case(string name,Action action){if(Environment.GetEnvironmentVariable("S4_BLOB_PREVIEW_CASE")=="Wrong"&&!name.StartsWith("真实单图")&&!name.StartsWith("独立只读")&&!name.StartsWith("同引用")&&!name.StartsWith("选错图"))return;if(Environment.GetEnvironmentVariable("S4_BLOB_PREVIEW_CASE")=="Ring"&&!name.StartsWith("真实单图")&&!name.StartsWith("独立只读")&&!name.StartsWith("圆环六叶"))return;var log=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"preview-progress.log");File.AppendAllText(log,DateTime.UtcNow.ToString("O")+" BEGIN "+name+Environment.NewLine);try{action();_passed++;Console.WriteLine("通过："+name);File.AppendAllText(log,DateTime.UtcNow.ToString("O")+" PASS "+name+Environment.NewLine);}catch(Exception error){File.AppendAllText(log,DateTime.UtcNow.ToString("O")+" FAIL "+name+Environment.NewLine+error+Environment.NewLine);throw;}}
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
