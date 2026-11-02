using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class InvocationOwnershipTests
{
    private static readonly ToolSchema Schema = new(Array.Empty<Port>(),Array.Empty<Port>());
    private static InputValues Inputs()=>new(Array.Empty<Port>(),new Dictionary<string,object?>());

    [Fact]
    public async Task Session_disposal_failure_releases_unreturned_output_but_not_caller_resources()
    {
        var output=new CountedOutput();var resources=new Scope();
        var error=await Assert.ThrowsAsync<InvalidOperationException>(async()=>await ToolRuntimeExecutor.ExecuteAsync(new Definition(output),new object(),Inputs(),new Context(resources)));
        Assert.Equal("session cleanup",error.Message);Assert.Equal(1,output.Count);Assert.Equal(0,resources.Count);
        resources.Dispose();Assert.Equal(1,resources.Count);
    }

    [Fact]
    public async Task Session_failure_keeps_direct_resource_alias_for_caller_and_aggregates_release_failure()
    {
        var alias=new Scope();
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await ToolRuntimeExecutor.ExecuteAsync(new Definition(alias),new object(),Inputs(),new Context(alias)));
        Assert.Equal(0,alias.Count);alias.Dispose();Assert.Equal(1,alias.Count);
        var bad=new CountedOutput(true);
        var error=await Assert.ThrowsAsync<AggregateException>(async()=>await ToolRuntimeExecutor.ExecuteAsync(new Definition(bad),new object(),Inputs(),new Context(new Scope())));
        Assert.Contains(error.InnerExceptions,e=>e.Message=="session cleanup");Assert.Contains(error.InnerExceptions,e=>e.Message=="output cleanup");Assert.Equal(1,bad.Count);
    }

    [Fact]
    public async Task Execution_and_session_failures_are_both_reported_without_releasing_unreturned_output()
    {
        var output=new CountedOutput();var scope=new Scope();
        var error=await Assert.ThrowsAsync<AggregateException>(async()=>await ToolRuntimeExecutor.ExecuteAsync(new Definition(output,true),new object(),Inputs(),new Context(scope)));
        Assert.Contains(error.InnerExceptions,e=>e.Message=="execute");Assert.Contains(error.InnerExceptions,e=>e.Message=="session cleanup");
        Assert.Equal(0,output.Count);Assert.Equal(0,scope.Count);scope.Dispose();
    }

    [Fact]
    public void Capture_failure_and_last_reader_release_dispose_direct_alias_only_once()
    {
        var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/node",Guid.NewGuid(),"5.0");var alias=new Scope();
        var result=DeclaredOutputSnapshot.Capture(Schema,0,identity,"node",new(true,alias,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),alias,0);
        var first=result.AcquireReader();var second=result.AcquireReader();result.Dispose();first.Dispose();Assert.Equal(0,alias.Count);
        second.Dispose();Assert.Equal(1,alias.Count);
        var staleAlias=new Scope();
        Assert.Throws<InvalidOperationException>(()=>DeclaredOutputSnapshot.Capture(Schema,0,identity,"node",new(true,staleAlias,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),staleAlias,0));
        Assert.Equal(1,staleAlias.Count);
    }

    [Fact]
    public void Capture_failure_releases_both_distinct_owners()
    {
        var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/node",Guid.NewGuid(),"5.0");
        var output=new BrokenOutput();var scope=new Scope();
        Assert.Throws<InvalidOperationException>(()=>DeclaredOutputSnapshot.Capture(Schema,0,identity,"node",new(true,output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),scope,0));
        Assert.Equal(1,output.Count);Assert.Equal(1,scope.Count);
    }

    private sealed class BrokenOutput:IToolOutput,IDisposable
    {public int Count;public IReadOnlyDictionary<string,object?> ToValues()=>throw new InvalidOperationException("capture");public void Dispose()=>Count++;}

    private sealed class CountedOutput(bool fail=false):IToolOutput,IDisposable
    {public int Count;public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>();public void Dispose(){Count++;if(fail)throw new InvalidOperationException("output cleanup");}}
    private sealed class Scope:IOutputResourceScope,IToolOutput
    {
        public int Count;public void Dispose(){if(++Count!=1)throw new InvalidOperationException("double disposal");}
        public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>();
        public IOutputResourceScope CreateScope()=>throw new NotSupportedException();
        public IReadOnlyImage CreateGray8(int width,int height,ReadOnlySpan<byte> pixels)=>throw new NotSupportedException();
        public IReadOnlyImage RetainInput(IReadOnlyImage input)=>throw new NotSupportedException();
        public IReadOnlyImage CreateImage(ImageLayout layout,ImageFrameAddress frame,IReadOnlyRegion domain,ReadOnlySpan<byte> pixels)=>throw new NotSupportedException();
        public IReadOnlyRegion CreateRegion(CoordinateFrame frame,ImageFrameAddress source,ReadOnlySpan<RegionRun> runs)=>throw new NotSupportedException();
        public IReadOnlyRegion RetainRegion(IReadOnlyRegion region)=>throw new NotSupportedException();
    }
    private sealed class Context(IOutputResources resources):IToolExecutionContext
    {public RunIdentity Identity=>default;public CancellationToken Cancellation=>default;public IOutputResources Resources=>resources;public void Log(string message){}}
    private sealed class Definition(IToolOutput output,bool executeFail=false):IToolDefinition
    {
        public string ToolId=>"ownership-test";public Type SettingsType=>typeof(object);public Type OutputType=>output.GetType();
        public object CreateSettings()=>new object();public object FreezeSettings(object settings)=>settings;public ToolSchema Describe()=>Schema;
        public IReadOnlyList<Diagnostic> Validate(object settings)=>Array.Empty<Diagnostic>();public ToolCapabilities Capabilities()=>ToolCapabilities.None;
        public IToolSession CreateSession()=>new Session(output,executeFail);
    }
    private sealed class Session(IToolOutput output,bool executeFail):IToolSession
    {public ValueTask<ToolOutcome> ExecuteAsync(ToolInvocation invocation)=>executeFail?ValueTask.FromException<ToolOutcome>(new InvalidOperationException("execute")):ValueTask.FromResult(new ToolOutcome(true,output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()));
     public ValueTask DisposeAsync()=>ValueTask.FromException(new InvalidOperationException("session cleanup"));}
}
