using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;

namespace SunEyeVision.Tool.NPointCalibration.Views;

/// <summary>VM-style point table; edits a copy and commits only on Confirm.</summary>
public sealed class NPointSampleEditor : Window
{
    private readonly ObservableCollection<NPointSample> _rows;
    private readonly DataGrid _grid;
    private readonly int _translationCount;

    public IReadOnlyList<NPointSample> Samples { get; private set; } = Array.Empty<NPointSample>();

    public NPointSampleEditor(NPointCalibrationParameters parameters)
    {
        Title = "编辑标定点";
        Width = 780;
        Height = 590;
        MinWidth = 670;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _translationCount = parameters.TranslationCount;
        int total = parameters.TranslationCount + parameters.RotationCount;
        _rows = new ObservableCollection<NPointSample>(Enumerable.Range(0, total).Select(index =>
        {
            var existing = parameters.Samples.FirstOrDefault(s => s.Id == index);
            return existing?.Copy() ?? new NPointSample
            {
                Id = index,
                Kind = index < _translationCount ? NPointSampleKind.Translation : NPointSampleKind.Rotation,
                Enabled = false,
            };
        }));

        var layout = new DockPanel { Margin = new Thickness(10) };
        Content = layout;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);
        AddButton(buttons, "导出", (_, _) => Export());
        AddButton(buttons, "导入", (_, _) => Import());
        AddButton(buttons, "清空标定点", (_, _) =>
        {
            if (MessageBox.Show("清空编辑表中的全部点？", "确认", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            foreach (var row in _rows) { row.Enabled = false; row.ImageX = row.ImageY = row.PhysicalX = row.PhysicalY = 0; }
            _grid.Items.Refresh();
        });
        AddButton(buttons, "确定", (_, _) => Confirm());
        AddButton(buttons, "取消", (_, _) => { DialogResult = false; });

        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            ItemsSource = _rows,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            IsReadOnly = false,
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding("Id"), IsReadOnly = true, Width = 42 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "类别", Binding = new Binding("Kind"), IsReadOnly = true, Width = 78 });
        _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "有效", Binding = new Binding("Enabled"), Width = 48 });
        AddNumberColumn("图像坐标X", "ImageX");
        AddNumberColumn("图像坐标Y", "ImageY");
        AddNumberColumn("物理坐标X", "PhysicalX");
        AddNumberColumn("物理坐标Y", "PhysicalY");
        AddNumberColumn("图像角度", "ImageAngle");
        AddNumberColumn("物理角度", "PhysicalAngle");
        layout.Children.Add(_grid);
    }

    private void AddNumberColumn(string title, string property)
        => _grid.Columns.Add(new DataGridTextColumn
        {
            Header = title,
            Binding = new Binding(property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
        });

    private static void AddButton(Panel panel, string title, RoutedEventHandler onClick)
    {
        var button = new Button { Content = title, Margin = new Thickness(3), Padding = new Thickness(9, 4, 9, 4) };
        button.Click += onClick;
        panel.Children.Add(button);
    }

    private void Confirm()
    {
        if (!_grid.CommitEdit(DataGridEditingUnit.Cell, true)
            || !_grid.CommitEdit(DataGridEditingUnit.Row, true))
        {
            MessageBox.Show("点表中有未完成或无效的编辑。", "点表错误");
            return;
        }
        var valid = _rows.Where(r => r.Enabled).ToList();
        if (valid.Any(r => !double.IsFinite(r.ImageX) || !double.IsFinite(r.ImageY)
                           || !double.IsFinite(r.PhysicalX) || !double.IsFinite(r.PhysicalY)))
        {
            MessageBox.Show("存在非有限数值的标定点。", "点表错误");
            return;
        }
        Samples = valid.Select(r => r.Copy()).ToArray();
        DialogResult = true;
    }

    private void Export()
    {
        var dialog = new SaveFileDialog { Filter = "标定点 CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(this) != true) return;
        var lines = new List<string> { "Id,Kind,Enabled,ImageX,ImageY,PhysicalX,PhysicalY,ImageAngle,PhysicalAngle" };
        lines.AddRange(_rows.Select(r => string.Join(",",
            r.Id, r.Kind, r.Enabled, F(r.ImageX), F(r.ImageY), F(r.PhysicalX), F(r.PhysicalY),
            F(r.ImageAngle), F(r.PhysicalAngle))));
        File.WriteAllLines(dialog.FileName, lines);
    }

    private void Import()
    {
        var dialog = new OpenFileDialog { Filter = "标定点 CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var loaded = File.ReadLines(dialog.FileName).Skip(1).Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(Parse).ToArray();
            if (loaded.Any(r => r.Id < 0 || r.Id >= _rows.Count)
                || loaded.Select(r => r.Id).Distinct().Count() != loaded.Length)
                throw new FormatException("点号超出当前平移/旋转次数或存在重复。");
            foreach (var row in loaded) _rows[row.Id] = row;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "导入标定点失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static NPointSample Parse(string line)
    {
        var values = line.Split(',');
        if (values.Length != 9) throw new FormatException("CSV 列数错误。");
        return new NPointSample
        {
            Id = int.Parse(values[0], CultureInfo.InvariantCulture),
            Kind = Enum.Parse<NPointSampleKind>(values[1]),
            Enabled = bool.Parse(values[2]),
            ImageX = double.Parse(values[3], CultureInfo.InvariantCulture),
            ImageY = double.Parse(values[4], CultureInfo.InvariantCulture),
            PhysicalX = double.Parse(values[5], CultureInfo.InvariantCulture),
            PhysicalY = double.Parse(values[6], CultureInfo.InvariantCulture),
            ImageAngle = double.Parse(values[7], CultureInfo.InvariantCulture),
            PhysicalAngle = double.Parse(values[8], CultureInfo.InvariantCulture),
        };
    }
}
