using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.DataClassification.Runtime;

/// <summary>
/// 数据分类会话（UI 骨架阶段）。算法语义与序列化格式未取证（sourceEvidence=[]、
/// algorithm-semantics 全 9 步 unknown），按 rule-015 禁止兜底：执行明确返回"待补证"诊断，
/// 不静默产出空结果、不按经验伪造分类逻辑。补证完成后再替换本会话实现。
/// </summary>
internal sealed class DataClassificationSession : IToolSession<DataClassificationSettings, DataClassificationOutput>
{
    private int _disposed;

    public ValueTask<ToolOutcome<DataClassificationOutput>> ExecuteAsync(
        ToolInvocation<DataClassificationSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var cancellation = invocation.Context.Cancellation;
        cancellation.ThrowIfCancellationRequested();
        var errors = invocation.Settings.Validate();
        if (errors.Count != 0)
            return ValueTask.FromResult(new ToolOutcome<DataClassificationOutput>(false, null,
                BusinessJudgment.NotEvaluated, errors));
        return ValueTask.FromResult(ToolOutcome<DataClassificationOutput>.Failed(
            "DATA_CLASSIFICATION_ALGORITHM_PENDING",
            "数据分类算法语义与序列化格式尚未经原厂源码级取证，当前仅完成 UI 骨架；不支持执行。"));
    }

    public ValueTask DisposeAsync()
    {
        Volatile.Write(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }
}
