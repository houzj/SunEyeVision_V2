using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.NPointCalibration.Views;

public partial class NPointVerificationDebugControl : ToolDebugControlBase
{
    public static readonly DependencyProperty ParametersProperty = DependencyProperty.Register(
        nameof(Parameters), typeof(NPointVerificationParameters), typeof(NPointVerificationDebugControl));

    public NPointVerificationParameters Parameters
    {
        get => (NPointVerificationParameters)GetValue(ParametersProperty);
        set => SetValue(ParametersProperty, value);
    }

    private string _resultText = "尚未验证";
    public string ResultText
    {
        get => _resultText;
        set { _resultText = value; OnPropertyChanged(nameof(ResultText)); }
    }

    public NPointVerificationDebugControl()
    {
        InitializeComponent();
        Parameters = new NPointVerificationParameters();
    }

    public override void SetParameters(ToolParameters parameters)
    {
        base.SetParameters(parameters);
        Parameters = parameters as NPointVerificationParameters
            ?? throw new InvalidOperationException("N点验证参数类型不匹配。");
    }

    protected override object ExecuteTool()
    {
        var result = ExecuteWithLiveContext(new NPointVerificationTool(), Parameters);
        if (result.IsSuccess && !Parameters.TryCommit(result, out var error))
            result.SetError(error ?? "验证点提交或发布失败。");
        ResultText = result is NPointVerificationResults verification && result.IsSuccess
            ? $"{verification.Message}\n已采 {verification.CollectedCount} 点；最大误差 {verification.MaximumErrorMm:F4} mm；{(verification.Published ? "已发布" : "未发布")}"
            : result.ErrorMessage ?? "验证失败";
        return result;
    }

    protected override bool CanExecuteTool() => Parameters != null;

    private void ClearSamples_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("清空当前独立验证点？", "清空验证点", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Parameters.ValidationSamples.Clear();
        Parameters.CandidateSha256 = string.Empty;
        Parameters.LastPublishedCandidateSha256 = string.Empty;
        Parameters.LastSampleEventId = 0;
        Parameters.SampleRevision++;
        ResultText = "验证点已清空。";
    }
}
