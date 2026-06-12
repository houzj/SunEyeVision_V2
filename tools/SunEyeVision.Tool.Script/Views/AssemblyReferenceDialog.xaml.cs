using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Tool.Script.Views
{
    /// <summary>
    /// 程序集引用显示转换器 — 系统程序集显示 📦 名称，自定义 DLL 显示 📄 文件名
    /// </summary>
    public class AssemblyDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrEmpty(s))
            {
                if (s.Contains('\\') || s.Contains('/'))
                    return $"\U0001F4C4 {Path.GetFileName(s)}";  // 📄 + 文件名
                return $"\U0001F4E6 {s}";  // 📦 + 程序集名称
            }
            return value;
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public partial class AssemblyReferenceDialog : Window
    {
        private readonly ObservableCollection<string> _references;

        /// <summary>
        /// 常用程序集分类列表（名称装载的全局系统程序集）
        /// </summary>
        private static readonly Dictionary<string, string[]> CommonAssemblyCategories = new()
        {
            ["数据访问"] = new[] { "System.Data", "System.Data.Common" },
            ["XML"] = new[] { "System.Xml", "System.Xml.Linq" },
            ["网络"] = new[] { "System.Net.Http" },
            ["图像/UI"] = new[] { "System.Drawing", "System.Windows.Forms" },
            ["数学"] = new[] { "System.Numerics" },
            ["IO/压缩"] = new[] { "System.IO.Compression" },
            ["序列化"] = new[] { "System.Runtime.Serialization" },
            ["视觉引擎"] = new[] { "HalconDotNet" }
        };

        public AssemblyReferenceDialog(ObservableCollection<string> references)
        {
            InitializeComponent();
            _references = new ObservableCollection<string>(references);
            ReferenceListBox.ItemsSource = _references;
        }

        /// <summary>
        /// 获取最终的引用列表
        /// </summary>
        public ObservableCollection<string> GetReferences() => _references;

        /// <summary>
        /// 添加自定义 DLL（存储完整路径）
        /// </summary>
        private void OnAddButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择程序集 DLL",
                Filter = "程序集文件 (*.dll)|*.dll|所有文件 (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true,
                InitialDirectory = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location)
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var fullPath in dialog.FileNames)
                {
                    if (!_references.Contains(fullPath) &&
                        !_references.Contains(Path.GetFileName(fullPath)))
                    {
                        _references.Add(fullPath);
                    }
                }
            }
        }

        /// <summary>
        /// 添加常用程序集（名称装载）—— 显示分类菜单
        /// </summary>
        private void OnCommonAssembliesClick(object sender, RoutedEventArgs e)
        {
            var contextMenu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC)),
                FontSize = 12
            };

            foreach (var kvp in CommonAssemblyCategories)
            {
                // 分类标题（只读）
                contextMenu.Items.Add(new MenuItem
                {
                    Header = $"\U0001F4C1 {kvp.Key}",  // 📁
                    IsEnabled = false,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99))
                });

                foreach (var asmName in kvp.Value)
                {
                    var displayName = asmName == "HalconDotNet"
                        ? $"\U0001F4E6 {asmName}  (需手动浏览找到 halcondotnetxl.dll)"
                        : $"\U0001F4E6 {asmName}";

                    var item = new MenuItem
                    {
                        Header = displayName,
                        Tag = asmName,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC)),
                        Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30))
                    };

                    item.Click += (_, _) => AddCommonAssembly(asmName);
                    contextMenu.Items.Add(item);
                }

                // 分类间分隔线
                contextMenu.Items.Add(new Separator
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C)),
                    Height = 1,
                    Margin = new Thickness(4, 0, 4, 0)
                });
            }

            contextMenu.PlacementTarget = sender as Button;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;

            // 移除最后一个分隔线
            if (contextMenu.Items.Count > 0 && contextMenu.Items[^1] is Separator)
                contextMenu.Items.RemoveAt(contextMenu.Items.Count - 1);

            contextMenu.IsOpen = true;
        }

        /// <summary>
        /// 添加一个常用程序集（按名称装载）
        /// HalconDotNet 特殊处理：提示用户浏览选择 halcondotnetxl.dll
        /// </summary>
        private void AddCommonAssembly(string asmName)
        {
            if (string.IsNullOrEmpty(asmName))
                return;

            if (asmName == "HalconDotNet")
            {
                // HalconDotNet 是自定义 DLL，需要用户指定路径
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "请选择 halcondotnetxl.dll",
                    Filter = "HalconDotNet DLL (halcondotnetxl.dll)|halcondotnetxl.dll|所有DLL (*.dll)|*.dll",
                    Multiselect = false,
                    CheckFileExists = true,
                    FileName = "halcondotnetxl.dll"
                };

                if (dialog.ShowDialog() == true)
                {
                    if (!_references.Contains(dialog.FileName))
                        _references.Add(dialog.FileName);
                }
                return;
            }

            // 系统程序集：按名称存储，编译器自动按名称装载
            if (!_references.Contains(asmName))
                _references.Add(asmName);
        }

        private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            if (ReferenceListBox.SelectedItem is string selected)
                _references.Remove(selected);
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
