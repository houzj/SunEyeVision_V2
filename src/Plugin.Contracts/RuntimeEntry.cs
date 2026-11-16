namespace SunEyeVision.Plugin.Contracts;

/// <summary>Host entry for a discovered Runtime. Settings and outputs retain their
/// original strongly typed instances; this boundary does not create a second model.</summary>
public interface IToolDefinition
{
    string ToolId { get; }
    Type SettingsType { get; }
    Type OutputType { get; }
    object CreateSettings();
    object FreezeSettings(object settings);
    ToolSchema Describe();
    IReadOnlyList<Diagnostic> Validate(object settings);
    ToolCapabilities Capabilities();
    IToolSession CreateSession();
}

public interface IToolSession : IAsyncDisposable
{
    ValueTask<ToolOutcome> ExecuteAsync(ToolInvocation invocation);
}

/// <summary>Marks a session whose internal state is scoped to an invocation and may be reused sequentially.</summary>
public interface IReusableToolSession
{
}

/// <summary>Optional startup hook for runtime components that need process-level native initialization.
/// The host runs it once per admitted generation on a background thread, never on the interactive
/// startup path. It must be idempotent, must not read solution resources, must not depend on an
/// execution identity, and must not change tool settings or session state.</summary>
public interface IToolRuntimeInitialization
{
    void InitializeRuntime();
}

public sealed record ToolOutcome(bool Success, IToolOutput? Output, BusinessJudgment Judgment,
    IReadOnlyList<Diagnostic> Diagnostics, bool Suppressed = false)
{
    public static ToolOutcome Suppress() => new(false, null, BusinessJudgment.NotEvaluated, Array.Empty<Diagnostic>(), true);
}

public sealed class ToolInvocation
{
    public ToolInvocation(object settings, InputValues inputs, IToolExecutionContext context,
        IEnumerable<OutputValueRequest>? outputRequests = null)
    {
        ArgumentNullException.ThrowIfNull(settings);ArgumentNullException.ThrowIfNull(inputs);ArgumentNullException.ThrowIfNull(context);
        Settings=settings;Inputs=inputs;Context=context;
        OutputRequests=Array.AsReadOnly((outputRequests??Array.Empty<OutputValueRequest>()).ToArray());
    }
    public object Settings { get; }
    public InputValues Inputs { get; }
    public IToolExecutionContext Context { get; }
    public IReadOnlyList<OutputValueRequest> OutputRequests { get; }
}
