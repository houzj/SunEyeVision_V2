using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>The sole persisted source definition. Display and execution cursors are host-owned.</summary>
public sealed record ImageSourceSettings : IToolSettings<ImageSourceSettings>
{
    public string SourceKind { get; init; } = "local";
    public IReadOnlyList<string> FileReferences { get; init; } = Array.Empty<string>();
    public int StartIndex { get; init; } = -1;
    public IReadOnlyList<string> SelectedReferences { get; init; } = Array.Empty<string>();
    public string RunMode { get; init; } = "all";
    public bool AutoSwitch { get; init; } = true;
    public bool StopAtLast { get; init; }
    public int PathCache { get; init; }
    public bool SubscribeFolder { get; init; }
    public string FolderPath { get; init; } = "";
    public bool RefreshFolder { get; init; } = true;
    public bool TriggerFolderRun { get; init; }
    public bool StitchEnabled { get; init; }
    public int StitchStart { get; init; }
    public int StitchHeight { get; init; } = 2000;
    public bool ClearStitch { get; init; }
    public long? ClearStitchValue { get; init; }
    public bool TriggerFilterEnabled { get; init; }
    public string? TriggerText { get; init; }
    public string? TriggerFilterText { get; init; }
    public string CameraDeviceId { get; init; } = string.Empty;
    public int CameraTimeoutMs { get; init; } = 3000;
    public double? ExposureTime { get; init; }
    public double? Gain { get; init; }
    public string OutputFormat { get; init; } = "mono8";
    public bool OutputMono8 { get; init; }
    public bool SaveImages {get;init;}
    public long InitialSequence { get; init; } = 1;

    public ImageSourceSettings Freeze() => this with
    {
        FileReferences = Array.AsReadOnly((FileReferences ?? throw new ArgumentException("图像引用列表不能为空。")).ToArray()),
        SelectedReferences = Array.AsReadOnly((SelectedReferences ?? throw new ArgumentException("运行选择列表不能为空。")).ToArray())
    };

    // Empty sources and unselected cameras can be edited; invocation readiness is checked separately.
    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Reject(string field, string message) => errors.Add(new("IMAGE_SOURCE_SETTINGS", message, field));
        if (SourceKind is not ("local" or "camera" or "sdk")) Reject(ImageSourceFields.SourceKind, "图像来源无效。");
        if (RunMode is not ("all" or "selected")) Reject(ImageSourceFields.RunMode, "运行模式无效。");
        if (OutputFormat is not ("original" or "mono8" or "rgb24")) Reject(ImageSourceFields.OutputFormat, "输出格式无效。");
        if (FileReferences is null || FileReferences.Any(string.IsNullOrWhiteSpace)) Reject(ImageSourceFields.Paths, "文件引用不能为空。");
        if (StartIndex < -1 || StartIndex >= (FileReferences?.Count ?? 0)) Reject(ImageSourceFields.Start, "运行起点超出列表范围。");
        if (SelectedReferences is null || SelectedReferences.Any(string.IsNullOrWhiteSpace) ||
            SelectedReferences.Distinct(StringComparer.Ordinal).Count() != SelectedReferences.Count ||
            SelectedReferences.Any(reference => FileReferences is null || !FileReferences.Contains(reference, StringComparer.Ordinal)))
            Reject(ImageSourceFields.Selected, "运行选择必须是当前列表内的唯一引用。");
        if (CameraDeviceId is null) Reject(ImageSourceFields.CameraDevice, "设备引用不能为null。");
        if (CameraTimeoutMs <= 0) Reject(ImageSourceFields.CameraTimeout, "取帧超时必须大于零。");
        if (ExposureTime is { } exposure && (!double.IsFinite(exposure) || exposure <= 0))
            Reject(ImageSourceFields.ExposureTime, "曝光时间必须为正数。");
        if (Gain is { } gain && !double.IsFinite(gain))
            Reject(ImageSourceFields.Gain, "增益必须为有限数值。");
        if (PathCache is < 0 or > 5) Reject(ImageSourceFields.PathCache, "图片缓存必须在 0 到 5 之间。");
        if (FolderPath is null || FolderPath.Length > 200 || FolderPath.Any(char.IsControl))
            Reject(ImageSourceFields.FolderPath, "订阅目录路径无效或超过 200 个字符。");
        if (StitchStart is < 0 or > 20000 || StitchHeight is < 0 or > 20000)
            Reject(ImageSourceFields.StitchHeight, "拼接起点和高度必须在 0 到 20000 之间。");
        if (InitialSequence is < 0 or > ImageSourceFields.MaximumInitialSequence)
            Reject(ImageSourceFields.InitialSequence, $"SN初始值必须在 0 到 {ImageSourceFields.MaximumInitialSequence} 之间。");
        return errors.AsReadOnly();
    }
}
