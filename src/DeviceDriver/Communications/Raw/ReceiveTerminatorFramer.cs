using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SunEyeVision.DeviceDriver.Communications;

/// <summary>Buffers a single stream peer until its configured terminator completes a message.</summary>
internal sealed class ReceiveTerminatorFramer
{
    private const int MaxBufferedBytes = 1024 * 1024;
    private readonly byte[] _terminator;
    private readonly List<byte> _buffer = new();

    public ReceiveTerminatorFramer(byte[] terminator)
    {
        if (terminator.Length == 0) throw new ArgumentException("接收结束符不能为空。", nameof(terminator));
        _terminator = terminator;
    }

    public static byte[]? GetTerminatorBytes(CommunicationDeviceSnapshot configuration)
    {
        if (!configuration.EnableReceiveTerminator) return null;
        var text = configuration.ReceiveTerminator
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal);
        if (text.Length == 0) throw new ArgumentException("启用接收结束符时必须填写内容。", nameof(configuration));
        return Encoding.UTF8.GetBytes(text);
    }

    public IReadOnlyList<byte[]> Append(ReadOnlySpan<byte> chunk)
    {
        var messages = new List<byte[]>();
        foreach (var value in chunk)
        {
            _buffer.Add(value);
            if (_buffer.Count > MaxBufferedBytes)
            {
                _buffer.Clear();
                throw new InvalidDataException($"接收报文超过 {MaxBufferedBytes} 字节且未遇到结束符。");
            }
            if (!EndsWithTerminator()) continue;
            messages.Add(_buffer.GetRange(0, _buffer.Count - _terminator.Length).ToArray());
            _buffer.Clear();
        }
        return messages;
    }

    private bool EndsWithTerminator()
    {
        if (_buffer.Count < _terminator.Length) return false;
        for (var index = 0; index < _terminator.Length; index++)
            if (_buffer[_buffer.Count - _terminator.Length + index] != _terminator[index]) return false;
        return true;
    }
}
