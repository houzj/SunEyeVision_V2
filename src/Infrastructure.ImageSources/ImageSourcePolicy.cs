using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;


namespace SunEyeVision.Infrastructure.ImageSources;

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
    public int PathCache { get; set; }
    public bool SubscribeFolder { get; set; }
    public string FolderPath { get; set; } = "";
    public bool RefreshFolder { get; set; } = true;
    public bool TriggerFolderRun { get; set; }
    public string CameraDeviceId { get; set; } = string.Empty;
    public int CameraTimeoutMs { get; set; } = 3000;
    public ImageOutputFormat OutputFormat { get; set; } = ImageOutputFormat.Mono8;
    public long SNInitialValue { get; set; } = 1;
    internal bool UsesFolder => SourceKind == ImageSourceKind.Local && SubscribeFolder;

    /// <summary>Initial accepted-directory projection only; does not watch, acquire a frame or advance a cursor.</summary>
    public LocalImageSourcePlan CaptureFolderList(IResourcePathResolver paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if(!UsesFolder)throw new InvalidOperationException("当前不是订阅目录图像来源。");
        return SubscribedImageFolder.CaptureList(ResolveFolder(paths),this,-1);
    }

    internal string ResolveFolder(IResourcePathResolver paths)
    {
        if (string.IsNullOrWhiteSpace(FolderPath)) throw new InvalidOperationException("请选择订阅图片文件夹。");
        if (RunMode != 0) throw new InvalidOperationException("订阅图片文件夹使用全部图片运行模式。");
        var resolved = paths.Resolve("local.folder-path", FolderPath, ResourcePurpose.DirectoryRead);
        return resolved.Address;
    }

    /// <summary>Freeze the same local-source plan for formal execution and private debug.</summary>
    public static LocalImageSourcePlan CreateLocalPlan(string nodeId,ImageSourcePolicy? config,
        IResourcePathResolver resourcePaths)
    {
        ArgumentNullException.ThrowIfNull(resourcePaths);
        config ??= new() { NodeId = nodeId };
        if (config.NodeId != nodeId || config.FilePaths is null || config.ForRunPaths is null ||
            config.RunMode is not (0 or 1) || config.CurrentIndex < -1 || config.CurrentIndex >= config.FilePaths.Count ||
            !Enum.IsDefined(config.SourceKind) || !Enum.IsDefined(config.OutputFormat) || config.CameraTimeoutMs <= 0 || config.SNInitialValue < 0)
            throw new InvalidOperationException("正式图像源身份、运行模式或选择无效。");
        var selected=config.ForRunPaths.ToHashSet(StringComparer.Ordinal);
        var entries=config.FilePaths.Select((reference,index)=>(Reference:reference,Index:index))
            .Where(item=>config.RunMode==0||selected.Contains(item.Reference))
            .Select(item=>new LocalImageSequenceEntry($"{item.Index}:{item.Reference}",item.Reference,
                resourcePaths.Resolve("local.paths",item.Reference,ResourcePurpose.FileRead).Address,item.Index)).ToArray();
        var start=entries.FirstOrDefault(entry=>entry.OriginalIndex==config.CurrentIndex);
        return new(nodeId,Array.AsReadOnly(entries),start?.EntryId,
            config.AutoSwitchEnabled,config.StopAtLastImage);
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
                "local" => ImageSourceKind.Local, "camera" => ImageSourceKind.Camera, "sdk" => ImageSourceKind.Sdk,
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
            PathCache = checked((int)values.Get<long>("local.path-cache")),
            SubscribeFolder = values.Get<bool>("local.subscribe-folder"),
            FolderPath = values.Get<string>("local.folder-path"),
            RefreshFolder = values.Get<bool>("local.refresh-folder"),
            TriggerFolderRun = values.Get<bool>("local.trigger-folder-run"),
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

    public IReadOnlyDictionary<string,object?> ToFieldValues(ToolSchema schema)
    {
        var fields = schema.Inputs.ToDictionary(field => field.Id, StringComparer.Ordinal);
        SequenceValue References(string field, IEnumerable<string> references) => new(fields[field].Type, references);
        return new Dictionary<string, object?>
        {
            ["source.kind"] = SourceKind switch
            {
                ImageSourceKind.Local => "local", ImageSourceKind.Camera => "camera", ImageSourceKind.Sdk => "sdk",
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
            ["sequence.initial"] = SNInitialValue,
            ["local.path-cache"] = (long)PathCache
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
