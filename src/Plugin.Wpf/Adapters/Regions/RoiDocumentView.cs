using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>
/// 标准 ROI 记录（<see cref="RoiDocumentValue"/>）的只读视图。
/// 组元数据由工具声明，其余全部由平台解释，工具不再需要自己的视图类。
/// </summary>
public sealed class RoiDocumentView : IRoiDocument
{
    private readonly RecordValue _root;
    private readonly IReadOnlyList<NativeRoiEditGroup> _groups;

    public RoiDocumentView(RecordValue root, IReadOnlyList<NativeRoiEditGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(groups);
        if (groups.Count == 0) throw new ArgumentException("ROI 文档至少需要一个组。", nameof(groups));
        _root = root;
        _groups = groups;
    }

    public IReadOnlyList<string> Groups => _groups.Select(group => group.Name).ToArray();

    public string Title(string group) => Group(group).Title;
    public bool PolygonOnly(string group) => Group(group).PolygonOnly;
    public int MaximumItems(string group) => Group(group).MaximumItems;
    public string DefaultCoverage(string group) => Group(group).EmptyCoverage;

    public string Coverage(string group) => RoiDocumentValue.Coverage(_root, group);

    public IReadOnlyList<RoiDocumentItem> Items(string group) => RoiDocumentValue.Items(_root, group)
        .Select(item => new RoiDocumentItem(
            Guid.ParseExact(item.Get<string>("id"), "N"),
            item.Get<string>("name"),
            item.Get<RecordValue>("shape"),
            item.Get<bool>("enabled"),
            item.Get<bool>("visible")))
        .ToArray();

    public IRoiDocument WithItems(string group, IReadOnlyList<RoiDocumentItem> items, string coverage)
    {
        ArgumentNullException.ThrowIfNull(items);
        var rows = items.Select(item => RoiDocumentValue.Item(item.Id, item.Name, item.Geometry, item.Enabled, item.Visible));
        var replaced = RoiDocumentValue.Group(coverage, rows);
        var others = _groups.Select(entry => entry.Name).Where(name => name != group)
            .Select(name => (Name: name, Value: _root.Get<RecordValue>(name)))
            .ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        var groups = new Dictionary<string, RecordValue>(StringComparer.Ordinal) { [group] = replaced };
        foreach (var pair in others) groups[pair.Key] = pair.Value;
        // Root 的字段顺序必须与声明一致，按 _groups 顺序重建。
        var ordered = _groups.Select(entry => groups[entry.Name]).ToArray();
        return new RoiDocumentView(RoiDocumentValue.Root(ordered[0], ordered[1]), _groups);
    }

    private NativeRoiEditGroup Group(string group)
        => _groups.FirstOrDefault(entry => entry.Name == group)
            ?? throw new ArgumentException("未声明的 ROI 组：" + group);
}
