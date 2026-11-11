using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Domain.Persistence;

namespace SunEyeVision.Persistence;

public sealed class SolutionResourceResolver : IResourcePathResolver
{
    public string? BaseDirectory { get; }
    private readonly EmbeddedFileCatalog? _files;
    public SolutionResourceResolver(string? directory,EmbeddedFileCatalog? files=null)
    {BaseDirectory=directory==null?null:Path.GetFullPath(directory);_files=files;}
    public string ResolveAddress(string fieldName, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) throw new InvalidDataException(fieldName + ": resource reference is empty.");
        if(EmbeddedFileCatalog.IsReference(reference))return (_files??throw new InvalidDataException("Embedded resource owner missing.")).ResolveAddress(reference);
        if (reference.Contains('{')) throw new InvalidDataException(fieldName + ": unresolved expression cannot be used for file I/O.");
        if (!Path.IsPathRooted(reference) && BaseDirectory == null)
            throw new InvalidDataException(fieldName + ": save the solution or select an absolute path before accessing relative resources.");
        return Path.IsPathRooted(reference) ? Path.GetFullPath(reference) : Path.GetFullPath(reference, BaseDirectory!);
    }
    public ResourcePath Resolve(string fieldName, string reference, ResourcePurpose purpose)
    {
        if(EmbeddedFileCatalog.IsReference(reference)&&purpose!=ResourcePurpose.FileRead)throw new InvalidDataException("Embedded assets are read-only files.");
        var address = ResolveAddress(fieldName, reference);
        return new(reference, address, purpose == ResourcePurpose.DirectoryRead ? Directory.Exists(address) : File.Exists(address));
    }

    internal IReadOnlyList<FrozenResourceReference> Inspect(JsonNode root, List<PersistenceDiagnostic> diagnostics, IReadOnlyDictionary<string,FrozenToolDeclarations> declarations)
    {
        var plan=new List<FrozenResourceReference>();
        foreach (var (setting, declaration, path) in DeclaredSettings(root, declarations))
        {
            if (setting["Kind"]?.GetValue<string>() != "Constant")
            { plan.Add(new(path,string.Empty,declaration.Purpose,null,true)); diagnostics.Add(new("RESOURCE_DEFERRED", "Resource", "Warning", path, "Reference retained for runtime binding.")); continue; }
            void Check(JsonNode? value, string itemPath)
            {
                var reference=Text(value,itemPath);
                if (string.IsNullOrWhiteSpace(reference)) return;
                if(EmbeddedFileCatalog.IsReference(reference))
                {
                    if(declaration.Purpose!=ResourcePurpose.FileRead||_files?.Contains(reference)!=true)
                        throw new JsonException("Embedded file reference has no admitted owner.",itemPath,null,null);
                    plan.Add(new(itemPath,reference,declaration.Purpose,null));return;
                }
                if (reference.Contains('{'))
                { plan.Add(new(itemPath,reference,declaration.Purpose,null,true)); diagnostics.Add(new("RESOURCE_DEFERRED", "Resource", "Warning", itemPath, "Expression retained for runtime binding.")); return; }
                var address=ResolveAddress(itemPath,reference);
                plan.Add(new(itemPath,reference,declaration.Purpose,address));
                var exists=declaration.Purpose == ResourcePurpose.DirectoryRead ? Directory.Exists(address) : File.Exists(address);
                if (declaration.Purpose != ResourcePurpose.FileWrite && !exists)
                    diagnostics.Add(new("RESOURCE_UNAVAILABLE", "Resource", "Warning", itemPath, "Reference retained; resource unavailable: " + reference));
            }
            if (declaration.Cardinality == ResourceReferenceCardinality.Scalar) Check(setting["Value"],path);
            else
            {
                var values=setting["Value"] as JsonArray ?? throw new JsonException("Resource list must be an array.",path,null,null);
                if(declaration.Embedding is {} policy&&values.Select(value=>Text(value,path)).Where(EmbeddedFileCatalog.IsReference)
                    .Distinct(StringComparer.Ordinal).Take(policy.MaximumFiles+1).Count()>policy.MaximumFiles)
                    throw new JsonException("Embedded file count exceeds its admitted policy.",path,null,null);
                for (var i=0;i<values.Count;i++) Check(values[i],path+"["+i+"]");
            }
        }
        return plan.AsReadOnly();
    }

    internal static string RebaseCopy(string json, string sourceDirectory, string targetDirectory, IReadOnlyDictionary<string,FrozenToolDeclarations> declarations)
    {
        var root=JsonNode.Parse(json) ?? throw new JsonException("Empty document.");
        string Rebase(string reference) => EmbeddedFileCatalog.IsReference(reference)||string.IsNullOrWhiteSpace(reference) || Path.IsPathRooted(reference) || reference.Contains('{')
            ? reference : Path.GetRelativePath(targetDirectory,Path.GetFullPath(reference,sourceDirectory));
        foreach (var (setting, declaration, path) in DeclaredSettings(root,declarations))
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

    internal static IEnumerable<(JsonObject Fields,FrozenToolDeclarations Tool,string Path)> DeclaredOwners(JsonNode root,IReadOnlyDictionary<string,FrozenToolDeclarations> declarations)
    {
        if (root["Workflows"] is not JsonArray workflows) yield break;
        for (var w=0;w<workflows.Count;w++)
        if (workflows[w]?["Nodes"] is JsonArray nodes)
        for (var n=0;n<nodes.Count;n++)
        {
            var node=nodes[n] ?? throw new JsonException("Node is missing.");
            var path="$.Workflows["+w+"].Nodes["+n+"]";
            var tool=node["ToolType"]?.GetValue<string>() ?? throw new JsonException("Tool identity is missing.",path,null,null);
            if(!declarations.TryGetValue(tool,out var admitted) || admitted.ToolId!=tool) throw new JsonException("Resource tool declaration missing.",path,null,null);
            var resources=admitted.HostDeclarations.ResourceFields;
            if (resources.Count==0) continue;
            if (node["Configuration"] is not JsonObject runtime || runtime["ToolId"]?.GetValue<string>() != tool || runtime["Fields"] is not JsonObject settings)
                throw new JsonException("Resource owner requires its admitted Runtime settings.",path+".Configuration",null,null);
            yield return (settings,admitted,path+".Configuration.Fields");
        }
    }

    internal static IEnumerable<(JsonObject Setting,ResourceFieldDeclaration Declaration,string Path)> DeclaredSettings(JsonNode root,IReadOnlyDictionary<string,FrozenToolDeclarations> declarations)
    {
        foreach(var (settings,admitted,path) in DeclaredOwners(root,declarations))
            foreach (var declaration in admitted.HostDeclarations.ResourceFields)
            {
                if (!settings.TryGetPropertyValue(declaration.FieldId,out var value)) continue;
                var fieldPath=path+"."+declaration.FieldId;
                if (value is not JsonObject setting || setting["Kind"] is not JsonValue)
                    throw new JsonException("Invalid declared resource setting.",fieldPath,null,null);
                yield return (setting,declaration,fieldPath);
            }
    }
}
