using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.FrameMean.Runtime;

/// <summary>One synchronous HALCON invocation; accumulation state lives per execution node.</summary>
internal sealed class FrameMeanSession : IToolSession<FrameMeanSettings,FrameMeanOutput>, IReusableToolSession
{
    private int _executing,_disposed;
    private readonly object _lifetimeGate=new();
    private FrameMeanRunState? _directState;
    public ValueTask<ToolOutcome<FrameMeanOutput>> ExecuteAsync(ToolInvocation<FrameMeanSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
        if(Interlocked.Exchange(ref _executing,1)!=0)throw new InvalidOperationException("同一帧均值会话不能并发执行。");
        try
        {
            lock(_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
                return ValueTask.FromResult(Execute(invocation));
            }
        }
        finally{Volatile.Write(ref _executing,0);}
    }
    private ToolOutcome<FrameMeanOutput> Execute(ToolInvocation<FrameMeanSettings> invocation)
    {
        var cancellation=invocation.Context.Cancellation;cancellation.ThrowIfCancellationRequested();
        var settings=invocation.Settings;
        var errors=settings.Validate();
        if(errors.Count!=0)return new(false,null,BusinessJudgment.NotEvaluated,errors);
        if(!invocation.Inputs.Contains(FrameMeanTool.ImageInput))
            return ToolOutcome<FrameMeanOutput>.Failed("FRAME_MEAN_INPUT","缺少本轮冻结的输入图像。");
        var inputImage=invocation.Inputs.Get<IReadOnlyImage>(FrameMeanTool.ImageInput);
        ResourceContractRules.ValidateImage(inputImage);
        var moduleStarted=System.Diagnostics.Stopwatch.GetTimestamp();
        IOutputResourceScope? resources=null;
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
            // The accumulator belongs to the execution node, not to a pooled session instance:
            // the host may hand any idle reusable session to any invocation, so session fields
            // must never carry per-node accumulation.
            var state=_directState??=new FrameMeanRunState();
            var accumulator=state.Node(invocation.Context.Identity.NodeId);
            using var frame=HalconResourceBridge.CopyImage(inputImage);
            using var channelsTuple=frame.CountChannels();
            int channels=channelsTuple.I;
            cancellation.ThrowIfCancellationRequested();
            if(channels is not (1 or 3 or 4))
                return ToolOutcome<FrameMeanOutput>.Failed("FRAME_MEAN_PIXEL_FORMAT","帧均值仅支持单通道或三/四通道图像。");
            resources=invocation.Context.Resources.CreateScope();
            switch(settings.ProcStep)
            {
                case "1":
                    // 原厂证据：ImgCountA >= 100 且选择统计当前图像时拒绝运行（统计图像上限 :100）。
                    if(accumulator.Count>=FrameMeanAccumulator.MaximumFrameCount)
                        return ToolOutcome<FrameMeanOutput>.Failed("FRAME_MEAN_COUNT_LIMIT","统计图像上限 :100。");
                    break;
                case "2":
                    // 原厂语义未知（无源码证据）：尚未统计任何图像时不得静默输出，直接报错。
                    if(accumulator.Count==0)
                        return ToolOutcome<FrameMeanOutput>.Failed("FRAME_MEAN_NO_FRAMES","尚未统计任何图像，无法输出均值图像。");
                    break;
                case "3":
                    // 原厂 ClearFrameResult 证据：清空已统计图像数量后运行。
                    accumulator.Clear();
                    break;
            }
            cancellation.ThrowIfCancellationRequested();
            // 只有"统计当前图像"模式把本轮输入计入累计；其余模式只读取已有统计。
            if(settings.ProcStep=="1")
            {
                HOperatorSet.GetImageSize(frame,out var widthTuple,out var heightTuple);
                using(widthTuple)using(heightTuple)
                {
                    int width=widthTuple.I,height=heightTuple.I;
                    HOperatorSet.ConvertImageType(frame,out var converted,"real");
                    using(converted)
                    using(var realFrame=new HImage(converted))
                        accumulator.AddFrame(realFrame,width,height,channels);
                }
            }
            cancellation.ThrowIfCancellationRequested();
            var values=new Dictionary<string,object?> {
                [FrameMeanOutput.ModuleStatus]=1L,
                [FrameMeanOutput.ImageCount]=(long)accumulator.Count
            };
            if(accumulator.Count>0)
            {
                using var mean=accumulator.TakeMean();
                cancellation.ThrowIfCancellationRequested();
                var imageFrame=new ImageFrameAddress(Guid.NewGuid(),"frame-mean:"+invocation.Context.Identity.NodeId,
                    invocation.Context.Identity,FrameMeanOutput.Image);
                var image=HalImageSnapshotSource.Capture(mean,imageFrame,resources);
                values[FrameMeanOutput.Image]=FrameMeanOutput.FreezeImage(image);
            }
            else values[FrameMeanOutput.Image]=null;
            values.Add(FrameMeanOutput.ModuleRunTime,
                (double)(float)System.Diagnostics.Stopwatch.GetElapsedTime(moduleStarted).TotalMilliseconds);
            var output=new FrameMeanOutput(values,resources);
            cancellation.ThrowIfCancellationRequested();
            resources=null;
            return ToolOutcome<FrameMeanOutput>.Completed(output);
        }
        catch(OperationCanceledException){throw;}
        catch(FrameMeanInputMismatchException error){return Failed("FRAME_MEAN_IMAGE_MISMATCH",error.Message);}
        catch(Exception error){return Failed("FRAME_MEAN_EXECUTION",error.Message);}
        finally{resources?.Dispose();}

        ToolOutcome<FrameMeanOutput> Failed(string code,string message)
            => ToolOutcome<FrameMeanOutput>.Failed(code,message);
    }
    public ValueTask DisposeAsync()
    {
        lock(_lifetimeGate)
            if(Interlocked.Exchange(ref _disposed,1)==0)Interlocked.Exchange(ref _directState,null)?.Dispose();
        return ValueTask.CompletedTask;
    }
}
