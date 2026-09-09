using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 参数绑定基类，提供数据源管理、树形结构构建、类型过滤等公共功能
    /// </summary>
    /// <remarks>
    /// V2.0 设计原则：
    /// 1. ParamBinding 继承此基类，专注于参数绑定功能
    /// 2. ConfigSetting 继承此基类，专注于参数值编辑功能
    /// 3. 基类提供公共的树形结构构建和类型过滤方法
    /// </remarks>
    public abstract class ParamBindingBase : Control, INotifyPropertyChanged
    {
        #region 依赖属性

        /// <summary>
        /// 数据类型过滤器
        /// </summary>
        public static readonly DependencyProperty DataTypeProperty =
            DependencyProperty.Register(
                nameof(DataType),
                typeof(Type),
                typeof(ParamBindingBase),
                new PropertyMetadata(null, OnDataTypeChanged));

        #endregion

        #region 公共属性

        /// <summary>
        /// 数据类型过滤器（精确类型）
        /// </summary>
        public Type DataType
        {
            get => (Type)GetValue(DataTypeProperty);
            set => SetValue(DataTypeProperty, value);
        }

        #endregion

        #region 事件

        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region 构造函数

        static ParamBindingBase()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ParamBindingBase),
                new FrameworkPropertyMetadata(typeof(ParamBindingBase)));
        }

        protected ParamBindingBase()
        {
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取可用的数据源（抽象方法，由子类实现）
        /// </summary>
        /// <returns>数据源集合</returns>
        public abstract List<AvailableDataSource> GetAvailableDataSources();

        /// <summary>
        /// 从数据源列表构建树形结构
        /// </summary>
        /// <remarks>
        /// 按节点名称分组，每个节点作为根节点，该节点的属性按 GroupName 分组。
        /// GroupName 支持 "/" 分隔的层级路径，例如 "Blob详细/边框" 会在 "Blob详细" 
        /// 分组下创建 "边框" 子分组。
        /// 示例：
        /// - 节点A
        ///   - 结果
        ///     - 实际使用的阈值
        ///     - 最大值
        ///   - Blob详细
        ///     - 面积
        ///     - 中心
        ///       - 中心X
        ///       - 中心Y
        ///     - 边框
        ///       - 边框宽
        ///       - 边框高
        ///   - 执行信息
        ///     - 处理时间戳
        /// </remarks>
        /// <param name="dataSources">数据源列表</param>
        /// <param name="targetType">目标参数类型（可选）。RoiShape[] 子树的形态按它定型：
        /// 数值目标只出类型容器+字段叶；ROI/几何/未指定只出整体+类型集合叶。</param>
        /// <returns>树形结构的根节点列表</returns>
        public static List<TreeNodeData> BuildTreeStructure(List<AvailableDataSource> dataSources, Type? targetType = null)
        {
            var rootNodes = new List<TreeNodeData>();
            var nodeGroups = dataSources.GroupBy(ds => ds.SourceNodeName);

            foreach (var nodeGroup in nodeGroups)
            {
                var rootNode = TreeNodeData.CreateGroupNode(nodeGroup.Key);
                var groupCache = new Dictionary<string, TreeNodeData>();

                foreach (var dataSource in nodeGroup)
                {
                    if (string.IsNullOrEmpty(dataSource.GroupName))
                    {
                        // 无分组：直接添加为叶子节点
                        var leafNode = TreeNodeData.CreateDataSourceNode(dataSource);
                        leafNode.Parent = rootNode;
                        leafNode.BindingPath = dataSource.PropertyName;
                        ApplyArrayDisplay(leafNode, dataSource, targetType);
                        rootNode.Children.Add(leafNode);
                    }
                    else
                    {
                        // 支持 "/" 分隔的层级分组，逐层创建/查找分组节点
                        var groupNames = dataSource.GroupName.Split('/');
                        TreeNodeData currentParent = rootNode;

                        for (int i = 0; i < groupNames.Length; i++)
                        {
                            var cacheKey = string.Join("/", groupNames.Take(i + 1));

                            if (!groupCache.TryGetValue(cacheKey, out var groupNode))
                            {
                                groupNode = TreeNodeData.CreateGroupNode(groupNames[i]);
                                groupNode.Parent = currentParent;
                                groupNode.BindingPath = string.Empty;
                                currentParent.Children.Add(groupNode);
                                groupCache[cacheKey] = groupNode;
                            }

                            currentParent = groupNode;
                        }

                        var leafNode = TreeNodeData.CreateDataSourceNode(dataSource);
                        leafNode.Parent = currentParent;
                        leafNode.BindingPath = dataSource.PropertyName;
                        ApplyArrayDisplay(leafNode, dataSource, targetType);
                        currentParent.Children.Add(leafNode);
                    }
                }

                rootNodes.Add(rootNode);
            }

            return rootNodes;
        }

        /// <summary>
        /// 数组叶子节点显示增强（方案步骤6 / 〇 原则3）：折叠态文本统一为 "名称[]"，展开态物化子节点。
        /// 不改动绑定路径，仅影响树中文本与可展开的子节点。
        /// RoiShape[] 子树形态由 targetType 词目定型（ArrayTreeFormatter.BuildRoiTypedChildren）。
        /// </summary>
        private static void ApplyArrayDisplay(TreeNodeData leafNode, AvailableDataSource ds, Type? targetType)
        {
            if (ds.PropertyType == null || !ds.PropertyType.IsArray)
                return;

            // 所有数组节点折叠文本统一为 "名称[]"，不带类型/数量/摘要（用户要求全部无摘要）
            leafNode.Text = $"{ds.DisplayName}[]";

            var elementType = ds.PropertyType.GetElementType() ?? typeof(object);

            // ROI(RoiShape[])：树形由目标参数词目定型（声明式 ShapeType 枚举 + RoiFieldSchema，
            // 不依赖运行值，设计期/运行期结构一致；绑定终点必为叶子，类型节点只导航）。
            if (elementType == typeof(RoiShape))
            {
                var targetVarType = targetType == null
                    ? null
                    : SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToTypeVocabulary(targetType);
                var roiChildren = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>(
                    ArrayTreeFormatter.BuildRoiTypedChildren(ds, targetVarType));

                // P1：给 RoI 子节点统一补 Parent 链，否则 GetRootNode 向上遍历到 null 时把分组
                // 自身当根（显示名根节点取成"圆"）。递归设置，覆盖整体/类型集合叶/字段叶。
                foreach (var child in roiChildren)
                    AttachParentChain(child, leafNode);

                leafNode.Children = roiChildren;

                // RoI 数组节点是"纯分组容器"：自身不绑定，绑定终点由子节点（整体/类型集合叶/字段叶）承担。
                leafNode.IsSelectable = false;
                return;
            }

            if (ds.CurrentValue is not Array arr)
                return;

            // 其他数组：逐元素 [i]
            leafNode.Children = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>(
                ArrayTreeFormatter.BuildExpansionChildren(arr, elementType));
        }

        /// <summary>
        /// 递归为子节点设置 Parent 引用（P1）。绑定树子节点由 ArrayTreeFormatter 构建时未挂 Parent，
        /// 导致 GetRootNode 向上遍历到 null 时把分组自身当根。此方法自底向上补齐整条链。
        /// </summary>
        private static void AttachParentChain(TreeNodeData node, TreeNodeData parent)
        {
            node.Parent = parent;
            if (node.Children == null)
                return;
            foreach (var child in node.Children)
                AttachParentChain(child, node);
        }

        /// <summary>
        /// 根据参数类型过滤数据源（统一走词汇表匹配，与 DataSourceQueryService 同口径）
        /// </summary>
        /// <param name="dataSources">数据源集合</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>过滤后的数据源</returns>
        /// <remarks>
        /// 呈现层规则（rule-008 零兼容 + 视觉软件行业标准，对齐 DataSourceQueryService）：
        /// 1. 统一通过 SdkTypeVocabulary.ToTypeVocabulary 把参数类型与数据源类型映射到词汇，
        ///    标量 T 与数组 T[] 归并到同一词汇（单值 = 长度 1 数组，对齐 Halcon tuple 语义）。
        /// 2. 词汇相等 → 显示（如 int 参数 → Integer 词汇，能看到上游 long[]/int[] 的 Integer 输出）。
        /// 3. 数字互通 → 显示（Integer 与 Double 跨类型可见，执行层 Convert.ChangeType 负责转换）。
        /// 3.5 目标词汇为几何图形（Circle/RotatedRect/Point/Polygon/Line/Annulus/Arc）时，
        ///     ROI 容器数据源（RoiShape[]）放行——其内部按 [Type=X] 分组可下钻到具体图形。
        /// 4. 其余词汇（Image/String/ROI/Transform/Boolean）→ 精确匹配，不互通，避免运行时类型错误。
        /// 5. 参数类型无法映射到词汇（未注册类型）→ 不显示任何上游候选，避免错误绑定。
        ///
        /// 手动路径：客户仍可在 BindingSource 文本框中强行写非筛选项，
        /// 绑定执行层会按既定规则做类型转换或抛错。
        /// </remarks>
        public static List<AvailableDataSource> FilterDataSourcesByType(List<AvailableDataSource> dataSources, Type targetType)
        {
            if (targetType == null)
            {
                // 未指定类型：返回所有数据源
                return dataSources;
            }

            var targetVarType = SdkTypeVocabulary.ToTypeVocabulary(targetType);

            return dataSources.Where(ds =>
            {
                if (ds.PropertyType == null)
                    return false;

                var dsVarType = SdkTypeVocabulary.ToTypeVocabulary(ds.PropertyType);

                // 数据源类型无法映射到词汇：不显示（rule-015 未映射类型不静默放行）
                if (dsVarType == null)
                    return false;

                // 参数类型无法映射到词汇：不显示任何候选
                if (targetVarType == null)
                    return false;

                // 规则 2：词汇相等 → 显示
                if (dsVarType == targetVarType)
                    return true;

                // 规则 3：数字互通（Integer ↔ Double）→ 显示
                if (IsNumericVocabulary(dsVarType) && IsNumericVocabulary(targetVarType))
                    return true;

                // 规则 3.5：几何图形目标放行 RoI 容器数据源。
                // Blob 检测区/屏蔽区图形以 ROI（RoiShape[]）输出，按 [Type=X] 分组可下钻
                // 到具体图形（圆/旋转矩形/矩形等）。目标词汇为几何图形时放行 ROI 容器，
                // 让绑定下拉能展开并精确选中对应图形分组（否则整条数据源被滤掉，用户看不到）。
                if (dsVarType == "ROI" && GeometryVocabularies.Contains(targetVarType))
                    return true;

                // 规则 3.6：数值目标（Integer/Double）放行 RoI 容器数据源。
                // ROI 容器可下钻到其内几何的数值字段（如 [0].Geometry.Width/Center.X，经 RoiFieldSchema 声明）。
                // 仅放行容器本身，真正选中时仍校验叶子类型与目标匹配（数值互通，rule-015 报错优先）。
                if (dsVarType == "ROI" && IsNumericVocabulary(targetVarType))
                    return true;

                // 规则 4：其余词汇精确匹配（上一步已判等），不互通 → 不显示
                return false;
            }).ToList();
        }

        private static bool IsNumericVocabulary(string variableType)
            => variableType == "Integer" || variableType == "Double";

        /// <summary>
        /// 几何图形词汇集合（Circle/RotatedRect/Point/Polygon/Line/Annulus/Arc）。
        /// 当显示项目标词汇是几何图形时，ROI 容器数据源（RoiShape[]，词汇 ROI）应放行，
        /// 因其内部按 [Type=X] 分组可精确下钻到对应图形（Blob 检测区图形 → 圆形/旋转矩形等）。
        /// </summary>
        private static readonly HashSet<string> GeometryVocabularies = new(StringComparer.Ordinal)
        {
            "Circle", "RotatedRect", "Point", "Polygon", "Line", "Annulus", "Arc",
        };

        #endregion

        #region 受保护方法

        /// <summary>
        /// 触发属性更改事件
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region 事件处理程序

        private static void OnDataTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ParamBindingBase control)
            {
                control.OnDataTypeChangedCore(e);
            }
        }

        /// <summary>
        /// DataType 变更时的虚方法，子类可覆写以响应数据类型变化
        /// </summary>
        protected virtual void OnDataTypeChangedCore(DependencyPropertyChangedEventArgs e)
        {
        }

        #endregion
    }
}
