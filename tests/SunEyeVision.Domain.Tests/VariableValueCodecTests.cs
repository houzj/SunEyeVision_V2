using System.Text.Json;
using SunEyeVision.Persistence.Variables;
using Xunit;

public sealed class VariableValueCodecTests
{
    private static JsonSerializerOptions Options(IVariableValueExtensionCodec? codec = null)
    { var result = new JsonSerializerOptions(); result.Converters.Add(new GlobalVariableValueConverter(codec)); return result; }
    [Fact] public void Pure_value_scalar_and_array_shapes_roundtrip()
    {
        foreach(var value in new object[] { 7L, 2.5, "汉字<&", true, new long[]{1,2}, new double[]{1.25,2.5}, new[]{"一","二"}, new[]{true,false} })
        {
            var json = JsonSerializer.Serialize(value,typeof(object),Options());
            var result = JsonSerializer.Deserialize<object>(json,Options());
            Assert.Equal(value.GetType(),result!.GetType());
            Assert.Equal(value,result);
        }
    }
    [Fact] public void Missing_extension_and_invalid_wrapper_are_rejected()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<object>("{\"Type\":\"Image\",\"Value\":\"AAAA\"}",Options()));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<object>("{\"Type\":\"Integer\",\"Value\":1,\"Extra\":true}",Options()));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize((object)DateTime.UtcNow,typeof(object),Options()));
    }
}
