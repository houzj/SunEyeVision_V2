namespace SunEyeVision.Execution;

/// <summary>Neutral execution coordination seam. The internal scheduling model remains replaceable.</summary>
public sealed class ExecutionCoordinator
{
    private readonly ExecutionServer _server;

    internal ExecutionCoordinator(ExecutionServer server) => _server = server;

    public Task<T> ExecuteAsync<T>(RunRequest request, Func<CancellationToken, Task<T>> execute,
        CancellationToken cancellation = default)
        => _server.RunAsync(request, execute, cancellation);
}
