using System.Runtime.InteropServices;
using System.Text.Json;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Persistence;
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
        try { Run(args); return 0; } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Run(string[] args)
    {
        var payload = Path.GetFullPath(args[0]);
        var data = Path.Combine(payload, "_root-collection-run", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(data);
        var imagePath = Path.Combine(data, "input.bmp");
        using (var writer = new BinaryWriter(File.Create(imagePath)))
        {
            writer.Write((ushort)0x4d42); writer.Write(54+8*8*3); writer.Write(0); writer.Write(54); writer.Write(40); writer.Write(8); writer.Write(8); writer.Write((ushort)1); writer.Write((ushort)24); writer.Write(0); writer.Write(8*8*3); for (var i=0;i<4;i++) writer.Write(0);
            for (var y=0;y<8;y++) for (var x=0;x<8;x++) { byte p=(byte)(x>=2 && x<6 && y>=2 && y<6 ? 255 : 0); writer.Write(p); writer.Write(p); writer.Write(p); }
        }
        var directory = new ReleasePayloadAuthority(payload).PrepareRuntimeDirectory();
        using var manager = new DocumentSessionManager(directory);
        var session = manager.CreateNew("整根复制运行");
        var root = session.GetWorkflows().Single();
        var graph = session.PrepareGraphEdit(root.Id);
        var image = session.CreateNode(graph, "ImageLoad", new CanvasPoint(0,0));
        var blob = session.CreateNode(graph, "BlobAnalysis", new CanvasPoint(200,0));
        graph.AddConnection(new ConnectionDefinition("edge", image.Id, blob.Id, "output.image", "input.image"));
        session.CommitGraphEdit(graph, "创建源图");
        var sourceDraft = session.Editing.Host.BeginEdit(root.Id, image.Id);
        var paths = sourceDraft.SettingsSchema.Inputs.Single(port => port.Id == "local.paths");
        sourceDraft.ApplyFields(new Dictionary<string,ParameterSetting> { ["local.paths"] = new ConstantSetting(new SequenceValue(paths.Type, new object?[] { imagePath })) });
        var targetDraft = session.Editing.Host.BeginEdit(root.Id, blob.Id);
        var imagePort = session.GetNodeSchema(root.Id, image.Id)!.Outputs.Single(port => port.Id == "output.image");
        targetDraft.ApplyFields(new Dictionary<string,ParameterSetting> { ["input.image"] = new ReferenceSetting(new OutputReference(image.Id,imagePort.Id,imagePort.Type,new[]{"data"},scopeId:root.Id)) });
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, nodes: new[] { sourceDraft, targetDraft }));
        RunOnce(session, root.Id, blob.Id);
        using var collection = session.Editing.Host.BeginRootCollectionEdit();
        var handle = collection.Copy(root, "复制流程", 1);
        var proposal = DocumentEditProposal.Prepare(session.Editing.Host, collection: collection);
        var result = session.CommitEdit(proposal);
        var copy = result.AcceptedRoots[handle];
        if (copy.Nodes.Any(node => node.Configuration.CaptureCurrentValues().Count != 0))
            throw new InvalidOperationException("整根复制错误继承源节点运行值。");
        var copyBlob = copy.Nodes.Single(node => node.ToolType == "BlobAnalysis");
        var copyId = copy.Id;
        RunOnce(session, copyId, copyBlob.Id);
        using (var activeFactory = new ImageSourceHostInputFactory())
        using (var active = session.CaptureRunBatch(new[] { copyId }, hostFactory: activeFactory))
        using (var blocked = session.Editing.Host.BeginRootCollectionEdit())
        {
            blocked.Remove(copy);
            try { _ = DocumentEditProposal.Prepare(session.Editing.Host, collection: blocked); throw new Exception("运行预约期间错误接受根删除。"); }
            catch (InvalidOperationException) { }
            if (!session.GetWorkflows().Any(flow => ReferenceEquals(flow, copy))) throw new Exception("被拒绝的删除改变了流程集合。");
        }        using (var held = session.AcquirePublishedResult(copyId, copyBlob.Id) ?? throw new InvalidOperationException("缺少复制流程结果。"))
        {
            using var deletion = session.Editing.Host.BeginRootCollectionEdit();
            deletion.Remove(copy);
            session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, collection: deletion));
            if (session.AcquirePublishedResult(copyId, copyBlob.Id) is not null) throw new InvalidOperationException("已删流程仍发布旧结果。");
            _ = held.Values;
            if (!session.Undo() || !ReferenceEquals(copy, session.GetWorkflows().Single(flow => flow.Id == copyId)))
                throw new InvalidOperationException("撤销未恢复实际流程。");
            if (session.AcquirePublishedResult(copyId, copyBlob.Id) is not null) throw new InvalidOperationException("撤销错误复活旧结果。");
            _ = held.Values;
        }
        RunOnce(session, copyId, copyBlob.Id);
        var solutionPath = Path.Combine(data,"copy.solution");
        var types = new FrozenVariableTypeCatalog(new Dictionary<string,string>());
        session.SaveAs(solutionPath, Path.Combine(data,"backup"), data, types, new JsonSerializerOptions(), _ => null);
        var reopened = manager.Open(solutionPath, _ => types, _ => null, new JsonSerializerOptions());
        var reopenedCopy = reopened.GetWorkflows().Single(flow => flow.Id == copyId);
        RunOnce(reopened, reopenedCopy.Id, reopenedCopy.Nodes.Single(node => node.ToolType == "BlobAnalysis").Id);
        Console.WriteLine("PASS: 复制 ImageLoad→Blob 正式运行、保存重开与再次运行");
    }
    private static void RunOnce(DocumentSession session, string workflowId, string blobId)
    {
        using var factory = new ImageSourceHostInputFactory();
        using var batch = session.CaptureRunBatch(new[] { workflowId }, hostFactory: factory);
        using var result = batch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        var run = result.Workflows[workflowId];
        if (!run.Success || !run.DeclaredOutputs.Any(output => output.Identity.NodeId == blobId))
            throw new InvalidOperationException("复制流程运行失败：" + string.Join(";", run.Errors));
        using var reader = session.AcquirePublishedResult(workflowId, blobId) ?? throw new InvalidOperationException("复制流程缺少结果 reader。");
    }
}
