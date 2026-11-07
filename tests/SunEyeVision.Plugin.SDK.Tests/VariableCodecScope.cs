using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Persistence;
using SunEyeVision.Persistence.Variables;
namespace SunEyeVision.Plugin.Editing.Wpf.Tests;
internal sealed class VariableCodecScope : IDisposable
{
    private readonly DecodedResourcesScope _scope = new();
    public JsonSerializerOptions Options { get; }
    public VariableCodecScope(Type holderType)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => { if (info.Type == holderType)
            info.Properties.Single(p => p.Name == "Value").CustomConverter = new GlobalVariableValueConverter(new ComplexVariableValueCodec(_scope)); });
        Options = new JsonSerializerOptions { TypeInfoResolver = resolver };
    }
    public void Dispose() => _scope.Dispose();
}

