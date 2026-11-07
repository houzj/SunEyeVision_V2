using System.IO;
using System.Diagnostics;
using Xunit;
namespace SunEyeVision.Plugin.Editing.Wpf.Tests;
public sealed class HostOwnershipTests
{
    [Fact]
    public void Formal_and_debug_hosts_release_output_and_readers_before_retirement()
    {
        var payload = Environment.GetEnvironmentVariable("SUNEYE_OUTCOME_TEST_PAYLOAD") ?? throw new InvalidOperationException("缺少正式封包测试目录。");
        var executable = Path.Combine(Path.GetFullPath(payload), "OutcomeAdmissionProbe.exe");
        using var process = Process.Start(new ProcessStartInfo(executable)
        {
            WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("资源所有权探针启动失败。");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(45000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("资源所有权探针超时。"); }
        Assert.True(process.ExitCode == 0, $"资源所有权探针失败，PID={process.Id}，退出码={process.ExitCode}：{stdout.GetAwaiter().GetResult()} {stderr.GetAwaiter().GetResult()}");
    }
}
