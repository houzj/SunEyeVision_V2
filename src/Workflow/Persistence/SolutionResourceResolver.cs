using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow.Persistence;

public sealed class SolutionResourceResolver : IResourcePathResolver
{
    public string? BaseDirectory { get; }
    public SolutionResourceResolver(string? directory) => BaseDirectory = directory == null ? null : Path.GetFullPath(directory);
    public static SolutionResourceResolver For(Solution? solution) => new(solution?.ResourceBaseDirectory ?? (solution?.FilePath is { Length: > 0 } path
        ? Path.GetDirectoryName(Path.GetFullPath(path)) : null));
    public ResourcePath Resolve(string fieldName, string reference, ResourcePurpose purpose)
    {
        if (string.IsNullOrWhiteSpace(reference)) throw new InvalidDataException(fieldName + ": resource reference is empty.");
        if (reference.Contains('{')) throw new InvalidDataException(fieldName + ": unresolved expression cannot be used for file I/O.");
        if (!Path.IsPathRooted(reference) && BaseDirectory == null)
            throw new InvalidDataException(fieldName + ": save the solution or select an absolute path before accessing relative resources.");
        var address = Path.IsPathRooted(reference) ? Path.GetFullPath(reference) : Path.GetFullPath(reference, BaseDirectory!);
        return new(reference, address, purpose == ResourcePurpose.DirectoryRead ? Directory.Exists(address) : File.Exists(address));
    }

    internal void Inspect(JsonNode root, List<PersistenceDiagnostic> diagnostics)
    {
        void Check(JsonNode? value, string path, ResourcePurpose purpose)
        {
            if (value is not JsonValue text || !text.TryGetValue<string>(out var reference))
                throw new JsonException("Resource reference must be a string.", path, null, null);
            if (string.IsNullOrWhiteSpace(reference)) return;
            if (reference.Contains('{'))
            { diagnostics.Add(new("RESOURCE_DEFERRED", "Resource", "Warning", path, "Expression retained for runtime binding.")); return; }
            var resource = Resolve(path, reference, purpose);
            if (purpose != ResourcePurpose.FileWrite && !resource.Exists)
                diagnostics.Add(new("RESOURCE_UNAVAILABLE", "Resource", "Warning", path, "Reference retained; resource unavailable: " + reference));
        }
        if (root["Workflows"] is not JsonArray workflows) return;
        for (int w = 0; w < workflows.Count; w++)
        if (workflows[w]?["Nodes"] is JsonArray nodes)
        for (int n = 0; n < nodes.Count; n++)
        {
            var node = nodes[n]!; var path = "$.Workflows[" + w + "].Nodes[" + n + "]";
            if (node["ImageSourceConfig"] is JsonObject config)
            foreach (var field in new[] { "FilePaths", "ForRunPaths" })
            {
                if (!config.ContainsKey(field)) continue;
                if (config[field] is not JsonArray paths) throw new JsonException("Resource list must be an array.", path + ".ImageSourceConfig." + field, null, null);
                for (int i = 0; i < paths.Count; i++) Check(paths[i], path + ".ImageSourceConfig." + field + "[" + i + "]", ResourcePurpose.FileRead);
            }
            if (node["Parameters"] is not JsonObject parameters) continue;
            if (node["ToolType"]?.GetValue<string>() == "ImageLoad" && parameters.ContainsKey("FilePath"))
            {
                var camera = parameters["SourceKind"]?.ToJsonString() is "1" or "\"Camera\"";
                if (!camera) Check(parameters["FilePath"], path + ".Parameters.FilePath", ResourcePurpose.FileRead);
            }
            if (node["ToolType"]?.GetValue<string>() == "NPointCalibration" && parameters.ContainsKey("CalibrationFilePath"))
                Check(parameters["CalibrationFilePath"], path + ".Parameters.CalibrationFilePath", ResourcePurpose.FileWrite);
        }
    }

    internal static string RebaseCopy(string json, string sourceDirectory, string targetDirectory)
    {
        var root = JsonNode.Parse(json)!;
        string Rebase(string reference) => string.IsNullOrWhiteSpace(reference) || Path.IsPathRooted(reference) || reference.Contains('{')
            ? reference : Path.GetRelativePath(targetDirectory, Path.GetFullPath(reference, sourceDirectory));
        if (root["Workflows"] is JsonArray workflows)
        foreach (var workflow in workflows)
        if (workflow?["Nodes"] is JsonArray nodes)
        foreach (var node in nodes)
        {
            if (node?["ImageSourceConfig"] is JsonObject config)
            foreach (var field in new[] { "FilePaths", "ForRunPaths" })
                if (config[field] is JsonArray paths)
                    for (int i = 0; i < paths.Count; i++) paths[i] = Rebase(paths[i]!.GetValue<string>());
            var tool = node?["ToolType"]?.GetValue<string>();
            var parameterField = tool == "ImageLoad" ? "FilePath" : tool == "NPointCalibration" ? "CalibrationFilePath" : null;
            if (parameterField != null && node?["Parameters"] is JsonObject parameters && parameters[parameterField] is JsonValue value)
                parameters[parameterField] = Rebase(value.GetValue<string>());
        }
        return root.ToJsonString();
    }
}
