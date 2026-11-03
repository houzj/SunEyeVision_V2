using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Acquisition;
using SunEyeVision.Plugin.Runtime;
namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

/// <summary>Owns one independently frozen device frame; independently retained readers survive disposal.</summary>
internal sealed class CapturedCameraFrame : ICameraFrameLease
{
    private FrozenImageResource? _image;
    internal CapturedCameraFrame(string deviceId,long frameId,DateTimeOffset timestamp,ImageLayout layout,
        ReadOnlySpan<byte> pixels,ReadOnlySpan<RegionRun> domain,RunIdentity? producerRun)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        DeviceId=deviceId;DeviceFrameId=frameId;Timestamp=timestamp;
        var address=new ImageFrameAddress(Guid.NewGuid(),"camera:"+deviceId,producerRun);
        _image=new FrozenImageResource(layout,address,new CoordinateFrame("camera:"+deviceId,CoordinateDomain.Image,CoordinateUnit.Pixel),pixels,domain);
    }
    public string DeviceId {get;}
    public long DeviceFrameId {get;}
    public DateTimeOffset Timestamp {get;}
    public IReadOnlyImage Image=>_image??throw new ObjectDisposedException(nameof(CapturedCameraFrame));
    public ValueTask DisposeAsync(){Interlocked.Exchange(ref _image,null)?.Dispose();return ValueTask.CompletedTask;}
}
