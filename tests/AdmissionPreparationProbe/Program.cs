using System.Runtime.InteropServices;
using SunEyeVision.AdmissionPreparation.Probe;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

return Probe.Run();
internal static class Probe
{
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    internal static int Run()
    {
        SetErrorMode(0x0001|0x0002|0x8000);
        try { RunCore(); return 0; } catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void RunCore()
    {
        var owner=new PluginManager();
        owner.LoadPluginsAsync().GetAwaiter().GetResult();
        if(owner.LastAdmissionError is not null || owner.DesktopAdmission is null) throw new Exception("初始载荷准入失败："+owner.LastAdmissionError);
        var first=owner.DesktopAdmission;
        var helper=new PluginManager();
        var calls=0;
        AppContext.SetData(Definition.CallbackKey,(Action)(()=>
        {
            if(Interlocked.Increment(ref calls)!=1)return;
            if(!Task.Run(helper.UnloadPluginsAsync).Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("插件准备回调持有目录锁。");
        }));
        var next=new PluginManager(); next.LoadPluginsAsync().GetAwaiter().GetResult();
        AppContext.SetData(Definition.CallbackKey,null);
        if(calls==0 || next.LastAdmissionError is not null || next.DesktopAdmission is null || ReferenceEquals(first,next.DesktopAdmission))
            throw new Exception("真实载荷准备阶段重入未成功："+next.LastAdmissionError);
        Console.WriteLine("probe: admitted component constructor reentered another manager outside publication gate");

        calls=0;
        AppContext.SetData(Definition.CallbackKey,(Action)(()=>
        {
            if(Interlocked.Increment(ref calls)!=1)return;
            if(!Task.Run(next.UnloadPluginsAsync).Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("当前 owner 卸载被准备阶段阻塞。");
        }));
        var stale=new PluginManager(); stale.LoadPluginsAsync().GetAwaiter().GetResult();
        AppContext.SetData(Definition.CallbackKey,null);
        if(calls==0 || stale.LastAdmissionError is null || stale.DesktopAdmission is not null)
            throw new Exception("owner 卸载后的过时准备结果错误发布。");
        Console.WriteLine("probe: owner unload invalidated the actual prepared admission");
    }
}
