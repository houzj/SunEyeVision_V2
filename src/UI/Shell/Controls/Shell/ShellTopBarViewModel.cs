using System.Collections;
using System.ComponentModel;
using System.Windows.Input;
using SunEyeVision.UI.Services;

using SunEyeVision.UI.Shell.Windows;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Shell.Controls
{
    /// <summary>
    /// 顶部标题栏视图模型 - 就近绑定顶部菜单+工具栏。
    /// 承载顶栏域：全局编排命令（运行/停止/管理器/保存/帮助）经 IRootCoordinator 复用根 VM 命令，
    /// 跨域状态（当前解决方案/全部工作流运行状态/工作流标签页）经 IRootCoordinator 获取并订阅变更，
    /// 不持有 MainWindowViewModel 类型引用。
    /// </summary>
    public class ShellTopBarViewModel : ViewModelBase
    {
        private readonly IRootCoordinator _coordinator;
        private readonly ThemeManager? _themeManager;
        private bool _isAllWorkflowsRunning;
        private bool _hasCurrentSolution;
        private string _currentSolutionName = string.Empty;
        private bool _isDarkTheme;

        public ShellTopBarViewModel(IRootCoordinator coordinator)
        {
            _coordinator = coordinator;
            _coordinator.PropertyChanged += OnCoordinatorPropertyChanged;
            _coordinator.SelectedTabChanged += OnSelectedTabChanged;
            RefreshState();

            _themeManager = SunEyeVision.UI.Core.ServiceLocator.Instance.GetService<ThemeManager>();
            _isDarkTheme = _themeManager?.CurrentTheme == AppTheme.Dark;
            if (_themeManager != null)
            {
                _themeManager.ThemeChanged += OnThemeChanged;
            }
            ToggleThemeCommand = new RelayCommand(ExecuteToggleTheme, null, "ToggleTheme");
        }

        /// <summary>
        /// 解绑根协调器和主题管理器（窗口关闭时调用）
        /// </summary>
        public void Shutdown()
        {
            _coordinator.PropertyChanged -= OnCoordinatorPropertyChanged;
            _coordinator.SelectedTabChanged -= OnSelectedTabChanged;
            if (_themeManager != null)
            {
                _themeManager.ThemeChanged -= OnThemeChanged;
            }
        }

        private void OnThemeChanged(AppTheme theme)
        {
            IsDarkTheme = theme == AppTheme.Dark;
        }

        private void ExecuteToggleTheme()
        {
            _themeManager?.ToggleTheme();
        }

        private void OnSelectedTabChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(SelectedTab));
        }

        private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IRootCoordinator.IsAllWorkflowsRunning):
                    IsAllWorkflowsRunning = _coordinator.IsAllWorkflowsRunning;
                    break;
                case nameof(IRootCoordinator.HasCurrentSolution):
                    HasCurrentSolution = _coordinator.HasCurrentSolution;
                    break;
                case nameof(IRootCoordinator.CurrentSolutionName):
                    CurrentSolutionName = _coordinator.CurrentSolutionName;
                    break;
            }
        }

        private void RefreshState()
        {
            IsAllWorkflowsRunning = _coordinator.IsAllWorkflowsRunning;
            HasCurrentSolution = _coordinator.HasCurrentSolution;
            CurrentSolutionName = _coordinator.CurrentSolutionName;
        }

        public ICommand NewSolutionCommand => _coordinator.NewSolutionCommand;
        public ICommand OpenSolutionCommand => _coordinator.OpenSolutionCommand;
        public ICommand ReloadSolutionCommand => _coordinator.ReloadSolutionCommand;
        public ICommand ShowStartPageCommand => _coordinator.ShowStartPageCommand;

        /// <summary>保存当前解决方案命令</summary>
        public ICommand SaveCurrentSolutionCommand => _coordinator.SaveCurrentSolutionCommand;

        /// <summary>显示路径拐点命令</summary>
        public ICommand TogglePathPointsCommand => _coordinator.TogglePathPointsCommand;

        /// <summary>运行工作流命令</summary>
        public ICommand RunWorkflowCommand => _coordinator.RunWorkflowCommand;

        /// <summary>停止工作流命令</summary>
        public ICommand StopWorkflowCommand => _coordinator.StopWorkflowCommand;

        /// <summary>显示全局变量管理命令</summary>
        public ICommand ShowGlobalVariableManagerCommand => _coordinator.ShowGlobalVariableManagerCommand;

        /// <summary>显示相机管理命令</summary>
        public ICommand ShowCameraManagerCommand => _coordinator.ShowCameraManagerCommand;

        /// <summary>显示通讯管理命令</summary>
        public ICommand ShowCommunicationManagerCommand => _coordinator.ShowCommunicationManagerCommand;

        /// <summary>显示帮助命令</summary>
        public ICommand ShowHelpCommand => _coordinator.ShowHelpCommand;

        /// <summary>显示快捷键命令</summary>
        public ICommand ShowShortcutsCommand => _coordinator.ShowShortcutsCommand;

        /// <summary>显示关于命令</summary>
        public ICommand ShowAboutCommand => _coordinator.ShowAboutCommand;

        /// <summary>运行全部工作流命令</summary>
        public ICommand RunAllWorkflowsCommand => _coordinator.RunAllWorkflowsCommand;

        /// <summary>切换全部连续运行命令</summary>
        public ICommand ToggleContinuousAllCommand => _coordinator.ToggleContinuousAllCommand;

        /// <summary>切换亮/暗主题命令</summary>
        public ICommand ToggleThemeCommand { get; }

        /// <summary>是否暗色主题（顶栏图标切换依据）</summary>
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            private set => SetProperty(ref _isDarkTheme, value);
        }

        /// <summary>是否全部工作流正在运行</summary>
        public bool IsAllWorkflowsRunning
        {
            get => _isAllWorkflowsRunning;
            private set => SetProperty(ref _isAllWorkflowsRunning, value);
        }

        /// <summary>是否已有当前解决方案</summary>
        public bool HasCurrentSolution
        {
            get => _hasCurrentSolution;
            private set => SetProperty(ref _hasCurrentSolution, value);
        }

        /// <summary>当前解决方案名称</summary>
        public string CurrentSolutionName
        {
            get => _currentSolutionName;
            private set => SetProperty(ref _currentSolutionName, value);
        }

        /// <summary>工作流标签页集合（顶栏下拉框）</summary>
        public IList Tabs => _coordinator.Tabs;

        /// <summary>当前选中工作流标签页（顶栏下拉框，只读单向绑定）</summary>
        public object? SelectedTab => _coordinator.SelectedTab;

        /// <summary>
        /// 用户切换顶栏下拉框选中项时调用 — 通过协调器唯一写入口设置选中，避免双向绑定回写重入。
        /// </summary>
        /// <param name="tab">目标标签页</param>
        public void SetSelectedTab(object? tab) => _coordinator.SetSelectedTab(tab);
    }
}
