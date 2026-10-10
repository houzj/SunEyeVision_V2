using System;
using System.Collections.Generic;

namespace SunEyeVision.Workflow;

/// <summary>An immutable, session-scoped upload, never a shared latest-value variable.</summary>
public sealed class CommunicationUploadSnapshot
{
    public Guid SessionId { get; }
    public string DeviceId { get; }
    public string? TargetId { get; }
    public DateTimeOffset Timestamp { get; }
    public string DataType { get; }
    public object Value { get; }
    public bool IsGood { get; }
    public IReadOnlyList<byte>? RawData { get; }
    public CommunicationUploadSnapshot(Guid sessionId, string deviceId, string? targetId,
        DateTimeOffset timestamp, string dataType, object value, bool isGood = true, byte[]? rawData = null)
    {
        SessionId = sessionId; DeviceId = deviceId; TargetId = targetId;
        Timestamp = timestamp; DataType = dataType; IsGood = isGood;
        Value = value switch
        {
            byte[] bytes => Array.AsReadOnly((byte[])bytes.Clone()),
            ushort[] words => Array.AsReadOnly((ushort[])words.Clone()),
            string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double => value,
            _ => throw new ArgumentException("上传值必须为受支持标量或原始字节／寄存器数组。", nameof(value))
        };
        RawData = rawData == null ? null : Array.AsReadOnly((byte[])rawData.Clone());
    }
}
