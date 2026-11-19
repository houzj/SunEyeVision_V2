using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>ROI 文档中的一个形状。工具决定几何记录的格式，平台只负责呈现与编辑。</summary>
public sealed record RoiDocumentItem(Guid Id, string Name, RecordValue Geometry, bool Enabled, bool Visible);

/// <summary>
/// ROI 文档的只读视图。平台按组渲染和编辑，形状如何存储由工具实现决定。
/// 不可变：<see cref="WithItems"/> 返回新文档，避免编辑器持有可变状态。
/// </summary>
public interface IRoiDocument
{
    IReadOnlyList<string> Groups { get; }
    /// <summary>组标题，用于编辑区表头。</summary>
    string Title(string group);
    /// <summary>该组是否只接受多边形。</summary>
    bool PolygonOnly(string group);
    /// <summary>该组允许的最多形状数。</summary>
    int MaximumItems(string group);
    /// <summary>该组没有形状时使用的覆盖模式。</summary>
    string DefaultCoverage(string group);
    IReadOnlyList<RoiDocumentItem> Items(string group);
    string Coverage(string group);
    /// <summary>用给定形状与覆盖模式替换某个组，返回新文档。</summary>
    IRoiDocument WithItems(string group, IReadOnlyList<RoiDocumentItem> items, string coverage);
}
