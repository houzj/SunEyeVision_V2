using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 数组在绑定树/结果树中的显示助手（方案步骤6 / 〇 原则3）。
    /// </summary>
    /// <remarks>
    /// 默认折叠态：数组节点单行显示，元素值用 `;` 串联，树高与数组长度无关，根除大数据炸树。
    /// 展开态（用户主动点 `+`）：才物化子节点。RoI(ShapeParameters[]) 按**字段聚合**下钻
    /// （左上X/左上Y/宽/高 各为跨所有 RoI 元素的数组，用 `;` 串联）；其余数组类型逐元素 `[i]` 封顶。
    /// 所有展开子节点均封顶（默认 100），超出部分用溢出节点引导索引查看（防炸，rule-008 KISS）。
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
        /// 构建展开子节点（封顶 ExpansionCap）。
        /// - ShapeParameters[]（RoI）：按字段聚合，每个字段节点值 = 跨所有元素该字段 `;` 串联。
        /// - 其余数组：逐元素 `[i]` 节点，封顶后加溢出节点。
        /// </summary>
        public static List<TreeNodeData> BuildExpansionChildren(object? value, System.Type elementType)
        {
            var children = new List<TreeNodeData>();
            if (value is not IEnumerable enumerable || value is string)
                return children;

            // RoI 字段聚合：元素为 ShapeParameters，且元素本身或其 Geometry 实现 IMonitorable
            if (elementType == typeof(ShapeParameters) && enumerable is IEnumerable<ShapeParameters> shapeParams)
            {
                var list = shapeParams.ToList();
                if (list.Count == 0)
                    return children;

                static IMonitorable? AsMonitor(object? o) => o as IMonitorable ?? (o as ShapeParameters)?.Geometry as IMonitorable;

                var firstMon = AsMonitor(list[0]);
                if (firstMon != null)
                {
                    var fields = firstMon.GetMonitorFields();
                    foreach (var (fieldName, _) in fields)
                    {
                        var fieldValues = list.Select(sp =>
                        {
                            var m = AsMonitor(sp);
                            if (m != null)
                            {
                                var f = m.GetMonitorFields().FirstOrDefault(x => x.FieldName == fieldName);
                                return f.FieldValue?.ToString() ?? "--";
                            }
                            return "--";
                        });
                        children.Add(new TreeNodeData
                        {
                            Text = $"{fieldName} ({list.Count}项): {string.Join("; ", fieldValues)}",
                            IsSelectable = false,
                            IsExpanded = false,
                        });
                    }
                    return children;
                }
            }

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
        /// 构建 RoI(ShapeParameters[]) 在绑定树中的可绑定子节点：按图形类型分组 + 逐元素 [i]。
        /// 取代 BuildExpansionChildren 对 RoI 的"字段聚合"（异质数组会错位且不可绑定）。
        /// 每个分组/元素节点挂合成 AvailableDataSource，携带 [Type=]/[i] BindingSuffix，运行期由
        /// OutputVariableResolver 按图形类型过滤或取单元素（方案步骤4）。
        /// </summary>
        public static List<TreeNodeData> BuildRoiBindingChildren(AvailableDataSource parentDs, ShapeParameters[] roi)
        {
            var children = new List<TreeNodeData>();
            if (roi.Length == 0)
                return children;

            // 按图形类型分组
            foreach (var group in roi.GroupBy(sp => sp.ShapeType).OrderBy(g => g.Key.ToString()))
            {
                var type = group.Key;
                var items = group.ToList();
                var enumName = type.ToString();
                var displayName = GetShapeTypeShortName(type);
                var groupDs = new AvailableDataSource
                {
                    SourceNodeId = parentDs.SourceNodeId,
                    SourceNodeName = parentDs.SourceNodeName,
                    PropertyName = parentDs.PropertyName,
                    DisplayName = parentDs.DisplayName,
                    PropertyType = typeof(ShapeParameters[]),
                    VariableType = parentDs.VariableType,
                    BindingSuffix = $"[Type={enumName}]",
                    CurrentValue = items.ToArray(),
                };
                children.Add(new TreeNodeData
                {
                    Text = $"{displayName} ({items.Count}项)",
                    IsSelectable = true,
                    DataSource = groupDs,
                    BindingPath = $"{parentDs.PropertyName}[Type={enumName}]",
                });
            }

            // 逐元素 [i]
            for (int i = 0; i < roi.Length; i++)
            {
                var idx = i;
                var elemDs = new AvailableDataSource
                {
                    SourceNodeId = parentDs.SourceNodeId,
                    SourceNodeName = parentDs.SourceNodeName,
                    PropertyName = parentDs.PropertyName,
                    DisplayName = parentDs.DisplayName,
                    PropertyType = typeof(ShapeParameters),
                    VariableType = parentDs.VariableType,
                    BindingSuffix = $"[{idx}]",
                    CurrentValue = roi[idx],
                };
                children.Add(new TreeNodeData
                {
                    Text = $"[{idx}] {FormatElementSummary(roi[idx])}",
                    IsSelectable = true,
                    DataSource = elemDs,
                    BindingPath = $"{parentDs.PropertyName}[{idx}]",
                });
            }

            return children;
        }

        /// <summary>
        /// 图形类型 → 绑定树标签短名（用户拍板 [圆] 风格）。
        /// </summary>
        public static string GetShapeTypeShortName(ShapeType shapeType)
        {
            return shapeType switch
            {
                ShapeType.Point => "点",
                ShapeType.Line => "直线",
                ShapeType.Circle => "圆",
                ShapeType.Rectangle => "矩形",
                ShapeType.RotatedRectangle => "旋转矩形",
                ShapeType.Polygon => "多边形",
                ShapeType.Annulus => "圆环",
                ShapeType.Arc => "弧形",
                _ => shapeType.ToString()
            };
        }
    }
}
