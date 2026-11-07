using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;

/// <summary>Immutable visual projection; no workflow configuration or image pixel ownership is transferred.</summary>
public sealed record ImageSourceStripItem(string Id, string Name, ImageSource? Thumbnail, bool IsForRun, string? ResourceError = null);

public enum ImageSourceStripRequestKind
{
    Activate, SelectStart, SetRunSelection, SetMode, SetAutoSwitch,
    ImportFiles, ImportDirectory, Remove, Clear, CancelImport, OpenDirectory
}

/// <summary>A versioned intent. Collection arguments are defensively frozen before asynchronous dispatch.</summary>
public sealed class ImageSourceStripRequest
{
    public ImageSourceStripRequest(ImageSourceStripRequestKind kind, long expectedVersion,
        string? itemId = null, bool? flag = null, IEnumerable<string>? itemIds = null, IEnumerable<string>? paths = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind; ExpectedVersion = expectedVersion; ItemId = itemId; Flag = flag;
        ItemIds = itemIds is null ? null : Array.AsReadOnly(itemIds.ToArray());
        Paths = paths is null ? null : Array.AsReadOnly(paths.ToArray());
        if ((kind is ImageSourceStripRequestKind.SetMode or ImageSourceStripRequestKind.SetAutoSwitch) != flag.HasValue)
            throw new ArgumentException("Only mode and automatic-switch intents carry Flag.");
        if ((kind == ImageSourceStripRequestKind.SetRunSelection) != (ItemIds is not null))
            throw new ArgumentException("Run selection must carry the complete immutable item ID set.");
        if (Paths is not null && kind is not (ImageSourceStripRequestKind.ImportFiles or ImageSourceStripRequestKind.ImportDirectory))
            throw new ArgumentException("Only file and directory import accept paths.");
        bool needsItem = kind is ImageSourceStripRequestKind.Activate or ImageSourceStripRequestKind.SelectStart
            or ImageSourceStripRequestKind.Remove or ImageSourceStripRequestKind.OpenDirectory;
        if (needsItem != !string.IsNullOrEmpty(itemId)) throw new ArgumentException("Invalid item identity for request kind.");
    }
    public ImageSourceStripRequestKind Kind { get; }
    public long ExpectedVersion { get; }
    public string? ItemId { get; }
    public bool? Flag { get; }
    public IReadOnlyList<string>? ItemIds { get; }
    public IReadOnlyList<string>? Paths { get; }
}

public interface IImageSourceStripSession : INotifyPropertyChanged
{
    IReadOnlyList<ImageSourceStripItem> Items { get; }
    long Version { get; }
    int DisplayIndex { get; }
    int ConfiguredStartIndex { get; }
    bool CanEdit { get; }
    bool CanNavigate { get; }
    /// <summary>
    /// 当前会话是否就是所选模块自己的图像源。只有拥有方给出「添加图片/添加目录/清空图片」入口；
    /// 选中下游模块时这里只呈现它引用的上游图像源，不提供增删图能力。
    /// </summary>
    bool ProvidesImageImport { get; }
    bool IsReadOnly { get; }
    bool IsSelectMode { get; }
    bool AutoSwitchEnabled { get; }
    bool IsImporting { get; }
    string? Status { get; }
    Task RequestAsync(ImageSourceStripRequest request, CancellationToken cancellationToken);
}

/// <summary>Optional view demand, not an edit intent. Empty means this view no longer displays any thumbnails.</summary>
public interface IImageSourceStripViewportSession
{
    void UpdateVisibleItems(IReadOnlyList<string> itemIds);
}
