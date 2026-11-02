using SunEyeVision.Plugin.Contracts;
namespace SunEyeVision.OutcomeOwnership.Probe;
public sealed record Settings:IToolSettings<Settings>{public Settings Freeze()=>this;}
public sealed class Output:IToolOutput,IDisposable
{
    public static int DisposeCount;
    public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>();
    public void Dispose(){if(Interlocked.Increment(ref DisposeCount)!=1)throw new InvalidOperationException("output disposed twice");}
}
public sealed class Definition:IToolDefinition<Settings,Output>,IToolSettingsFields,IToolCatalogProvider
{
    public const string Id="OutcomeOwnershipProbe";
    public static IOutputResources? LastResources;
    public static bool FailCleanup;
    private static readonly ToolSchema Empty=new(Array.Empty<Port>(),Array.Empty<Port>());
    public string ToolId=>Id;public ToolCatalogDescriptor Catalog {get;}=new("资源测试","测试资源释放","generic","自定义工具",0);
    public ToolSchema Describe()=>Empty;public ToolSchema SettingsSchema=>Empty;
    public InputValues ReadSettings(object settings)=>new(Empty.Inputs,new Dictionary<string,object?>());
    public object MaterializeSettings(InputValues values)=>new Settings();
    public IReadOnlyList<Diagnostic> Validate(Settings settings)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities()=>ToolCapabilities.None;
    public IToolSession<Settings,Output> CreateSession()=>new Session();
    private sealed class Session:IToolSession<Settings,Output>
    {
        public ValueTask<ToolOutcome<Output>> ExecuteAsync(ToolInvocation<Settings> invocation)
        {LastResources=invocation.Context.Resources;LastResources.CreateGray8(1,1,new byte[]{1});return ValueTask.FromResult(ToolOutcome<Output>.Completed(new Output()));}
        public ValueTask DisposeAsync()=>FailCleanup?ValueTask.FromException(new InvalidOperationException("probe-session-cleanup")):ValueTask.CompletedTask;
    }
}
