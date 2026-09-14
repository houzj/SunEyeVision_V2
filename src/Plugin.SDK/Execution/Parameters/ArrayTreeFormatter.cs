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
    /// - 其余数组默认折叠态单行显示，展开态逐元素 `[i]` 封顶；所有展开子节点均封顶（默认 100），
    ///   超出部分用溢出节点引导索引查看（防炸，rule-008 KISS）。
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
        public static List<TreeNodeData> BuildRoiTypedChildren(
            AvailableDataSource parentDs, string? targetVarType, ShapeType? shapeFilter = null)
        {
            var children = new List<TreeNodeData>();
            bool numericTarget = targetVarType is "Double" or "Integer";

            if (numericTarget)
            {
                // 数值目标：类型集合/整体是 RoiShape[]（ROI 词目），绑到 double 必类型错 → 不出现；
                // 仅提供"类型容器 → 字段叶"，容器自身不可绑（只导航）。
                foreach (var type in Enum.GetValues<ShapeType>())
                {
                    if (type == ShapeType.None) continue;

                    var fields = RoiFieldSchema.GetFields(type);
                    if (fields.Count == 0)
                        continue; // 无可下钻字段的类型（如多边形）在数值目标下无处可绑 → 不出现

                    var enumName = type.ToString();
                    var typeNode = new TreeNodeData
                    {
                        Text = GetShapeTypeShortName(type),
                        IsSelectable = false, // 容器：单击只展开字段，绝不绑定
                        IsExpanded = false,
                    };
                    var leaves = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>();
                    foreach (var f in fields)
                    {
                        leaves.Add(CreateRoiFieldLeaf(parentDs, type, f));
                    }
                    typeNode.Children = leaves;
                    children.Add(typeNode);
                }
                return children;
            }

            // 形状受限的几何目标（如点参数的 Point 词目）：只出**语义等价**的形状叶。
            // 依据：ShapeTypeVocabulary（词目 → 允许形状的唯一真相源）。
            // 历史缺陷一：此处对几何目标一律全列出 7 类形状，与目标类型无关 → 能选不能用。
            // 历史缺陷二：「整体」叶（绑整个 RoiShape[]）曾是异构场景出口，现已取消（见方法 remarks）：
            //   ROI / 未限定目标只出类型集合叶，不再产出整体。
            var allowedTypes = ShapeTypeVocabulary.AllowedShapeTypes(targetVarType);

            // 形状筛选（区域订阅）：目标词目**未限定形状**时，由调用方声明的形状把候选收敛为单一类型叶。
            // 呈现诉求与值语义分离（决策 15）：筛选走独立入参，**不复用 DataType** —— 后者是值语义
            //（区域源的 [Type=X] 过滤结果恒为 RoiShape[]），复用会把值类型误声明成 Circle2d[]。
            if (shapeFilter is { } filter)
            {
                if (allowedTypes != null && !System.Linq.Enumerable.Contains(allowedTypes, filter))
                {
                    throw new InvalidOperationException(
                        $"形状筛选 '{filter}' 与目标词目 '{targetVarType}' 允许的形状（{string.Join("/", allowedTypes)}）冲突："
                        + "几何值目标的形状由词目决定，不允许再叠加筛选（rule-015 报错优先，不静默）。");
                }
                allowedTypes = new[] { filter };
            }

            foreach (var type in allowedTypes ?? (IReadOnlyList<ShapeType>)Enum.GetValues<ShapeType>())
            {
                if (type == ShapeType.None) continue;

                var enumName = type.ToString();
                var groupDs = new AvailableDataSource
                {
                    SourceNodeId = parentDs.SourceNodeId,
                    SourceNodeName = parentDs.SourceNodeName,
                    PropertyName = parentDs.PropertyName,
                    DisplayName = parentDs.DisplayName,
                    // 显示名 SSOT：类型集合叶的叶子名 = 图形类型短名（"圆"），语义为集合（带 []）
                    LeafDisplayName = GetShapeTypeShortName(type),
                    IsArraySemantics = true,
                    PropertyType = typeof(RoiShape[]),
                    VariableType = parentDs.VariableType,
                    BindingSuffix = $"[Type={enumName}]",
                    CurrentValue = Array.Empty<RoiShape>(),
                };
                children.Add(new TreeNodeData
                {
                    Text = GetShapeTypeShortName(type),
                    IsSelectable = true, // 类型集合叶 = 绑定终点（无 children，无展开歧义）
                    DataSource = groupDs,
                    BindingPath = $"{parentDs.PropertyName}[Type={enumName}]",
                });
            }
            return children;
        }

        /// <summary>
        /// 构建几何数组（同质，如 Circle2d[]）在数值目标下的字段叶：
        /// 绑定路径 = 属性名.字段（集合语义，运行期由 OutputVariableResolver 逐元素投影，
        /// 返回该几何数组全部元素的该字段；取单个元素由用户手写 [i]，如 Circle[2].Radius）。
        /// 字段叶即绑定终点（不含容器节点）。
        /// </summary>
        public static List<TreeNodeData> BuildGeometryFieldChildren(AvailableDataSource parentDs, System.Type geometryType)
        {
            var children = new List<TreeNodeData>();
            foreach (var f in GeometryFieldSchema.GetFields(geometryType))
            {
                var fieldDs = new AvailableDataSource
                {
                    SourceNodeId = parentDs.SourceNodeId,
                    SourceNodeName = parentDs.SourceNodeName,
                    PropertyName = parentDs.PropertyName,
                    DisplayName = f.DisplayName,
                    // 显示名 SSOT：几何字段叶 = "{父显示名}.{字段显示名}"（如 "圆心.X"），避免 "圆查找1.X" 歧义；
                    // 字段值类型是标量，但语义是"数组全部元素的该字段"，属集合语义 → 显式声明 true。
                    LeafDisplayName = $"{parentDs.EffectiveLeafDisplayName}.{f.DisplayName}",
                    IsArraySemantics = true,
                    PropertyType = f.ValueType,
                    VariableType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToTypeVocabulary(f.ValueType),
                    BindingSuffix = $".{f.Path}",
                    CurrentValue = null,
                };
                children.Add(new TreeNodeData
                {
                    Text = f.DisplayName,
                    IsSelectable = true,
                    IsExpanded = false,
                    BindingPath = $"{parentDs.PropertyName}.{f.Path}",
                    DataSource = fieldDs,
                });
            }
            return children;
        }

        /// <summary>
        /// 创建 RoI 字段叶（schema 唯一字段源）。绑定路径 <c>[Type=X][].Geometry.{f.Path}</c>——
        /// 集合语义：绑定"该类型全部图形的该字段"（[] 空括号 = 数组，不自动填索引）；
        /// 运行期对该类型逐图形投影字段：仅 1 个时自动解包为单值，N 个返回数组；
        /// 需要第 N 个时由用户手动在 [] 中写索引（[Type=X][N]）。检测区无该类型直接报错（rule-015）。
        /// </summary>
        private static TreeNodeData CreateRoiFieldLeaf(AvailableDataSource parentDs, ShapeType type, RoiFieldDef f)
        {
            var enumName = type.ToString();
            var suffix = $"[Type={enumName}][].Geometry.{f.Path}";
            var fieldDs = new AvailableDataSource
            {
                SourceNodeId = parentDs.SourceNodeId,
                SourceNodeName = parentDs.SourceNodeName,
                PropertyName = parentDs.PropertyName,
                DisplayName = f.DisplayName,
                // 显示名 SSOT：ROI 字段叶叶子名 = 字段显示名（"中心X"）；语义为集合（带 []）。
                IsArraySemantics = true,
                PropertyType = f.ValueType,
                VariableType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToTypeVocabulary(f.ValueType),
                BindingSuffix = suffix,
                CurrentValue = null,
            };
            return new TreeNodeData
            {
                Text = f.DisplayName,
                IsSelectable = true, // 字段叶 = 绑定终点
                IsExpanded = false,
                BindingPath = $"{parentDs.PropertyName}{suffix}",
                DataSource = fieldDs,
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
                ShapeType.RotatedRectangle => "旋转矩形",
                ShapeType.Polygon => "多边形",
                ShapeType.Annulus => "圆环",
                ShapeType.Arc => "弧形",
                _ => shapeType.ToString()
            };
        }
    }
}
