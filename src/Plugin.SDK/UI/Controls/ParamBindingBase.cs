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
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

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
        /// <returns>树形结构的根节点列表</returns>
        public static List<TreeNodeData> BuildTreeStructure(List<AvailableDataSource> dataSources)
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
                        ApplyArrayDisplay(leafNode, dataSource);
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
                        ApplyArrayDisplay(leafNode, dataSource);
                        currentParent.Children.Add(leafNode);
                    }
                }

                rootNodes.Add(rootNode);
            }

            return rootNodes;
        }

        /// <summary>
        /// 数组叶子节点显示增强（方案步骤6 / 〇 原则3）：折叠态单行 `;` 串联，展开态物化子节点。
        /// 不改动绑定路径，仅影响树中文本与可展开的子节点。
        /// </summary>
        private static void ApplyArrayDisplay(TreeNodeData leafNode, AvailableDataSource ds)
        {
            if (ds.PropertyType == null || !ds.PropertyType.IsArray)
                return;
            if (ds.CurrentValue is not Array arr)
                return;

            var elementType = ds.PropertyType.GetElementType() ?? typeof(object);
            var n = arr.Length;

            // RoI(ShapeParameters[])：绑定树按图形类型分组 + 逐元素 [i]，每组/元素可单独绑定
            if (elementType == typeof(ShapeParameters) && arr is ShapeParameters[] roi)
            {
                var roiTypeLabel = (ds.VariableType ?? "RoI") + "[]";
                var roiSummary = ArrayTreeFormatter.FormatCollapsedSummary(arr);
                leafNode.Text = $"{ds.DisplayName} ({roiTypeLabel}, {n}项): {roiSummary}";
                leafNode.Children = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>(
                    ArrayTreeFormatter.BuildRoiBindingChildren(ds, roi));
                return;
            }

            // 类型标签：词汇名 + "[]"（如 "Region[]" / "Double[]" / "RoI[]"）。
            // 优先用词汇名（VariableType），否则用元素 CLR 类型名取基础名（避免 "IVisionRegion[][]" 双括号）。
            var elementName = elementType.Name;
            var typeLabel = (ds.VariableType ?? elementName) + "[]";
            var summary = ArrayTreeFormatter.FormatCollapsedSummary(arr);
            leafNode.Text = $"{ds.DisplayName} ({typeLabel}, {n}项): {summary}";
            leafNode.Children = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>(
                ArrayTreeFormatter.BuildExpansionChildren(arr, elementType));
        }

        /// <summary>
        /// 根据参数类型过滤数据源
        /// </summary>
        /// <param name="dataSources">数据源集合</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>过滤后的数据源</returns>
        /// <remarks>
        /// 呈现层严格规则（rule-008 零兼容 + 视觉软件行业标准）：
        /// 1. 数组参数 = 只显示数组（不显示标量元素，避免"数组字段选了标量"的歧义）
        /// 2. 标量参数 = 数组不显示（避免"标量字段选了数组"的歧义）
        /// 3. 数字标量 = 跨类型可见（int 字段能看到 double/long/short 等上游，
        ///    绑定执行时 ScriptBindingResolver.CoerceValue 会用 Convert.ChangeType 转）
        /// 4. 其他类型 = 精确类型匹配（ShapeParameters 之间不互通，避免运行时类型错误）
        ///
        /// 手动路径：客户仍可在 BindingSource 文本框中强行写非筛选项，
        /// ScriptBindingResolver 会按既定规则做类型转换或抛错。
        /// </remarks>
        public static List<AvailableDataSource> FilterDataSourcesByType(List<AvailableDataSource> dataSources, Type targetType)
        {
            if (targetType == null)
            {
                // 未指定类型：返回所有数据源
                return dataSources;
            }

            return dataSources.Where(ds =>
            {
                if (ds.PropertyType == null)
                    return false;

                // 规则 1：数组参数 = 只显示数组
                if (targetType.IsArray)
                    return ds.PropertyType.IsArray;

                // 规则 2：标量参数 = 数组不显示
                if (ds.PropertyType.IsArray)
                    return false;

                // 规则 3：数字标量 = 跨类型可见
                if (NumericTypes.IsNumericType(ds.PropertyType)
                    && NumericTypes.IsNumericType(targetType))
                    return true;

                // 规则 4：其他 = 精确类型匹配
                return ds.PropertyType == targetType;
            }).ToList();
        }

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
