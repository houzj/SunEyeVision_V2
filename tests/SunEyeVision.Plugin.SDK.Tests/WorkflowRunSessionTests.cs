using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Runtime;
using Xunit;
namespace SunEyeVision.Plugin.SDK.Tests;
public class WorkflowRunSessionTests
{
    private sealed class GateFactory : IHostInputFactory, IDisposable
    {
        private readonly ImageSourceHostInputFactory _inner = new();
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IHostInputPreparer? Create(HostInputFactoryContext context)
        {
            var inner = _inner.Create(context); return inner is null ? null : new GatePreparer(inner, this);
        }
        public void Dispose() => _inner.Dispose();
    }
    private sealed class GatePreparer(IHostInputPreparer inner, GateFactory gate) : IHostInputPreparer
    {
        public string CapabilityId => inner.CapabilityId;
        public int CapabilityVersion => inner.CapabilityVersion;
        public IReadOnlyList<string> Services => inner.Services;
        public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)
        {
            gate.Started.TrySetResult();
            await gate.Release.Task.WaitAsync(context.Cancellation);
            return await inner.PrepareAsync(context);
        }
        public void Dispose() => inner.Dispose();
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Actual_batch_pause_resume_and_stop_preserve_execution_boundary(bool stop)
    {
        var payload = Environment.GetEnvironmentVariable("SUNEYE_ROOT_TEST_PAYLOAD") ?? throw new InvalidOperationException("缺少正式载荷。");
        var caseRoot = Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? throw new InvalidOperationException("缺少测试数据目录。");
        var folder = Path.Combine(caseRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var imagePath = Path.Combine(folder,"source.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(8,8,96,96,PixelFormats.Gray8,null,new byte[64],8)));
            using (var file = File.Create(imagePath)) encoder.Save(file);
            var directory = new ReleasePayloadAuthority(payload).PrepareRuntimeDirectory();
            using var session = DocumentSession.CreateNew(directory);
            var flow = Assert.Single(session.GetWorkflows());
            var graph = session.PrepareGraphEdit(flow.Id);
            var source = session.CreateNode(graph, "ImageLoad", new CanvasPoint(0,0));
            var blob = session.CreateNode(graph, "BlobAnalysis", new CanvasPoint(200,0));
            graph.AddConnection(new ConnectionDefinition("edge", source.Id, blob.Id, "output.image", "input.image"));
            session.CommitGraphEdit(graph,"添加运行链");
            var sourceDraft = session.Editing.Host.BeginEdit(flow.Id, source.Id);
            var pathPort = sourceDraft.SettingsSchema.Inputs.Single(p => p.Id == "local.paths");
            sourceDraft.ApplyFields(new Dictionary<string,ParameterSetting> { ["local.paths"] = new ConstantSetting(new SequenceValue(pathPort.Type, new object?[] { imagePath })) });
            var blobDraft = session.Editing.Host.BeginEdit(flow.Id, blob.Id);
            var output = session.GetNodeSchema(flow.Id,source.Id)!.Outputs.Single(p => p.Id == "output.image");
            blobDraft.ApplyFields(new Dictionary<string,ParameterSetting> { ["input.image"] = new ReferenceSetting(new OutputReference(source.Id,output.Id,output.Type,new[] { "data" },scopeId:flow.Id)) });
            session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[] { sourceDraft,blobDraft }));
            using var factory = new GateFactory();
            using var batch = session.CaptureRunBatch(new[] { flow.Id },hostFactory:factory);
            var startedBlob = 0;
            batch.NodeStatusChanged += (_,nodeId,status) => { if (nodeId == blob.Id && status == ExecutionStatus.Running) Interlocked.Exchange(ref startedBlob,1); };
            var pending = batch.RunAsync(_ => { });
            Assert.True(factory.Started.Task.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(batch.Pause());
            if (stop) batch.Stop(); else
            {
                factory.Release.TrySetResult();
                Assert.False(pending.Wait(TimeSpan.FromMilliseconds(250)));
                Assert.Equal(0, Volatile.Read(ref startedBlob));
                Assert.True(batch.Resume());
            }
            try
            {
                using var result = pending.WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
                if (stop) Assert.False(result.Workflows[flow.Id].Success);
                else { Assert.True(result.Workflows[flow.Id].Success,string.Join(";",result.Workflows[flow.Id].Errors)); Assert.Equal(1,Volatile.Read(ref startedBlob)); }
            }
            catch (OperationCanceledException) when (stop) { }
        }
        finally { Directory.Delete(folder,true); }
    }
}
