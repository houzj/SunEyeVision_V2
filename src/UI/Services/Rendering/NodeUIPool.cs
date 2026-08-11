using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Rendering
{
    /// <summary>
    /// 节点UI池 - 预创建和复用节点UI元素
    /// 减少节点创建时的UI初始化开销
    /// </summary>
    public class NodeUIPool : IDisposable
    {
        // 可用节点池
        private readonly ConcurrentStack<Border> _availableNodes = new();

        // 已分配节点跟踪
        private readonly ConcurrentDictionary<string, Border> _allocatedNodes = new();

        // 池配置
        private const int DefaultPoolSize = 10;
        // 从 NodeStyleConfig 读取默认值
        private static readonly NodeStyleConfig _styleConfig = NodeStyles.Standard;

        // 预定义画刷
        private static readonly SolidColorBrush _defaultBackground = CachedBrushes.White;
        private static readonly SolidColorBrush _defaultBorderBrush = CachedBrushes.OrangeBorder;

        private bool _disposed;

        /// <summary>
        /// 初始化节点池，预创建指定数量的节点
        /// </summary>
        public void Prewarm(int count = DefaultPoolSize)
        {
            for (int i = 0; i < count; i++)
            {
                var nodeUI = CreateNodeUI();
                _availableNodes.Push(nodeUI);
            }
        }

        /// <summary>
        /// 获取节点UI元素（从池中获取或创建新的）
        /// </summary>
        public Border? GetNodeUI(WorkflowNode node)
        {
            if (_disposed)
                return null;

            Border? nodeUI;

            // 尝试从池中获取
            if (!_availableNodes.TryPop(out nodeUI))
            {
                // 池为空，创建新的
                nodeUI = CreateNodeUI();
            }

            // 配置节点UI
            ConfigureNodeUI(nodeUI, node);

            // 跟踪分配
            _allocatedNodes[node.Id] = nodeUI;

            return nodeUI;
        }

        /// <summary>
        /// 返回节点UI元素到池中
        /// </summary>
        public void ReturnNodeUI(string nodeId)
        {
            if (_disposed)
                return;

            if (_allocatedNodes.TryRemove(nodeId, out var nodeUI))
            {
                // 重置节点UI状态
                ResetNodeUI(nodeUI);

                // 返回池中（限制池大小）
                if (_availableNodes.Count < DefaultPoolSize * 2)
                {
                    _availableNodes.Push(nodeUI);
                }
            }
        }

        /// <summary>
        /// 创建新的节点UI元素
        /// </summary>
        private Border CreateNodeUI()
        {
            var border = new Border
            {
                Width = _styleConfig.NodeWidth,
                Height = _styleConfig.NodeHeight,
                Background = _defaultBackground,
                CornerRadius = new CornerRadius(_styleConfig.CornerRadius),
                BorderThickness = new Thickness(2),
                BorderBrush = _defaultBorderBrush
            };

            // 创建内部Grid
            var grid = new Grid
            {
                Margin = new Thickness(12, 0, 12, 0)
            };

            // 创建列定义
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 创建图标TextBlock
            var iconBlock = new TextBlock
            {
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(iconBlock, 0);
            grid.Children.Add(iconBlock);

            // 创建全局序号TextBlock
            var indexBlock = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 149, 0)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(indexBlock, 1);
            grid.Children.Add(indexBlock);

            // 创建名称TextBlock
            var nameBlock = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameBlock, 2);
            grid.Children.Add(nameBlock);

            border.Child = grid;

            return border;
        }

        /// <summary>
        /// 配置节点UI元素的数据
        /// </summary>
        private void ConfigureNodeUI(Border border, WorkflowNode node)
        {
            // 设置背景和边框颜色
            border.Background = _defaultBackground;
            border.BorderBrush = node.IsSelected ? CachedBrushes.OrangeBorder : _defaultBorderBrush;

            // 设置边框厚度
            border.BorderThickness = node.IsSelected ? new Thickness(3) : new Thickness(2);

            // 配置内部元素
            if (border.Child is Grid grid)
            {
                // 图标
                if (grid.Children[0] is TextBlock iconBlock)
                {
                    iconBlock.Text = node.Icon ?? string.Empty;
                }

                // 全局序号
                if (grid.Children[1] is TextBlock indexBlock)
                {
                    indexBlock.Text = $"{node.GlobalIndex}";
                }

                // 名称
                if (grid.Children[2] is TextBlock nameBlock)
                {
                    nameBlock.Text = node.DispName ?? string.Empty;
                }
            }

            // 存储节点引用
            border.Tag = node;
        }

        /// <summary>
        /// 重置节点UI元素到默认状态
        /// </summary>
        private void ResetNodeUI(Border border)
        {
            border.Background = _defaultBackground;
            border.BorderBrush = _defaultBorderBrush;
            border.BorderThickness = new Thickness(2);
            border.Tag = null;

            // 清除Effect
            border.Effect = null;
        }

        /// <summary>
        /// 获取池统计信息
        /// </summary>
        public (int Available, int Allocated) GetStatistics()
        {
            return (_availableNodes.Count, _allocatedNodes.Count);
        }

        /// <summary>
        /// 清空池
        /// </summary>
        public void Clear()
        {
            while (_availableNodes.TryPop(out _)) { }
            _allocatedNodes.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Clear();
            _disposed = true;
        }
    }
}
