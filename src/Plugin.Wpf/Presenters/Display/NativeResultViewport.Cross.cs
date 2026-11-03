using System.ComponentModel;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Display;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Display;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.Shape;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

public sealed partial class NativeResultViewport
{
    public CrosshairLayer CreateCrosshairLayer(int order = 20)
    {
        _view.Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        var layer = new CrosshairLayer(this, order); _shapeLayers.Add(layer);
        try { if (CurrentFrame is {} receipt) layer.AttachFrame(receipt); return layer; }
        catch (Exception failure)
        { try { layer.Dispose(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); } throw; }
    }
    public sealed class CrosshairLayer : ShapeLayer
    {
        private readonly FrameworkElement _view;
        private readonly CrosshairSettings[] _crosses;
        private NativeResultFrameReceipt? _editReceipt;
        private CrosshairSettingsWindow? _window;
        private Window? _windowOwner;
        private long _generation, _windowGeneration;
        private bool _emptyAllowed = true, _attached, _opened;
        internal CrosshairLayer(NativeResultViewport owner, int order) : base(owner, order)
        {
            _view = owner._view;
            _crosses = new[] { new CrosshairSettings(1, CanEdit), new CrosshairSettings(2, CanEdit) };
            CrossDatas = Array.AsReadOnly(_crosses);
            foreach (var model in _crosses) model.PropertyChanged += ModelChanged;
            _view.Unloaded += ViewUnloaded;
        }
        public IReadOnlyList<CrosshairSettings> CrossDatas { get; }
        public CrosshairSettingsWindow? CurrentWindow => _window;
        public Exception? LastWindowError { get; private set; }
        public Exception? LastSourceError { get; private set; }
        private bool CanEdit()
        {
            var owner = Owner; if (owner is null || owner._disposed) return false;
            owner._view.Dispatcher.VerifyAccess();
            var receipt = _editReceipt; var generation = _generation;
            if (receipt is null) return _emptyAllowed && owner.CurrentFrame is null;
            bool current;
            try
            {
                current = owner.Current(receipt);
                if (generation != _generation || !ReferenceEquals(owner, Owner) || receipt != _editReceipt) return false;
                if (!current) { Clear(); return false; }
            }
            catch (Exception failure)
            {
                LastSourceError = failure;
                try { if (generation == _generation && ReferenceEquals(owner, Owner)) Clear(); }
                catch (Exception cleanup) { LastSourceError = new AggregateException(failure, cleanup); throw LastSourceError; }
                throw;
            }
            if (generation != _generation || !ReferenceEquals(owner, Owner) || receipt != _editReceipt) return false;
            return true;
        }
        internal void Activate()
        {
            if (!CanEdit() || _opened) return;
            _opened = true;
            var owner = Owner; var receipt = _editReceipt; var generation = _generation;
            if (owner is not null && receipt is not null)
                foreach (var model in _crosses)
                {
                    model.SetDimensions(owner._frame!.Width, owner._frame.Height, true);
                    if (generation != _generation || !ReferenceEquals(owner, Owner) || !CanEdit()) return;
                }
            _crosses[0].IsVisible = true;
        }
        internal void AttachFrame(NativeResultFrameReceipt receipt)
        {
            var owner = Owner; var generation = ++_generation;
            bool Current() => owner is not null && ReferenceEquals(owner, Owner) && generation == _generation
                && owner.Current(receipt) && ReferenceEquals(owner, Owner) && generation == _generation && receipt == owner.CurrentFrame;
            if (!Current()) return;
            if (receipt.Coordinates.Domain != CoordinateDomain.Image || receipt.Coordinates.Unit != CoordinateUnit.Pixel)
                throw new NotSupportedException("辅助十字需要已证明的当前图像像素坐标。");
            _emptyAllowed = false; _editReceipt = receipt; _attached = false;
            foreach (var model in _crosses)
            {
                model.SetDimensions(owner!._frame!.Width, owner._frame.Height, _opened || model.IsVisible);
                if (!Current()) return;
            }
            _attached = true;
            if (!Present(receipt, 0, () => generation == _generation && receipt == _editReceipt,
                (_, current) => { NativeResultRaster.CheckCurrent(current); return new(_crosses.Cast<object>().ToArray(), 2, 0); }))
                if (generation == _generation) Clear();
        }
        private void ModelChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (_attached && Owner is {} owner)
            {
                owner.MutableShapeChanged();
                if (args.PropertyName == nameof(CrosshairSettings.IsVisible) && sender is CrosshairSettings { IsVisible: true, NeedsInitialization: true } model
                    && CanEdit() && owner._frame is {} frame) model.SetDimensions(frame.Width, frame.Height, true);
            }
        }
        public CrosshairSettingsWindow OpenSettings(Window ownerWindow)
        {
            ArgumentNullException.ThrowIfNull(ownerWindow);
            var owner = Owner ?? throw new ObjectDisposedException(nameof(CrosshairLayer)); owner._view.Dispatcher.VerifyAccess();
            if (ownerWindow.Dispatcher != owner._view.Dispatcher || !ownerWindow.IsVisible || !CanEdit())
                throw new InvalidOperationException("辅助线配置需要有效的同线程窗口及当前显示来源。");
            var beforeClose = _windowGeneration; CloseWindow();
            if (_windowGeneration != beforeClose + 1)
                return _window ?? throw new InvalidOperationException("辅助线配置已由其他请求接管。");
            var attempt = ++_windowGeneration;
            Activate();
            if (attempt != _windowGeneration)
                return _window ?? throw new InvalidOperationException("辅助线配置已由其他请求接管。");
            if (!CanEdit()) throw new InvalidOperationException("辅助线配置来源已切换。");
            if (!ReferenceEquals(_windowOwner, ownerWindow))
            {
                if (_windowOwner is {} previous) previous.Closed -= OwnerClosed;
                _windowOwner = ownerWindow; ownerWindow.Closed += OwnerClosed;
            }
            var window = new CrosshairSettingsWindow { Owner = ownerWindow, DataContext = this };
            window.Closed += WindowClosed; window.AfterClosed = closed => { if (closed.CloseFailure is {} error) LastWindowError = error; }; _window = window;
            try
            {
                window.Show();
                if (attempt != _windowGeneration || !ReferenceEquals(window, _window) || !CanEdit())
                    throw new InvalidOperationException("辅助线配置窗口已失效。");
                return window;
            }
            catch (Exception failure)
            { try { if (ReferenceEquals(window, _window)) CloseWindow(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); } throw; }
        }
        private void WindowClosed(object? sender, EventArgs args)
        {
            if (sender is CrosshairSettingsWindow window) window.Closed -= WindowClosed;
            if (ReferenceEquals(sender, _window)) { _window = null; ++_windowGeneration; }
        }
        private void CloseWindow()
        {
            ++_windowGeneration; var window = _window; _window = null;
            if (window is null) return;
            window.Closed -= WindowClosed;
            try { window.Retire(); } catch (Exception error) { LastWindowError = error; throw; }
        }
        private void OwnerClosed(object? sender, EventArgs args) => Clear();
        private void ViewUnloaded(object sender, RoutedEventArgs args)
        { if (ReferenceEquals(args.OriginalSource, _view)) CloseWindow(); }
        internal void RefreshInteractions()
        {
            var owner = Owner; var receipt = _editReceipt; var generation = _generation;
            if (owner is null || receipt is null || !_attached) return;
            bool Current() => ReferenceEquals(owner, Owner) && generation == _generation && receipt == _editReceipt
                && owner.Current(receipt) && ReferenceEquals(owner, Owner) && generation == _generation;
            if (!Current()) return;
            foreach (var cross in Descendants<CrossControl>(_view).Where(control => _crosses.Any(model => ReferenceEquals(control.DataContext, model))).ToArray())
                cross.BindInteraction(Current);
        }
        protected override void OnInvalidated()
        {
            ++_generation; _emptyAllowed = false; _editReceipt = null; _attached = false;
            // Capture the retired controls before closing invokes external observers that may bind a new frame.
            var retiredControls = Descendants<CrossControl>(_view)
                .Where(control => _crosses.Any(model => ReferenceEquals(control.DataContext, model))).ToArray();
            var ownerWindow = _windowOwner; _windowOwner = null;
            ParameterCleanup.Run(new Action[] { CloseWindow,
                () => { if (ownerWindow is not null) ownerWindow.Closed -= OwnerClosed; },
                () => ParameterCleanup.Run(retiredControls.Select<CrossControl, Action>(cross => cross.RetireInteraction).ToArray()) });
        }
        protected override void OnDisposed()
        {
            _view.Unloaded -= ViewUnloaded;
            foreach (var model in _crosses) model.PropertyChanged -= ModelChanged;
        }
    }
}
