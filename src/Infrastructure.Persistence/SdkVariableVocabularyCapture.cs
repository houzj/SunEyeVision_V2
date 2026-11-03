using System.Text.Json;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Infrastructure.Persistence;

/// <summary>Freeze exactly the variable vocabulary needed by one encoded candidate without decoding values.</summary>
public static class SdkVariableVocabularyCapture
{
    public static FrozenVariableTypeCatalog FromCandidate(ReadOnlySpan<byte> utf8)
        => Capture(utf8, "GlobalVariables");

    public static FrozenVariableTypeCatalog FromVariableArchive(ReadOnlySpan<byte> utf8)
        => Capture(utf8, "Variables");

    private static FrozenVariableTypeCatalog Capture(ReadOnlySpan<byte> utf8, string propertyName)
    {
        using var document=JsonDocument.Parse(utf8.ToArray());
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(document.RootElement.TryGetProperty(propertyName,out var variables) && variables.ValueKind==JsonValueKind.Array)
            foreach(var item in variables.EnumerateArray())
            {
                if(item.ValueKind!=JsonValueKind.Object)throw new JsonException("Variable must be an object.");
                if(item.TryGetProperty("Type",out var type) && type.ValueKind==JsonValueKind.String) Add(type.GetString());
                if(item.TryGetProperty("Value",out var wrapper) && wrapper.ValueKind==JsonValueKind.Object && wrapper.TryGetProperty("Type",out var valueType) && valueType.ValueKind==JsonValueKind.String) Add(valueType.GetString());
            }
        var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var name in names)
        {
            // Explicit current scalar/sequence declarations are resolved by their element type.
            var probe=name is "Integer[]" or "Double[]" or "String[]" or "Boolean[]" ? name[..^2] : name;
            var type=SdkTypeVocabulary.ToSystemType(probe) ?? throw new JsonException("Unknown variable type: "+name);
            if(type.IsArray)type=type.GetElementType()!;
            if(type==typeof(int))type=typeof(long);else if(type==typeof(float))type=typeof(double);
            map.Add(name,type.FullName ?? throw new JsonException("Variable type has no stable identity."));
        }
        return new FrozenVariableTypeCatalog(map);
        void Add(string? name) { if(!string.IsNullOrWhiteSpace(name))names.Add(name); }
    }
}
