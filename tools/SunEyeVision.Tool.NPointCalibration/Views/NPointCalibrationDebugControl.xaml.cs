using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.NPointCalibration.Views;

public partial class NPointCalibrationDebugControl : ToolDebugControlBase
{
    public static readonly DependencyProperty ParametersProperty = DependencyProperty.Register(
        nameof(Parameters), typeof(NPointCalibrationParameters), typeof(NPointCalibrationDebugControl));

    public NPointCalibrationParameters Parameters
    {
        get => (NPointCalibrationParameters)GetValue(ParametersProperty);
        set => SetValue(ParametersProperty, value);
    }

    private string _resultText = "尚未执行";
    public string ResultText
    {
        get => _resultText;
        set { _resultText = value; OnPropertyChanged(nameof(ResultText)); }
    }

    public NPointCalibrationDebugControl()
    {
        InitializeComponent();
        Parameters = new NPointCalibrationParameters();
    }

    public override void SetParameters(ToolParameters parameters)
    {
        base.SetParameters(parameters);
        Parameters = parameters as NPointCalibrationParameters
            ?? throw new InvalidOperationException("N点标定参数类型不匹配。");
    }

    protected override object ExecuteTool()
    {
        var result = ExecuteWithLiveContext(new NPointCalibrationTool(), Parameters);
        if (result.IsSuccess && !Parameters.TryCommit(result, out var error))
            result.SetError(error ?? "标定点保存失败");
        ResultText = result is NPointCalibrationResults calibration
            ? $"{calibration.ProgressText}\n" +
              (calibration.CalibrationReady
                  ? $"求解完成：平均误差 {calibration.MeanError:F4} mm，最大误差 {calibration.MaxError:F4} mm" +
                    (calibration.HasConvertedPoint
                        ? $"\n本轮转换物理坐标：({calibration.ConvertedPhysicalX:F4}, {calibration.ConvertedPhysicalY:F4}) mm"
                        : string.Empty)
                  : "采样未满，尚未产生标定矩阵。")
            : result.ErrorMessage ?? "标定执行失败";
        return result;
    }

    protected override bool CanExecuteTool() => Parameters != null;

    private void EditSamples_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NPointSampleEditor(Parameters) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            Parameters.ReplaceSamples(dialog.Samples);
            ResultText = $"已编辑 {Parameters.Samples.Count} 个标定点；请重新求解。";
        }
    }

    private void ClearSamples_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("确定清空当前 N点标定的全部采样数据？", "清空标定点",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        Parameters.ReplaceSamples(Array.Empty<NPointSample>());
        ResultText = "标定点已清空。";
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "标定文件 (*.json)|*.json", FileName = "NPointCalibration.json" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            Parameters.CalibrationFilePath = dialog.FileName;
    }

    private void SaveCalibration_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = ExecuteWithLiveContext(new NPointCalibrationTool(), Parameters);
            if (result is not NPointCalibrationResults calibration || !calibration.CalibrationReady)
                throw new InvalidOperationException(result.ErrorMessage ?? "采样未完成，不能生成标定文件。");
            if (string.IsNullOrWhiteSpace(Parameters.CalibrationFilePath))
            {
                ChooseFile_Click(sender, e);
                if (string.IsNullOrWhiteSpace(Parameters.CalibrationFilePath)) return;
            }
            NPointCalibrationParameters.WriteCalibrationFile(Parameters.CalibrationFilePath,
                calibration, Parameters.Samples);
            Parameters.LastWrittenRevision = Parameters.SampleRevision;
            ResultText = $"已生成标定文件：{Parameters.CalibrationFilePath}";
        }
        catch (Exception ex)
        {
            ResultText = ex.Message;
            MessageBox.Show(ex.Message, "生成标定文件失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
