using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Xml.Linq;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

namespace SunEyeVision.UI.Shell.Controls.Toolbox;

/// <summary>用户确认的展示目录；ToolId 是独立产品身份，不因目录项存在而表示已加载。</summary>
internal sealed class ToolboxDisplayCatalog
{
    internal sealed record Entry(string Category, string Id, string Name, string Icon, string? ToolId, bool MappingVerified);
    internal IReadOnlyList<DirectoryToolGroup> Groups { get; }
    internal IReadOnlyList<Entry> Entries { get; }
    private ToolboxDisplayCatalog(IReadOnlyList<DirectoryToolGroup> groups, IReadOnlyList<Entry> entries)
        => (Groups, Entries) = (groups, entries);

    internal static ToolboxDisplayCatalog Load()
    {
        static Stream Open(string name) => System.Windows.Application.GetResourceStream(new Uri(
            "pack://application:,,,/SunEyeVision.UI;component/Resources/Toolbox/" + name))?.Stream
            ?? throw new InvalidDataException("工具箱资源不存在：" + name);
        using var groupStream = Open("ToolGroups.xml");
        var root = XDocument.Load(groupStream).Root ?? throw new InvalidDataException("工具箱分类为空。");
        var groups = root.Elements("Group").Select(e => new DirectoryToolGroup(
            (string?)e.Attribute("Name") ?? throw new InvalidDataException("分类缺少名称。"),
            (int?)e.Attribute("Order") ?? throw new InvalidDataException("分类缺少顺序。"),
            (string?)e.Attribute("Icon") ?? throw new InvalidDataException("分类缺少图标。"),
            (string?)e.Attribute("HoverIcon") ?? throw new InvalidDataException("分类缺少悬停图标。")))
            .OrderBy(g => g.Order).ToArray();
        if (groups.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != groups.Length)
            throw new InvalidDataException("工具箱分类重复。");
        using var stream = Open("ToolboxDisplayCatalog.json");
        using var document = JsonDocument.Parse(stream);
        var entries = new List<Entry>();
        foreach (var group in document.RootElement.EnumerateArray())
        {
            var category = group.GetProperty("Name").GetString() ?? throw new InvalidDataException("分类名称为空。");
            if (!groups.Any(g => g.Name == category)) throw new InvalidDataException("工具箱分类未声明：" + category);
            foreach (var item in group.GetProperty("Items").EnumerateArray())
            {
                string Required(string name) => item.GetProperty(name).GetString() is { Length: > 0 } s ? s : throw new InvalidDataException("工具箱字段为空：" + name);
                var toolId = item.TryGetProperty("ToolId", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                var verified = item.TryGetProperty("MappingVerified", out var flag) && flag.ValueKind == JsonValueKind.True;
                entries.Add(new(category, Required("Id"), Required("Name"), Required("Icon"), toolId, verified));
            }
        }
        if (entries.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != entries.Count ||
            entries.Where(e => e.MappingVerified).Any(e => string.IsNullOrWhiteSpace(e.ToolId)) ||
            entries.Where(e => e.MappingVerified).Select(e => e.ToolId).Distinct(StringComparer.Ordinal).Count() != entries.Count(e => e.MappingVerified))
            throw new InvalidDataException("工具箱目录身份或已核实映射重复。");
        return new(groups, entries);
    }
}
