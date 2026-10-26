using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using SunEyeVision.Plugin.SDK.Metadata;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

public sealed record DirectoryTool(string Id, string DisplayName, string Category, int Order,
    string Icon, string Description, string EntryType, string AssemblyPath, bool IsVisible)
{
    public ToolMetadata ToMetadata() => new() { Id=Id, DisplayName=DisplayName, Category=Category,
        Icon=Icon, Description=Description, DeclaredOrder=Order };
}
public sealed record DirectoryToolGroup(string Name, int Order, string Icon, string HoverIcon);

/// <summary>部署目录声明。读取XML不加载程序集，也不以DLL是否可创建决定条目显隐。</summary>
public sealed class ToolDirectoryCatalog
{
    private readonly object _gate=new();
    private readonly Dictionary<string,DirectoryTool> _tools=new(StringComparer.Ordinal);
    private readonly Dictionary<string,(string Category,string Manifest,bool Visible)> _index=new(StringComparer.Ordinal);
    private readonly HashSet<string> _read=new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string>? _verify;
    public IReadOnlyList<DirectoryToolGroup> Groups {get;}
    public IReadOnlyCollection<string> ToolIds=>_index.Keys;
    public IReadOnlyCollection<DirectoryTool> Tools
    { get { lock(_gate) { foreach(var path in _index.Values.Select(i=>i.Manifest).Distinct(StringComparer.OrdinalIgnoreCase))Load(path);return _tools.Values.ToArray(); } } }
    public int CountCategory(string name)=>_index.Values.Count(i=>i.Category==name&&i.Visible);
    public ToolDirectoryCatalog(string pluginRoot,string groupsFile,Action<string>? verifyFile=null)
    {
        _verify=verifyFile;verifyFile?.Invoke(groupsFile);
        var root=Read(groupsFile,"ToolGroups");
        Groups=root.Elements("Group").Select(e=>new DirectoryToolGroup(Required(e,"Name"),(int)e.Attribute("Order")!,Required(e,"Icon"),Required(e,"HoverIcon"))).OrderBy(g=>g.Order).ToArray();
        if(Groups.Select(g=>g.Name).Distinct(StringComparer.Ordinal).Count()!=Groups.Count)throw new InvalidDataException("分类名称重复。");
        pluginRoot=Path.GetFullPath(pluginRoot);
        if(!Directory.Exists(pluginRoot))throw new DirectoryNotFoundException(pluginRoot);
        foreach(var e in root.Elements("Tool"))
        {
            var id=Required(e,"Id");var category=Required(e,"Category");var directory=Required(e,"Directory");
            if(Path.GetFileName(directory)!=directory||directory is "." or ".."||directory.Contains('\\')||directory.Contains('/'))throw new InvalidDataException("工具目录越界。");
            if(!Groups.Any(g=>g.Name==category))throw new InvalidDataException("未知分类："+category);
            var manifest=Path.Combine(pluginRoot,directory,"ToolItemInfo.xml");
            if(!Directory.Exists(Path.GetDirectoryName(manifest)))continue;
            if(!_index.TryAdd(id,(category,manifest,(bool?)e.Attribute("IsVisible")??true)))throw new InvalidDataException("工具ID重复："+id);
        }
    }
    public DirectoryTool? Find(string id)
    { lock(_gate) { if(!_index.TryGetValue(id,out var entry))return null;Load(entry.Manifest);return _tools.GetValueOrDefault(id); } }
    public IReadOnlyList<DirectoryTool> GetCategory(string name)
    {
        lock(_gate)
        {
            foreach(var path in _index.Values.Where(i=>i.Category==name).Select(i=>i.Manifest).Distinct(StringComparer.OrdinalIgnoreCase))Load(path);
            return _tools.Values.Where(t=>t.Category==name&&t.IsVisible).OrderBy(t=>t.Order).ThenBy(t=>t.Id,StringComparer.Ordinal).ToArray();
        }
    }
    private void Load(string manifest)
    {
        if(_read.Contains(manifest))return;
        _verify?.Invoke(manifest);
        var root=Read(manifest,"ToolItems");var assembly=Required(root,"Assembly");
        if(Path.GetFileName(assembly)!=assembly||assembly.Contains('\\')||assembly.Contains('/')||!assembly.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("工具入口程序集越界。");
        var entries=root.Elements("Tool").Select(e=>new DirectoryTool(Required(e,"Id"),Required(e,"DisplayName"),Required(e,"Category"),(int)e.Attribute("Order")!,Required(e,"Icon"),(string?)e.Attribute("Description")??"",Required(e,"EntryType"),Path.Combine(Path.GetDirectoryName(manifest)!,assembly),(bool?)e.Attribute("IsVisible")??true)).ToArray();
        if(entries.Length==0||entries.Select(t=>t.Id).Distinct(StringComparer.Ordinal).Count()!=entries.Length)throw new InvalidDataException("工具清单为空或ID重复。");
        var expected=_index.Where(p=>string.Equals(p.Value.Manifest,manifest,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(entries.Length!=expected.Length||entries.Any(t=>!expected.Any(p=>p.Key==t.Id&&p.Value.Category==t.Category&&p.Value.Visible==t.IsVisible)))throw new InvalidDataException("工具条目与分类索引不一致。");
        foreach(var entry in entries)_tools.Add(entry.Id,entry);
        _read.Add(manifest);
    }    private static XElement Read(string path,string name)
    {
        using var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
        var root=XDocument.Load(reader).Root;
        if(root?.Name!=name||(int?)root.Attribute("SchemaVersion")!=1)
            throw new InvalidDataException("不支持的工具目录清单："+path);
        return root;
    }
    private static string Required(XElement e,string name)=>(string?)e.Attribute(name) is {Length:>0} value
        ? value:throw new InvalidDataException("工具目录缺少字段："+name);
}