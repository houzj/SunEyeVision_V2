using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using SunEyeVision.Tool.Script.ScriptEngine;

namespace SunEyeVision.Tool.Script.Views
{
    /// <summary>
    /// 程序集引用勾选条目（DisplayName + IsChecked + IsEditable）
    /// </summary>
    public sealed class AssemblyRefItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public string Name { get; }
        public string DisplayName { get; }
        public bool IsEditable { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public AssemblyRefItem(string name, string displayName, bool isChecked, bool isEditable)
        {
            Name = name;
            DisplayName = displayName;
            _isChecked = isChecked;
            IsEditable = isEditable;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// 程序集引用管理对话框 — 当前节点勾选列表（唯一真相源：ScriptParameters.AssemblyReferences）。
    /// 一张列表：系统必需项（BCL + Script 基类）锁定勾选不可取消，可选宿主（halcon/OpenCvSharp）
    /// 与 ScriptRefs 用户 DLL 可勾选。勾选变化直接写回当前节点的 AssemblyReferences。
    /// </summary>
    public partial class AssemblyReferenceDialog : Window
    {
        private readonly ObservableCollection<AssemblyRefItem> _refItems = new();
        private readonly string _refDir;
        private readonly ObservableCollection<string> _assemblyReferences;
        private readonly List<string> _requiredNames;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="refDir">ScriptRefs 引用目录完整路径（用户 DLL 来源）</param>
        /// <param name="assemblyReferences">当前节点启用的程序集引用列表（唯一真相源，勾选写回此处）</param>
        public AssemblyReferenceDialog(string refDir, ObservableCollection<string> assemblyReferences)
        {
            InitializeComponent();

            _refDir = refDir;
            _assemblyReferences = assemblyReferences;
            _requiredNames = MetadataReferenceProvider.RequiredAssemblyNames.ToList();

            // 确保目录存在
            Directory.CreateDirectory(_refDir);

            BuildRefList();
            RefCheckListBox.ItemsSource = _refItems;
        }

        /// <summary>
        /// 构建勾选列表：系统必需项（锁定）+ 可选宿主（halcon/OpenCvSharp）+ ScriptRefs 用户 DLL
        /// </summary>
        private void BuildRefList()
        {
            _refItems.Clear();
            var enabled = new HashSet<string>(_assemblyReferences, StringComparer.OrdinalIgnoreCase);

            // 1. 系统必需项（BCL + Script 基类）— 锁定勾选，不可取消
            foreach (var name in _requiredNames.OrderBy(n => n))
            {
                _refItems.Add(new AssemblyRefItem(name, name, isChecked: true, isEditable: false));
            }

            // 2. 可选宿主（halcon/OpenCvSharp）— 可勾选
            foreach (var kvp in MetadataReferenceProvider.GetAvailableApis().OrderBy(k => k.Key))
            {
                var isChecked = enabled.Contains(kvp.Key) || _requiredNames.Contains(kvp.Key);
                _refItems.Add(new AssemblyRefItem(kvp.Key, kvp.Key, isChecked, isEditable: true));
            }

            // 3. ScriptRefs 目录用户 DLL — 可勾选
            foreach (var dll in Directory.EnumerateFiles(_refDir, "*.dll")
                .OrderBy(f => Path.GetFileName(f)))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (_requiredNames.Contains(name)) continue;
                var isChecked = enabled.Contains(name);
                _refItems.Add(new AssemblyRefItem(name, Path.GetFileName(dll), isChecked, isEditable: true));
            }
        }

        /// <summary>
        /// 勾选变化 — 写回当前节点 AssemblyReferences（唯一真相源）
        /// </summary>
        private void OnRefCheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox { DataContext: AssemblyRefItem item })
            {
                // 系统必需项锁定：不可取消
                if (!item.IsEditable)
                {
                    item.IsChecked = true;
                    return;
                }

                if (item.IsChecked)
                {
                    if (!_assemblyReferences.Contains(item.Name, StringComparer.OrdinalIgnoreCase))
                        _assemblyReferences.Add(item.Name);
                }
                else
                {
                    var found = _assemblyReferences.FirstOrDefault(n =>
                        n.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                        _assemblyReferences.Remove(found);
                }
            }
        }

        /// <summary>
        /// + 添加用户 DLL — 复制文件到 ScriptRefs 目录并加入勾选列表
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
                    if (File.Exists(destPath)) continue;
                    File.Copy(sourcePath, destPath);
                }

                // 重建列表，新 DLL 默认不勾选（用户手动勾选）
                BuildRefList();
                RefCheckListBox.ItemsSource = _refItems;
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
