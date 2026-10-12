using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SunEyeVision.Plugin.SDK.Models.Communication;

/// <summary>Pure protocol conversions shared by receive events and workflow tools.</summary>
public static class ProtocolValueCodec
{
    public static Encoding EncodingFor(string name) => name switch
    {
        "UTF8" => new UTF8Encoding(false, true),
        "ASCII" => Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
        _ => throw new FormatException("字符编码必须为UTF8或ASCII。")
    };

    public static byte[] ParseHex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new List<byte>();
        var groups = value.Split([' ', '\t', '\r', '\n', ',', ';', '-', ':'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var (group, index) in groups.Select((g, i) => (g, i)))
        {
            var text = group.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? group[2..] : group;
            if (text.Length == 0) continue;
            if (text.Length % 2 != 0) throw new FormatException($"第 {index + 1} 组 HEX 数据位数不完整。每个字节需要两个十六进制字符。");
            for (var offset = 0; offset < text.Length; offset += 2)
            {
                if (!byte.TryParse(text.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                    throw new FormatException($"第 {index + 1} 组包含无效的 HEX 字符：{group}");
                result.Add(b);
            }
        }
        if (result.Count == 0) throw new FormatException("请输入 HEX 字节，例如：01 03 00 00。");
        return result.ToArray();
    }

    public static object ConvertText(string text, string type) => type.ToLowerInvariant() switch
    {
        "string" => text,
        "int" or "int32" when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
        "float" or "float32" when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && float.IsFinite(f) => f,
        _ => throw new FormatException($"字段无法按{type}转换：{text}")
    };

    public static byte[] Slice(byte[] source, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (start < 0 || end < start || end >= source.Length)
            throw new FormatException($"起止位置越界，输入{source.Length}字节。");
        return source[start..(end + 1)];
    }

    public static object Decode(byte[] source, int start, int end, string type, string order = "ABCD",
        string numericInterpretation = "Auto", string encoding = "UTF8", bool ascii = false)
    {
        var bytes = Slice(source, start, end);
        type = type.ToLowerInvariant();
        if (type == "binary") return new BinaryPayload(bytes);
        if (ascii) return ConvertText(EncodingFor("ASCII").GetString(bytes), type);
        if (type == "string") return EncodingFor(encoding).GetString(bytes);
        if (type is not ("int" or "float")) throw new FormatException("字节字段类型必须为int、float、string或Binary。");
        if (type == "int" && bytes.Length is < 1 or > 4) throw new FormatException("int需要1至4字节。");
        if (type == "float" && bytes.Length != 4) throw new FormatException("float必须为4字节。");
        switch (order)
        {
            case "ABCD": break;
            case "BADC" when bytes.Length is 2 or 4:
                for (var i = 0; i < bytes.Length; i += 2) (bytes[i], bytes[i + 1]) = (bytes[i + 1], bytes[i]); break;
            case "DCBA" when bytes.Length is 2 or 4: Array.Reverse(bytes); break;
            case "CDAB" when bytes.Length == 4: (bytes[0], bytes[1], bytes[2], bytes[3]) = (bytes[2], bytes[3], bytes[0], bytes[1]); break;
            default: throw new FormatException("顺序与字节长度不兼容。");
        }
        long number = 0;
        foreach (var b in bytes) number = (number << 8) | b;
        if (type == "float")
        {
            var value = BitConverter.Int32BitsToSingle(unchecked((int)number));
            return float.IsFinite(value) ? value : throw new FormatException("float结果不是有限值。");
        }
        var signed = numericInterpretation switch
        {
            "Auto" => bytes.Length == 4,
            "Signed" => true,
            "Unsigned" => false,
            _ => throw new FormatException("整数解释必须为Auto、Signed或Unsigned。")
        };
        if (signed && (bytes[0] & 0x80) != 0) number -= 1L << (bytes.Length * 8);
        if (number < int.MinValue || number > int.MaxValue) throw new FormatException("结果超出配置的int32范围。");
        return (int)number;
    }
}
