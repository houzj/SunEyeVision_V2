using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
using SunEyeVision.UI.Services.Editing.Regions;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using Canvas=System.Windows.Controls.Canvas;

internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        try{return Run();}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static int Run()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        app.DispatcherUnhandledException+=(_,args)=>Console.Error.WriteLine("未处理UI异常："+args.Exception);
        var dialogs=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        dialogs.Tick+=(_,_)=>{foreach(Window dialog in app.Windows.Cast<Window>().Where(window=>window.GetType().Name.Contains("MessageDialog",StringComparison.Ordinal)).ToArray())
            {Console.Error.WriteLine("本检查进程的产品提示："+dialog.Title+" "+string.Join(" | ",Logicals(dialog).OfType<TextBlock>().Select(text=>text.Text)));dialog.Close();}};dialogs.Start();
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
        var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
        var plugins=new PluginManager();plugins.LoadPlugins(Path.Combine(product,"plugins"));plugins.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(tool=>tool.Id));PluginLoader.MarkAsLoaded();
        var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"document");Directory.CreateDirectory(folder);
        ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(folder);
        var solutions=ServiceInitializer.SolutionManager;
        Console.WriteLine("进度：开始创建真实MainWindowViewModel");var vm=new MainWindowViewModel();var document=solutions.CreateUntitledSolution();Console.WriteLine("进度：真实方案与主视图模型已创建");
        if(vm.WorkflowTabViewModel.SelectedTab is null)vm.NewWorkflowCommand.Execute(null);
        var tab=vm.WorkflowTabViewModel.SelectedTab??throw new Exception("正式新建方案没有工作流页");
        Console.WriteLine("进度：创建画布");var canvasControl=new WorkflowCanvasControl{DataContext=tab,Width=600,Height=500};var canvas=(Canvas)canvasControl.FindName("WorkflowCanvas");
        var image=new ImageControl{Width=500,Height=500};vm.GetMainImageControl=()=>image;
        var resultManager=(SunEyeVision.UI.Services.Workflow.NodeResultManager)typeof(MainWindowViewModel).GetField("_nodeResultManager",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
        resultManager.SetResultRenderLayer(image.ResultRenderLayerControl);
        var content=new StackPanel{Orientation=Orientation.Horizontal};content.Children.Add(canvasControl);content.Children.Add(image);
        var resultHost=new ContentControl{Width=620};resultHost.SetBinding(ContentControl.ContentProperty,new System.Windows.Data.Binding(nameof(MainWindowViewModel.SelectedNodeDataAnalysisControl)){Source=vm});content.Children.Add(resultHost);
        var shell=new Window{Content=content,ShowActivated=false,ShowInTaskbar=false,Left=-10000,Width=1760,Height=560};Console.WriteLine("进度：显示检查视口");shell.Show();shell.UpdateLayout();Pump();Require(canvas.ActualWidth>140,"实际画布尚未完成布局");Console.WriteLine("进度：检查视口已显示");
        // Use the production binding engine's dispatcher marshalling, not a test callback
        // that writes a dispatcher-owned ImageControl from a worker notification.
        image.SetBinding(ImageControl.SourceImageProperty,new System.Windows.Data.Binding(nameof(MainWindowViewModel.DisplayImage)){Source=vm.ImageResult});
        var drop=new WorkflowDragDropHandler(canvasControl){TabViewModel=vm.WorkflowTabViewModel};
        // Inject only the existing host lookup, not node creation or publication. The real drop path calls CreateNode/AddNodeToWorkflow.
        typeof(WorkflowDragDropHandler).GetField("_cachedViewModel",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,vm);
        typeof(WorkflowDragDropHandler).GetField("_viewModelCacheTime",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,DateTime.Now);
        SunEyeVision.UI.Shared.WorkflowNode Drop(string id)
        {
            var count=tab.WorkflowNodes.Count;var item=vm.Toolbox.AllTools.Single(tool=>tool.ToolId==id);
            var data=new DataObject("ToolItem",item);
            var constructor=typeof(DragEventArgs).GetConstructors(BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance)
                .Single(ctor=>ctor.GetParameters().Any(parameter=>parameter.ParameterType==typeof(IDataObject)));
            object? Argument(ParameterInfo parameter)=>parameter.ParameterType==typeof(IDataObject)?data:
                parameter.ParameterType==typeof(DragDropKeyStates)?DragDropKeyStates.None:
                parameter.ParameterType==typeof(DragDropEffects)?DragDropEffects.Copy:
                parameter.ParameterType==typeof(DependencyObject)?canvas:
                parameter.ParameterType==typeof(Point)?new Point(100+count*170,100):throw new Exception("未知的WPF拖放参数："+parameter);
            var args=(DragEventArgs)constructor.Invoke(constructor.GetParameters().Select(Argument).ToArray());
            Console.WriteLine("进度：Drop "+id);drop.Canvas_Drop(canvas,args);Require(tab.WorkflowNodes.Count==count+1,"实际Drop未添加节点："+id);
            return (SunEyeVision.UI.Shared.WorkflowNode)tab.WorkflowNodes.Last();
        }
        var input=Drop("ImageLoad");var blob=Drop("BlobAnalysis");var downstream=Drop("BlobAnalysis");
        var flow=document.Workflows.Single(workflow=>workflow.Id==tab.CurrentWorkflowId);
        Require(ReferenceEquals(flow.Nodes,tab.WorkflowNodes)&&flow.Nodes.Count==3,"拖入后方案与画布不是同一节点集合");
        var pixels=new byte[64*64];for(int y=5;y<9;y++)for(int x=5;x<9;x++)pixels[y*64+x]=200;
        for(int y=30;y<35;y++)for(int x=30;x<35;x++)pixels[y*64+x]=200;
        var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Gray8,null,pixels,64);bitmap.Freeze();
        var file=Path.Combine(folder,"source.png");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(file))png.Save(output);
        void Select(WorkflowNodeBase selected){foreach(var item in vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes)item.IsSelected=ReferenceEquals(item,selected);vm.SelectedNode=selected;}
        Select(input);
        var localSource=vm.ActiveInputSource??throw new Exception("图像源节点没有当前图像输入面板模型");
        localSource.AddImage(new SunEyeVision.UI.Shell.Controls.Panels.ImageInfo{Name="真实链输入",FilePath=file,SourceReference=file,IsForRun=true});
        var executionManager=(SunEyeVision.UI.Services.Workflow.WorkflowExecutionManager)typeof(MainWindowViewModel).GetField("_executionManager",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(vm)!;
        executionManager.WorkflowExecutionError+=(_,args)=>Console.Error.WriteLine("主流程实际错误："+args.ErrorMessage);
        executionManager.WorkflowExecutionStarted+=(_,args)=>Console.WriteLine("进度：主流程开始 "+args.WorkflowId);
        executionManager.WorkflowExecutionCompleted+=(_,args)=>Console.WriteLine("进度：主流程完成 "+args.WorkflowId);
        executionManager.NodeExecutionCompleted+=(_,args)=>Console.WriteLine("进度：节点返回 "+args.Node.ToolType+" "+args.Node.Id+" 成功="+args.Result.IsSuccess+" 当前="+args.Result.ExecutionIdentity?.IsCurrent);
        vm.AddConnectionToWorkflow(new WorkflowConnection("input-blob",input.Id,blob.Id));vm.AddConnectionToWorkflow(new WorkflowConnection("blob-downstream",blob.Id,downstream.Id));
        Console.WriteLine("通过：正式工具箱项→WPF Drop→CreateNode/AddNodeToWorkflow→方案共享定义与实际连线；使用本地测试图像，不访问设备");
        Select(blob);Until(()=>!vm.IsRunning,TimeSpan.FromSeconds(20));Await(vm.RunSelectedImageAsync());Pump();
        WorkflowContext Context()=>(WorkflowContext)typeof(MainWindowViewModel).GetProperty("CurrentWorkflowContext",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
        ToolResults Result(WorkflowNodeBase node)=>Context().GetNodeResult(node.Id)?.Result??throw new Exception("没有正式节点结果："+node.Id);
        int Count(ToolResults result)=>Convert.ToInt32(result.GetType().GetProperty("FilteredBlobCount")!.GetValue(result));
        void VerifyResourceFrame(ToolResults result)
        {
            var outputs=result.GetOutputVariables();
            var gray=outputs.Single(output=>output.Name=="GrayImage");
            var region=outputs.Single(output=>output.Name=="Region");
            Require(gray.RuntimeValue is SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage,"SDK图像来源失去现行IVisionImage消费合同");
            using var snapshot=result.DeclaredOutput!.AcquireReader();
            var frozenImage=snapshot.Values!.Get<SunEyeVision.Plugin.Contracts.IReadOnlyImage>(BindingClrAdapter.OutputPortId(gray));
            var frozenRegion=snapshot.Values.Get<SunEyeVision.Plugin.Contracts.IReadOnlyRegion>(BindingClrAdapter.OutputPortId(region));
            Require(frozenImage.Frame==frozenRegion.SourceFrame,"正式冻结图像和区域丢失共同来源帧身份");
        }
        Require(Count(Result(blob))==2&&Count(Result(downstream))==2,"正式图像载入→Blob→下游没有产生两个对象");
        VerifyResourceFrame(Result(blob));
        Require(image.SourceImage is not null,"生产执行没有刷新主图像");
        Console.WriteLine("通过：MainWindow正式执行入口读取PNG，真实HALCON及下游链运行，主图像同步更新");
        // GrayImage intentionally remains the original gray input. To consume the filtered
        // final set, configure the downstream Region source explicitly through its real UI.
        Select(downstream);
        var downstreamOpen=new NodeOpenContext(shell,image);Require(new NodeOpenDecisionChain().Execute(downstream,downstreamOpen),"下游配置窗口打开失败");
        var downstreamWindow=(DefaultDebugWindow)downstreamOpen.CreatedWindow!;downstreamWindow.Left=-10000;downstreamWindow.ShowActivated=false;downstreamWindow.ShowInTaskbar=false;
        Await((Task)typeof(MainWindowViewModel).GetMethod("InitializeToolControlAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vm,new object[]{downstreamWindow,downstream})!);Pump();
        var downstreamEditor=(DeclaredToolConfigurationControl)downstreamOpen.DebugControl!;
        var downstreamContext=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(downstreamEditor)!;
        var downstreamProvider=(IConfigurationFieldProvider)downstreamContext.DraftNode.Parameters;
        var regionGroup=downstreamProvider.ConfigurationSchema!.Presentation!.Regions.Single(group=>group.Role=="inspection");
        downstreamProvider.GetConfigurationFields().Single(field=>field.Port.Id==regionGroup.ModeFieldId).Write("InheritedRegion");downstreamEditor.OnExternalResultArrived();
        var regionOutput=((IOutputContractProvider)blob.Parameters).GetOutputContract().Single(output=>output.Name=="Region");
        var regionSelector=Logicals(downstreamEditor).OfType<SourceSelectionField>().Single(field=>Equals(field.Tag,regionGroup.RegionSourceFieldId));
        regionSelector.Select(blob.Id+"/"+BindingClrAdapter.OutputPortId(regionOutput));Require(regionSelector.ApplySelection(),regionSelector.Error);
        Require(downstreamWindow.TryConfirm(out var downstreamError),downstreamError);Pump();Select(blob);
        Console.WriteLine("通过：真实下游配置窗口显式订阅上游最终Region；原始灰度图语义保持，区域绑定经草稿确认发布");
        var opening=new NodeOpenContext(shell,image);Require(new NodeOpenDecisionChain().Execute(blob,opening),"真实节点打开链失败");
        var editor=(DeclaredToolConfigurationControl)opening.DebugControl!;
        var window=(DefaultDebugWindow)opening.CreatedWindow!;window.Left=-10000;window.ShowActivated=false;window.ShowInTaskbar=false;
        Await((Task)typeof(MainWindowViewModel).GetMethod("InitializeToolControlAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vm,new object[]{window,blob})!);Pump();
        var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(editor)!;
        Require(context.IsValid&&window.IsVisible&&editor.RegionViewport is RoiViewportEditScope,"真实主窗口初始化缺少草稿/图像编辑范围");
        var roi=(RoiViewportEditScope)editor.RegionViewport!;Require(roi.BeginDraw("inspection","rectangle",out var roiError),roiError);roi.PointerDown(new(3,3));roi.PointerMove(new(12,12));roi.PointerUp(new(12,12));
        Require(((RoiParameters)blob.Parameters).InspectionRois.Rois.Count==0,"画ROI提前污染正式参数");
        SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventArgs? execution=null;
        var completed=0;window.ToolExecutionCompleted+=(_,args)=>{execution=args;completed++;};
        ToolCommands.Execute.Execute(null,window);Until(()=>execution is not null,TimeSpan.FromSeconds(20));
        Require(execution!.Results is ToolResults debug&&debug.IsSuccess&&Count(debug)==1,"窗口调试未产生一颗Blob："+execution.ErrorMessage);
        Require(context.DebugResults.GetNodeResult(blob.Id)?.Result is { } draftResult&&Count(draftResult)==1&&Count(Result(blob))==2,"调试结果没有正确隔离于生产结果");
        var analysis=vm.SelectedNodeDataAnalysisControl??throw new Exception("没有数据分析视图");
        var table=Logicals(analysis).OfType<DataGrid>().Single();Require(table.Items.Count==1,"真实事件链没有刷新Blob结果表");
        table.SelectedItem=table.Items[0];Pump();
        var highlight=typeof(ResultRenderLayer).GetField("_highlightItems",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(image.ResultRenderLayerControl) as System.Collections.IEnumerable;
        Require(highlight?.Cast<object>().Count()==1,"结果行选择未通过真实主窗口订阅形成图像高亮");
        shell.UpdateLayout();var rendering=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);rendering.Render(content);
        var renderedPng=new PngBitmapEncoder();renderedPng.Frames.Add(BitmapFrame.Create(rendering));using(var rendered=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"full-chain.png")))renderedPng.Save(rendered);
        Console.WriteLine("通过：真实节点窗口初始化、私有ROI、窗口执行命令→HALCON→主窗口事件→草稿结果/结果表；正式结果未污染");
        var inspectionMode=((IConfigurationFieldProvider)context.DraftNode.Parameters).GetConfigurationFields().Single(field=>field.StoragePath=="InspectionMode");
        inspectionMode.Write("InheritedRegion");editor.OnExternalResultArrived();execution=null;
        ToolCommands.ContinuousExecute.Execute(null,window);Until(()=>execution is not null,TimeSpan.FromSeconds(20));
        var failureCount=completed;var delay=System.Diagnostics.Stopwatch.StartNew();Until(()=>delay.ElapsedMilliseconds>=650,TimeSpan.FromSeconds(2));
        Require(completed==failureCount&&!execution!.IsSuccess&&!string.IsNullOrEmpty(execution.ErrorMessage)
            &&((TextBlock)window.FindName("ExecutionMessage")).Visibility==Visibility.Visible,"失败被报成成功、错误不可见或连续运行仍反复失败");
        Require(execution!.Results is ToolResults failed&&!failed.IsSuccess&&table.Items.Count==0
            &&Convert.ToDouble(analysis.GetType().GetProperty("FilteredTotalArea")!.GetValue(analysis))==0,"运行失败后结果表或统计残留旧值");
        var dormant=typeof(ResultRenderLayer).GetField("_regionItems",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(image.ResultRenderLayerControl) as System.Collections.IEnumerable;
        Require(dormant is null||!dormant.Cast<object>().Any(),"像素区域模式仍显示本地底稿");
        inspectionMode.Write("LocalGeometry");editor.OnExternalResultArrived();execution=null;
        ToolCommands.Execute.Execute(null,window);Until(()=>execution is not null,TimeSpan.FromSeconds(20));Require(execution!.Results is ToolResults recovered&&recovered.IsSuccess&&Count(recovered)==1&&table.Items.Count==1,"修正来源后窗口执行未恢复");
        Console.WriteLine("通过：缺失激活Region使实际调试失败并清空旧表/摘要/休眠区域；修正后同一窗口可继续执行");
        Require(window.TryConfirm(out var confirmError),confirmError);Pump();
        Require(!context.IsValid&&((RoiParameters)blob.Parameters).InspectionRois.Rois.Count==1,"确定未发布ROI或关闭未退出范围");
        Await(vm.RunSelectedImageAsync());Pump();Require(Count(Result(blob))==1&&Count(Result(downstream))==1,"确认后的生产与下游没有采用新ROI");
        VerifyResourceFrame(Result(blob));
        var solutionFile=Path.Combine(folder,"full-chain.solution");var save=new SunEyeVision.UI.Services.Solutions.SolutionApplicationService(solutions);
        Require(save.Save(solutionFile,()=>solutions.Session!.Editing.AcceptAll()).FileWritten,"实际保存失败："+solutions.LastError);
        var reopened=solutions.LoadSolutionOnly(solutionFile)??throw new Exception(solutions.LastError);solutions.SetCurrentSolution(reopened);Pump();
        var restored=reopened.Workflows.Single().Nodes.Single(node=>node.Id==blob.Id);Select(restored);
        Await(vm.RunSelectedImageAsync());Pump();Require(Count(Result(restored))==1,"保存重开后真实执行未保留区域");
        Console.WriteLine("通过：窗口确定发布、主流程与下游采用ROI，实际保存→方案切换重开→再次真实执行一致");
        var cancelledOpen=new NodeOpenContext(shell,image);Require(new NodeOpenDecisionChain().Execute(restored,cancelledOpen),"重开节点窗口失败");
        var cancelledWindow=(DefaultDebugWindow)cancelledOpen.CreatedWindow!;cancelledWindow.Left=-10000;cancelledWindow.ShowActivated=false;cancelledWindow.ShowInTaskbar=false;
        Await((Task)typeof(MainWindowViewModel).GetMethod("InitializeToolControlAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vm,new object[]{cancelledWindow,restored})!);Pump();
        var cancelledEditor=(DeclaredToolConfigurationControl)cancelledOpen.DebugControl!;
        var cancelledContext=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(cancelledEditor)!;
        var threshold=((IConfigurationFieldProvider)cancelledContext.DraftNode.Parameters).GetConfigurationFields().Single(field=>field.StoragePath=="Threshold");threshold.Write(230L);
        cancelledWindow.Close();Pump();
        Require(!cancelledContext.IsValid&&Convert.ToInt32(restored.Parameters.GetType().GetProperty("Threshold")!.GetValue(restored.Parameters))==128,"关闭取消未退出范围或写回未确认参数");
        Require(solutions.Session!.Editing.RegisteredScopeCount==0,"关闭后仍残留编辑范围");
        Console.WriteLine("通过：重开窗口后修改再关闭仅丢弃私有草稿，正式参数保留，编辑范围退出");
        restored.DebugWindowStyle=SunEyeVision.Plugin.SDK.Core.DebugWindowStyle.Custom;
        var custom=new NodeOpenContext(shell,image);Require(new NodeOpenDecisionChain().Execute(restored,custom),"请求自定义布局时打开失败");
        Require(custom.CreatedWindow is DefaultDebugWindow,"声明配置错误使用了依赖专用页自带执行/转发的旧自定义壳");
        custom.CreatedWindow!.Left=-10000;custom.CreatedWindow.ShowActivated=false;custom.CreatedWindow.ShowInTaskbar=false;
        Await((Task)typeof(MainWindowViewModel).GetMethod("InitializeToolControlAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vm,new object[]{custom.CreatedWindow,restored})!);Pump();custom.CreatedWindow.Close();Pump();
        Console.WriteLine("通过：声明编辑器即使请求Custom布局也使用完整通用壳，执行与结果转发不依赖已删除的专用页");
        shell.Close();Await(ServiceInitializer.DisposeCommunicationRuntimeAsync().AsTask());
        Console.WriteLine("S4真实宿主连续链：8组通过；管理事件与命令路径，不冒充物理鼠标拖拽或用户人工验收。");return 0;
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
    private static IEnumerable<DependencyObject> Logicals(DependencyObject root){yield return root;foreach(var item in LogicalTreeHelper.GetChildren(root))if(item is DependencyObject child)foreach(var next in Logicals(child))yield return next;}
    private static void Await(Task task){Until(()=>task.IsCompleted,TimeSpan.FromSeconds(30));task.GetAwaiter().GetResult();}
    private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void Until(Func<bool> done,TimeSpan timeout)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();var frame=new DispatcherFrame();
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(10)};
        timer.Tick+=(_,_)=>{if(done()||clock.Elapsed>timeout)frame.Continue=false;};timer.Start();try{Dispatcher.PushFrame(frame);}finally{timer.Stop();}
        Require(done(),"等待正式宿主异步操作超时");
    }
}
