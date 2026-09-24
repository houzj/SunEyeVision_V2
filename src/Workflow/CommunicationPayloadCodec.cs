using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SunEyeVision.Workflow;

/// <summary>Text/hex conversion shared by communication tooling without UI dependencies.</summary>
public static class CommunicationPayloadCodec
{
    private static readonly char[] HexSeparators = [' ', '\t', '\r', '\n', ',', ';', '-', ':'];

    public static byte[] ParseHex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new List<byte>();
        var groups = value.Split(HexSeparators, StringSplitOptions.RemoveEmptyEntries);

        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            var group = groups[groupIndex];
            if (group.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                group = group[2..];
            if (group.Length == 0)
                continue;
            if ((group.Length & 1) != 0)
                throw new FormatException($"第 {groupIndex + 1} 组 HEX 数据位数不完整。每个字节需要两个十六进制字符。");

            for (var index = 0; index < group.Length; index += 2)
            {
                if (!byte.TryParse(group.AsSpan(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
                    throw new FormatException($"第 {groupIndex + 1} 组包含无效的 HEX 字符：{group}");
                result.Add(parsed);
            }
        }

        if (result.Count == 0)
            throw new FormatException("请输入要发送的 HEX 字节，例如：01 03 00 00。");
        return result.ToArray();
    }

    public static string FormatHex(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return string.Empty;
        return string.Join(" ", bytes.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
