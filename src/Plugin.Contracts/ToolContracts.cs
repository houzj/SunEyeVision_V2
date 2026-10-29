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
public interface IToolDefinition<TSettings,TOutput> : IToolDefinition
    where TSettings : IToolSettings<TSettings>, new() where TOutput : class, IToolOutput
{
    ToolSchema Describe(TSettings committedSettings);
    IReadOnlyList<Diagnostic> Validate(TSettings settings);
    ToolCapabilities Capabilities(TSettings settings);
    new IToolSession<TSettings,TOutput> CreateSession();
    Type IToolDefinition.SettingsType=>typeof(TSettings);
    Type IToolDefinition.OutputType=>typeof(TOutput);
    object IToolDefinition.CreateSettings()=>new TSettings();
    object IToolDefinition.FreezeSettings(object settings)=>RequireSettings(settings).Freeze()!;
    ToolSchema IToolDefinition.Describe(object settings)=>Describe(RequireSettings(settings));
    IReadOnlyList<Diagnostic> IToolDefinition.Validate(object settings)=>Validate(RequireSettings(settings));
    ToolCapabilities IToolDefinition.Capabilities(object settings)=>Capabilities(RequireSettings(settings));
    IToolSession IToolDefinition.CreateSession()=>CreateSession();
    private static TSettings RequireSettings(object settings)=>settings is TSettings typed?typed:
        throw new ArgumentException("Runtime settings must be "+typeof(TSettings).FullName,nameof(settings));
}
public interface IToolSession<TSettings,TOutput> : IToolSession
    where TSettings : IToolSettings<TSettings> where TOutput : class, IToolOutput
{
    ValueTask<ToolOutcome<TOutput>> ExecuteAsync(ToolInvocation<TSettings> invocation);
    async ValueTask<ToolOutcome> IToolSession.ExecuteAsync(ToolInvocation invocation)
    {
        if(invocation.Settings is not TSettings settings)throw new ArgumentException("Runtime invocation settings type differs.",nameof(invocation));
        var result=await ExecuteAsync(new ToolInvocation<TSettings>(settings,invocation.Inputs,invocation.Context,invocation.OutputRequests)).ConfigureAwait(false);
        return new(result.Success,result.Output,result.Judgment,result.Diagnostics);
    }
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
    public TypeRef Validate(ToolSchema schema)
    {
        var type=schema.Outputs.SingleOrDefault(port=>port.Id==PortId)?.Type
            ??throw new ArgumentException("Unknown requested output: "+PortId);
        foreach(var field in FieldPath)
        {
            while(type.Shape==ValueShape.Sequence)type=type.Element!;
            if(type.Shape!=ValueShape.Record)throw new ArgumentException("Requested field is not a record member.");
            type=type.Fields.SingleOrDefault(port=>port.Id==field)?.Type
                ??throw new ArgumentException("Unknown requested output field: "+field);
        }
        return type;
    }
}
/// <summary>Host-provided, single-invocation capability for committing an actual
/// frozen local-image read. The returned address is the admitted entry, not a UI selection.</summary>
public interface ILocalImageReadContext
{
    string AcceptLocalImage();
}
/// <summary>Restricted invocation capabilities; deliberately no graph, UI, arbitrary service locator or writable globals.</summary>
public interface IToolExecutionContext
{
    RunIdentity Identity { get; }
    CancellationToken Cancellation { get; }
    void Log(string message);
    IOutputResources Resources { get; }
}
