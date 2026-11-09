using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using SunEyeVision.Foundation.Versioning;
using SunEyeVision.Plugin.Contracts;


namespace SunEyeVision.Plugin.Runtime;

/// <summary>The build-produced payload is the only approval source. Runtime framework
/// catalogs prove the origin selected by the CLR; they never approve application files.</summary>
public sealed class ReleasePayloadAuthority
{
    private sealed record Component(ToolComponentMetadata Metadata,string EntryType,string Path);
    private readonly List<Component> _components=new();
    private sealed record Entry(string Path,string Hash,string? Identity);
    private readonly string _root;
    private readonly Dictionary<string,Entry> _entries=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,string> _expectedTools=new(StringComparer.Ordinal);
    private readonly HashSet<string> _frameworkPayloadPaths=new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _frameworkNames=new(StringComparer.Ordinal);
    private readonly HashSet<string> _trustedPlatformAssemblyNames=new(StringComparer.OrdinalIgnoreCase);
    private readonly string _mode;
    private readonly bool _verifyAllFilesOnAdmission;
    public IReadOnlyDictionary<string,long> LastPreparationStageMs { get; private set; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string,long>(new Dictionary<string,long>());
    public IReadOnlyDictionary<string,long> LastLoadSubstageMs { get; private set; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string,long>(new Dictionary<string,long>());
    private long _dependencyVerifyMs;
    private long _dependencyLoadMs;
    private long _dependencyRecurseMs;
    public IReadOnlyDictionary<string,string> ExpectedTools=>new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(_expectedTools);

    public ReleasePayloadAuthority(string appRoot,bool verifyAllFilesOnAdmission=false)
    {
        _verifyAllFilesOnAdmission=verifyAllFilesOnAdmission;
        _root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(appRoot));
        // Framework assemblies are already authenticated by the CLR shared
        // framework. Do not remap or recursively load the publish directory's
        // copies during tool admission; VM only admits tool metadata here.
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
            foreach (var path in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                _trustedPlatformAssemblyNames.Add(Path.GetFileNameWithoutExtension(path));
        using var document=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_root,"release-payload.json")));
        var payload=document.RootElement;
        RejectDuplicateProperties(payload);
        if(payload.GetProperty("payloadSchemaVersion").GetInt32()!=2)throw new InvalidDataException("Unsupported release payload schema.");
        foreach(var field in new[]{"buildId","sourceCommit","artifactVersion"})
            if(string.IsNullOrWhiteSpace(payload.GetProperty(field).GetString()))throw new InvalidDataException("Missing payload identity: "+field);
        foreach(var item in payload.GetProperty("entries").EnumerateArray())
        {
            if(!item.TryGetProperty("components",out var components))continue;
            foreach(var component in components.EnumerateArray())
            {
                var role=component.GetProperty("role").GetString();
                if(role is not ("Runtime" or "Editor"))throw new InvalidDataException("Unknown tool component role.");
                var metadata=new ToolComponentMetadata(component.GetProperty("componentId").GetString()!,component.GetProperty("toolId").GetString()!,
                    Enum.Parse<ToolComponentRole>(role),component.GetProperty("contractMajor").GetInt32(),component.GetProperty("contractMinor").GetInt32(),
                    component.TryGetProperty("companionToolId",out var companion)?companion.GetString():null);
                var type=component.GetProperty("entryType").GetString();
                if(string.IsNullOrWhiteSpace(type))throw new InvalidDataException("Component entry type is missing.");
                _components.Add(new(metadata,type,item.GetProperty("path").GetString()!));
            }
        }
        _=SunEyeVision.Plugin.Runtime.ToolComponentCatalog.Prepare(_components.Select(c=>c.Metadata));
        foreach(var item in payload.GetProperty("entries").EnumerateArray())
        {
            var relative=item.GetProperty("path").GetString()??throw new InvalidDataException("Missing payload path.");
            if (relative.StartsWith("plugins/", StringComparison.OrdinalIgnoreCase) || item.TryGetProperty("tools", out _))
                throw new InvalidDataException("Retired SDK tool deployments are not supported.");
            var path=Resolve(relative);
            if(relative.Equals("release-payload.json",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Recursive payload entry.");
            var hash=item.GetProperty("sha256").GetString()??"";
            if(hash.Length!=64||!hash.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid payload SHA: "+relative);
            var identity=item.TryGetProperty("assemblyIdentity",out var value)?value.GetString():null;
            if(!_entries.TryAdd(relative,new(relative,hash,identity)))throw new InvalidDataException("Duplicate payload path: "+relative);
            var optional=false;
            if(item.TryGetProperty("optionalComponentId",out var optionalOwner))
            {
                var owner=_components.SingleOrDefault(c=>c.Metadata.ComponentId==optionalOwner.GetString()&&c.Metadata.Role==ToolComponentRole.Editor);
                if(owner is null||!relative.StartsWith(owner.Path[..^4]+".",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid optional editor payload ownership.");
                optional=true;
            }
            // The manifest and product anchor are checked during admission. The
            // remaining payload bytes are verified when their assembly is loaded
            // through VerifyApprovedFile. Strict probes may opt into the old
            // full-payload audit with verifyAllFilesOnAdmission=true.
            if((!optional||File.Exists(path))&&(_verifyAllFilesOnAdmission||relative.Equals("SunEyeVision.Foundation.dll",StringComparison.OrdinalIgnoreCase)))
                VerifyFile(path,_entries[relative]);
        }
        VerifyHostProductIdentity(payload);
        var declared = payload.GetProperty("productComponents").EnumerateArray().ToArray();
        if (declared.Length == 0 || declared.Length != _components.Count)
            throw new InvalidDataException("Product component set is missing or incomplete.");
        var admitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expected in declared)
        {
            var id = expected.GetProperty("componentId").GetString();
            if (string.IsNullOrWhiteSpace(id) || !admitted.Add(id))
                throw new InvalidDataException("Duplicate product component identity.");
            var actual = _components.SingleOrDefault(c => c.Metadata.ComponentId == id);
            if (actual is null || actual.Metadata.ToolId != expected.GetProperty("toolId").GetString()
                || actual.Metadata.Role.ToString() != expected.GetProperty("role").GetString()
                || actual.EntryType != expected.GetProperty("entryType").GetString()
                || actual.Path != expected.GetProperty("path").GetString()
                || (actual.Metadata.CompanionToolId ?? "") != expected.GetProperty("companionToolId").GetString())
                throw new InvalidDataException("Product component differs from its admitted declaration: " + id);
        }
        foreach (var runtime in _components.Where(c => c.Metadata.Role == ToolComponentRole.Runtime))
            if (!_expectedTools.TryAdd(runtime.Metadata.ToolId, runtime.Path + ":" + runtime.EntryType))
                throw new InvalidDataException("Duplicate Runtime producer.");
        if (_expectedTools.Count == 0) throw new InvalidDataException("Product has no Runtime tools.");
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

    /// <summary>Prepare one complete runtime generation from the approved physical payload.</summary>
    /// <summary>Prepare the complete desktop projection and runtime identities in one admitted generation.</summary>
    public RuntimeDirectorySnapshot PrepareDesktopDirectory()
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        var runtime=LoadToolComponents(null,runtimeOnly:true);
        var stages=new Dictionary<string,long>(StringComparer.Ordinal)
        {
            ["RuntimeAssemblies"] = clock.ElapsedMilliseconds
        };
        clock.Restart();
        var editorCache=new System.Collections.Concurrent.ConcurrentDictionary<string,ToolComponentRegistration?>(StringComparer.Ordinal);
        ToolComponentRegistration? ResolveEditor(string toolId)
            =>editorCache.GetOrAdd(toolId,id=>LoadToolComponents(new HashSet<string>(StringComparer.Ordinal){id},runtimeOnly:false)
                .SingleOrDefault(component=>component.Metadata.Role==ToolComponentRole.Editor));
        var snapshot=RuntimeDirectorySnapshot.PrepareRuntimeWithEditorResolver(runtime,_components.Select(component=>component.Metadata),ResolveEditor);
        stages["RuntimeDefinitions"] = clock.ElapsedMilliseconds;
        if(!snapshot.ToolIds.OrderBy(id=>id,StringComparer.Ordinal).SequenceEqual(_expectedTools.Keys.OrderBy(id=>id,StringComparer.Ordinal),StringComparer.Ordinal))
            throw new InvalidDataException("Admitted runtime directory differs from the declared release set.");
        LastPreparationStageMs=new System.Collections.ObjectModel.ReadOnlyDictionary<string,long>(stages);
        return snapshot;
    }
    public RuntimeDirectorySnapshot PrepareRuntimeDirectory()
    {
        var snapshot=RuntimeDirectorySnapshot.Prepare(LoadToolComponents(null, runtimeOnly:true));
        if(!snapshot.ToolIds.OrderBy(id=>id,StringComparer.Ordinal).SequenceEqual(_expectedTools.Keys.OrderBy(id=>id,StringComparer.Ordinal),StringComparer.Ordinal))
            throw new InvalidDataException("Admitted runtime directory differs from the declared release set.");
        return snapshot;
    }
    public IReadOnlyList<ToolComponentRegistration> LoadToolComponents(IReadOnlySet<string>? toolIds=null)
        =>LoadToolComponents(toolIds, runtimeOnly:false);

    private IReadOnlyList<ToolComponentRegistration> LoadToolComponents(IReadOnlySet<string>? toolIds,bool runtimeOnly)
    {
        var result=new List<ToolComponentRegistration>();
        var verifyMs=0L; var loadMs=0L; var dependencyMs=0L; var metadataMs=0L;
        _dependencyVerifyMs=0; _dependencyLoadMs=0; _dependencyRecurseMs=0;
        foreach(var group in _components.Where(c=>(!runtimeOnly||c.Metadata.Role==ToolComponentRole.Runtime) && (toolIds==null||toolIds.Contains(c.Metadata.ToolId))).GroupBy(c=>c.Path,StringComparer.OrdinalIgnoreCase))
        {
            var path=Resolve(group.Key);
            if(!File.Exists(path)&&group.All(c=>c.Metadata.Role==ToolComponentRole.Editor))continue;
            var stage=System.Diagnostics.Stopwatch.StartNew();
            VerifyApprovedFile(path);
            verifyMs+=stage.ElapsedMilliseconds; stage.Restart();
            var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            loadMs+=stage.ElapsedMilliseconds; stage.Restart();
            VerifyApprovedAssembly(assembly);
            LoadApprovedDependencies(assembly,_entries[group.Key],new HashSet<string>(StringComparer.Ordinal));
            dependencyMs+=stage.ElapsedMilliseconds; stage.Restart();
            var actual=assembly.GetCustomAttributes<ToolComponentAttribute>().ToArray();
            if(actual.Length!=group.Count()||actual.Any(a=>!group.Any(c=>a.Metadata==c.Metadata&&a.EntryType==c.EntryType)))
                throw new InvalidDataException("Component PE declarations differ from the release payload: "+group.Key);
            foreach(var declaration in group)
            {
                var type=assembly.GetType(declaration.EntryType,throwOnError:true)!;
                result.Add(new(declaration.Metadata,type));
            }
            metadataMs+=stage.ElapsedMilliseconds;
        }
        LastLoadSubstageMs=new System.Collections.ObjectModel.ReadOnlyDictionary<string,long>(new Dictionary<string,long>(StringComparer.Ordinal)
        {
            ["VerifyEntry"] = verifyMs,
            ["LoadAssembly"] = loadMs,
            ["LoadDependencies"] = dependencyMs,
            ["LoadDependencies.VerifyEntry"] = _dependencyVerifyMs,
            ["LoadDependencies.LoadAssembly"] = _dependencyLoadMs,
            ["LoadDependencies.Recurse"] = _dependencyRecurseMs,
            ["ReadComponentMetadata"] = metadataMs
        });
        return result;
    }

    // Component dependencies belong to the approved payload, not to an accidental host deps.json.
    // Follow only exact PE identities; framework resolution and existing runtime guards remain unchanged.
    private void LoadApprovedDependencies(Assembly owner,Entry ownerEntry,HashSet<string> visited)
    {
        if(!visited.Add(owner.FullName!))return;
        AssemblyDependencyResolver? resolver=null;
        var depsPath=Path.ChangeExtension(ownerEntry.Path,".deps.json").Replace('\\','/');
        if(_entries.TryGetValue(depsPath,out var deps))
        {
            VerifyFile(Resolve(depsPath),deps);
            resolver=new AssemblyDependencyResolver(Resolve(ownerEntry.Path));
        }
        foreach(var reference in owner.GetReferencedAssemblies())
        {
            if (!string.IsNullOrWhiteSpace(reference.Name) && _trustedPlatformAssemblyNames.Contains(reference.Name))
                continue;
            var matches=_entries.Values.Where(entry=>entry.Identity==reference.FullName).ToArray();
            if(matches.Length==0)continue;
            var winner=matches.Length==1?null:resolver?.ResolveAssemblyToPath(reference);
            var entry=matches.Length==1?matches[0]:matches.SingleOrDefault(candidate=>
                winner is not null&&string.Equals(Resolve(candidate.Path),Path.GetFullPath(winner),StringComparison.OrdinalIgnoreCase))
                ??throw new InvalidDataException("Approved dependency has no unique declared runtime winner: "+reference.FullName);
            var path=Resolve(entry.Path);
            var stage=System.Diagnostics.Stopwatch.StartNew();
            VerifyFile(path,entry);
            _dependencyVerifyMs+=stage.ElapsedMilliseconds;
            // VM 的启动阶段只建立工具元数据和声明，不把 HALCON/OpenCV
            // 这类大平台运行库递归装入进程。它们仍在准入时完成发布身份校验，
            // 但交给 CLR 在真正执行图像算法时按需装载，避免启动阶段承担算法库
            // 的映射、静态初始化和原生依赖探测成本。
            if (IsDeferredPlatformDependency(reference.Name))
                continue;
            var loaded=AssemblyLoadContext.Default.Assemblies.SingleOrDefault(assembly=>assembly.FullName==reference.FullName);
            stage.Restart();
            loaded??=AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            _dependencyLoadMs+=stage.ElapsedMilliseconds;
            // The CLR can return an existing host instance even for a requested approved path.
            // Apply the same exact PE identity and byte proof to both resolution outcomes.
            if(loaded.FullName!=entry.Identity)throw new FileLoadException("Loaded dependency identity differs from the approved payload.",loaded.Location);
            VerifyFile(loaded.Location,entry);
            stage.Restart();
            LoadApprovedDependencies(loaded,entry,visited);
            _dependencyRecurseMs+=stage.ElapsedMilliseconds;
        }
    }
    private static bool IsDeferredPlatformDependency(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.Equals("halcondotnetxl",StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("halcon",StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("opencv",StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("mv",StringComparison.OrdinalIgnoreCase);
    }
    private void VerifyHostProductIdentity(JsonElement payload)
    {
        // The Foundation PE is the product identity anchor. A fixture/root copy
        // is approved only when it has the exact bytes and identity of that host.
        const string corePath="SunEyeVision.Foundation.dll";
        var core=typeof(AppVersionInfo).Assembly;
        if(!_entries.TryGetValue(corePath,out var entry)||string.IsNullOrWhiteSpace(entry.Identity))
            throw new InvalidDataException("The host Foundation product identity is not approved by the release payload.");
        if(AssemblyLoadContext.GetLoadContext(core)!=AssemblyLoadContext.Default||core.FullName!=entry.Identity)
            throw new FileLoadException("The loaded Foundation identity differs from the release payload.",core.Location);
        VerifyFile(core.Location,entry);
        if(string.IsNullOrWhiteSpace(AppVersionInfo.Product)||string.IsNullOrWhiteSpace(AppVersionInfo.Commit)||string.IsNullOrWhiteSpace(AppVersionInfo.Channel))
            throw new InvalidDataException("The approved host Foundation product identity metadata is missing.");
        if(!string.Equals(payload.GetProperty("sourceCommit").GetString(),AppVersionInfo.Commit,StringComparison.Ordinal))
            throw new InvalidDataException("Payload sourceCommit differs from the approved host Foundation product identity.");
        var artifact=AppVersionInfo.Product+(AppVersionInfo.Channel=="stable"?"":"_"+AppVersionInfo.Channel);
        if(!string.Equals(payload.GetProperty("artifactVersion").GetString(),artifact,StringComparison.Ordinal))
            throw new InvalidDataException("Payload artifactVersion differs from the approved host Foundation product identity.");
        // buildId is an opaque generator-run identifier; there is no PE buildId anchor.
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
            if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Payload reparse points are not supported: "+relative);
        return full;
    }
    private static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
    private static void VerifyFile(string path,Entry entry)
    {
        if(!string.Equals(Hash(path),entry.Hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Payload SHA mismatch: "+entry.Path);
        if(entry.Identity is not null&&!string.Equals(AssemblyName.GetAssemblyName(path).FullName,entry.Identity,StringComparison.Ordinal))
            throw new InvalidDataException("Payload assembly identity mismatch: "+entry.Path);
    }
    public void VerifyApprovedFile(string path)
    {
        var relative=Path.GetRelativePath(_root,Path.GetFullPath(path)).Replace('\\','/');
        if(!_entries.TryGetValue(relative,out var entry))throw new InvalidDataException("File is not approved by the release payload: "+relative);
        VerifyFile(Resolve(relative),entry);
    }
    private void VerifyApprovedAssembly(Assembly assembly)
    {
        VerifyApprovedFile(assembly.Location);
        var relative=Path.GetRelativePath(_root,Path.GetFullPath(assembly.Location)).Replace('\\','/');
        if(_entries[relative].Identity!=assembly.FullName)throw new FileLoadException("Loaded assembly identity differs from the approved payload.",assembly.Location);
    }
}
