using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;
using Xunit;
public sealed class CanonicalCodecTests
{
    private static IReadOnlyDictionary<string,string> Golden()=>JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"legacy-v3-golden.json")))!;
    private static string Fields(bool relaxed){using var stream=new MemoryStream();using(var writer=new Utf8JsonWriter(stream,new JsonWriterOptions{Encoder=relaxed?JavaScriptEncoder.UnsafeRelaxedJsonEscaping:null}))ParameterSettingCanonical.Write(writer,CanonicalCases.Fields());return Encoding.UTF8.GetString(stream.ToArray());}
    [Fact] public void Current_writer_matches_pre_migration_bytes_including_nested_documents_and_escaping()
    {
        var golden=Golden();Assert.Equal(golden["output"],ParameterSettingCanonical.Serialize(CanonicalCases.Nested));Assert.Equal(golden["variable"],ParameterSettingCanonical.Serialize(CanonicalCases.Variable));Assert.Equal(golden["resource"],ParameterSettingCanonical.Serialize(CanonicalCases.Resource));Assert.Equal(golden["constant-index"],ParameterSettingCanonical.Serialize(CanonicalCases.ConstantIndex));Assert.Equal(golden["index"],ParameterSettingCanonical.Serialize(CanonicalCases.Index));Assert.Equal(golden["fields"],Fields(false));Assert.Equal(golden["fields-relaxed"],Fields(true));
    }
    [Fact] public void Strict_current_reader_retains_version_duplicate_and_recursive_validation()
    {
        var golden=Golden();var output=ParameterSettingCanonical.Deserialize(golden["output"]);var variable=ParameterSettingCanonical.DeserializeVariable(golden["variable"]);
        Assert.Equal(golden["output"],ParameterSettingCanonical.Serialize(output));Assert.Equal(golden["variable"],ParameterSettingCanonical.Serialize(variable));
        Assert.Throws<JsonException>(()=>ParameterSettingCanonical.Deserialize(golden["output"].Replace("\"ContractMajor\":6","\"ContractMajor\":5")));
        Assert.Throws<JsonException>(()=>ParameterSettingCanonical.Deserialize(golden["output"].Replace("\"ContractMinor\":0","\"ContractMinor\":0,\"ContractMinor\":0")));
        Assert.Throws<JsonException>(()=>ParameterSettingCanonical.DeserializeVariable(golden["variable"].Replace("\"Selection\":\"AtField\"","\"Selection\":\"All\"")));
    }
    [Fact] public void Unset_nonfinite_and_unsupported_values_are_not_persisted()
    {
        static void Write(ParameterSetting value){using var stream=new MemoryStream();using var writer=new Utf8JsonWriter(stream);ParameterSettingCanonical.Write(writer,new Dictionary<string,ParameterSetting>{{"x",value}});}
        Assert.Throws<JsonException>(()=>Write(new UnsetSetting()));Assert.Throws<JsonException>(()=>Write(new ConstantSetting(double.NaN)));Assert.Throws<JsonException>(()=>Write(new ConstantSetting(new object())));
    }
}
