using System.Text;
using System.Text.Json;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Persistence;
using SunEyeVision.Plugin.Contracts;
using Xunit;

public sealed class VariableArchiveTests
{
    private sealed class Resource : IDisposable { public bool Released; public void Dispose() => Released = true; }
    private static readonly FrozenVariableTypeCatalog Types = new(new Dictionary<string,string>{{"Integer","System.Int64"},{"Double","System.Double"}});
    private static readonly JsonSerializerOptions Options = new();
    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);
    private const string Valid = "{\"Format\":\"SunEyeVision.GlobalVariables\",\"FormatVersion\":1,\"Variables\":[{\"Id\":\"v\",\"Name\":\"v\",\"Type\":\"Integer\",\"Value\":{\"Type\":\"Integer\",\"Value\":[1,2]}}]}";
    [Fact] public void Scalar_declaration_accepts_array_and_transfer_once()
    {
        var scope = new DecodedResourcesScope(); var resource = new Resource(); scope.RegisterNew(resource);
        using var candidate = VariableArchive.Read(Bytes(Valid), Types, Options, scope);
        Assert.Equal(new long[]{1,2}, Assert.IsType<long[]>(Assert.Single(candidate.Variables).Value));
        using var owner = candidate.TransferResources(); candidate.Dispose(); Assert.False(resource.Released);
        owner.Dispose(); Assert.True(resource.Released); Assert.Throws<InvalidOperationException>(() => candidate.TransferResources());
    }
    [Theory]
    [InlineData("[]")]
    [InlineData("{\"Format\":\"SunEyeVision.GlobalVariables\",\"FormatVersion\":2,\"Variables\":[]}")]
    [InlineData("{\"Format\":\"SunEyeVision.GlobalVariables\",\"FormatVersion\":1,\"Variables\":[],\"extra\":1}")]
    public void Old_or_unknown_format_releases_candidate(string json)
    {
        var scope = new DecodedResourcesScope(); var resource = new Resource(); scope.RegisterNew(resource);
        Assert.ThrowsAny<Exception>(() => VariableArchive.Read(Bytes(json), Types, Options, scope)); Assert.True(resource.Released);
    }
    [Fact] public void Editable_value_validator_preserves_finite_and_array_rules()
    {
        VariableValueDeclarations.ValidateEditableValue("Integer", new long[]{1,2});
        VariableValueDeclarations.ValidateEditableValue("Double", new double[]{1,2});
        Assert.Throws<InvalidOperationException>(() => VariableValueDeclarations.ValidateEditableValue("Double", new double[]{double.NaN}));
        Assert.Throws<InvalidOperationException>(() => VariableValueDeclarations.ValidateEditableValue("Integer[]", 1L));
    }
}
