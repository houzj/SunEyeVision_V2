using System.Text.Json;
using OpenCvSharp;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Persistence.Variables;
using SunEyeVision.Persistence;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Communication;
using Xunit;

public sealed class VariableCodecIntegrationTests
{
    private static JsonSerializerOptions Options() { var options=new JsonSerializerOptions();options.Converters.Add(new GlobalVariableValueConverter(new SdkVariableValueExtensionCodec()));return options; }
    [Fact] public void Current_v3_wrapper_bytes_match_original_converter_for_actual_values()
    {
        var expected=File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"variable-values-v3.golden.txt"));
        using var image=new Mat(2,3,MatType.CV_8UC1,Scalar.All(23));
        object[] values={7L,2.5,"汉字<&",true,new long[]{1,2},new double[]{1.25,2.5},new BinaryPayload(new byte[]{1,2,3}),new AffineMatrix(1,2,3,4,5,6),new RoiShape(),image};
        Assert.Equal(expected,values.Select(value => JsonSerializer.Serialize(value,typeof(object),Options())).ToArray());
    }
    [Fact] public void Binary_and_matrix_use_real_sdk_types()
    {
        var binary=new BinaryPayload(new byte[]{1,2,3});
        var json=JsonSerializer.Serialize((object)binary,Options());
        Assert.Equal(binary.ToBase64(),Assert.IsType<BinaryPayload>(JsonSerializer.Deserialize<object>(json,Options())).ToBase64());
        var matrix=new AffineMatrix(1,2,3,4,5,6);
        json=JsonSerializer.Serialize((object)matrix,Options());
        var actual=Assert.IsType<AffineMatrix>(JsonSerializer.Deserialize<object>(json,Options()));
        Assert.Equal(matrix.M11,actual.M11);Assert.Equal(matrix.OffsetY,actual.OffsetY);
    }
    [Fact] public void Real_variable_property_uses_extension_without_hijacking_other_object_properties()
    {
        var options=SolutionSerializationOptions.Configure(new JsonSerializerOptions(),new SdkVariableValueExtensionCodec());
        var variable=new GlobalVariable("图片",new Mat(2,3,MatType.CV_8UC1,Scalar.All(23)),"Image");
        using var original=Assert.IsType<Mat>(variable.Value);
        var json=JsonSerializer.Serialize(variable,options);
        var restored=JsonSerializer.Deserialize<GlobalVariable>(json,options)!;
        using var loaded=Assert.IsType<Mat>(restored.Value);
        Assert.Equal((byte)23,loaded.At<byte>(0,0));
        var communication=new Communication { Name="设备",Settings=new(){["port"]=123L} };
        json=JsonSerializer.Serialize(communication,options);
        Assert.Contains("\"port\":123",json);
    }
    [Fact] public void Invalid_resources_and_unsupported_carriers_fail_explicitly()
    {
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<object>("{\"Type\":\"Image\",\"Value\":\"!\"}",Options()));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<object>("{\"Type\":\"Transform\",\"Value\":{}}",Options()));
        using var empty=new Mat();
        Assert.Throws<JsonException>(()=>JsonSerializer.Serialize((object)empty,Options()));
    }
    [Fact] public void Roi_shape_preserves_real_sdk_type()
    {
        var json=JsonSerializer.Serialize((object)new RoiShape(),Options());
        var loaded=Assert.IsType<RoiShape[]>(JsonSerializer.Deserialize<object>(json,Options()));
        Assert.Single(loaded);
    }
    [Fact] public void Image_roundtrip_owns_a_new_real_mat()
    {
        using var image=new Mat(2,3,MatType.CV_8UC1,Scalar.All(23));
        var json=JsonSerializer.Serialize((object)image,Options());
        using var loaded=Assert.IsType<Mat>(JsonSerializer.Deserialize<object>(json,Options()));
        Assert.NotSame(image,loaded);Assert.Equal(2,loaded.Rows);Assert.Equal(3,loaded.Cols);Assert.Equal((byte)23,loaded.At<byte>(0,0));
    }
}







