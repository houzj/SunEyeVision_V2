using System.Runtime.InteropServices;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.OutcomeOwnership.Probe;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

return ProbeMain.Run();
static class ProbeMain
{
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    public static int Run()
    {
        SetErrorMode(0x0001 | 0x0002 | 0x8000);
        try { return Execute(); } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static int Execute()
    {
        var plugins = new PluginManager();
        plugins.LoadPluginsAsync().GetAwaiter().GetResult();
        if (plugins.LastAdmissionError is not null) throw new Exception("准入失败：" + plugins.LastAdmissionError);
        var directory = plugins.RuntimeDirectory ?? throw new Exception("缺少准入目录");
        if (!directory.Contains(Definition.Id)) throw new Exception("故障组件不在可信目录");
        using var manager = new DocumentSessionManager(directory);
        var session = manager.CreateNew("资源所有权");
        var flow = session.GetWorkflows().Single();
        var graph = session.PrepareGraphEdit(flow.Id);
        var node = session.CreateNode(graph, Definition.Id, new CanvasPoint(0,0));
        session.CommitGraphEdit(graph, "添加资源节点");
        try
        {
            Definition.FailCleanup = true; Output.DisposeCount = 0; Definition.LastResources = null;
            using (var batch = session.CaptureRunBatch(new[] { flow.Id }))
            using (var run = batch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
            {
                var result = run.Workflows[flow.Id];
                if (result.Success || !result.Errors.Any(e => e.Contains("probe-session-cleanup"))) throw new Exception("正式执行未报告清理错误");
                CheckRelease();
            }
            Output.DisposeCount = 0; Definition.LastResources = null;
            using (var editor = session.OpenDebugSession(flow.Id, node))
            {
                var draft = session.Editing.Host.BeginEdit(flow.Id, node.Id);
                using var debug = editor.Capture(draft, null, _ => { });
                try { using var unexpected = debug.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult(); throw new Exception("调试未报告清理错误"); }
                catch (InvalidOperationException e) when (e.Message == "probe-session-cleanup") { }
                CheckRelease();
            }
            Definition.FailCleanup = false; Output.DisposeCount = 0; Definition.LastResources = null;
            using (var batch = session.CaptureRunBatch(new[] { flow.Id }))
            {
                var run = batch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                var result = run.Workflows[flow.Id];
                if (!result.Success) throw new Exception("正式执行失败：" + string.Join(";", result.Errors));
                var reader = result.DeclaredOutputs.Single().AcquireReader();
                run.Dispose();
                batch.Dispose();
                session.Dispose();
                var drain = plugins.UnloadPluginsAsync();
                if (drain.Wait(TimeSpan.FromMilliseconds(100))) throw new Exception("结果 reader 持有期间目录过早排空");
                reader.Dispose();
                drain.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                if (Output.DisposeCount != 1) throw new Exception("reader 释放后的输出次数=" + Output.DisposeCount);
            }
            Console.WriteLine("通过：正式与调试清理、reader 及目录排空");
            return 0;
        }
        finally { Definition.FailCleanup = false; Definition.LastResources = null; }
    }
    private static void CheckRelease()
    {
        if (Definition.LastResources is null || Output.DisposeCount != 1) throw new Exception("输出或资源 scope 未正确释放");
        try { Definition.LastResources.CreateScope(); } catch (ObjectDisposedException) { return; }
        throw new Exception("资源 scope 未关闭");
    }
}
