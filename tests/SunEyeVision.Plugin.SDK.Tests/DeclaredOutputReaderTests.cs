using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class DeclaredOutputReaderTests
{
    [Fact]
    public void Two_readers_survive_run_closure_and_release_the_original_owner_exactly_once()
    {
        var released=0;
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("values",TypeRef.SequenceOf(DataType.Number))});
        var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/source",Guid.NewGuid(),"5.0");
        var input=new[]{1d,2d};
        var frozen=DeclaredOutputSnapshot.Capture(schema,0,identity,"source",
            new(true,new Output(input),BusinessJudgment.Pass,Array.Empty<Diagnostic>()),new Release(()=>released++),0);
        input[0]=99;
        var run=new ExecutionResult{NodeResults=new Dictionary<string,NodeExecutionResult>{{"source",new(){Result=frozen,Success=true,Status=ExecutionStatus.Success}}}};
        run.CaptureDeclaredOutputs(new[]{frozen});
        var first=run.AcquireDeclaredOutput(frozen.Identity);var second=run.AcquireDeclaredOutput(frozen.Identity);
        frozen.Dispose();run.Dispose();
        Assert.Equal(0,released);
        Assert.Equal(1,second.Values!.Read("values").GetElement(0).GetScalar<double>());
        first.Dispose();first.Dispose();Assert.Equal(0,released);
        Assert.Equal(2,second.Values!.Read("values").Count);
        second.Dispose();Assert.Equal(1,released);
        Assert.Throws<ObjectDisposedException>(()=>second.Values);
        Assert.Throws<ObjectDisposedException>(()=>frozen.AcquireReader());
        Assert.Throws<ObjectDisposedException>(()=>run.AcquireDeclaredOutput(frozen.Identity));
    }

    [Fact]
    public void Exact_run_identity_is_required_and_not_run_is_distinct_from_a_successful_empty_sequence()
    {
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("values",TypeRef.SequenceOf(DataType.Number))});
        var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/source",Guid.NewGuid(),"5.0");
        var success=DeclaredOutputSnapshot.Capture(schema,0,identity,"source",
            new(true,new Output(Array.Empty<double>()),BusinessJudgment.Pass,Array.Empty<Diagnostic>()),new Release(()=>{}),0);
        var skippedIdentity=success.Identity with {NodeId="skipped",RequestId=Guid.NewGuid()};
        var skipped=DeclaredOutputSnapshot.NotExecuted(schema,0,skippedIdentity);
        using var run=new ExecutionResult{NodeResults=new Dictionary<string,NodeExecutionResult>{{"source",new(){Result=success}}}};
        run.CaptureDeclaredOutputs(new[]{success,skipped});success.Dispose();
        using var empty=run.AcquireDeclaredOutput(success.Identity);
        using var notRun=run.AcquireDeclaredOutput(skippedIdentity);
        Assert.Equal(ExecutionStatus.Success,empty.Snapshot.Status);Assert.Equal(0,empty.Values!.Read("values").Count);
        Assert.Equal(ExecutionStatus.NotExecuted,notRun.Snapshot.Status);Assert.Null(notRun.Values);
        Assert.Throws<KeyNotFoundException>(()=>run.AcquireDeclaredOutput(success.Identity with {RequestId=Guid.NewGuid()}));
        Assert.Throws<InvalidOperationException>(()=>DeclaredOutputSnapshot.Capture(schema,0,identity,"source",new(true,new Output(Array.Empty<double>()),BusinessJudgment.Pass,Array.Empty<Diagnostic>()),new Release(()=>{}),0));
    }

    private sealed record Output(double[] Values):IToolOutput
    {public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>{{"values",Values}};}
    private sealed class Release(Action release):IDisposable
    {public void Dispose()=>release();}
}
