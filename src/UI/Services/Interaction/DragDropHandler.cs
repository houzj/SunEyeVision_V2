using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Core;
using SunEyeVision.UI;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.UI.Services.Canvas;
using ControlsCanvas = System.Windows.Controls.Canvas;

using SunEyeVision.UI.Shell.Controls.Toolbox;

using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Services.Interaction
{
    /// <summary>
    /// 工作流拖放处理器
    /// 负责从工具箱拖放节点到画布
    /// </summary>
    public class WorkflowDragDropHandler
    {
        private readonly WorkflowCanvasControl _canvasControl;

#if DEBUG
        private static DateTime _lastLogTime = DateTime.Now;
        private static DateTime _dropStartTime = DateTime.Now; // Drop 操作开始时间，用于累计时间计算
#endif

        // 直接引用注入（优先使用，O(1) 访问）
        private WorkflowTabControlViewModel? _tabViewModel;

        // 缓存 ViewModel 引用，避免每次通过 Application.Current 获取（回退方案）
        private static MainWindowViewModel? _cachedViewModel;
        private static DateTime _viewModelCacheTime = DateTime.MinValue;
        private static readonly TimeSpan _cacheExpiry = TimeSpan.FromSeconds(30);

        // 临时拖拽节点（拖拽工具箱工具时跟随鼠标的视觉元素，完全复刻真实节点外观）
        private Border? _tempDragNode;
        private const double _tempNodeWidth = 140;   // 匹配 NodeWidth
        private const double _tempNodeHeight = 36;   // 匹配 NodeHeight

        /// <summary>
        /// 工作流标签页管理器（属性注入，在 Canvas Loaded 后设置）
        /// </summary>
        public WorkflowTabControlViewModel? TabViewModel
        {
            get => _tabViewModel;
            set => _tabViewModel = value;
        }

#if DEBUG
        /// <summary>
        /// 带时间戳的调试日志（仅DEBUG模式）
        /// </summary>
        private static void LogTimestamp(string tag, string message)
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastLogTime).TotalMilliseconds;
            _lastLogTime = now;
            var totalElapsed = (now - _dropStartTime).TotalMilliseconds;
}
#endif

        /// <summary>
        /// 获取缓存的 MainWindowViewModel（带过期时间）
        /// </summary>
        private static MainWindowViewModel? GetCachedViewModel()
        {
            // 检查缓存是否有效
            if (_cachedViewModel != null && (DateTime.Now - _viewModelCacheTime) < _cacheExpiry)
            {
                return _cachedViewModel;
            }

            // 重新获取并缓存：遍历所有窗口找 MainWindow（比 Application.Current.MainWindow
            // 更可靠，规避多窗口/时序场景下 MainWindow 属性为 null 或指向非主窗口的问题）。
            if (Application.Current != null)
            {
                foreach (var window in Application.Current.Windows)
                {
                    if (window is MainWindow mainWindow
                        && mainWindow.DataContext is MainWindowViewModel vm)
                    {
                        _cachedViewModel = vm;
                        _viewModelCacheTime = DateTime.Now;
                        break;
                    }
                }
            }

            // 根因显性化（rule-015 不静默回退）：获取失败必须暴露，而非让调用方裸操作集合绕过业务管道。
            // 节点添加必须经 AddNodeToWorkflow 统一处理（日志/命令/绑定/预编译），缓存失败即报错。
            if (_cachedViewModel == null)
            {
                throw new InvalidOperationException(
                    "无法获取 MainWindowViewModel，节点添加无法走统一管道。应修复 ViewModel 获取逻辑，而非回退裸操作集合。");
            }

            return _cachedViewModel;
        }

        public WorkflowDragDropHandler(
            WorkflowCanvasControl canvasControl)
        {
            _canvasControl = canvasControl;
        }

        /// <summary>
        /// 显示临时节点（完全复刻真实节点的胶囊外观，半透明 + 蓝色虚线边框）
        /// </summary>
        private void ShowTempNode(ToolItem item, Point position, ControlsCanvas canvas)
        {
            if (_tempDragNode != null)
            {
                _tempDragNode.Visibility = Visibility.Visible;
                ControlsCanvas.SetLeft(_tempDragNode, position.X - _tempNodeWidth / 2);
                ControlsCanvas.SetTop(_tempDragNode, position.Y - _tempNodeHeight / 2);

                // 更新图标和名称（重用已有节点）
                if (_tempDragNode.Child is StackPanel panel && panel.Children.Count >= 2)
                {
                    if (panel.Children[0] is TextBlock iconBlock)
                        iconBlock.Text = item.Icon;
                    if (panel.Children[1] is TextBlock nameBlock)
                        nameBlock.Text = item.Name;
                }
                return;
            }

            // 内容面板：图标 + 名称
            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            contentPanel.Children.Add(new TextBlock
            {
                Text = item.Icon,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = item.Name,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            // 创建节点外观的 Border（完全复刻真实节点的胶囊外形）
            _tempDragNode = new Border
            {
                Width = _tempNodeWidth,
                Height = _tempNodeHeight,
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(Color.FromArgb(191, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(128, 0x00, 0x66, 0xCC)),
                BorderThickness = new Thickness(1),
                Opacity = 0.75,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 8,
                    Opacity = 0.3,
                    ShadowDepth = 2
                },
                Child = contentPanel
            };

            ControlsCanvas.SetZIndex(_tempDragNode, 10000);
            ControlsCanvas.SetLeft(_tempDragNode, position.X - _tempNodeWidth / 2);
            ControlsCanvas.SetTop(_tempDragNode, position.Y - _tempNodeHeight / 2);
            canvas.Children.Add(_tempDragNode);
        }

        /// <summary>
        /// 更新临时节点位置（跟随鼠标）
        /// </summary>
        private void UpdateTempNodePosition(Point position)
        {
            if (_tempDragNode != null)
            {
                ControlsCanvas.SetLeft(_tempDragNode, position.X - _tempNodeWidth / 2);
                ControlsCanvas.SetTop(_tempDragNode, position.Y - _tempNodeHeight / 2);
            }
        }

        /// <summary>
        /// 隐藏临时节点
        /// </summary>
        private void HideTempNode()
        {
            if (_tempDragNode != null)
            {
                _tempDragNode.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 拖放进入事件 - 显示临时节点
        /// </summary>
        public void Canvas_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ToolItem"))
            {
                e.Effects = DragDropEffects.Copy;

                // 显示临时节点（跟随鼠标）
                if (sender is ControlsCanvas canvas && e.Data.GetData("ToolItem") is ToolItem item)
                {
                    var position = e.GetPosition(canvas);
                    ShowTempNode(item, position, canvas);
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        /// <summary>
        /// 拖放悬停事件 - 更新临时节点位置
        /// </summary>
        public void Canvas_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ToolItem"))
            {
                e.Effects = DragDropEffects.Copy;

                // 更新临时节点位置
                if (_tempDragNode != null && sender is ControlsCanvas canvas)
                {
                    var position = e.GetPosition(canvas);
                    UpdateTempNodePosition(position);
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        /// <summary>
        /// 拖放离开事件 - 隐藏临时节点
        /// </summary>
        public void Canvas_DragLeave(object sender, DragEventArgs e)
        {
            HideTempNode();
            e.Handled = true;
        }

        /// <summary>
        /// 拖放放下事件 - 创建新节点
        /// </summary>
        public void Canvas_Drop(object sender, DragEventArgs e)
        {
#if DEBUG
            _lastLogTime = DateTime.Now; // 重置计时器
            _dropStartTime = DateTime.Now; // 重置累计时间起点
LogTimestamp("Drop", "▶ Canvas_Drop 开始执行");
            LogTimestamp("Drop", $"  Sender: {sender?.GetType().Name ?? "null"}");
#endif

            try
            {
                // 隐藏临时节点（即将创建真正的节点）
                HideTempNode();

                // 检查 sender
                if (sender is not System.Windows.Controls.Canvas canvas)
                {
#if DEBUG
                    LogTimestamp("Drop", "✗ Sender 不是 Canvas 类型");
#endif
                    return;
                }
#if DEBUG
                LogTimestamp("Drop", "✓ Canvas 获取成功");
#endif

                // 检查拖拽数据
                var toolItemData = e.Data.GetData("ToolItem");
#if DEBUG
                LogTimestamp("Drop", $"GetData(\"ToolItem\") 类型: {toolItemData?.GetType().Name ?? "null"}");
#endif

                if (toolItemData is not ToolItem item)
                {
#if DEBUG
                    LogTimestamp("Drop", "✗ 数据不是 ToolItem 类型");
#endif
                    return;
                }

                // 获取放置位置
                Point dropPosition = e.GetPosition(canvas);
#if DEBUG
                LogTimestamp("Drop", $"放置位置: ({dropPosition.X:F1}, {dropPosition.Y:F1})");
                LogTimestamp("Drop", $"ToolItem: Name={item.Name}, ToolId={item.ToolId}");
#endif

                if (string.IsNullOrEmpty(item.ToolId))
                {
#if DEBUG
                    LogTimestamp("Drop", "✗ ToolId 为空");
#endif
                    return;
                }

                // 从 MainWindow 动态获取当前选中的工作流
#if DEBUG
                LogTimestamp("Drop", "▶ 获取当前工作流 Tab...");
#endif
                WorkflowTabViewModel workflowTab = GetCurrentWorkflowTab();
                if (workflowTab == null)
                {
#if DEBUG
                    LogTimestamp("Drop", "✗ 无法获取当前工作流Tab");
#endif
                    MessageBox.Show("无法获取当前工作流，请确保已打开工作流标签页", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
#if DEBUG
                LogTimestamp("Drop", $"✓ 工作流 Tab 获取成功: {workflowTab.Name}, 节点数={workflowTab.WorkflowNodes.Count}");
#endif

                // 注意：Drop 创建节点不计为选中节点，用户再次点击才算点击节点
                // 因此不自动选中、不设置 IsSelected、不设置 SelectedNode

                // 使用 ViewModel 的 CreateNode 方法创建节点
#if DEBUG
                LogTimestamp("Drop", "▶ 调用 WorkflowTabViewModel.CreateNode...");
#endif
                var newNode = workflowTab.CreateNode(item.ToolId, item.Name);
#if DEBUG
                LogTimestamp("Drop", $"✓ 节点创建完成: Id={newNode.Id}, Name={newNode.Name}");
#endif

                // 应用边界限制（使用新的统一方法，默认策略为 Both - 画布和视口都限制）
                newNode.Position = CanvasHelper.ClampNodeToBounds(newNode, dropPosition, canvas);
#if DEBUG
                LogTimestamp("Drop", "✓ 节点属性设置完成");
#endif

                // 添加新节点（通过 MainWindowViewModel.AddNodeToWorkflow 以记录日志）
#if DEBUG
                LogTimestamp("Drop", "▶ 添加节点到集合...");
#endif
                var viewModel = GetCachedViewModel();
                // 统一管道：节点添加必须经 AddNodeToWorkflow（日志/命令/绑定/预编译）。
                // viewModel 由 GetCachedViewModel 保证非 null（失败即抛异常，无回退分支）。
                viewModel.AddNodeToWorkflow(newNode);
#if DEBUG
                LogTimestamp("Drop", $"✓ 节点已添加, 当前节点数: {workflowTab.WorkflowNodes.Count}");
                LogTimestamp("Drop", "═══ 节点添加完成 ═══");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                LogTimestamp("Drop", $"✗✗✗ 异常: {ex.Message}");
#endif
                MessageBox.Show($"拖放节点失败: {ex.Message}\n\n类型: {ex.GetType().Name}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取当前选中的工作流 Tab（优先使用直接引用）
        /// </summary>
        private WorkflowTabViewModel? GetCurrentWorkflowTab()
        {
            try
            {
                // 优先使用直接引用（O(1) 访问）
                if (_tabViewModel != null)
                {
                    var selectedTab = _tabViewModel.SelectedTab;
                    if (selectedTab != null)
                    {
#if DEBUG
                        LogTimestamp("GetTab", $"✓ 通过直接引用获取成功: {selectedTab.Name}");
#endif
                        return selectedTab;
                    }
                }

                // 回退：使用缓存方式
                var viewModel = GetCachedViewModel();
                if (viewModel == null)
                {
#if DEBUG
                    LogTimestamp("GetTab", "✗ 缓存的 ViewModel 为 null");
#endif
                    return null;
                }

                var tab = viewModel.WorkflowTabViewModel?.SelectedTab;
                if (tab != null)
                {
#if DEBUG
                    LogTimestamp("GetTab", $"✓ 通过缓存获取成功: {tab.Name}");
#endif
                    return tab;
                }

#if DEBUG
                LogTimestamp("GetTab", "✗ SelectedTab 为 null");
#endif
                return null;
            }
            catch (Exception ex)
            {
#if DEBUG
                LogTimestamp("GetTab", $"✗ 异常: {ex.Message}");
#endif
                return null;
            }
        }
    }
}