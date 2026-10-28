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
    public string CameraDeviceId { get; init; } = string.Empty;
    public int CameraTimeoutMs { get; init; } = 3000;
    public string OutputFormat { get; init; } = "original";
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
        if (SourceKind is not ("local" or "camera")) Reject(ImageSourceFields.SourceKind, "图像来源无效。");
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
        if (InitialSequence < 0) Reject(ImageSourceFields.InitialSequence, "SN不能为负数。");
        return errors.AsReadOnly();
    }
}
