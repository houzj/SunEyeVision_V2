using System.Text.Json;

namespace SunEyeVision.Persistence.Variables;

/// <summary>Host-specific persistence for complex values. The JSON element is borrowed only during Read.</summary>
public interface IVariableValueExtensionCodec
{
    SunEyeVision.Persistence.DecodedResourcesScope Scope { get; }
    bool SupportsType(string typeName);
    object? Read(string typeName, JsonElement value, JsonSerializerOptions options);
    bool TryGetToken(object value, out string typeName);
    void WriteValue(Utf8JsonWriter writer, string typeName, object value, JsonSerializerOptions options);
}


