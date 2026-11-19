using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>把工具提供的 ROI 文档投影成编辑会话快照；组、覆盖模式与本地绘制语义都在这里统一解释。</summary>
public static class RoiDocumentProjection
{
    public static RoiEditSnapshot Project(IRoiDocument document, string editableGroup, Guid scope, long revision,
        ImageFrameAddress frame, Func<string, bool> isLocalDrawing, Func<RecordValue, RecordValue> toNative)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(isLocalDrawing);
        ArgumentNullException.ThrowIfNull(toNative);

        var rows = new List<RoiEditItem>();
        var groups = new List<RoiGroupState>();
        foreach (var group in document.Groups)
        {
            groups.Add(new RoiGroupState(group, Enum.Parse<RoiCoverage>(document.Coverage(group)),
                Enum.Parse<RoiCoverage>(document.DefaultCoverage(group))));
            var local = isLocalDrawing(group);
            foreach (var item in document.Items(group))
                rows.Add(new RoiEditItem(group, item.Id, item.Name, toNative(item.Geometry),
                    item.Enabled && local, item.Visible && local));
        }
        return new RoiEditSnapshot(new RoiEditStamp(scope, revision, frame), rows.AsReadOnly(), groups.AsReadOnly());
    }
}
