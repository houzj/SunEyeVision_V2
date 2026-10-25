using System.Reflection;
namespace SunEyeVision.Plugin.Contracts;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class PluginContractAttribute(int major, int minor) : Attribute
{
    public int Major { get; } = major; public int Minor { get; } = minor;
}
public static class FoundationContract
{
    public const int Major = 5, Minor = 0;
    public static void RequireCurrent(Assembly assembly)
    {
        var v = assembly.GetCustomAttribute<PluginContractAttribute>();
        if(v is null || v.Major != Major || v.Minor != Minor)
            throw new InvalidOperationException($"Plugin contract must be {Major}.{Minor}; actual {v?.Major.ToString() ?? "missing"}.{v?.Minor.ToString() ?? "missing"}. Rebuild; legacy binaries are not supported.");
    }
}
public interface IToolSettings<TSelf> { TSelf Freeze(); }
public interface IToolOutput { IReadOnlyDictionary<string, object?> ToValues(); }
public sealed record Diagnostic(string Code, string Message, string? FieldId = null);
public enum BusinessJudgment { NotEvaluated, Pass, Fail }
public sealed record ToolOutcome<T>(bool Success, T? Output, BusinessJudgment Judgment, IReadOnlyList<Diagnostic> Diagnostics)
    where T : class, IToolOutput
{
    public static ToolOutcome<T> Completed(T output, BusinessJudgment judgment=BusinessJudgment.NotEvaluated) => new(true,output,judgment,Array.Empty<Diagnostic>());
    public static ToolOutcome<T> Failed(string code,string message) => new(false,null,BusinessJudgment.NotEvaluated,new[]{new Diagnostic(code,message)});
}
/// <summary>RequestId is the original admission Guid, never a derived counter or hash.</summary>
public readonly record struct RunIdentity(Guid DocumentSessionId, Guid RunId, Guid ScopeId, string NodeId, Guid RequestId);
public sealed record ToolCapabilities(IReadOnlyList<string> ResourceKeys)
{
    public static ToolCapabilities None { get; } = new(Array.Empty<string>());
}
public interface IToolDefinition<TSettings,TOutput>
    where TSettings : IToolSettings<TSettings> where TOutput : class, IToolOutput
{
    string ToolId { get; }
    ToolSchema Describe(TSettings committedSettings);
    IReadOnlyList<Diagnostic> Validate(TSettings settings);
    ToolCapabilities Capabilities(TSettings settings);
    IToolSession<TSettings,TOutput> CreateSession();
}
public interface IToolSession<TSettings,TOutput> : IAsyncDisposable
    where TSettings : IToolSettings<TSettings> where TOutput : class, IToolOutput
{
    ValueTask<ToolOutcome<TOutput>> ExecuteAsync(ToolInvocation<TSettings> invocation);
}
public sealed class ToolInvocation<TSettings> where TSettings : IToolSettings<TSettings>
{
    public ToolInvocation(TSettings settings, InputValues inputs, IToolExecutionContext context, IEnumerable<OutputValueRequest>? outputRequests = null)
    { Settings=settings; Inputs=inputs; Context=context; OutputRequests=Array.AsReadOnly((outputRequests??Array.Empty<OutputValueRequest>()).ToArray()); }
    public TSettings Settings { get; }
    public InputValues Inputs { get; }
    public IToolExecutionContext Context { get; }
    public IReadOnlyList<OutputValueRequest> OutputRequests { get; }
}

/// <summary>Frozen downstream demand. Visibility switches cannot suppress resources needed by a consumer.</summary>
public sealed class OutputValueRequest
{
    public OutputValueRequest(string portId, IEnumerable<string>? fieldPath = null)
    {
        if(string.IsNullOrWhiteSpace(portId))throw new ArgumentException("Output identity is required.",nameof(portId));
        var path=(fieldPath??Array.Empty<string>()).ToArray();
        if(path.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("Output field path contains an empty identity.",nameof(fieldPath));
        PortId=portId; FieldPath=Array.AsReadOnly(path);
    }
    public string PortId { get; }
    public IReadOnlyList<string> FieldPath { get; }
}
/// <summary>Restricted invocation capabilities; deliberately no graph, UI, arbitrary service locator or writable globals.</summary>
public interface IToolExecutionContext
{
    RunIdentity Identity { get; }
    CancellationToken Cancellation { get; }
    void Log(string message);
    IOutputResources Resources { get; }
}
