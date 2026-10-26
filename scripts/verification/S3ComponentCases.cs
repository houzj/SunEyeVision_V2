using System.IO;
using System.Windows;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Workflow;
using ServiceInitializer=SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer;
using DataSourceQueryService=SunEyeVision.Plugin.SDK.Execution.Parameters.DataSourceQueryService;

internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        try
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledExceptionFilter+=(_,args)=>
            {
                File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"unhandled-ui-error.log"),args.Exception+Environment.NewLine);
                // Do not let the product App replace a failing test with a modal
                // crash dialog. The exception must escape and fail this runner.
                args.RequestCatch=false;
            };
            bool expectEditor=Environment.GetEnvironmentVariable("S3_EXPECT_EDITOR")!="0";
            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var manager=new PluginManager();
            if(Environment.GetEnvironmentVariable("S3_USE_GLOBAL_LOADER")=="1")
            {
                var admitted=PluginLoader.LoadPlugins(Path.Combine(product,"plugins"));
                if(Environment.GetEnvironmentVariable("S3_EXPECT_REJECTION")=="1")
                {
                    Require(!admitted&&!PluginLoader.IsLoaded,"全局装载器将失败标为已完成");
                    Console.WriteLine("通过：正式全局装载器拒绝不完整组件且不设置已完成");return 0;
                }
                Require(admitted&&PluginLoader.IsLoaded,"全局装载器未接纳完整组件");
            }
            else
            {
                manager.LoadPlugins(Path.Combine(product,"plugins"));
                manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(x=>x.Id));
            }
            var components=ToolRegistry.GetComponentMetadata();
            Require(components.Count(c=>c.ToolId=="BlobAnalysis"&&c.Role==ToolComponentRole.Runtime)==1,"Blob没有唯一Runtime");
            Require(components.Count(c=>c.ToolId=="BlobAnalysis"&&c.Role==ToolComponentRole.Editor)==(expectEditor?1:0),"可选Editor登记不符");
            Console.WriteLine("通过：正式PluginManager发现/准入/注册，单ToolId及组件角色正确");
            var parameters=ToolRegistry.CreateParameters("BlobAnalysis");
            void Set(string name,object value)=>parameters.GetType().GetProperty(name)!.SetValue(parameters,value);
            Set("Threshold",100);Set("IsAreaFilterEnabled",false);Set("IsBlobCountFilterEnabled",false);Set("OutputBlobImage",true);
            var node=new WorkflowNodeBase("blob","blob","Blob","BlobAnalysis"){Parameters=parameters};
            var tool=node.CreateInstance()??throw new Exception("节点无法创建工具");
            var editor=ToolRegistry.CreateEditorFactory(tool);
            Require(editor is null,"Blob结果页仍依赖旧SDK视图工厂");
            EnsurePresentationApplication();
            using var resultView=ToolRegistry.CreateResultView("BlobAnalysis")??throw new Exception("Runtime声明没有结果视图");
            if(expectEditor)
            {
                var debug=SunEyeVision.UI.Services.Editing.ToolConfigurationEditors.CreateControl(tool);
                Require(debug is SunEyeVision.UI.Services.Editing.DeclaredToolConfigurationControl
                    &&resultView.View.GetType().Assembly.GetName().Name=="SunEyeVision.Tool.BlobAnalysis.Wpf","默认配置或独立Wpf结果视图装配错误");
                var refs=resultView.View.GetType().Assembly.GetReferencedAssemblies().Select(reference=>reference.Name).ToArray();
                Require(!refs.Contains("SunEyeVision.Plugin.SDK")&&!refs.Contains("SunEyeVision.UI"),"Blob.Wpf直接依赖SDK或UI宿主");
                Console.WriteLine("通过：Runtime提供结果声明、可选Wpf中立工厂供应结果页且不引用SDK；配置仍由宿主声明页组装");
            }
            else
            {
                Require(resultView is SunEyeVision.Plugin.Wpf.Presenters.Results.DeclaredResultTable,"缺少Wpf时没有通用声明结果表");
                Console.WriteLine("通过：缺少Wpf时保留算法与通用声明结果表");
            }
            if(!expectEditor)
            {
                void Progress(string step){Console.WriteLine("降级窗口进度："+step);File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"fallback-editor-progress.log"),step+Environment.NewLine);}
                EnsurePresentationApplication();
                System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
                var documentFolder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"fallback-document");Directory.CreateDirectory(documentFolder);
                ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(documentFolder);
                var context=new SunEyeVision.UI.Services.Editing.NodeOpenContext(null!,null);
                Progress("创建真实节点打开链");
                Require(new SunEyeVision.UI.Services.Editing.NodeOpenDecisionChain().Execute(node,context)
                    &&context.DebugControl is SunEyeVision.UI.Services.Editing.DeclaredToolConfigurationControl
                    &&context.CreatedWindow is not null,"缺编辑器时真实节点打开链没有基础配置入口");
                var fallback=(SunEyeVision.UI.Services.Editing.DeclaredToolConfigurationControl)context.DebugControl;
                Progress("节点打开链已创建基础窗口");
                var editorFlow=new SunEyeVision.Workflow.Workflow("component-editor","缺少可选组件的配置草稿");editorFlow.Nodes.Add(node);
                var editorSolution=new Solution();editorSolution.Workflows.Add(editorFlow);
                ServiceInitializer.SolutionManager.SetCurrentSolution(editorSolution);
                var edits=ServiceInitializer.SolutionManager.Session!.Editing;
                void BindDraft(WorkflowNodeBase draft){fallback.SetCurrentNode(draft);fallback.SetParameters(draft.Parameters);fallback.SetDataProvider(new DataSourceQueryService());}
                using var draftContext=new SunEyeVision.UI.Services.Editing.ProductionEditorContext(edits.Host,editorFlow.Id,node,()=>true,BindDraft,documentEdits:edits);
                BindDraft(draftContext.DraftNode);
                SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.AttachDraftContext(fallback,draftContext);
                var formalThreshold=parameters.GetType().GetProperty("Threshold")!.GetValue(parameters);
                Progress("私有草稿上下文已附着");
                var thresholdId=SunEyeVision.Tool.BlobAnalysis.BlobInputContract.Fields.Single(field=>field.LegacyPath=="Threshold").Port.Id;
                if(!context.CreatedWindow.IsVisible)context.CreatedWindow.Show();
                Progress("窗口已显示");
                context.CreatedWindow.UpdateLayout();
                Progress("窗口同步布局已完成");
                var dispatcher=System.Windows.Threading.Dispatcher.CurrentDispatcher;
                Require(!dispatcher.HasShutdownStarted,"验证宿主Dispatcher提前关闭");
                var frame=new System.Windows.Threading.DispatcherFrame();
                dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                Progress("真实模板已完成布局");
                var fields=Find<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView>(fallback).Single();
                var pages=Find<System.Windows.Controls.TabControl>(fields).Single();
                pages.SelectedItem=pages.Items.Cast<System.Windows.Controls.TabItem>().Single(tab=>Equals(tab.Header,"运行参数"));
                context.CreatedWindow.UpdateLayout();
                dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var threshold=fields.Fields.Single(field=>field.FieldId==thresholdId).InputElement as System.Windows.Controls.TextBox
                    ??throw new Exception("真实数值模板没有输入框");
                Require(threshold.IsVisible&&threshold.ActualWidth>0,"降级窗口没有显示实际待检数值字段");
                DependencyObject? ancestor=threshold;
                var ancestry=new List<string>();
                while(ancestor is not null&&ancestor is not SunEyeVision.Plugin.Wpf.Fields.NativeNumberField)
                {
                    ancestry.Add(ancestor.GetType().AssemblyQualifiedName!);
                    ancestor=System.Windows.Media.VisualTreeHelper.GetParent(ancestor);
                }
                Require(ancestor is SunEyeVision.Plugin.Wpf.Fields.NativeNumberField numeric&&ReferenceEquals(numeric.TextEditor,threshold)
                    &&numeric.NativeControl is SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.SliderArrange,
                    "降级窗口未使用已迁入的数值控件模板；实际="+(ancestor is SunEyeVision.Plugin.Wpf.Fields.NativeNumberField actual?actual.NativeControl.GetType().AssemblyQualifiedName:"无适配祖先："+string.Join(" → ",ancestry)));
                threshold.Text="210";
                Progress("有效数字已键入");
                Require(Convert.ToInt32(draftContext.DraftNode.Parameters.GetType().GetProperty("Threshold")!.GetValue(draftContext.DraftNode.Parameters))==210
                    &&Equals(parameters.GetType().GetProperty("Threshold")!.GetValue(parameters),formalThreshold),"默认配置页未写入私有草稿或提前污染正式参数");
                threshold.Text="invalid";
                Progress("无效数字已键入");
                Require(!fallback.TryValidateEditor(out var invalid)&&invalid.Length>0,"基础配置入口接受了无效数字");
                threshold.Text="100";
                Progress("已修正数字");
                Require(fallback.TryValidateEditor(out _),"基础配置入口无法从无效草稿恢复");
                context.CreatedWindow.Close();
                Progress("窗口已关闭");
                Require(Equals(parameters.GetType().GetProperty("Threshold")!.GetValue(parameters),formalThreshold),"关闭降级窗口未丢弃私有草稿");
                Console.WriteLine("通过：缺专用编辑器时，真实节点打开基础配置窗口并按声明编辑参数");
            }
            using var image=HalImage.CreateFromBytes(Enumerable.Repeat((byte)200,64).ToArray(),8,8,1);
            var engine=new WorkflowEngine(new CheckLogger());var flow=engine.CreateWorkflow("S3-component-flow","组件验证");flow.Nodes.Add(node);
            var executor=new WorkflowExecutionEngine(engine,manager,new CheckLogger());
            System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
            using var execution=WithoutUiPump(()=>executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,image)));
            Require(execution.Success&&execution.NodeResults.TryGetValue("blob",out var nodeResult),"正式工作流失败："+string.Join(";",execution.Errors??new()));
            var result=execution.NodeResults["blob"].Result??throw new Exception("工作流没有Blob结果");
            Require(result.IsSuccess&&Convert.ToInt32(result.GetType().GetProperty("FilteredBlobCount")!.GetValue(result))==1&&Convert.ToDouble(result.GetType().GetProperty("FilteredTotalArea")!.GetValue(result))==64,"真实节点结果不符");
            Require(execution.DeclaredOutputs.Count==1,"正式执行未发布声明结果");
            Console.WriteLine("通过：正式节点与WorkflowExecutionEngine真实HALCON执行、声明结果发布，Blob面积64");
            {
                var grid=Find<System.Windows.Controls.DataGrid>(resultView.View).Single();
                IReadOnlyList<GeometryValue>? selectedGeometry=null;
                resultView.SelectionHighlightChanged+=(_,geometry)=>selectedGeometry=geometry;
                using(var published=result.DeclaredOutput?.AcquireReader()??throw new Exception("真实运行没有发布声明值"))
                    resultView.Update(published.Values??throw new Exception("真实运行没有发布值"));
                execution.Dispose();
                var resultWindow=new Window{Content=resultView.View,Width=700,Height=350,Left=-10000,ShowActivated=false,ShowInTaskbar=false};
                resultWindow.Show();resultWindow.UpdateLayout();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Require(grid.Items.Count==1&&grid.Columns.Count==8,$"中立结果视图未显示声明行和列：Rows={grid.Items.Count},Columns={grid.Columns.Count}");
                grid.SelectedItem=grid.Items[0];
                Require(selectedGeometry is {Count:1}&&selectedGeometry[0] is PolygonGeometry polygon&&polygon.Vertices.Count==4,
                    "结果资源释放后视图未保留独立的不可变选中几何");
                resultView.Update(null);Require(grid.Items.Count==0&&selectedGeometry is null,"失败清空后行或高亮残留");
                resultView.Dispose();
                resultWindow.Close();
                var rejected=false;try{resultView.Update(null);}catch(ObjectDisposedException){rejected=true;}
                Require(rejected,"已释放结果视图继续更新");
                Console.WriteLine("通过：中立视图只复制显示值/几何，资源scope关闭后仍可选择；空更新清空行和高亮，释放后拒绝更新");
            }
            Require(executor.CurrentState==WorkflowExecutionState.Idle&&executor.CurrentContext is null,"运行请求返回后会话未清理");
            foreach(var start in new Func<Task<ExecutionResult>>[]{()=>executor.ExecuteWorkflowAsync(flow.Id,image),()=>executor.ExecuteWorkflow(flow.Id,image)})
            {
                using var repeated=WithoutUiPump(start);
                Require(repeated.Success&&executor.CurrentState==WorkflowExecutionState.Idle&&executor.CurrentContext is null,
                    "兼容执行入口仍依赖UI回调或未释放会话");
            }
            Console.WriteLine("通过：三个正式执行入口在WPF上下文且不处理UI消息时完成真实HALCON执行与会话清理");
            using(var cancelledSource=new CancellationTokenSource())
            {
                cancelledSource.Cancel();
                using var cancelled=WithoutUiPump(()=>executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,image),cancelledSource.Token));
                Require(!cancelled.Success&&cancelled.Status==ExecutionStatus.Cancelled
                    &&executor.CurrentState==WorkflowExecutionState.Idle&&executor.CurrentContext is null,
                    "取消结果或取消后的会话清理仍依赖UI");
            }
            using(var recovered=WithoutUiPump(()=>executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,image))))
                Require(recovered.Success,"取消清理后无法开始下一次运行");
            Console.WriteLine("通过：已取消请求在无UI消息泵时返回Cancelled并清理，下一次真实运行成功");
            Console.WriteLine($"S3组件消费：{(expectEditor?6:7)}组通过；Editor={(expectEditor?"存在":"缺席")}。不是完整新UI或整个S3验收。");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void EnsurePresentationApplication()
    {
        if(Application.Current is not null)return;
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        // Component consumption pumps WPF messages, so constructing the product
        // App here would also start its interactive bootstrap. Actual App entry
        // is verified separately by the deployment script's executable probes.
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Themes/Dark/DarkTheme.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
    }
    private static ExecutionResult WithoutUiPump(Func<Task<ExecutionResult>> start)
    {
        var task=start();
        // Deliberately do not pump Dispatcher: execution and root-session cleanup
        // must finish even while the UI caller is unable to process callbacks.
        Require(task.Wait(TimeSpan.FromSeconds(30)),"工作流完成仍在等待UI上下文");
        return task.GetAwaiter().GetResult();
    }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T item)yield return item;
        foreach(var child in LogicalTreeHelper.GetChildren(root))
            if(child is DependencyObject dependency)
                foreach(var found in Find<T>(dependency))yield return found;
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
    private sealed class CheckLogger:ILogger
    {
        public ILogger ForSource(string source)=>this;
        public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All)
        {if(level==LogLevel.Error)Console.Error.WriteLine(message+" "+exception);}
    }
}
