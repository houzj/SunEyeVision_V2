using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.MachineLearningClassifier.Runtime;

/// <summary>
/// ML 分类会话（UI 骨架阶段）。模型格式与分类算法语义未取证（sourceEvidence=[]、
/// algorithm-semantics 全 9 步 unknown），按 rule-015 禁止兜底：执行明确返回"待补证"诊断，
/// 不静默产出空结果、不按经验伪造分类数值。补证完成后再替换本会话实现。
/// </summary>
internal sealed class MachineLearningClassifierSession : IToolSession<MachineLearningClassifierSettings, MachineLearningClassifierOutput>
{
    private int _disposed;

    public ValueTask<ToolOutcome<MachineLearningClassifierOutput>> ExecuteAsync(
        ToolInvocation<MachineLearningClassifierSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var cancellation = invocation.Context.Cancellation;
        cancellation.ThrowIfCancellationRequested();
        var errors = invocation.Settings.Validate();
        if (errors.Count != 0)
            return ValueTask.FromResult(new ToolOutcome<MachineLearningClassifierOutput>(false, null,
                BusinessJudgment.NotEvaluated, errors));
        return ValueTask.FromResult(ToolOutcome<MachineLearningClassifierOutput>.Failed(
            "MLCLASSIFIER_ALGORITHM_PENDING",
            "ML 分类模型格式与算法语义尚未经原厂源码级取证，当前仅完成 UI 骨架；不支持执行。"));
    }

    public ValueTask DisposeAsync()
    {
        Volatile.Write(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }
}
