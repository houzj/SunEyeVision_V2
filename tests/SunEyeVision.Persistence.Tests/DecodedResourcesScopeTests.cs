using SunEyeVision.Persistence;
using Xunit;

public sealed class DecodedResourcesScopeTests
{
    private sealed class Resource : IDisposable { public int Count; public void Dispose()=>Interlocked.Increment(ref Count); }
    [Fact] public void Failed_candidate_releases_only_registered_resources_and_is_idempotent()
    {
        var scope=new DecodedResourcesScope();var owned=new Resource();var external=new Resource();scope.RegisterNew(owned);
        Assert.Throws<InvalidOperationException>(()=>scope.RegisterNew(owned));scope.Dispose();scope.Dispose();
        Assert.Equal(1,owned.Count);Assert.Equal(0,external.Count);
        var late=new Resource();Assert.Throws<ObjectDisposedException>(()=>scope.RegisterNew(late));Assert.Equal(1,late.Count);
    }
    [Fact] public void Successful_transfer_has_single_owner_and_prevents_second_transfer()
    {
        var scope=new DecodedResourcesScope();var resource=new Resource();scope.RegisterNew(resource);
        var owner=scope.Transfer();scope.Dispose();Assert.Equal(0,resource.Count);
        Assert.Throws<InvalidOperationException>(()=>scope.Transfer());owner.Dispose();owner.Dispose();Assert.Equal(1,resource.Count);
    }
    [Fact] public async Task Concurrent_transfer_or_dispose_never_releases_twice()
    {
        for(var i=0;i<50;i++)
        {
            var scope=new DecodedResourcesScope();var resource=new Resource();scope.RegisterNew(resource);IDisposable? owner=null;
            await Task.WhenAll(Task.Run(()=>{try{owner=scope.Transfer();}catch(InvalidOperationException){}}),Task.Run(scope.Dispose));
            owner?.Dispose();scope.Dispose();Assert.Equal(1,resource.Count);
        }
    }
}
