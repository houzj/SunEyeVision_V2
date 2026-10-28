using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Workflow;

/// <summary>Transient host projection of declared Runtime source settings. Never persisted on a node.</summary>
public sealed class ImageSourcePolicy
{
    public string NodeId { get; set; } = string.Empty;
    public ImageSourceKind SourceKind { get; set; }
    public List<string> FilePaths { get; set; } = new();
    public int CurrentIndex { get; set; } = -1;
    public List<string> ForRunPaths { get; set; } = new();
    public int RunMode { get; set; }
    public bool AutoSwitchEnabled { get; set; } = true;
    public bool StopAtLastImage { get; set; }
    public string CameraDeviceId { get; set; } = string.Empty;
    public int CameraTimeoutMs { get; set; } = 3000;
    public ImageOutputFormat OutputFormat { get; set; }
    public long SNInitialValue { get; set; } = 1;

    public static ImageSourcePolicy? Read(WorkflowNodeBase node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.ToolType != "ImageLoad" || node.RuntimeConfiguration is not { } runtime) return null;
        if (runtime.ToolId != node.ToolType) throw new InvalidOperationException("图像源Runtime身份不匹配。");
        return ReadFields(node.Id,runtime.ReadFieldValues());
    }

    /// <summary>Read the same declared settings from an editor-owned draft.</summary>
    public static ImageSourcePolicy ReadFields(string nodeId,InputValues values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(values);
        List<string> References(string field) => values.Get<SequenceValue>(field)
            .Select(value => value as string ?? throw new InvalidOperationException("图像源引用必须是文本。"))
            .ToList();
        return new()
        {
            NodeId = nodeId,
            SourceKind = values.Get<string>("source.kind") switch
            {
                "local" => ImageSourceKind.Local, "camera" => ImageSourceKind.Camera,
                _ => throw new InvalidOperationException("图像来源无效。")
            },
            FilePaths = References("local.paths"), ForRunPaths = References("local.selected"),
            CurrentIndex = checked((int)values.Get<long>("local.start")),
            RunMode = values.Get<string>("local.run-mode") switch
            {
                "all" => 0, "selected" => 1, _ => throw new InvalidOperationException("运行模式无效。")
            },
            AutoSwitchEnabled = values.Get<bool>("local.auto-switch"),
            StopAtLastImage = values.Get<bool>("local.stop-at-last"),
            CameraDeviceId = values.Get<string>("camera.device"),
            CameraTimeoutMs = checked((int)values.Get<long>("camera.timeout-ms")),
            OutputFormat = values.Get<string>("output.format") switch
            {
                "original" => ImageOutputFormat.Original, "mono8" => ImageOutputFormat.Mono8,
                "rgb24" => ImageOutputFormat.Rgb24, _ => throw new InvalidOperationException("输出格式无效。")
            },
            SNInitialValue = values.Get<long>("sequence.initial")
        };
    }

    /// <summary>Apply to an owned formal edit draft. This does not commit the document or update a view.</summary>
    public void ApplyTo(WorkflowNodeBase draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (NodeId != draft.Id || draft.ToolType != "ImageLoad" || draft.RuntimeConfiguration is not { } runtime)
            throw new InvalidOperationException("图像源投影与正式Runtime节点不匹配。");
        draft.RuntimeConfiguration=runtime.WithFieldValues(ToFieldValues(runtime.SettingsSchema
            ??throw new InvalidOperationException("图像源缺少设置声明。")));
    }

    public IReadOnlyDictionary<string,object?> ToFieldValues(ToolSchema schema)
    {
        var fields = schema.Inputs.ToDictionary(field => field.Id, StringComparer.Ordinal);
        SequenceValue References(string field, IEnumerable<string> references) => new(fields[field].Type, references);
        return new Dictionary<string, object?>
        {
            ["source.kind"] = SourceKind switch
            {
                ImageSourceKind.Local => "local", ImageSourceKind.Camera => "camera",
                _ => throw new InvalidOperationException("图像来源无效。")
            },
            ["local.paths"] = References("local.paths", FilePaths),
            ["local.selected"] = References("local.selected", ForRunPaths),
            ["local.start"] = (long)CurrentIndex,
            ["local.run-mode"] = RunMode switch
            {
                0 => "all", 1 => "selected", _ => throw new InvalidOperationException("运行模式无效。")
            },
            ["local.auto-switch"] = AutoSwitchEnabled, ["local.stop-at-last"] = StopAtLastImage,
            ["camera.device"] = CameraDeviceId, ["camera.timeout-ms"] = (long)CameraTimeoutMs,
            ["output.format"] = OutputFormat switch
            {
                ImageOutputFormat.Original => "original", ImageOutputFormat.Mono8 => "mono8",
                ImageOutputFormat.Rgb24 => "rgb24", _ => throw new InvalidOperationException("输出格式无效。")
            },
            ["sequence.initial"] = SNInitialValue
        };
    }

    public void UpdateLocalInput(ImageSourcePolicy local)
    {
        ArgumentNullException.ThrowIfNull(local);
        if (local.NodeId != NodeId) throw new InvalidOperationException("本地列表不属于当前图像源。");
        FilePaths = new(local.FilePaths); ForRunPaths = new(local.ForRunPaths);
        CurrentIndex = local.CurrentIndex; RunMode = local.RunMode; AutoSwitchEnabled = local.AutoSwitchEnabled;
    }
}
