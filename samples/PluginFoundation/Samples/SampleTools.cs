using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.Kernels;
[assembly:PluginContract(FoundationContract.Major,FoundationContract.Minor)]
namespace SunEyeVision.Plugin.Foundation.Samples;
public sealed record PointSettings(double[] Coordinates):IToolSettings<PointSettings>
{ public PointSettings Freeze()=>new((double[])Coordinates.Clone()); }
public sealed record DistanceOutput(double Distance):IToolOutput
{ public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>{{"distance",Distance}}; }
public sealed class PointPairDefinition:IToolDefinition<PointSettings,DistanceOutput>
{
    public string ToolId=>"PointPointMeasure.FoundationSample";
    public ToolSchema Describe(PointSettings s)=>new(Array.Empty<Port>(),new[]{new Port("distance",DataType.Number)});
    public IReadOnlyList<Diagnostic> Validate(PointSettings s)=>s.Coordinates.Length==4&&s.Coordinates.All(double.IsFinite)?Array.Empty<Diagnostic>():new[]{new Diagnostic("points","Expected four finite coordinates")};
    public ToolCapabilities Capabilities(PointSettings s)=>ToolCapabilities.None;
    public IToolSession<PointSettings,DistanceOutput> CreateSession()=>new Session();
    private sealed class Session:IToolSession<PointSettings,DistanceOutput>
    {
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
        public ValueTask<ToolOutcome<DistanceOutput>> ExecuteAsync(ToolInvocation<PointSettings> call)
        {
            call.Context.Cancellation.ThrowIfCancellationRequested();var c=call.Settings.Coordinates;
            return ValueTask.FromResult(ToolOutcome<DistanceOutput>.Completed(new(PointPairMath.Measure(c[0],c[1],c[2],c[3]).Distance)));
        }
    }
}
public sealed record WaitSettings(bool Cooperates=true):IToolSettings<WaitSettings>{public WaitSettings Freeze()=>this;}
public sealed record NumberOutput(double Value):IToolOutput{public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>{{"value",Value}};}
/// <summary>Controlled receive-wait substitute, no device calls.</summary>
public sealed class WaitDefinition:IToolDefinition<WaitSettings,NumberOutput>
{
    public TaskCompletionSource Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<double> Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Disposed {get;private set;}
    public string ToolId=>"ReceiveWait.Substitute";
    public ToolSchema Describe(WaitSettings s)=>new(Array.Empty<Port>(),new[]{new Port("value",DataType.Number)});
    public IReadOnlyList<Diagnostic> Validate(WaitSettings s)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities(WaitSettings s)=>new(new[]{"device:substitute"});
    public IToolSession<WaitSettings,NumberOutput> CreateSession()=>new Session(this);
    private sealed class Session(WaitDefinition owner):IToolSession<WaitSettings,NumberOutput>
    {
        public ValueTask DisposeAsync(){owner.Disposed=true;return ValueTask.CompletedTask;}
        public async ValueTask<ToolOutcome<NumberOutput>> ExecuteAsync(ToolInvocation<WaitSettings> call)
        {owner.Entered.TrySetResult();var v=call.Settings.Cooperates?await owner.Release.Task.WaitAsync(call.Context.Cancellation):await owner.Release.Task;return ToolOutcome<NumberOutput>.Completed(new(v));}
    }
}
public sealed record DynamicSettings(string OutputId,DataType Kind):IToolSettings<DynamicSettings>{public DynamicSettings Freeze()=>this;}
/// <summary>Script output-schema example only; does not execute scripts or claim sandbox support.</summary>
public static class ScriptSchema
{
    public static ToolSchema Describe(DynamicSettings s)=>new(Array.Empty<Port>(),new[]{new Port(s.OutputId,s.Kind)});
}
public sealed record ModelSettings(double[] Data):IToolSettings<ModelSettings>{public ModelSettings Freeze()=>new((double[])Data.Clone());}
public sealed class ModelDefinition:IToolDefinition<ModelSettings,NumberOutput>
{
    public int Created,Disposed,Calls;
    public string ToolId=>"ModelLifetime.Substitute";
    public ToolSchema Describe(ModelSettings s)=>new(Array.Empty<Port>(),new[]{new Port("value",DataType.Number)});
    public IReadOnlyList<Diagnostic> Validate(ModelSettings s)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities(ModelSettings s)=>new(new[]{"model:substitute"});
    public IToolSession<ModelSettings,NumberOutput> CreateSession(){Created++;return new Session(this);}
    private sealed class Session(ModelDefinition owner):IToolSession<ModelSettings,NumberOutput>
    {
        private bool _disposed;
        public ValueTask DisposeAsync(){if(!_disposed){_disposed=true;owner.Disposed++;}return ValueTask.CompletedTask;}
        public ValueTask<ToolOutcome<NumberOutput>> ExecuteAsync(ToolInvocation<ModelSettings> call){if(_disposed)throw new ObjectDisposedException("model");owner.Calls++;return ValueTask.FromResult(ToolOutcome<NumberOutput>.Completed(new(call.Settings.Data.Sum())));}
    }
}
