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
        var request=new ToolExecutionRequest(new Parameters()){NodeId="source",BindingOutputSchema=schema};
        var result=new ToolExecutor(new ParameterResolver()).Run(new Probe(()=>released++),request);
        var input=new[]{1d,2d};
        var frozen=DeclaredOutputSnapshot.Publish(result,request.GetInvocationIdentity(),new Dictionary<string,object?>{{"values",input}});
        input[0]=99;
        var run=new ExecutionResult{NodeResults=new Dictionary<string,NodeExecutionResult>{{"source",new(){Result=result,Success=true,Status=ExecutionStatus.Success}}}};
        run.CaptureDeclaredOutputs(new[]{frozen});
        var first=run.AcquireDeclaredOutput(frozen.Identity);var second=run.AcquireDeclaredOutput(frozen.Identity);
        result.Resources!.Dispose();run.Dispose();
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
        var request=new ToolExecutionRequest(new Parameters()){NodeId="source",BindingOutputSchema=schema};
        var result=new ToolExecutor(new ParameterResolver()).Run(new Probe(()=>{}),request);
        var success=DeclaredOutputSnapshot.Publish(result,request.GetInvocationIdentity(),new Dictionary<string,object?>{{"values",Array.Empty<double>()}});
        var skippedIdentity=success.Identity with {NodeId="skipped",RequestId=Guid.NewGuid()};
        var skipped=DeclaredOutputSnapshot.NotExecuted(schema,0,skippedIdentity);
        using var run=new ExecutionResult{NodeResults=new Dictionary<string,NodeExecutionResult>{{"source",new(){Result=result}}}};
        run.CaptureDeclaredOutputs(new[]{success,skipped});result.Resources!.Dispose();
        using var empty=run.AcquireDeclaredOutput(success.Identity);
        using var notRun=run.AcquireDeclaredOutput(skippedIdentity);
        Assert.Equal(ExecutionStatus.Success,empty.Snapshot.Status);Assert.Equal(0,empty.Values!.Read("values").Count);
        Assert.Equal(ExecutionStatus.NotExecuted,notRun.Snapshot.Status);Assert.Null(notRun.Values);
        Assert.Throws<KeyNotFoundException>(()=>run.AcquireDeclaredOutput(success.Identity with {RequestId=Guid.NewGuid()}));
        Assert.Throws<InvalidOperationException>(()=>DeclaredOutputSnapshot.Publish(result,success.Identity,new Dictionary<string,object?>()));
    }

    public sealed class Parameters:ToolParameters {}
    private sealed class Results:ToolResults
    {public Results(){} public Results(Action release){Resources=new(release);SetSuccess();}}
    private sealed class Probe(Action release):IToolPlugin<Parameters,Results>
    {public Results Run(Parameters parameters)=>new(release);}
}
