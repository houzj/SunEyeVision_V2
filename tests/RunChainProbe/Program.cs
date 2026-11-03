using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.DeviceDriver.Cameras.Simulation;

internal static class Program
{
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    [STAThread] private static int Main(string[] args)
    {
        SetErrorMode(0x0001 | 0x0002 | 0x8000);
        try
        {
            var exitCode = Run(args);
            var forbidden = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetName().Name)
                .Where(name => name is "PresentationFramework" or "PresentationCore" or "WindowsBase" or "System.Xaml"
                    or "SunEyeVision.Plugin.Wpf" or "SunEyeVision.UI" or "SunEyeVision.Plugin.SDK"
                    or "SunEyeVision.Tool.ImageSource.Wpf" or "SunEyeVision.Tool.BlobAnalysis.Wpf")
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (forbidden.Length != 0)
                throw new InvalidOperationException("生产工厂非视觉运行链加载了桌面或旧 SDK 程序集：" + string.Join(", ", forbidden));
            Console.WriteLine("PASS: 真实本地/模拟相机工厂、执行及释放全链未加载 WPF、UI 或旧 SDK");
            return exitCode;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
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
        var disabled = session.CreateNode(graph, "ImageLoad", new CanvasPoint(400, 0));
        graph.SetNodeEnabled(disabled, false);
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
        var tracking = new TrackingFactory(factory);
        var context = session.CaptureHostInputContext(flow.Id, source)!;
        using (var batch = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: tracking))
        using (var result = batch.RunAsync(Console.WriteLine).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
        {
            factory.SelectIndex(context, 1);
            var run = result.Workflows[flow.Id];
            if (!run.Success) throw new Exception("运行失败：" + string.Join("; ",run.Errors));
            if (!run.DeclaredOutputs.Any(o => o.Identity.NodeId == source.Id) || !run.DeclaredOutputs.Any(o => o.Identity.NodeId == blob.Id)) throw new Exception("缺少真实图像源或 Blob 结果");
            using var reader = session.AcquirePublishedResult(flow.Id, blob.Id) ?? throw new Exception("会话缺少 Blob 结果 reader");
            Console.WriteLine("PASS: 实际准入、图像读取、Blob 执行和结果 reader");
        }
        if (tracking.Seen.Contains(disabled.Id)) throw new Exception("禁用节点不应创建宿主输入");
        if (factory.TakeCompletedReads(context).Count != 0) throw new Exception("新选择不能被旧轮次收据覆盖");
        using (var batch = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: tracking))
        using (var result = batch.RunAsync(Console.WriteLine).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
            if (!result.Workflows[flow.Id].Success) throw new Exception("第二轮执行失败");
        var receipts = factory.TakeCompletedReads(context);
        if (receipts.Count != 1 || receipts[0].Read.Entry.OriginalIndex != 1 || !factory.IsReceiptCurrent(context, receipts[0]))
            throw new Exception("选图后的读取收据或实际来源不一致");
        using var editorDebug = session.OpenDebugSession(flow.Id, blob);
        var privateDraft = session.Editing.Host.BeginEdit(flow.Id, blob.Id);
        using (var previewDebug = editorDebug.Capture(privateDraft, null, _ => { }, previewInputs: new[] { "input.image" }))
        {
            using (var preview = previewDebug.CaptureInputs())
            {
                if (!preview.IsCurrent || preview.Inputs.Get<IReadOnlyImage>("input.image") is null) throw new Exception("真实来源预览缺失图像");
                try { session.Dispose(); throw new Exception("预览未排空时方案意外关闭"); } catch (InvalidOperationException) { }
                try { previewDebug.Dispose(); throw new Exception("预览未排空时调试意外释放"); } catch (InvalidOperationException) { }
            }
        }
        using (var debug = editorDebug.Capture(privateDraft, null, _ => { }))
        using (var debugResult = debug.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
        {
            if (!debugResult.IsSuccess) throw new Exception("真实私有 Blob 调试失败");
            debug.AcceptParameters();
        }
        using (var stale = editorDebug.Capture(privateDraft, null, _ => { }))
        {
            var field = privateDraft.Configuration.Fields.Capture().First(pair => pair.Value is ConstantSetting);
            privateDraft.ApplyFields(new Dictionary<string, ParameterSetting> { [field.Key] = field.Value });
            try { using var ignored = stale.ExecuteAsync().GetAwaiter().GetResult(); throw new Exception("旧草稿调试意外成功"); }
            catch (InvalidOperationException) { }
        }
        using (var cancelledDebug = editorDebug.Capture(privateDraft, null, _ => { }))
        {
            cancelledDebug.Cancel();
            try { using var ignored = cancelledDebug.ExecuteAsync().GetAwaiter().GetResult(); throw new Exception("取消的调试意外成功"); }
            catch (OperationCanceledException) { }
        }
        if (session.ActiveActivities.Count != 1) throw new Exception("取消调试后仍有未释放的调用活动");
        var debugSourceDraft = session.Editing.Host.BeginEdit(flow.Id, blob.Id);
        using (var staleSource = editorDebug.Capture(debugSourceDraft, null, _ => { }))
        {
            using (var replacing = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: tracking))
            using (var replaced = replacing.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
                if (!replaced.Workflows[flow.Id].Success) throw new Exception("来源重新执行失败");
            try { using var ignored = staleSource.ExecuteAsync().GetAwaiter().GetResult(); throw new Exception("来源已替换的调试意外成功"); }
            catch (InvalidOperationException) { }
        }
        _ = factory.TakeCompletedReads(context);
        factory.SelectIndex(context, 1);
        Console.WriteLine("PASS: 私有 Blob 调试读取真实来源并拒绝旧草稿和旧结果");
        using (var nextBatch = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: tracking))
        using (var nextRun = nextBatch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
            if (!nextRun.Workflows[flow.Id].Success) throw new Exception("连续执行失败");
        if (factory.IsReceiptCurrent(context, receipts[0])) throw new Exception("旧收据在下一次读取后仍有效");
        var latest = factory.TakeCompletedReads(context);
        if (latest.Count != 1 || latest[0].Read.Entry.OriginalIndex != 1) throw new Exception("连续运行未自动切换图像");
        using (var resetBatch = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: tracking))
        {
            factory.Reset(context);
            using var resetRun = resetBatch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            if (!resetRun.Workflows[flow.Id].Success) throw new Exception("重置影响了已冻结运行");
        }
        if (factory.TakeCompletedReads(context).Count != 0) throw new Exception("重置后收到旧轮次回执");
        factory.SelectIndex(context, 0);
        if (factory.IsReceiptCurrent(context, latest[0])) throw new Exception("旧收据在新选择后仍有效");
        Console.WriteLine("PASS: 连续轮次、选图及旧收据失效");
        using (var variableDraft = session.Editing.Host.BeginVariableEdit())
        {
            var sourceReference = new OutputReference(source.Id, imagePort.Id, imagePort.Type, new[] { "width" }, scopeId: flow.Id);
            variableDraft.Variables.Add(new GlobalVariable("宽度变量", 0L, "Integer")
            { ProducerBinding = new ProducerBinding { ReferenceExpression = ParameterSettingCanonical.Serialize(sourceReference) } });
            session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, variables: variableDraft));
        }
        var widthVariable = session.Document.GlobalVariables.Single(item => item.Name == "宽度变量");
        var variableDraftNode = session.Editing.Host.BeginEdit(flow.Id, blob.Id);
        const string threshold = "field:43bda03261df55b88f963bd18f2ba4d9";
        variableDraftNode.ApplyFields(new Dictionary<string,ParameterSetting> {
            [threshold] = new VariableSetting(new VariableReference(session.Document.Id, widthVariable.Id, TypeRef.ScalarOf(ScalarKind.Int64)))
        });
        using (var variableDebug = editorDebug.Capture(variableDraftNode, null, _ => { }))
        using (var result = variableDebug.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
        {
            if (!result.IsSuccess) throw new Exception("生产变量私有调试失败");
            variableDebug.AcceptParameters();
            if (variableDraftNode.Configuration.ReadCurrentParameter(threshold)?.Value is not long current || current != 8L)
                throw new Exception("生产变量未从实际图像结果重算并写入私有草稿：" + variableDraftNode.Configuration.ReadCurrentParameter(threshold)?.Value + " 类型=" + variableDraftNode.Configuration.ReadCurrentParameter(threshold)?.Value?.GetType().FullName);
        }
        Console.WriteLine("PASS: 私有调试从真实来源重算生产变量并原子接纳参数");
        factory.SelectIndex(context, 1);

        File.Delete(imagePath2);
        using (var failedBatch = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: tracking))
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
        using (var cancelled = session.CaptureRunBatch(new[] { flow.Id }, hostFactory: tracking))
        {
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { using var ignored = cancelled.RunAsync(_ => { }, cancellation.Token).GetAwaiter().GetResult(); throw new Exception("取消的运行意外成功"); }
            catch (OperationCanceledException) { }
        }
        if (factory.TakeCompletedReads(context).Count != 0) throw new Exception("取消前未读取却生成收据");
        editorDebug.Dispose();
        using (var sourceEditor = session.OpenDebugSession(flow.Id, source))
        {
            string ReadPrivate()
            {
                var privateSource = session.Editing.Host.BeginEdit(flow.Id, source.Id);
                using var invocation = sourceEditor.Capture(privateSource, null, _ => { }, hostFactory: factory);
                using var result = invocation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                if (!result.IsSuccess) throw new Exception("私有图像源执行失败");
                using var reader = result.AcquireReader();
                return reader.Values!.Get<string>("output.file");
            }
            if (!string.Equals(ReadPrivate(), imagePath, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ReadPrivate(), imagePath2, StringComparison.OrdinalIgnoreCase))
                throw new Exception("私有图像源游标没有独立跨次切换");
            if (factory.TakeCompletedReads(context).Count != 0) throw new Exception("私有读取污染正式运行回执");
            var blocking = new BlockingFactory(factory);
            var cancelDraft = session.Editing.Host.BeginEdit(flow.Id, source.Id);
            using (var pending = sourceEditor.Capture(cancelDraft, null, _ => { }, hostFactory: blocking))
            {
                var running = pending.ExecuteAsync();
                if (!blocking.Started.Task.Wait(TimeSpan.FromSeconds(10))) throw new Exception("调试读取未进入异步准备");
                try { sourceEditor.Dispose(); throw new Exception("未排空调试时编辑器意外关闭"); }
                catch (InvalidOperationException) { }
                try { using var ignored = running.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); throw new Exception("编辑器关闭未取消调试"); }
                catch (OperationCanceledException) { }
            }
            sourceEditor.Dispose();
            Console.WriteLine("PASS: 异步输入准备中关闭取消并排空编辑器调试");
        }
        Console.WriteLine("PASS: 私有图像源连续游标与正式运行隔离");
        using var retiredEditor = session.OpenDebugSession(flow.Id, source);
        var retiredDraft = session.Editing.Host.BeginEdit(flow.Id, source.Id);
        var retiringFactory = new BlockingFactory(factory);
        using var retiredInvocation = retiredEditor.Capture(retiredDraft, null, _ => { }, hostFactory: retiringFactory);
        var retiredTask = retiredInvocation.ExecuteAsync();
        if (!retiringFactory.Started.Task.Wait(TimeSpan.FromSeconds(10))) throw new Exception("待删除节点的调试未进入异步准备");
        var remove = session.PrepareGraphEdit(flow.Id);
        remove.DeleteNodesAndConnections(new[] { source }, Array.Empty<ConnectionDefinition>());
        session.CommitGraphEdit(remove, "删除图像源");
        try { using var ignored = retiredTask.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); throw new Exception("删除节点未取消其调试"); }
        catch (OperationCanceledException) { }
        retiredInvocation.Dispose(); retiredEditor.Dispose();
        Console.WriteLine("PASS: 删除节点取消并排空其在途调试");
        try { context.EnsureCurrent(); throw new Exception("删除后的旧上下文仍然有效"); }
        catch (InvalidOperationException) { }
        if (!session.Undo()) throw new Exception("图像源删除撤销失败");
        var restored = session.CaptureHostInputContext(flow.Id, source)!;
        if (restored.NodeGeneration == context.NodeGeneration || !context.NodeRetirement.IsCancellationRequested)
            throw new Exception("撤销恢复错误复用已退役游标代次");
        Console.WriteLine("PASS: 取消不产生读取收据，删除和撤销隔离游标代次");
        var dataDir = Path.Combine(Path.GetDirectoryName(path)!, "_validation", "cross-flow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDir);
        var savedPath = Path.Combine(dataDir, "original.solution");
        var types = new FrozenVariableTypeCatalog(new Dictionary<string,string> { ["Integer"] = "System.Int64" });
        session.SaveAs(savedPath, Path.Combine(dataDir,"backup"), dataDir, types, new JsonSerializerOptions(), _ => null);
        var document = JsonNode.Parse(File.ReadAllText(savedPath))!;
        var workflows = document["Workflows"]!.AsArray();
        var second = workflows[0]!.DeepClone();
        second["Id"] = Guid.NewGuid().ToString("N"); second["Name"] = "跨流程测试";
        second["Nodes"] = new JsonArray(); second["Connections"] = new JsonArray();
        workflows.Add(second);
        var multiPath = Path.Combine(dataDir, "multi.solution");
        File.WriteAllText(multiPath, document.ToJsonString());
        var reopened = manager.Open(multiPath, _ => types, _ => null, new JsonSerializerOptions());
        var sourceFlow = reopened.GetWorkflows().Single(item => item.Id == flow.Id);
        var targetFlow = reopened.GetWorkflows().Single(item => item.Name == "跨流程测试");
        var targetGraph = reopened.PrepareGraphEdit(targetFlow.Id);
        var targetSource = reopened.CreateNode(targetGraph, "ImageLoad", new CanvasPoint(0, 0));
        var targetBlob = reopened.CreateNode(targetGraph, "BlobAnalysis", new CanvasPoint(200, 0));
        targetGraph.AddConnection(new ConnectionDefinition("cross-wire", targetSource.Id, targetBlob.Id, "output.image", "input.image"));
        reopened.CommitGraphEdit(targetGraph, "创建跨流程测试");
        var targetSourceDraft = reopened.Editing.Host.BeginEdit(targetFlow.Id, targetSource.Id);
        var targetPaths = targetSourceDraft.SettingsSchema.Inputs.Single(port => port.Id == "local.paths");
        targetSourceDraft.ApplyFields(new Dictionary<string,ParameterSetting> { ["local.paths"] = new ConstantSetting(new SequenceValue(targetPaths.Type, new object?[] { imagePath })) });
        var targetBlobDraft = reopened.Editing.Host.BeginEdit(targetFlow.Id, targetBlob.Id);
        var targetImage = reopened.GetNodeSchema(targetFlow.Id, targetSource.Id)!.Outputs.Single(port => port.Id == "output.image");
        var reopenedVariable = reopened.Document.GlobalVariables.Single(item => item.Name == "宽度变量");
        targetBlobDraft.ApplyFields(new Dictionary<string,ParameterSetting> {
            ["input.image"] = new ReferenceSetting(new OutputReference(targetSource.Id, targetImage.Id, targetImage.Type, new[] { "data" }, scopeId: targetFlow.Id)),
            [threshold] = new VariableSetting(new VariableReference(reopened.Document.Id, reopenedVariable.Id, TypeRef.ScalarOf(ScalarKind.Int64)))
        });
        reopened.CommitEdit(DocumentEditProposal.Prepare(reopened.Editing.Host, nodes: new[] { targetSourceDraft, targetBlobDraft }));
        using var crossFactory = new ImageSourceHostInputFactory();
        using (var crossBatch = reopened.CaptureRunBatch(new[] { sourceFlow.Id, targetFlow.Id }, hostFactory: crossFactory))
        using (var crossRun = crossBatch.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
            if (!crossRun.Workflows.Values.All(item => item.Success)) throw new Exception("跨流程正式运行失败");
        using (var crossEditor = reopened.OpenDebugSession(targetFlow.Id, targetBlob))
        {
            var crossDraft = reopened.Editing.Host.BeginEdit(targetFlow.Id, targetBlob.Id);
            using var crossDebug = crossEditor.Capture(crossDraft, null, _ => { });
            using var crossResult = crossDebug.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            if (!crossResult.IsSuccess) throw new Exception("跨流程生产变量调试失败");
            crossDebug.AcceptParameters();
            if (crossDraft.Configuration.ReadCurrentParameter(threshold)?.Value is not long number || number != 8L)
                throw new Exception("跨流程生产变量不是实际图像宽度");
        }
        Console.WriteLine("PASS: V4 保存重开、跨流程 producer 与私有参数接纳");
        var cameraOptions=new SimulatedCameraOptions {Width=8,Height=8,FrameDelay=TimeSpan.Zero};
        var cameraRuntime=new CameraRuntimeManager(new[]{new SimulatedCameraFactory(cameraOptions)});
        try
        {
            cameraRuntime.RegisterDevices(new SimulatedCameraDiscoveryProvider(cameraOptions).DiscoverAsync(CancellationToken.None).GetAwaiter().GetResult());
            using var cameraManager=new DocumentSessionManager(directory);
            var cameraSession=cameraManager.CreateNew("模拟相机运行");var cameraFlow=cameraSession.GetWorkflows().Single();
            var cameraGraph=cameraSession.PrepareGraphEdit(cameraFlow.Id);
            var cameraSource=cameraSession.CreateNode(cameraGraph,"ImageLoad",new CanvasPoint(0,0));
            var cameraBlob=cameraSession.CreateNode(cameraGraph,"BlobAnalysis",new CanvasPoint(200,0));
            cameraGraph.AddConnection(new ConnectionDefinition("camera-wire",cameraSource.Id,cameraBlob.Id,"output.image","input.image"));
            cameraSession.CommitGraphEdit(cameraGraph,"连接相机和 Blob");
            var cameraDraft=cameraSession.Editing.Host.BeginEdit(cameraFlow.Id,cameraSource.Id);
            cameraDraft.ApplyFields(new Dictionary<string,ParameterSetting>{{"source.kind",new ConstantSetting("camera")},{"camera.device",new ConstantSetting(cameraOptions.DeviceId)}});
            cameraSession.CommitEdit(DocumentEditProposal.Prepare(cameraSession.Editing.Host,nodes:new[]{cameraDraft}));
            using var cameraFactory=new ImageSourceHostInputFactory(cameraRuntime);
            using(var batch=cameraSession.CaptureRunBatch(new[]{cameraFlow.Id},hostFactory:cameraFactory))
            using(var result=batch.RunAsync(Console.WriteLine).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult())
            {
                if(!result.Workflows[cameraFlow.Id].Success)throw new Exception("模拟相机正式运行失败："+string.Join(";",result.Workflows[cameraFlow.Id].Errors));
                using var reader=cameraSession.AcquirePublishedResult(cameraFlow.Id,cameraBlob.Id)??throw new Exception("模拟相机 Blob reader 缺失");
            }
            var pendingOwner=new System.Collections.Concurrent.ConcurrentQueue<Action>();using var ownerSignal=new AutoResetEvent(false);
            void PostOwner(Action action){pendingOwner.Enqueue(action);ownerSignal.Set();}
            using(var continuous=cameraManager.CaptureCurrentContinuousRun(new[]{cameraFlow.Id},cameraFactory,PostOwner))
            {
                var completed=0;continuous.RoundCompleted+=_=>{if(++completed==2)continuous.Stop();};
                var running=continuous.RunAsync(_=>{});var deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
                while(!running.IsCompleted)
                {
                    while(pendingOwner.TryDequeue(out var work))work();
                    if(DateTime.UtcNow>deadline){continuous.Stop();throw new TimeoutException("模拟相机连续运行超时");}
                    ownerSignal.WaitOne(20);
                }
                while(pendingOwner.TryDequeue(out var work))work();
                var completion=running.GetAwaiter().GetResult();
                if(completion.Error is not null || completed<2)throw new Exception("模拟相机连续运行失败",completion.Error);
            }
            Console.WriteLine("PASS: 模拟相机生产工厂连续两轮与正常停止");
            using(var editor=cameraSession.OpenDebugSession(cameraFlow.Id,cameraSource))
            {
                var draft=cameraSession.Editing.Host.BeginEdit(cameraFlow.Id,cameraSource.Id);
                using var debug=editor.Capture(draft,null,_=>{},hostFactory:cameraFactory);
                using var result=debug.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                if(!result.IsSuccess)throw new Exception("模拟相机私有调试失败");
            }
            Console.WriteLine("PASS: 模拟相机实际工厂、正式 ImageLoad→Blob 与私有调试");
        }
        finally { cameraRuntime.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        File.Delete(imagePath2);
        File.Delete(imagePath);
        return 0;
    }
    private sealed class TrackingFactory(ImageSourceHostInputFactory inner) : IHostInputFactory
    {
        public readonly HashSet<string> Seen = new(StringComparer.Ordinal);
        public IHostInputPreparer? Create(HostInputFactoryContext context)
        {
            Seen.Add(context.Node.NodeId);
            return inner.Create(context);
        }
    }    private sealed class BlockingFactory(ImageSourceHostInputFactory inner) : IHostInputFactory
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IHostInputPreparer? Create(HostInputFactoryContext context)
        {
            var prepared = inner.Create(context);
            return prepared is null ? null : new BlockingPreparer(prepared, Started);
        }
    }
    private sealed class BlockingPreparer(IHostInputPreparer inner, TaskCompletionSource started) : IHostInputPreparer
    {
        public string CapabilityId => inner.CapabilityId;
        public int CapabilityVersion => inner.CapabilityVersion;
        public IReadOnlyList<string> Services => inner.Services;
        public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, context.Cancellation);
            return await inner.PrepareAsync(context);
        }
        public void Dispose() => inner.Dispose();
    }}
