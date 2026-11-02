using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 数组在绑定树/结果树中的显示助手（绑定树 DataType 定型方案）。
    /// </summary>
    /// <remarks>
    /// - RoI(RoiShape[])：绑定树由 <see cref="BuildRoiTypedChildren"/> 按目标词目定型
    ///   （ROI/几何→类型集合叶；数值→类型容器+字段叶），不逐元素展开。
    /// - 其余数组默认折叠态单行显示，展开态引导索引查看（防炸，rule-008 KISS）。
    /// </remarks>
    public static class ArrayTreeFormatter
    {
        /// <summary>展开子节点封顶数量（防炸）。</summary>
        public const int ExpansionCap = 100;

        /// <summary>折叠态摘要最多串联的元素个数（超出截断，完整串由调用方决定放 ToolTip）。</summary>
        public const int CollapsedPreviewCount = 30;

        /// <summary>
        /// 折叠态单行摘要：`值1; 值2; …`（封顶 CollapsedPreviewCount，超出加 "…"）。
        /// </summary>
        public static string FormatCollapsedSummary(object? value, int previewCount = CollapsedPreviewCount)
        {
            if (value is not IEnumerable enumerable || value is string)
                return value?.ToString() ?? "--";

            var items = new List<string>(previewCount + 1);
            foreach (var element in enumerable)
            {
                if (items.Count >= previewCount)
                {
                    items.Add("…");
                    break;
                }
                items.Add(FormatElementSummary(element));
            }

            return items.Count == 0 ? "(空)" : string.Join("; ", items);
        }

        /// <summary>
        /// 单个元素的紧凑摘要（用于折叠态与逐元素展开节点）。
        /// RoI 取几何 ToString；区域取 ToString；其余取值本身。
        /// </summary>
        public static string FormatElementSummary(object? element)
        {
            if (element == null)
                return "--";

            // 区域特征：紧凑描述
            if (element is IVisionRegion region)
                return $"区域({region.BoundingBox.Width}x{region.BoundingBox.Height})";

            // 几何/RoI：用其 ToString（如 Circle2d(Center=..., R=...)）
            if (element is IMonitorable)
                return element.ToString() ?? "--";

            return element.ToString() ?? "--";
        }

        /// <summary>
        /// 构建普通数组展开子节点（封顶 ExpansionCap）。逐元素 `[i]` 节点，封顶后加溢出节点。
        /// （RoI(RoiShape[]) 不在此展开：绑定树由 <see cref="BuildRoiTypedChildren"/> 按目标词目定型。）
        /// </summary>
        public static List<TreeNodeData> BuildExpansionChildren(object? value, System.Type elementType)
        {
            var children = new List<TreeNodeData>();
            if (value is not IEnumerable enumerable || value is string)
                return children;

            // 逐元素 [i]（区域/数值/字符串等），封顶防炸
            int index = 0;
            foreach (var element in enumerable)
            {
                if (index >= ExpansionCap)
                {
                    children.Add(new TreeNodeData
                    {
                        Text = $"… 还有更多项（共 {CountEnumerable(enumerable)} 项，输入索引 [i] 查看单项）",
                        IsSelectable = false,
                    });
                    break;
                }

                children.Add(new TreeNodeData
                {
                    Text = $"[{index}] {FormatElementSummary(element)}",
                    IsSelectable = false,
                    IsExpanded = false,
                });
                index++;
            }

            return children;
        }

        private static int CountEnumerable(IEnumerable enumerable)
        {
            if (enumerable is ICollection collection)
                return collection.Count;
            int count = 0;
            foreach (var _ in enumerable)
                count++;
            return count;
        }

        /// <summary>
        /// 构建 RoI(RoiShape[]) 在绑定树中的可绑定子节点——**树形由目标参数词目（DataType）定型**。
        /// 铁律：绑定终点必是叶子；有可绑子节点的类型节点一律为纯容器（IsSelectable=false，只导航）。
        /// - 数值目标（Double/Integer，订阅几何参数）：类型集合绑不上 double，不出现；
        ///   每个声明类型 = 容器，children = <see cref="RoiFieldSchema"/> 字段叶（可绑，路径
        ///   <c>[Type=X][].Geometry.字段</c>：数组集合语义，绑定该类型全部图形的该字段；
        ///   仅 1 个时自动解包单值，N 个返回数组；需第 N 个由用户手写 [Type=X][N]）。
        /// - 几何值目标（Point/Circle/Line/RotatedRect/Polygon/Annulus/Arc）：只出
        ///   <see cref="ShapeTypeVocabulary"/> 允许的形状叶（如 Point → 仅"点"）——消除"能选不能用"。
        /// - ROI / 未限定目标（区域订阅等）：全部类型集合叶（绑 [Type=X]），均为叶子、无下钻。
        /// **「整体」叶已取消**（决策 2 + §11）：它原为"上游一个节点绘制多种图形 → 一把全绑"的
        /// 异构场景出口；决策 1（一个节点绑一种图形）与"订阅方只绑固定形状"确立后语义已废。
        /// 取消只影响"选择器里能否选"——老工程已绑的整体路径仍可解析，值不丢。
        /// 设计期/运行期结构一致：声明式由 ShapeType 枚举 + RoiFieldSchema 驱动，不依赖运行值
        /// （rule-016 数据驱动优先）。已删除旧逐元素 [i] 层级与 Geometry 反射枚举（schema 唯一字段源）。
        /// </summary>
        public static List<TreeNodeData> BuildRoiTypedChildren(AvailableDataSource parentDs, string? targetVarType, ShapeType? shapeFilter = null)
            => throw new NotSupportedException("旧 ROI 字符串来源不具备中立声明身份，当前入口不可用。");

        public static List<TreeNodeData> BuildGeometryFieldChildren(AvailableDataSource parentDs, System.Type geometryType)
            => throw new NotSupportedException("旧几何字符串来源不具备中立声明身份，当前入口不可用。");
        public static string GetShapeTypeShortName(ShapeType shapeType)
        {
            return shapeType switch
            {
                ShapeType.Point => "点",
                ShapeType.Line => "直线",
                ShapeType.Circle => "圆",
                ShapeType.RotatedRectangle => "旋转矩形",
                ShapeType.Polygon => "多边形",
                ShapeType.Annulus => "圆环",
                ShapeType.Arc => "弧形",
                _ => shapeType.ToString()
            };
        }
    }
}
