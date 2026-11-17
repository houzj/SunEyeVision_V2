using System.Diagnostics;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.DivideImage.Runtime;

/// <summary>一次同步调用只做几何计算：读冻结图像尺寸 → 算分片矩形 → 输出声明值。</summary>
internal sealed class DivideImageSession : IToolSession<DivideImageSettings, DivideImageOutput>
{
    private int _executing, _disposed;
    private readonly object _lifetimeGate = new();

    public ValueTask<ToolOutcome<DivideImageOutput>> ExecuteAsync(ToolInvocation<DivideImageSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _executing, 1) != 0) throw new InvalidOperationException("同一划片拆分会话不能并发执行。");
        try
        {
            lock (_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                return ValueTask.FromResult(Execute(invocation));
            }
        }
        finally { Volatile.Write(ref _executing, 0); }
    }

    private ToolOutcome<DivideImageOutput> Execute(ToolInvocation<DivideImageSettings> invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var cancellation = invocation.Context.Cancellation;
        cancellation.ThrowIfCancellationRequested();
        var settings = invocation.Settings;
        var errors = settings.Validate();
        if (errors.Count != 0) return new(false, null, BusinessJudgment.NotEvaluated, errors);
        var image = invocation.Inputs.Get<IReadOnlyImage>(DivideImageTool.ImageInput);
        ResourceContractRules.ValidateImage(image);
        var requested = (long)settings.Columns * settings.Rows;
        if (requested > DivideImageFields.MaximumTileCount)
            return ToolOutcome<DivideImageOutput>.Failed("DIVIDE_IMAGE_TILE_COUNT",
                $"分片总数 {requested} 超出上限 {DivideImageFields.MaximumTileCount}。");

        var started = Stopwatch.GetTimestamp();
        try
        {
            var tiles = DivideImageTiling.Compute(image.Width, image.Height,
                (int)settings.Columns, (int)settings.Rows, (int)settings.OverlapX, (int)settings.OverlapY);
            cancellation.ThrowIfCancellationRequested();
            var rects = new List<object?>(tiles.Count);
            foreach (var tile in tiles)
            {
                cancellation.ThrowIfCancellationRequested();
                rects.Add(DivideImageOutput.FreezeRect(tile.Column + tile.Width / 2d, tile.Row + tile.Height / 2d,
                    tile.Width, tile.Height));
            }
            var values = new Dictionary<string, object?>
            {
                // 原件 ROI 输出：未提供 ROI 输入时按整幅图像，与全图执行口径一致。
                [DivideImageOutput.Roi] = DivideImageOutput.FreezeRect(image.Width / 2d, image.Height / 2d,
                    image.Width, image.Height),
                [DivideImageOutput.Rects] = new SequenceValue(DivideImageOutput.RectsType, rects),
                [DivideImageOutput.Count] = (long)tiles.Count,
                [DivideImageOutput.ModuleStatus] = 1L,
                [DivideImageOutput.ModuleRunTime] =
                    (double)(float)Stopwatch.GetElapsedTime(started).TotalMilliseconds
            };
            var output = new DivideImageOutput(values);
            cancellation.ThrowIfCancellationRequested();
            return ToolOutcome<DivideImageOutput>.Completed(output);
        }
        catch (OperationCanceledException) { throw; }
        // 参数与几何不成立时给明确诊断，不产出空结果、不静默修正。
        catch (ArgumentException error)
        {
            return ToolOutcome<DivideImageOutput>.Failed("DIVIDE_IMAGE_GEOMETRY", error.Message);
        }
        catch (Exception error)
        {
            return ToolOutcome<DivideImageOutput>.Failed("DIVIDE_IMAGE_EXECUTION", error.Message);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetimeGate) Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }
}
