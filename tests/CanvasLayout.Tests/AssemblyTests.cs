using Xunit;
[assembly:CollectionBehavior(DisableTestParallelization=true)]
namespace SunEyeVision.UI.Tests;
public sealed class CanvasDesktop : IDisposable
{
 public CanvasDesktop()=>SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
 public void Dispose()=>SunEyeVision.TestShared.WpfTestDispatcher.ShutdownOwned();
}
[CollectionDefinition("Canvas UI")] public sealed class CanvasCollection : ICollectionFixture<CanvasDesktop> { }
