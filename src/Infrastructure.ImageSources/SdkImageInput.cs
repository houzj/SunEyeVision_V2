using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Infrastructure.ImageSources;

/// <summary>One copied SDK input. Admission assigns its identity to the actual invocation.</summary>
public sealed class SdkImageInput
{
    private readonly byte[] _pixels;
    private readonly ImageLayout _layout;

    public SdkImageInput(ImageLayout layout,ReadOnlyMemory<byte> pixels)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if(layout.Format is not (PixelLayout.Gray8 or PixelLayout.Rgb24))
            throw new ArgumentException("SDK 内存供图支持 MONO8 和 RGB24。",nameof(layout));
        if(pixels.Length!=layout.BufferLength)
            throw new ArgumentException("SDK 像素长度必须包含声明的行填充。",nameof(pixels));
        _layout=layout;
        _pixels=pixels.ToArray();
    }

    internal FrozenImageResource Admit(RunIdentity run)
    {
        var frame=new ImageFrameAddress(Guid.NewGuid(),"sdk-image:"+run.NodeId,run,"input.sdk-image");
        var domain=Enumerable.Range(0,_layout.Height).Select(row=>new RegionRun(row,0,_layout.Width-1)).ToArray();
        return new FrozenImageResource(_layout,frame,RoiDeclarations.ImageFrame,_pixels,domain);
    }
}
