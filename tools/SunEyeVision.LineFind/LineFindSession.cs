using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.LineFind.Runtime;

/// <summary>
/// 一次直线查找调用。本阶段为 UI 对齐交付：参数、端口、页签、结果列、显示需求均按原厂证据固定；
/// 原生直线检测算法体（<c>IMVSLineFindModu.dll</c> 213KB 原生 C++）未反汇编，
/// algorithmComparison 七维中 pixelNumerics/nativeAbi 为 not-established（见 evidence.json）。
/// 本 Session 完成输入/设置校验后返回明确的「算法待补证」诊断，绝不伪造直线端点/角度数值
/// （遵循 rule-015 禁止静默兜底、禁止伪造等价）。待原机取证/原生反汇编后再补实现。
/// </summary>
internal sealed class LineFindSession : IToolSession<LineFindSettings, LineFindOutput>, IReusableToolSession
{
    private int _disposed;

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ToolOutcome<LineFindOutput>> ExecuteAsync(ToolInvocation<LineFindSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(invocation);
        try
        {
            return ValueTask.FromResult(Execute(invocation));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(ToolOutcome<LineFindOutput>.Failed("LINEFIND_CANCELLED", "直线查找已取消。"));
        }
        catch (LineFindCapabilityException error)
        {
            return ValueTask.FromResult(ToolOutcome<LineFindOutput>.Failed(error.Code, error.Message));
        }
        catch (Exception error)
        {
            return ValueTask.FromResult(ToolOutcome<LineFindOutput>.Failed("LINEFIND_EXECUTION", error.Message));
        }
    }

    private static ToolOutcome<LineFindOutput> Execute(ToolInvocation<LineFindSettings> invocation)
    {
        var settings = invocation.Settings.Freeze();
        var errors = settings.Validate();
        if (errors.Count != 0)
            throw new LineFindCapabilityException("LINEFIND_SETTINGS_INVALID",
                string.Join("；", errors.Select(error => error.Message)));
        invocation.Context.Cancellation.ThrowIfCancellationRequested();

        var inputs = invocation.Inputs;
        if (!inputs.Contains(LineFindSchema.ImageInput))
            throw new LineFindCapabilityException("LINEFIND_IMAGE_REQUIRED", "缺少本轮冻结的输入图像。");
        var image = inputs.Get<IReadOnlyImage?>(LineFindSchema.ImageInput)
            ?? throw new LineFindCapabilityException("LINEFIND_IMAGE_REQUIRED", "缺少本轮冻结的输入图像。");
        ResourceContractRules.ValidateImage(image);

        // 验证输入图像可被 HALCON 接受；仅确认图像链可用，不进入直线检测数值计算。
        using var native = HalconResourceBridge.CopyImage(image);
        HOperatorSet.GetImageSize(native, out var widthTuple, out var heightTuple);
        using (widthTuple) using (heightTuple) { _ = widthTuple.I; _ = heightTuple.I; }

        // 原生直线检测算法未取证：返回明确诊断，禁止伪造端点/角度/拟合误差数值。
        throw new LineFindCapabilityException("LINEFIND_ALGORITHM_PENDING",
            "直线查找原生算法语义未取证（pixelNumerics/nativeAbi not-established），本阶段仅完成 UI/契约对齐，直线检测数值待补证。");
    }
}

/// <summary>携带明确诊断码的执行期能力错误；只有这一类错误会被转成失败结果。</summary>
internal sealed class LineFindCapabilityException(string code, string message) : Exception(message)
{
    internal string Code { get; } = code;
}
