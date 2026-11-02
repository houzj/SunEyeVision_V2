using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Workflow.Persistence;

public sealed class SolutionResourceResolver : IResourcePathResolver
{
    public string? BaseDirectory { get; }
    public SolutionResourceResolver(string? directory) => BaseDirectory = directory == null ? null : Path.GetFullPath(directory);
    public static SolutionResourceResolver For(Solution? solution) => new(solution?.ResourceBaseDirectory ?? (solution?.FilePath is { Length: > 0 } path
        ? Path.GetDirectoryName(Path.GetFullPath(path)) : null));
    public string ResolveAddress(string fieldName, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) throw new InvalidDataException(fieldName + ": resource reference is empty.");
        if (reference.Contains('{')) throw new InvalidDataException(fieldName + ": unresolved expression cannot be used for file I/O.");
        if (!Path.IsPathRooted(reference) && BaseDirectory == null)
            throw new InvalidDataException(fieldName + ": save the solution or select an absolute path before accessing relative resources.");
        return Path.IsPathRooted(reference) ? Path.GetFullPath(reference) : Path.GetFullPath(reference, BaseDirectory!);
    }
    public ResourcePath Resolve(string fieldName, string reference, ResourcePurpose purpose)
    {
        var address = ResolveAddress(fieldName, reference);
        return new(reference, address, purpose == ResourcePurpose.DirectoryRead ? Directory.Exists(address) : File.Exists(address));
    }

    internal void Inspect(JsonNode root, List<PersistenceDiagnostic> diagnostics, RuntimeDirectorySnapshot directory)
    {
        foreach (var (setting, declaration, path) in DeclaredSettings(root, directory))
        {
            if (setting["Kind"]?.GetValue<string>() != "Constant")
            { diagnostics.Add(new("RESOURCE_DEFERRED", "Resource", "Warning", path, "Reference retained for runtime binding.")); continue; }
            void Check(JsonNode? value, string itemPath)
            {
                var reference=Text(value,itemPath);
                if (string.IsNullOrWhiteSpace(reference)) return;
                if (reference.Contains('{'))
                { diagnostics.Add(new("RESOURCE_DEFERRED", "Resource", "Warning", itemPath, "Expression retained for runtime binding.")); return; }
                var address=ResolveAddress(itemPath,reference);
                var exists=declaration.Purpose == ResourcePurpose.DirectoryRead ? Directory.Exists(address) : File.Exists(address);
                if (declaration.Purpose != ResourcePurpose.FileWrite && !exists)
                    diagnostics.Add(new("RESOURCE_UNAVAILABLE", "Resource", "Warning", itemPath, "Reference retained; resource unavailable: " + reference));
            }
            if (declaration.Cardinality == ResourceReferenceCardinality.Scalar) Check(setting["Value"],path);
            else
            {
                var values=setting["Value"] as JsonArray ?? throw new JsonException("Resource list must be an array.",path,null,null);
                for (var i=0;i<values.Count;i++) Check(values[i],path+"["+i+"]");
            }
        }
    }

    internal static string RebaseCopy(string json, string sourceDirectory, string targetDirectory, RuntimeDirectorySnapshot directory)
    {
        var root=JsonNode.Parse(json) ?? throw new JsonException("Empty document.");
        string Rebase(string reference) => string.IsNullOrWhiteSpace(reference) || Path.IsPathRooted(reference) || reference.Contains('{')
            ? reference : Path.GetRelativePath(targetDirectory,Path.GetFullPath(reference,sourceDirectory));
        foreach (var (setting, declaration, path) in DeclaredSettings(root,directory))
        {
            if (setting["Kind"]?.GetValue<string>() != "Constant") continue;
            if (declaration.Cardinality == ResourceReferenceCardinality.Scalar)
                setting["Value"]=Rebase(Text(setting["Value"],path));
            else
            {
                var values=setting["Value"] as JsonArray ?? throw new JsonException("Resource list must be an array.",path,null,null);
                for (var i=0;i<values.Count;i++) values[i]=Rebase(Text(values[i],path+"["+i+"]"));
            }
        }
        return root.ToJsonString();
    }

    private static string Text(JsonNode? value,string path)
    {
        if (value is not JsonValue text || !text.TryGetValue<string>(out var reference))
            throw new JsonException("Resource reference must be a string.",path,null,null);
        return reference;
    }

    private static IEnumerable<(JsonObject Setting,ResourceFieldDeclaration Declaration,string Path)> DeclaredSettings(JsonNode root,RuntimeDirectorySnapshot directory)
    {
        if (root["Workflows"] is not JsonArray workflows) yield break;
        for (var w=0;w<workflows.Count;w++)
        if (workflows[w]?["Nodes"] is JsonArray nodes)
        for (var n=0;n<nodes.Count;n++)
        {
            var node=nodes[n] ?? throw new JsonException("Node is missing.");
            var path="$.Workflows["+w+"].Nodes["+n+"]";
            var tool=node["ToolType"]?.GetValue<string>() ?? throw new JsonException("Tool identity is missing.",path,null,null);
            var declarations=directory.GetResourceFields(tool);
            if (declarations.Count==0) continue;
            if (node["RuntimeConfiguration"] is not JsonObject runtime || runtime["ToolId"]?.GetValue<string>() != tool || runtime["Fields"] is not JsonObject settings)
                throw new JsonException("Resource owner requires its admitted Runtime settings.",path+".RuntimeConfiguration",null,null);
            foreach (var declaration in declarations)
            {
                if (!settings.TryGetPropertyValue(declaration.FieldId,out var value)) continue;
                var fieldPath=path+".RuntimeConfiguration.Fields."+declaration.FieldId;
                if (value is not JsonObject setting || setting["Kind"] is not JsonValue)
                    throw new JsonException("Invalid declared resource setting.",fieldPath,null,null);
                yield return (setting,declaration,fieldPath);
            }
        }
    }
}
