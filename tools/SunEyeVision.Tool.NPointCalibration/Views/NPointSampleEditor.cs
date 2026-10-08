using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace SunEyeVision.Tool.NPointCalibration.Views;

/// <summary>Theme-aware point table; edits a copy and commits only on Confirm.</summary>
public partial class NPointSampleEditor : Window
{
    private readonly ObservableCollection<NPointSample> _rows;
    private readonly int _translationCount;

    public IReadOnlyList<NPointSample> Samples { get; private set; } = Array.Empty<NPointSample>();

    public NPointSampleEditor(NPointCalibrationParameters parameters)
    {
        InitializeComponent();
        _translationCount = parameters.TranslationCount;
        int total = parameters.TranslationCount + parameters.RotationCount;
        var source = parameters.Samples.Concat(parameters.ArchivedSamples.AsEnumerable().Reverse());
        _rows = new ObservableCollection<NPointSample>(Enumerable.Range(0, total).Select(index =>
        {
            var existing = source.FirstOrDefault(s => s.Id == index);
            return existing?.Copy() ?? new NPointSample
            {
                Id = index,
                Kind = index < _translationCount ? NPointSampleKind.Translation : NPointSampleKind.Rotation,
                Enabled = false,
                Source = "Placeholder",
            };
        }));
        SampleGrid.ItemsSource = _rows;
    }

    private void Export_Click(object sender, RoutedEventArgs e) => Export();
    private void Import_Click(object sender, RoutedEventArgs e) => Import();
    private void Confirm_Click(object sender, RoutedEventArgs e) => Confirm();
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("清空编辑表中的全部点？", "确认", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        foreach (var row in _rows)
        {
            row.Enabled = false;
            row.ImageX = row.ImageY = row.AxisX = row.AxisY = row.AxisAngle = 0;
            row.Source = "Placeholder";
        }
        SampleGrid.Items.Refresh();
    }

    private void Enabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: NPointSample row } && row.Enabled && row.Source == "Placeholder")
            row.Source = "Manual";
        SampleGrid.Items.Refresh();
    }

    private void Confirm()
    {
        if (!SampleGrid.CommitEdit(DataGridEditingUnit.Cell, true)
            || !SampleGrid.CommitEdit(DataGridEditingUnit.Row, true))
        {
            MessageBox.Show("点表中有未完成或无效的编辑。", "点表错误");
            return;
        }
        var valid = _rows.Where(r => r.Enabled).ToList();
        if (valid.Any(r => !double.IsFinite(r.ImageX) || !double.IsFinite(r.ImageY)
                           || !double.IsFinite(r.AxisX) || !double.IsFinite(r.AxisY)
                           || !double.IsFinite(r.AxisAngle)))
        {
            MessageBox.Show("存在非有限数值的标定点。", "点表错误");
            return;
        }
        Samples = _rows.Where(r => r.Source != "Placeholder").Select(r => r.Copy()).ToArray();
        DialogResult = true;
    }

    private void Export()
    {
        var dialog = new SaveFileDialog { Filter = "标定点 CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(this) != true) return;
        var lines = new List<string> { "Id,Kind,Enabled,ImageX,ImageY,AxisX,AxisY,AxisAngle" };
        lines.AddRange(_rows.Where(r => r.Source != "Placeholder").Select(r => string.Join(",",
            r.Id, r.Kind, r.Enabled, F(r.ImageX), F(r.ImageY), F(r.AxisX), F(r.AxisY), F(r.AxisAngle))));
        File.WriteAllLines(dialog.FileName, lines);
    }

    private void Import()
    {
        var dialog = new OpenFileDialog { Filter = "标定点 CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var lines = File.ReadAllLines(dialog.FileName);
            if (lines.Length == 0 || lines[0].Trim() != "Id,Kind,Enabled,ImageX,ImageY,AxisX,AxisY,AxisAngle")
                throw new FormatException("CSV 表头不匹配；旧研发点表不做兼容，请重新采集。");
            var loaded = lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(Parse).ToArray();
            if (loaded.Any(r => r.Id < 0 || r.Id >= _rows.Count)
                || loaded.Select(r => r.Id).Distinct().Count() != loaded.Length)
                throw new FormatException("点号超出当前平移/旋转次数或存在重复。");
            if (loaded.Any(r => r.Kind != (r.Id < _translationCount
                    ? NPointSampleKind.Translation : NPointSampleKind.Rotation)))
                throw new FormatException("CSV 点类别与当前平移/旋转点号不一致。");
            for (int i = 0; i < _rows.Count; i++)
                _rows[i] = new NPointSample
                {
                    Id = i,
                    Kind = i < _translationCount ? NPointSampleKind.Translation : NPointSampleKind.Rotation,
                    Enabled = false,
                    Source = "Placeholder",
                };
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
        if (values.Length != 8) throw new FormatException("CSV 列数错误。");
        return new NPointSample
        {
            Id = int.Parse(values[0], CultureInfo.InvariantCulture),
            Kind = Enum.Parse<NPointSampleKind>(values[1]),
            Enabled = bool.Parse(values[2]),
            ImageX = double.Parse(values[3], CultureInfo.InvariantCulture),
            ImageY = double.Parse(values[4], CultureInfo.InvariantCulture),
            AxisX = double.Parse(values[5], CultureInfo.InvariantCulture),
            AxisY = double.Parse(values[6], CultureInfo.InvariantCulture),
            AxisAngle = double.Parse(values[7], CultureInfo.InvariantCulture),
        };
    }
}
