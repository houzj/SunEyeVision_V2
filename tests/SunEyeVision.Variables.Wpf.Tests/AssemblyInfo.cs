using Xunit;
[assembly:CollectionBehavior(DisableTestParallelization=true)]
namespace SunEyeVision.TestOwner;
[CollectionDefinition("正式WPF owner")] public sealed class OwnerCollection:ICollectionFixture<OwnerLifetime>{}
public sealed class OwnerLifetime:IDisposable { public OwnerLifetime()=>SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop(); public void Dispose()=>SunEyeVision.TestShared.WpfTestDispatcher.ShutdownOwned(); }
