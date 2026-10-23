using SunEyeVision.Plugin.Contracts;
namespace SunEyeVision.Plugin.Foundation.Host;
/// <summary>Gray8 foundation implementation. Production native/zero-copy adapters are not part of this sample.</summary>
internal sealed class ImageState
{
    private byte[]? _pixels; private int _references=1;
    public static int LiveBuffers;
    public int Width {get;} public int Height {get;}
    public ImageState(int width,int height,ReadOnlySpan<byte> pixels)
    {
        if(width<=0||height<=0||checked(width*height)!=pixels.Length)throw new ArgumentException("Invalid Gray8 dimensions");
        Width=width;Height=height;_pixels=pixels.ToArray();Interlocked.Increment(ref LiveBuffers);
    }
    public void Retain(){lock(this){if(_pixels is null)throw new ObjectDisposedException("image");_references++;}}
    public byte[] Copy(){lock(this)return _pixels?.ToArray()??throw new ObjectDisposedException("image");}
    public void Release(){lock(this){if(_references<=0)throw new InvalidOperationException("Double image release");if(--_references==0){_pixels=null;Interlocked.Decrement(ref LiveBuffers);}}}
}
internal sealed class ImageView(ImageState state):IReadOnlyImage
{
    internal ImageState State=>state;
    public int Width=>state.Width;public int Height=>state.Height;public byte[] CopyPixels()=>state.Copy();
}
public sealed class ImageLease:IDisposable
{
    private ImageState? _state;
    internal ImageLease(ImageState state){_state=state;View=new ImageView(state);}
    public IReadOnlyImage View {get;}
    public static int LiveBuffers=>Volatile.Read(ref ImageState.LiveBuffers);
    public static ImageLease CreateGray8(int width,int height,ReadOnlySpan<byte> pixels)=>new(new ImageState(width,height,pixels));
    public void Dispose()=>Interlocked.Exchange(ref _state,null)?.Release();
    internal static ImageLease Borrow(IReadOnlyImage image)
    {
        if(image is not ImageView view)throw new ArgumentException("Foundation host requires its registered Gray8 image adapter.");
        view.State.Retain();return new(view.State);
    }
}
internal sealed class OutputResources:IOutputResources,IDisposable
{
    private readonly HashSet<IReadOnlyImage> _inputs;
    private readonly Dictionary<IReadOnlyImage,ImageState> _owned=new(ReferenceEqualityComparer.Instance);
    public OutputResources(IEnumerable<IReadOnlyImage> inputs)=>_inputs=new(inputs,ReferenceEqualityComparer.Instance);
    public IReadOnlyImage CreateGray8(int width,int height,ReadOnlySpan<byte> pixels)
    {var state=new ImageState(width,height,pixels);var view=new ImageView(state);_owned.Add(view,state);return view;}
    public IReadOnlyImage RetainInput(IReadOnlyImage input)
    {
        if(!_inputs.Contains(input)||input is not ImageView view)throw new ArgumentException("Not a borrowed input of this invocation");
        view.State.Retain();var retained=new ImageView(view.State);_owned.Add(retained,view.State);return retained;
    }
    public List<ImageLease> Transfer(IEnumerable<IReadOnlyImage> outputs)
    {
        var images=outputs.Distinct(ReferenceEqualityComparer.Instance).Cast<IReadOnlyImage>().ToArray();
        foreach(var image in images)if(!_owned.ContainsKey(image))throw new InvalidOperationException("Output image must be created by this scope or explicitly retained");
        var leases=new List<ImageLease>();foreach(var image in images){leases.Add(new(_owned[image]));_owned.Remove(image);}return leases;
    }
    public void Dispose(){foreach(var state in _owned.Values)state.Release();_owned.Clear();}
}
