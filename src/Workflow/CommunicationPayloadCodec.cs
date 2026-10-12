using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SunEyeVision.Workflow;

/// <summary>Text/hex conversion shared by communication tooling without UI dependencies.</summary>
public static class CommunicationPayloadCodec
{
    public static byte[] ParseHex(string value) => SunEyeVision.Plugin.SDK.Models.Communication.ProtocolValueCodec.ParseHex(value);

    public static string FormatHex(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return string.Empty;
        return string.Join(" ", bytes.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
