using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.CircleCircleMeasure.Runtime;

/// <summary>
/// 圆圆测量会话。原厂算法语义 7 维均为 not-established（reference-codex Measurement-IMVSC2CMeasureModu
/// 页 algorithmComparison，证据 C2CMeasureModuTool.cs / C2CMeasureModuParams.cs / C2CMeasureResult.cs
/// 只恢复构造、参数与结果桥），卡尺采样、圆拟合与交点/距离计算没有源码级或 ABI 证据。
/// 按 rule-015 禁止兜底回退，本会话明确返回"算法待补证"失败诊断，不静默产出测量数值。
/// </summary>
internal sealed class CircleCircleMeasureSession : IToolSession<CircleCircleMeasureSettings, CircleCircleMeasureOutput>, IReusableToolSession
{
    private int _disposed;

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ToolOutcome<CircleCircleMeasureOutput>> ExecuteAsync(ToolInvocation<CircleCircleMeasureSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(invocation);
        try
        {
            var errors = invocation.Settings.Freeze().Validate();
            if (errors.Count != 0)
                return ValueTask.FromResult(ToolOutcome<CircleCircleMeasureOutput>.Failed("CIRCLECIRCLEMEASURE_SETTINGS_INVALID",
                    string.Join("；", errors.Select(error => error.Message))));
            if (!invocation.Inputs.Contains(CircleCircleMeasureTool.ImageInput))
                return ValueTask.FromResult(ToolOutcome<CircleCircleMeasureOutput>.Failed("CIRCLECIRCLEMEASURE_IMAGE_REQUIRED",
                    "缺少本轮冻结的输入图像。"));
            invocation.Context.Cancellation.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ToolOutcome<CircleCircleMeasureOutput>.Failed("CIRCLECIRCLEMEASURE_ALGORITHM_PENDING",
                "圆圆测量算法数值取证未完成（algorithmComparison 7 维 not-established）；本版本只交付 UI、参数与端口对齐，不产出测量值。"));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(ToolOutcome<CircleCircleMeasureOutput>.Failed("CIRCLECIRCLEMEASURE_CANCELLED", "圆圆测量已取消。"));
        }
        catch (Exception error)
        {
            return ValueTask.FromResult(ToolOutcome<CircleCircleMeasureOutput>.Failed("CIRCLECIRCLEMEASURE_EXECUTION", error.Message));
        }
    }
}
