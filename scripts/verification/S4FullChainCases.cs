using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Commands;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;
using Canvas=System.Windows.Controls.Canvas;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    [STAThread] private static int Main(){try{return Run();}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
    private static int Run()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        SunEyeVision.Plugin.SDK.Vision.Hosting.VisionImageFactoryLocator.Current=new SunEyeVision.HalAdapter.HalImageFactory();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
        var dialogs=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        dialogs.Tick+=(_,_)=>{foreach(var dialog in app.Windows.OfType<Window>().Where(w=>w.GetType().Name.Contains("MessageDialog")).ToArray()){Console.Error.WriteLine("本进程产品提示："+string.Join(" | ",Logical(dialog).OfType<TextBlock>().Select(t=>t.Text)));dialog.Close();}};dialogs.Start();
        var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;var plugins=new PluginManager();plugins.LoadPlugins();
        plugins.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(t=>t.Id));PluginLoader.MarkAsLoaded();
        VerifyRuntimeGraphCopy();
        VerifyOverlappingInsertion();
        VerifyRuntimeRootProxies();
        Case("画布图标与工具箱读取同一正式目录且不恢复旧VM工具目录",()=>
        {
            var converter=new SunEyeVision.UI.Converters.Node.ToolNodeIconConverter();
            foreach(var id in new[]{"ImageLoad","BlobAnalysis"})
            {
                var declaration=ToolRegistry.DirectoryCatalog!.Find(id)!;
                var toolbox=new SunEyeVision.UI.Shell.Controls.Toolbox.ToolItem(declaration.DisplayName,declaration.Category,declaration.Icon,declaration.Description,id);
                var uri=(string)converter.Convert(id,typeof(string),null!,System.Globalization.CultureInfo.InvariantCulture);
                Require(uri==toolbox.IconSource,"画布与工具箱图标声明分叉："+id);
                using var stream=Application.GetResourceStream(new Uri(uri))!.Stream;
                Require(stream.Length>0,"正式图标资源缺失："+id);
            }
            Require(ReferenceEquals(converter.Convert("unapproved-tool",typeof(string),null!,System.Globalization.CultureInfo.InvariantCulture),DependencyProperty.UnsetValue),"未准入工具被旧目录补回");
        });
        var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"document");Directory.CreateDirectory(folder);
        ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(folder);
        var solutions=ServiceInitializer.SolutionManager;var vm=new MainWindowViewModel();var document=solutions.CreateUntitledSolution();
        if(vm.WorkflowTabViewModel.SelectedTab is null)vm.NewWorkflowCommand.Execute(null);
        var tab=vm.WorkflowTabViewModel.SelectedTab!;var canvasControl=new WorkflowCanvasControl{DataContext=tab,Width=600,Height=400};
        var canvas=(Canvas)canvasControl.FindName("WorkflowCanvas");
        var imageHost=new SunEyeVision.UI.Shell.Controls.Shell.ImageResultControl{DataContext=vm.ImageResult,Width=780};
        imageHost.Initialize(vm);
        var workspace=(SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell.ResultWorkspaceShell)imageHost.FindName("Workspace");
        var image=(SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.ImageViewBox)workspace.OutputViewport;
        var panel=new StackPanel{Orientation=Orientation.Horizontal};panel.Children.Add(canvasControl);panel.Children.Add(imageHost);
        var shell=new Window{DataContext=vm,Content=panel,Width=1640,Height=500,Left=-10000,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();Pump();
        var drop=new WorkflowDragDropHandler(canvasControl){TabViewModel=vm.WorkflowTabViewModel};
        SunEyeVision.UI.Shared.WorkflowNode Drop(string tool)
        {
            var count=tab.WorkflowNodes.Count;var data=new DataObject("ToolItem",vm.Toolbox.AllTools.Single(t=>t.ToolId==tool));
            var constructor=typeof(DragEventArgs).GetConstructors(BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).Single(c=>c.GetParameters().Any(p=>p.ParameterType==typeof(IDataObject)));
            object Argument(ParameterInfo p)=>p.ParameterType==typeof(IDataObject)?data:p.ParameterType==typeof(DragDropKeyStates)?DragDropKeyStates.None:
                p.ParameterType==typeof(DragDropEffects)?DragDropEffects.Copy:p.ParameterType==typeof(DependencyObject)?canvas:p.ParameterType==typeof(Point)?new Point(80+count*180,80):throw new Exception("未知拖放参数");
            drop.Canvas_Drop(canvas,(DragEventArgs)constructor.Invoke(constructor.GetParameters().Select(Argument).ToArray()));
            Require(tab.WorkflowNodes.Count==count+1,"真实Drop未创建节点");return (SunEyeVision.UI.Shared.WorkflowNode)tab.WorkflowNodes.Last();
        }
        var input=Drop("ImageLoad");var blob=Drop(BlobTool.Id);var downstream=Drop(BlobTool.Id);
        var flow=document.Workflows.Single(w=>w.Id==tab.CurrentWorkflowId);
        ReferenceSetting? ImageBinding(WorkflowNodeBase n)=>n.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) as ReferenceSetting;
        void Select(WorkflowNodeBase n){foreach(var item in vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes)item.IsSelected=ReferenceEquals(item,n);vm.SelectedNode=n;Pump();}
        WorkflowContext Context()=>(WorkflowContext)typeof(MainWindowViewModel).GetProperty("CurrentWorkflowContext",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
        DeclaredOutputSnapshot Result(string id)=>Context().GetNodeResult(id)?.Result as DeclaredOutputSnapshot??throw new Exception("缺少Runtime结果："+id);
        int Count(string id)
        {
            var result=Result(id);
            return result.Output is BlobOutput output?output.Count:throw new Exception("Blob没有成功输出："+result.Status+" "+result.ErrorMessage+"；本轮节点："+
                string.Join(" | ",flow.Nodes.Select(node=>Context().GetNodeResult(node.Id)?.Result is DeclaredOutputSnapshot value?node.Name+":"+value.Status+":"+value.ErrorMessage:node.Name+":无结果")));
        }
        void Execute(){var task=vm.RunSelectedImageAsync();Await(task);}
        DefaultDebugWindow Open(SunEyeVision.UI.Shared.WorkflowNode n){Select(n);vm.OpenDebugWindowCommand.Execute(n);Pump();return app.Windows.OfType<DefaultDebugWindow>().Single(w=>w.IsVisible);}
        RuntimeNodeConfigurationControl Control(DefaultDebugWindow w)=>(RuntimeNodeConfigurationControl)((ContentControl)w.FindName("ContentHost")).Content;
        TextBox Field(DefaultDebugWindow w,string property)
        {
            var id=((IToolSettingsFields)new BlobTool()).SettingsSchema.Presentation!.Fields.Single(f=>f.DisplayName==BlobParameterDefinition.Instance.Label(property)).FieldId;
            Logical(Control(w)).OfType<TabControl>().Single().SelectedIndex=1;Pump();return (TextBox)Logical(Control(w)).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==id).InputElement!;
        }
        Case("工具箱实际Drop默认创建Runtime，节点与文档共用定义，未创建SDK Blob参数",()=>
        {
            Require(blob.RuntimeConfiguration?.SettingsType==typeof(BlobSettings)&&downstream.RuntimeConfiguration?.SettingsType==typeof(BlobSettings),"默认UI工厂仍走SDK");
            Require(input.RuntimeConfiguration?.ToolId=="ImageLoad"&&ImageSourcePolicy.Read(input) is not null,"图像源默认Drop没有创建正式Runtime配置");
            Require(ReferenceEquals(flow.Nodes,tab.WorkflowNodes)&&((BlobSettings)blob.RuntimeConfiguration.CaptureSettings()).Threshold==BlobParameterDefinition.Instance.Default<int>("Threshold"),"节点定义或XML默认值错误");
        });
        var pixels=new byte[64*64];for(var y=5;y<9;y++)for(var x=5;x<9;x++)pixels[y*64+x]=200;for(var y=30;y<35;y++)for(var x=30;x<35;x++)pixels[y*64+x]=200;
        var file=Path.Combine(folder,"input.png");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,64,96,96,PixelFormats.Gray8,null,pixels,64)));
        using(var output=File.Create(file))png.Save(output);Select(input);
        (vm.ActiveInputSource??throw new Exception("未建立实际输入源面板")).AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{Name="Runtime默认链输入",FilePath=file,SourceReference=file,IsForRun=true}},selectFirstAdded:true);
        var sourcePolicy=ImageSourcePolicy.Read(input)??throw new Exception("源条没有接入正式Runtime");
        Require(sourcePolicy.FilePaths.SequenceEqual(new[]{file})&&sourcePolicy.CurrentIndex==0,"源条导入未正式接纳到Runtime稳定字段");
        Case("实际连线按声明图像端口建立语义引用并记录自动来源所有权",()=>
        {
            vm.AddConnectionToWorkflow(new WorkflowConnection("input-blob",input.Id,blob.Id));vm.AddConnectionToWorkflow(new WorkflowConnection("blob-next",blob.Id,downstream.Id));
            var a=ImageBinding(blob);var b=ImageBinding(downstream);
            Console.WriteLine("自动来源状态："+System.Text.Json.JsonSerializer.Serialize(new[]{a,b}));
            Require(a?.OriginId=="connection:input-blob"&&b?.OriginId=="connection:blob-next","默认图像订阅或所有权缺失");
            Require(b!.Reference.PortId==BlobSchema.Field(5),"未按Runtime图像声明选源");
        });
        Case("连线及Runtime自动来源共享一次正式历史提交，撤销重做不留下外部配置变化",()=>
        {
            var host=solutions.Session!.Editing.Host;
            host.BeginEdit(flow.Id,downstream.Id);
            tab.CommandManager.Undo();
            Require(!flow.Connections.Any(edge=>edge.Id=="blob-next")&&ImageBinding(downstream) is null,"撤销连线遗留自动来源");
            host.BeginEdit(flow.Id,downstream.Id);
            tab.CommandManager.Redo();
            Require(flow.Connections.Any(edge=>edge.Id=="blob-next")&&ImageBinding(downstream)?.OriginId=="connection:blob-next","重做没有恢复同一自动来源");
            host.BeginEdit(flow.Id,downstream.Id);
        });
        Case("真实主窗口工作流运行ImageLoad→Runtime Blob→下游，发布同帧图像及区域",()=>
        {
            Select(blob);Execute();Require(Count(blob.Id)==2&&Count(downstream.Id)==2,"默认入口真实HALCON结果不符");
            var output=(BlobOutput)Result(blob.Id).Output!;using var region=output.Region.AcquireReader();Require(output.GrayImage is not null&&output.GrayImage.Frame==region.SourceFrame,"物理帧丢失");
            Require(Logical(vm.SelectedNodeDataAnalysisControl!).OfType<DataGrid>().Single().Items.Count==2,"正式结果未进入实际表");
            Require(image.ImageDataSource?.Width==64&&vm.CreateDataSourceQueryService(flow.Id).GetImageDisplaySources(blob.Id).Any(s=>s.SourceNodeId==blob.Id&&s.PropertyName==BlobSchema.Field(5)),"Runtime主图像或自身图像来源未进入真实显示链");
        });
        Case("Runtime窗口编辑数量上限确认发布，下游实际来源弹窗订阅最终Region",()=>
        {
            var window=Open(blob);Field(window,"BlobCountLimit").Text="1";Pump();Require(window.TryConfirm(out var error),error);
            window=Open(downstream);var editor=Control(window);var inputField=Logical(editor).OfType<DeclaredFieldEditor>().Single(f=>f.FieldId==BlobTool.InspectionInput);
            Logical(editor).OfType<TabControl>().Single().SelectedIndex=0;window.UpdateLayout();Pump();
            var popup=Visual(inputField).OfType<SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton>().Single(item=>item.Name=="rt");
            popup.ApplyTemplate();popup.Focus();popup.IsDropDownOpen=true;Pump();
            var nativePopup=(System.Windows.Controls.Primitives.Popup)popup.Template.FindName("PART_Popup",popup);
            var tree=Visual(nativePopup.Child).OfType<SunEyeVision.Plugin.Wpf.Controls.References.Primitives.SubTreeView>().Single();
            var actual=(TreeView)tree.FindName("treeView");
            TreeViewItem? FindSource(ItemsControl parent)
            {
                for(var index=0;index<parent.Items.Count;index++)
                {
                    if(parent.ItemContainerGenerator.ContainerFromIndex(index) is not TreeViewItem item)continue;
                    if(item.DataContext?.GetType().GetProperty("Candidate")?.GetValue(item.DataContext) is DeclaredReferenceCandidate candidate
                        &&candidate.NodeId==blob.Id&&candidate.Output.Id==BlobSchema.RegionPort&&candidate.MemberPath.Count==0)return item;
                    item.IsExpanded=true;item.UpdateLayout();Pump();var found=FindSource(item);if(found is not null)return found;
                }
                return null;
            }
            var source=FindSource(actual)??throw new Exception("原来源树未包含正式Region输出");
            source.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Controls.Control.MouseDoubleClickEvent});Pump();
            Require(window.TryConfirm(out error),error);
            Select(blob);Execute();Require(Count(blob.Id)==1&&Count(downstream.Id)==1,"最终ROI未限制下游集合");
        });
        Case("窗口实际执行只显示私有阈值结果，不替换生产结果或设置",()=>
        {
            var production=Result(blob.Id);var window=Open(blob);Field(window,"Threshold").Text="255";Pump();
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(window))!;ToolCommands.Execute.Execute(null,window);
            Until(()=>context.DebugResults.GetNodeResult(blob.Id)?.Result is not null);
            Require(context.DebugResults.GetNodeResult(blob.Id)!.Result is DeclaredOutputSnapshot {Output:BlobOutput {Count:0}}&&ReferenceEquals(production,Result(blob.Id)),"私有调试污染正式结果");
            Require(Logical(vm.SelectedNodeDataAnalysisControl!).OfType<DataGrid>().Single().Items.Count==0,"实际结果表未显示私有结果");
            window.Close();Pump();Require(solutions.Session!.Editing.RegisteredScopeCount==0&&((BlobSettings)blob.RuntimeConfiguration!.CaptureSettings()).Threshold==100,"关闭污染正式值或遗留范围");
        });
        Case("删除自动来源阻断运行，不静默回退；显式选择不受旧连线删除影响",()=>
        {
            var copy=blob.Clone();Require(ConnectionBindingPolicy.RemoveAutomaticImageBinding(copy,"input-blob"),"自动来源未识别");
            var marked=ImageBinding(copy)!;Require(marked.RebindingReason is not null&&marked.OriginId is null,"未保留重选状态");
            var copied=System.Text.Json.JsonSerializer.Deserialize<RuntimeToolConfiguration>(System.Text.Json.JsonSerializer.Serialize(copy.RuntimeConfiguration))!.Fields.Read(BlobTool.ImageInput) as ReferenceSetting;Require(copied?.RebindingReason==marked.RebindingReason&&copy.Clone().RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is ReferenceSetting {RebindingReason:not null},"绑定快照/复制丢失重选状态");
            try{using var request=RuntimeDebugInvocation.Capture(flow,copy,new SunEyeVision.Plugin.Runtime.ToolExecutionOwner(),1,id=>Context().GetNodeResult(id)?.Result,default,publishedOwner:document.ExecutionOwner);throw new Exception("删除自动连线后仍执行");}
            catch(InvalidOperationException){}
            var manual=blob.Clone();var explicitSource=ImageBinding(manual)! with {OriginId=null};manual.RuntimeConfiguration=manual.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{BlobTool.ImageInput,explicitSource}});
            Require(!ConnectionBindingPolicy.RemoveAutomaticImageBinding(manual,"input-blob")&&Equals(ImageBinding(manual),explicitSource),"删除旧连线清除了显式来源");
        });
        var path=Path.Combine(folder,"runtime-host.solution");
        Case("正式保存重开恢复唯一Runtime模型、语义来源及自动连线所有权，再运行真实HALCON",()=>
        {
            var saved=solutions.SaveCurrentDocument(path);Require(saved.FileWritten,"正式保存失败："+solutions.LastError+" "+System.Text.Json.JsonSerializer.Serialize(saved));var text=File.ReadAllText(path);
            Require(!text.Contains("BlobAnalysisParameters")&&!text.Contains("FilteredBlobCount"),"方案仍含SDK Blob模型");
            var reopened=solutions.LoadSolutionOnly(path)??throw new Exception(solutions.LastError);solutions.SetCurrentSolution(reopened);Pump();
            var restored=(SunEyeVision.UI.Shared.WorkflowNode)vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.Single(n=>n.Id==blob.Id);
            Require(restored.RuntimeConfiguration?.SettingsType==typeof(BlobSettings)&&((BlobSettings)restored.RuntimeConfiguration.CaptureSettings()).BlobCountLimit==1&&ImageBinding(restored)?.OriginId=="connection:input-blob","重开丢失强类型值或来源所有权");
            foreach(var key in new[]{"OriginId","UnknownField"})
            {
                var malformed=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
                var stored=malformed["Workflows"]![0]!["Nodes"]!.AsArray().Single(n=>n!["Id"]!.GetValue<string>()==blob.Id)!["RuntimeConfiguration"]!["Fields"]![BlobTool.ImageInput]!;
                stored[key]=17;var invalid=Path.Combine(folder,"invalid-"+key+".solution");File.WriteAllText(invalid,malformed.ToJsonString());
                var repository=new SolutionRepository();Require(repository.LoadCandidate(invalid).Candidate is null&&
                    !string.IsNullOrEmpty(repository.LastError),"持久化未按错型所有权或未知字段拒绝："+repository.LastError);
            }
            Select(restored);Execute();Require(Count(restored.Id)==1&&Count(downstream.Id)==1,"重开未执行新Runtime链");
            var window=Open(restored);Field(window,"Threshold").Text="230";window.Close();Pump();Require(((BlobSettings)restored.RuntimeConfiguration.CaptureSettings()).Threshold==100,"重开取消污染正式值");
        });
        var view=vm.SelectedNodeDataAnalysisControl;vm.SelectedNode=null;Pump();Require(vm.SelectedNodeDataAnalysisControl is null,"选择退出仍保留结果页");
        if(!File.Exists(Path.Combine(product,"plugins/SunEyeVision.Tool.BlobAnalysis/SunEyeVision.Tool.BlobAnalysis.dll")))
            Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="SunEyeVision.Tool.BlobAnalysis"),"缺少SDK入口时仍加载旧Blob");
        imageHost.Shutdown();shell.Close();dialogs.Stop();Console.WriteLine($"S4真实Runtime宿主连续链：{_passed}组通过；默认工具箱/真实Drop/连线/运行/ROI引用/私有调试/保存重开；未验证本地ROI、全部VM视觉或人工验收。");return 0;
    }
    private sealed class RootProxyFixture:IDisposable
    {
        internal readonly SunEyeVision.Workflow.Workflow Root=new("root-proxies","正式根参数代理");
        internal readonly WorkflowNodeBase Source=RuntimeNode("root-source"),Target=RuntimeNode("root-target");
        internal readonly SolutionSession Session;internal readonly object Context;internal readonly DataSourceQueryService Query;
        internal readonly string Threshold,Upper;internal Action? OnRead;internal Window? Window;internal bool ThrowGate;
        internal RootProxyFixture(bool bound=false)
        {
            Root.Nodes.Add(Source);Root.Nodes.Add(Target);Root.Connections.Add(new WorkflowConnection("root-edge",Source.Id,Target.Id));
            var metadata=Target.RuntimeConfiguration!.SettingsSchema.Presentation!.Fields;
            Threshold=metadata.Single(field=>field.DisplayName==BlobParameterDefinition.Instance.Label("Threshold")).FieldId;
            Upper=metadata.Single(field=>field.DisplayName==BlobParameterDefinition.Instance.Label("UpperThreshold")).FieldId;
            if(bound)Target.RuntimeConfiguration=Target.RuntimeConfiguration.WithFieldSettings(new Dictionary<string,ParameterSetting>{{Threshold,CheckedReference()}});
            var solution=new Solution();solution.Workflows.Add(Root);
            Session=(SolutionSession)Activator.CreateInstance(typeof(SolutionSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{solution},null)!;
            Query=new DataSourceQueryService{WorkflowId=Root.Id,BindingCandidateNodeIds=id=>id==Target.Id?new[]{Source.Id}:Array.Empty<string>(),FormalOutputContractReader=id=>
            {
                var callback=OnRead;OnRead=null;callback?.Invoke();
                var node=Root.Nodes.SingleOrDefault(node=>node.Id==id);return node is null?null:new OutputBindingChangeService().ReadContract(node);
            }};
            var type=typeof(RuntimeNodeConfigurationControl).Assembly.GetType("SunEyeVision.UI.Services.Editing.RootWorkflowConfigurationContext",true)!;
            Context=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{Session,Root,Query,(Func<bool>)(()=>true),(Func<bool>)(()=>ThrowGate?throw new InvalidOperationException("编辑准入回调失败"):true),(Action<BindingConfigurationPublication>)(_=>{})},null)!;
        }
        internal static object? Invoke(object target,string name,params object?[] args)
        {
            try{return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(target,args);}
            catch(TargetInvocationException error)when(error.InnerException is not null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }
        internal RootParameterProxy Add(string fieldId)
        {
            var candidates=(System.Collections.IEnumerable)Invoke(Context,"Candidates")!;
            var candidate=candidates.Cast<object>().Single(value=>((WorkflowNodeBase)value.GetType().GetProperty("Node")!.GetValue(value)!).Id==Target.Id
                &&(string)value.GetType().GetProperty("StoragePath")!.GetValue(value)! ==fieldId);
            return (RootParameterProxy)Invoke(Context,"Add",candidate)!;
        }
        internal ParameterSetting Read(RootParameterProxy proxy)=>(ParameterSetting)Invoke(Context,"Read",proxy)!;
        internal void Write(RootParameterProxy proxy,ParameterSetting setting)=>Invoke(Context,"Write",proxy,setting);
        internal ReferenceSetting CheckedReference()=>new(new OutputReference(Source.Id,BlobSchema.Field(12),DataType.Number,scopeId:Root.Id),BindingConversion.Float64ToInt64Checked);
        internal string? Validate(RootParameterProxy proxy,ReferenceSetting setting)
        {
            var picker=typeof(RuntimeNodeConfigurationControl).Assembly.GetType("SunEyeVision.UI.Services.Editing.RootWorkflowReferencePicker",true)!;
            var options=(ReferenceFieldOptions)picker.GetMethod("Options",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{Context,proxy})!;
            return options.ValidateProposal!(Target.RuntimeConfiguration!.BindingTargets.Single(port=>port.Id==proxy.StoragePath),setting);
        }
        internal void Apply()
        {
            object?[] args={null};Require((bool)Invoke(Context,"TryApply",args)!,"根代理应用失败："+args[0]);
        }
        internal Window Open()
        {
            Window=(Window)Activator.CreateInstance(typeof(SunEyeVision.UI.Shell.Windows.RootWorkflowConfigurationWindow),BindingFlags.Instance|BindingFlags.NonPublic,null,
                new object[]{Context,(Func<Task>)(()=>Task.CompletedTask),(Action)(()=>{}),(Action)(()=>{}),(Func<bool>)(()=>false),(Func<string,DeclaredOutputSnapshot?>)(_=>null)},null)!;
            Window.Show();Window.UpdateLayout();Pump();return Window;
        }
        public void Dispose(){Window?.Close();((IDisposable)Context).Dispose();Session.Editing.Close();Session.Document.ExecutionOwner.Retire();}
    }
    private static void VerifyRuntimeRootProxies()
    {
        Case("真实根窗口可打开已绑定整数设置，原预检链保显式转换且旧行关闭后失效",()=>
        {
            using var f=new RootProxyFixture(true);var proxy=f.Add(f.Threshold);var window=f.Open();
            var field=Logical(window).OfType<DeclaredFieldEditor>().Single(field=>field.FieldId==f.Threshold);
            Require(field.Setting is ReferenceSetting {Conversion:BindingConversion.Float64ToInt64Checked}&&!field.HasInvalidText,"已订阅整数代理被伪null常量备份破坏");
            Require(f.Validate(proxy,f.CheckedReference()) is null,"实际Picker预检丢显式转换");
            Require(f.Validate(proxy,f.CheckedReference() with {Conversion=BindingConversion.Automatic}) is not null,"隐式Float64到Int32未拒绝");
            window.Close();Pump();Require(!field.TrySetConstant(73L)&&f.Target.RuntimeConfiguration!.Fields.Read(f.Threshold) is ReferenceSetting {Conversion:BindingConversion.Float64ToInt64Checked},"旧根窗口行在关闭后写入");
        });
        Case("Runtime根代理应用撤销重做和正式保存重开保持同一完整引用",()=>
        {
            using var f=new RootProxyFixture();var proxy=f.Add(f.Threshold);var proposal=f.CheckedReference();
            Require(f.Validate(proxy,proposal) is null&&f.Read(proxy) is ConstantSetting {Value:128L},"预检写了活草稿");
            f.Write(proxy,proposal);Require(f.Target.RuntimeConfiguration!.Fields.Read(f.Threshold) is ConstantSetting {Value:128L},"未应用代理污染正式配置");
            f.Apply();Require(f.Target.RuntimeConfiguration.Fields.Read(f.Threshold) is ReferenceSetting {Conversion:BindingConversion.Float64ToInt64Checked}&&!f.Target.ParamSettings.Settings.Any(),"根应用没有发布唯一Map");
            RootProxyFixture.Invoke(f.Context,"Undo");Require(f.Target.RuntimeConfiguration.Fields.Read(f.Threshold) is ConstantSetting {Value:128L}&&f.Root.RootConfiguration.RunParameters.Length==0,"根撤销没有恢复配置与代理");
            RootProxyFixture.Invoke(f.Context,"Redo");Require(f.Target.RuntimeConfiguration.Fields.Read(f.Threshold) is ReferenceSetting {Conversion:BindingConversion.Float64ToInt64Checked}&&f.Root.RootConfiguration.RunParameters.Length==1,"根重做丢引用或代理");
            var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"root-proxies.solution");var repository=new SolutionRepository();Require(repository.Save(f.Session.Document,path),repository.LastError??"保存失败");
            var reopened=repository.LoadCandidate(path).Candidate??throw new Exception(repository.LastError);var restored=reopened.Workflows.Single().Nodes.Single(node=>node.Id==f.Target.Id);
            Require(restored.RuntimeConfiguration!.Fields.Read(f.Threshold) is ReferenceSetting {Conversion:BindingConversion.Float64ToInt64Checked}&&!restored.ParamSettings.Settings.Any(),"根代理保存重开丢转换或恢复旧容器");
            f.Write(proxy,new ConstantSetting(99L));RootProxyFixture.Invoke(f.Context,"Cancel");Require(f.Target.RuntimeConfiguration.Fields.Read(f.Threshold) is ReferenceSetting,"取消发布了新常量");
        });
        Case("移除初始缺键图像代理仅恢复该字段，不撤销另一根参数修改",()=>
        {
            using var f=new RootProxyFixture();var image=f.Add(BlobTool.ImageInput);var threshold=f.Add(f.Threshold);
            f.Write(threshold,new ConstantSetting(73L));var port=f.Source.RuntimeConfiguration!.Schema.Outputs.Single(port=>port.Id==BlobSchema.Field(5));
            f.Write(image,new ReferenceSetting(new OutputReference(f.Source.Id,port.Id,port.Type,scopeId:f.Root.Id)));
            RootProxyFixture.Invoke(f.Context,"Remove",image);
            Require(f.Read(threshold) is ConstantSetting {Value:73L}&&f.Target.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is UnsetSetting,"移除图像代理撤销其他修改或污染正式输入");
            f.Apply();Require(f.Target.RuntimeConfiguration!.Fields.Read(f.Threshold) is ConstantSetting {Value:73L}&&!f.Target.RuntimeConfiguration.Fields.Capture().ContainsKey(BlobTool.ImageInput),"opening缺键恢复失败或偷偷补来源");
        });
        Case("根引用预检回调修改另一字段或关闭窗口，拒旧校验且保留新状态",()=>
        {
            using(var f=new RootProxyFixture())
            {
                var threshold=f.Add(f.Threshold);var upper=f.Add(f.Upper);
                f.OnRead=()=>f.Write(upper,new ConstantSetting(201L));
                Require(f.Validate(threshold,f.CheckedReference()) is not null,"预检忽略回调的新编辑");
                Require(f.Read(upper) is ConstantSetting {Value:201L}&&f.Read(threshold) is ConstantSetting {Value:128L},"整配置回滚覆盖新编辑或临时提案落地");
                Require(f.Validate(threshold,f.CheckedReference()) is null,"拒旧校验后无法重试");
                f.Write(threshold,f.CheckedReference());f.Apply();Require(f.Target.RuntimeConfiguration!.Fields.Get<long>(f.Upper)==201,"接纳后丢回调的新参数");
            }
            using(var f=new RootProxyFixture())
            {
                var proxy=f.Add(f.Threshold);var window=f.Open();f.OnRead=window.Close;
                Require(f.Validate(proxy,f.CheckedReference()) is not null&&f.Target.RuntimeConfiguration!.Fields.Read(f.Threshold) is ConstantSetting {Value:128L},"关闭后旧校验继续接纳");
            }
            using(var f=new RootProxyFixture())
            {
                var proxy=f.Add(f.Threshold);f.OnRead=()=>{f.ThrowGate=true;throw new InvalidOperationException("来源读取失败");};
                Require(f.Validate(proxy,f.CheckedReference()) is not null&&f.Target.RuntimeConfiguration!.Fields.Read(f.Threshold) is ConstantSetting {Value:128L},"双回调异常未返回诊断或污染正式设置");
                f.ThrowGate=false;Require(f.Read(proxy) is ConstantSetting {Value:128L}&&f.Validate(proxy,f.CheckedReference()) is null,"双异常后旧候选遗留或校验未复位");
            }
        });
    }
    private static WorkflowNodeBase RuntimeNode(string id,string tool=BlobTool.Id)=>new(id,id,id,tool){RuntimeConfiguration=RuntimeToolConfiguration.Create(tool)};
    private static ReferenceSetting ImageReference(WorkflowNodeBase source,string scope,string edge)
    {
        var port=source.RuntimeConfiguration!.Schema.Outputs.First(port=>port.Type==TypeRef.ResourceOf(ResourceKind.Image));
        return new(new OutputReference(source.Id,port.Id,port.Type,scopeId:scope,storage:port.Storage),OriginId:"connection:"+edge);
    }
    private static void BindImage(WorkflowNodeBase target,ReferenceSetting reference)=>target.RuntimeConfiguration=target.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{BlobTool.ImageInput,reference}});
    private static void VerifyRuntimeGraphCopy()
    {
        var flow=new SunEyeVision.Workflow.Workflow("copy-origin","来源归属");var source=RuntimeNode("source","ImageLoad");var consumer=RuntimeNode("consumer");var other=RuntimeNode("other");
        flow.Nodes.Add(source);flow.Nodes.Add(consumer);flow.Nodes.Add(other);
        flow.Connections.Add(new WorkflowConnection("source-consumer",source.Id,consumer.Id));
        flow.Connections.Add(new WorkflowConnection("unrelated",source.Id,other.Id));
        var reference=ImageReference(source,flow.Id,"source-consumer");BindImage(consumer,reference);
        var original=consumer.RuntimeConfiguration;var creates=0;
        WorkflowFragment Copy(string target,params string[] ids)=>WorkflowFragmentCopy.Prepare(flow,ids,target,_=>null,(node,_)=>new WorkflowNodeBase("copy-"+node.Id+"-"+(++creates),node.Name,node.DispName,node.ToolType));
        Case("Runtime复制按完整源图校验，复制边映射归属且未复制边保明确外部来源",()=>
        {
            var both=Copy("other-flow",source.Id,consumer.Id);var edge=both.Connections.Single();
            var copied=both.Nodes.Single(node=>node.ToolType==BlobTool.Id).RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) as ReferenceSetting;
            Require(copied?.OriginId=="connection:"+edge.Id&&copied.Reference.NodeId==edge.SourceNodeId&&copied.Reference.ScopeId=="other-flow"&&copied.Conversion==reference.Conversion,"复制边/引用/转换归属没有同图映射");
            var single=Copy("other-flow",consumer.Id);copied=(ReferenceSetting)single.Nodes.Single().RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput);
            Require(single.Connections.Count==0&&copied.OriginId is null&&copied.Reference.NodeId==source.Id&&copied.Reference.ScopeId==flow.Id,"合法未复制边错误拒绝或来源被猜改");
            var same=Copy(flow.Id,consumer.Id);copied=(ReferenceSetting)same.Nodes.Single().RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput);
            Require(same.Connections.Single().SourceNodeId==source.Id&&copied.OriginId=="connection:"+same.Connections.Single().Id,"同流程复制漏未选前驱入线或其归属");
            Require(ReferenceEquals(original,consumer.RuntimeConfiguration),"复制修改了源配置");
        });
        Case("Runtime非法归属复制在创建候选前拒绝，不清成可运行手动引用",()=>
        {
            foreach(var origin in new[]{"unknown:source-consumer","connection:missing","connection:unrelated","connection:"})
            {
                BindImage(consumer,reference with {OriginId=origin});var prior=consumer.RuntimeConfiguration;var count=creates;
                try{Copy("other-flow",source.Id,consumer.Id);throw new Exception("非法归属被复制："+origin);}
                catch(InvalidOperationException error){Require(error.Message.Contains("归属"),"复制因错误原因拒绝："+error.Message);}
                Require(creates==count&&ReferenceEquals(prior,consumer.RuntimeConfiguration),"拒绝复制已创建候选或污染源");
            }
            consumer.RuntimeConfiguration=original;
        });
        Case("已明确失效的Runtime引用允许复制但保留修复原因",()=>
        {
            BindImage(consumer,reference with {OriginId="unknown:retired",RebindingReason="请重新选择原来源"});
            var copied=(ReferenceSetting)Copy("other-flow",consumer.Id).Nodes.Single().RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput);
            Require(copied.RebindingReason=="请重新选择原来源"&&copied.OriginId is null&&copied.Conversion==reference.Conversion,"复制待修引用清除了阻断原因或转换");
            consumer.RuntimeConfiguration=original;
        });
    }
    private static void VerifyOverlappingInsertion()
    {
        Case("重叠插线保既有边及Runtime归属，端口路径位置和配置完整撤销重做",()=>
        {
            foreach(var pattern in new[]{1,2,3})
            {
                var a=new SunEyeVision.UI.Shared.WorkflowNode{Id="a",ToolType="ImageLoad",RuntimeConfiguration=RuntimeToolConfiguration.Create("ImageLoad")};
                var b=new SunEyeVision.UI.Shared.WorkflowNode{Id="b",ToolType=BlobTool.Id,RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id),Position=new Point(20,30)};
                var c=new SunEyeVision.UI.Shared.WorkflowNode{Id="c",ToolType=BlobTool.Id,RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id)};
                var original=new WorkflowConnection("ac",a.Id,c.Id){SourcePort="BottomPort",TargetPort="TopPort",ManualPathPoints=new[]{new Point(1,2),new Point(3,4)}};
                var left=new WorkflowConnection("ab",a.Id,b.Id){SourcePort="RightPort",TargetPort="LeftPort",ManualPathPoints=new[]{new Point(5,6),new Point(7,8)}};
                var right=new WorkflowConnection("bc",b.Id,c.Id){SourcePort="RightPort",TargetPort="LeftPort",ManualPathPoints=new[]{new Point(9,10),new Point(11,12)}};
                var edges=new System.Collections.ObjectModel.ObservableCollection<WorkflowConnection>{original};
                if((pattern&1)!=0){edges.Add(left);BindImage(b,ImageReference(a,"scope",left.Id));}
                if((pattern&2)!=0){edges.Add(right);BindImage(c,ImageReference(b,"scope",right.Id));}else BindImage(c,ImageReference(a,"scope",original.Id));
                var before=edges.ToArray();var configs=new[]{b.RuntimeConfiguration,c.RuntimeConfiguration};var calls=0;
                SunEyeVision.UI.Features.WorkflowUI.Commands.InsertNodeIntoConnectionCommand? command=null;
                command=new(edges,new[]{a,b,c},original,b,new Point(120,130),()=>
                {
                    calls++;ConnectionBindingPolicy.RemoveAutomaticImageBinding(c,original.Id);
                    foreach(var edge in command!.AddedConnections)
                    {
                        var target=edge.TargetNodeId==b.Id?b:c;var producer=edge.SourceNodeId==a.Id?a:b;
                        if(target.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is UnsetSetting)BindImage(target,ImageReference(producer,"scope",edge.Id));
                    }
                },()=>{});
                Require(edges.SequenceEqual(before)&&b.Position==new Point(20,30),"插线构造已改变图或位置");
                command.Execute(null);var after=edges.ToArray();
                Require(edges.Count==2&&command.AddedConnections.Count==2-((pattern&1)!=0?1:0)-((pattern&2)!=0?1:0)&&b.Position==new Point(120,130),"新增边清单含既有边或位置错误");
                foreach(var existing in before.Where(edge=>edge!=original))
                {
                    Require(edges.Contains(existing)&&existing.SourcePort=="BottomPort"&&existing.TargetPort=="TopPort"&&existing.ManualPathPoints is null,"既有边身份/端口/路由错误");
                    var target=existing.TargetNodeId==b.Id?b:c;
                    Require(((ReferenceSetting)target.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput)).OriginId=="connection:"+existing.Id,"既有边自动来源归属丢失");
                }
                command.Undo();Require(edges.SequenceEqual(before)&&b.Position==new Point(20,30),"撤销图顺序或位置不完整");
                Require(Equals(configs[0]!.Fields.Read(BlobTool.ImageInput),b.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput))&&Equals(configs[1]!.Fields.Read(BlobTool.ImageInput),c.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput)),"撤销未恢复Runtime Map");
                foreach(var existing in before.Where(edge=>edge!=original))Require(existing.SourcePort=="RightPort"&&existing.TargetPort=="LeftPort"
                    &&existing.ManualPathPoints?.SequenceEqual(existing==left?new[]{new Point(5,6),new Point(7,8)}:new[]{new Point(9,10),new Point(11,12)})==true,"撤销未恢复既有端口和手工路径坐标");
                command.Execute(null);Require(calls==1&&edges.SequenceEqual(after)&&b.Position==new Point(120,130),"重做换边身份或再次配置来源");
            }
        });
        Case("重叠插线配置或刷新抛错仍恢复图端口路径位置及Runtime配置",()=>
        {
            foreach(var failConfigure in new[]{true,false})
            {
                var a=new SunEyeVision.UI.Shared.WorkflowNode{Id="a"};var b=new SunEyeVision.UI.Shared.WorkflowNode{Id="b",ToolType=BlobTool.Id,RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id),Position=new Point(20,30)};var c=new SunEyeVision.UI.Shared.WorkflowNode{Id="c"};
                var original=new WorkflowConnection("ac","a","c");var left=new WorkflowConnection("ab","a","b"){SourcePort="RightPort",ManualPathPoints=new[]{new Point(8,9)}};
                var edges=new System.Collections.ObjectModel.ObservableCollection<WorkflowConnection>{original,left};var configuration=b.RuntimeConfiguration;var refresh=0;
                var command=new SunEyeVision.UI.Features.WorkflowUI.Commands.InsertNodeIntoConnectionCommand(edges,new[]{a,b,c},original,b,new Point(100,120),()=>
                {b.RuntimeConfiguration=b.RuntimeConfiguration!.WithSettings(((BlobSettings)b.RuntimeConfiguration.CaptureSettings()) with {Threshold=73});if(failConfigure)throw new InvalidOperationException("配置失败标记");},()=>{if(!failConfigure&&++refresh==1)throw new InvalidOperationException("刷新失败标记");});
                try{command.Execute(null);throw new Exception("失败插线竟然接纳");}catch(InvalidOperationException error){Require(error.Message.Contains("失败标记"),"丢失首个插线失败原因");}
                Require(edges.SequenceEqual(new[]{original,left})&&b.Position==new Point(20,30)&&left.SourcePort=="RightPort"&&left.ManualPathPoints?.Single()==new Point(8,9)&&((BlobSettings)b.RuntimeConfiguration!.CaptureSettings()).Threshold==100,"失败恢复遗漏图/位置/端口/路径/配置");
            }
        });
        Case("端口恢复观察者抛错仍完成其余端口与路由恢复",()=>
        {
            var path=new[]{new Point(1,2),new Point(3,4)};
            var edge=new WorkflowConnection("ports","a","b"){SourcePort="RightPort",TargetPort="LeftPort",ManualPathPoints=path};
            var command=new SunEyeVision.UI.Features.WorkflowUI.Commands.EditConnectionPortsCommand(edge,"BottomPort","TopPort");
            path[0]=new Point(999,999);command.Execute(null);
            System.ComponentModel.PropertyChangedEventHandler observer=(_,args)=>{if(args.PropertyName==nameof(edge.SourcePort))throw new InvalidOperationException("源端口恢复通知失败");};
            edge.PropertyChanged+=observer;
            try{command.Undo();throw new Exception("恢复通知异常被吞");}catch(InvalidOperationException error){Require(error.Message=="源端口恢复通知失败","恢复丢失原异常");}
            finally{edge.PropertyChanged-=observer;}
            Require(edge.SourcePort=="RightPort"&&edge.TargetPort=="LeftPort"&&edge.ManualPathPoints?.SequenceEqual(new[]{new Point(1,2),new Point(3,4)})==true,"恢复通知截断其余端口/深拷贝路由恢复");
        });
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root){
        var seen=new HashSet<DependencyObject>();var pending=new Stack<DependencyObject>();pending.Push(root);
        while(pending.Count>0)
        {
            var node=pending.Pop();if(!seen.Add(node))continue;yield return node;
            foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())pending.Push(child);
            if(node is SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView list)
                foreach(var row in list.Items.OfType<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>())if(row.View is {} view)pending.Push(view);
        }
    }
    private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void Until(Func<bool> complete){var end=DateTime.UtcNow.AddSeconds(30);while(!complete()){if(DateTime.UtcNow>end)throw new TimeoutException("实际宿主操作超时");Pump();Thread.Sleep(5);}Pump();}
    private static void Await(Task task){Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
    private static void Case(string name,Action action){action();_passed++;Console.WriteLine("通过："+name);}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {
        yield return root;
        if(root is not System.Windows.Media.Visual)yield break;
        for(var index=0;index<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);index++)
            foreach(var child in Visual(System.Windows.Media.VisualTreeHelper.GetChild(root,index)))yield return child;
    }

}
