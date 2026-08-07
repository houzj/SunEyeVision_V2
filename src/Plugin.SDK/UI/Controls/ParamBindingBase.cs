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
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
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
        /// 数组叶子节点显示增强（方案步骤6 / 〇 原则3）：折叠态文本统一为 "名称[]"，展开态物化子节点。
        /// 不改动绑定路径，仅影响树中文本与可展开的子节点。
        /// </summary>
        private static void ApplyArrayDisplay(TreeNodeData leafNode, AvailableDataSource ds)
        {
            if (ds.PropertyType == null || !ds.PropertyType.IsArray)
                return;

            // 所有数组节点折叠文本统一为 "名称[]"，不带类型/数量/摘要（用户要求全部无摘要）
            leafNode.Text = $"{ds.DisplayName}[]";

            var elementType = ds.PropertyType.GetElementType() ?? typeof(object);

            // RoI(ShapeParameters[])：按图形类型分组。有实际值按真实类型分组 + 逐元素 [i]；
            // 无值/空（未运行、未绘制）按 ShapeType 枚举生成全部分组（声明式，不依赖绘制数据）。
            if (elementType == typeof(ShapeParameters))
            {
                var roi = ds.CurrentValue as ShapeParameters[];
                leafNode.Children = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>(
                    roi is { Length: > 0 }
                        ? ArrayTreeFormatter.BuildRoiBindingChildren(ds, roi)
                        : ArrayTreeFormatter.BuildRoiDesignTimeGroups(ds));

                // 方案B：RoI 数组节点是"纯分组容器"，本身不可整体绑定（避免"可绑定 + 可展开"点击冲突）。
                // 整体绑定改由子节点"整体"承担（BindingSuffix=null → 路径 "[]"，等价于绑整个数组）。
                // IsSelectable=false 时点击不触发绑定，展开箭头默认行为正常执行，分组子节点仍可绑定。
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
        /// 3.5 目标词汇为几何图形（Circle/RotatedRect/Point/Polygon/Line/Annulus）时，
        ///     RoI 容器数据源（ShapeParameters[]）放行——其内部按 [Type=X] 分组可下钻到具体图形。
        /// 4. 其余词汇（Image/String/RoI/Transform/Boolean）→ 精确匹配，不互通，避免运行时类型错误。
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
                // Blob 检测区/屏蔽区图形以 RoI（ShapeParameters[]）输出，按 [Type=X] 分组可下钻
                // 到具体图形（圆/旋转矩形/矩形等）。目标词汇为几何图形时放行 RoI 容器，
                // 让绑定下拉能展开并精确选中对应图形分组（否则整条数据源被滤掉，用户看不到）。
                if (dsVarType == "RoI" && GeometryVocabularies.Contains(targetVarType))
                    return true;

                // 规则 4：其余词汇精确匹配（上一步已判等），不互通 → 不显示
                return false;
            }).ToList();
        }

        private static bool IsNumericVocabulary(string variableType)
            => variableType == "Integer" || variableType == "Double";

        /// <summary>
        /// 几何图形词汇集合（Circle/RotatedRect/Point/Polygon/Line/Annulus）。
        /// 当显示项目标词汇是几何图形时，RoI 容器数据源（ShapeParameters[]，词汇 RoI）应放行，
        /// 因其内部按 [Type=X] 分组可精确下钻到对应图形（Blob 检测区图形 → 圆形/旋转矩形等）。
        /// </summary>
        private static readonly HashSet<string> GeometryVocabularies = new(StringComparer.Ordinal)
        {
            "Circle", "RotatedRect", "Point", "Polygon", "Line", "Annulus",
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
