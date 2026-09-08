using System.Windows;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.ViewModels;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Views;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Logic
{
    /// <summary>
    /// Region Editor集成帮助类 - 简化RegionEditorControl的使用
    /// </summary>
    public class RoiEditorIntegration
    {
        private readonly RoiEditorViewModel _viewModel;
        private readonly DataSourceQueryService _dataProvider;
        private readonly NodeSelectorPopup _nodeSelectorPopup;

        /// <summary>
        /// 构造函数
        /// </summary>
        public RoiEditorIntegration(RoiEditorViewModel viewModel, DataSourceQueryService dataProvider)
        {
            _viewModel = viewModel;
            _dataProvider = dataProvider;
            _nodeSelectorPopup = new NodeSelectorPopup();

            // 初始化ViewModel
            _viewModel.Initialize(_dataProvider);

            // 设置节点选择器的DataContext
            _nodeSelectorPopup.DataContext = _viewModel.NodeSelector;

            // 订阅选择确认事件
            if (_viewModel.NodeSelector != null)
            {
                _viewModel.NodeSelector.SelectionConfirmed += OnNodeSelectionConfirmed;
            }
        }

        /// <summary>
        /// 节点选择确认事件
        /// </summary>
        private void OnNodeSelectionConfirmed(object? sender, NodeOutputInfo? selectedNode)
        {
            // 选择已确认，由ParameterPanelViewModel处理
            // 这里可以添加额外的逻辑，如日志记录等
        }

        /// <summary>
        /// 显示节点选择器
        /// </summary>
        public void ShowNodeSelector(FrameworkElement placementTarget, string targetDataType)
        {
            if (_viewModel.NodeSelector != null)
            {
                _viewModel.NodeSelector.TargetDataType = targetDataType;
                _nodeSelectorPopup.Open(placementTarget);
            }
        }

        /// <summary>
        /// 隐藏节点选择器
        /// </summary>
        public void HideNodeSelector()
        {
            _nodeSelectorPopup.Close();
        }

        /// <summary>
        /// 设置当前节点ID（用于过滤）
        /// </summary>
        public void SetCurrentNodeId(string nodeId)
        {
            _dataProvider.CurrentNodeId = nodeId;
        }

        /// <summary>
        /// 获取数据提供者（用于直接访问）
        /// </summary>
        public DataSourceQueryService GetDataProvider()
        {
            return _dataProvider;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            if (_viewModel.NodeSelector != null)
            {
                _viewModel.NodeSelector.SelectionConfirmed -= OnNodeSelectionConfirmed;
            }

            _nodeSelectorPopup.Close();
            _viewModel.Dispose();
        }
    }
}
