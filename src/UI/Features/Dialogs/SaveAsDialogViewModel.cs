using System.Windows.Input;
using SunEyeVision.UI.ViewModels;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 另存为对话框的ViewModel
    /// </summary>
    public class SaveAsDialogViewModel : ViewModelBase
    {
        private string _name;
        private string _description = string.Empty;

        /// <summary>
        /// 对话框标题
        /// </summary>
        public string DialogTitle { get; set; } = "另存为";

        /// <summary>
        /// 名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, "名称");
        }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value, "描述");
        }

        /// <summary>
        /// 保存命令
        /// </summary>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// 对话框结果
        /// </summary>
        public bool? DialogResult { get; private set; }

        public SaveAsDialogViewModel(string defaultName, string? defaultDescription = null, string? title = null)
        {
            _name = defaultName;
            _description = defaultDescription ?? string.Empty;
            
            if (!string.IsNullOrEmpty(title))
            {
                DialogTitle = title;
            }

            SaveCommand = new RelayCommand(_ =>
            {
                DialogResult = true;
            }, _ => !string.IsNullOrWhiteSpace(_name));
        }
    }
}
