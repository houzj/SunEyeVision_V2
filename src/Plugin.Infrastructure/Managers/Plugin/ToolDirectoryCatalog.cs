using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

public enum DirectoryToolKind { Runtime }

public sealed record DirectoryTool(string Id, string DisplayName, string Category, int Order,
    string Icon, string Description, string EntryType, string AssemblyPath, bool IsVisible, DirectoryToolKind Kind = DirectoryToolKind.Runtime)
;
public sealed record DirectoryToolGroup(string Name, int Order, string Icon, string HoverIcon);

/// <summary>产品目录。只消费已准入Runtime声明，分类资源不承载工具生产者。</summary>
public sealed class ToolDirectoryCatalog
{
    private readonly Dictionary<string, DirectoryTool> _tools = new(StringComparer.Ordinal);
    public IReadOnlyList<DirectoryToolGroup> Groups { get; }
    public IReadOnlyCollection<string> ToolIds => _tools.Keys.ToArray();
    public IReadOnlyCollection<DirectoryTool> Tools => _tools.Values.ToArray();
    public int CountCategory(string name) => _tools.Values.Count(t => t.Category == name && t.IsVisible);

    internal ToolDirectoryCatalog(string groupsFile, IEnumerable<DirectoryTool> runtimeTools, Action<string> verifyFile)
    {
        ArgumentNullException.ThrowIfNull(verifyFile);
        verifyFile(groupsFile);
        var root = Read(groupsFile, "ToolGroups");
        if (root.Elements().Any(e => e.Name != "Group"))
            throw new InvalidDataException("产品分类文件不能声明旧SDK工具目录。");
        Groups = Array.AsReadOnly(root.Elements("Group").Select(e => new DirectoryToolGroup(Required(e, "Name"),
            (int)e.Attribute("Order")!, Required(e, "Icon"), Required(e, "HoverIcon"))).OrderBy(g => g.Order).ToArray());
        if (Groups.Select(g => g.Name).Distinct(StringComparer.Ordinal).Count() != Groups.Count)
            throw new InvalidDataException("分类名称重复。");
        foreach (var tool in runtimeTools)
        {
            if (tool.Kind != DirectoryToolKind.Runtime || !Groups.Any(g => g.Name == tool.Category) || !_tools.TryAdd(tool.Id, tool))
                throw new InvalidDataException("Runtime目录类型、分类或工具ID无效：" + tool.Id);
        }
        if (_tools.Count == 0) throw new InvalidDataException("产品目录缺少Runtime工具。");
    }

    public DirectoryTool? Find(string id) => _tools.GetValueOrDefault(id);
    public IReadOnlyList<DirectoryTool> GetCategory(string name) => _tools.Values
        .Where(t => t.Category == name && t.IsVisible).OrderBy(t => t.Order).ThenBy(t => t.Id, StringComparer.Ordinal).ToArray();

    private static XElement Read(string path, string name)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var root = XDocument.Load(reader).Root;
        if (root?.Name != name || (int?)root.Attribute("SchemaVersion") != 1)
            throw new InvalidDataException("不支持的工具目录清单：" + path);
        return root;
    }
    private static string Required(XElement e, string name) => (string?)e.Attribute(name) is { Length: > 0 } value
        ? value : throw new InvalidDataException("工具目录缺少字段：" + name);
}
