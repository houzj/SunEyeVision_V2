using System.Collections.Concurrent;
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
        Console.WriteLine($"PASS 多轮={completion.CompletedRounds} 末张停止，真实结果reader可用");
    }
    private static void WriteBitmap(string path)
    {
        using var writer=new BinaryWriter(File.Create(path));
        writer.Write((ushort)0x4d42); writer.Write(54+8*8*3); writer.Write(0); writer.Write(54); writer.Write(40); writer.Write(8); writer.Write(8); writer.Write((ushort)1); writer.Write((ushort)24); writer.Write(0); writer.Write(8*8*3); for(int i=0;i<4;i++)writer.Write(0);
        for(int y=0;y<8;y++)for(int x=0;x<8;x++){byte v=(byte)(x>=2&&x<6&&y>=2&&y<6?255:0);writer.Write(v);writer.Write(v);writer.Write(v);}
    }
}
