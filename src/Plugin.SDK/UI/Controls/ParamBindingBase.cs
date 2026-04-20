using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;

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
        /// 按节点名称分组，每个节点作为根节点，该节点的属性按 GroupName 分组
        /// 示例：
        /// - 节点A
        ///   - 结果
        ///     - 实际使用的阈值
        ///     - 最大值
        ///   - 执行信息
        ///     - 处理时间戳
        /// - 节点B
        ///   - 图像
        ///     - 宽度
        ///     - 高度
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
                        rootNode.Children.Add(leafNode);
                    }
                    else
                    {
                        // 有分组：查找或创建分组节点
                        if (!groupCache.TryGetValue(dataSource.GroupName, out var groupNode))
                        {
                            groupNode = TreeNodeData.CreateGroupNode(dataSource.GroupName);
                            groupNode.Parent = rootNode;
                            groupNode.BindingPath = string.Empty;
                            rootNode.Children.Add(groupNode);
                            groupCache[dataSource.GroupName] = groupNode;
                        }

                        var leafNode = TreeNodeData.CreateDataSourceNode(dataSource);
                        leafNode.Parent = groupNode;
                        leafNode.BindingPath = dataSource.PropertyName;
                        groupNode.Children.Add(leafNode);
                    }
                }

                rootNodes.Add(rootNode);
            }

            return rootNodes;
        }

        /// <summary>
        /// 根据参数类型过滤数据源
        /// </summary>
        /// <param name="dataSources">数据源集合</param>
        /// <param name="targetType">目标类型（精确类型匹配 + 数组元素类型匹配）</param>
        /// <returns>过滤后的数据源</returns>
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

                // 精确匹配
                if (ds.PropertyType == targetType)
                    return true;

                // 数组元素类型匹配：targetType=double 匹配 double[]
                if (ds.PropertyType.IsArray && ds.PropertyType.GetElementType() == targetType)
                    return true;

                return false;
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
                // 子类可以重写此方法进行额外处理
            }
        }

        #endregion
    }
}
