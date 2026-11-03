using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Infrastructure.Persistence;

/// <summary>One owned immutable persistent raster; readers retain independently of its root owner.</summary>
internal sealed class FrozenStoredRaster : IReadOnlyStoredRaster, IDisposable
{
    private sealed class State(StoredRasterLayout layout,byte[] pixels)
    {
        internal readonly StoredRasterLayout Layout=layout;
        internal readonly byte[] Pixels=pixels;
        internal readonly object Gate=new();
        internal int References=1;
    }
    private readonly object _gate=new();
    private State? _state;
    internal FrozenStoredRaster(StoredRasterLayout layout,ReadOnlySpan<byte> pixels)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if(pixels.Length!=layout.BufferLength)throw new ArgumentException("Raster bytes differ from the layout.",nameof(pixels));
        _state=new State(layout,pixels.ToArray());
    }
    public StoredRasterLayout Layout { get { lock(_gate)return (_state??throw new ObjectDisposedException(nameof(FrozenStoredRaster))).Layout; } }
    public IStoredRasterReader AcquireReader()
    {
        lock(_gate)
        {
            var state=_state??throw new ObjectDisposedException(nameof(FrozenStoredRaster));
            lock(state.Gate)checked{state.References++;}
            return new Reader(state);
        }
    }
    public void Dispose()
    {
        State? state;lock(_gate){state=_state;_state=null;}
        if(state is not null)lock(state.Gate)state.References--;
    }
    private sealed class Reader(State state):IStoredRasterReader
    {
        private readonly object _gate=new();
        private State? _state=state;
        public StoredRasterLayout Layout { get { lock(_gate)return (_state??throw new ObjectDisposedException(nameof(Reader))).Layout; } }
        public ReadOnlySpan<byte> GetRow(int row)
        {
            lock(_gate)
            {
                var current=_state??throw new ObjectDisposedException(nameof(Reader));
                if(row<0||row>=current.Layout.Height)throw new ArgumentOutOfRangeException(nameof(row));
                return current.Pixels.AsSpan(checked(row*current.Layout.RowStrideBytes),current.Layout.RowStrideBytes);
            }
        }
        public void Dispose()
        {
            State? current;lock(_gate){current=_state;_state=null;}
            if(current is not null)lock(current.Gate)current.References--;
        }
    }
}
