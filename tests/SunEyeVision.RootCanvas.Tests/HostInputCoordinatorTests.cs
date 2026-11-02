using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("VM canvas UI")]
public sealed class HostInputCoordinatorTests
{
    private sealed class Context(RunIdentity identity,CancellationToken cancellation,IOutputResources resources):IToolExecutionContext
    {
        public RunIdentity Identity=>identity;
        public CancellationToken Cancellation=>cancellation;
        public IOutputResources Resources=>resources;
        public void Log(string message){}
    }
    private sealed class Lease:IPreparedHostInputs
    {
        public int Disposals;
        public IReadOnlyDictionary<string,object?> Values {get;set;}=new Dictionary<string,object?>();
        public Func<IToolExecutionContext,IToolExecutionContext>? Wrap;
        public IToolExecutionContext BindContext(IToolExecutionContext context)=>Wrap?.Invoke(context)??context;
        public ValueTask DisposeAsync(){Disposals++;return ValueTask.CompletedTask;}
    }
    private sealed class Preparer: IHostInputPreparer
    {
        public string CapabilityId {get;set;}="image-source";
        public int CapabilityVersion {get;set;}=1;
        public IReadOnlyList<string> Services {get;set;}=new[]{"local-image-read"};
        public Func<HostInputPreparationContext,ValueTask<IPreparedHostInputs>> Prepare {get;set;}=_=>throw new InvalidOperationException();
        public ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)=>Prepare(context);
        public void Dispose(){}
    }
    private static Task Start(RuntimeToolConfiguration configuration,IHostInputPreparer input,ToolExecutionIdentity identity,IToolExecutionContext context)
    {
        var coordinator=typeof(Solution).Assembly.GetType("SunEyeVision.Workflow.HostInputCoordinator",true)!;
        var method=coordinator.GetMethod("PrepareAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
        var pending=method.Invoke(null,new[]{configuration,input,configuration.CaptureSettings(),identity,"source",context})!;
        return (Task)pending.GetType().GetMethod("AsTask")!.Invoke(pending,null)!;
    }

    [Fact]
    public async Task Late_preparation_after_retirement_is_disposed_and_never_enters_execution()
    {
        var config=RuntimeToolConfiguration.Create(RootCanvasFixture.Directory,"ImageLoad");
        var owner=new ToolExecutionOwner();var identity=owner.Begin("source",Guid.NewGuid(),"test");
        using var resources=new FrozenOutputResources();var context=new Context(identity.ToInvocationIdentity("source"),identity.CancellationToken,resources);
        var release=new TaskCompletionSource<IPreparedHostInputs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparer=new Preparer{Prepare=_=>new ValueTask<IPreparedHostInputs>(release.Task)};
        var task=Start(config,preparer,identity,context);owner.RetireNode("source");
        var lease=new Lease();release.SetResult(lease);
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await task);
        Assert.Equal(1,lease.Disposals);
    }

    [Fact]
    public async Task Foreign_resource_scope_and_undeclared_ports_are_rejected_and_disposed()
    {
        var config=RuntimeToolConfiguration.Create(RootCanvasFixture.Directory,"ImageLoad");
        var owner=new ToolExecutionOwner();var identity=owner.Begin("source",Guid.NewGuid(),"test");
        using var resources=new FrozenOutputResources();using var foreign=new FrozenOutputResources();
        var context=new Context(identity.ToInvocationIdentity("source"),identity.CancellationToken,resources);
        var lease=new Lease{Wrap=_=>new Context(context.Identity,context.Cancellation,foreign)};
        var preparer=new Preparer{Prepare=_=>ValueTask.FromResult<IPreparedHostInputs>(lease)};
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await Start(config,preparer,identity,context));Assert.Equal(1,lease.Disposals);
        var second=new Lease{Values=new Dictionary<string,object?>{{"unregistered",1}}};
        preparer.Prepare=_=>ValueTask.FromResult<IPreparedHostInputs>(second);
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await Start(config,preparer,identity,context));Assert.Equal(1,second.Disposals);
    }
}
