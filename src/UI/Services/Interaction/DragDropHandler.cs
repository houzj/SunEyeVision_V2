using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.Dialogs;
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

        // 缓存 ViewModel 引用，避免每次通过 System.Windows.Application.Current 获取（回退方案）


        // 临时拖拽节点（拖拽工具箱工具时跟随鼠标的视觉元素，完全复刻真实节点外观）
        private Border? _tempDragNode;
        private const double _tempNodeWidth = 110;   // 匹配 NodeWidth
        private const double _tempNodeHeight = 30;   // 匹配 NodeHeight

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
        private MainWindowViewModel GetOwningViewModel() => Window.GetWindow(_canvasControl)?.DataContext as MainWindowViewModel
            ?? throw new InvalidOperationException("当前画布没有有效宿主窗口。");

        public WorkflowDragDropHandler(
            WorkflowCanvasControl canvasControl)
        {
            _canvasControl = canvasControl;
        }

        /// <summary>
        /// 显示临时节点（完全复刻真实节点的胶囊外观，半透明 + 蓝色虚线边框）
        /// </summary>
        internal static Point SnapCanvasPoint(Point point)=>new(Math.Round(point.X/5)*5,Math.Round(point.Y/5)*5);
        private void ShowTempNode(ToolItem item,Point position,ControlsCanvas canvas)
        {
            if(_tempDragNode?.Tag is string id&&id==item.ToolId&&ReferenceEquals(_tempDragNode.Parent,canvas))
            {_tempDragNode.Visibility=Visibility.Visible;UpdateTempNodePosition(position);return;}
            if(_tempDragNode?.Parent is ControlsCanvas previous)previous.Children.Remove(_tempDragNode);
            var preview = new ToolboxNodePreview(item);
            var presenter = new ContentPresenter { Content = preview, ContentTemplate = _canvasControl.GetCanvasNodeTemplate() };
            _tempDragNode=new Border{Width=preview.HitAreaWidth,Height=preview.HitAreaHeight,Opacity=.7,
                Child=presenter,Tag=item.ToolId,IsHitTestVisible=false};
            ControlsCanvas.SetZIndex(_tempDragNode,10000);canvas.Children.Add(_tempDragNode);UpdateTempNodePosition(position);
        }
        // 拖放预览仅包含模板需要的展示值，不创建或模拟正式节点身份。
        private sealed class ToolboxNodePreview(ToolItem item) : SunEyeVision.UI.Features.WorkflowUI.Canvas.ICanvasToolIconSource
        {
            public string ToolType => item.ToolId;
            public string DispName => item.Name;
            public string? ToolIconKey => item.NodeIconKey?.Invoke();
            // The shared node template creates a TwoWay TextBox binding even while hidden.
            // This is only local preview text; it never edits a document node.
            public string RenameBuffer { get; set; } = item.Name;
            public bool IsToolboxPreview => true;
            public int GlobalIndex => -1;
            public bool IsEnabled => true;
            public bool IsSelected => false;
            public bool IsEditingName => false;
            public bool IsGroupLoopEnabled => false;
            public bool IsSubroutineNode => string.Equals(ToolType, "Subroutine", StringComparison.OrdinalIgnoreCase);
            private SunEyeVision.UI.Shared.NodeStyleConfig Style => IsSubroutineNode ? SunEyeVision.UI.Shared.NodeStyles.Group : SunEyeVision.UI.Shared.NodeStyles.Standard;
            public double VisualWidth => Style.NodeWidth;
            public double VisualHeight => Style.NodeHeight;
            public double HitAreaWidth => VisualWidth + 20;
            public double HitAreaHeight => VisualHeight + 20;
        }
        /// <summary>
        /// 更新临时节点位置（跟随鼠标）
        /// </summary>
        private void UpdateTempNodePosition(Point position)
        {
            position=SnapCanvasPoint(position);
            _canvasControl.UpdateCanvasPlacementFeedback(position, _tempNodeHeight);
            _canvasControl.UpdateCanvasInsertionWire(position, Array.Empty<string>());
            if (_tempDragNode != null)
            {
                ControlsCanvas.SetLeft(_tempDragNode, position.X - _tempDragNode.Width / 2);
                ControlsCanvas.SetTop(_tempDragNode, position.Y - _tempDragNode.Height / 2);
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
            if (!e.Data.GetDataPresent("ToolItem")) return;
            if (e.Data.GetData("ToolItem") is ToolItem {IsAvailable:true} active && active.CanActivate())
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
            if (!e.Data.GetDataPresent("ToolItem")) return;
            if (e.Data.GetData("ToolItem") is ToolItem {IsAvailable:true} active && active.CanActivate())
            {
                e.Effects = DragDropEffects.Copy;

                // 更新临时节点位置
                if (sender is ControlsCanvas canvas)
                {
                    var position = e.GetPosition(canvas);
                    ShowTempNode(active, position, canvas);
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                HideTempNode();
                _canvasControl.ClearCanvasPlacementFeedback(); _canvasControl.ClearCanvasInsertionWire();
            }
            e.Handled = true;
        }

        /// <summary>
        /// 拖放离开事件 - 隐藏临时节点
        /// </summary>
        public void Canvas_DragLeave(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("ToolItem")) return;
            HideTempNode();
            _canvasControl.ClearCanvasPlacementFeedback(); _canvasControl.ClearCanvasInsertionWire();
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
                // 落点处理结束后统一清理幽灵与热区。

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

                if (toolItemData is not ToolItem {IsAvailable:true} item || !item.CanActivate())
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
                    ThemeMessageDialog.Show("无法获取当前工作流，请确保已打开工作流标签页", "错误",
                        MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Warning);
                    return;
                }
#if DEBUG
                LogTimestamp("Drop", $"✓ 工作流 Tab 获取成功: {workflowTab.Name}, 节点数={workflowTab.DocumentCanvas?.Nodes.Count}");
#endif

                var viewModel = GetOwningViewModel();
                if (!viewModel.CanEditWorkflowCanvas || workflowTab.DocumentCanvas is not { } graph) return;
                _canvasControl.UpdateCanvasPlacementFeedback(SnapCanvasPoint(dropPosition), _tempNodeHeight);
                var insertion = _canvasControl.FindCanvasInsertionWire(dropPosition, Array.Empty<string>());
                var predecessorId = insertion == null ? _canvasControl.CanvasAvailableHotPredecessor() : null;
                var predecessor = predecessorId == null ? null : graph.Nodes.SingleOrDefault(node => node.Id == predecessorId);
                if (predecessorId != null && predecessor == null) throw new InvalidOperationException("自动连接的前驱节点已失效。");
                var placement = _canvasControl.ResolveCanvasPlacement(SnapCanvasPoint(dropPosition), _tempNodeHeight);
                graph.CreateNode(item.ToolId, new CanvasPoint(Math.Clamp(placement.X, 55, 4945), Math.Clamp(placement.Y, 15, 4985)), insertion, predecessor);
#if DEBUG
                LogTimestamp("Drop", $"✓ 节点已添加, 当前节点数: {workflowTab.DocumentCanvas?.Nodes.Count}");
                LogTimestamp("Drop", "═══ 节点添加完成 ═══");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                LogTimestamp("Drop", $"✗✗✗ 异常: {ex.Message}");
#endif
                ThemeMessageDialog.Show($"拖放节点失败: {ex.Message}\n\n类型: {ex.GetType().Name}", "错误",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            }
            finally { HideTempNode(); _canvasControl.ClearCanvasPlacementFeedback(); _canvasControl.ClearCanvasInsertionWire(); }
        }

        /// <summary>
        /// 获取当前选中的工作流 Tab（优先使用直接引用）
        /// </summary>
        private WorkflowTabViewModel? GetCurrentWorkflowTab() => _canvasControl.CurrentWorkflowTab;

    }
}
