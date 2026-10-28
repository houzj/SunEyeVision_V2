using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>Immutable declared values and one transferred child resource owner.</summary>
public sealed class ImageSourceOutput : IToolOutput,IDisposable
{
    public const string Image="output.image",GrayImage="output.gray-image",Width="output.width",Height="output.height",
        PixelFormat="output.pixel-format",SourceKind="output.source-kind",DeviceId="output.device",FileReference="output.file",
        Sequence="output.sequence",FrameId="output.device-frame-id",Timestamp="output.device-timestamp";
    public static IReadOnlyList<Port> Ports {get;}=Array.AsReadOnly(new[] {
        new Port(Image,TypeRef.ResourceOf(ResourceKind.Image)),new Port(GrayImage,TypeRef.ResourceOf(ResourceKind.Image)),
        new Port(Width,TypeRef.ScalarOf(ScalarKind.Int64)),new Port(Height,TypeRef.ScalarOf(ScalarKind.Int64)),
        new Port(PixelFormat,TypeRef.ScalarOf(ScalarKind.Text)),new Port(SourceKind,TypeRef.ScalarOf(ScalarKind.Text)),
        new Port(DeviceId,TypeRef.ScalarOf(ScalarKind.Text)),new Port(FileReference,TypeRef.ScalarOf(ScalarKind.Text)),
        new Port(Sequence,TypeRef.ScalarOf(ScalarKind.Int64)),new Port(FrameId,TypeRef.ScalarOf(ScalarKind.Text)),
        new Port(Timestamp,TypeRef.ScalarOf(ScalarKind.Text))
    });
    private readonly InputValues _values;
    private IOutputResourceScope? _owned;
    internal ImageSourceOutput(IReadOnlyDictionary<string,object?> values,IOutputResourceScope owned)
    {
        // Caller retains responsibility until construction succeeds.
        _values=new(Ports,values);_owned=owned;
    }
    public IReadOnlyDictionary<string,object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _owned) is null,this);
        return _values.CopyValues();
    }
    public void Dispose()=>Interlocked.Exchange(ref _owned,null)?.Dispose();
}
