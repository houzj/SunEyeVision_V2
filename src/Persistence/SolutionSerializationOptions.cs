using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Persistence.Variables;

namespace SunEyeVision.Persistence;

/// <summary>Configure the one variable value property without replacing the host's existing JSON settings.</summary>
public static class SolutionSerializationOptions
{
    public static JsonSerializerOptions Configure(JsonSerializerOptions source, IVariableValueExtensionCodec? extension = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var resolver = (source.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(info =>
        {
            if (info.Type != typeof(GlobalVariable)) return;
            var value = info.Properties.Single(property =>
                property.AttributeProvider is System.Reflection.PropertyInfo member
                    ? member.Name == nameof(GlobalVariable.Value)
                    : property.Name == nameof(GlobalVariable.Value));
            value.CustomConverter = new GlobalVariableValueConverter(extension);
        });
        return new JsonSerializerOptions(source) { TypeInfoResolver = resolver };
    }
}

