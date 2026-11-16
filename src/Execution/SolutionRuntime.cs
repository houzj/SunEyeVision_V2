namespace SunEyeVision.Execution;

/// <summary>Resident solution execution identity owned by the execution server.</summary>
public sealed class SolutionRuntime : IAsyncDisposable
{
    private int _retired;

    public SolutionRuntime(string solutionId, Guid sessionId)
    {
        if (string.IsNullOrWhiteSpace(solutionId)) throw new ArgumentException("方案身份不能为空。", nameof(solutionId));
        SolutionId = solutionId;
        SessionId = sessionId;
    }

    public string SolutionId { get; }
    public Guid SessionId { get; }
    public bool IsRetired => Volatile.Read(ref _retired) != 0;

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _retired, 1);
        return ValueTask.CompletedTask;
    }
}
