using SunEyeVision.Platform.Sdk;

// VM Platform SDK 对应的外部应用接入顺序：创建运行时 → Load 方案 → 运行/订阅结果 → Save/Close/Dispose。
Console.WriteLine($"SunEyeVision Platform SDK contract {PlatformSdkContract.Version}");
if (args.Length >= 2)
{
    using var runtime = new PlatformRuntimeHost(args[0]);
    using var solution = runtime.Load(args[1]);
    Console.WriteLine($"Loaded solution: {solution.SolutionPath}");
    Console.WriteLine($"Devices: {solution.Devices.Count}");
    foreach (var workflow in solution.Workflows)
        Console.WriteLine($"Workflow {workflow.Id}: {workflow.Nodes.Count} node(s)");
    if (args.Length >= 3)
    {
        using var execution = solution.CreateExecution(args[2]);
        var result = execution.RunAsync().GetAwaiter().GetResult();
        Console.WriteLine($"Run {result.WorkflowId}: success={result.Success}, nodes={result.Nodes.Count}");
        if (args.Length >= 4 && string.Equals(args[3], "continuous", StringComparison.OrdinalIgnoreCase))
        {
            using var continuous = solution.CreateContinuousExecution(new[] { args[2] });
            var continuousTask = continuous.RunAsync();
            Thread.Sleep(50);
            continuous.Stop();
            var completion = continuousTask.GetAwaiter().GetResult();
            Console.WriteLine($"Continuous {args[2]}: reason={completion.Reason}, rounds={completion.CompletedRounds}");
        }
    }
}
