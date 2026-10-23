using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using System.Windows.Threading;

namespace SunEyeVision.Tool.NPointCalibration.Views;

public partial class NPointCalibrationDebugControl : ToolDebugControlBase
{
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private string ActivityPath => NPointCalibrationParameters.ResolveActivityPath(Parameters.CalibrationFilePath, (DataProvider as DataSourceQueryService)?.ResourcePaths);
    private string _seenFingerprint = "";
    private string _lastError = "";
    public NPointCalibrationResults? LatestResult { get; private set; }
    public string StageText => Parameters.Samples.Count(s => s.Kind == NPointSampleKind.Translation) < Parameters.TranslationCount ? "平移采样" : "旋转采样 / 待求解";
    public string CandidatePathText {get {try{return string.IsNullOrWhiteSpace(Parameters.CalibrationFilePath)?"未设置":NPointCalibrationParameters.CandidatePath(ActivityPath);}catch(Exception){return "路径无效";}}}
    public string MatrixText => LatestResult?.HomMat2D.Length == 6 ? string.Join("  ", LatestResult.HomMat2D.Select(v => v.ToString("G12"))) : "尚未求解";
    public string SourceInfoText => Parameters.Samples.LastOrDefault(s => s.FrameId.HasValue) is { } s
        ? $"相机：{s.CameraId}；工作平面：{s.WorkPlaneId}\n会话：{s.SessionId}；运动步：{s.StepId}；帧：{s.FrameId}"
        : "当前为手动点表或未提供采样上下文。触发采样需上游提供到位/新帧/XYR证据，不能手填元数据。";
    public bool IsCurrentResult => LatestResult != null && LatestResult.AcceptedFingerprint == Parameters.Fingerprint();
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
        if (p.RuntimeResult != null) LatestResult = p.RuntimeResult;
        OnPropertyChanged(nameof(LatestResult)); OnPropertyChanged(nameof(StageText)); OnPropertyChanged(nameof(MatrixText)); OnPropertyChanged(nameof(SourceInfoText));
        try { OnPropertyChanged(nameof(CandidatePathText)); } catch (Exception) { }
        if (LatestResult != null && !IsCurrentResult) ResultText = "结果已过期；请重新执行标定。";
        else if (LatestResult != null)
            ResultText = !LatestResult.IsSuccess ? LatestResult.ErrorMessage ?? "标定执行失败"
                : LatestResult.TrainingPassed ? "HALCON原生标定训练合格；候选须经独立验证才能发布。"
                : "采样未满，尚未求解。";
        if(!string.IsNullOrEmpty(_lastError))ResultText=_lastError;
        ProgressText = $"平移 {p.Samples.Count(s => s.Kind == NPointSampleKind.Translation)}/{p.TranslationCount}，" +
            $"旋转 {p.Samples.Count(s => s.Kind == NPointSampleKind.Rotation)}/{p.RotationCount}";
        if (string.IsNullOrWhiteSpace(p.CalibrationFilePath))
        {
            CandidateStatusText = "未设置路径";
            return;
        }
        try
        {
            string candidatePath = NPointCalibrationParameters.CandidatePath(ActivityPath);
            bool candidateExists = File.Exists(candidatePath);
            bool activeExists = File.Exists(ActivityPath);
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
                    var active = NPointCalibrationFile.ReadVerified(ActivityPath,
                        p.SourceDeviceId, p.ImageWidth, p.ImageHeight, p.ImageDomain);
                    published = !candidateExists || active.CandidateSha256 == candidateDigest;
                }
                catch (Exception) { /* An unrelated or invalid activity file is not this candidate's release. */ }
            }
            CandidateStatusText = candidateExists && p.LastWrittenFingerprint != p.Fingerprint()
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
        _refresh.Tick += (_, _) => { var key = Parameters.Fingerprint(); if (key != _seenFingerprint || Parameters.RuntimeResult != LatestResult) { _seenFingerprint = key; RefreshOverview(); } };
        Loaded += (_, _) => _refresh.Start(); Unloaded += (_, _) => _refresh.Stop();
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
        LatestResult = result as NPointCalibrationResults;
        ResultText = !result.IsSuccess ? result.ErrorMessage ?? "标定执行失败"
            : LatestResult?.CalibrationReady == true ? "HALCON原生标定训练合格；候选须经独立验证才能发布。" : "采样未满，尚未求解。";
        _lastError = result.IsSuccess ? "" : ResultText;
        RefreshOverview();
        return result;
    }

    protected override bool CanExecuteTool() => Parameters != null;

    private void EditSamples_Click(object sender, RoutedEventArgs e)
    {
        var expected = Parameters.Fingerprint();
        var dialog = new NPointSampleEditor(Parameters) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            if(expected!=Parameters.Fingerprint()){CalibrationDialogs.Error(this,"点表已变化","编辑期间参数或采样已更新，请重新打开点表。");return;}
            Parameters.ReplaceSamples(dialog.Samples);
            ResultText = $"已编辑 {Parameters.Samples.Count} 个标定点；请重新求解。";
            RefreshOverview();
        }
    }

    private void RecordCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (Parameters.AcquisitionMode != NPointAcquisitionMode.Trigger) return;
        // Do not manufacture an event/frame/position timestamp in the editor.
        bool previousSignal = Parameters.TeachSignal;
        try
        {
            Parameters.TeachSignal = true;
            ExecuteTool();
        }
        finally
        {
            Parameters.TeachSignal = previousSignal;
        }
    }

    private void ClearSamples_Click(object sender, RoutedEventArgs e)
    {
        if (!CalibrationDialogs.Confirm(this,"清空标定点","确定清空当前标定点？求解结果将失效；已发布正式文件不会删除。"))
            return;
        Parameters.ReplaceSamples(Array.Empty<NPointSample>());
        LatestResult = null;
        _lastError = "";
        ResultText = "标定点已清空。";
        RefreshOverview();
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title="选择正式文件目标路径（仅选择，不写入）", Filter = "标定文件 (*.json)|*.json", FileName = "NPointCalibration.json", OverwritePrompt=false };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            Parameters.CalibrationFilePath = dialog.FileName;
    }

    private void SaveCalibration_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var calibration = LatestResult;
            if (calibration == null || !calibration.IsSuccess || !calibration.TrainingPassed || !IsCurrentResult)
                throw new InvalidOperationException("请先设置目标路径并重新执行标定；不使用过期或未合格结果。");
            if (string.IsNullOrWhiteSpace(Parameters.CalibrationFilePath))
            {
                throw new InvalidOperationException("未设置正式目标路径，请设置后重新执行标定。");
            }
            NPointCalibrationParameters.WriteCandidateFile(ActivityPath,
                calibration, Parameters.Samples, Parameters.MaximumTrainingErrorMm,Parameters.ArchivedSamples);
            Parameters.LastWrittenRevision = Parameters.SampleRevision;
            Parameters.LastWrittenFingerprint = Parameters.Fingerprint();
            ResultText = $"已生成待验证候选文件：{NPointCalibrationParameters.CandidatePath(ActivityPath)}";
            RefreshOverview();
        }
        catch (Exception ex)
        {
            ResultText = ex.Message;
            CalibrationDialogs.Error(this,"生成候选文件失败",ex.Message);
        }
    }
    private void Solve_Click(object sender,RoutedEventArgs e)
    {
        var mode=Parameters.AcquisitionMode;
        try { Parameters.AcquisitionMode=NPointAcquisitionMode.Manual; ExecuteTool(); }
        finally { Parameters.AcquisitionMode=mode; }
    }
    private void ShowResiduals_Click(object sender,RoutedEventArgs e)
    {
        if(LatestResult==null) { CalibrationDialogs.Error(this,"逐点误差","尚无标定结果。");return; }
        CalibrationDialogs.Residuals(this,LatestResult,IsCurrentResult,Parameters.MaximumTrainingErrorMm);
    }
    public override void OnExternalResultArrived() { base.OnExternalResultArrived(); RefreshOverview(); }
}
