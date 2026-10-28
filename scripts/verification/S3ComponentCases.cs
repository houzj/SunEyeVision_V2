using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using System.IO;
using System.Windows;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private static ResultPublication CreatePublication(DeclaredOutputSnapshot snapshot)
    {
        var reader=snapshot.AcquireReader();
        try
        {
            var state=snapshot.Status switch
            {
                ExecutionStatus.NotExecuted=>ResultViewState.NotExecuted,ExecutionStatus.Running=>ResultViewState.Running,
                ExecutionStatus.Success=>ResultViewState.Success,ExecutionStatus.PartialSuccess=>ResultViewState.PartialSuccess,
                ExecutionStatus.Failed=>ResultViewState.Failed,ExecutionStatus.Timeout=>ResultViewState.Timeout,
                ExecutionStatus.Cancelled=>ResultViewState.Cancelled,_=>throw new ArgumentOutOfRangeException()
            };
            return new(snapshot.Schema,new(Guid.NewGuid(),snapshot.Identity,snapshot.SchemaRevision,snapshot.Schema.Fingerprint),
                state,reader.Values,reader,()=>true,snapshot.Diagnostics,
                snapshot.Fields.ToDictionary(item=>item.Key,item=>item.Value.DisplayName,StringComparer.Ordinal),
                snapshot.Fields.Where(item=>item.Value.DisplayInResultPanel).Select(item=>item.Key).ToHashSet(StringComparer.Ordinal));
        }
        catch{reader.Dispose();throw;}
    }
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
                var admitted=PluginLoader.LoadPlugins();
                if(Environment.GetEnvironmentVariable("S3_EXPECT_REJECTION")=="1")
                {
                    Require(!admitted&&!PluginLoader.IsLoaded,"全局装载器将失败标为已完成");
                    Console.WriteLine("通过：正式全局装载器拒绝不完整组件且不设置已完成");return 0;
                }
                Require(admitted&&PluginLoader.IsLoaded,"全局装载器未接纳完整组件");
            }
            else
            {
                manager.LoadPlugins();
                manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(x=>x.Id));
                // Match App.OnStartup: publish readiness only after the actual
                // manager admitted and checked the complete formal tool set.
                PluginLoader.MarkAsLoaded();
            }
            var components=ToolRegistry.GetComponentMetadata();
            Require(components.Count(c=>c.ToolId=="BlobAnalysis"&&c.Role==ToolComponentRole.Runtime)==1,"Blob没有唯一Runtime");
            Require(components.Count(c=>c.ToolId=="BlobAnalysis"&&c.Role==ToolComponentRole.Editor)==(expectEditor?1:0),"可选Editor登记不符");
            Console.WriteLine("通过：正式PluginManager发现/准入/注册，单ToolId及组件角色正确");
            VerifyRuntimeConfiguration();
            var node=new WorkflowNodeBase("blob","blob","Blob","BlobAnalysis"){
                RuntimeConfiguration=RuntimeToolConfiguration.Create("BlobAnalysis").WithOverrides(new Dictionary<string,object?>{{"Threshold",100},{"IsAreaFilterEnabled",false},{"OutputGrayImage",true}})};
            EnsurePresentationApplication();VerifySettingsView(expectEditor);
            using var resultView=ToolRegistry.CreateResultView("BlobAnalysis")??throw new Exception("Runtime没有结果视图");
            if(expectEditor)
            {
                Require(resultView.View.GetType().Assembly.GetName().Name=="SunEyeVision.Tool.BlobAnalysis.Wpf","可选Wpf结果视图错误");
                var refs=resultView.View.GetType().Assembly.GetReferencedAssemblies().Select(reference=>reference.Name).ToArray();
                Require(!refs.Contains("SunEyeVision.Plugin.SDK")&&!refs.Contains("SunEyeVision.UI"),"Blob.Wpf依赖SDK或UI宿主");
                Console.WriteLine("通过：可选Wpf中立工厂供应真实Runtime结果页，不依赖SDK或UI宿主");
            }
            else
            {
                Require(resultView is SunEyeVision.Plugin.Wpf.Presenters.Results.DeclaredResultTable,"缺Wpf时没有通用声明结果表");
                Console.WriteLine("通过：缺Wpf时保留新Runtime配置和通用声明结果表；真实窗口另由RuntimeEditor轨道验证");
            }
            using var image=HalImage.CreateFromBytes(Enumerable.Repeat((byte)200,64).ToArray(),8,8,1);
            {
                var runtime=ToolRegistry.CreateRuntime("BlobAnalysis");var state=runtime.CreateSettings();var schema=runtime.Describe(state);
                Require(state.GetType()==runtime.SettingsType,"注册入口未创建真实运行参数类型");
                var imagePort=schema.Inputs.Single(port=>port.Type.Shape==ValueShape.Resource&&port.Type.Resource==ResourceKind.Image);
                using var frozen=image.CaptureSnapshot(new(Guid.NewGuid(),"registered-runtime-input"));
                using var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources();
                var inputs=new InputValues(schema.Inputs,new Dictionary<string,object?>{{imagePort.Id,frozen}});
                var task=SunEyeVision.Plugin.Runtime.ToolRuntimeExecutor.ExecuteAsync(runtime,state,inputs,new DirectRuntimeContext(resources)).AsTask();
                Require(task.Wait(TimeSpan.FromSeconds(30)),"注册Runtime直接执行等待UI消息");
                var outcome=task.GetAwaiter().GetResult();using var owned=outcome.Output as IDisposable;
                Require(outcome.Success&&outcome.Output?.GetType()==runtime.OutputType,"注册入口没有返回真实运行结果类型");
                var values=new InputValues(schema.Outputs,outcome.Output!.ToValues());
                var projection=((SunEyeVision.Plugin.Contracts.Results.Presentation.IToolResultPresentationProvider)runtime).ResultProjection;
                var rows=values.Get<SequenceValue>(projection.Rows!.PortId);
                Require(rows.Count==1&&((RecordValue)rows[0]!).Get<double>("area")==64,"非泛型宿主入口未运行真实HALCON或复制了不同结果");
                Console.WriteLine("通过：宿主从正式注册表直接取得Runtime/强类型参数，经公共入口执行真实HALCON，未创建SDK参数或结果模型");
            }
            var engine=new WorkflowEngine(new CheckLogger());var flow=engine.CreateWorkflow("S3-component-flow","组件验证");flow.Nodes.Add(node);
            var executor=new WorkflowExecutionEngine(engine,manager,new CheckLogger());
            System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
            using var execution=WithoutUiPump(()=>executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,image)));
            Require(execution.Success&&execution.NodeResults.TryGetValue("blob",out var nodeResult),"正式工作流失败："+string.Join(";",execution.Errors??new()));
            var result=execution.NodeResults["blob"].Result??throw new Exception("工作流没有Blob结果");
            var runtimeResult=(DeclaredOutputSnapshot)result;
            Require(runtimeResult.Output is not null,"缺少实际Runtime输出");
            var resultValues=new InputValues(runtimeResult.BindingOutputSchema!.Outputs,runtimeResult.Output!.ToValues());
            var rowPort=((SunEyeVision.Plugin.Contracts.Results.Presentation.IToolResultPresentationProvider)ToolRegistry.CreateRuntime("BlobAnalysis")).ResultProjection.Rows!.PortId;
            var resultRows=resultValues.Get<SequenceValue>(rowPort);
            Require(resultRows.Count==1&&((RecordValue)resultRows[0]!).Get<double>("area")==64,"新Runtime真实节点结果不符");
            Require(execution.DeclaredOutputs.Count==1,"正式执行未发布声明结果");
            Console.WriteLine("通过：正式节点与WorkflowExecutionEngine真实HALCON执行、声明结果发布，Blob面积64");
            {
                var grid=Find<System.Windows.Controls.DataGrid>(resultView.View).Single();
                var snapshot=result??throw new Exception("真实运行没有发布声明值");
                var record=(RecordValue)resultRows[0]!;
                var region=record.Get<IReadOnlyRegion>("region");
                ImageFrameAddress expectedFrame;CoordinateFrame expectedCoordinates;
                using(var origin=region.AcquireReader()){expectedFrame=origin.SourceFrame;expectedCoordinates=origin.Frame;}
                using var publication=CreatePublication(snapshot);
                ResultSelection? selected=null;var events=0;
                resultView.SelectionHighlightChanged+=(_,selection)=>{selected=selection;events++;};
                resultView.Present(publication);
                execution.Dispose();
                var resultWindow=new Window{Content=resultView.View,Width=700,Height=350,Left=-10000,ShowActivated=false,ShowInTaskbar=false};
                resultWindow.Show();resultWindow.UpdateLayout();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Require(grid.Items.Count==1&&grid.Columns.Count==8,$"中立结果视图未显示声明行和列：Rows={grid.Items.Count},Columns={grid.Columns.Count}");
                grid.SelectedItem=grid.Items[0];
                Require(selected is not null&&selected.Publication==publication.Stamp&&Equals(selected.ElementIdentity,record.Get<long>("element-id"))&&selected.Targets.Count==1,
                    "结果选择缺少真实publication或稳定元素身份");
                var target=selected!.Targets[0];
                Require(target.ImageFrame==expectedFrame&&target.Coordinates==expectedCoordinates&&target.Geometry.Count==1
                    &&target.Geometry[0] is PolygonGeometry polygon&&polygon.Frame==expectedCoordinates&&polygon.Vertices.Count==4,
                    "高亮物理来源/坐标/不可变几何不符");
                resultView.Clear();Require(grid.Items.Count==0&&selected is {ElementIdentity:null,Targets.Count:0}&&selected.Publication==publication.Stamp,"清空后行或同publication高亮残留");
                resultView.Present(publication);publication.Dispose();
                var before=events;grid.SelectedItem=null;grid.SelectedItem=grid.Items[0];
                Require(events==before,"退休publication仍发送选择高亮");
                resultView.Dispose();resultWindow.Close();
                var rejected=false;try{resultView.Present(publication);}catch(ObjectDisposedException){rejected=true;}
                Require(rejected,"已释放结果视图继续更新");
                Console.WriteLine("通过：执行owner释放后publication唯一reader仍支持展示；选择含真实物理帧/稳定元素身份，清理及退休拒旧事件");
            }
            Require(executor.CurrentState==WorkflowExecutionState.Idle&&executor.CurrentContext is null,"运行请求返回后会话未清理");
            foreach(var start in new Func<Task<ExecutionResult>>[]{()=>executor.ExecuteWorkflowAsync(flow.Id,image),()=>executor.ExecuteWorkflow(flow.Id,image)})
            {
                using var repeated=WithoutUiPump(start);
                Require(repeated.Success&&executor.CurrentState==WorkflowExecutionState.Idle&&executor.CurrentContext is null,
                    "公开执行入口仍依赖UI回调或未释放会话");
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
            Console.WriteLine($"S3组件消费：{9}组通过；Editor={(expectEditor?"存在":"缺席")}。不是完整新UI或整个S3验收。");return 0;
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
    private static void VerifySettingsView(bool expectEditor)
    {
        var configuration=RuntimeToolConfiguration.Create("BlobAnalysis");
        var provider=(IToolSettingsFields)ToolRegistry.CreateRuntime("BlobAnalysis");
        using var view=ToolRegistry.CreateSettingsView("BlobAnalysis",configuration.CaptureSettings());
        Require(view.View.GetType().Assembly.GetName().Name==(expectEditor?"SunEyeVision.Tool.BlobAnalysis.Wpf":"SunEyeVision.Plugin.Wpf"),"设置编辑器有无Wpf回退错误");
        var fields=Find<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView>(view.View).Single().Fields.ToArray();
        Require(fields.Length==54,"设置编辑器未消费完整XML字段");
        var id=provider.SettingsSchema.Presentation!.Fields.Single(f=>f.DisplayName=="阈值").FieldId;
        var editor=fields.Single(f=>f.FieldId==id);var changed=0;view.Changed+=()=>changed++;
        Require(editor.TrySetConstant(73L)&&view.TryCapture(out var candidate,out var references,out var error),"有效设置编辑未捕获");
        view.TryCapture(out candidate,out references,out error);
        int Threshold(object settings)=>(int)settings.GetType().GetProperty("Threshold")!.GetValue(settings)!;
        Require(Threshold(candidate)==73&&changed==1&&references.Count==0
            &&Threshold(configuration.CaptureSettings())==128,"私有编辑污染正式配置或未发事件");
        Require(!editor.TrySetConstant(300L)&&!view.TryCapture(out _,out _,out _),"非法设置可提交");
        Console.WriteLine("通过：实际Runtime设置模型经XML生成54字段，有无专属Wpf均提供私有编辑和非法提交阻断");
    }

    private static void VerifyRuntimeConfiguration()
    {
        var initial=RuntimeToolConfiguration.Create("BlobAnalysis");
        RuntimeToolConfiguration Change(RuntimeToolConfiguration source,int threshold)
        {
            var fields=(IToolSettingsFields)ToolRegistry.CreateRuntime(source.ToolId);
            var id=fields.SettingsSchema.Presentation!.Fields.Single(field=>field.DisplayName=="阈值").FieldId;
            return source.WithSettings(fields.ApplySettings(source.CaptureSettings(),new Dictionary<string,object?>{{id,(long)threshold}}));
        }
        int Threshold(RuntimeToolConfiguration source)
        {var settings=source.CaptureSettings();return (int)settings.GetType().GetProperty("Threshold")!.GetValue(settings)!;}
        void Reject(Action action)
        {
            try{action();}
            catch(Exception error) when(error is JsonException or InvalidOperationException or ArgumentException){return;}
            throw new Exception("非法中立Runtime配置未被拒绝");
        }
        var flow=new Workflow("runtime-configuration-flow","Runtime配置");
        var node=new WorkflowNodeBase("runtime-config","runtime-config","Runtime配置","BlobAnalysis"){RuntimeConfiguration=initial};
        flow.Nodes.Add(node);var solution=new Solution{Name="Runtime配置验证"};solution.Workflows.Add(flow);
        var host=new NodeConfigurationEditHost(solution);
        var stale=host.BeginEdit(flow.Id,node.Id);var draft=host.BeginEdit(flow.Id,node.Id);
        draft.Node.RuntimeConfiguration=Change(draft.Node.RuntimeConfiguration!,73);
        Require(host.HasPendingChanges(draft)&&Threshold(node.RuntimeConfiguration!)==128,"中立设置草稿提前污染正式配置或签名未识别变更");
        var frozen=host.Freeze(draft);
        draft.Node.RuntimeConfiguration=Change(draft.Node.RuntimeConfiguration!,99);
        Require(Threshold(frozen.Node.RuntimeConfiguration!)==73,"编辑冻结共享了后续设置修改");
        var run=WorkflowRunConfiguration.Capture(new[]{flow});
        host.Commit(frozen);
        Require(Threshold(node.RuntimeConfiguration!)==73&&Threshold(run.CopyWorkflow(flow.Id)!.Nodes[0].RuntimeConfiguration!)==128,
            "Runtime设置未发布或修改污染执行配置快照");
        stale.Node.RuntimeConfiguration=Change(stale.Node.RuntimeConfiguration!,66);
        Reject(()=>host.Commit(stale));
        host.Undo();Require(Threshold(node.RuntimeConfiguration!)==128,"撤销丢失Runtime设置");
        host.Redo();Require(Threshold(node.RuntimeConfiguration!)==73,"重做丢失Runtime设置");
        var rollback=host.BeginEdit(flow.Id,node.Id);rollback.Node.RuntimeConfiguration=Change(rollback.Node.RuntimeConfiguration!,88);
        var revision=host.Capture().Version;
        var injection=typeof(NodeConfigurationEditHost).GetField("_acceptanceFailureAfterRawMutation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        injection.SetValue(host,0);
        try{Reject(()=>host.Commit(rollback));}finally{injection.SetValue(host,-1);}
        Require(host.Capture().Version==revision&&Threshold(node.RuntimeConfiguration!)==73&&host.HasPendingChanges(rollback),
            "接受失败后Runtime设置/历史版本未回滚或私有编辑被丢失");
        var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"runtime-config.solution");
        var repository=new SolutionRepository();Require(repository.Save(solution,path),"Runtime配置无法经过正式方案保存门："+repository.LastError);
        var json=File.ReadAllText(path);var stored=JsonNode.Parse(json)!["Workflows"]![0]!["Nodes"]![0]!;
        Require(stored["Parameters"] is null&&stored["RuntimeConfiguration"]!["ToolId"]!.GetValue<string>()=="BlobAnalysis",
            "中立节点仍同时保存SDK参数或使用CLR类型名作为工具身份");
        var loaded=repository.LoadCandidate(path).Candidate??throw new Exception("Runtime配置保存后不能重新读取");
        Require(Threshold(loaded.Workflows[0].Nodes[0].RuntimeConfiguration!)==73,"XML默认值覆盖保存的Runtime设置");
        var restoredNode=loaded.Workflows[0].Nodes[0];var definition=ToolRegistry.CreateRuntime(restoredNode.ToolType);
        var schema=definition.Describe(restoredNode.RuntimeConfiguration!.CaptureSettings());
        using(var image=HalImage.CreateFromBytes(Enumerable.Repeat((byte)100,64).ToArray(),8,8,1))
        using(var input=image.CaptureSnapshot(new(Guid.NewGuid(),"runtime-node-config-input")))
        using(var resources=new SunEyeVision.Plugin.Runtime.FrozenOutputResources())
        {
            var imagePort=schema.Inputs.Single(port=>port.Type.Shape==ValueShape.Resource&&port.Type.Resource==ResourceKind.Image);
            var inputs=new InputValues(schema.Inputs,new Dictionary<string,object?>{{imagePort.Id,input}});
            var task=restoredNode.ExecuteRuntimeAsync(inputs,new DirectRuntimeContext(resources,restoredNode.Id)).AsTask();
            Require(task.Wait(TimeSpan.FromSeconds(30)),"中立节点执行等待UI回调");
            var outcome=task.GetAwaiter().GetResult();using var output=outcome.Output as IDisposable;
            Require(outcome.Success&&outcome.Output?.GetType()==definition.OutputType,"重开后的中立节点未返回真实Runtime模型");
            var values=new InputValues(schema.Outputs,outcome.Output!.ToValues());
            var projection=((SunEyeVision.Plugin.Contracts.Results.Presentation.IToolResultPresentationProvider)definition).ResultProjection;
            var rows=values.Get<SequenceValue>(projection.Rows!.PortId);
            Require(rows.Count==1&&((RecordValue)rows[0]!).Get<double>("area")==64,"节点执行忽略保存的阈值73，或回退XML默认128");
            Reject(()=>restoredNode.ExecuteRuntimeAsync(inputs,new DirectRuntimeContext(resources,"another-node")));
        }
        Reject(()=>JsonSerializer.Deserialize<RuntimeToolConfiguration>(JsonSerializer.Serialize(initial).Replace("\"Threshold\":128","\"Threshold\":128,\"Threshold\":2")));
        foreach(var mutation in new Action<JsonNode>[] {
            value=>value["Settings"]!.AsObject().Remove("Threshold"),
            value=>value["Settings"]!["unknown"]=1,
            value=>value["Settings"]!["Threshold"]="100",
            value=>value["Settings"]!["Judgments"]![0]!.AsObject().Remove("Kind"),
            value=>value["ToolId"]="missing-runtime"
        })
        {var value=JsonSerializer.SerializeToNode(initial)!;mutation(value);Reject(()=>value.Deserialize<RuntimeToolConfiguration>());}
        var both=JsonNode.Parse(json)!;both["Workflows"]![0]!["Nodes"]![0]!["Parameters"]=new JsonObject();
        Reject(()=>both.Deserialize<Solution>(WorkflowSerializationOptions.Default));
        var mismatch=JsonNode.Parse(json)!;mismatch["Workflows"]![0]!["Nodes"]![0]!["ToolType"]="ImageLoad";
        Reject(()=>mismatch.Deserialize<Solution>(WorkflowSerializationOptions.Default));
        var absent=JsonNode.Parse(json)!;absent["Workflows"]![0]!["Nodes"]![0]!["RuntimeConfiguration"]=null;
        Reject(()=>absent.Deserialize<Solution>(WorkflowSerializationOptions.Default));
        Console.WriteLine("通过：中立Runtime设置进入实际节点编辑/冻结/冲突/撤销重做/接受回滚、正式保存重开及节点直接HALCON执行，拒绝缺失/重复/错型和双模型；默认UI整链另见FullChain，调度链另见RuntimeWorkflow用例");
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
    private sealed class DirectRuntimeContext(IOutputResources resources,string nodeId="direct-runtime"):IToolExecutionContext
    {
        public RunIdentity Identity{get;}=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),nodeId,Guid.NewGuid());
        public CancellationToken Cancellation=>default;public IOutputResources Resources=>resources;public void Log(string message){}
    }
    private sealed class CheckLogger:ILogger
    {
        public ILogger ForSource(string source)=>this;
        public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All)
        {if(level==LogLevel.Error)Console.Error.WriteLine(message+" "+exception);}
    }
}
