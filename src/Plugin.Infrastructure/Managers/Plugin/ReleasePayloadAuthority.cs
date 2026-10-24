using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

/// <summary>The build-produced payload is the only approval source. Runtime framework
/// catalogs prove the origin selected by the CLR; they never approve application files.</summary>
internal sealed class ReleasePayloadAuthority
{
    private sealed record Entry(string Path,string Hash,string? Identity);
    private readonly string _root;
    private readonly Dictionary<string,Entry> _entries=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,string> _tools=new(StringComparer.Ordinal);
    private readonly HashSet<string> _pluginPaths=new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _frameworkPayloadPaths=new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _frameworkNames=new(StringComparer.Ordinal);
    private readonly string _mode;
    internal IReadOnlyDictionary<string,string> ExpectedTools=>_tools;

    internal ReleasePayloadAuthority(string appRoot)
    {
        _root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(appRoot));
        using var document=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_root,"release-payload.json")));
        var payload=document.RootElement;
        RejectDuplicateProperties(payload);
        if(payload.GetProperty("payloadSchemaVersion").GetInt32()!=1)throw new InvalidDataException("Unsupported release payload schema.");
        foreach(var field in new[]{"buildId","sourceCommit","artifactVersion"})
            if(string.IsNullOrWhiteSpace(payload.GetProperty(field).GetString()))throw new InvalidDataException("Missing payload identity: "+field);
        foreach(var item in payload.GetProperty("entries").EnumerateArray())
        {
            var relative=item.GetProperty("path").GetString()??throw new InvalidDataException("Missing payload path.");
            var path=Resolve(relative);
            if(relative.Equals("release-payload.json",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Recursive payload entry.");
            var hash=item.GetProperty("sha256").GetString()??"";
            if(hash.Length!=64||!hash.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid payload SHA: "+relative);
            var identity=item.TryGetProperty("assemblyIdentity",out var value)?value.GetString():null;
            if(!_entries.TryAdd(relative,new(relative,hash,identity)))throw new InvalidDataException("Duplicate payload path: "+relative);
            VerifyFile(path,_entries[relative]);
        }
        foreach(var name in payload.GetProperty("expectedPluginAssemblies").EnumerateArray())
        {
            var simpleName=name.GetString();
            if(string.IsNullOrWhiteSpace(simpleName)||Path.GetFileName(simpleName)!=simpleName)throw new InvalidDataException("Invalid expected plugin name.");
            var relative=$"plugins/{simpleName}/{simpleName}.dll";
            if(!_pluginPaths.Add(relative)||!_entries.ContainsKey(relative))throw new InvalidDataException("Missing or duplicate expected plugin: "+relative);
            var declaration=payload.GetProperty("entries").EnumerateArray().Single(e=>string.Equals(e.GetProperty("path").GetString(),relative,StringComparison.OrdinalIgnoreCase));
            var tools=declaration.GetProperty("tools").EnumerateArray().ToArray();
            if(tools.Length==0)throw new InvalidDataException("Expected plugin has no tools: "+relative);
            foreach(var tool in tools)
            {
                var id=tool.GetProperty("id").GetString();var type=tool.GetProperty("typeName").GetString();
                if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(type)||!_tools.TryAdd(id,relative+":"+type))
                    throw new InvalidDataException("Invalid or duplicate declared ToolId: "+id);
            }
        }
        if(_pluginPaths.Count==0)throw new InvalidDataException("Release payload has no expected plugins.");
        var frameworks=payload.GetProperty("runtimeFrameworks");
        _mode=frameworks.GetProperty("deploymentMode").GetString()??"";
        if(_mode is not ("frameworkDependent" or "selfContained"))throw new InvalidDataException("Unknown framework deployment mode.");
        foreach(var framework in frameworks.GetProperty("frameworks").EnumerateArray())
        {
            var name=framework.GetProperty("name").GetString()??"";
            if(name is not ("Microsoft.NETCore.App" or "Microsoft.WindowsDesktop.App")||!_frameworkNames.Add(name))
                throw new InvalidDataException("Unsupported or duplicate framework declaration: "+name);
            if(!Version.TryParse(framework.GetProperty("version").GetString(),out _))throw new InvalidDataException("Invalid framework version.");
            if(_mode=="selfContained")foreach(var asset in framework.GetProperty("managedAssets").EnumerateArray())
            {
                var path=asset.GetProperty("payloadPath").GetString()??"";
                var source=asset.GetProperty("runtimePath").GetString()??"";
                var pack=asset.GetProperty("packId").GetString()??"";
                if(!_entries.TryGetValue(path,out var entry)||entry.Identity is null||
                    asset.GetProperty("assemblyIdentity").GetString()!=entry.Identity||
                    !pack.StartsWith(name+".Runtime.",StringComparison.OrdinalIgnoreCase)||
                    !Version.TryParse(asset.GetProperty("packVersion").GetString(),out _)||
                    string.IsNullOrWhiteSpace(source)||source.Contains('\\')||Path.IsPathRooted(source)||source.Split('/').Any(p=>p is "" or "." or "..")||
                    !_frameworkPayloadPaths.Add(path))
                    throw new InvalidDataException("Invalid framework publish winner: "+path);
            }
            if(_mode=="selfContained"&&framework.GetProperty("managedAssets").GetArrayLength()==0)
                throw new InvalidDataException("Framework has no managed publish winners: "+name);
        }
        if(!_frameworkNames.Contains("Microsoft.NETCore.App"))throw new InvalidDataException("Core framework declaration is missing.");
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if(element.ValueKind==JsonValueKind.Object)
        {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in element.EnumerateObject())
            {
                if(!names.Add(property.Name))throw new InvalidDataException("Duplicate payload JSON property: "+property.Name);
                RejectDuplicateProperties(property.Value);
            }
        }
        else if(element.ValueKind==JsonValueKind.Array)foreach(var value in element.EnumerateArray())RejectDuplicateProperties(value);
    }

    private string Resolve(string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||relative.Contains('\\')||Path.IsPathRooted(relative)||relative.Split('/').Any(p=>p is "" or "." or ".."))
            throw new InvalidDataException("Invalid payload relative path: "+relative);
        var full=Path.GetFullPath(Path.Combine(_root,relative));
        if(!full.StartsWith(Path.TrimEndingDirectorySeparator(_root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Payload path escapes its root.");
        // Reparse points would make a lexical path an approval for a different location.
        for(var current=full;!string.Equals(current,_root,StringComparison.OrdinalIgnoreCase);current=Path.GetDirectoryName(current)!)
            if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Payload reparse points are not supported: "+relative);
        return full;
    }
    private static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
    private static void VerifyFile(string path,Entry entry)
    {
        if(!string.Equals(Hash(path),entry.Hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Payload SHA mismatch: "+entry.Path);
        if(entry.Identity is not null&&!string.Equals(AssemblyName.GetAssemblyName(path).FullName,entry.Identity,StringComparison.Ordinal))
            throw new InvalidDataException("Payload assembly identity mismatch: "+entry.Path);
    }
    internal void VerifyApprovedFile(string path)
    {
        var relative=Path.GetRelativePath(_root,Path.GetFullPath(path)).Replace('\\','/');
        if(!_entries.TryGetValue(relative,out var entry))throw new InvalidDataException("File is not approved by the release payload: "+relative);
        VerifyFile(Resolve(relative),entry);
    }
    internal void VerifyPlugin(string path)
    {
        var relative=Path.GetRelativePath(_root,Path.GetFullPath(path)).Replace('\\','/');
        if(!_pluginPaths.Contains(relative))throw new InvalidDataException("Plugin is not in the expected release set: "+relative);
        VerifyApprovedFile(path);
    }
    private void VerifyApprovedAssembly(Assembly assembly)
    {
        VerifyApprovedFile(assembly.Location);
        var relative=Path.GetRelativePath(_root,Path.GetFullPath(assembly.Location)).Replace('\\','/');
        if(_entries[relative].Identity!=assembly.FullName)throw new FileLoadException("Loaded assembly identity differs from the approved payload.",assembly.Location);
    }
    internal void VerifyToolSet(string path,IEnumerable<Type> types)
    {
        var relative=Path.GetRelativePath(_root,Path.GetFullPath(path)).Replace('\\','/');
        var actual=types.Select(t=>relative+":"+t.FullName).OrderBy(t=>t,StringComparer.Ordinal).ToArray();
        var expected=_tools.Values.Where(t=>t.StartsWith(relative+":",StringComparison.OrdinalIgnoreCase)).OrderBy(t=>t,StringComparer.Ordinal).ToArray();
        if(!actual.SequenceEqual(expected,StringComparer.Ordinal))throw new InvalidDataException("Loaded tool types differ from the declared release set: "+relative);
        foreach(var type in types)
        {
            var attribute=type.GetCustomAttribute<SDK.Core.ToolAttribute>();
            if(attribute is null||attribute.GetType()!=typeof(SDK.Core.ToolAttribute)||!_tools.TryGetValue(attribute.Id,out var declaration)||declaration!=relative+":"+type.FullName)
                throw new InvalidDataException("Loaded ToolId differs from the declared release set: "+type.FullName);
        }
    }
    internal void VerifyShared(AssemblyName requested,Assembly host,string? localCandidate)
    {
        if(AssemblyLoadContext.GetLoadContext(host)!=AssemblyLoadContext.Default||!HostSatisfiesDefaultRequest(requested,host.GetName()))
            throw new FileLoadException("Shared assembly identity differs from the request.",requested.FullName);
        if(IsActualFramework(host))
        {
            VerifyLocalCandidate(host,localCandidate);
            if(_mode=="selfContained")
            {
                VerifyApprovedAssembly(host);
            }
            return;
        }
        VerifyApprovedAssembly(host);
        VerifyLocalCandidate(host,localCandidate);
    }
    private static void VerifyLocalCandidate(Assembly host,string? localCandidate)
    {
        if(localCandidate is not null)
        {
            if(!string.Equals(AssemblyName.GetAssemblyName(localCandidate).FullName,host.FullName,StringComparison.Ordinal)||!string.Equals(Hash(localCandidate),Hash(host.Location),StringComparison.Ordinal))
                throw new FileLoadException("Plugin-local shared candidate differs from the approved host bytes.",localCandidate);
        }
    }
    // Only for the actual assembly already returned by Default.LoadFromAssemblyName.
    // Main assembly and BAML bridge matching retain their exact-version policy.
    internal static bool HostSatisfiesDefaultRequest(AssemblyName requested,AssemblyName actual)
    {
        if(requested.Version is not null&&(actual.Version is null||actual.Version<requested.Version))return false;
        var boundRequest=new AssemblyName(requested.FullName){Version=actual.Version};
        return PluginLoadContext.MatchesRequestedIdentity(boundRequest,actual);
    }
    private bool IsActualFramework(Assembly assembly)
    {
        var path=Path.GetFullPath(assembly.Location);
        if(_mode=="selfContained")return _frameworkPayloadPaths.Contains(Path.GetRelativePath(_root,path).Replace('\\','/'));
        foreach(var anchor in new[]{typeof(object).Assembly,typeof(System.Windows.Window).Assembly})
        {
            var root=Path.GetDirectoryName(anchor.Location)!;
            var name=ReferenceEquals(anchor,typeof(object).Assembly)?"Microsoft.NETCore.App":"Microsoft.WindowsDesktop.App";
            if(!_frameworkNames.Contains(name)||!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))continue;
            if(root.StartsWith(Path.TrimEndingDirectorySeparator(_root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||string.Equals(root,Path.TrimEndingDirectorySeparator(_root),StringComparison.OrdinalIgnoreCase))continue;
            var catalog=Path.Combine(root,name+".deps.json");
            var selectedCatalogs=(AppContext.GetData("APP_CONTEXT_DEPS_FILES") as string??"").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries);
            if(!selectedCatalogs.Any(selected=>string.Equals(Path.GetFullPath(selected),catalog,StringComparison.OrdinalIgnoreCase)))continue;
            // Read only the CLR-selected framework's own catalog, respecting host roll-forward.
            using var deps=JsonDocument.Parse(File.ReadAllBytes(catalog));
            foreach(var target in deps.RootElement.GetProperty("targets").EnumerateObject())
                foreach(var library in target.Value.EnumerateObject())
                    if((library.Name.StartsWith(name+"/",StringComparison.Ordinal)||library.Name.StartsWith(name+".Runtime.",StringComparison.Ordinal))&&library.Value.TryGetProperty("runtime",out var runtime))
                        foreach(var asset in runtime.EnumerateObject())
                            if(string.Equals(Path.GetFullPath(Path.Combine(root,asset.Name)),path,StringComparison.OrdinalIgnoreCase))
                                return string.Equals(AssemblyName.GetAssemblyName(path).FullName,assembly.FullName,StringComparison.Ordinal);
        }
        return false;
    }
}
