using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using Xunit;

namespace SunEyeVision.Plugin.Editing.Wpf.Tests;

public sealed class InvocationOwnershipTests
{
    [Fact]
    public void Hidden_module_status_is_readable_on_failed_results_without_overriding_execution_status()
    {
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("status",DataType.Integer)},
            outputDisplay:new[]{new OutputDisplayNode("status","模块状态",displayInResultPanel:false)});
        var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/node",Guid.NewGuid(),"5.0");
        try
        {
            using var result=DeclaredOutputSnapshot.Capture(schema,0,identity,"node",
                new(false,new AvailableOutput(new Dictionary<string,object?>{{"status",1L}}),BusinessJudgment.NotEvaluated,
                    new[]{new Diagnostic("IMAGE_SOURCE_CAPTURE","取帧失败")}),new Scope(),0,
                fields:new Dictionary<string,DeclaredOutputField>{{"status",new("status","旧标题","",null,null,null,true)}});
            Assert.Equal(ExecutionStatus.Failed,result.Status);Assert.False(result.IsSuccess);
            Assert.False(result.Fields["status"].DisplayInResultPanel);Assert.Equal("模块状态",result.Fields["status"].DisplayName);
            using var reader=result.AcquireReader();Assert.Equal(1L,reader.Values!.Get<long>("status"));
        }
        finally{identity.CompleteCallbacks();owner.Retire();}
    }

    [Fact]
    public void Failed_result_retains_available_fields_without_satisfying_missing_outputs_and_drains_its_readers()
    {
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("sn",DataType.Integer),new Port("image",TypeRef.ResourceOf(ResourceKind.Image))});
        var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/node",Guid.NewGuid(),"5.0");
        var output=new AvailableOutput(new Dictionary<string,object?>{{"sn",8L}});var resources=new Scope();
        var result=DeclaredOutputSnapshot.Capture(schema,0,identity,"node",new(false,output,BusinessJudgment.NotEvaluated,
            new[]{new Diagnostic("IMAGE_SOURCE_STITCH","拼接失败"){NativeErrorCode=0xE0000502}}),resources,0);
        Assert.Equal(ExecutionStatus.Failed,result.Status);Assert.False(result.IsSuccess);Assert.True(result.IsComplete);
        Assert.Equal(0xE0000502u,Assert.Single(result.Diagnostics).NativeErrorCode);
        var reader=result.AcquireReader();
        Assert.Equal(8L,reader.Values!.Get<long>("sn"));Assert.True(reader.Values.Read("image").IsMissing);
        Assert.Equal(schema.Outputs.Count,reader.Values.Declarations.Count);
        result.Dispose();Assert.Equal(0,output.Count);Assert.Equal(0,resources.Count);
        reader.Dispose();Assert.Equal(1,output.Count);Assert.Equal(1,resources.Count);
        identity.CompleteCallbacks();owner.Retire();
    }

    [Fact]
    public void Available_failed_outputs_still_validate_types_and_success_cannot_omit_required_fields()
    {
        var ports=new[]{new Port("sn",DataType.Integer),new Port("image",TypeRef.ResourceOf(ResourceKind.Image))};
        Assert.Throws<ArgumentException>(()=>InputValues.CaptureAvailableOutputs(ports,new Dictionary<string,object?>{{"sn","8"}}));
        Assert.Throws<ArgumentException>(()=>InputValues.CaptureAvailableOutputs(ports,new Dictionary<string,object?>{{"other",8L}}));
        Assert.Throws<ArgumentException>(()=>new InputValues(ports,new Dictionary<string,object?>{{"sn",8L}}));
        var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/node",Guid.NewGuid(),"5.0");
        var output=new AvailableOutput(new Dictionary<string,object?>{{"sn",8L}});var resources=new Scope();
        Assert.Throws<ArgumentException>(()=>DeclaredOutputSnapshot.Capture(new(Array.Empty<Port>(),ports),0,identity,"node",
            new(true,output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),resources,0));
        Assert.Equal(1,output.Count);Assert.Equal(1,resources.Count);
        identity.CompleteCallbacks();owner.Retire();
    }

    private sealed class AvailableOutput(IReadOnlyDictionary<string,object?> values):IToolOutput,IDisposable
    {
        public int Count;
        public IReadOnlyDictionary<string,object?> ToValues()=>values;
        public void Dispose()=>Count++;
    }

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
    public void Broken_history_formatter_keeps_the_accepted_output_and_releases_owners()
    {
        var owner = new ToolExecutionOwner();
        var identity = owner.Begin("scope/node", Guid.NewGuid(), "5.0");
        var output = new BrokenHistoryOutput();
        var scope = new Scope();
        using (var result = DeclaredOutputSnapshot.Capture(Schema, 0, identity, "node",
            new(true, output, BusinessJudgment.Pass, Array.Empty<Diagnostic>()), scope, 0))
        {
            Assert.True(result.IsSuccess);
            Assert.Equal("历史记录生成失败：formatter failed", Assert.Single(result.ResultRecords));
            using var reader = result.AcquireReader();
            Assert.NotNull(reader.Values);
            Assert.Equal(1, output.FormatAttempts);
        }
        Assert.Equal(1, output.Disposals);
        Assert.Equal(1, scope.Count);
    }

    private sealed class BrokenHistoryOutput : IToolOutput, IToolResultRecordOutput, IDisposable
    {
        public int FormatAttempts;
        public int Disposals;
        public IReadOnlyDictionary<string, object?> ToValues() => new Dictionary<string, object?>();
        public IEnumerable<string> DescribeResultRecords()
        {
            FormatAttempts++;
            throw new InvalidOperationException("formatter failed");
        }
        public void Dispose() => Disposals++;
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
