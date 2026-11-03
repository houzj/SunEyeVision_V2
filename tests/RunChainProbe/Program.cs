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
        var imagePath2 = Path.Combine(path, "run-probe-2.bmp");
        File.Copy(imagePath, imagePath2, true);
        var sourceDraft = session.Editing.Host.BeginEdit(flow.Id, source.Id);
        var pathsPort = sourceDraft.SettingsSchema.Inputs.Single(p => p.Id == "local.paths");
        sourceDraft.ApplyFields(new Dictionary<string,ParameterSetting> { ["local.paths"] = new ConstantSetting(new SequenceValue(pathsPort.Type, new object?[] { imagePath, imagePath2 })) });
        var blobDraft = session.Editing.Host.BeginEdit(flow.Id, blob.Id);
        var imagePort = session.GetNodeSchema(flow.Id, source.Id)!.Outputs.Single(p => p.Id == "output.image");
        blobDraft.ApplyFields(new Dictionary<string,ParameterSetting> { ["input.image"] = new ReferenceSetting(new OutputReference(source.Id,imagePort.Id,imagePort.Type,new[]{"data"},scopeId:flow.Id)) });
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, nodes: new[] { sourceDraft, blobDraft }));
        using var factory = new ImageSourceHostInputFactory();
        var context = session.CaptureHostInputContext(flow.Id, source)!;
        using (var batch = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: factory))
        using (var result = batch.RunAsync(Console.WriteLine).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
        {
            factory.SelectIndex(context, 1);
            var run = result.Workflows[flow.Id];
            if (!run.Success) throw new Exception("运行失败：" + string.Join("; ",run.Errors));
            if (!run.DeclaredOutputs.Any(o => o.Identity.NodeId == source.Id) || !run.DeclaredOutputs.Any(o => o.Identity.NodeId == blob.Id)) throw new Exception("缺少真实图像源或 Blob 结果");
            using var reader = session.AcquirePublishedResult(flow.Id, blob.Id) ?? throw new Exception("会话缺少 Blob 结果 reader");
            Console.WriteLine("PASS: 实际准入、图像读取、Blob 执行和结果 reader");
        }
        if (factory.TakeCompletedReads(context).Count != 0) throw new Exception("新选择不能被旧轮次收据覆盖");
        using (var batch = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: factory))
        using (var result = batch.RunAsync(Console.WriteLine).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
            if (!result.Workflows[flow.Id].Success) throw new Exception("第二轮执行失败");
        var receipts = factory.TakeCompletedReads(context);
        if (receipts.Count != 1 || receipts[0].Read.Entry.OriginalIndex != 1 || !factory.IsReceiptCurrent(context, receipts[0]))
            throw new Exception("选图后的读取收据或实际来源不一致");
        using (var nextBatch = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: factory))
        using (var nextRun = nextBatch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
            if (!nextRun.Workflows[flow.Id].Success) throw new Exception("连续执行失败");
        if (factory.IsReceiptCurrent(context, receipts[0])) throw new Exception("旧收据在下一次读取后仍有效");
        var latest = factory.TakeCompletedReads(context);
        if (latest.Count != 1 || latest[0].Read.Entry.OriginalIndex != 0) throw new Exception("连续运行未自动切换图像");
        factory.SelectIndex(context, 0);
        if (factory.IsReceiptCurrent(context, latest[0])) throw new Exception("旧收据在新选择后仍有效");
        Console.WriteLine("PASS: 连续轮次、选图及旧收据失效");
        factory.SelectIndex(context, 1);
        File.Delete(imagePath2);
        using (var failedBatch = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: factory))
        using (var failedRun = failedBatch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
        {
            if (failedRun.Workflows[flow.Id].Success) throw new Exception("缺失图像意外执行成功");
            using var failedReader = session.AcquirePublishedResult(flow.Id, source.Id) ?? throw new Exception("失败后没有发布节点状态");
            if (failedReader.Snapshot.Status != ExecutionStatus.Failed) throw new Exception("节点失败后仍显示上轮成功结果");
        }
        File.Copy(imagePath, imagePath2, true);
        var failedReceipts = factory.TakeCompletedReads(context);
        if (failedReceipts.Count != 1) throw new Exception("读取接纳后的失败应保留实际读取收据");
        Console.WriteLine("PASS: 实际读取失败替换上轮成功状态");
        using (var cancelled = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: factory))
        {
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { using var ignored = cancelled.RunAsync(_ => { }, cancellation.Token).GetAwaiter().GetResult(); throw new Exception("取消的运行意外成功"); }
            catch (OperationCanceledException) { }
        }
        if (factory.TakeCompletedReads(context).Count != 0) throw new Exception("取消前未读取却生成收据");
        var remove = session.PrepareGraphEdit(flow.Id);
        remove.DeleteNodesAndConnections(new[] { source }, Array.Empty<ConnectionDefinition>());
        session.CommitGraphEdit(remove, "删除图像源");
        try { context.EnsureCurrent(); throw new Exception("删除后的旧上下文仍然有效"); }
        catch (InvalidOperationException) { }
        if (!session.Undo()) throw new Exception("图像源删除撤销失败");
        var restored = session.CaptureHostInputContext(flow.Id, source)!;
        if (restored.NodeGeneration == context.NodeGeneration || !context.NodeRetirement.IsCancellationRequested)
            throw new Exception("撤销恢复错误复用已退役游标代次");
        Console.WriteLine("PASS: 取消不产生读取收据，删除和撤销隔离游标代次");
        File.Delete(imagePath2);
        File.Delete(imagePath);
        return 0;
    }
}
