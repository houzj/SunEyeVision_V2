using System.Windows;
using SunEyeVision.UI.ViewModels;

namespace SunEyeVision.UI.Views.Windows
{
    /// <summary>
    /// SaveAsDialog.xaml 的交互逻辑
    /// </summary>
    public partial class SaveAsDialog : Window
    {
        public SaveAsDialogViewModel ViewModel { get; }

        public SaveAsDialog(string defaultName, string? defaultDescription = null, string? title = null)
        {
            InitializeComponent();
            
            ViewModel = new SaveAsDialogViewModel(defaultName, defaultDescription, title);
            DataContext = ViewModel;
        }

        /// <summary>
        /// 获取输入的名称
        /// </summary>
        public string GetInputName()
        {
            return ViewModel.Name;
        }

        /// <summary>
        /// 获取输入的描述
        /// </summary>
        public string? GetInputDescription()
        {
            return string.IsNullOrWhiteSpace(ViewModel.Description) ? null : ViewModel.Description;
        }
    }
}
