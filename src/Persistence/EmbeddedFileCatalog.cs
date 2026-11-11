using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SunEyeVision.Persistence;

/// <summary>Encoded file assets owned by one decoded-resource scope, never by a tool result.</summary>
public sealed class EmbeddedFileCatalog : IDisposable
{
    internal const long MaximumBytes=200L*1024*1024;
    private const string Prefix="suneye-file:";
    internal sealed record Entry(string Reference,string Name,string OriginalAddress,string OriginIdentity,string Sha256,byte[] Bytes);
    private readonly object _gate=new();
    private readonly Dictionary<string,Entry> _entries;
    private readonly Dictionary<string,FileStream> _materialized=new(StringComparer.Ordinal);
    private readonly string _directory=Path.Combine(Path.GetTempPath(),"SunEyeVision","solution-files",Guid.NewGuid().ToString("N"));
    private bool _disposed;
    private EmbeddedFileCatalog(Dictionary<string,Entry> entries)=>_entries=entries;
    public static bool IsReference(string reference)=>reference.StartsWith(Prefix,StringComparison.Ordinal);
    internal bool HasFiles {get{lock(_gate){ObjectDisposedException.ThrowIf(_disposed,this);return _entries.Count>0;}}}
    internal static Entry Create(string address,byte[] bytes,string? originIdentity=null)
    {
        address=Path.GetFullPath(address);
        originIdentity??=address;
        var name=Path.GetFileName(address);
        if(string.IsNullOrWhiteSpace(name)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new JsonException("Invalid embedded file name.");
        var sha=Convert.ToHexString(SHA256.HashData(bytes));
        var id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(originIdentity+"\0"+address.ToUpperInvariant()+"\0"+sha)));
        return new(Prefix+id+"/"+name,name,address,originIdentity,sha,bytes);
    }
    internal static EmbeddedFileCatalog Parse(JsonObject root)
    {
        var entries=new Dictionary<string,Entry>(StringComparer.Ordinal);long total=0;
        if(root.TryGetPropertyValue("EmbeddedFiles",out var value))
        {
            if(value is not JsonArray files)throw new JsonException("EmbeddedFiles must be an array.");
            foreach(var item in files)
            {
                if(item is not JsonObject file)throw new JsonException("Invalid embedded file entry.");
                var length=file["Length"]?.GetValue<long>()??throw new JsonException("Embedded length missing.");
                if(length<0||length>MaximumBytes-total)throw new JsonException("Embedded file budget exceeded.");
                var content=file["Content"]?.GetValue<string>()??throw new JsonException("Embedded bytes missing.");
                if(content.Length!=checked((length+2)/3*4))throw new JsonException("Embedded encoded length differs.");
                byte[] bytes;
                try{bytes=Convert.FromBase64String(content);}catch(FormatException error){throw new JsonException("Invalid embedded encoding.",error);}
                if(bytes.LongLength!=length)throw new JsonException("Embedded length differs.");
                var address=file["OriginalAddress"]?.GetValue<string>()??throw new JsonException("Embedded origin missing.");
                if(!Path.IsPathFullyQualified(address))throw new JsonException("Embedded origin must be absolute.");
                var origin=file["OriginIdentity"]?.GetValue<string>();
                if(string.IsNullOrEmpty(origin))throw new JsonException("Embedded reference identity missing.");
                var entry=Create(address,bytes,origin);
                if(file["Reference"]?.GetValue<string>()!=entry.Reference||file["Sha256"]?.GetValue<string>()!=entry.Sha256
                    ||file["Name"]?.GetValue<string>()!=entry.Name||!entries.TryAdd(entry.Reference,entry))
                    throw new JsonException("Embedded identity, digest or name differs, or repeats.");
                total+=length;
            }
        }
        return new(entries);
    }
    internal Entry Copy(string reference)
    {
        lock(_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            var entry=_entries.GetValueOrDefault(reference)??throw new InvalidDataException("Embedded resource is not owned by this solution: "+reference);
            return entry with{Bytes=entry.Bytes.ToArray()};
        }
    }
    internal string OriginalAddress(string reference)
    {lock(_gate){ObjectDisposedException.ThrowIf(_disposed,this);return (_entries.GetValueOrDefault(reference)??throw new InvalidDataException("Embedded source owner missing.")).OriginalAddress;}}
    internal void ValidateReferences(IEnumerable<FrozenResourceReference> resources)
    {
        var references=resources.Select(resource=>resource.Reference).Where(IsReference).ToHashSet(StringComparer.Ordinal);
        lock(_gate)
            if(!references.SetEquals(_entries.Keys))throw new JsonException("Embedded resources require exact declared owners; orphan or dangling reference found.");
    }
    internal bool Contains(string reference)
    {lock(_gate){ObjectDisposedException.ThrowIf(_disposed,this);return _entries.ContainsKey(reference);}}
    /// <summary>Application calls this after admission, before publishing the candidate; validation alone performs no IO.</summary>
    internal void MaterializeForAdmission()
    {
        lock(_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            foreach(var reference in _entries.Keys)ResolveAddress(reference);
        }
    }
    internal string ResolveAddress(string reference)
    {
        lock(_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            if(_materialized.TryGetValue(reference,out var materialized))return materialized.Name;
            var entry=_entries.GetValueOrDefault(reference)??throw new InvalidDataException("Embedded resource is not owned by this solution: "+reference);
            var id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reference)));
            var directory=Path.Combine(_directory,id);var target=Path.Combine(directory,entry.Name);
            if(!_materialized.ContainsKey(reference))
            {
                Directory.CreateDirectory(directory);
                FileStream? read=null;
                var created=false;
                try
                {
                    using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)){created=true;output.Write(entry.Bytes);}
                    read=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read);
                    _materialized.Add(reference,read);read=null;
                }
                catch(Exception failure)
                {
                    var errors=new List<Exception>{failure};
                    try{read?.Dispose();}catch(Exception error){errors.Add(error);}
                    try{if(created&&File.Exists(target))File.Delete(target);}catch(Exception error){errors.Add(error);}
                    if(errors.Count>1)throw new AggregateException("Embedded materialization and cleanup failed.",errors);
                    throw;
                }
            }
            return target;
        }
    }
    internal static JsonObject Write(Entry entry)=>new()
    { ["Reference"]=entry.Reference,["Name"]=entry.Name,["OriginalAddress"]=entry.OriginalAddress,["OriginIdentity"]=entry.OriginIdentity,["Sha256"]=entry.Sha256,
      ["Length"]=entry.Bytes.LongLength,["Content"]=Convert.ToBase64String(entry.Bytes) };
    public void Dispose()
    {
        lock(_gate)
        {
            if(_disposed)return;_disposed=true;_entries.Clear();
            var errors=new List<Exception>();
            foreach(var stream in _materialized.Values)try{stream.Dispose();}catch(Exception error){errors.Add(error);}
            _materialized.Clear();
            try{if(Directory.Exists(_directory))Directory.Delete(_directory,true);}catch(Exception error){errors.Add(error);}
            if(errors.Count>0)throw new AggregateException("Embedded file release failed.",errors);
        }
    }
}
