using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.PointPointMeasure.Runtime;

/// <summary>
/// 点点测量会话。原厂算法语义 7 维均为 not-established（reference-codex Measurement-IMVSP2PMeasureModu 页
/// algorithmComparison；P2PMeasureModuTool.cs / Params.cs / P2PMeasureResult.cs 只恢复构造、参数与结果桥），
/// 角度/距离/中点的具体计算、标定矩阵消费与 OutputAngleRange 语义未取到源码或 ABI 证据。
/// 按 rule-015 禁止兜底回退，明确返回"算法待补证"诊断，不静默产出测量数值。
/// </summary>
internal sealed class PointPointMeasureSession : IToolSession<PointPointMeasureSettings, PointPointMeasureOutput>, IReusableToolSession
{
    private int _disposed;

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ToolOutcome<PointPointMeasureOutput>> ExecuteAsync(ToolInvocation<PointPointMeasureSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(invocation);
        try
        {
            var errors = invocation.Settings.Freeze().Validate();
            if (errors.Count != 0)
                return ValueTask.FromResult(ToolOutcome<PointPointMeasureOutput>.Failed("POINTMEASURE_SETTINGS_INVALID",
                    string.Join("；", errors.Select(error => error.Message))));
            if (!invocation.Inputs.Contains(PointPointMeasureTool.ImageInput))
                return ValueTask.FromResult(ToolOutcome<PointPointMeasureOutput>.Failed("POINTMEASURE_IMAGE_REQUIRED",
                    "缺少本轮冻结的输入图像。"));
            invocation.Context.Cancellation.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ToolOutcome<PointPointMeasureOutput>.Failed("POINTMEASURE_ALGORITHM_PENDING",
                "点点测量算法数值取证未完成（algorithmComparison 7 维 not-established）；本版本只交付 UI、参数与端口对齐，不产出测量值。"));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(ToolOutcome<PointPointMeasureOutput>.Failed("POINTMEASURE_CANCELLED", "点点测量已取消。"));
        }
        catch (Exception error)
        {
            return ValueTask.FromResult(ToolOutcome<PointPointMeasureOutput>.Failed("POINTMEASURE_EXECUTION", error.Message));
        }
    }
}
