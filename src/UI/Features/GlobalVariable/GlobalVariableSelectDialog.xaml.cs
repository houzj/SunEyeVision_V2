using System.Linq;
using System.Windows;
using SunEyeVision.UI.ViewModels;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Views.Windows
{
    /// <summary>
    /// 全局变量选择对话框（简化版，只读选择，非管理器）
    /// </summary>
    public partial class GlobalVariableSelectDialog : Window
    {
        private readonly GlobalVariableSelectViewModel _viewModel;

        /// <summary>
        /// 选中的变量（对话框关闭后读取）
        /// </summary>
        public GlobalVariable? SelectedVariable => _viewModel.SelectedVariable;

        /// <summary>
        /// 使用 ViewModel 构造
        /// </summary>
        public GlobalVariableSelectDialog(GlobalVariableSelectViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;

            // ViewModel 请求关闭时确认选择
            _viewModel.RequestClose += OnViewModelRequestClose;
        }

        private void OnViewModelRequestClose(object? sender, EventArgs e)
        {
            if (_viewModel.SelectedVariable == null)
                return;

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 静态便捷方法：传入变量列表，弹出选择对话框
        /// </summary>
        /// <param name="variables">可用的全局变量列表</param>
        /// <param name="filterDataType">可选的数据类型过滤（如 "Integer"），null=不过滤</param>
        /// <returns>选中的变量，取消则返回 null</returns>
        public static GlobalVariable? ShowSelect(System.Collections.Generic.List<GlobalVariable> variables, string? filterDataType = null)
        {
            var viewModel = new GlobalVariableSelectViewModel(variables, filterDataType);
            var dialog = new GlobalVariableSelectDialog(viewModel);
            return dialog.ShowDialog() == true ? dialog.SelectedVariable : null;
        }

        /// <summary>
        /// 从 SolutionManager 创建并显示选择对话框
        /// </summary>
        /// <param name="solutionManager">解决方案管理器</param>
        /// <param name="filterDataType">可选的数据类型过滤（如 "Integer"），null=不过滤</param>
        /// <returns>选中的变量，取消则返回 null</returns>
        public static GlobalVariable? ShowSelect(SolutionManager solutionManager, string? filterDataType = null)
        {
            var variables = solutionManager.CurrentSolution?.GlobalVariables;
            if (variables == null || variables.Count == 0)
            {
                MessageBox.Show("当前解决方案中没有全局变量。\n请在\"全局变量管理器\"中添加后重试。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            return ShowSelect(variables.ToList(), filterDataType);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedVariable == null)
            {
                MessageBox.Show("请先选择一个变量。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_viewModel.SelectedVariable != null)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
