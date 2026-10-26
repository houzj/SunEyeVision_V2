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

        // 缓存 ViewModel 引用，避免每次通过 Application.Current 获取（回退方案）


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
        internal static Point SnapVmPoint(Point point)=>new(Math.Round(point.X/5)*5,Math.Round(point.Y/5)*5);
        private void ShowTempNode(ToolItem item,Point position,ControlsCanvas canvas)
        {
            if(_tempDragNode?.Tag is string id&&id==item.ToolId)
            {_tempDragNode.Visibility=Visibility.Visible;UpdateTempNodePosition(position);return;}
            if(_tempDragNode?.Parent is ControlsCanvas previous)previous.Children.Remove(_tempDragNode);
            var content=new Grid();content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});content.ColumnDefinitions.Add(new ColumnDefinition());
            content.Children.Add(new Image{Source=new System.Windows.Media.Imaging.BitmapImage(new Uri(item.IconSource)),Width=16,Height=16,Margin=new Thickness(4,0,0,0),VerticalAlignment=VerticalAlignment.Center});
            var title=new TextBlock{Text=item.Name,FontSize=12,FontWeight=FontWeights.Bold,Foreground=new SolidColorBrush(Color.FromRgb(55,58,60)),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};Grid.SetColumn(title,1);content.Children.Add(title);
            _tempDragNode=new Border{Width=_tempNodeWidth,Height=_tempNodeHeight,CornerRadius=new CornerRadius(item.ToolId=="ImageLoad"?15:4),
                Background=new LinearGradientBrush(Color.FromRgb(250,247,245),Color.FromRgb(235,230,226),90),BorderBrush=Brushes.Gray,BorderThickness=new Thickness(.7),Opacity=.7,
                Effect=new DropShadowEffect{BlurRadius=3,ShadowDepth=3,Opacity=.45},Child=content,Tag=item.ToolId,IsHitTestVisible=false};
            ControlsCanvas.SetZIndex(_tempDragNode,10000);canvas.Children.Add(_tempDragNode);UpdateTempNodePosition(position);
        }
        /// <summary>
        /// 更新临时节点位置（跟随鼠标）
        /// </summary>
        private void UpdateTempNodePosition(Point position)
        {
            position=SnapVmPoint(position);
            _canvasControl.UpdateVmPlacementFeedback(position, _tempNodeHeight);
            _canvasControl.UpdateVmInsertionWire(position, Array.Empty<string>());
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
            if (e.Data.GetData("ToolItem") is ToolItem {IsAvailable:true})
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
            if (e.Data.GetData("ToolItem") is ToolItem {IsAvailable:true})
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
            _canvasControl.ClearVmPlacementFeedback(); _canvasControl.ClearVmInsertionWire();
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

                if (toolItemData is not ToolItem {IsAvailable:true} item)
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
                _canvasControl.UpdateVmPlacementFeedback(SnapVmPoint(dropPosition), _tempNodeHeight);
                var insertion = _canvasControl.FindVmInsertionWire(dropPosition, Array.Empty<string>());
                var predecessor = insertion == null ? _canvasControl.VmAvailableHotPredecessor() : null;
                var placement = _canvasControl.ResolveVmPlacement(SnapVmPoint(dropPosition), _tempNodeHeight);
                newNode.Position = new Point(Math.Clamp(placement.X, 55, 4945), Math.Clamp(placement.Y, 15, 4985));
#if DEBUG
                LogTimestamp("Drop", "✓ 节点属性设置完成");
#endif

                // 添加新节点（通过 MainWindowViewModel.AddNodeToWorkflow 以记录日志）
#if DEBUG
                LogTimestamp("Drop", "▶ 添加节点到集合...");
#endif
                var viewModel = GetOwningViewModel();
                // 统一管道：节点添加必须经 AddNodeToWorkflow（日志/命令/绑定/预编译）。
                // viewModel 由 GetCachedViewModel 保证非 null（失败即抛异常，无回退分支）。
                using (var transaction = workflowTab.CommandManager.BeginTransaction())
                {
                    viewModel.AddNodeToWorkflow(newNode);
                    if (insertion != null)
                    {
                        if (!viewModel.InsertNodeIntoConnection(newNode, insertion, newNode.Position))
                            throw new InvalidOperationException(viewModel.StatusText);
                    }
                    else if (predecessor != null)
                    {
                        var link = new SunEyeVision.Workflow.WorkflowConnection(Guid.NewGuid().ToString("N"), predecessor, newNode.Id)
                        { SourcePort = "BottomPort", TargetPort = "TopPort" };
                        viewModel.AddConnectionToWorkflow(link);
                    }
                    transaction.Complete();
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
                ThemeMessageDialog.Show($"拖放节点失败: {ex.Message}\n\n类型: {ex.GetType().Name}", "错误",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            }
            finally { HideTempNode(); _canvasControl.ClearVmPlacementFeedback(); _canvasControl.ClearVmInsertionWire(); }
        }

        /// <summary>
        /// 获取当前选中的工作流 Tab（优先使用直接引用）
        /// </summary>
        private WorkflowTabViewModel? GetCurrentWorkflowTab() => _canvasControl.CurrentWorkflowTab;

    }
}