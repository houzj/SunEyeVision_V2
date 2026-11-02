namespace SunEyeVision.Plugin.Contracts;

/// <summary>Restricted admission gate for one invocation; an expired or cancelled identity rejects late callbacks.</summary>
public interface IHostInvocationGate
{
    bool TryInvoke(Action callback);
}

/// <summary>Frozen input preparation facts, independent of UI, workflow and hardware SDK types.</summary>
public sealed record HostInputPreparationContext(
    HostInputDeclaration Declaration, InputValues Settings, RunIdentity Identity,
    CancellationToken Cancellation, IHostInvocationGate Gate);

/// <summary>A single frozen host selection. The owner disposes it even if execution never starts.</summary>
public interface IHostInputPreparer : IDisposable
{
    string CapabilityId { get; }
    int CapabilityVersion { get; }
    IReadOnlyList<string> Services { get; }
    ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context);
}

/// <summary>Per-invocation values and restricted context capability. No output ownership is transferred.</summary>
public interface IPreparedHostInputs : IAsyncDisposable
{
    IReadOnlyDictionary<string, object?> Values { get; }
    IToolExecutionContext BindContext(IToolExecutionContext context);
}
