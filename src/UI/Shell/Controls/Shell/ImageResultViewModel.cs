using SunEyeVision.Plugin.Runtime;
using System.Collections.ObjectModel;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services;

using SunEyeVision.UI.Core;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Shell.Controls
{
    /// <summary>
    /// 图像结果视图模型 - 就近绑定右侧图像结果区（图像显示 + 模块结果表 + 图像预览）。
    /// 承载图像结果域的展示状态，实现 INodeResultSink 供 NodeResultManager 写入结果。
    /// 跨域协调能力（运行工作流/状态文字/日志/模块结果树更新）经 IRootCoordinator 窄接口注入。
    /// </summary>
    public class ImageResultViewModel : ViewModelBase, Services.Workflow.INodeResultSink
    {
        private readonly IRootCoordinator _coordinator;


        private bool _hasModuleResults;
        private bool _showImagePreview;
        private SunEyeVision.UI.Shared.ImageInputSource? _activeInputSource;

        private ObservableCollection<FlatResultRow> _flatResultRows = new();

        public ImageResultViewModel(IRootCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        /// <summary>
        /// 附加共享集合实例（与根 VM 共用同一份数据源/结果行集合，避免状态分裂）
        /// </summary>
        public void AttachCollections(
            ObservableCollection<FlatResultRow> flatResultRows)
        {
            _flatResultRows = flatResultRows;
        }

        /// <summary>
        /// 图像显示控件的数据源（当前节点 + 所有父节点）
        /// </summary>
        /// <summary>
        /// 扁平化结果行列表（用于 DataGrid 绑定）
        /// </summary>
        public ObservableCollection<FlatResultRow> FlatResultRows => _flatResultRows;

        /// <summary>
        /// 是否有模块结果（用于空状态显示）
        /// </summary>
        public bool HasModuleResults
        {
            get => _hasModuleResults;
            set => SetProperty(ref _hasModuleResults, value);
        }

        /// <summary>
        /// 是否显示图像及其预览模块（ImageCaptureTool 节点专用）
        /// </summary>
        public bool ShowImagePreview
        {
            get => _showImagePreview;
            set
            {
                if (SetProperty(ref _showImagePreview, value))
                {
                    OnPropertyChanged(nameof(ImagePreviewHeight));
                }
            }
        }

        /// <summary>
        /// 图像预览高度，用于动态控制图像预览模块的空间
        /// </summary>
        public System.Windows.GridLength ImagePreviewHeight
        {
            get => ShowImagePreview ? new System.Windows.GridLength(60) : new System.Windows.GridLength(0);
        }

        /// <summary>
        /// 当前活动输入源（图像预览器绑定）
        /// </summary>
        public SunEyeVision.UI.Shared.ImageInputSource? ActiveInputSource
        {
            get => _activeInputSource;
            set => SetProperty(ref _activeInputSource, value);
        }


        #region INodeResultSink 实现

        public void UpdateModuleResults(DeclaredOutputSnapshot? result, string? nodeName, string? toolId = null, string? nodeId = null)
        {
            _coordinator.UpdateModuleResults(result, nodeName, toolId, nodeId);
        }

        /// <summary>
        /// 按当前选中的图像源刷新显示（实现 INodeResultSink，转发给根 VM 统一处理）
        /// </summary>
        public void RefreshDisplayImageFromCurrentSource()
        {
            _coordinator.RefreshDisplayImageFromCurrentSource();
        }

        public void ClearResultGraphics()=>_coordinator.ClearResultGraphics();
        public void ClearResultHighlight()=>_coordinator.ClearResultHighlight();

        public void ClearResultDisplay()
        {
            _coordinator.ClearResultDisplay();
        }

        #endregion
    }
}
