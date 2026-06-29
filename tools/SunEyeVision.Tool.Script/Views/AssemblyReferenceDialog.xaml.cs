using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Tool.Script.Views
{
    /// <summary>
    /// 内置程序集状态显示转换器 — 显示 ✓（存在）或 ✗（缺失）
    /// </summary>
    public class AssemblyStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool exists)
                return exists ? "✓" : "✗";
            return "?";
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// 内置程序集条目（只读显示）
    /// </summary>
    public record BuiltInAssemblyItem(string DisplayName, bool Status);

    /// <summary>
    /// 用户自定义 DLL 条目（可管理）
    /// </summary>
    public record UserCustomDllItem(string FullPath, string DisplayName)
    {
        public string DisplayName => Path.GetFileName(FullPath);
    }

    /// <summary>
    /// 程序集引用管理对话框。
    /// 两标签页：内置程序集（只读：7 个关键 DLL 状态）+ 用户自定义DLL（可添加/删除）。
    /// ScriptRefs 目录由 CopyScriptRefs.ps1 自动化部署，用户仅管理自定义 DLL。
    /// </summary>
    public partial class AssemblyReferenceDialog : Window
    {
        // CopyScriptRefs.ps1 部署的 7 个关键 DLL 名称
        private static readonly HashSet<string> _criticalNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "halcondotnetxl.dll",
            "OpenCvSharp.dll",
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll",
            "System.Windows.Forms.dll",
            "netstandard.dll"
        };

        private readonly ObservableCollection<BuiltInAssemblyItem> _builtInItems = new();
        private readonly ObservableCollection<UserCustomDllItem> _userCustomItems = new();
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

            // 刷新两个标签页列表
            RefreshAll();

            BuiltInAssemblyListBox.ItemsSource = _builtInItems;
            UserCustomListBox.ItemsSource = _userCustomItems;
        }

        /// <summary>
        /// 刷新所有列表
        /// </summary>
        private void RefreshAll()
        {
            RefreshBuiltInList();
            RefreshUserCustomList();
        }

        /// <summary>
        /// 刷新内置程序集列表（只读：7 个关键 DLL 的存在状态）
        /// </summary>
        private void RefreshBuiltInList()
        {
            _builtInItems.Clear();
            var existingFiles = new HashSet<string>(
                Directory.EnumerateFiles(_refDir, "*.dll")
                    .Select(f => Path.GetFileName(f)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var name in _criticalNames.OrderBy(n => n))
            {
                _builtInItems.Add(new BuiltInAssemblyItem(name, existingFiles.Contains(name)));
            }
        }

        /// <summary>
        /// 刷新用户自定义 DLL 列表（不在 7 个关键列表中的 DLL）
        /// </summary>
        private void RefreshUserCustomList()
        {
            _userCustomItems.Clear();
            foreach (var dll in Directory.EnumerateFiles(_refDir, "*.dll")
                .Where(f => !_criticalNames.Contains(Path.GetFileName(f)))
                .OrderBy(f => Path.GetFileName(f)))
            {
                _userCustomItems.Add(new UserCustomDllItem(dll, Path.GetFileName(dll)));
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

                    // 禁止覆盖内置关键 DLL
                    if (_criticalNames.Contains(fileName))
                        continue;

                    File.Copy(sourcePath, destPath);
                }

                RefreshAll();
            }
        }

        /// <summary>
        /// ✕ 删除按钮 — 从目录中删除自定义 DLL 文件
        /// </summary>
        private void OnItemDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is UserCustomDllItem item && File.Exists(item.FullPath))
            {
                // 禁止删除内置关键 DLL（安全防护）
                if (_criticalNames.Contains(Path.GetFileName(item.FullPath)))
                    return;

                File.Delete(item.FullPath);
                _userCustomItems.Remove(item);
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
