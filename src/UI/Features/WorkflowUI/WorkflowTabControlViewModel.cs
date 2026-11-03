using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SunEyeVision.Core.Events;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.UI.Shared;

using SunEyeVision.UI.Core;

using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.WorkflowUI
{
    /// <summary>
    /// 工作流标签页管理ViewModel
    /// </summary>
    public class WorkflowTabControlViewModel : ViewModelBase
    {
        private ObservableCollection<WorkflowTabViewModel> _tabs = new ObservableCollection<WorkflowTabViewModel>();
        private WorkflowTabViewModel? _selectedTab;
        private readonly Dictionary<string, long> _recentStopRequests = new();
        private const long StopClickGuardMilliseconds = 500;


        /// <summary>
        /// 选中画布变化事件
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// 工作流状态变化事件。
        /// </summary>
        public event EventHandler? WorkflowStatusChanged;

        /// <summary>
        /// 连续执行由宿主提供。标签页只负责状态和交互，不能自行模拟执行。
        /// </summary>
        public Func<WorkflowTabViewModel, bool>? StartContinuousExecution { get; set; }
        public Func<WorkflowTabViewModel, bool>? StopContinuousExecution { get; set; }
          public Func<bool>? StartAllContinuousExecution { get; set; }
          public Func<bool>? StopAllContinuousExecution { get; set; }

        private readonly SunEyeVision.Plugin.Runtime.RuntimeDirectorySnapshot _directory;
        private SunEyeVision.Application.DocumentSessionManager? _sessionManager;

        internal void BindDocumentManager(SunEyeVision.Application.DocumentSessionManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);
            if (ReferenceEquals(_sessionManager, manager)) return;
            var next = PrepareDocumentTabs(manager.CurrentSession);
            UnbindDocumentManager();
            _sessionManager = manager;
            manager.SessionChanged += DocumentSessionChanged;
            try { InstallDocumentTabs(next); }
            catch
            {
                manager.SessionChanged -= DocumentSessionChanged;
                _sessionManager = null;
                foreach (var tab in next) tab.UnbindDocumentCanvas();
                throw;
            }
        }

        internal void UnbindDocumentManager()
        {
            if (_sessionManager is null) return;
            _sessionManager.SessionChanged -= DocumentSessionChanged;
            _sessionManager = null;
            foreach (var tab in Tabs) tab.UnbindDocumentCanvas();
        }

        private void DocumentSessionChanged(object? sender, SunEyeVision.Application.DocumentSessionReplacement change)
        {
            if (!ReferenceEquals(sender, _sessionManager)) return;
            try { ReplaceDocumentTabs(change.Current); }
            catch
            {
                // 管理器已完成切换，旧 session 不可复活；失败时不能留下可编辑的旧画布。
                foreach (var tab in Tabs) tab.UnbindDocumentCanvas();
                Tabs.Clear();
                SetSelectedTab(null);
                throw;
            }
        }

        private void ReplaceDocumentTabs(SunEyeVision.Application.DocumentSession? session)
            => InstallDocumentTabs(PrepareDocumentTabs(session));

        private List<WorkflowTabViewModel> PrepareDocumentTabs(SunEyeVision.Application.DocumentSession? session)
        {
            var next = new List<WorkflowTabViewModel>();
            try
            {
                if (session is not null)
                    foreach (var flow in session.GetWorkflows().Where(flow => !flow.IsSubroutine))
                    {
                        var tab = new WorkflowTabViewModel() { Id = flow.Id, Name = flow.Name };
                        next.Add(tab);
                        tab.BindDocumentCanvas(session, flow.Id);
                    }
            }
            catch
            {
                foreach (var tab in next) tab.UnbindDocumentCanvas();
                throw;
            }
            return next;
        }

        private void InstallDocumentTabs(List<WorkflowTabViewModel> next)
        {
            foreach (var tab in Tabs) tab.UnbindDocumentCanvas();
            Tabs.Clear();
            foreach (var tab in next) Tabs.Add(tab);
            SetSelectedTab(next.FirstOrDefault());
        }
        public WorkflowTabControlViewModel(SunEyeVision.Plugin.Runtime.RuntimeDirectorySnapshot directory)
        {
            _directory=directory??throw new ArgumentNullException(nameof(directory));
            // 创建默认工作流。

        }

        /// <summary>
        /// 标签页集合。
        /// </summary>
        public ObservableCollection<WorkflowTabViewModel> Tabs
        {
            get => _tabs;
            set => SetProperty(ref _tabs, value);
        }

        /// <summary>
        /// 当前选中的标签页
        /// </summary>
        public WorkflowTabViewModel? SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetProperty(ref _selectedTab, value))
                {
                    // 触发选中变化事件
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        #region 工作流管理。

        /// <summary>
        /// Set selected tab without triggering SelectionChanged event
        /// </summary>
        /// <param name="tab">Tab to select</param>
        /// <param name="suppressEvent">Whether to suppress SelectionChanged event</param>
        public void SetSelectedTab(WorkflowTabViewModel? tab, bool suppressEvent = false)
        {
            if (suppressEvent)
            {
                _selectedTab = tab;
                OnPropertyChanged(nameof(SelectedTab));
            }
            else
            {
                SelectedTab = tab;
            }
        }


        /// <summary>
        /// 创建默认工作流。
        /// </summary>
        #endregion

        #region 工作流运行控制。

        /// <summary>
        /// 开始连续运行工作流
        /// </summary>
        public void StartContinuous(WorkflowTabViewModel workflow)
        {
            if (workflow == null || workflow.IsRunning)
                return;

            // 上一轮停止刚被确认时，忽略按钮抖动/连点造成的立即重启。
            if (_recentStopRequests.TryGetValue(workflow.RootWorkflowId, out var stoppedAt)
                && Environment.TickCount64 - stoppedAt < StopClickGuardMilliseconds)
                return;

            if (StartContinuousExecution?.Invoke(workflow) == true)
            {
                _recentStopRequests.Remove(workflow.RootWorkflowId);
                WorkflowStatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 停止工作流运行。
        /// </summary>
        public void StopWorkflow(WorkflowTabViewModel workflow)
        {
            if (workflow == null)
                return;

            // 取消请求发出后，仍保持“运行中”直到执行管理器确认当前轮已退出。
            if (_recentStopRequests.TryGetValue(workflow.RootWorkflowId, out var stoppedAt)
                && Environment.TickCount64 - stoppedAt < StopClickGuardMilliseconds)
                return;
            if (StopContinuousExecution?.Invoke(workflow) == true)
                _recentStopRequests[workflow.RootWorkflowId] = Environment.TickCount64;
        }

        /// <summary>
        /// 切换连续运行/停止
        /// </summary>
        public void ToggleContinuous(WorkflowTabViewModel workflow)
        {
            if (workflow == null)
                return;

            if (workflow.IsRunning)
            {
                StopWorkflow(workflow);
            }
            else
            {
                StartContinuous(workflow);
            }
        }

        /// <summary>
        /// 开始连续运行所有工作流
        /// </summary>
        public void StartAllWorkflows()
        {
            StartAllContinuousExecution?.Invoke();
        }

        /// <summary>
        /// 停止所有工作流
        /// </summary>
        public void StopAllWorkflows()
        {
            StopAllContinuousExecution?.Invoke();
        }

        /// <summary>
        /// 切换所有工作流的连续运行/停止。
        /// </summary>
        public void ToggleAllWorkflows()
        {
            var anyRunning = Tabs.Any(w => w.IsRunning);
            
            if (anyRunning)
            {
                StopAllWorkflows();
            }
            else
            {
                StartAllWorkflows();
            }
        }

        /// <summary>
        /// 判断是否有任何工作流正在运行
        /// </summary>
        public bool IsAnyWorkflowRunning
        {
            get => Tabs.Any(w => w.IsRunning);
        }

        #endregion
    }
}
