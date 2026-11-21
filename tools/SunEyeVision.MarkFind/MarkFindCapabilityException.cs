namespace SunEyeVision.Tool.MarkFind.Runtime;

/// <summary>算法内部可诊断失败：带稳定错误码，不使用异常文本猜测原因。</summary>
public sealed class MarkFindCapabilityException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
