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
        var executable=Path.Combine(root!,"AdmissionLifecycleProbe.exe");Assert.True(File.Exists(executable),"缺少真实准入探针："+executable);
        var start=new ProcessStartInfo(executable){WorkingDirectory=root!,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        using var process=Process.Start(start)??throw new InvalidOperationException("无法启动准入探针");
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();throw new TimeoutException("准入探针超时");}
        var output=await stdout;var error=await stderr;Assert.True(process.ExitCode==0,$"准入探针失败：{error}\n{output}");
        foreach(var marker in new[]{"probe: initial admission","probe: admitted runtime metadata and schema identities are unique","probe: non-owner unload and failed reload","probe: recovered admission","probe: concurrent load and unload completed","probe: reentrant preparation","probe: production workflow result reader","Desktop admission and production workflow reader drain checks passed"})
            Assert.Contains(marker,output);
    }
}
