using System;

namespace SunEyeVision.Plugin.SDK.Models.Communication;

/// <summary>
/// One immutable protocol payload, not a tuple of numeric Byte values.
/// The workflow may carry a tuple of payloads; each payload retains its byte boundaries.
/// </summary>
public sealed class BinaryPayload
{
    private readonly byte[] _bytes;

    public BinaryPayload(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        _bytes = (byte[])bytes.Clone();
    }

    public int Length => _bytes.Length;

    /// <summary>Returns an owned copy so consumers cannot mutate another node's snapshot.</summary>
    public byte[] ToArray() => (byte[])_bytes.Clone();

    public string ToBase64() => Convert.ToBase64String(_bytes);

    public override string ToString() => Convert.ToHexString(_bytes);
}
