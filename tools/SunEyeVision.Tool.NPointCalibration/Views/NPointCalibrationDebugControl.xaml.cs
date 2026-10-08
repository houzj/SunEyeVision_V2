using System;
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
        nameof(Parameters), typeof(NPointCalibrationParameters), typeof(NPointCalibrationDebugControl),
        new PropertyMetadata(null, OnParametersChanged));

    private static void OnParametersChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        var control = (NPointCalibrationDebugControl)owner;
        if (args.OldValue is NPointCalibrationParameters previous)
            previous.PropertyChanged -= control.Parameters_PropertyChanged;
        if (args.NewValue is NPointCalibrationParameters current)
            current.PropertyChanged += control.Parameters_PropertyChanged;
        control.RefreshOverview();
    }

    private void Parameters_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NPointCalibrationParameters.CalibrationFilePath)
            or nameof(NPointCalibrationParameters.TranslationCount)
            or nameof(NPointCalibrationParameters.RotationCount)
            or nameof(NPointCalibrationParameters.Samples))
            RefreshOverview();
    }

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

    private string _progressText = "平移 0/9，旋转 0/3";
    public string ProgressText
    {
        get => _progressText;
        private set { _progressText = value; OnPropertyChanged(nameof(ProgressText)); }
    }

    private string _candidateStatusText = "未设置路径";
    public string CandidateStatusText
    {
        get => _candidateStatusText;
        private set { _candidateStatusText = value; OnPropertyChanged(nameof(CandidateStatusText)); }
    }

    private void RefreshOverview()
    {
        var p = Parameters;
        if (p == null) return;
        ProgressText = $"平移 {p.Samples.Count(s => s.Kind == NPointSampleKind.Translation)}/{p.TranslationCount}，" +
            $"旋转 {p.Samples.Count(s => s.Kind == NPointSampleKind.Rotation)}/{p.RotationCount}";
        if (string.IsNullOrWhiteSpace(p.CalibrationFilePath))
        {
            CandidateStatusText = "未设置路径";
            return;
        }
        try
        {
            string candidatePath = NPointCalibrationParameters.CandidatePath(p.CalibrationFilePath);
            bool candidateExists = File.Exists(candidatePath);
            bool activeExists = File.Exists(Path.GetFullPath(p.CalibrationFilePath));
            if (!candidateExists && !activeExists)
            {
                CandidateStatusText = "未生成";
                return;
            }
            string? candidateDigest = null;
            if (candidateExists)
            {
                try { (_, candidateDigest) = NPointCalibrationFile.ReadCandidate(candidatePath); }
                catch (Exception) { CandidateStatusText = "候选文件无效"; return; }
            }
            bool published = false;
            if (activeExists)
            {
                try
                {
                    var active = NPointCalibrationFile.ReadVerified(p.CalibrationFilePath,
                        p.SourceDeviceId, p.ImageWidth, p.ImageHeight, p.ImageDomain);
                    published = !candidateExists || active.CandidateSha256 == candidateDigest;
                }
                catch (Exception) { /* An unrelated or invalid activity file is not this candidate's release. */ }
            }
            CandidateStatusText = candidateExists && p.LastWrittenRevision != p.SampleRevision
                ? "候选存在；点表已修改，请重新生成"
                : published ? "已发布"
                : candidateExists ? "候选待独立验证" : "活动文件无效或环境不匹配";
        }
        catch (Exception) { CandidateStatusText = "路径无效"; }
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
        RefreshOverview();
    }

    protected override object ExecuteTool()
    {
        var result = ExecuteWithLiveContext(new NPointCalibrationTool(), Parameters);
        if (result.IsSuccess && !Parameters.TryCommit(result, out var error))
            result.SetError(error ?? "标定点保存失败");
        ResultText = !result.IsSuccess ? result.ErrorMessage ?? "标定执行失败"
            : result is NPointCalibrationResults calibration
                ? $"{calibration.ProgressText}\n" +
                  (calibration.CalibrationReady
                      ? $"联合求解完成：RMS {calibration.RmsError:F4} mm，最大误差 {calibration.MaxError:F4} mm\n" +
                        $"轴心→Mark 偏心：({calibration.AxisToMarkX:F4}, {calibration.AxisToMarkY:F4}) mm\n" +
                        $"权重：{calibration.WeightFunction}；阈值 {calibration.RobustThresholdMm:F4} mm\n" +
                        "状态：已求解，待独立验证\n" +
                        string.Join("\n", calibration.Residuals.Select(r =>
                            $"点 {r.Id} {r.Kind}：{r.ErrorMm:F4} mm，权重 {r.Weight:F3}")) +
                        (calibration.HasConvertedPoint
                            ? $"\n本轮 Mark 机台坐标：({calibration.MarkMachineX:F4}, {calibration.MarkMachineY:F4}) mm"
                            : string.Empty)
                      : "采样未满，尚未产生联合标定结果。")
                : result.ErrorMessage ?? "标定执行失败";
        RefreshOverview();
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
            RefreshOverview();
        }
    }

    private void RecordCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (Parameters.AcquisitionMode != NPointAcquisitionMode.Trigger) return;
        // The explicit click is one new acquisition event. Existing frame/XYR pairing checks still apply.
        long previousEventId = Parameters.TeachEventId;
        bool previousSignal = Parameters.TeachSignal;
        try
        {
            Parameters.TeachEventId = Math.Max(previousEventId, Parameters.LastTeachEventId) + 1;
            Parameters.TeachSignal = true;
            ExecuteTool();
        }
        finally
        {
            Parameters.TeachSignal = previousSignal;
            Parameters.TeachEventId = previousEventId;
        }
    }

    private void ClearSamples_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("确定清空当前 N点标定的全部采样数据？", "清空标定点",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        Parameters.ReplaceSamples(Array.Empty<NPointSample>());
        ResultText = "标定点已清空。";
        RefreshOverview();
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
            // Candidate generation must never consume a held trigger or create an extra sample.
            var snapshot = new NPointCalibrationParameters();
            snapshot.CopyFrom(Parameters);
            snapshot.AcquisitionMode = NPointAcquisitionMode.Manual;
            var result = ExecuteWithLiveContext(new NPointCalibrationTool(), snapshot);
            if (result is not NPointCalibrationResults calibration || !calibration.CalibrationReady)
                throw new InvalidOperationException(result is NPointCalibrationResults pending
                    ? pending.ErrorMessage ?? "标定点不足，尚未完成求解。" : result.ErrorMessage ?? "标定未完成求解。");
            if (string.IsNullOrWhiteSpace(Parameters.CalibrationFilePath))
            {
                ChooseFile_Click(sender, e);
                if (string.IsNullOrWhiteSpace(Parameters.CalibrationFilePath)) return;
            }
            NPointCalibrationParameters.WriteCandidateFile(Parameters.CalibrationFilePath,
                calibration, Parameters.Samples, Parameters.MaximumTrainingErrorMm);
            Parameters.LastWrittenRevision = Parameters.SampleRevision;
            ResultText = $"已生成待验证候选文件：{NPointCalibrationParameters.CandidatePath(Parameters.CalibrationFilePath)}";
            RefreshOverview();
        }
        catch (Exception ex)
        {
            ResultText = ex.Message;
            MessageBox.Show(ex.Message, "生成候选文件失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
