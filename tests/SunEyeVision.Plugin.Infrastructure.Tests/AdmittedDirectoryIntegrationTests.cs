using System.Diagnostics;
using System.IO;
using Xunit;

namespace SunEyeVision.Plugin.Infrastructure.Tests;
public sealed class AdmittedDirectoryIntegrationTests
{
    [Fact]
    public async Task Sealed_real_host_exercises_atomic_admission_generations_and_readers()
    {
        var root=Environment.GetEnvironmentVariable("SUNEYE_INFRA_PRODUCT_ROOT");
        Assert.False(string.IsNullOrWhiteSpace(root),"必须指定本工作区正式封包根 SUNEYE_INFRA_PRODUCT_ROOT，不能静默跳过。");
        Assert.True(Path.IsPathFullyQualified(root!),"产品封包根必须是绝对路径。");
        var executable=Environment.GetEnvironmentVariable("SUNEYE_INFRA_PROBE_PATH");
        Assert.False(string.IsNullOrWhiteSpace(executable),"必须指定独立Release/_validation探针 SUNEYE_INFRA_PROBE_PATH。");
        Assert.True(Path.IsPathFullyQualified(executable!)&&File.Exists(executable),"缺少真实准入探针："+executable);
        Assert.False(string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root!)),Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(executable!)!),StringComparison.OrdinalIgnoreCase),"探针必须输出到独立验证目录，不能污染产品根。");
        var evidence=Path.GetFullPath(Path.Combine(root!,"..","..","_validation","AdmissionLifecycle","child-"+Guid.NewGuid().ToString("N")));Directory.CreateDirectory(evidence);
        foreach(var hidden in new[]{false,true})
        {
            var start=new ProcessStartInfo(executable!){WorkingDirectory=root!,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
            if(hidden)start.ArgumentList.Add("--hidden-node-window");
            start.Environment["SUNEYE_INFRA_PRODUCT_ROOT"]=Path.GetFullPath(root!);
            using var process=Process.Start(start)??throw new InvalidOperationException("无法启动准入探针");
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
            try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){if(!process.HasExited)process.Kill(entireProcessTree:true);await process.WaitForExitAsync();throw new TimeoutException("准入探针超时：hidden="+hidden);}
            var output=await stdout;var error=await stderr;
            var mode=hidden?"hidden":"core";await File.WriteAllTextAsync(Path.Combine(evidence,mode+".stdout.log"),output);await File.WriteAllTextAsync(Path.Combine(evidence,mode+".stderr.log"),error);
            await File.WriteAllTextAsync(Path.Combine(evidence,mode+".exit.txt"),process.ExitCode.ToString());
            Assert.True(process.ExitCode==0,$"准入探针失败(hidden={hidden})：{error}\n{output}");
            Assert.DoesNotContain("Unhandled exception",error,StringComparison.OrdinalIgnoreCase);
            var markers=hidden?new[]{"probe: actual main command rejected unadmitted and paused nodes without constructing windows","隐藏桌面真实窗口通过: ImageLoad","隐藏桌面真实窗口通过: BlobAnalysis","probe: hidden production parameter windows closed and scopes released"}:
                new[]{"probe: initial admission","probe: admitted runtime metadata and schema identities are unique","probe: non-owner unload and failed reload","probe: recovered admission","probe: concurrent load and unload completed","probe: retirement callback reentered another manager","probe: production workflow result reader","probe: tool admission, paused control flow, workflow and debug rejection passed","probe: admitted save/reopen and missing/paused rejected open/save preserve bytes passed","probe: old runtime generation rejected for execution and debug admission","probe: active production invocation cancelled and drained before unload","probe: automatic display source and upstream width=8 passed","probe: runtime debug execution and upstream width=8 passed","probe: cross-node text reader retained retiring generation","Desktop admission and production workflow reader drain checks passed"};
            foreach(var marker in markers)Assert.Contains(marker,output);
        }
    }
}
