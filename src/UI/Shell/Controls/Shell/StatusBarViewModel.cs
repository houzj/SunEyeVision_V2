using System.ComponentModel;
using System;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Workflow;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Shell.Controls
{
    /// <summary>
    /// 状态栏视图模型 - 就近绑定底部状态栏。
    /// 独立自包含：状态文字/相机状态/运行状态/流程计数经 IRootCoordinator 窄接口获取，
    /// 并订阅其属性变更通知保持同步，不持有根 VM 类型引用。
    /// </summary>
    public class StatusBarViewModel : ViewModelBase
    {
        private readonly IRootCoordinator _coordinator;

        private string _statusText = string.Empty;
        private string _cameraStatus = string.Empty;
        private bool _isRunning;
        private int _workflowCount;
        private WorkflowExecutionSnapshot? _executionSnapshot;
        private readonly DispatcherTimer _executionTimer;

        public Action<WorkflowExecutionSnapshot?>? ShowExecutionSnapshotRequested { get; set; }
        public ICommand ShowExecutionSnapshotCommand { get; }

        public StatusBarViewModel(IRootCoordinator coordinator)
        {
            _coordinator = coordinator;
            _coordinator.PropertyChanged += OnCoordinatorPropertyChanged;
            ShowExecutionSnapshotCommand = new RelayCommand(() => ShowExecutionSnapshotRequested?.Invoke(ExecutionSnapshot));
            _executionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _executionTimer.Tick += (_, _) => RefreshExecutionElapsed();
            _executionTimer.Start();
            RefreshAll();
        }

        /// <summary>
        /// 状态栏文字
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// 相机状态
        /// </summary>
        public string CameraStatus
        {
            get => _cameraStatus;
            private set => SetProperty(ref _cameraStatus, value);
        }

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            private set => SetProperty(ref _isRunning, value);
        }

        /// <summary>
        /// 工作流数量
        /// </summary>
        public int WorkflowCount
        {
            get => _workflowCount;
            private set => SetProperty(ref _workflowCount, value);
        }

        public WorkflowExecutionSnapshot? ExecutionSnapshot
        {
            get => _executionSnapshot;
            private set
            {
                if (SetProperty(ref _executionSnapshot, value))
                    OnPropertyChanged(nameof(WorkflowExecutionText));
            }
        }

        public string WorkflowExecutionText => ExecutionSnapshot?.StatusBarText ?? "--";

        public void SetExecutionSnapshot(WorkflowExecutionSnapshot? snapshot)
        {
            if (ReferenceEquals(ExecutionSnapshot, snapshot))
            {
                OnPropertyChanged(nameof(WorkflowExecutionText));
                return;
            }
            ExecutionSnapshot = snapshot;
        }

        /// <summary>
        /// 解绑根协调器（窗口关闭时调用）
        /// </summary>
        public void Shutdown()
        {
            _coordinator.PropertyChanged -= OnCoordinatorPropertyChanged;
            _executionTimer.Stop();
        }

        private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IRootCoordinator.StatusText):
                    StatusText = _coordinator.StatusText;
                    break;
                case nameof(IRootCoordinator.CameraStatus):
                    CameraStatus = _coordinator.CameraStatus;
                    break;
                case nameof(IRootCoordinator.IsRunning):
                    IsRunning = _coordinator.IsRunning;
                    break;
                case nameof(IRootCoordinator.WorkflowCount):
                    WorkflowCount = _coordinator.WorkflowCount;
                    break;
            }
        }

        private void RefreshAll()
        {
            StatusText = _coordinator.StatusText;
            CameraStatus = _coordinator.CameraStatus;
            IsRunning = _coordinator.IsRunning;
            WorkflowCount = _coordinator.WorkflowCount;
        }

        private void RefreshExecutionElapsed()
        {
            if (ExecutionSnapshot?.Status != SunEyeVision.Plugin.SDK.Execution.Results.ExecutionStatus.Running)
                return;

            ExecutionSnapshot.Tick();
            OnPropertyChanged(nameof(WorkflowExecutionText));
        }
    }
}
