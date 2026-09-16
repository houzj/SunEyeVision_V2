using System;
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


        /// <summary>
        /// 选中画布变化事件
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// 工作流状态变化事件。
        /// </summary>
        public event EventHandler? WorkflowStatusChanged;

        /// <summary>
        /// 工作流添加事件（用于通知 MainWindowViewModel 注册引擎 + 同步 solution.Workflows）
        /// </summary>
        public event System.EventHandler<WorkflowTabViewModel>? WorkflowAdded;

        /// <summary>
        /// 工作流删除事件（Phase 3: 用于通知 MainWindowViewModel 清理引擎和 Solution 中的对应工作流）
        /// </summary>
        public event System.EventHandler<WorkflowTabViewModel>? WorkflowDeleted;

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

        /// <summary>
        /// 添加新工作流
        /// </summary>
        /// <remarks>
        /// 触发 WorkflowAdded 事件，通知 MainWindowViewModel 同步注册引擎和 solution.Workflows。
        /// 这样无论从哪个入口（Ctrl+N / UI 按钮）添加工作流，都能保证数据层一致。
        /// </remarks>
        public void AddWorkflow()
        {
            int nextNumber = GetNextWorkflowNumber();
            var newWorkflow = new WorkflowTabViewModel
            {
                Name = $"工作流{nextNumber}"
            };
            // ✅ 为没有绑定 Solution 的工作流创建独立的连接集合
            newWorkflow.SetConnections(new ObservableCollection<WorkflowConnection>());
            Tabs.Add(newWorkflow);
            SelectedTab = newWorkflow;

            // 触发工作流添加事件（统一处理引擎注册和 Solution 同步）
            WorkflowAdded?.Invoke(this, newWorkflow);
        }

        /// <summary>
        /// 删除工作流。
        /// </summary>
        public bool DeleteWorkflow(WorkflowTabViewModel workflow)
        {
            if (workflow == null)
                return false;

            if (Tabs.Count <= 1)
            {
                return false; // 至少保留一个工作流
            }

            if (workflow.IsRunning)
            {
                return false; // 运行中不能删除。
            }

            var index = Tabs.IndexOf(workflow);
            Tabs.Remove(workflow);

            // 选择其他标签页。
            if (SelectedTab == workflow)
            {
                if (Tabs.Count > 0)
                {
                    var newIndex = Math.Min(index, Tabs.Count - 1);
                    SelectedTab = Tabs[newIndex];
                }
                else
                {
                    SelectedTab = null;
                }
            }

            // Phase 3: 触发工作流删除事件（通知 MainWindowViewModel 清理引擎和 Solution）
            WorkflowDeleted?.Invoke(this, workflow);

            return true;
        }

        /// <summary>
        /// 获取下一个可用的工作流编号。
        /// </summary>
        private int GetNextWorkflowNumber()
        {
            // 直接从 Tabs 中提取已使用编号，无需维护独立跟踪集合
            var usedNumbers = new System.Collections.Generic.HashSet<int>();
            foreach (var tab in Tabs)
            {
                var match = System.Text.RegularExpressions.Regex.Match(tab.Name, @"工作流(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                    usedNumbers.Add(number);
            }

            // 找最小未使用编号（自然实现空洞填充）
            for (int i = 1; ; i++)
                if (!usedNumbers.Contains(i))
                    return i;
        }

        #endregion

        #region 工作流运行控制。

        /// <summary>
        /// 单次运行工作流。
        /// </summary>
        public void RunSingle(WorkflowTabViewModel workflow)
        {
            if (workflow == null || workflow.IsRunning)
                return;

            workflow.IsRunning = true;
            
            // 模拟单次运行
            Task.Delay(500).ContinueWith(_ =>
            {
                workflow.IsRunning = false;
                WorkflowStatusChanged?.Invoke(this, EventArgs.Empty);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// 开始连续运行工作流
        /// </summary>
        public void StartContinuous(WorkflowTabViewModel workflow)
        {
            if (workflow == null || workflow.IsRunning)
                return;

            if (StartContinuousExecution?.Invoke(workflow) == true)
            {
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
            StopContinuousExecution?.Invoke(workflow);
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
        /// 单次运行所有工作流
        /// </summary>
        public async Task RunAllWorkflowsAsync()
        {
            var runningWorkflows = new List<WorkflowTabViewModel>();
            
            foreach (var workflow in Tabs)
            {
                if (!workflow.IsRunning)
                {
                    workflow.IsRunning = true;
                    runningWorkflows.Add(workflow);
                }
            }

            WorkflowStatusChanged?.Invoke(this, EventArgs.Empty);

            // 模拟执行所有工作流
            await Task.Delay(500);

            foreach (var workflow in runningWorkflows)
            {
                workflow.IsRunning = false;
            }

            WorkflowStatusChanged?.Invoke(this, EventArgs.Empty);
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
