using System.Text.Json;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Plugin.Contracts;
using Xunit;

public sealed class SolutionDataTests
{
    [Theory]
    [InlineData("Integer",ValueShape.Scalar,ScalarKind.Int64,NumericStorage.Int64)]
    [InlineData("Integer[]",ValueShape.Sequence,ScalarKind.Int64,NumericStorage.Int64)]
    [InlineData("integer[]",ValueShape.Sequence,ScalarKind.Int64,NumericStorage.Int32)]
    [InlineData("Double",ValueShape.Scalar,ScalarKind.Float64,NumericStorage.Float64)]
    [InlineData("Float[]",ValueShape.Sequence,ScalarKind.Float64,NumericStorage.Float32)]
    public void Numeric_declarations_preserve_existing_storage(string name,ValueShape shape,ScalarKind kind,NumericStorage storage)
    {
        Assert.True(VariableValueDeclarations.TryGetPort(name,out var port));
        Assert.Equal(shape,port.Type.Shape);
        Assert.Equal(kind,(shape == ValueShape.Sequence ? port.Type.Element! : port.Type).Scalar);
        Assert.Equal(storage,port.Storage);
    }
    [Fact]
    public void Definition_does_not_persist_runtime_state()
    {
        var device = new Communication { Name="设备", IsEnabled=true, ConnectionType=CommunicationType.TCP };
        var json=JsonSerializer.Serialize(device);
        Assert.DoesNotContain("RuntimeStatus",json);
        Assert.DoesNotContain("RuntimeConnection",json);
        var point = new ModbusAddressPoint { Name="点位", StartAddress=2, Count=2 };
        json=JsonSerializer.Serialize(point);
        Assert.DoesNotContain("ReceiveRecords",json);
        Assert.DoesNotContain("CurrentValue",json);
        Assert.Equal(3,point.EndAddress);
    }
    [Fact]
    public void Variable_definition_contains_no_persisted_consumer_cache()
    {
        var variable = new GlobalVariable("变量",7L,"Integer");
        Assert.Null(typeof(GlobalVariable).GetProperty("ConsumerBindings"));
        Assert.Null(typeof(GlobalVariable).GetProperty("OutputTargetDisplayText"));
        Assert.DoesNotContain("ConsumerBindings",JsonSerializer.Serialize(variable));
    }
    [Fact]
    public void Variable_captures_revision_and_copies_primitive_arrays()
    {
        var variable=new GlobalVariable("变量",null,"Integer[]");
        var source=new long[]{1,2}; variable.Value=source;
        var snapshot=variable.CaptureValue(); source[0]=9;
        Assert.Equal(1,Assert.IsType<long[]>(snapshot.Value)[0]);
        Assert.Equal(1,snapshot.Revision);
        Assert.Equal(1,variable.ValueRevision);
    }
}

