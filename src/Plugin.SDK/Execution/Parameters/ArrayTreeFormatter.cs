using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
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

            // 第一个节点为"整体"：绑定整个检测区域数组（BindingSuffix=null → 路径 "[]"）
            children.Add(CreateRoiOverallNode(parentDs, roi));

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

            // 逐元素 [i]：每个单项节点下挂可绑定的几何属性叶子（[i].Geometry.属性 穿透）
            for (int i = 0; i < roi.Length; i++)
            {
                var idx = i;
                var sp = roi[idx];
                var elemDs = new AvailableDataSource
                {
                    SourceNodeId = parentDs.SourceNodeId,
                    SourceNodeName = parentDs.SourceNodeName,
                    PropertyName = parentDs.PropertyName,
                    DisplayName = parentDs.DisplayName,
                    PropertyType = typeof(ShapeParameters),
                    VariableType = parentDs.VariableType,
                    BindingSuffix = $"[{idx}]",
                    CurrentValue = sp,
                };
                var elemNode = new TreeNodeData
                {
                    Text = $"[{idx}] {FormatElementSummary(sp)}",
                    IsSelectable = true,
                    DataSource = elemDs,
                    BindingPath = $"{parentDs.PropertyName}[{idx}]",
                };
                // 属性叶子：遍历 sp.Geometry 的具体类型，生成可绑定属性路径
                elemNode.Children = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>(
                    BuildGeometryAttributeLeaves(elemDs, sp, idx));
                children.Add(elemNode);
            }

            return children;
        }

        /// <summary>
        /// 构建 RoI(ShapeParameters[]) 在设计期（未运行/未绘制）的可绑定子节点。
        /// 第一个节点为"整体"（绑定整个检测区域数组，BindingSuffix=null → 路径 "[]"），
        /// 其后按 ShapeType 枚举（跳过 None）生成全部分组，每个分组携带 [Type=X] BindingSuffix，
        /// 不依赖任何实际数据（rule-016 数据驱动优先：结构由类型声明驱动）。
        /// 运行期由 OutputVariableResolver 按 [Type=X] 过滤真实图形；若检测区无该类型直接报错。
        /// </summary>
        public static List<TreeNodeData> BuildRoiDesignTimeGroups(AvailableDataSource parentDs)
        {
            var children = new List<TreeNodeData>
            {
                CreateRoiOverallNode(parentDs),
            };
            foreach (var type in Enum.GetValues<ShapeType>())
            {
                if (type == ShapeType.None) continue;

                var enumName = type.ToString();
                var groupDs = new AvailableDataSource
                {
                    SourceNodeId = parentDs.SourceNodeId,
                    SourceNodeName = parentDs.SourceNodeName,
                    PropertyName = parentDs.PropertyName,
                    DisplayName = parentDs.DisplayName,
                    PropertyType = typeof(ShapeParameters[]),
                    VariableType = parentDs.VariableType,
                    BindingSuffix = $"[Type={enumName}]",
                    CurrentValue = Array.Empty<ShapeParameters>(),
                };
                children.Add(new TreeNodeData
                {
                    Text = GetShapeTypeShortName(type),
                    IsSelectable = true,
                    DataSource = groupDs,
                    BindingPath = $"{parentDs.PropertyName}[Type={enumName}]",
                });
            }
            return children;
        }

        /// <summary>
        /// 为 RoI 单项 [i] 生成几何属性叶子（Geometry 穿透）。
        /// 遍历 ShapeParameters.Geometry 的具体类型（Circle2d/RotatedRect2d/Point2d/Line2d/Polygon2d/Annulus2d），
        /// 对标量字段（double/int/bool/string）与 Point/Vector 字段生成可绑定叶子，
        /// 绑定路径形如 "InspectionGeometries[i].Geometry.Center.X"（方案层级 3b）。
        /// </summary>
        private static List<TreeNodeData> BuildGeometryAttributeLeaves(AvailableDataSource elemDs, ShapeParameters sp, int idx)
        {
            var leaves = new List<TreeNodeData>();
            var geometry = sp.Geometry;
            if (geometry == null) return leaves;

            // 遍历 Geometry 具体类型的公共数据字段（Center/Radius/Width/Height/Angle/Point/Direction/X/Y 等）。
            // 标量字段直接成叶子；Point/Vector 字段下钻 .X/.Y。
            var geometryType = geometry.GetType();
            var fields = geometryType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .ToList();

            foreach (var field in fields)
            {
                object? fieldValue;
                try { fieldValue = field.GetValue(geometry); }
                catch { continue; }

                if (IsScalarType(field.FieldType))
                {
                    AddLeaf(leaves, elemDs, idx, field.Name, field.Name, field.FieldType, fieldValue);
                }
                else if (field.FieldType == typeof(Point))
                {
                    var pt = (Point)(fieldValue ?? default(Point));
                    AddLeaf(leaves, elemDs, idx, $"{field.Name}.X", $"{field.Name} X", typeof(double), pt.X);
                    AddLeaf(leaves, elemDs, idx, $"{field.Name}.Y", $"{field.Name} Y", typeof(double), pt.Y);
                }
                else if (field.FieldType == typeof(Vector))
                {
                    var v = (Vector)(fieldValue ?? default(Vector));
                    AddLeaf(leaves, elemDs, idx, $"{field.Name}.X", $"{field.Name} X", typeof(double), v.X);
                    AddLeaf(leaves, elemDs, idx, $"{field.Name}.Y", $"{field.Name} Y", typeof(double), v.Y);
                }
                // Point[] / 其他复合类型（如 Polygon2d.Vertices）暂不生成叶子（YAGNI，需时经轮廓工具）
            }

            return leaves;
        }

        private static void AddLeaf(List<TreeNodeData> leaves, AvailableDataSource elemDs, int idx,
            string bindingSuffix, string displayName, Type propertyType, object? value)
        {
            leaves.Add(new TreeNodeData
            {
                Text = displayName,
                IsSelectable = true,
                BindingPath = $"{elemDs.PropertyName}[{idx}].Geometry.{bindingSuffix}",
                DataSource = new AvailableDataSource
                {
                    SourceNodeId = elemDs.SourceNodeId,
                    SourceNodeName = elemDs.SourceNodeName,
                    PropertyName = elemDs.PropertyName,
                    DisplayName = displayName,
                    PropertyType = propertyType,
                    VariableType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToTypeVocabulary(propertyType),
                    BindingSuffix = $"[{idx}].Geometry.{bindingSuffix}",
                    CurrentValue = value,
                },
            });
        }

        private static bool IsScalarType(Type type)
        {
            return type == typeof(double) || type == typeof(int) || type == typeof(long)
                || type == typeof(float) || type == typeof(bool) || type == typeof(string)
                || type == typeof(uint) || type == typeof(short) || type == typeof(byte);
        }

        /// <summary>
        /// 创建 RoI"整体"绑定节点：绑定整个检测区域数组。
        /// BindingSuffix=null + PropertyType=ShapeParameters[] → GetBindingPath() 输出 "NodeId.属性名[]"，
        /// 运行期 OutputVariableResolver 对显式 "[]"（firstIndex=-1）保留整个数组（方案 B + 整体入口）。
        /// </summary>
        private static TreeNodeData CreateRoiOverallNode(AvailableDataSource parentDs, ShapeParameters[]? roi = null)
        {
            var overallDs = new AvailableDataSource
            {
                SourceNodeId = parentDs.SourceNodeId,
                SourceNodeName = parentDs.SourceNodeName,
                PropertyName = parentDs.PropertyName,
                DisplayName = parentDs.DisplayName,
                PropertyType = typeof(ShapeParameters[]),
                VariableType = parentDs.VariableType,
                BindingSuffix = null,
                CurrentValue = roi,
            };
            return new TreeNodeData
            {
                Text = "整体",
                IsSelectable = true,
                DataSource = overallDs,
                BindingPath = $"{parentDs.PropertyName}[]",
            };
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
