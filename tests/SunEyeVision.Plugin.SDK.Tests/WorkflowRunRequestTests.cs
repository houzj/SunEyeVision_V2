using System.IO;
using SunEyeVision.Application;
using SunEyeVision.Plugin.Runtime;
using Xunit;
namespace SunEyeVision.Plugin.SDK.Tests;
public class WorkflowRunRequestTests
{
    private sealed class CountingLease(IDisposable inner) : IDisposable
    {
        public int Count;
        public void Dispose() { if (Interlocked.Increment(ref Count) != 1) throw new InvalidOperationException("租约重复释放"); inner.Dispose(); }
    }
    [Fact]
    public void Actual_batch_freezes_input_mapping_and_releases_only_its_lease()
    {
        var payload = Environment.GetEnvironmentVariable("SUNEYE_ROOT_TEST_PAYLOAD") ?? throw new InvalidOperationException("缺少正式载荷。");
        var directory = new ReleasePayloadAuthority(payload).PrepareRuntimeDirectory();
        using var session = DocumentSession.CreateNew(directory);
        var flow = Assert.Single(session.GetWorkflows());
        using var owner = new FrozenOutputResources();
        var image = owner.CreateGray8(2, 1, new byte[] { 12, 34 });
        var lease = new CountingLease(image.AcquirePixels());
        var inputs = new Dictionary<string, WorkflowImageInput> { [flow.Id] = new(image, lease) };
        using (var batch = session.CaptureRunBatch(new[] { flow.Id }, workflowImages: inputs))
        {
            Assert.Equal(0, lease.Count);
            inputs.Clear();
            using var result = batch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
            Assert.True(result.Workflows.ContainsKey(flow.Id));
            Assert.Equal(new byte[] { 12, 34 }, image.CopyPixels());
            Assert.Equal(1, lease.Count);
        }
        Assert.Equal(1, lease.Count);
        Assert.Equal(new byte[] { 12, 34 }, image.CopyPixels());
    }
}
