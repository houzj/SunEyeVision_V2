using System.Text.Json;
using SunEyeVision.Persistence;
using Xunit;

public sealed class SolutionFormatPolicyTests
{
    [Theory]
    [InlineData("{\"SchemaVersion\":3}")]
    [InlineData("{\"SchemaVersion\":4}")]
    [InlineData("{}")]
    [InlineData("{\"SchemaVersion\":5,\"schemaversion\":5}")]
    [InlineData("{\"SchemaVersion\":5,\"GlobalVariables\":[{\"consumerbindings\":[]}]}")]
    [InlineData("{\"SchemaVersion\":5,\"GlobalVariables\":[],\"globalvariables\":[]}")]
    public void Invalid_versions_and_obsolete_consumer_cache_are_rejected(string json)
    { using var document=JsonDocument.Parse(json);Assert.Throws<JsonException>(()=>SolutionFormatPolicy.ValidateReadable(document.RootElement)); }
    [Fact] public void Current_version_passes_version_gate()
    { using var document=JsonDocument.Parse("{\"SchemaVersion\":5,\"GlobalVariables\":[]}");SolutionFormatPolicy.ValidateReadable(document.RootElement); }
}

