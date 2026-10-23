using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Workflow;
using SunEyeVision.Workflow.Persistence;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class SolutionPersistenceContractTests
{
    private static string WriteSample(string content)
    {
        var directory = Path.Combine(Path.GetTempPath(), "SunEye-B1-read", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "sample.solution"); File.WriteAllText(path, content); return path;
    }

    [Fact]
    public void Invalid_utf8_is_rejected_instead_of_replacing_corrupt_name_bytes()
    {
        var path = WriteSample("{}");
        var prefix = System.Text.Encoding.UTF8.GetBytes("{\"Id\":\"s\",\"Name\":\"");
        var suffix = System.Text.Encoding.UTF8.GetBytes("\"}");
        var bytes = prefix.Concat(new byte[] { 255 }).Concat(suffix).ToArray();
        File.WriteAllBytes(path, bytes);
        var result = new SolutionRepository().LoadCandidate(path);
        Assert.True(result.HasErrors); Assert.Null(result.Candidate); Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void Load_preserves_device_json_shapes_extensions_and_never_rewrites_file()
    {
        var source = "{\"Id\":\"s\",\"Devices\":[{\"Id\":\"d\",\"Type\":0,\"futureDevice\":[1],\"Settings\":{\"nested\":{\"x\":[1,null]},\"empty\":null}}],\"futureRoot\":{\"ToolType\":\"not a tool\"}}";
        var path = WriteSample(source); var repository = new SolutionRepository();
        var loaded = repository.LoadCandidate(path);
        Assert.False(loaded.HasErrors, string.Join(";", loaded.Diagnostics.Select(d => d.Message)));
        var saved = SolutionSnapshot.Prepare(loaded.Candidate!, path).Json;
        var root = JsonNode.Parse(saved)!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(source)!["Devices"]![0]!["Settings"], root["Devices"]![0]!["Settings"]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(source)!["futureRoot"], root["futureRoot"]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(source)!["Devices"]![0]!["futureDevice"], root["Devices"]![0]!["futureDevice"]));
        Assert.Equal(source, File.ReadAllText(path));
    }

    [Fact]
    public void Legacy_scalar_migrates_only_in_candidate_and_reports_it()
    {
        const string source = "{\"Id\":\"s\",\"GlobalVariables\":[{\"Id\":\"v\",\"Name\":\"x\",\"Type\":\"Integer\",\"Value\":{\"Type\":\"Integer\",\"Value\":3}}]}";
        var path = WriteSample(source); var loaded = new SolutionRepository().LoadCandidate(path);
        Assert.False(loaded.HasErrors, string.Join(";", loaded.Diagnostics.Select(d => d.Message)));
        Assert.Equal(new long[] { 3 }, Assert.IsType<long[]>(loaded.Candidate!.GlobalVariables[0].Value));
        Assert.Contains(loaded.Diagnostics, d => d.Code == "COMPAT_SCALAR"); Assert.Equal(source, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("{\"Id\":\"s\",\"Devices\":[{\"Id\":\"d\",\"Port\":\"bad\"}]}", "$.Devices[0].Port")]
    [InlineData("{\"Id\":\"s\",\"Devices\":[{\"Id\":\"d\",\"Type\":999}]}", "$.Devices[0].Type")]
    [InlineData("{\"Id\":\"s\",\"Devices\":[{\"Id\":\"d\",\"Settings\":[]}]}", "$.Devices[0].Settings")]
    [InlineData("{\"Id\":\"s\",\"Workflows\":[{\"Id\":\"w\",\"Nodes\":[{\"Name\":\"no id\"}]}]}", "$.Workflows[0].Nodes[0].Id")]
    public void Invalid_raw_fields_are_rejected_with_full_path(string json, string expectedPath)
    {
        var path = WriteSample(json); var result = new SolutionRepository().LoadCandidate(path);
        Assert.True(result.HasErrors); Assert.Null(result.Candidate);
        Assert.Contains(result.Diagnostics, d => d.JsonPath == expectedPath); Assert.Equal(json, File.ReadAllText(path));
    }
    [Fact]
    public void Canonical_comparison_preserves_array_order_and_unknown_fields()
    {
        Assert.True(SolutionSemanticComparer.Equivalent("{\"x\":1,\"y\":[2,3]}", "{\"y\":[2,3],\"x\":1}"));
        Assert.False(SolutionSemanticComparer.Equivalent("{\"x\":[1,2]}", "{\"x\":[2,1]}"));
        Assert.False(SolutionSemanticComparer.Equivalent("{\"future\":1}", "{}"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Type\":\"Integer\"}")]
    [InlineData("{\"Type\":\"Integer\",\"Value\":[\"bad\"]}")]
    [InlineData("{\"Type\":\"Unknown\",\"Value\":null}")]
    [InlineData("{\"Type\":\"Image\",\"Value\":\"not base64\"}")]
    public void Malformed_variable_does_not_silently_become_null(string json)
    {
        var options = new JsonSerializerOptions(); options.Converters.Add(new GlobalVariableValueConverter());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<object>(json, options));
    }

    [Fact]
    public void Valid_null_and_integer_tuple_survive()
    {
        var options = new JsonSerializerOptions(); options.Converters.Add(new GlobalVariableValueConverter());
        Assert.Null(JsonSerializer.Deserialize<object>("null", options));
        Assert.Null(JsonSerializer.Deserialize<object>("{\"Type\":\"Integer\",\"Value\":null}", options));
        Assert.Equal(new long[] { 1, 2 }, Assert.IsType<long[]>(JsonSerializer.Deserialize<object>("{\"Type\":\"Integer\",\"Value\":[1,2]}", options)));
    }

    [Fact]
    public void Orphan_connection_rejects_instead_of_deleting_it()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SunEyeVision.Workflow.Workflow>(
            "{\"Id\":\"w\",\"Name\":\"w\",\"Nodes\":[],\"Connections\":[{\"Id\":\"c\",\"SourceNodeId\":\"missing\",\"TargetNodeId\":\"missing\"}]}", WorkflowSerializationOptions.Default));
    }

    [Fact]
    public void Workflow_extension_survives_empty_connections()
    {
        var source = "{\"Id\":\"w\",\"Name\":\"w\",\"Nodes\":[],\"Connections\":[],\"future\":{\"array\":[true,null]}}";
        var candidate = JsonSerializer.Deserialize<SunEyeVision.Workflow.Workflow>(source, WorkflowSerializationOptions.Default)!;
        var written = JsonNode.Parse(JsonSerializer.Serialize(candidate, WorkflowSerializationOptions.Default))!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(source)!["future"], written["future"]));
    }

    [Fact]
    public void Snapshot_write_uses_frozen_content_and_preserves_original_on_failure()
    {
        var root = Path.Combine(Path.GetTempPath(), "SunEye-B1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "sample.solution");
        var document = Solution.Create(); document.Name = "未命名方案";
        var snapshot = SolutionSnapshot.Prepare(document, path);
        document.Description = "edit arriving after snapshot";
        var repository = new SolutionRepository(Path.Combine(root, "recovery"));
        Assert.True(repository.WriteSnapshot(snapshot).FileWritten);
        Assert.Null(document.FilePath);
        Assert.Equal("sample", JsonNode.Parse(File.ReadAllText(path))!["Name"]!.GetValue<string>());
        Assert.DoesNotContain("edit arriving after snapshot", File.ReadAllText(path));
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.False(repository.WriteSnapshot(snapshot).FileWritten);
        Assert.Null(document.FilePath);
    }

    [Fact]
    public void Relative_resource_requires_base_and_never_changes_reference()
    {
        var resolver = new SolutionResourceResolver(Path.GetTempPath());
        var resource = resolver.Resolve("FilePath", "missing/sub.png", SunEyeVision.Plugin.SDK.Execution.Parameters.ResourcePurpose.FileRead);
        Assert.Equal("missing/sub.png", resource.Reference);
        Assert.True(Path.IsPathRooted(resource.Address));
        Assert.Throws<InvalidDataException>(() => new SolutionResourceResolver(null).Resolve("FilePath", "sub.png", SunEyeVision.Plugin.SDK.Execution.Parameters.ResourcePurpose.FileRead));
    }

    [Fact]
    public void Loading_missing_duplicate_images_is_not_an_edit_and_preserves_check_list()
    {
        var input = new SunEyeVision.UI.Shared.ImageInputSource("node");
        var changes = 0; input.ConfigChanged = _ => changes++;
        var config = new ImageSourceConfig { NodeId = "node", FilePaths = new() { "missing.png", "missing.png", "other.png" },
            ForRunPaths = new() { "other.png", "absent.png", "missing.png", "other.png" }, CurrentIndex = 1, AutoSwitchEnabled = false, RunMode = 1 };
        input.LoadFromConfig(config, new SolutionResourceResolver(Path.GetTempPath()));
        Assert.Equal(0, changes); Assert.Equal(3, input.Count);
        input.AutoSwitchEnabled = true; // An unrelated edit must not reorder the persisted check list.
        var written = input.ToConfig();
        Assert.Equal(config.FilePaths, written.FilePaths); Assert.Equal(config.ForRunPaths, written.ForRunPaths); Assert.Equal(1, written.CurrentIndex);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 3)]
    [InlineData(0, 4)]
    [InlineData(2, 1)]
    [InlineData(2, 3)]
    public void Png_roundtrip_preserves_pixel_depth_channels_and_values(int depth, int channels)
    {
        using var original = new OpenCvSharp.Mat(2, 3, OpenCvSharp.MatType.MakeType(depth, channels), new OpenCvSharp.Scalar(101, 47, 231, 209));
        var options = new JsonSerializerOptions(); options.Converters.Add(new GlobalVariableValueConverter());
        var json = JsonSerializer.Serialize<object>(original, options);
        using var actual = Assert.IsType<OpenCvSharp.Mat>(JsonSerializer.Deserialize<object>(json, options));
        Assert.Equal(original.Type(), actual.Type()); Assert.Equal(original.Size(), actual.Size());
        Assert.Equal(0d, OpenCvSharp.Cv2.Norm(original, actual, OpenCvSharp.NormTypes.INF));
    }

    [Fact]
    public void Unsupported_image_depth_is_not_silently_converted_to_eight_bit()
    {
        using var original = new OpenCvSharp.Mat(2, 2, OpenCvSharp.MatType.CV_32FC1, new OpenCvSharp.Scalar(.25));
        var options = new JsonSerializerOptions(); options.Converters.Add(new GlobalVariableValueConverter());
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize<object>(original, options));
    }
}


