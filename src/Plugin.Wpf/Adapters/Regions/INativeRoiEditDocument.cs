using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>ROI 编辑会话的组定义。工具决定组名、标题与数量上限，平台决定渲染与交互。</summary>
public sealed record NativeRoiEditGroup(string Name, string Title, bool PolygonOnly, int MaximumItems, string EmptyCoverage,
    bool AllowFullDomain = false);

/// <summary>
/// ROI 编辑宿主与工具文档之间的唯一接缝：读文档、写文档、把形状转成平台几何、接受编辑提案。
/// 宿主不直接操作工具的数据结构，工具也不直接操作编辑器。
/// </summary>
public interface INativeRoiEditDocument
{
    IReadOnlyList<NativeRoiEditGroup> Groups { get; }

    RecordValue Read();
    void Write(RecordValue document);

    /// <summary>把文档投影成平台可读的视图。</summary>
    IRoiDocument View(RecordValue document);

    /// <summary>工具形状 → 平台几何。</summary>
    RecordValue ToNative(RecordValue geometry);

    /// <summary>平台几何 → 工具形状，可参考旧形状保留未在几何中出现的成员。</summary>
    RecordValue FromNative(RecordValue geometry, RecordValue? previous);

    RecordValue Empty { get; }
    bool Same(RecordValue left, RecordValue right);

    /// <summary>应用一次编辑提案；失败时返回 false 并给出原因，调用方不得写入文档。</summary>
    bool TryApply(RecordValue before, string group, RoiEditProposal proposal,
        out RecordValue after, out RoiEditSelection selection, out string error);

    bool CanUndo { get; }
    bool CanRedo { get; }

    /// <summary>当前继承模式，用于预览切换判定。</summary>
    string PreviewMode();

    /// <summary>借用物理帧时使用的图像输入字段身份。</summary>
    string ImageInputFieldId { get; }

    /// <summary>读取某个组当前的覆盖模式。</summary>
    string Coverage(RecordValue document, string group);

    /// <summary>继承模式字段身份，预览时需要随输入一起借用。</summary>
    string ModeFieldId { get; }

    /// <summary>表示"本地绘制"的模式取值。</summary>
    string LocalModeValue { get; }

    /// <summary>该字段变化后是否需要重算预览。</summary>
    bool IsPreviewSource(string fieldId);

    /// <summary>预览需要借用的输入字段：模式对应的来源或参数成员。</summary>
    IReadOnlyList<string> PreviewInputIds(string mode, bool maskInherited);

    /// <summary>按当前模式与屏蔽区状态物化出用于预览的区域文档。</summary>
    RecordValue MaterializePreview(InputValues inputs, RecordValue document);
}
