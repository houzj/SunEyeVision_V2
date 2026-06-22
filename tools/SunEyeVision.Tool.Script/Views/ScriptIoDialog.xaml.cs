using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using SunEyeVision.Tool.Script.Models;

namespace SunEyeVision.Tool.Script.Views
{
    /// <summary>
    /// 脚本变量管理对话框 — 添加/删除/编辑输入和输出变量。
    /// </summary>
    public partial class ScriptIoDialog : Window
    {
        private readonly ObservableCollection<ScriptInputConfig> _inputs;
        private readonly ObservableCollection<ScriptOutputConfig> _outputs;

        /// <summary>
        /// 可用类型列表（与 CsTypeName 映射一致）
        /// </summary>
        public string[] AvailableTypes { get; } =
        [
            "Image", "Integer", "Double", "Float", "String", "Boolean", "Byte",
            "Point", "Rectangle", "Size",
            "Region", "Circle", "Line", "Annulus", "RotatedRect", "Polygon"
        ];

        public ScriptIoDialog(
            ObservableCollection<ScriptInputConfig> inputs,
            ObservableCollection<ScriptOutputConfig> outputs)
        {
            InitializeComponent();

            _inputs = inputs;
            _outputs = outputs;

            // 复制一份副本，取消时不修改原数据
            InputGrid.ItemsSource = new ObservableCollection<ScriptInputConfig>(
                inputs.Select(i => new ScriptInputConfig
                {
                    Name = i.Name,
                    TypeName = i.TypeName,
                    BindingSource = i.BindingSource,
                    BindingDisplayText = i.BindingDisplayText,
                    Source = i.Source
                }));

            OutputGrid.ItemsSource = new ObservableCollection<ScriptOutputConfig>(
                outputs.Select(o => new ScriptOutputConfig
                {
                    Name = o.Name,
                    TypeName = o.TypeName,
                    IsAccumulated = o.IsAccumulated,
                    BindingSource = o.BindingSource,
                    BindingDisplayText = o.BindingDisplayText,
                    Source = o.Source
                }));

            DataContext = this;
        }

        private void AddInput_Click(object sender, RoutedEventArgs e)
        {
            if (InputGrid.ItemsSource is ObservableCollection<ScriptInputConfig> list)
            {
                list.Add(new ScriptInputConfig
                {
                    Name = $"input{list.Count + 1}",
                    TypeName = "Integer"
                });
            }
        }

        private void DeleteInput_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element &&
                element.DataContext is ScriptInputConfig config &&
                InputGrid.ItemsSource is ObservableCollection<ScriptInputConfig> list)
            {
                list.Remove(config);
            }
        }

        private void AddOutput_Click(object sender, RoutedEventArgs e)
        {
            if (OutputGrid.ItemsSource is ObservableCollection<ScriptOutputConfig> list)
            {
                list.Add(new ScriptOutputConfig
                {
                    Name = $"output{list.Count + 1}",
                    TypeName = "Double"
                });
            }
        }

        private void DeleteOutput_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element &&
                element.DataContext is ScriptOutputConfig config &&
                OutputGrid.ItemsSource is ObservableCollection<ScriptOutputConfig> list)
            {
                list.Remove(config);
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (InputGrid.ItemsSource is ObservableCollection<ScriptInputConfig> editedInputs &&
                OutputGrid.ItemsSource is ObservableCollection<ScriptOutputConfig> editedOutputs)
            {
                // 同步回原始集合
                _inputs.Clear();
                foreach (var item in editedInputs)
                    _inputs.Add(item);

                _outputs.Clear();
                foreach (var item in editedOutputs)
                    _outputs.Add(item);
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
