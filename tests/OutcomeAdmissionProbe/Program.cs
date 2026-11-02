using SunEyeVision.OutcomeOwnership.Probe;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow;

return ProbeMain.Run();
static class ProbeMain
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    public static int Run()
    {
        SetErrorMode(0x0001 | 0x0002 | 0x8000);
        try { return RunAsync().GetAwaiter().GetResult(); }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static async Task<int> RunAsync()
    {
var manager=new PluginManager();
await manager.LoadPluginsAsync();
if(manager.LastAdmissionError is not null)throw new Exception("准入失败："+manager.LastAdmissionError);
var directory=manager.RuntimeDirectory??throw new Exception("缺少准入目录");
if(!directory.Contains(Definition.Id))throw new Exception("故障组件不在可信目录");
Console.WriteLine("通过：正式封包校验及组件准入");
var logger=VisionLogger.Instance;var engine=new WorkflowEngine(logger,directory);
var flow=engine.CreateWorkflow("owner-"+Guid.NewGuid().ToString("N"),"owner");
var node=new WorkflowNodeBase("node","node","node",Definition.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(directory,Definition.Id)};
flow.Nodes.Add(node);var solution=new Solution();solution.Workflows.Add(flow);flow.AttachExecutionOwner(solution.ExecutionOwner);
var failures=new List<string>();
try
{
    Definition.FailCleanup=true;Output.DisposeCount=0;Definition.LastResources=null;
    var runner=new WorkflowExecutionEngine(engine,logger);
    using(var run=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id){ExecutionOwner=solution.ExecutionOwner}).WaitAsync(TimeSpan.FromSeconds(30)))
    {
        if(run.Success||!run.Errors.Any(error=>error.Contains("probe-session-cleanup")))failures.Add("正式执行未报告会话清理错误");
        if(Definition.LastResources is null)failures.Add("正式执行未取得资源scope");
        else try{Definition.LastResources.CreateScope();failures.Add("正式执行未关闭资源scope");}catch(ObjectDisposedException){}
        if(Output.DisposeCount!=1)failures.Add("正式执行未释放输出，次数="+Output.DisposeCount);
    }
    Console.WriteLine("正式执行清理输出次数="+Output.DisposeCount);
    Output.DisposeCount=0;Definition.LastResources=null;
    var debugOwner=new ToolExecutionOwner();
    try
    {
        using(var debug=RuntimeDebugInvocation.Capture(flow,directory,node.Clone(),debugOwner,0,_=>null,CancellationToken.None))
        {
            try{using var unexpected=await debug.ExecuteAsync();failures.Add("调试未报告会话清理错误");}
            catch(InvalidOperationException error)when(error.Message=="probe-session-cleanup"){}
            if(Definition.LastResources is null)failures.Add("调试未取得资源scope");
            else try{Definition.LastResources.CreateScope();failures.Add("调试未关闭资源scope");}catch(ObjectDisposedException){}
            if(Output.DisposeCount!=1)failures.Add("调试未释放输出，次数="+Output.DisposeCount);
        }
    }
    finally{debugOwner.Retire();}
    Console.WriteLine("调试清理输出次数="+Output.DisposeCount);
    Definition.FailCleanup=false;Output.DisposeCount=0;Definition.LastResources=null;
    using(var run=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id){ExecutionOwner=solution.ExecutionOwner}).WaitAsync(TimeSpan.FromSeconds(30)))
    {
        if(!run.Success)throw new Exception("成功执行失败："+string.Join(";",run.Errors));
        var output=run.DeclaredOutputs.Single();var reader=output.AcquireReader();
        run.Dispose();var drain=manager.UnloadPluginsAsync();await Task.Delay(100);
        if(drain.IsCompleted)failures.Add("结果reader持有期间目录过早退役");
        reader.Dispose();await drain.WaitAsync(TimeSpan.FromSeconds(10));
        if(Output.DisposeCount!=1)failures.Add("reader释放后输出次数="+Output.DisposeCount);
        Console.WriteLine("通过：结果reader阻止代次排空，释放后完成；输出次数="+Output.DisposeCount);
    }
}
finally{Definition.FailCleanup=false;Definition.LastResources=null;solution.ExecutionOwner.Retire();}
if(failures.Count>0)throw new Exception("探针失败："+string.Join("；",failures));
Console.WriteLine("通过：正式与调试会话清理、reader及代次排空");
return 0;
    }
}
