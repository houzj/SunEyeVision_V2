using System;
using System.Collections.Generic;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Communication;

/// <summary>Host data-analysis surface only; never reads a device or executes a tool.</summary>
public partial class CommunicationDataAnalysisControl : UserControl, IToolControlDataAnalysis
{
    public CommunicationDataAnalysisControl() => InitializeComponent();

    public CommunicationToolResults? CurrentResult { get; private set; }

    public event EventHandler<IEnumerable<DisplayItemBase>?>? SelectionHighlightChanged
    {
        add { } // Communications do not publish image geometry highlights.
        remove { }
    }

    public void OnResultUpdated(ToolResults result)
    {
        Dispatcher.VerifyAccess();
        ArgumentNullException.ThrowIfNull(result);
        if (result is not CommunicationToolResults communication)
            throw new ArgumentException("通信分析控件只能接收通信执行结果。", nameof(result));
        CurrentResult = communication;
        AnalysisGrid.ItemsSource = communication.Analysis;
        ExecutionSummary.Text = $"{communication.ToolName} · {communication.ExecutionStatusText} · {communication.ExecutionTimeMs:F2} ms" +
            (string.IsNullOrEmpty(communication.ErrorMessage) ? string.Empty : " · " + communication.ErrorMessage);
    }
}
