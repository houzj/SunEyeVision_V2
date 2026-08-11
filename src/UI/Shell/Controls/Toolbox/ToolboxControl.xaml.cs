using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.UI.Models;
using SunEyeVision.UI.ViewModels;
using SunEyeVision.UI.Views.Windows;

namespace SunEyeVision.UI.Views.Controls.Toolbox
{
    /// <summary>
    /// ToolboxControl.xaml 的交互逻辑（超简化版）
    /// </summary>
    public partial class ToolboxControl : UserControl
    {
    private ToolboxViewModel _viewModel;
    private double _popupVerticalOffset;  // Popup相对于CategorySidebar的垂直偏移

        public ToolboxControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 获取MainWindow设置的ViewModel
            _viewModel = DataContext as ToolboxViewModel;

            if (_viewModel == null)
            {
                return;
            }

            // 设置Popup的DataContext（Popup不在Visual Tree中，需要手动设置）
            CompactModePopup.DataContext = _viewModel;

            // 使用内置定位模式（相对于CategorySidebar）
            CompactModePopup.Placement = PlacementMode.Right;
            CompactModePopup.PlacementTarget = CategorySidebar;  // 相对于CategorySidebar
            CompactModePopup.CustomPopupPlacementCallback = null;  // 移除自定义回调

            CompactModePopup.Opened += OnPopupOpened;
            CompactModePopup.Closed += OnPopupClosed;

            // 初始化宽度
            AdjustParentWidth();
        }

        /// <summary>
        /// 自定义Popup定位方法（已废弃，使用Placement.Right + VerticalOffset）
        /// </summary>
        private CustomPopupPlacement[] CustomPopupPlacementMethod(Size popupSize, Size targetSize, Point offset)
        {
            // 此方法已不再使用，保留仅为兼容性
            return new CustomPopupPlacement[] { new CustomPopupPlacement(new Point(10, 0), PopupPrimaryAxis.Horizontal) };
        }

        /// <summary>
        /// 开始拖拽工具项
        /// </summary>
        private void ToolItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
if (border.Tag is ToolItem tool)
                {
// 在拖拽开始前关闭Popup，防止拖拽过程中Popup不消失
                    if (CompactModePopup.IsOpen)
                    {
                        CompactModePopup.IsOpen = false;
                        _viewModel.SelectedCategory = null;
                    }

                    var data = new DataObject("ToolItem", tool);
var result = DragDrop.DoDragDrop(border, data, DragDropEffects.Copy);
}
                else
                {
}
            }
            else
            {
}
        }

        /// <summary>
        /// 鼠标悬停在分类图标上（紧凑模式）
        /// </summary>
        private void CategoryItem_MouseEnter(object sender, MouseEventArgs _)
        {
            if (sender is Border border && border.Tag is ToolCategory category)
            {
                // 计算分类图标相对于CategorySidebar的位置（纯相对坐标）
                var categoryInSidebar = border.TransformToAncestor(CategorySidebar).Transform(new Point(0, 0));
                _popupVerticalOffset = categoryInSidebar.Y;

                // 设置Popup的垂直偏移（纯相对坐标，不涉及屏幕坐标）
                CompactModePopup.VerticalOffset = _popupVerticalOffset;

                // 悬停时直接打开popup
                _viewModel.SelectedCategory = category.Name;

                // 直接设置Popup.IsOpen
                CompactModePopup.IsOpen = true;
            }
        }

        /// <summary>
        /// Popup打开时的处理
        /// </summary>
        private void OnPopupOpened(object sender, EventArgs e)
        {
            // 设计时执行计算，避免设计器错误
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                CompactModePopup.MaxHeight = 500;
                return;
            }

            // 使用固定高度
            CompactModePopup.MaxHeight = 500;
        }

        /// <summary>
        /// Popup关闭时的处理
        /// </summary>
        private void OnPopupClosed(object sender, EventArgs e)
        {
        }

        /// <summary>
        /// 鼠标进入Popup区域
        /// </summary>
        private void CompactModePopupBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            // 什么都不做，只是接收事件，防止popup关闭
        }

        /// <summary>
        /// 鼠标离开Popup区域 - 即时检测联合区域
        /// </summary>
        private void CompactModePopupBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            // 即时检测鼠标是否在popup或侧边栏的联合区域内
            try
            {
                var mousePos = Mouse.GetPosition(this);

                bool isOverSidebar = IsPointInElement(mousePos, CategorySidebar);
                bool isOverPopup = CompactModePopup.IsOpen && IsPointInElement(mousePos, CompactModePopupBorder);

                // 如果鼠标不在popup内也不在侧边栏内，关闭popup
                if (!isOverPopup && !isOverSidebar)
                {
                    CompactModePopup.IsOpen = false;
                    _viewModel.SelectedCategory = null;
                }
            }
            catch (Exception ex)
            {
                // 如果出错，安全关闭popup
                CompactModePopup.IsOpen = false;
                _viewModel.SelectedCategory = null;
            }
        }

        /// <summary>
        /// 鼠标离开侧边栏
        /// </summary>
        private void CategorySidebar_MouseLeave(object sender, MouseEventArgs e)
        {
            // 如果Popup没有打开，直接返回
            if (!CompactModePopup.IsOpen)
                return;

            try
            {
                var mousePos = Mouse.GetPosition(this);

                bool isOverSidebar = IsPointInElement(mousePos, CategorySidebar);
                bool isOverPopupBorder = IsPointInElement(mousePos, CompactModePopupBorder);

                // 如果鼠标在Popup内容区域内，不关闭popup
                if (isOverPopupBorder)
                {
                    return;
                }

                // 如果鼠标不在popup内也不在侧边栏内，关闭popup
                if (!isOverSidebar)
                {
                    CompactModePopup.IsOpen = false;
                    _viewModel.SelectedCategory = null;
                }
            }
            catch (Exception ex)
            {
                // 如果出错，安全关闭popup
                CompactModePopup.IsOpen = false;
                _viewModel.SelectedCategory = null;
            }
        }

        /// <summary>
        /// 检查元素是否在指定祖先的Visual Tree中
        /// </summary>
        private bool IsPointInElement(Point point, FrameworkElement element)
        {
            if (element == null || !element.IsVisible)
                return false;

            // 检查元素是否在当前Visual Tree中
            bool isInVisualTree = IsElementInVisualTree(element, this);

            if (isInVisualTree)
            {
                // 元素在Visual Tree中，使用TransformToAncestor
                try
                {
                    var elementPos = element.TransformToAncestor(this).Transform(new Point(0, 0));
                    return point.X >= elementPos.X &&
                           point.X <= elementPos.X + element.ActualWidth &&
                           point.Y >= elementPos.Y &&
                           point.Y <= elementPos.Y + element.ActualHeight;
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                // 元素不在Visual Tree中（如Popup），使用屏幕坐标进行比较
                try
                {
                    // 将UserControl相对坐标转换为屏幕坐标
                    var mouseScreenPos = this.PointToScreen(point);

                    // 获取popup元素的屏幕位置
                    var elementScreenPos = element.PointToScreen(new Point(0, 0));

                    // 使用屏幕坐标检测
                    return mouseScreenPos.X >= elementScreenPos.X &&
                           mouseScreenPos.X <= elementScreenPos.X + element.ActualWidth &&
                           mouseScreenPos.Y >= elementScreenPos.Y &&
                           mouseScreenPos.Y <= elementScreenPos.Y + element.ActualHeight;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 检查元素是否在指定祖先的Visual Tree中
        /// </summary>
        private bool IsElementInVisualTree(FrameworkElement element, FrameworkElement ancestor)
        {
            if (element == null || ancestor == null)
                return false;

            try
            {
                DependencyObject current = element;
                while (current != null)
                {
                    if (current == ancestor)
                        return true;
                    current = VisualTreeHelper.GetParent(current);
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 点击分类标题（展开模式）
        /// </summary>
        private void ExpandCategory_MouseLeftButtonUp(object sender, MouseButtonEventArgs _)
        {
            if (sender is Border border && border.Tag is ToolCategory category)
            {
                // 切换展开/折叠状态
                category.IsExpanded = !category.IsExpanded;
            }
        }

        /// <summary>
        /// 点击切换显示模式
        /// </summary>
        private void ToggleModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && _viewModel.ToggleDisplayModeCommand.CanExecute(null))
            {
                _viewModel.ToggleDisplayModeCommand.Execute(null);
                AdjustParentWidth();
            }
        }

        /// <summary>
        /// 调整父容器宽度
        /// </summary>
        private void AdjustParentWidth()
        {
            var mainWindow = System.Windows.Window.GetWindow(this) as MainWindow;
            if (mainWindow?.ToolboxColumn != null)
            {
                double newWidth = _viewModel.IsCompactMode ? 60 : 260;
                mainWindow.ToolboxColumn.Width = new GridLength(newWidth);
            }
        }
    }
}
