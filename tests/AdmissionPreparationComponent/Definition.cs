using SunEyeVision.Plugin.Contracts;
namespace SunEyeVision.AdmissionPreparation.Probe;

public sealed record Settings : IToolSettings<Settings> { public Settings Freeze()=>this; }
public sealed class Output : IToolOutput { public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>(); }
public sealed class Definition : IToolDefinition<Settings,Output>, IToolSettingsFields, IToolCatalogProvider
{
    public const string Id="AdmissionPreparationProbe";
    public const string CallbackKey="SunEyeVision.AdmissionPreparation.Probe.Callback";
    private static readonly ToolSchema Empty=new(Array.Empty<Port>(),Array.Empty<Port>());
    public Definition() { if (AppContext.GetData(CallbackKey) is Action callback) callback(); }
    public string ToolId=>Id;
    public ToolCatalogDescriptor Catalog {get;}=new("准入测试","准备阶段并发测试","generic","采集",0);
    public ToolSchema Describe()=>Empty;
    public ToolSchema SettingsSchema=>Empty;
    public InputValues ReadSettings(object settings)=>new(Empty.Inputs,new Dictionary<string,object?>());
    public object MaterializeSettings(InputValues values)=>new Settings();
    public IReadOnlyList<Diagnostic> Validate(Settings settings)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities()=>ToolCapabilities.None;
    public IToolSession<Settings,Output> CreateSession()=>new Session();
    private sealed class Session:IToolSession<Settings,Output>
    {
        public ValueTask<ToolOutcome<Output>> ExecuteAsync(ToolInvocation<Settings> invocation)=>ValueTask.FromResult(ToolOutcome<Output>.Completed(new Output()));
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
