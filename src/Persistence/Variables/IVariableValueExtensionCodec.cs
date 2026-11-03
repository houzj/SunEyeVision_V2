using System.Text.Json;

namespace SunEyeVision.Persistence.Variables;

/// <summary>Host-specific persistence for complex values. The JSON element is borrowed only during Read.</summary>
public interface IVariableValueExtensionCodec
{
    bool SupportsType(string typeName);
    object? Read(string typeName, JsonElement value, JsonSerializerOptions options);
    bool TryWrite(object value, JsonSerializerOptions options, out string typeName, out object? serializedValue);
}


