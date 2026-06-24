using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Tool.Subroutine.ViewModels;

namespace SunEyeVision.Tool.Subroutine.Views
{
    /// <summary>
    /// 子程序调试控件 — 包装参数设置的 TabControl 内容，由 DefaultDebugWindow 包裹弹出。
    /// 点击设置按钮时创建此控件 + DefaultDebugWindow，替代旧的独立对话框。
    /// </summary>
    public partial class SubroutineDebugControl : UserControl
    {
        /// <summary>ViewModel 引用（保持对象存活）</summary>
        public SubroutineParameterDialogViewModel ViewModel { get; }

        public SubroutineDebugControl(
            string subroutineId,
            string subroutineName,
            string nodeId,
            IDataSourceQueryService? dataSourceQueryService,
            List<AvailableDataSource>? subroutineInternalSources,
            SubroutineParameters parameters,
            Action? onOutputsChanged = null)
        {
            InitializeComponent();

            ViewModel = new SubroutineParameterDialogViewModel(
                subroutineId, subroutineName, nodeId, dataSourceQueryService, subroutineInternalSources, parameters, onOutputsChanged);
            DataContext = ViewModel;
        }
    }
}
