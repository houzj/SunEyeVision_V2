using System;
using SunEyeVision.Plugin.SDK.Models.Communication;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class ProtocolValueCodecTests
{
    [Theory]
    [InlineData("01 02", "0102")]
    [InlineData("0x01,0x02", "0102")]
    [InlineData("01:02;03-04", "01020304")]
    public void Hex_forms_preserve_real_bytes(string input, string expected)
        => Assert.Equal(expected, Convert.ToHexString(ProtocolValueCodec.ParseHex(input)));

    [Theory]
    [InlineData("", "HEX")]
    [InlineData("0", "位数")]
    [InlineData("GG", "无效")]
    public void Invalid_hex_has_no_partial_result(string input, string reason)
        => Assert.Contains(reason, Assert.Throws<FormatException>(() => ProtocolValueCodec.ParseHex(input)).Message);

    [Theory]
    [InlineData("FF", 0, 255)]
    [InlineData("FFFF", 1, 65535)]
    [InlineData("FFFFFF", 2, 16777215)]
    [InlineData("FFFFFFFF", 3, -1)]
    public void Default_integer_matches_receive_event_width_rule(string input, int end, int expected)
        => Assert.Equal(expected, ProtocolValueCodec.Decode(Convert.FromHexString(input), 0, end, "int"));

    [Fact]
    public void Overrides_and_non_finite_values_fail_without_mutating_source()
    {
        var source = Convert.FromHexString("FFFF");
        Assert.Equal(-1, ProtocolValueCodec.Decode(source, 0, 1, "int", numericInterpretation: "Signed"));
        Assert.Equal(65535, ProtocolValueCodec.Decode(source, 0, 1, "int", numericInterpretation: "Unsigned"));
        Assert.Throws<FormatException>(() => ProtocolValueCodec.Decode(Convert.FromHexString("FFFFFFFF"), 0, 3, "int", numericInterpretation: "Unsigned"));
        Assert.Throws<FormatException>(() => ProtocolValueCodec.Decode(Convert.FromHexString("7F800000"), 0, 3, "float"));
        Assert.Equal("FFFF", Convert.ToHexString(source));
    }

    [Theory]
    [InlineData("3F800000", "ABCD")]
    [InlineData("803F0000", "BADC")]
    [InlineData("00003F80", "CDAB")]
    [InlineData("0000803F", "DCBA")]
    public void Reorder_is_local_and_float_stays_native(string input, string order)
    {
        var source = Convert.FromHexString(input);
        Assert.Equal(1f, ProtocolValueCodec.Decode(source, 0, 3, "float", order));
        Assert.Equal(input, Convert.ToHexString(source));
    }

    [Fact]
    public void Binary_fields_are_owned_payloads_and_do_not_decode_text()
    {
        var bytes = Convert.FromHexString("00FF");
        var payload = Assert.IsType<BinaryPayload>(ProtocolValueCodec.Decode(bytes, 0, 1, "Binary"));
        bytes[0] = 42;
        Assert.Equal("00FF", payload.ToString());
        Assert.Throws<FormatException>(() => ProtocolValueCodec.Decode(bytes, 0, 2, "Binary"));
    }

    [Fact]
    public void Ascii_conversion_and_binary_interpretation_are_distinct()
    {
        var bytes = Convert.FromHexString("3132");
        Assert.Equal(12, ProtocolValueCodec.Decode(bytes, 0, 1, "int", ascii: true));
        Assert.Equal(12594, ProtocolValueCodec.Decode(bytes, 0, 1, "int"));
        Assert.Throws<FormatException>(() => ProtocolValueCodec.ConvertText("NaN", "float"));
    }
}
