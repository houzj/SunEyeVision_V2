using System.Runtime.InteropServices;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Runtime;

internal static class Program
{
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    [STAThread] private static int Main(string[] args)
    {
        SetErrorMode(0x0001 | 0x0002 | 0x8000);
        try { return Run(args); } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static int Run(string[] args)
    {
        var path = Path.GetFullPath(args[0]);
        var directory = new ReleasePayloadAuthority(path).PrepareRuntimeDirectory();
        using var manager = new DocumentSessionManager(directory);
        var session = manager.CreateNew("运行链探针");
        var flow = session.GetWorkflows().Single();
        var graph = session.PrepareGraphEdit(flow.Id);
        var source = session.CreateNode(graph, "ImageLoad", new CanvasPoint(0, 0));
        var blob = session.CreateNode(graph, "BlobAnalysis", new CanvasPoint(200, 0));
        graph.AddConnection(new ConnectionDefinition("wire", source.Id, blob.Id, "output.image", "input.image"));
        session.CommitGraphEdit(graph, "连接图像源和 Blob");
        var imagePath = Path.Combine(path, "run-probe.bmp");
        using (var writer = new BinaryWriter(File.Create(imagePath)))
        {
            writer.Write((ushort)0x4d42); writer.Write(54+8*8*3); writer.Write(0); writer.Write(54); writer.Write(40); writer.Write(8); writer.Write(8); writer.Write((ushort)1); writer.Write((ushort)24); writer.Write(0); writer.Write(8*8*3); for (int i=0;i<4;i++) writer.Write(0);
            for (int y=0;y<8;y++) for (int x=0;x<8;x++) { byte p=(byte)(x>=2 && x<6 && y>=2 && y<6 ? 255 : 0); writer.Write(p); writer.Write(p); writer.Write(p); }
        }
        var sourceDraft = session.Editing.Host.BeginEdit(flow.Id, source.Id);
        var pathsPort = sourceDraft.SettingsSchema.Inputs.Single(p => p.Id == "local.paths");
        sourceDraft.ApplyFields(new Dictionary<string,ParameterSetting> { ["local.paths"] = new ConstantSetting(new SequenceValue(pathsPort.Type, new object?[] { imagePath })) });
        var blobDraft = session.Editing.Host.BeginEdit(flow.Id, blob.Id);
        var imagePort = session.GetNodeSchema(flow.Id, source.Id)!.Outputs.Single(p => p.Id == "output.image");
        blobDraft.ApplyFields(new Dictionary<string,ParameterSetting> { ["input.image"] = new ReferenceSetting(new OutputReference(source.Id,imagePort.Id,imagePort.Type,new[]{"data"},scopeId:flow.Id)) });
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, nodes: new[] { sourceDraft, blobDraft }));
        using var factory = new ImageSourceHostInputFactory();
        using (var batch = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: factory))
        using (var result = batch.RunAsync(Console.WriteLine).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
        {
            var run = result.Workflows[flow.Id];
            if (!run.Success) throw new Exception("运行失败：" + string.Join("; ",run.Errors));
            if (!run.DeclaredOutputs.Any(o => o.Identity.NodeId == source.Id) || !run.DeclaredOutputs.Any(o => o.Identity.NodeId == blob.Id)) throw new Exception("缺少真实图像源或 Blob 结果");
            using var reader = session.AcquirePublishedResult(flow.Id, blob.Id) ?? throw new Exception("会话缺少 Blob 结果 reader");
            Console.WriteLine("PASS: 实际准入、图像读取、Blob 执行和结果 reader");
        }
        File.Delete(imagePath);
        return 0;
    }
}
