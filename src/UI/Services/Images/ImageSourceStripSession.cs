using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Images;

/// <summary>One source/owner view session. Formal writes remain owned by ImageInputSource.ConfigurationWriter.</summary>
public sealed partial class ImageSourceStripSession : IImageSourceStripSession, IImageSourceStripViewportSession, IDisposable
{
    private sealed class ThumbnailRequest
    {
        public ThumbnailRequest(ImageInfo image, CancellationToken lifetime) { Image = image; Cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime); }
        public ImageInfo Image { get; }
        public CancellationTokenSource Cancellation { get; }
        public BitmapSource? Bitmap { get; set; }
        public string? Error { get; set; }
        public bool Started { get; set; }
    }
    private readonly ImageInputSource _source;
    private readonly Func<bool> _isCurrent;
    private readonly Func<ImageInfo, int, long, Task>? _activate;
    private readonly Dispatcher _dispatcher;
    private readonly Func<Window?>? _dialogOwner;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<ImageInfo, ThumbnailRequest> _thumbnails = new();
    private readonly HashSet<ImageInfo> _subscribedImages = new();
    private HashSet<string> _visibleIds = new(StringComparer.Ordinal);
    private readonly System.Collections.ObjectModel.ObservableCollection<ImageSourceStripItem> _items = new();
    private int _configuredStartIndex = -1;
    private bool _disposed, _refreshQueued;
    private long _refreshGeneration;
    private string? _status;
    public ImageSourceStripSession(ImageInputSource source, Func<bool> isCurrent,
        Func<ImageInfo, int, long, Task>? activate = null, Dispatcher? dispatcher = null, Func<Window?>? dialogOwner = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
        _activate = activate; _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _dialogOwner = dialogOwner; _dispatcher.VerifyAccess();
        _source.PropertyChanged += SourcePropertyChanged;
        _source.CollectionChanged += SourceCollectionChanged;
        Refresh();
    }
    public ImageInputSource Source => _source;
    public IReadOnlyList<ImageSourceStripItem> Items => _items;
    public long Version => _source.EditVersion;
    public int DisplayIndex => _source.CurrentIndex;
    public int ConfiguredStartIndex => _configuredStartIndex;
    public bool IsReadOnly => _disposed || _source.IsReadOnly || !OwnerIsCurrent();
    public bool IsSelectMode => _source.RunMode == ImageRunMode.运行选择;
    public bool AutoSwitchEnabled => _source.AutoSwitchEnabled;
    public bool IsImporting { get; private set; }
    public string? Status => _status;
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool OwnerIsCurrent() => !_disposed && _isCurrent() && !_disposed;
    private bool CanAccept(long version, bool edit = true)
        => OwnerIsCurrent() && _source.EditVersion == version && (!edit || !_source.IsReadOnly);
    private void RequireCurrent(long version, bool edit = true)
    {
        if (!CanAccept(version, edit)) throw new InvalidOperationException("图像源会话、版本或编辑权限已变化，请重新操作。");
    }
    private void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e) => QueueRefresh();
    private void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueRefresh();
    private void ImagePropertyChanged(object? sender, PropertyChangedEventArgs e) => QueueRefresh();
    private void QueueRefresh()
    {
        if (_disposed || _dispatcher.HasShutdownStarted) return;
        if (!_dispatcher.CheckAccess()) { _dispatcher.BeginInvoke(new Action(QueueRefresh)); return; }
        if (_refreshQueued) return;
        _refreshQueued = true;
        _dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
        { _refreshQueued = false; if (!_disposed) Refresh(); }));
    }
    private void Refresh()
    {
        _dispatcher.VerifyAccess();
        if (_disposed) return;
        var generation=++_refreshGeneration;var version=_source.EditVersion;
        bool Current()=>generation==_refreshGeneration&&version==_source.EditVersion&&OwnerIsCurrent();
        if(!Current())return;
        var rows = _source.Images.ToArray();
        foreach (var old in _subscribedImages.Where(x => !rows.Contains(x)).ToArray())
        { old.PropertyChanged -= ImagePropertyChanged; _subscribedImages.Remove(old); RemoveThumbnail(old); }
        foreach (var row in rows)
            if (_subscribedImages.Add(row)) row.PropertyChanged += ImagePropertyChanged;
        foreach (var old in _thumbnails.Keys.Where(row => !_visibleIds.Contains(row.Id)).ToArray()) RemoveThumbnail(old);
        foreach (var row in rows.Where(row => _visibleIds.Contains(row.Id)))
            if (!_thumbnails.ContainsKey(row)) _thumbnails.Add(row, new(row, _lifetime.Token));
        _configuredStartIndex = _source.CapturePolicy().CurrentIndex;
        var nextItems = rows.Select(row =>
        {
            _thumbnails.TryGetValue(row, out var thumbnail);
            return new ImageSourceStripItem(row.Id, row.Name,
                thumbnail is null ? null : Frozen(row.Thumbnail) ?? thumbnail.Bitmap,
                row.IsForRun, row.ResourceError ?? thumbnail?.Error);
        }).ToArray();
        // Keep ItemsSource identity during viewport/thumbnail refresh. Replacing
        // the whole source resets virtualization and can scroll back to selection,
        // which starts the opposite viewport request again.
        for(var index=0;index<nextItems.Length;index++)
        {
            if(!Current())return;
            if(index>=_items.Count)_items.Add(nextItems[index]);
            else if(_items[index]!=nextItems[index])_items[index]=nextItems[index];
            if(!Current())return;
        }
        while(_items.Count>nextItems.Length)
        {
            if(!Current())return;
            _items.RemoveAt(_items.Count-1);
            if(!Current())return;
        }
        NotifyAll(Current);
        if (!Current()) return;
        foreach (var thumbnail in _thumbnails.Values.ToArray())
        {
            if(!Current())return;
            if (thumbnail.Started || thumbnail.Image.Thumbnail is not null) continue;
            thumbnail.Started = true;
            _ = LoadThumbnailAsync(thumbnail, thumbnail.Cancellation.Token);
        }
    }
    public void UpdateVisibleItems(IReadOnlyList<string> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        var snapshot = itemIds.Take(128).ToArray();
        if (!_dispatcher.CheckAccess()) { _dispatcher.Invoke(() => UpdateVisibleItems(snapshot)); return; }
        if (_disposed) return;
        var next = snapshot.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
        if (_visibleIds.SetEquals(next)) return;
        _visibleIds = next;
        // Visibility owns only independent cache waiters and visual bitmap references, not formal configuration.
        Refresh();
    }
    private static BitmapSource? Frozen(BitmapSource? bitmap)
    {
        if (bitmap is null || bitmap.IsFrozen) return bitmap;
        if (!bitmap.CanFreeze) return null;
        var frozen = bitmap.CloneCurrentValue(); frozen.Freeze(); return frozen;
    }
    private async Task LoadThumbnailAsync(ThumbnailRequest request, CancellationToken token)
    {
        BitmapSource? bitmap = null; string? error = null;
        try
        {
            bitmap = await LocalImageAssetCache.LoadThumbnailAsync(request.Image.FilePath, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (bitmap is null) error = "图像缩略图无法解码";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (Exception ex) { error = ex.Message; }
        if (_dispatcher.HasShutdownStarted || token.IsCancellationRequested) return;
        try
        {
            await _dispatcher.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || !OwnerIsCurrent() || !_thumbnails.TryGetValue(request.Image, out var current)
                    || !ReferenceEquals(current, request)) return;
                request.Bitmap = Frozen(bitmap); request.Error = error; QueueRefresh();
            }, DispatcherPriority.Background, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    private void RemoveThumbnail(ImageInfo image)
    {
        if (!_thumbnails.Remove(image, out var request)) return;
        request.Cancellation.Cancel(); request.Cancellation.Dispose();
        // Never call ImageInfo.CancelThumbnail/ReleaseFullImage: that state may be consumed by another view.
    }
    private void NotifyAll(Func<bool> current)
    {
        foreach (var name in new[] { nameof(Items), nameof(Version), nameof(DisplayIndex), nameof(ConfiguredStartIndex),
            nameof(IsReadOnly), nameof(IsSelectMode), nameof(AutoSwitchEnabled), nameof(IsImporting), nameof(Status) })
        { if (!current()) return; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
    }
    private void SetStatus(string? status)
    { _status = status; if (!_disposed) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status))); }
    public async Task RequestAsync(ImageSourceStripRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_dispatcher.CheckAccess())
        {
            await _dispatcher.InvokeAsync(() => RequestAsync(request, cancellationToken)).Task.Unwrap(); return;
        }
        cancellationToken.ThrowIfCancellationRequested();
        bool edit = request.Kind is not (ImageSourceStripRequestKind.CancelImport or ImageSourceStripRequestKind.OpenDirectory);
        RequireCurrent(request.ExpectedVersion, edit);
        if (request.Kind == ImageSourceStripRequestKind.CancelImport) { CancelFileImport(); return; }
        if (IsImporting && request.Kind is not (ImageSourceStripRequestKind.ImportFiles or ImageSourceStripRequestKind.ImportDirectory or ImageSourceStripRequestKind.OpenDirectory))
            throw new InvalidOperationException("图像导入尚未结束。");
        var rows = _source.Images.ToArray();
        int index = request.ItemId is null ? -1 : Array.FindIndex(rows, row => row.Id == request.ItemId);
        if (request.ItemId is not null && index < 0) throw new InvalidOperationException("图像条目已不属于当前来源。");
        if(request.Kind is ImageSourceStripRequestKind.Activate or ImageSourceStripRequestKind.SelectStart
            &&!_source.CanSelectImage(index))
        {
            SetStatus("运行选择模式只能选择已勾选的图像。");
            if(OwnerIsCurrent())Refresh();
            return;
        }
        switch (request.Kind)
        {
            case ImageSourceStripRequestKind.Activate:
                if (_activate is not null) await _activate(rows[index], index, request.ExpectedVersion);
                else _source.SelectImage(index);
                break;
            case ImageSourceStripRequestKind.SelectStart: _source.SelectImage(index); break;
            case ImageSourceStripRequestKind.SetRunSelection:
                var ids = request.ItemIds ?? throw new InvalidOperationException("缺少完整运行勾选集合。");
                if (ids.Count != ids.Distinct(StringComparer.Ordinal).Count() || ids.Any(id => !rows.Any(row => row.Id == id)))
                    throw new InvalidOperationException("运行勾选包含重复或过期条目。");
                _source.SetRunSelection(ids); break;
            case ImageSourceStripRequestKind.SetMode:
                _source.RunMode = request.Flag == true ? ImageRunMode.运行选择 : ImageRunMode.运行全部; break;
            case ImageSourceStripRequestKind.SetAutoSwitch: _source.AutoSwitchEnabled = request.Flag == true; break;
            case ImageSourceStripRequestKind.Remove: _source.RemoveImage(rows[index]); break;
            case ImageSourceStripRequestKind.Clear: _source.ClearImages(); break;
            case ImageSourceStripRequestKind.ImportFiles:
                await ImportFilesRequestAsync(request, cancellationToken); break;
            case ImageSourceStripRequestKind.ImportDirectory:
                await ImportDirectoryRequestAsync(request, cancellationToken); break;
            case ImageSourceStripRequestKind.OpenDirectory:
                OpenDirectory(rows[index], request.ExpectedVersion); break;
            default: throw new ArgumentOutOfRangeException(nameof(request));
        }
        if (_dispatcher.CheckAccess()) { if (OwnerIsCurrent()) Refresh(); }
        else if (!_dispatcher.HasShutdownStarted)
            await _dispatcher.InvokeAsync(() => { if (OwnerIsCurrent()) Refresh(); });
    }
    private void OpenDirectory(ImageInfo image, long version)
    {
        RequireCurrent(version, edit: false);
        var path = image.FilePath;
        if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("图像文件或目录不存在。", path);
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        if (Directory.Exists(path)) start.ArgumentList.Add(path);
        else
        {
            var item = ILCreateFromPathW(path);
            if (item == IntPtr.Zero) throw new IOException("无法定位图像文件：" + path);
            try { RequireCurrent(version, edit: false); Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item, 0, IntPtr.Zero, 0)); }
            finally { ILFree(item); }
            return;
        }
        RequireCurrent(version, edit: false); Process.Start(start)?.Dispose();
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ILCreateFromPathW(string path);
    [DllImport("shell32.dll")]
    private static extern int SHOpenFolderAndSelectItems(IntPtr item, uint count, IntPtr children, uint flags);
    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr item);
    public void Dispose()
    {
        if (!_dispatcher.CheckAccess()) { _dispatcher.Invoke(Dispose); return; }
        if (_disposed) return;
        _disposed = true; ++_refreshGeneration; _source.PropertyChanged -= SourcePropertyChanged; _source.CollectionChanged -= SourceCollectionChanged;
        CancelFileImport(); _lifetime.Cancel();
        foreach (var row in _thumbnails.Keys.ToArray()) RemoveThumbnail(row);
        foreach (var row in _subscribedImages) row.PropertyChanged -= ImagePropertyChanged;
        _subscribedImages.Clear(); _visibleIds.Clear();
        _items.Clear(); _lifetime.Dispose();
    }
}
