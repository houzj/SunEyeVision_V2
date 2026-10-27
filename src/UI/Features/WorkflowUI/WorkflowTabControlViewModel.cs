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

        public WorkflowTabControlViewModel()
        {
            // 创建默认工作流。
            CreateDefaultWorkflow();
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
        private void CreateDefaultWorkflow()
        {
            if (Tabs.Count > 0)
                return;

            var defaultWorkflow = new WorkflowTabViewModel
            {
                Name = "工作流1"
            };
            // ✅ 为没有绑定 Solution 的工作流创建独立的连接集合
            defaultWorkflow.SetConnections(new ObservableCollection<WorkflowConnection>());
            Tabs.Add(defaultWorkflow);
            // 静默设置 SelectedTab，不触发 SelectionChanged 事件
            SetSelectedTab(defaultWorkflow, suppressEvent: true);
        }

        /// <summary>
        /// 重置为默认状态（清空所有标签页并创建新的默认工作流）
        /// </summary>
        /// <remarks>
        /// 用于删除解决方案后恢复到启动时的初始状态。
        /// 与启动时保持一致，彻底清空节点和连接。
        /// </remarks>
        public void ResetToDefault()
        {
            // 重置所有工作流的节点序号管理器（防止全局索引污染）
            foreach (var tab in Tabs)
            {
                if (tab.SequenceManager != null)
                {
                    tab.SequenceManager.Reset();
                }
            }

            // 清空现有标签页
            Tabs.Clear();

            // 创建新的默认工作流（新对象，彻底清空节点和连接）
            var defaultWorkflow = new WorkflowTabViewModel
            {
                Name = "工作流1"
            };
            // ✅ 为没有绑定 Solution 的工作流创建独立的连接集合
            defaultWorkflow.SetConnections(new ObservableCollection<WorkflowConnection>());
            Tabs.Add(defaultWorkflow);
            // 静默设置 SelectedTab，不触发 SelectionChanged 事件
            SetSelectedTab(defaultWorkflow, suppressEvent: true);
        }

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
                workflow.IsRunning = true;
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
            foreach (var workflow in Tabs)
            {
                StartContinuous(workflow);
            }
        }

        /// <summary>
        /// 停止所有工作流
        /// </summary>
        public void StopAllWorkflows()
        {
            foreach (var workflow in Tabs)
            {
                StopWorkflow(workflow);
            }
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
