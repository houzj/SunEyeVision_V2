using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Display;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

public sealed record NativeResultFrameReceipt(Guid OwnerId, long Epoch, ImageFrameAddress Frame, CoordinateFrame Coordinates);

/// <summary>Single owner of an original image view. Regions use its real raster-layer entry, not approximated shapes.</summary>
public sealed class NativeResultViewport : IDisposable
{
    private static readonly ConditionalWeakTable<FrameworkElement, NativeResultViewport> Owners = new();
    private readonly ImageViewBox _view;
    private readonly NativeResultRasterLimits _limits;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly List<RegionLayer> _layers = new();
    private readonly List<ResourceDictionary> _resources = new();
    private NativeResultImageFrame? _frame;
    private Func<bool>? _imageCurrent;
    private long _request, _epoch;
    private bool _disposed;
    private DispatcherOperation? _refresh;
    private readonly DependencyPropertyDescriptor _templateDescriptor;
    public FrameworkElement View => _view;
    public ImageLayout? Layout => _frame?.OriginalLayout;
    public NativeResultFrameReceipt? CurrentFrame => _frame is {} frame ? new(_ownerId, _epoch, frame.Frame, frame.Coordinates) : null;

    public NativeResultViewport(NativeResultRasterLimits? limits = null) : this(new ImageViewBox(), limits ?? new()) { }
    private NativeResultViewport(ImageViewBox view, NativeResultRasterLimits limits)
    {
        view.Dispatcher.VerifyAccess(); limits.Validate();
        if (Owners.TryGetValue(view, out _) || view.ImageDataSource is not null || view.ShapeList is not null || view.ROIList is not null
            || BindingOperations.IsDataBound(view, ImageViewBox.ImageDataSourceProperty)
            || BindingOperations.IsDataBound(view, ImageViewBox.ShapeListProperty)
            || BindingOperations.IsDataBound(view, ImageViewBox.ROIListProperty))
            throw new InvalidOperationException("原图像视口仍有显示所有者或绑定，不能并存第二条图像路径。");
        _view = view; _limits = limits;
        _templateDescriptor = DependencyPropertyDescriptor.FromProperty(Control.TemplateProperty, typeof(ImageViewBox));
        Owners.Add(view, this);
        try
        {
            foreach (var path in new[] { "Themes/Native/OriginalTheme.xaml", "Themes/Native/Display/VMBaseControls/base/imageview/shape/shapeitemspagestyle.xaml" })
            {
                var resource = new ResourceDictionary { Source = new Uri("/SunEyeVision.Plugin.Wpf;component/" + path, UriKind.Relative) };
                _resources.Add(resource); view.Resources.MergedDictionaries.Add(resource);
            }
            view.Loaded += Loaded;
            _templateDescriptor.AddValueChanged(view, TemplateChanged);
        }
        catch (Exception failure)
        {
            _disposed = true;
            var cleanup = _resources.Select<ResourceDictionary, Action>(resource => () => view.Resources.MergedDictionaries.Remove(resource)).ToList();
            cleanup.Add(() => view.Loaded -= Loaded); cleanup.Add(() => _templateDescriptor.RemoveValueChanged(view, TemplateChanged)); cleanup.Add(() => Owners.Remove(view));
            try { ParameterCleanup.Run(cleanup); } catch (Exception error) { throw new AggregateException(failure, error); }
            throw;
        }
    }

    public static NativeResultViewport Attach(FrameworkElement originalView, NativeResultRasterLimits? limits = null)
        => new(originalView as ImageViewBox ?? throw new ArgumentException("必须借原ImageViewBox实例。", nameof(originalView)), limits ?? new());

    private bool Owns(long epoch, NativeResultImageFrame? frame)
        => !_disposed && epoch == _epoch && ReferenceEquals(frame, _frame) && Owners.TryGetValue(_view, out var owner) && ReferenceEquals(owner, this);
    private bool Current(NativeResultFrameReceipt receipt)
    {
        var frame = _frame; var proof = _imageCurrent;
        if (frame is null || receipt != CurrentFrame || !Owns(receipt.Epoch, frame)) return false;
        var valid = proof?.Invoke() == true;
        return valid && Owns(receipt.Epoch, frame) && ReferenceEquals(proof, _imageCurrent) && receipt == CurrentFrame;
    }

    public bool BindImage(IReadOnlyImage image, Func<bool> isCurrent)
    {
        _view.Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(image); ArgumentNullException.ThrowIfNull(isCurrent);
        var request = ++_request;
        bool AttemptCurrent()
        {
            if (_disposed || request != _request) return false;
            var valid = isCurrent(); return valid && !_disposed && request == _request;
        }
        NativeResultImageFrame candidate;
        try { candidate = new(image, _limits, AttemptCurrent); }
        catch (NativeProjectionRetiredException) { return false; }
        NativeResultImageFrame? previous = null; var attached = false; long epoch = -1; Exception? failure = null;
        void Clean(Action action) { try { action(); } catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); } }
        try
        {
            if (AttemptCurrent())
            {
                previous = _frame; epoch = ++_epoch; _frame = candidate; _imageCurrent = isCurrent; attached = true;
                foreach (var layer in _layers.ToArray())
                { if (!Owns(epoch, candidate)) break; layer.RetireFrame(); }
                if (Owns(epoch, candidate)) _view.ImageDataSource = candidate;
            }
        }
        catch (Exception error)
        {
            failure = error;
            if (attached && Owns(epoch, candidate)) Clean(Retire);
        }
        if (!attached) Clean(candidate.Dispose); else Clean(() => previous?.Dispose());
        var receipt = new NativeResultFrameReceipt(_ownerId, epoch, candidate.Frame, candidate.Coordinates);
        var valid = false;
        if (failure is null)
            try { valid = attached && AttemptCurrent() && Current(receipt); } catch (Exception error) { failure = error; }
        if (failure is not null)
        {
            if (attached && Owns(epoch, candidate)) Clean(Retire);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
        if (valid) QueueRefresh(); else if (Owns(epoch, candidate)) Retire();
        return valid;
    }

    public RegionLayer CreateRegionLayer(int zIndex = 0)
    {
        _view.Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        var layer = new RegionLayer(this, zIndex); _layers.Add(layer); return layer;
    }
    public byte[]? ReadPixel(int x, int y)
    {
        _view.Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        var receipt = CurrentFrame; var frame = _frame;
        if (receipt is null || !Current(receipt)) return null;
        var value = frame!.ReadPixel(x, y);
        return Current(receipt) ? value : null;
    }
    private void Loaded(object sender, RoutedEventArgs args) { if (ReferenceEquals(args.OriginalSource, _view)) QueueRefresh(); }
    private void TemplateChanged(object? sender, EventArgs args) => QueueRefresh();
    private void QueueRefresh()
    {
        if (_disposed || _refresh is not null) return;
        DispatcherOperation? operation = null;
        operation = _view.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!ReferenceEquals(_refresh, operation)) return;
            _refresh = null; var receipt = CurrentFrame;
            if (receipt is null || !Current(receipt) || !_view.IsLoaded) return;
            _view.ApplyTemplate(); _view.UpdateLayout();
            if (!Current(receipt)) return;
            if (_view.Source is null) _view.Init();
            if (!Current(receipt)) return;
            var failures = new List<Exception>();
            foreach (var layer in _layers.ToArray())
            {
                if (!Current(receipt)) break;
                try { layer.Install(); } catch (Exception failure) { failures.Add(failure); }
            }
            if (failures.Count != 0) throw new AggregateException("结果图层重装失败。", failures);
        }));
        _refresh = operation;
    }
    public void RefreshCurrentness()
    {
        _view.Dispatcher.VerifyAccess(); if (_disposed) return;
        var receipt = CurrentFrame;
        if (receipt is not null && !Current(receipt)) { if (receipt == CurrentFrame) Clear(); return; }
        foreach (var layer in _layers.ToArray()) layer.RefreshCurrentness();
    }
    public void Clear()
    {
        _view.Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); Retire();
    }
    private void Retire()
    {
        ++_request; var epoch = ++_epoch; var frame = _frame; _frame = null; _imageCurrent = null;
        var refresh = _refresh; _refresh = null;
        var actions = new List<Action> { () => refresh?.Abort() };
        actions.AddRange(_layers.ToArray().Select<RegionLayer, Action>(layer => () => { if (epoch == _epoch) layer.RetireFrame(); }));
        actions.Add(() => { if (epoch == _epoch && Owners.TryGetValue(_view, out var owner) && ReferenceEquals(owner, this)) _view.ImageDataSource = null; });
        actions.Add(() => frame?.Dispose()); ParameterCleanup.Run(actions);
    }
    public void Dispose()
    {
        _view.Dispatcher.VerifyAccess(); if (_disposed) return; _disposed = true;
        _view.Loaded -= Loaded;
        var actions = new List<Action> { () => _templateDescriptor.RemoveValueChanged(_view, TemplateChanged), Retire };
        actions.AddRange(_layers.ToArray().Select<RegionLayer, Action>(layer => layer.Dispose));
        actions.AddRange(_resources.Select<ResourceDictionary, Action>(resource => () => _view.Resources.MergedDictionaries.Remove(resource)));
        actions.Add(() => Owners.Remove(_view)); ParameterCleanup.Run(actions);
    }

    /// <summary>Only this lease may replace or remove its own raster. Disposing it never clears another layer.</summary>
    public sealed class RegionLayer : IDisposable
    {
        private NativeResultViewport? _owner;
        private readonly Image _image = new() { IsHitTestVisible = false };
        private NativeResultFrameReceipt? _receipt;
        private Func<bool>? _current;
        private long _revision = -1, _attempt;
        private Panel? _installedParent;
        internal RegionLayer(NativeResultViewport owner, int zIndex)
        { _owner = owner; Panel.SetZIndex(_image, zIndex); RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor); }
        public bool Present(NativeResultFrameReceipt receipt, long revision, IReadOnlyRegion region, Color color, Func<bool> isCurrent)
        {
            var owner = _owner ?? throw new ObjectDisposedException(nameof(RegionLayer)); owner._view.Dispatcher.VerifyAccess();
            ArgumentNullException.ThrowIfNull(receipt); ArgumentNullException.ThrowIfNull(region); ArgumentNullException.ThrowIfNull(isCurrent);
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!owner.Current(receipt) || revision <= _revision) return false;
            var attempt = ++_attempt;
            bool Current()
            {
                if (!ReferenceEquals(owner, _owner) || attempt != _attempt || !owner.Current(receipt)
                    || !ReferenceEquals(owner, _owner) || attempt != _attempt) return false;
                var valid = isCurrent();
                if (!valid || !ReferenceEquals(owner, _owner) || attempt != _attempt) return false;
                valid = owner.Current(receipt);
                return valid && ReferenceEquals(owner, _owner) && attempt == _attempt;
            }
            try
            {
                var frame = owner._frame!;
                var runs = NativeResultRaster.Read(region, receipt.Frame, receipt.Coordinates, frame.Width, frame.Height, owner._limits, Current);
                var pixels = NativeResultRaster.Paint(frame.Width, frame.Height, runs, color, owner._limits, Current);
                var source = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, pixels, checked(frame.Width * 4)); source.Freeze();
                if (!Current()) return false;
                _receipt = receipt; _current = isCurrent; _revision = revision; _image.Source = source;
                if (!Current()) { if (attempt == _attempt) RetireFrame(); return false; }
                Install(); owner.QueueRefresh(); return Current();
            }
            catch (NativeProjectionRetiredException) { return false; }
            catch (Exception failure)
            {
                if (ReferenceEquals(owner, _owner) && attempt == _attempt)
                    try { RetireFrame(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
                throw;
            }
        }
        internal void Install()
        {
            var owner = _owner; var receipt = _receipt; var proof = _current;
            var attempt = _attempt;
            try { InstallCore(); }
            catch (Exception failure)
            {
                if (ReferenceEquals(owner, _owner) && attempt == _attempt)
                    try { RetireFrame(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
                throw;
            }
            void InstallCore()
            {
            if (owner is null || receipt is null || _image.Source is null || !owner.Current(receipt)) return;
            var valid = proof?.Invoke() == true;
            if (!valid || !ReferenceEquals(owner, _owner) || !ReferenceEquals(proof, _current) || receipt != _receipt) return;
            valid = owner.Current(receipt);
            if (!valid || !ReferenceEquals(owner, _owner) || !ReferenceEquals(proof, _current) || receipt != _receipt) return;
            var parent = owner._view.Template?.FindName("ImageLayers", owner._view) as Panel;
            if (parent is null) return;
            var previous = _image.Parent as Panel ?? _installedParent;
            if (previous is not null && !ReferenceEquals(previous, parent)) previous.Children.Remove(_image);
            if (!ReferenceEquals(owner, _owner) || !ReferenceEquals(proof, _current) || receipt != _receipt) return;
            owner._view.AddImageLayer(_image);
            if (ReferenceEquals(owner, _owner) && ReferenceEquals(proof, _current) && receipt == _receipt) _installedParent = _image.Parent as Panel;
            }
        }
        internal void RefreshCurrentness()
        {
            var owner = _owner; var receipt = _receipt; var proof = _current; var attempt = _attempt;
            if (owner is null || receipt is null) return;
            var valid = owner.Current(receipt) && proof?.Invoke() == true;
            if (!valid && ReferenceEquals(owner, _owner) && attempt == _attempt && ReferenceEquals(proof, _current)) RetireFrame();
        }
        internal void RetireFrame()
        {
            var attempt = ++_attempt; _revision = -1; _receipt = null; _current = null;
            var owner = _owner; var parent = _image.Parent as Panel ?? _installedParent; _installedParent = null;
            ParameterCleanup.Run(new Action[] { () => _image.Source = null,
                () => { if (attempt == _attempt && ReferenceEquals(owner, _owner)) parent?.Children.Remove(_image); } });
        }
        public void Dispose()
        {
            var owner = _owner; if (owner is null) return; owner._view.Dispatcher.VerifyAccess();
            _owner = null; ++_attempt; _receipt = null; _current = null; var parent = _image.Parent as Panel ?? _installedParent; _installedParent = null;
            ParameterCleanup.Run(new Action[] { () => _image.Source = null, () => parent?.Children.Remove(_image), () => owner._layers.Remove(this) });
        }
    }
}
