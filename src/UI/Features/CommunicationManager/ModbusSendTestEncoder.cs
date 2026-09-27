using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

/// <summary>将发送测试的输入严格编码成已配置地址块的寄存器范围。</summary>
public static class ModbusSendTestEncoder
{
    public static ushort[] Encode(ModbusAddressPoint point, string dataType, string content) =>
        EncodeDetailed(point, dataType, content).Registers;

    public static ModbusSendPayload EncodeDetailed(ModbusAddressPoint point, string dataType, string content)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (point.AccessMode == "只读")
            throw new InvalidOperationException("所选地址块为只读，不能发送。");
        if (point.Count < 1 || point.StartAddress < 0 ||
            (long)point.StartAddress + point.Count - 1 > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(point), "地址块范围必须在 0 到 65535 之间。");
        if (string.IsNullOrEmpty(content))
            throw new FormatException("请输入发送数据内容。");

        var (bytes, dropped, unit) = dataType switch
        {
            "int" => EncodeIntegers(point, content),
            "float" => EncodeFloats(point, content),
            "string" => EncodeString(point, content),
            _ => throw new FormatException("请选择 int、float 或 string 数据类型。")
        };
        var registers = new ushort[bytes.Length / 2];
        for (var index = 0; index < registers.Length; index++)
            registers[index] = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(index * 2, 2));
        return new ModbusSendPayload(registers, dropped > 0
            ? $"已截取地址块容量，忽略末尾 {dropped} {unit}。"
            : string.Empty);
    }

    private static (byte[] Bytes, int Dropped, string Unit) EncodeIntegers(ModbusAddressPoint point, string content)
    {
        var width = point.IntBits == "32bit" ? 4 : 2;
        var tokens = SplitValues(content);
        var valueCount = GetValueCount(point, width, tokens.Length);
        var bytes = new byte[valueCount * width];
        for (var index = 0; index < valueCount; index++)
        {
            var destination = bytes.AsSpan(index * width, width);
            if (width == 2)
            {
                if (!short.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    throw new FormatException($"整数“{tokens[index]}”超出 16bit 有符号范围。");
                BinaryPrimitives.WriteInt16BigEndian(destination, value);
            }
            else
            {
                if (!int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    throw new FormatException($"整数“{tokens[index]}”超出 32bit 有符号范围。");
                BinaryPrimitives.WriteInt32BigEndian(destination, value);
            }
            ApplyByteOrder(destination, point.ByteOrder);
        }
        return (bytes, tokens.Length - valueCount, "个整数");
    }

    private static (byte[] Bytes, int Dropped, string Unit) EncodeFloats(ModbusAddressPoint point, string content)
    {
        if (point.IntBits != "32bit")
            throw new FormatException("float 需要将地址块的 Int 位数设为 32bit。");
        var tokens = SplitValues(content);
        var valueCount = GetValueCount(point, 4, tokens.Length);
        var bytes = new byte[valueCount * 4];
        for (var index = 0; index < valueCount; index++)
        {
            if (!float.TryParse(tokens[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                !float.IsFinite(value))
                throw new FormatException($"浮点数“{tokens[index]}”无效。请使用英文小数点。");
            var destination = bytes.AsSpan(index * 4, 4);
            BinaryPrimitives.WriteInt32BigEndian(destination, BitConverter.SingleToInt32Bits(value));
            ApplyByteOrder(destination, point.ByteOrder);
        }
        return (bytes, tokens.Length - valueCount, "个浮点数");
    }

    private static (byte[] Bytes, int Dropped, string Unit) EncodeString(ModbusAddressPoint point, string content)
    {
        var payload = Encoding.UTF8.GetBytes(content);
        var byteCount = Math.Min(payload.Length, point.Count * 2);
        // 容量边界不能截断 UTF-8 多字节字符。
        if (byteCount < payload.Length)
            while (byteCount > 0 && (payload[byteCount] & 0xC0) == 0x80)
                byteCount--;
        if (byteCount == 0)
            throw new FormatException("地址块不足以容纳第一个 UTF-8 字符。");
        var bytes = new byte[(byteCount + 1) / 2 * 2];
        Array.Copy(payload, bytes, byteCount);
        // 字符串按寄存器高字节在前传输，不套用 int/float 的 32bit 字节序。
        return (bytes, payload.Length - byteCount, "个 UTF-8 字节");
    }

    private static string[] SplitValues(string content) =>
        content.Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int GetValueCount(ModbusAddressPoint point, int width, int actual)
    {
        var capacity = point.Count * 2 / width;
        if (capacity == 0)
            throw new FormatException($"当前地址块不足以容纳一个完整的 {width * 8}bit 值。");
        if (actual == 0)
            throw new FormatException("请至少输入一个数值；多个值请用英文分号分隔。");
        return Math.Min(actual, capacity);
    }

    private static void ApplyByteOrder(Span<byte> bytes, string order)
    {
        if (bytes.Length == 2)
        {
            if (order is "BADC" or "DCBA")
                (bytes[0], bytes[1]) = (bytes[1], bytes[0]);
            return;
        }
        var canonical = bytes.ToArray();
        var pattern = order is "ABCD" or "BADC" or "CDAB" or "DCBA" ? order : "ABCD";
        for (var index = 0; index < 4; index++)
            bytes[index] = canonical[pattern[index] - 'A'];
    }
}

public readonly record struct ModbusSendPayload(ushort[] Registers, string TruncationMessage);
