using System.Collections;
using SunEyeVision.UI.Services;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 工作区视图模型 - 就近绑定中间工作区。
    /// 承载工作流标签页的 ViewModel 状态（标签集合/当前选中标签），经 IRootCoordinator 复用根 VM 的
    /// WorkflowTabViewModel。工作区的纯视觉交互（缩放/滚动/画布切换）仍由 ShellWorkspaceControl 的
    /// code-behind 承载（属于 View 层职责），本 VM 不接管视觉操作。
    /// </summary>
    public class WorkspaceViewModel : ViewModelBase
    {
        private readonly IRootCoordinator _coordinator;
        private object? _selectedTab;

        public WorkspaceViewModel(IRootCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        /// <summary>
        /// 工作流标签页集合（TabControl 绑定）
        /// </summary>
        public IList Tabs => _coordinator.Tabs;

        /// <summary>
        /// 当前选中的工作流标签页（TabControl 双向绑定）
        /// </summary>
        public object? SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetProperty(ref _selectedTab, value))
                {
                    _coordinator.SelectedTab = value!;
                }
            }
        }
    }
}
