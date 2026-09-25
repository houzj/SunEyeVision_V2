using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Workflow;
using Xunit;
using WorkflowDefinition = SunEyeVision.Workflow.Workflow;

namespace SunEyeVision.UI.Tests;

public sealed class SolutionLoadRegressionTests
{
    private const string SolutionPath = @"C:\Users\houzhongjie\Desktop\1.solution";
    private const string DebugPluginPath = @"D:\MyWork\SunEyeVision_V2\dev\Debug\plugins";

    [Fact(Timeout = 15000)]
    public async Task WorkflowSectionDeserializesWithinTimeout()
    {
        var root = await ReadRootAsync();
        var workflowJson = root.GetProperty("Workflows").GetRawText();

        var result = await RunWithTimeoutAsync(
            "Workflows 反序列化",
            () => JsonSerializer.Deserialize<List<WorkflowDefinition>>(
                workflowJson,
                WorkflowSerializationOptions.Default));

        Assert.NotNull(result);
        Assert.Single(result!);
    }

    [Fact(Timeout = 15000)]
    public async Task DeviceSectionParsesWithoutWorkflowSerializer()
    {
        var root = await ReadRootAsync();
        var devices = root.GetProperty("Devices");

        var result = await RunWithTimeoutAsync(
            "Devices JSON 遍历",
            () =>
            {
                var list = new List<(string Id, int Port, int Settings)>();
                foreach (var device in devices.EnumerateArray())
                {
                    var id = device.GetProperty("Id").GetString() ?? string.Empty;
                    var port = device.GetProperty("Port").GetInt32();
                    var settings = device.TryGetProperty("Settings", out var settingsElement)
                        && settingsElement.ValueKind == JsonValueKind.Object
                        ? settingsElement.EnumerateObject().Count()
                        : 0;
                    list.Add((id, port, settings));
                }

                return list;
            });

        Assert.Single(result);
        Assert.Equal("hikvision-mvs:DA5892282", result[0].Id);
    }

    [Fact(Timeout = 20000)]
    public async Task SolutionRepositoryLoadCompletesWithinTimeout()
    {
        var result = await RunWithTimeoutAsync(
            "SolutionRepository.Load",
            () => new SolutionRepository().Load(SolutionPath),
            timeout: TimeSpan.FromSeconds(10));

        Assert.NotNull(result);
        Assert.Single(result!.Workflows);
        Assert.Single(result.Devices);
    }

    [Fact(Timeout = 20000)]
    public async Task SolutionManagerLoadAndSetCurrentSolutionCompletesWithinTimeout()
    {
        var result = await RunWithTimeoutAsync(
            "SolutionManager.LoadSolutionOnly + SetCurrentSolution",
            () =>
            {
                var directory = Path.Combine(Path.GetTempPath(), "SunEyeVision-SolutionLoadTest", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                try
                {
                    var manager = new SolutionManager(directory);
                    var solution = manager.LoadSolutionOnly(SolutionPath);
                    if (solution is null)
                        throw new InvalidOperationException("LoadSolutionOnly 返回 null");

                    manager.RegisterMetadata(new SolutionMetadata
                    {
                        Id = solution.Id,
                        Name = "RegressionTest",
                        FilePath = SolutionPath,
                        DirectoryPath = Path.GetDirectoryName(SolutionPath) ?? string.Empty
                    });

                    manager.SetCurrentSolution(solution);
                    return manager.CurrentSolution?.Id;
                }
                finally
                {
                    try { Directory.Delete(directory, recursive: true); } catch { }
                }
            },
            timeout: TimeSpan.FromSeconds(10));

        Assert.Equal("adbf6b09-efb9-425a-ac27-9e6bb986e231", result);
    }

    [Fact(Timeout = 30000)]
    public async Task MainWindowViewModelSolutionChangedPathCompletesOnStaThread()
    {
        var result = await RunOnStaWithTimeoutAsync(
            "MainWindowViewModel + CurrentSolutionChanged 工作流标签页加载",
            () =>
            {
                var directory = Path.Combine(Path.GetTempPath(), "SunEyeVision-SolutionLoadTest", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                try
                {
                    if (Application.Current is null)
                        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

                    ServiceInitializer.InitializeServices();
                    ServiceInitializer.InitializeSolutionManager(directory);
                    var manager = ServiceInitializer.SolutionManager;
                    var viewModel = new MainWindowViewModel();
                    var solution = manager.LoadSolutionOnly(SolutionPath);
                    if (solution is null)
                        throw new InvalidOperationException("LoadSolutionOnly 返回 null");

                    manager.RegisterMetadata(new SolutionMetadata
                    {
                        Id = solution.Id,
                        Name = "RegressionTest",
                        FilePath = SolutionPath,
                        DirectoryPath = Path.GetDirectoryName(SolutionPath) ?? string.Empty
                    });

                    manager.SetCurrentSolution(solution);
                    return (solution.Workflows.Count, viewModel.WorkflowTabViewModel.Tabs.Count);
                }
                finally
                {
                    try { Directory.Delete(directory, recursive: true); } catch { }
                }
            },
            timeout: TimeSpan.FromSeconds(15));

        Assert.Equal(1, result.Item1);
        Assert.Equal(1, result.Item2);
    }

    [Fact(Timeout = 30000)]
    public async Task SolutionLoadAfterProductionPluginLoadCompletesOnStaThread()
    {
        var result = await RunOnStaWithTimeoutAsync(
            "生产插件加载后 SolutionRepository.Load",
            () =>
            {
                if (Application.Current is null)
                    _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

                var pluginPath = Path.Combine(AppContext.BaseDirectory, "plugins");
                if (!Directory.Exists(pluginPath))
                    pluginPath = DebugPluginPath;
                if (!Directory.Exists(pluginPath))
                    throw new DirectoryNotFoundException($"测试插件目录不存在: {pluginPath}");

                ServiceInitializer.InitializeServices();
                var pluginManager = new PluginManager(VisionLogger.Instance);
                pluginManager.LoadPlugins(pluginPath);

                var solution = new SolutionRepository().Load(SolutionPath);
                return (solution?.Workflows.Count ?? -1, solution?.Devices.Count ?? -1);
            },
            timeout: TimeSpan.FromSeconds(15));

        Assert.Equal((1, 1), result);
    }

    private static async Task<JsonElement> ReadRootAsync()
    {
        Assert.True(File.Exists(SolutionPath), $"测试方案不存在: {SolutionPath}");
        var json = await File.ReadAllTextAsync(SolutionPath);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async Task<T> RunWithTimeoutAsync<T>(
        string stage,
        Func<T> action,
        TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(5);
        var stopwatch = Stopwatch.StartNew();
        var task = Task.Run(action);
        var completed = await Task.WhenAny(task, Task.Delay(limit));
        if (!ReferenceEquals(completed, task))
        {
            throw new Xunit.Sdk.XunitException(
                $"阶段“{stage}”超过 {limit.TotalSeconds:0.#} 秒未完成，耗时 {stopwatch.ElapsedMilliseconds}ms。");
        }

        return await task;
    }

    private static async Task<T> RunOnStaWithTimeoutAsync<T>(
        string stage,
        Func<T> action,
        TimeSpan timeout)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "SolutionLoadRegression-STA"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var completed = await Task.WhenAny(completion.Task, Task.Delay(timeout));
        if (!ReferenceEquals(completed, completion.Task))
        {
            throw new Xunit.Sdk.XunitException(
                $"阶段“{stage}”在 STA 线程超过 {timeout.TotalSeconds:0.#} 秒未完成。");
        }

        return await completion.Task;
    }
}
