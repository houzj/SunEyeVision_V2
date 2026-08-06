using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.ViewModels;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Views
{
    /// <summary>
    /// NodeSelectorPopup.xaml 的交互逻辑
    /// </summary>
    public partial class NodeSelectorPopup : Popup
    {
        public NodeSelectorPopup()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 树状项双击事件
        /// </summary>
        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item && item.DataContext is NodeOutputInfo nodeInfo)
            {
                if (nodeInfo.IsTypeMatched)
                {
                    // 如果类型匹配，立即确认选择
                    if (DataContext is NodeSelectorViewModel viewModel)
                    {
                        viewModel.SelectedItem = nodeInfo;
                        viewModel.SelectCommand.Execute(null);
                    }
                }
            }
        }

        /// <summary>
        /// 键盘事件处理
        /// </summary>
        private void TreeViewItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is TreeViewItem item && item.DataContext is NodeOutputInfo nodeInfo)
                {
                    if (nodeInfo.IsTypeMatched)
                    {
                        if (DataContext is NodeSelectorViewModel viewModel)
                        {
                            viewModel.SelectedItem = nodeInfo;
                            viewModel.SelectCommand.Execute(null);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 打开弹窗
        /// </summary>
        public void Open(FrameworkElement placementTarget)
        {
            PlacementTarget = placementTarget;
            IsOpen = true;

            // 刷新节点列表
            if (DataContext is NodeSelectorViewModel viewModel)
            {
                viewModel.RefreshNodes();
            }
        }

        /// <summary>
        /// 关闭弹窗
        /// </summary>
        public void Close()
        {
            IsOpen = false;
        }
    }
}
