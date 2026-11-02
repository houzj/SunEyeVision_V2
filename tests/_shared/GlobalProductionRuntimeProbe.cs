using System.Collections.Concurrent;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.TestFixtures;

public sealed class GlobalProductionProbeState
{
    public ConcurrentQueue<string> Trace {get;}=new();
    public Action? BeforeSourceReturns {get;set;}
}
public sealed record GlobalProductionProbeSettings : IToolSettings<GlobalProductionProbeSettings>
{
    public string CaseId {get;init;}="";
    public string Label {get;init;}="";
    public bool Source {get;init;}
    public bool Fail {get;init;}
    public double Value {get;init;}
    public double Input {get;init;}=777;
    public GlobalProductionProbeSettings Freeze()=>this with{};
}
public sealed class GlobalProductionProbeOutput(double value,double[] values):IToolOutput,IDisposable
{
    public double Value {get;}=value;
    public double[] Values {get;}=values;
    public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>{{"output.value",Value},{"output.values",Values},{"output.index",checked((long)Value)}};
    public void Dispose(){}
}
// Test-only Runtime producer. This assembly references Contracts only; never deployed as a product tool.
public sealed class GlobalProductionProbeDefinition : IToolDefinition<GlobalProductionProbeSettings,GlobalProductionProbeOutput>,IToolSettingsFields,IToolCatalogProvider,IToolParameterBindingNames
{
    public IReadOnlyDictionary<string,string> ParameterBindingNames=>SettingsSchema.Inputs.ToDictionary(port=>port.Id,port=>port.Id,StringComparer.Ordinal);
    public const string Id="WorkflowGlobalProductionProbe";
    public static ConcurrentDictionary<string,GlobalProductionProbeState> States {get;}=new();
    public static readonly ToolSchema Outputs=new(Array.Empty<Port>(),new[]{
        new Port("output.value",TypeRef.ScalarOf(ScalarKind.Float64)),new Port("output.index",TypeRef.ScalarOf(ScalarKind.Int64)),
        new Port("output.values",TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64),CountConstraint.Exactly(2)))});
    public string ToolId=>Id;
    public ToolCatalogDescriptor Catalog {get;}=new("变量生产测试","仅测试运行变量生产链","generic","测试",0);
    public ToolSchema Describe()=>Outputs;
    public ToolSchema SettingsSchema {get;}=new(new[]{
        new Port("case",TypeRef.ScalarOf(ScalarKind.Text)),new Port("label",TypeRef.ScalarOf(ScalarKind.Text)),
        new Port("source",TypeRef.ScalarOf(ScalarKind.Boolean)),new Port("fail",TypeRef.ScalarOf(ScalarKind.Boolean)),
        new Port("value",TypeRef.ScalarOf(ScalarKind.Float64)),new Port("input",TypeRef.ScalarOf(ScalarKind.Float64))},Array.Empty<Port>());
    public InputValues ReadSettings(object settings)
    {
        var s=(GlobalProductionProbeSettings)settings;
        return new(SettingsSchema.Inputs,new Dictionary<string,object?>{{"case",s.CaseId},{"label",s.Label},{"source",s.Source},{"fail",s.Fail},{"value",s.Value},{"input",s.Input}});
    }
    public object MaterializeSettings(InputValues values)=>new GlobalProductionProbeSettings {
        CaseId=values.Get<string>("case"),Label=values.Get<string>("label"),Source=values.Get<bool>("source"),
        Fail=values.Get<bool>("fail"),Value=values.Get<double>("value"),Input=values.Get<double>("input")};
    public IReadOnlyList<Diagnostic> Validate(GlobalProductionProbeSettings settings)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities()=>ToolCapabilities.None;
    public IToolSession<GlobalProductionProbeSettings,GlobalProductionProbeOutput> CreateSession()=>new Session();
    private sealed class Session:IToolSession<GlobalProductionProbeSettings,GlobalProductionProbeOutput>
    {
        public ValueTask<ToolOutcome<GlobalProductionProbeOutput>> ExecuteAsync(ToolInvocation<GlobalProductionProbeSettings> invocation)
        {
            invocation.Context.Cancellation.ThrowIfCancellationRequested();
            var s=invocation.Settings;var state=States[s.CaseId];state.Trace.Enqueue(s.Label);
            if(s.Source)state.BeforeSourceReturns?.Invoke();
            invocation.Context.Cancellation.ThrowIfCancellationRequested();
            return ValueTask.FromResult(s.Fail?ToolOutcome<GlobalProductionProbeOutput>.Failed("controlled-failure","controlled producer failure"):
                ToolOutcome<GlobalProductionProbeOutput>.Completed(new(s.Source?s.Value:s.Input,new[]{s.Value,s.Value+1})));
        }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
