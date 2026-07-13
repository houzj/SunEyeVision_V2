using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace SunEyeVision.Tool.Script.Views
{
    /// <summary>
    /// DLL 引用条目
    /// </summary>
    public record DllItem(string FullPath, string DisplayName)
    {
        public string DisplayName => Path.GetFileName(FullPath);
    }

    /// <summary>
    /// 程序集引用管理对话框。只包含一个 Tab：引用程序集。
    /// ScriptRefs 目录的所有 *.dll 均可在 UI 中查看/添加/删除。
    /// </summary>
    public partial class AssemblyReferenceDialog : Window
    {
        private readonly ObservableCollection<DllItem> _refItems = new();
        private readonly string _refDir;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="refDir">引用目录完整路径（ScriptRefs）</param>
        public AssemblyReferenceDialog(string refDir)
        {
            InitializeComponent();

            _refDir = refDir;

            // 确保目录存在
            Directory.CreateDirectory(_refDir);

            // 刷新列表
            RefreshRefList();

            UserRefListBox.ItemsSource = _refItems;
        }

        /// <summary>
        /// 刷新引用程序集列表
        /// </summary>
        private void RefreshRefList()
        {
            _refItems.Clear();
            foreach (var dll in Directory.EnumerateFiles(_refDir, "*.dll")
                .OrderBy(f => Path.GetFileName(f)))
            {
                _refItems.Add(new DllItem(dll, Path.GetFileName(dll)));
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

                RefreshRefList();
            }
        }

        /// <summary>
        /// ✕ 删除按钮 — 从目录中删除 DLL 文件
        /// </summary>
        private void OnItemDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is DllItem item && File.Exists(item.FullPath))
            {
                File.Delete(item.FullPath);
                _refItems.Remove(item);
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
