namespace SunEyeVision.Plugin.Contracts;

/// <summary>An immutable, complete binary message. Both construction and reads copy bytes.</summary>
public sealed class BinaryValue
{
    private readonly byte[] _bytes;
    public BinaryValue(ReadOnlySpan<byte> bytes) => _bytes = bytes.ToArray();
    public int Length => _bytes.Length;
    public byte[] ToArray() => _bytes.ToArray();
    public string ToBase64() => Convert.ToBase64String(_bytes);
    public override string ToString() => Convert.ToHexString(_bytes);
}

public enum StoredRasterFormat { Gray8, Gray16, Bgr24, Bgr48, Bgra32, Bgra64 }

/// <summary>Layout of persistent pixels only; it carries no execution identity or domain.</summary>
public sealed record StoredRasterLayout
{
    public StoredRasterLayout(int width, int height, int rowStrideBytes, StoredRasterFormat format)
    {
        if (width <= 0 || height <= 0 || !Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(width));
        int bytesPerPixel = format switch { StoredRasterFormat.Gray8 => 1, StoredRasterFormat.Gray16 => 2,
            StoredRasterFormat.Bgr24 => 3, StoredRasterFormat.Bgr48 => 6, StoredRasterFormat.Bgra32 => 4, _ => 8 };
        if (rowStrideBytes < checked(width * bytesPerPixel) || (format is StoredRasterFormat.Gray16 or StoredRasterFormat.Bgr48 or StoredRasterFormat.Bgra64) && rowStrideBytes % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(rowStrideBytes));
        Width=width; Height=height; RowStrideBytes=rowStrideBytes; Format=format; BufferLength=checked(rowStrideBytes*height);
    }
    public int Width { get; }
    public int Height { get; }
    public int RowStrideBytes { get; }
    public StoredRasterFormat Format { get; }
    public int BufferLength { get; }
}

public interface IReadOnlyStoredRaster
{
    StoredRasterLayout Layout { get; }
    IStoredRasterReader AcquireReader();
}

public interface IStoredRasterReader : IDisposable
{
    StoredRasterLayout Layout { get; }
    ReadOnlySpan<byte> GetRow(int row);
}

