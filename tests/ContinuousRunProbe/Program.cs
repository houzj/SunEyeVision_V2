using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Solutions;
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
        SetErrorMode(0x0001|0x0002|0x8000);
        try { Run(args); return 0; } catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Run(string[] args)
    {
        var payload=Path.GetFullPath(args[0]); var data=Path.GetFullPath(args[1]); Directory.CreateDirectory(data);
        var paths=Enumerable.Range(0,3).Select(i=>Path.Combine(data,$"image-{i}.bmp")).ToArray();
        foreach(var path in paths) WriteBitmap(path);
        var directory=new ReleasePayloadAuthority(payload).PrepareRuntimeDirectory();
        using var manager=new DocumentSessionManager(directory);
        var session=manager.CreateNew("连续运行探针"); var flow=session.GetWorkflows().Single();
        var graph=session.PrepareGraphEdit(flow.Id);
        var source=session.CreateNode(graph,"ImageLoad",new CanvasPoint(0,0));
        var blob=session.CreateNode(graph,"BlobAnalysis",new CanvasPoint(200,0));
        graph.AddConnection(new ConnectionDefinition("wire",source.Id,blob.Id,"output.image","input.image"));
        session.CommitGraphEdit(graph,"创建真实运行链");
        var sourceDraft=session.Editing.Host.BeginEdit(flow.Id,source.Id);
        var port=sourceDraft.SettingsSchema.Inputs.Single(p=>p.Id=="local.paths");
        sourceDraft.ApplyFields(new Dictionary<string,ParameterSetting> {
            ["local.paths"]=new ConstantSetting(new SequenceValue(port.Type,paths.Cast<object?>().ToArray())),
            ["local.auto-switch"]=new ConstantSetting(true), ["local.stop-at-last"]=new ConstantSetting(true) });
        var blobDraft=session.Editing.Host.BeginEdit(flow.Id,blob.Id);
        var output=session.GetNodeSchema(flow.Id,source.Id)!.Outputs.Single(p=>p.Id=="output.image");
        blobDraft.ApplyFields(new Dictionary<string,ParameterSetting> { ["input.image"]=new ReferenceSetting(new OutputReference(source.Id,output.Id,output.Type,new[]{"data"},scopeId:flow.Id)) });
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{sourceDraft,blobDraft}));
        using var factory=new ImageSourceHostInputFactory();
        var queue=new ConcurrentQueue<Action>(); using var signal=new AutoResetEvent(false);
        void Post(Action action){queue.Enqueue(action);signal.Set();}
        using var continuous=manager.CaptureCurrentContinuousRun(new[]{flow.Id},factory,Post);
        var rounds=new List<ContinuousRoundSummary>();continuous.RoundCompleted+=rounds.Add;
        var task=continuous.RunAsync(Console.WriteLine);
        var deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
        while(!task.IsCompleted)
        {
            while(queue.TryDequeue(out var action))action();
            if(DateTime.UtcNow>deadline){continuous.Stop();throw new TimeoutException("连续运行探针超时。");}
            signal.WaitOne(20);
        }
        while(queue.TryDequeue(out var action))action();
        var completion=task.GetAwaiter().GetResult();
        if(completion.Error is not null)throw new Exception("连续运行失败",completion.Error);
        if(completion.Reason!=ContinuousStopReason.LastImage || completion.CompletedRounds<2 || rounds.Count<2)
            throw new Exception($"末张停止或多轮无效：{completion} 通知数={rounds.Count}");
        using var reader=session.AcquirePublishedResult(flow.Id,blob.Id)??throw new Exception("缺少最终结果reader");
        var firstIdentity=reader.Snapshot.Identity;
        continuous.Dispose();
        Console.WriteLine($"PASS 多轮={completion.CompletedRounds} 末张停止，真实结果reader可用");

        var nextSource=session.Editing.Host.BeginEdit(flow.Id,source.Id);
        nextSource.ApplyFields(new Dictionary<string,ParameterSetting> { ["local.stop-at-last"]=new ConstantSetting(false) });
        var root=session.Editing.Host.BeginRootEdit(flow.Id);
        root.WorkspaceSettings=root.WorkspaceSettings with { RunIntervalMs=500 };
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{nextSource},roots:new[]{root}));
        using var repeated=manager.CaptureCurrentContinuousRun(new[]{flow.Id},factory,Post);
        var roundCount=0; var stoppedAt=DateTime.MinValue; var resumedAt=DateTime.MinValue; var firstStoppedAt=DateTime.MinValue;
        repeated.RoundCompleted+=_=>throw new InvalidOperationException("观察者故障探针");
        repeated.RoundCompleted+=summary=>
        {
            roundCount++;
            if(summary.Round==1)
            {
                if(!repeated.Pause())throw new Exception("首轮后暂停失败");
                firstStoppedAt=DateTime.UtcNow;
            }
            if(summary.Round==2){stoppedAt=DateTime.UtcNow;repeated.Stop();}
        };
        var again=repeated.RunAsync(_=>{}); deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
        while(firstStoppedAt==DateTime.MinValue) { Pump(queue,signal,deadline); if(again.IsCompleted)throw new Exception("暂停前连续运行已结束"); }
        try { using var unexpected=manager.CaptureCurrentContinuousRun(new[]{flow.Id},factory,Post); throw new Exception("暂停期间错误地释放根预约"); }
        catch(InvalidOperationException) { }
        var pauseUntil=DateTime.UtcNow+TimeSpan.FromMilliseconds(700);
        while(DateTime.UtcNow<pauseUntil)Pump(queue,signal,deadline);
        if(roundCount!=1||again.IsCompleted||repeated.State.Phase!=ContinuousPhase.Paused)throw new Exception("暂停期间仍执行下一轮");
        resumedAt=DateTime.UtcNow;
        if(!repeated.Resume())throw new Exception("恢复失败");
        while(!again.IsCompleted)Pump(queue,signal,deadline);
        while(queue.TryDequeue(out var pending))pending();
        var stopped=again.GetAwaiter().GetResult();
        if(stopped.Error is not null || stopped.Reason!=ContinuousStopReason.Requested || roundCount!=2 || stoppedAt-resumedAt<TimeSpan.FromMilliseconds(300))
            throw new Exception($"暂停剩余间隔或停止错误：{stopped} 轮数={roundCount} 恢复等待={stoppedAt-resumedAt}");
        if(repeated.LastObserverError is null)throw new Exception("观察者异常未聚合");
        if(reader.Snapshot.Identity!=firstIdentity || reader.Values is null)throw new Exception("跨轮 reader 被提前释放");
        repeated.Dispose();
        Console.WriteLine("PASS 暂停保留间隔、根预约、停止及观察者失败隔离；旧reader跨轮有效");
        var waitingRoot=session.Editing.Host.BeginRootEdit(flow.Id);
        waitingRoot.WorkspaceSettings=waitingRoot.WorkspaceSettings with { RunIntervalMs=1500 };
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,roots:new[]{waitingRoot}));
        using(var waitRun=manager.CaptureCurrentContinuousRun(new[]{flow.Id},factory,Post))
        {
            var settled=new List<ContinuousRoundSummary>();waitRun.RoundCompleted+=settled.Add;
            var waiting=waitRun.RunAsync(_=>{});deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
            while(waitRun.State.Phase!=ContinuousPhase.Waiting) { Pump(queue,signal,deadline);if(waiting.IsCompleted)throw new Exception("间隔等待前已结束"); }
            var stopAt=DateTime.UtcNow;waitRun.Stop();
            while(!waiting.IsCompleted)Pump(queue,signal,deadline);
            while(queue.TryDequeue(out var doneAction))doneAction();
            var stoppedWait=waiting.GetAwaiter().GetResult();
            if(stoppedWait.Reason!=ContinuousStopReason.Requested || stoppedWait.Error is not null || DateTime.UtcNow-stopAt>TimeSpan.FromMilliseconds(1000) || settled.Count!=1)
                throw new Exception("间隔停止未及时唤醒或额外执行："+stoppedWait);
        }
        Console.WriteLine("PASS 间隔期间Stop立即唤醒且不执行下一轮");

        var ngRoot=session.Editing.Host.BeginRootEdit(flow.Id);
        ngRoot.WorkspaceSettings=ngRoot.WorkspaceSettings with { StopOnNg=true,RunIntervalMs=0 };
        var ngBlob=session.Editing.Host.BeginEdit(flow.Id,blob.Id);
        ngBlob.ApplyFields(new Dictionary<string,ParameterSetting> {
            ["field:fa6fb37f3a3e5fde8a7f15becfbedf07"]=new ConstantSetting(true),
            ["field:bb037ce3fa405b059d7cb3e696e4138c"]=new ConstantSetting(2d) });
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{ngBlob},roots:new[]{ngRoot}));
        using(var ngRun=manager.CaptureCurrentContinuousRun(new[]{flow.Id},factory,Post))
        {
            var ngSummaries=new List<ContinuousRoundSummary>();ngRun.RoundCompleted+=ngSummaries.Add;
            var ngTask=ngRun.RunAsync(_=>{}); deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
            while(!ngTask.IsCompleted)Pump(queue,signal,deadline);
            while(queue.TryDequeue(out var pendingNg))pendingNg();
            var ng=ngTask.GetAwaiter().GetResult();
            if(ng.Error is not null||ng.Reason!=ContinuousStopReason.BusinessNg||ng.CompletedRounds!=1||!ngSummaries.Single().Workflows.Single().HasBusinessNg)
                throw new Exception("真实Blob NG未按根配置停止："+ng);
        }
        Console.WriteLine("PASS 真实Blob判定NG后停止且非执行失败");
        using var canceled=manager.CaptureCurrentContinuousRun(new[]{flow.Id},factory,Post);
        canceled.Stop();
        var canceledResult=canceled.RunAsync(_=>{}).GetAwaiter().GetResult();
        if(canceledResult.Reason!=ContinuousStopReason.Requested || canceledResult.Error is not null)throw new Exception("运行前停止无效");
        Console.WriteLine("PASS 运行前停止及释放");
        canceled.Dispose(); reader.Dispose();

        var resetRoot=session.Editing.Host.BeginRootEdit(flow.Id);
        resetRoot.WorkspaceSettings=resetRoot.WorkspaceSettings with { StopOnNg=false,RunIntervalMs=50 };
        var resetBlob=session.Editing.Host.BeginEdit(flow.Id,blob.Id);
        resetBlob.ApplyFields(new Dictionary<string,ParameterSetting> { ["field:fa6fb37f3a3e5fde8a7f15becfbedf07"]=new ConstantSetting(false) });
        using(var variables=session.Editing.Host.BeginVariableEdit())
        {
            var producer=new OutputReference(source.Id,output.Id,output.Type,new[]{"width"},scopeId:flow.Id);
            variables.Variables.Add(new GlobalVariable("宽度变量",0L,"Integer") { ProducerBinding=new ProducerBinding { ReferenceExpression=ParameterSettingCanonical.Serialize(producer) } });
            session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{resetBlob},roots:new[]{resetRoot},variables:variables));
        }
        var types=new FrozenVariableTypeCatalog(new Dictionary<string,string> { ["Integer"]="System.Int64" });
        var saved=Path.Combine(data,"single.solution");
        session.SaveAs(saved,Path.Combine(data,"backup"),data,types,new JsonSerializerOptions(),_=>null);
        var json=JsonNode.Parse(File.ReadAllText(saved))!;var workflows=json["Workflows"]!.AsArray();
        var copied=workflows[0]!.DeepClone();copied["Id"]=Guid.NewGuid().ToString("N");copied["Name"]="跨流程";copied["Nodes"]=new JsonArray();copied["Connections"]=new JsonArray();workflows.Add(copied);
        var multi=Path.Combine(data,"multi.solution");File.WriteAllText(multi,json.ToJsonString());
        var reopened=manager.Open(multi,_=>types,_=>null,new JsonSerializerOptions());
        var sourceFlow=reopened.GetWorkflows().Single(item=>item.Id==flow.Id);
        var targetFlow=reopened.GetWorkflows().Single(item=>item.Name=="跨流程");
        var g=reopened.PrepareGraphEdit(targetFlow.Id);
        var targetSource=reopened.CreateNode(g,"ImageLoad",new CanvasPoint(0,0));var targetBlob=reopened.CreateNode(g,"BlobAnalysis",new CanvasPoint(200,0));
        g.AddConnection(new ConnectionDefinition("target-wire",targetSource.Id,targetBlob.Id,"output.image","input.image"));reopened.CommitGraphEdit(g,"创建跨流程链");
        var ts=reopened.Editing.Host.BeginEdit(targetFlow.Id,targetSource.Id);var tp=ts.SettingsSchema.Inputs.Single(item=>item.Id=="local.paths");
        ts.ApplyFields(new Dictionary<string,ParameterSetting> { ["local.paths"]=new ConstantSetting(new SequenceValue(tp.Type,new object?[]{paths[0]})) });
        var tb=reopened.Editing.Host.BeginEdit(targetFlow.Id,targetBlob.Id);var sourceImage=reopened.GetNodeSchema(targetFlow.Id,targetSource.Id)!.Outputs.Single(item=>item.Id=="output.image");
        var width=reopened.Document.GlobalVariables.Single(item=>item.Name=="宽度变量");const string threshold="field:43bda03261df55b88f963bd18f2ba4d9";
        tb.ApplyFields(new Dictionary<string,ParameterSetting> { ["input.image"]=new ReferenceSetting(new OutputReference(targetSource.Id,sourceImage.Id,sourceImage.Type,new[]{"data"},scopeId:targetFlow.Id)), [threshold]=new VariableSetting(new VariableReference(reopened.Document.Id,width.Id,TypeRef.ScalarOf(ScalarKind.Int64))) });
        var targetRoot=reopened.Editing.Host.BeginRootEdit(targetFlow.Id);targetRoot.WorkspaceSettings=targetRoot.WorkspaceSettings with { RunIntervalMs=1500 };
        reopened.CommitEdit(DocumentEditProposal.Prepare(reopened.Editing.Host,nodes:new[]{ts,tb},roots:new[]{targetRoot}));
        using var crossFactory=new ImageSourceHostInputFactory();
        using var cross=manager.CaptureCurrentContinuousRun(new[]{targetFlow.Id,sourceFlow.Id},crossFactory,Post);
        var crossRounds=new List<ContinuousRoundSummary>();var starts=new List<(string Flow,DateTime At)>();var targetEnds=new List<DateTime>();
        cross.NodeStatusChanged+=status=>{if(status.Status==ExecutionStatus.Running && (status.NodeId==source.Id||status.NodeId==targetSource.Id))starts.Add((status.WorkflowId,DateTime.UtcNow)); if(status.WorkflowId==targetFlow.Id&&status.NodeId==targetBlob.Id&&status.Status==ExecutionStatus.Success)targetEnds.Add(DateTime.UtcNow);};
        cross.RoundCompleted+=summary=>{crossRounds.Add(summary);if(summary.Round==2)cross.Stop();};
        var crossTask=cross.RunAsync(_=>{});deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
        while(!crossTask.IsCompleted)Pump(queue,signal,deadline);
        while(queue.TryDequeue(out var action))action();
        var crossResult=crossTask.GetAwaiter().GetResult();
        if(crossResult.Error is not null||crossResult.Reason!=ContinuousStopReason.Requested||crossRounds.Count!=2 ||
            crossRounds.Any(round=>!round.Workflows.Select(item=>item.WorkflowId).SequenceEqual(new[]{sourceFlow.Id,targetFlow.Id})))
            throw new Exception("跨流程顺序或停止错误："+crossResult);
        if(targetBlob.Configuration.ReadCurrentParameter(threshold)?.Value is not long value || value!=8L)
            throw new Exception("跨流程变量未在连续轮次接纳图像宽度。");
        var sourceTimes=starts.Where(item=>item.Flow==sourceFlow.Id).Select(item=>item.At).ToArray();var targetTimes=starts.Where(item=>item.Flow==targetFlow.Id).Select(item=>item.At).ToArray();
        if(sourceTimes.Length!=2||targetTimes.Length!=2||targetTimes[1]-targetTimes[0]<TimeSpan.FromMilliseconds(1400)||sourceTimes[1]>=targetTimes[1]||targetEnds.Count!=2||sourceTimes[1]-targetEnds[0]>TimeSpan.FromMilliseconds(900))
            throw new Exception("不同根的各自间隔或拓扑顺序错误。");
        Console.WriteLine("PASS 两根反序请求按跨流程生产依赖排序，变量逐轮接纳，各自间隔生效");
        cross.Dispose();
        var oneSource=reopened.Editing.Host.BeginEdit(sourceFlow.Id,source.Id);
        var onePathPort=oneSource.SettingsSchema.Inputs.Single(item=>item.Id=="local.paths");
        oneSource.ApplyFields(new Dictionary<string,ParameterSetting> {
            ["local.paths"]=new ConstantSetting(new SequenceValue(onePathPort.Type,new object?[]{paths[0]})),
            ["local.stop-at-last"]=new ConstantSetting(true) });
        reopened.CommitEdit(DocumentEditProposal.Prepare(reopened.Editing.Host,nodes:new[]{oneSource}));
        using(var lastFactory=new ImageSourceHostInputFactory())
        using(var multiLast=manager.CaptureCurrentContinuousRun(new[]{targetFlow.Id,sourceFlow.Id},lastFactory,Post))
        {
            var settled=new List<ContinuousRoundSummary>();var targetStarts=0;
            multiLast.RoundCompleted+=settled.Add;
            multiLast.NodeStatusChanged+=status=>{if(status.WorkflowId==targetFlow.Id&&status.Status==ExecutionStatus.Running)targetStarts++;};
            var pending=multiLast.RunAsync(_=>{});deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
            while(!pending.IsCompleted)Pump(queue,signal,deadline);
            while(queue.TryDequeue(out var doneAction))doneAction();
            var stop=pending.GetAwaiter().GetResult();
            if(stop.Error is not null||stop.Reason!=ContinuousStopReason.LastImage||settled.Count!=1||
               settled[0].Workflows.Count!=1||settled[0].Workflows[0].WorkflowId!=sourceFlow.Id||targetStarts!=0)
                throw new Exception("前序根末图未停止后续依赖根："+stop);
        }
        var restoreSource=reopened.Editing.Host.BeginEdit(sourceFlow.Id,source.Id);
        var restorePort=restoreSource.SettingsSchema.Inputs.Single(item=>item.Id=="local.paths");
        restoreSource.ApplyFields(new Dictionary<string,ParameterSetting> {
            ["local.paths"]=new ConstantSetting(new SequenceValue(restorePort.Type,paths.Cast<object?>().ToArray())),
            ["local.stop-at-last"]=new ConstantSetting(false) });
        reopened.CommitEdit(DocumentEditProposal.Prepare(reopened.Editing.Host,nodes:new[]{restoreSource}));
        Console.WriteLine("PASS 前序根末图停止整个会话且后续根未启动");
        var multiNgRoot=reopened.Editing.Host.BeginRootEdit(sourceFlow.Id);
        multiNgRoot.WorkspaceSettings=multiNgRoot.WorkspaceSettings with { StopOnNg=true };
        var multiNgBlob=reopened.Editing.Host.BeginEdit(sourceFlow.Id,blob.Id);
        multiNgBlob.ApplyFields(new Dictionary<string,ParameterSetting> {
            ["field:fa6fb37f3a3e5fde8a7f15becfbedf07"]=new ConstantSetting(true),
            ["field:bb037ce3fa405b059d7cb3e696e4138c"]=new ConstantSetting(2d) });
        reopened.CommitEdit(DocumentEditProposal.Prepare(reopened.Editing.Host,nodes:new[]{multiNgBlob},roots:new[]{multiNgRoot}));
        using(var stopFactory=new ImageSourceHostInputFactory())
        using(var multiNg=manager.CaptureCurrentContinuousRun(new[]{targetFlow.Id,sourceFlow.Id},stopFactory,Post))
        {
            var settled=new List<ContinuousRoundSummary>();var targetStarts=0;
            multiNg.RoundCompleted+=settled.Add;
            multiNg.NodeStatusChanged+=status=>{if(status.WorkflowId==targetFlow.Id&&status.Status==ExecutionStatus.Running)targetStarts++;};
            var pending=multiNg.RunAsync(_=>{});deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
            while(!pending.IsCompleted)Pump(queue,signal,deadline);
            while(queue.TryDequeue(out var doneAction))doneAction();
            var stop=pending.GetAwaiter().GetResult();
            if(stop.Error is not null||stop.Reason!=ContinuousStopReason.BusinessNg||settled.Count!=1||
               settled[0].Workflows.Count!=1||settled[0].Workflows[0].WorkflowId!=sourceFlow.Id||targetStarts!=0)
                throw new Exception("前序根NG未停止后续依赖根："+stop);
        }
        var resumeRoot=reopened.Editing.Host.BeginRootEdit(sourceFlow.Id);
        resumeRoot.WorkspaceSettings=resumeRoot.WorkspaceSettings with { StopOnNg=false };
        var resumeBlob=reopened.Editing.Host.BeginEdit(sourceFlow.Id,blob.Id);
        resumeBlob.ApplyFields(new Dictionary<string,ParameterSetting> { ["field:fa6fb37f3a3e5fde8a7f15becfbedf07"]=new ConstantSetting(false) });
        reopened.CommitEdit(DocumentEditProposal.Prepare(reopened.Editing.Host,nodes:new[]{resumeBlob},roots:new[]{resumeRoot}));
        Console.WriteLine("PASS 前序根NG停止整个会话且后续根未启动");
        foreach(var mode in new[]{0,1,2,3})
        {
            using var gate=new BlockingFactory(mode==1||mode==3,mode==3);
            using var external=new CancellationTokenSource();
            using var pendingRun=manager.CaptureCurrentContinuousRun(new[]{sourceFlow.Id},gate,Post);
            var work=pendingRun.RunAsync(_=>{},external.Token);deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
            while(!gate.Started.Task.IsCompleted) { Pump(queue,signal,deadline); if(work.IsCompleted)throw new Exception("输入准备尚未开始即结束"); }
            if(mode==2)external.Cancel();else if(mode!=3) { pendingRun.Stop();pendingRun.Stop(); }
            while(!work.IsCompleted)Pump(queue,signal,deadline);
            while(queue.TryDequeue(out var pendingAction))pendingAction();
            var outcome=work.GetAwaiter().GetResult();
            if(gate.DisposeCount!=1)throw new Exception("停止后宿主输入未恰好释放一次");
            if(mode==1||mode==3)
            {
                if(outcome.Reason!=ContinuousStopReason.Failed||outcome.Error is not AggregateException||!outcome.Error.ToString().Contains("synthetic-dispose-failure"))
                    throw new Exception("资源释放失败未聚合并报告："+outcome.Error);
                if(mode==3&&!outcome.Error.ToString().Contains("synthetic-prepare-failure"))throw new Exception("执行失败与释放失败未同时保留："+outcome.Error);
            }
            else if(outcome.Reason!=(mode==2?ContinuousStopReason.Cancelled:ContinuousStopReason.Requested)||outcome.Error is not null)throw new Exception("在途停止或取消未排空："+outcome);
        }
        Console.WriteLine("PASS 在途停止幂等、资源唯一释放及释放失败聚合");

        var sourceGraph=reopened.PrepareGraphEdit(sourceFlow.Id);
        sourceGraph.SetNodeEnabled(sourceFlow.Nodes.Single(item=>item.Id==blob.Id),false);
        reopened.CommitGraphEdit(sourceGraph,"暂停Blob，仅运行未判定图像源");
        var unknownRoot=reopened.Editing.Host.BeginRootEdit(sourceFlow.Id);
        unknownRoot.WorkspaceSettings=unknownRoot.WorkspaceSettings with { StopOnNg=true,RunIntervalMs=0 };
        reopened.CommitEdit(DocumentEditProposal.Prepare(reopened.Editing.Host,roots:new[]{unknownRoot}));
        using(var unknownFactory=new ImageSourceHostInputFactory())
        using(var unknown=manager.CaptureCurrentContinuousRun(new[]{sourceFlow.Id},unknownFactory,Post))
        {
            var observed=new List<ContinuousRoundSummary>();
            unknown.RoundCompleted+=round=>{observed.Add(round);if(round.Round==2)unknown.Stop();};
            var pending=unknown.RunAsync(_=>{});deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
            while(!pending.IsCompleted)Pump(queue,signal,deadline);
            while(queue.TryDequeue(out var next))next();
            var result=pending.GetAwaiter().GetResult();
            if(result.Error is not null||result.Reason!=ContinuousStopReason.Requested||observed.Count!=2||observed.Any(r=>r.Workflows.Single().HasBusinessNg))
                throw new Exception("NotEvaluated 被误判为NG："+result);
        }
        Console.WriteLine("PASS NotEvaluated不触发NG停止");

        using(var failFactory=new ImageSourceHostInputFactory())
        using(var failed=manager.CaptureCurrentContinuousRun(new[]{sourceFlow.Id},failFactory,Post))
        {
            var failedRounds=new List<ContinuousRoundSummary>();failed.RoundCompleted+=failedRounds.Add;
            foreach(var path in paths)File.Delete(path);
            try
            {
                var failedTask=failed.RunAsync(_=>{});deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
                while(!failedTask.IsCompleted)Pump(queue,signal,deadline);
                while(queue.TryDequeue(out var next))next();
                var failedResult=failedTask.GetAwaiter().GetResult();
                if(failedResult.Reason!=ContinuousStopReason.Failed||failedResult.Error is null||failedRounds.Count!=1||failedRounds[0].Workflows.Single().Errors.Count==0)
                    throw new Exception("源文件缺失未保留正式失败诊断："+failedResult);
            }
            finally { foreach(var path in paths)WriteBitmap(path); }
        }
        Console.WriteLine("PASS 源读取失败时停止并保留原始诊断");

        using(var forgedFactory=new ForgedFactory())
        using(var forged=manager.CaptureCurrentContinuousRun(new[]{sourceFlow.Id},forgedFactory,Post))
        {
            var pending=forged.RunAsync(_=>{});deadline=DateTime.UtcNow+TimeSpan.FromSeconds(30);
            while(!pending.IsCompleted)Pump(queue,signal,deadline);
            var result=pending.GetAwaiter().GetResult();
            if(result.Reason!=ContinuousStopReason.Failed||result.Error is null||!result.Error.ToString().Contains("回执不属于本轮"))
                throw new Exception("伪造图像请求回执未拒绝："+result);
        }
        Console.WriteLine("PASS 本轮以外的伪造末图请求回执被拒绝");
        using(var badFactory=new ImageSourceHostInputFactory())
        using(var badOwner=manager.CaptureCurrentContinuousRun(new[]{sourceFlow.Id},badFactory,action=>Task.Run(action)))
        {
            var result=badOwner.RunAsync(_=>{}).WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
            if(result.Reason!=ContinuousStopReason.Failed||result.Error is null||!result.Error.ToString().Contains("所属线程"))
                throw new Exception("错误线程投递未被拒绝："+result);
        }
        using(var verifyFactory=new ImageSourceHostInputFactory())
        using(var verify=manager.CaptureCurrentRunBatch(new[]{sourceFlow.Id},hostFactory:verifyFactory))
        using(var verifyResult=verify.RunAsync(_=>{}).WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult())
            if(!verifyResult.Workflows[sourceFlow.Id].Success)throw new Exception("错误投递后不能重新运行");
        Console.WriteLine("PASS 错误线程投递拒绝，owner释放后可重新运行");
    }
    private sealed class ForgedFactory : IHostInputFactory, IDisposable
    {
        private readonly ImageSourceHostInputFactory _inner=new();
        public IHostInputPreparer? Create(HostInputFactoryContext context)
        {
            var item=_inner.Create(context);return item is null?null:new ForgedPreparer(context,item);
        }
        public void Dispose()=>_inner.Dispose();
    }
    private sealed class ForgedPreparer(HostInputFactoryContext owner,IHostInputPreparer inner) : IHostInputPreparer,IHostInputRoundObserver
    {
        private RunIdentity? _identity;
        public string CapabilityId=>inner.CapabilityId;
        public int CapabilityVersion=>inner.CapabilityVersion;
        public IReadOnlyList<string> Services=>inner.Services;
        public ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)
        { _identity=context.Identity;return inner.PrepareAsync(context); }
        public void Dispose()=>inner.Dispose();
        public IReadOnlyList<HostReadCompletion> CaptureCompletedReads()
        {
            var actual=_identity??throw new InvalidOperationException("测试准备器未接纳");
            return new[]{new HostReadCompletion(owner.EditSessionId,owner.NodeGeneration,owner.Node,actual with { RequestId=Guid.NewGuid() },true)};
        }
    }
    private sealed class BlockingFactory(bool failCleanup,bool failPreparation=false) : IHostInputFactory, IDisposable
    {
        private readonly ImageSourceHostInputFactory _inner=new();
        public readonly TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DisposeCount;
        public IHostInputPreparer? Create(HostInputFactoryContext context)
        {
            var item=_inner.Create(context);return item is null?null:new BlockingPreparer(this,item,failCleanup,failPreparation);
        }
        public void Dispose()=>_inner.Dispose();
    }
    private sealed class BlockingPreparer(BlockingFactory factory,IHostInputPreparer inner,bool failCleanup,bool failPreparation) : IHostInputPreparer
    {
        public string CapabilityId=>inner.CapabilityId;
        public int CapabilityVersion=>inner.CapabilityVersion;
        public IReadOnlyList<string> Services=>inner.Services;
        public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)
        { factory.Started.TrySetResult();if(failPreparation)throw new InvalidOperationException("synthetic-prepare-failure");await Task.Delay(Timeout.InfiniteTimeSpan,context.Cancellation);return await inner.PrepareAsync(context); }
        public void Dispose()
        {
            Interlocked.Increment(ref factory.DisposeCount);inner.Dispose();
            if(failCleanup)throw new InvalidOperationException("synthetic-dispose-failure");
        }
    }
    private static void Pump(ConcurrentQueue<Action> queue,AutoResetEvent signal,DateTime deadline)
    {
        while(queue.TryDequeue(out var action))action();
        if(DateTime.UtcNow>deadline)throw new TimeoutException("连续运行探针超时");
        signal.WaitOne(20);
    }
    private static void WriteBitmap(string path)
    {
        using var writer=new BinaryWriter(File.Create(path));
        writer.Write((ushort)0x4d42); writer.Write(54+8*8*3); writer.Write(0); writer.Write(54); writer.Write(40); writer.Write(8); writer.Write(8); writer.Write((ushort)1); writer.Write((ushort)24); writer.Write(0); writer.Write(8*8*3); for(int i=0;i<4;i++)writer.Write(0);
        for(int y=0;y<8;y++)for(int x=0;x<8;x++){byte v=(byte)(x>=2&&x<6&&y>=2&&y<6?255:0);writer.Write(v);writer.Write(v);writer.Write(v);}
    }
}
