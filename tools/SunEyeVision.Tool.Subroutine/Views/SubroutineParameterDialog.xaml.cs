using System;
using System.Collections.Generic;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.Subroutine.Models;
using SunEyeVision.Tool.Subroutine.ViewModels;

namespace SunEyeVision.Tool.Subroutine.Views
{
    /// <summary>
    /// 子程序参数配置对话框（内联 ComboBox 版本，无需弹窗）
    /// </summary>
    public partial class SubroutineParameterDialog : Window
    {
        private readonly SubroutineParameterDialogViewModel _viewModel;

        public SubroutineParameterDialog(
            string subroutineId,
            string subroutineName,
            string nodeId,
            IDataSourceQueryService? dataSourceQueryService,
            List<AvailableDataSource>? subroutineInternalSources,
            SubroutineParameters parameters,
            Action? onOutputsChanged = null)
        {
            InitializeComponent();

            _viewModel = new SubroutineParameterDialogViewModel(
                subroutineId, subroutineName, nodeId, dataSourceQueryService, subroutineInternalSources, parameters, onOutputsChanged);
            DataContext = _viewModel;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
