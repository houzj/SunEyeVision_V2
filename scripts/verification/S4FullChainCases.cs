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
        void Select(WorkflowNodeBase n){foreach(var item in vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes)item.IsSelected=ReferenceEquals(item,n);vm.SelectedNode=n;Pump();}
        WorkflowContext Context()=>(WorkflowContext)typeof(MainWindowViewModel).GetProperty("CurrentWorkflowContext",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
        RuntimeToolResults Result(string id)=>Context().GetNodeResult(id)?.Result as RuntimeToolResults??throw new Exception("缺少Runtime结果："+id);
        int Count(string id)=>((BlobOutput)Result(id).Output!).Count;
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
            var a=blob.ParamSettings.GetSetting(BlobTool.ImageInput);var b=downstream.ParamSettings.GetSetting(BlobTool.ImageInput);
            Console.WriteLine("自动来源状态："+System.Text.Json.JsonSerializer.Serialize(new[]{a,b}));
            Require(a?.AutomaticConnectionId=="input-blob"&&b?.AutomaticConnectionId=="blob-next"&&a.ReferenceExpression is not null&&b.ReferenceExpression is not null,"默认图像订阅或所有权缺失");
            Require(SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Deserialize(b.ReferenceExpression).PortId==BlobSchema.Field(5),"未按Runtime图像声明选源");
        });
        Case("连线及Runtime自动来源共享一次正式历史提交，撤销重做不留下外部配置变化",()=>
        {
            var host=solutions.Session!.Editing.Host;
            host.BeginEdit(flow.Id,downstream.Id);
            tab.CommandManager.Undo();
            Require(!flow.Connections.Any(edge=>edge.Id=="blob-next")&&downstream.ParamSettings.GetSetting(BlobTool.ImageInput) is null,"撤销连线遗留自动来源");
            host.BeginEdit(flow.Id,downstream.Id);
            tab.CommandManager.Redo();
            Require(flow.Connections.Any(edge=>edge.Id=="blob-next")&&downstream.ParamSettings.GetSetting(BlobTool.ImageInput)?.AutomaticConnectionId=="blob-next","重做没有恢复同一自动来源");
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
            var popup=Visual(inputField).OfType<SunEyeVision.Plugin.Wpf.Native.Input.Apps.PcUI.Controls.PopupButton>().Single(item=>item.Name=="rt");
            popup.ApplyTemplate();popup.Focus();popup.IsDropDownOpen=true;Pump();
            var nativePopup=(System.Windows.Controls.Primitives.Popup)popup.Template.FindName("PART_Popup",popup);
            var tree=Visual(nativePopup.Child).OfType<SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.SubTreeView>().Single();
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
            Require(context.DebugResults.GetNodeResult(blob.Id)!.Result is RuntimeToolResults {Output:BlobOutput {Count:0}}&&ReferenceEquals(production,Result(blob.Id)),"私有调试污染正式结果");
            Require(Logical(vm.SelectedNodeDataAnalysisControl!).OfType<DataGrid>().Single().Items.Count==0,"实际结果表未显示私有结果");
            window.Close();Pump();Require(solutions.Session!.Editing.RegisteredScopeCount==0&&((BlobSettings)blob.RuntimeConfiguration!.CaptureSettings()).Threshold==128,"关闭污染正式值或遗留范围");
        });
        Case("删除自动来源阻断运行，不静默回退；显式选择不受旧连线删除影响",()=>
        {
            var copy=blob.Clone();Require(ConnectionBindingPolicy.RemoveAutomaticImageBinding(copy,"input-blob"),"自动来源未识别");
            var marked=copy.ParamSettings.GetSetting(BlobTool.ImageInput)!;Require(marked.RebindingReason is not null&&marked.AutomaticConnectionId is null,"未保留重选状态");
            var copied=ParameterBindingDefinition.FromSetting(marked).ToSetting();Require(copied.RebindingReason==marked.RebindingReason&&!copied.IsValid&&copied.Clone().RebindingReason==marked.RebindingReason,"绑定快照/复制丢失重选状态");
            try{using var request=RuntimeDebugInvocation.Capture(flow,copy,new SunEyeVision.Plugin.SDK.Core.ToolExecutionOwner(),1,id=>Context().GetNodeResult(id)?.Result,default,publishedOwner:document.ExecutionOwner);throw new Exception("删除自动连线后仍执行");}
            catch(InvalidOperationException){}
            var manual=blob.Clone();var explicitSource=manual.ParamSettings.GetSetting(BlobTool.ImageInput)!.Clone();explicitSource.AutomaticConnectionId=null;manual.ParamSettings.SetSetting(explicitSource);
            Require(!ConnectionBindingPolicy.RemoveAutomaticImageBinding(manual,"input-blob")&&manual.ParamSettings.GetSetting(BlobTool.ImageInput)!.ReferenceExpression==explicitSource.ReferenceExpression,"删除旧连线清除了显式来源");
        });
        var path=Path.Combine(folder,"runtime-host.solution");
        Case("正式保存重开恢复唯一Runtime模型、语义来源及自动连线所有权，再运行真实HALCON",()=>
        {
            var saved=solutions.SaveCurrentDocument(path);Require(saved.FileWritten,"正式保存失败："+solutions.LastError+" "+System.Text.Json.JsonSerializer.Serialize(saved));var text=File.ReadAllText(path);
            Require(!text.Contains("BlobAnalysisParameters")&&!text.Contains("FilteredBlobCount"),"方案仍含SDK Blob模型");
            var reopened=solutions.LoadSolutionOnly(path)??throw new Exception(solutions.LastError);solutions.SetCurrentSolution(reopened);Pump();
            var restored=(SunEyeVision.UI.Shared.WorkflowNode)vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.Single(n=>n.Id==blob.Id);
            Require(restored.RuntimeConfiguration?.SettingsType==typeof(BlobSettings)&&((BlobSettings)restored.RuntimeConfiguration.CaptureSettings()).BlobCountLimit==1&&restored.ParamSettings.GetSetting(BlobTool.ImageInput)?.AutomaticConnectionId=="input-blob","重开丢失强类型值或来源所有权");
            foreach(var key in new[]{"AutomaticConnectionId","UnknownField"})
            {
                var malformed=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
                var stored=malformed["Workflows"]![0]!["Nodes"]!.AsArray().Single(n=>n!["Id"]!.GetValue<string>()==blob.Id)!["ParamSettings"]!["Settings"]!.AsArray().Single(s=>s!["ParameterName"]!.GetValue<string>()==BlobTool.ImageInput)!;
                stored[key]=17;var invalid=Path.Combine(folder,"invalid-"+key+".solution");File.WriteAllText(invalid,malformed.ToJsonString());
                var repository=new SolutionRepository();Require(repository.LoadCandidate(invalid).Candidate is null&&
                    repository.LastError?.Contains(key=="UnknownField"?"Unknown parameter setting member":"Binding ownership",StringComparison.Ordinal)==true,"持久化未按错型所有权或未知字段拒绝："+repository.LastError);
            }
            Select(restored);Execute();Require(Count(restored.Id)==1&&Count(downstream.Id)==1,"重开未执行新Runtime链");
            var window=Open(restored);Field(window,"Threshold").Text="230";window.Close();Pump();Require(((BlobSettings)restored.RuntimeConfiguration.CaptureSettings()).Threshold==128,"重开取消污染正式值");
        });
        var view=vm.SelectedNodeDataAnalysisControl;vm.SelectedNode=null;Pump();Require(vm.SelectedNodeDataAnalysisControl is null,"选择退出仍保留结果页");
        if(!File.Exists(Path.Combine(product,"plugins/SunEyeVision.Tool.BlobAnalysis/SunEyeVision.Tool.BlobAnalysis.dll")))
            Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="SunEyeVision.Tool.BlobAnalysis"),"缺少SDK入口时仍加载旧Blob");
        imageHost.Shutdown();shell.Close();dialogs.Stop();Console.WriteLine($"S4真实Runtime宿主连续链：{_passed}组通过；默认工具箱/真实Drop/连线/运行/ROI引用/私有调试/保存重开；未验证本地ROI、全部VM视觉或人工验收。");return 0;
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
