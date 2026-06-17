using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Tool.Script.Views
{
    /// <summary>
    /// 程序集引用显示转换器 — 显示 DLL 文件名
    /// </summary>
    public class AssemblyDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrEmpty(s))
            {
                return $"\U0001F4C4 {Path.GetFileName(s)}";
            }
            return value;
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// 程序集引用管理对话框。
    /// 所有操作均针对引用目录下的 *.dll 文件（扫描、复制、删除）。
    /// </summary>
    public partial class AssemblyReferenceDialog : Window
    {
        private readonly ObservableCollection<string> _dllFiles;
        private readonly string _refDir;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="refDir">引用目录完整路径</param>
        public AssemblyReferenceDialog(string refDir)
        {
            InitializeComponent();

            _refDir = refDir;
            _dllFiles = new ObservableCollection<string>();

            // 确保目录存在
            Directory.CreateDirectory(_refDir);

            // 扫描所有 DLL
            RefreshList();

            ReferenceListBox.ItemsSource = _dllFiles;
        }

        /// <summary>
        /// 刷新列表：重新扫描目录下所有 *.dll
        /// </summary>
        private void RefreshList()
        {
            _dllFiles.Clear();
            foreach (var dll in Directory.EnumerateFiles(_refDir, "*.dll").OrderBy(f => Path.GetFileName(f)))
            {
                _dllFiles.Add(dll);
            }
        }

        /// <summary>
        /// + 添加 DLL — 复制文件到引用目录
        /// </summary>
        private void OnAddButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要添加的程序集 DLL",
                Filter = "程序集文件 (*.dll)|*.dll|所有文件 (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var sourcePath in dialog.FileNames)
                {
                    var fileName = Path.GetFileName(sourcePath);
                    var destPath = Path.Combine(_refDir, fileName);

                    // 同名文件已存在则跳过
                    if (File.Exists(destPath))
                        continue;

                    File.Copy(sourcePath, destPath);
                }

                RefreshList();
            }
        }

        /// <summary>
        /// ✕ 删除按钮 — 从目录中删除 DLL 文件
        /// </summary>
        private void OnItemDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is string path && File.Exists(path))
            {
                File.Delete(path);
                _dllFiles.Remove(path);
            }
        }

        private void OnOkButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
