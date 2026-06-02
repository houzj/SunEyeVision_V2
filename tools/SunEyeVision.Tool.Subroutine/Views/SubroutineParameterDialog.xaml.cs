using System.Windows;
using SunEyeVision.Tool.Subroutine.ViewModels;

namespace SunEyeVision.Tool.Subroutine.Views
{
    /// <summary>
    /// 子程序参数映射对话框
    /// </summary>
    public partial class SubroutineParameterDialog : Window
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="subroutineId">子程序工作流ID</param>
        /// <param name="subroutineName">子程序名称</param>
        public SubroutineParameterDialog(string subroutineId, string subroutineName)
        {
            InitializeComponent();
            DataContext = new SubroutineParameterDialogViewModel(subroutineId, subroutineName);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SubroutineParameterDialogViewModel vm)
            {
                vm.Save();
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
