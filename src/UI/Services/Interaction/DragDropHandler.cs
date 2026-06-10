using System;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.UI.Models;
using SunEyeVision.UI.ViewModels;
using SunEyeVision.UI;
using SunEyeVision.UI.Views.Controls.Canvas;
using SunEyeVision.UI.Views.Windows;
using SunEyeVision.UI.Services.Canvas;

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

            // 重新获取并缓存
            if (Application.Current?.MainWindow is MainWindow mainWindow)
            {
                _cachedViewModel = mainWindow.DataContext as MainWindowViewModel;
                _viewModelCacheTime = DateTime.Now;
            }

            return _cachedViewModel;
        }

        public WorkflowDragDropHandler(
            WorkflowCanvasControl canvasControl)
        {
            _canvasControl = canvasControl;
        }

        /// <summary>
        /// 拖放进入事件
        /// </summary>
        public void Canvas_DragEnter(object sender, DragEventArgs e)
        {
#if DEBUG
#endif
            if (e.Data.GetDataPresent("ToolItem"))
            {
                e.Effects = DragDropEffects.Copy;
#if DEBUG
#endif
            }
            else
            {
                e.Effects = DragDropEffects.None;
#if DEBUG
#endif
            }
            e.Handled = true;
        }

        /// <summary>
        /// 拖放悬停事件
        /// </summary>
        public void Canvas_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ToolItem"))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        /// <summary>
        /// 拖放离开事件
        /// </summary>
        public void Canvas_DragLeave(object sender, DragEventArgs e)
        {
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
                if (viewModel != null)
                {
                    viewModel.AddNodeToWorkflow(newNode);
                }
                else
                {
                    // 回退方案：直接添加（不记录日志）
                    workflowTab.WorkflowNodes.Add(newNode);
                }
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