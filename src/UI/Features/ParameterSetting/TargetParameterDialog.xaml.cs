using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Features.ParameterSetting
{
    /// <summary>
    /// 目标参数管理对话框
    /// </summary>
    public partial class TargetParameterDialog : ThemeDialogWindow
    {
        private readonly TargetParameterViewModel _viewModel;
        private bool _isSaved;

        /// <summary>
        /// 保存后的 ConsumerBinding 列表
        /// </summary>
        public List<ConsumerBinding>? ResultBindings { get; private set; }

        public TargetParameterDialog(TargetParameterViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;

            _viewModel.RequestClose += OnViewModelRequestClose;
        }

        private void OnViewModelRequestClose(object? sender, EventArgs e)
        {
            _isSaved = true;
            ResultBindings = _viewModel.ResultBindings;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 静态便捷方法：弹出目标参数管理对话框
        /// </summary>
        /// <param name="workflows">解决方案中的所有工作流</param>
        /// <param name="existingBindings">当前 ConsumerBinding 列表</param>
        /// <returns>更新后的 ConsumerBinding 列表，取消则返回 null</returns>
        public static List<ConsumerBinding>? ShowDialog(
            IEnumerable<Workflow.Workflow> workflows,
            List<ConsumerBinding>? existingBindings,
            string? targetTypeName = null)
        {
            var viewModel = new TargetParameterViewModel(workflows, existingBindings, targetTypeName);
            var dialog = new TargetParameterDialog(viewModel);

            // 设置 Owner 为当前活动窗口（通常是 GlobalVariableManagerDialog），
            // 避免对话框出现在父窗口后方需要手动切换
            dialog.Owner = Application.Current?.Windows.OfType<Window>()
                .FirstOrDefault(w => w.IsActive);

            return dialog.ShowDialog() == true ? dialog.ResultBindings : null;
        }
    }

}
