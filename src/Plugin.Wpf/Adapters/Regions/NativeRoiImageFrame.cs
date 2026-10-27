using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>Private tight-row display buffer and a retained reader of the actual frozen frame.</summary>
internal sealed class NativeRoiImageFrame : IImageData,IDisposable
{
    private IImagePixelReadLease? _lease;
    private byte[] _buffer;
    public ImageFrameAddress Frame {get;}
    public int Width {get;}
    public int Height {get;}
    public string MemoryAddress=>"";
    public string PixelFormat {get;}
    public byte[] Buffer {get=>_lease is not null?_buffer:throw new ObjectDisposedException(nameof(NativeRoiImageFrame));set=>throw new InvalidOperationException("显示帧不可替换像素。");}
    internal NativeRoiImageFrame(IReadOnlyImage image)
    {
        ResourceContractRules.ValidateImage(image);Frame=image.Frame;Width=image.Width;Height=image.Height;
        PixelFormat=image.Layout.Format.ToString();var bpp=image.Layout.Format switch{PixelLayout.Gray8=>1,PixelLayout.Bgr24=>3,_=>4};
        var stride=checked(Width*bpp);_buffer=new byte[checked(stride*Height)];
        var lease=image.AcquirePixels();
        try
        {
            if(lease.Frame!=Frame||lease.Layout!=image.Layout)throw new ArgumentException("显示读取租约与冻结帧不一致。");
            for(var row=0;row<Height;row++)lease.GetRow(row)[..stride].CopyTo(_buffer.AsSpan(row*stride,stride));
            _lease=lease;
        }
        catch{lease.Dispose();throw;}
    }
    public void Dispose(){var lease=_lease;_lease=null;_buffer=Array.Empty<byte>();lease?.Dispose();}
}
