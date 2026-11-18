using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.CircleFind.Runtime;

/// <summary>
/// 一次圆查找调用。本阶段为 UI 对齐交付：参数、端口、页签、结果列、显示需求均按原厂证据固定；
/// 原生圆检测算法本体（<c>IMVSCircleFindModu.dll</c>）未反汇编取证，
/// algorithmComparison 七维均 not-established（见 <c>algorithm-semantics.md</c> 与交接单）。
/// 因此本 Session 完成输入/设置校验后返回明确的「算法待补证」诊断，绝不伪造圆心/半径数值
/// （遵循 rule-015 禁止静默兜底、禁止伪造等价）。待原生算法取证后再补实现。
/// </summary>
internal sealed class CircleFindSession : IToolSession<CircleFindSettings, CircleFindOutput>, IReusableToolSession
{
    private int _disposed;

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ToolOutcome<CircleFindOutput>> ExecuteAsync(ToolInvocation<CircleFindSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(invocation);
        try
        {
            return ValueTask.FromResult(Execute(invocation));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(ToolOutcome<CircleFindOutput>.Failed("CIRCLEFIND_CANCELLED", "圆查找已取消。"));
        }
        catch (CircleFindCapabilityException error)
        {
            return ValueTask.FromResult(ToolOutcome<CircleFindOutput>.Failed(error.Code, error.Message));
        }
        catch (Exception error)
        {
            return ValueTask.FromResult(ToolOutcome<CircleFindOutput>.Failed("CIRCLEFIND_EXECUTION", error.Message));
        }
    }

    private static ToolOutcome<CircleFindOutput> Execute(ToolInvocation<CircleFindSettings> invocation)
    {
        var settings = invocation.Settings.Freeze();
        var errors = settings.Validate();
        if (errors.Count != 0)
            throw new CircleFindCapabilityException("CIRCLEFIND_SETTINGS_INVALID",
                string.Join("；", errors.Select(error => error.Message)));
        invocation.Context.Cancellation.ThrowIfCancellationRequested();

        var inputs = invocation.Inputs;
        if (!inputs.Contains(CircleFindSchema.ImageInput))
            throw new CircleFindCapabilityException("CIRCLEFIND_IMAGE_REQUIRED", "缺少本轮冻结的输入图像。");
        var image = inputs.Get<IReadOnlyImage?>(CircleFindSchema.ImageInput)
            ?? throw new CircleFindCapabilityException("CIRCLEFIND_IMAGE_REQUIRED", "缺少本轮冻结的输入图像。");
        ResourceContractRules.ValidateImage(image);

        // 验证输入图像可被 HALCON 接受；仅确认图像链可用，不进入圆检测数值计算。
        using var native = HalconResourceBridge.CopyImage(image);
        HOperatorSet.GetImageSize(native, out var widthTuple, out var heightTuple);
        using (widthTuple) using (heightTuple) { _ = widthTuple.I; _ = heightTuple.I; }

        // 原生圆检测算法未取证：返回明确诊断，禁止伪造圆/半径/拟合误差数值。
        throw new CircleFindCapabilityException("CIRCLEFIND_ALGORITHM_PENDING",
            "圆查找原生算法语义未取证（algorithmComparison 七维 not-established），本阶段仅完成 UI/契约对齐，圆检测数值待补证。");
    }
}

/// <summary>携带明确诊断码的执行期能力错误；只有这一类错误会被转成失败结果。</summary>
internal sealed class CircleFindCapabilityException(string code, string message) : Exception(message)
{
    internal string Code { get; } = code;
}
