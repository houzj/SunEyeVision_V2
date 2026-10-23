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
    private readonly NPointSample[] _original;

    public IReadOnlyList<NPointSample> Samples { get; private set; } = Array.Empty<NPointSample>();

    public NPointSampleEditor(NPointCalibrationParameters parameters)
    {
        InitializeComponent();
        _translationCount = parameters.TranslationCount;
        _original = parameters.Samples.Select(s=>s.Copy()).ToArray();
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
        if (!CalibrationDialogs.Confirm(this,"清空点表","清空编辑副本？仅按确定后提交，不删除正式文件。")) return;
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
            CalibrationDialogs.Error(this,"点表错误","点表中有未完成或无效的编辑。");
            return;
        }
        var valid = _rows.Where(r => r.Enabled).ToList();
        if (valid.Any(r => !double.IsFinite(r.ImageX) || !double.IsFinite(r.ImageY)
                           || !double.IsFinite(r.AxisX) || !double.IsFinite(r.AxisY)
                           || !double.IsFinite(r.AxisAngle)))
        {
            CalibrationDialogs.Error(this,"点表错误","存在非有限数值的标定点。");
            return;
        }
        foreach(var row in valid)
        {
            var old=_original.FirstOrDefault(s=>s.Id==row.Id);
            if(old==null || old.ImageX!=row.ImageX || old.ImageY!=row.ImageY || old.AxisX!=row.AxisX || old.AxisY!=row.AxisY || old.AxisAngle!=row.AxisAngle)
            {row.Source="ManualModified";row.FrameId=null;row.FrameCapturedAt=null;row.AxisReadAt=null;row.SessionId="";row.StepId=0;row.CameraId="";row.WorkPlaneId="";}
        }
        Samples = _rows.Where(r => r.Source != "Placeholder").Select(r => r.Copy()).ToArray();
        DialogResult = true;
    }

    private void Export()
    {
        if(!SampleGrid.CommitEdit(DataGridEditingUnit.Cell,true) || !SampleGrid.CommitEdit(DataGridEditingUnit.Row,true))return;
        var dialog = new SaveFileDialog { Filter = "标定点 CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(this) != true) return;
        File.WriteAllLines(dialog.FileName,NPointSampleCsv.Export(_rows));
    }

    private void Import()
    {
        var dialog = new OpenFileDialog { Filter = "标定点 CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var loaded=NPointSampleCsv.Parse(File.ReadAllLines(dialog.FileName),_translationCount,_rows.Count-_translationCount);
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
            CalibrationDialogs.Error(this,"导入标定点失败",ex.Message);
        }
    }

}
