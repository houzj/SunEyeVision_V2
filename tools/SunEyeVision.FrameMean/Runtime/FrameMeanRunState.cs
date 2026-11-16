using HalconDotNet;

namespace SunEyeVision.Tool.FrameMean.Runtime;

/// <summary>One node's frame accumulator: a real-valued running sum and the counted frames.</summary>
public sealed class FrameMeanAccumulator : IDisposable
{
    public const int MaximumFrameCount=100;
    private HImage? _sum;
    private int _count;
    public int Count=>Volatile.Read(ref _count);
    public bool HasSum=>_sum is not null;

    /// <summary>Adds one real frame; dimensions and channel count must match earlier frames.</summary>
    public void AddFrame(HImage realFrame,int width,int height,int channels)
    {
        lock(_gate)
        {
            if(_sum is not null && (_channels!=channels||_width!=width||_height!=height))
                throw new FrameMeanInputMismatchException("输入图像与已统计图像尺寸或通道数不一致。");
            if(_sum is null)
            {
                _sum=realFrame.Clone();
                _width=width;_height=height;_channels=channels;
            }
            else
            {
                HOperatorSet.AddImage(_sum,realFrame,out var added,1.0,0.0);
                using(added)
                {
                    _sum.Dispose();
                    _sum=new HImage(added);
                }
            }
            _count++;
        }
    }

    /// <summary>Returns the byte mean image; the caller owns the returned instance.</summary>
    public HImage TakeMean()
    {
        lock(_gate)
        {
            if(_sum is null||_count==0)throw new InvalidOperationException("没有可用的统计图像。");
            HOperatorSet.ScaleImage(_sum,out var scaled,1.0/_count,0.0);
            using(scaled)
            {
                HOperatorSet.ConvertImageType(scaled,out var converted,"byte");
                using(converted)return new HImage(converted);
            }
        }
    }

    public void Clear()
    {
        lock(_gate)
        {
            _sum?.Dispose();_sum=null;
            _count=0;
        }
    }
    private readonly object _gate=new();
    private int _width,_height,_channels;
    public void Dispose()=>Clear();
}

/// <summary>Per-node accumulation state borrowed for the duration of an invocation.</summary>
public sealed class FrameMeanRunState : IDisposable
{
    private readonly Dictionary<string,FrameMeanAccumulator> _nodes=new(StringComparer.Ordinal);
    private readonly object _gate=new();
    public FrameMeanAccumulator Node(string nodeId)
    {
        lock(_gate)
        {
            if(!_nodes.TryGetValue(nodeId,out var node))
                _nodes[nodeId]=node=new FrameMeanAccumulator();
            return node;
        }
    }
    public void Dispose()
    {
        FrameMeanAccumulator[] nodes;
        lock(_gate)
        {
            nodes=_nodes.Values.ToArray();
            _nodes.Clear();
        }
        foreach(var node in nodes)node.Dispose();
    }
}

/// <summary>Sum and frame geometry disagree with the running accumulator.</summary>
public sealed class FrameMeanInputMismatchException : Exception
{
    public FrameMeanInputMismatchException(string message):base(message) { }
}
