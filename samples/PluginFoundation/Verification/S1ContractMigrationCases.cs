using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow.Persistence;

/// <summary>Invoked by the isolated S1 runner. Source-linked format/value policies are exercised directly.
/// Does not claim Workflow repository, native bridge, production discovery or UI acceptance.</summary>
internal static class S1ContractMigrationCases
{
    public static void Run()
    {
        foreach (var version in new[] { "1", "2" })
        {
            var json = "{\"SchemaVersion\":" + version + "}";
            Require(SolutionFormatPolicy.ReadVersion(JsonNode.Parse(json)!) == int.Parse(version), "format node gate");
            using var doc = JsonDocument.Parse(json);
            SolutionFormatPolicy.ValidateReadable(doc.RootElement);
        }
        Require(SolutionFormatPolicy.ReadVersion(JsonNode.Parse("{}")!) == 1, "missing version must be legacy");
        using (var doc = JsonDocument.Parse("{}")) SolutionFormatPolicy.ValidateReadable(doc.RootElement);
        foreach (var version in new[] { "0", "null", "3", "999", "\"2\"", "true" })
        {
            var json = "{\"SchemaVersion\":" + version + "}";
            Reject(() => SolutionFormatPolicy.ReadVersion(JsonNode.Parse(json)!));
            Reject(() => { using var doc = JsonDocument.Parse(json); SolutionFormatPolicy.ValidateReadable(doc.RootElement); });
        }
        var source = "{\"Type\":\"System.Int64\",\"Value\":{\"Type\":\"System.Int64\",\"Value\":3}}";
        var folder = Path.Combine(AppContext.BaseDirectory, "case-data");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "legacy-variable.json");
        File.WriteAllText(path, source);
        var original = File.ReadAllBytes(path);
        var candidate = JsonNode.Parse(File.ReadAllText(path))!;
        var codes = new List<string>();
        SolutionVariableValuePolicy.Prepare(candidate, "$.v", true, Canonical,
            (code, _, _) => codes.Add(code));
        Require(candidate["Type"]!.GetValue<string>() == "Integer"
            && candidate["Value"]!["Value"] is JsonArray tuple && tuple.Count == 1,
            "legacy alias/scalar candidate normalization");
        Require(codes.Contains("COMPAT_TYPE_ALIAS") && codes.Contains("COMPAT_SCALAR"), "migration diagnostics");
        Require(original.SequenceEqual(File.ReadAllBytes(path)), "migration changed original file");
        Reject(() => SolutionVariableValuePolicy.Prepare(JsonNode.Parse(source)!, "$.v", false, Canonical, (_, _, _) => { }));
        Reject(() => SolutionVariableValuePolicy.Prepare(JsonNode.Parse("{\"Type\":\"Integer\",\"Value\":{\"Type\":\"Integer\",\"Value\":3}}")!, "$.v", false, Canonical, (_, _, _) => { }));
        var current = JsonNode.Parse("{\"Type\":\"Integer\",\"Value\":{\"Type\":\"Integer\",\"Value\":[3]}}")!;
        var before = current.ToJsonString();
        SolutionVariableValuePolicy.Prepare(current, "$.v", false, Canonical, (_, _, _) => throw new Exception("V2 legacy conversion"));
        Require(before == current.ToJsonString(), "V2 unexpectedly converted");
        Reject(() => SolutionVariableValuePolicy.Prepare(JsonNode.Parse("{\"Type\":\"Integer\",\"Value\":{\"Type\":\"Integer\",\"Value\":[3],\"unknown\":1}}")!, "$.v", true, Canonical, (_, _, _) => { }));
        Require(original.SequenceEqual(File.ReadAllBytes(path)), "rejection changed original file");

        var reference = new OutputReference("node", "field", TypeRef.ScalarOf(ScalarKind.Float64));
        var currentReference = BindingReferenceSerializer.Serialize(reference);
        Require(BindingReferenceSerializer.Deserialize(currentReference).PortId == "field", "current reference");
        var oldReference = JsonNode.Parse(currentReference)!;
        oldReference["ContractMajor"] = 4;
        var old = oldReference.ToJsonString();
        Reject(() => BindingReferenceSerializer.Deserialize(old));
        JsonNode Container(string expression) => new JsonObject
        {
            ["Settings"] = new JsonArray(new JsonObject { ["ParameterName"] = "threshold", ["ReferenceExpression"] = expression })
        };
        SolutionBindingExpressionPolicy.Validate(Container(currentReference), "$.ParamSettings");
        var actualContainer = new SunEyeVision.Plugin.SDK.Execution.Parameters.ParamSettingContainer();
        actualContainer.SetSetting(SunEyeVision.Plugin.SDK.Execution.Parameters.ParamSetting.CreateConstant("Threshold", 128));
        var actualJson = JsonSerializer.Serialize(actualContainer);
        Require(actualJson.Contains("\"IsValid\""), "real SDK fixture must exercise the computed projection");
        SolutionBindingExpressionPolicy.Validate(JsonNode.Parse(actualJson), "$.ParamSettings");
        Reject(() => SolutionBindingExpressionPolicy.Validate(Container(old), "$.ParamSettings"));
        var duplicatedSetting = Container(currentReference);
        ((JsonArray)duplicatedSetting["Settings"]!).Add(duplicatedSetting["Settings"]![0]!.DeepClone());
        Reject(() => SolutionBindingExpressionPolicy.Validate(duplicatedSetting, "$.ParamSettings"));
        var emptyName = Container(currentReference);
        emptyName["Settings"]![0]!["ParameterName"] = "";
        Reject(() => SolutionBindingExpressionPolicy.Validate(emptyName, "$.ParamSettings"));
        var untypedBinding = Container(currentReference);
        untypedBinding["Settings"]![0]!["ReferenceExpression"] = null;
        untypedBinding["Settings"]![0]!["BindingType"] = 1;
        Reject(() => SolutionBindingExpressionPolicy.Validate(untypedBinding, "$.ParamSettings"));
        var unknownSetting = Container(currentReference);
        unknownSetting["Settings"]![0]!["future"] = true;
        Reject(() => SolutionBindingExpressionPolicy.Validate(unknownSetting, "$.ParamSettings"));
        Require(original.SequenceEqual(File.ReadAllBytes(path)), "binding rejection changed original file");
        bool resolved = false;
        var migrated = BindingReferenceSerializer.MigrateVersion4(old, legacy => { resolved = true; return legacy; });
        Require(resolved && BindingReferenceSerializer.Deserialize(migrated).NodeId == "node", "explicit legacy owner callback");
        Reject(() => BindingReferenceSerializer.MigrateVersion4(old, _ => throw new JsonException("No approved identity")));
        var unknown = JsonNode.Parse(currentReference)!;
        unknown["Reference"]!["unknown"] = 1;
        Reject(() => BindingReferenceSerializer.Deserialize(unknown.ToJsonString()));
        var duplicate = currentReference.Insert(1, "\"ContractMajor\":5,");
        Reject(() => BindingReferenceSerializer.Deserialize(duplicate));
        var bad = JsonNode.Parse(currentReference)!;
        bad["Reference"]!["nodeId"] = "";
        Reject(() => BindingReferenceSerializer.Deserialize(bad.ToJsonString()));
        var region = TypeRef.ResourceOf(ResourceKind.Region);
        Reject(() => new ParameterEditSession(new[] { new Port("r", region, Nullable: true) },
            new Dictionary<string, ParameterSetting> { ["r"] = new ConstantSetting(null) }));
        var nested = TypeRef.RecordOf("has-region", new[] { new Port("r", region, Nullable: true) });
        Reject(() => new ParameterEditSession(new[] { new Port("x", nested) },
            new Dictionary<string, ParameterSetting> { ["x"] = new ConstantSetting(new Dictionary<string, object?> { ["r"] = null }) }));

        var ctor = typeof(PluginContractAttribute).GetConstructor(new[] { typeof(int), typeof(int) })!;
        Assembly Probe(int major)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("S1Probe_" + major + "_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            assembly.SetCustomAttribute(new CustomAttributeBuilder(ctor, new object[] { major, 0 }));
            return assembly;
        }
        Reject(() => FoundationContract.RequireCurrent(Probe(4)));
        FoundationContract.RequireCurrent(Probe(5));
        var runtime = new ToolComponentMetadata("blob.runtime", "blob", ToolComponentRole.Runtime, 5, 0);
        var editor = new ToolComponentMetadata("blob.wpf", "blob", ToolComponentRole.Editor, 5, 0, "blob");
        Require(ToolComponentCatalog.Prepare(new[] { runtime }).Runtimes.Count == 1, "runtime without editor");
        var catalog = ToolComponentCatalog.Prepare(new[] { editor, runtime });
        Require(catalog.Runtimes.Count == 1 && catalog.Editors.Count == 1, "optional editor algorithm count");
        Reject(() => ToolComponentCatalog.Prepare(new[] { runtime, new ToolComponentMetadata("duplicate", "blob", ToolComponentRole.Runtime, 5, 0) }));
        Reject(() => ToolComponentCatalog.Prepare(new[] { editor }));
        Reject(() => ToolComponentCatalog.Prepare(new[] { new ToolComponentMetadata("old", "blob", ToolComponentRole.Runtime, 4, 0) }));
        Require(catalog.Runtimes.Count == 1 && catalog.Editors.Count == 1, "rejected candidate modified published set");
    }
    private static string Canonical(string name) => name switch
    {
        "Integer" or "System.Int64" => "System.Int64",
        "Double" or "System.Double" => "System.Double",
        "String" or "System.String" => "System.String",
        "Boolean" or "System.Boolean" => "System.Boolean",
        _ => throw new JsonException("Unknown fixture type")
    };
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException) { return; }
        throw new Exception("Expected rejection.");
    }
}
