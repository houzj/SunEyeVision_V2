using System;
using System.IO;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.UI.Features.Camera;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.Workflow;
using Xunit;
using Xunit.Abstractions;

namespace SunEyeVision.UI.Tests;

/// <summary>
/// 真实相机启动恢复诊断。只走“方案设备 → 发现 → 注册 → 自动连接”链路，
/// 不打开窗口、不读取参数、不保存方案。
/// </summary>
public sealed class RealCameraRestoreDiagnosticTests
{
    private readonly ITestOutputHelper _output;

    public RealCameraRestoreDiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact(Timeout = 30000)]
    [Trait("Category", "RealHardware")]
    public async Task RestorePersistedHikrobotCameraThroughUiManager()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SunEyeVision-RealCameraRestore", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            ServiceInitializer.InitializeServices();
            ServiceInitializer.InitializeSolutionManager(directory);
            var manager = ServiceInitializer.SolutionManager;
            var solution = new Solution { Id = Guid.NewGuid().ToString(), FilePath = Path.Combine(directory, "diagnostic.solution") };
            solution.Devices.Add(new Device("Hikrobot-MV-CU120-10GM-001", DeviceType.Camera)
            {
                Id = "hikvision-mvs:DA5892282",
                Manufacturer = "Hikrobot",
                Model = "MV-CU120-10GM",
                SerialNumber = "DA5892282",
                IpAddress = "11.245.254.169",
                IsEnabled = true,
                Settings =
                {
                    ["DeviceId"] = "hikvision-mvs:DA5892282",
                    ["Transport"] = "GigEVision",
                    ["AutoConnect"] = true
                }
            });
            manager.SetCurrentSolution(solution);

            _output.WriteLine("RESTORE_BEGIN");
            var started = System.Diagnostics.Stopwatch.StartNew();
            await CameraManagerViewModel.RestoreConnectionsForCurrentSolutionAsync(manager);
            _output.WriteLine($"RESTORE_END elapsedMs={started.ElapsedMilliseconds}");
        }
        finally
        {
            await ServiceInitializer.DisposeCameraRuntimeAsync();
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }
}
