using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// 目标词目 → 可绑定 ROI 图形形状（<see cref="ShapeType"/>）的收敛表。
/// </summary>
/// <remarks>
/// 与 <c>Models.SdkTypeVocabulary</c>（CLR 类型 ↔ 词目）互补：本表回答
/// "这个**几何值词目**在 ROI 容器下钻时，允许出现哪些形状叶"。
///
/// <b>解决的问题（设计缺陷）</b>：ROI 容器（<c>RoiShape[]</c>）下钻此前对"几何目标"一律
/// 全列出 7 类形状（点/直线/圆/旋转矩形/多边形/圆环/弧形），**与目标数据类型无关** ——
/// 用户能选中，运行期却必然转换失败（"能选不能用"，且失败被推迟到运行期、只记 Warning）。
/// 现由本表收敛：目标 <c>Point</c> 只出"点"，<c>Circle</c> 只出"圆"，以此类推。
///
/// <b>三类目标的处理</b>：
/// <list type="bullet">
/// <item><b>形状受限</b>（几何值词目）→ 只出本表允许的形状叶；</item>
/// <item><b>形状不限</b>（<c>ROI</c> 区域订阅、未限定目标）→ 返回 <c>null</c>，
///   出**全部形状叶**（区域订阅要的就是一筐图形本身；「整体」入口已取消，见方案 §11）。</item>
/// </list>
///
/// ★ 本表是"词目是否几何图形""允许哪些形状"的**唯一真相源**；
///   调用方（<c>ParamBindingBase</c> 放行规则、<c>ArrayTreeFormatter</c> 下钻收敛）
///   一律查询本表，不得各自维护形状集合（rule-008 反对重复概念）。
/// </remarks>
public static class ShapeTypeVocabulary
{
    /// <summary>几何值词目 → 允许的 ROI 图形形状（1:1，仅语义完全等价才放行）。</summary>
    private static readonly Dictionary<string, IReadOnlyList<ShapeType>> ByVocabulary =
        new(StringComparer.Ordinal)
        {
            ["Point"]       = new[] { ShapeType.Point },
            ["Line"]        = new[] { ShapeType.Line },
            ["Circle"]      = new[] { ShapeType.Circle },
            // P0 已把轴对齐矩形统一为 RotatedRect2d（ShapeType.Rectangle 已删除），故仅 RotatedRectangle
            ["RotatedRect"] = new[] { ShapeType.RotatedRectangle },
            ["Polygon"]     = new[] { ShapeType.Polygon },
            ["Annulus"]     = new[] { ShapeType.Annulus },
            ["Arc"]         = new[] { ShapeType.Arc },
        };

    /// <summary>
    /// 该词目是否为几何形状词目（即：ROI 容器下钻时需按形状收敛、绑定下拉需放行 ROI 容器）。
    /// </summary>
    public static bool IsShapeVocabulary(string? vocabulary)
        => vocabulary != null && ByVocabulary.ContainsKey(vocabulary);

    /// <summary>
    /// 目标词目允许的 ROI 图形形状。
    /// </summary>
    /// <returns>受限形状列表；<c>null</c> = 形状不限（ROI / 未限定目标 → 全列出）。</returns>
    public static IReadOnlyList<ShapeType>? AllowedShapeTypes(string? vocabulary)
        => vocabulary != null && ByVocabulary.TryGetValue(vocabulary, out var types)
            ? types
            : null;
}
