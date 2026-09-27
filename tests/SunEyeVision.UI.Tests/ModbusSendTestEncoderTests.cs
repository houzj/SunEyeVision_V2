using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ModbusSendTestEncoderTests
{
    [Fact]
    public void Int16_encodes_one_value_per_register()
    {
        var point = new ModbusAddressPoint { Count = 2, IntBits = "16bit" };

        Assert.Equal(new ushort[] { 0x0001, 0xFFFF },
            ModbusSendTestEncoder.Encode(point, "int", "1;-1"));
    }

    [Theory]
    [InlineData("ABCD", 0x0102, 0x0304)]
    [InlineData("BADC", 0x0201, 0x0403)]
    [InlineData("CDAB", 0x0304, 0x0102)]
    [InlineData("DCBA", 0x0403, 0x0201)]
    public void Int32_applies_configured_byte_order(string order, ushort first, ushort second)
    {
        var point = new ModbusAddressPoint { Count = 2, IntBits = "32bit", ByteOrder = order };

        Assert.Equal(new[] { first, second },
            ModbusSendTestEncoder.Encode(point, "int", "16909060"));
    }

    [Fact]
    public void Float32_uses_two_registers()
    {
        var point = new ModbusAddressPoint { Count = 2, IntBits = "32bit" };

        Assert.Equal(new ushort[] { 0x3F80, 0x0000 },
            ModbusSendTestEncoder.Encode(point, "float", "1.0"));
    }

    [Fact]
    public void String_writes_only_used_registers_and_truncates_at_capacity()
    {
        var point = new ModbusAddressPoint { Count = 2 };

        Assert.Equal(new ushort[] { 0x4142 },
            ModbusSendTestEncoder.Encode(point, "string", "AB"));
        var capped = ModbusSendTestEncoder.EncodeDetailed(point, "string", "ABCDE");
        Assert.Equal(new ushort[] { 0x4142, 0x4344 }, capped.Registers);
        Assert.Contains("忽略末尾", capped.TruncationMessage);
    }

    [Fact]
    public void Short_integer_input_leaves_unwritten_registers_untouched()
    {
        var point = new ModbusAddressPoint { Count = 20, IntBits = "16bit" };

        var payload = ModbusSendTestEncoder.EncodeDetailed(point, "int", "1;2;3;4;5;6;7;8;9");

        Assert.Equal(new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, payload.Registers);
        Assert.Empty(payload.TruncationMessage);
    }

    [Fact]
    public void Extra_values_are_capped_to_configured_register_count()
    {
        var point = new ModbusAddressPoint { Count = 2, IntBits = "16bit" };

        var payload = ModbusSendTestEncoder.EncodeDetailed(point, "int", "1;2;3;4");

        Assert.Equal(new ushort[] { 1, 2 }, payload.Registers);
        Assert.Contains("2 个整数", payload.TruncationMessage);
    }

    [Fact]
    public void Utf8_truncation_does_not_split_character()
    {
        var point = new ModbusAddressPoint { Count = 1 };

        var payload = ModbusSendTestEncoder.EncodeDetailed(point, "string", "A中B");

        Assert.Equal(new ushort[] { 0x4100 }, payload.Registers);
        Assert.Contains("4 个 UTF-8 字节", payload.TruncationMessage);
    }

    [Fact]
    public void Readonly_is_rejected_and_32bit_input_uses_only_complete_values()
    {
        var readonlyPoint = new ModbusAddressPoint { Count = 1, AccessMode = "只读" };
        Assert.Throws<InvalidOperationException>(() =>
            ModbusSendTestEncoder.Encode(readonlyPoint, "int", "1"));

        var point = new ModbusAddressPoint { Count = 2, IntBits = "32bit" };
        Assert.Equal(new ushort[] { 0x0000, 0x0001 },
            ModbusSendTestEncoder.Encode(point, "int", "1;2"));
    }
}
