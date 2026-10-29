using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Display;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

public sealed record NativeGeometryOverlay(ImageFrameAddress SourceFrame, GeometryValue Geometry,
    Color Stroke, double Thickness = 1, double Opacity = 1);
public sealed record NativeResultShapeLimits(int MaximumShapes = 10000, int MaximumPolygonVertices = 1000000)
{
    internal void Validate()
    { if (MaximumShapes <= 0 || MaximumPolygonVertices <= 0) throw new ArgumentOutOfRangeException(nameof(NativeResultShapeLimits)); }
}

public sealed partial class NativeResultViewport
{
    private readonly NativeResultShapeLimits _shapeLimits;
    private readonly List<ShapeLayer> _shapeLayers = new();
    private long _shapeVersion, _installedShapeVersion = -1;
    private bool _installingShapes, _shapeDirty;
    private object? _installedShapeCollection;
    private ShapeItemsPage? _shapePage;
    private (int Width, int Height, double Ratio, double Thickness, double ViewWidth, double ViewHeight)? _shapeContext;
    private int ShapeBudget => Math.Min(_shapeLimits.MaximumShapes, GlobalRenderVariable.ShapeRenderingMaxNumber);
    private void ValidateShapeBudget(IEnumerable<PreparedNativeShapes> shapes)
    {
        var snapshot = shapes.ToArray();
        if (snapshot.Sum(shape => (long)shape.Count) > ShapeBudget
            || snapshot.Sum(shape => (long)shape.Vertices) > _shapeLimits.MaximumPolygonVertices)
            throw new ArgumentException("shape-budget: 完整合成场景超过原绘制或顶点预算。");
    }
    private void ValidateExportShapeBudget(NativeResultFrameReceipt receipt)
    {
        var active = new List<PreparedNativeShapes>();
        foreach (var layer in _shapeLayers.ToArray()) if (layer.Read(receipt) is {} prepared) active.Add(prepared);
        ValidateShapeBudget(active);
    }

    public GeometryLayer CreateGeometryLayer(int order = 0)
    {
        _view.Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        var layer = new GeometryLayer(this, order); _shapeLayers.Add(layer); return layer;
    }
    /// <summary>Selection is an independently owned geometry layer, using the same original renderer.</summary>
    public GeometryLayer CreateSelectionLayer(int order = 100) => CreateGeometryLayer(order);
    public TextLayer CreateTextLayer(int order = 10)
    {
        _view.Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        var layer = new TextLayer(this, order); _shapeLayers.Add(layer); return layer;
    }
    private void SizeChanged(object sender, SizeChangedEventArgs args) => QueueRefresh();
    private void ViewChanged(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName is "ControlToImageRadio" or "NeedRefreshShapes") QueueRefresh(); }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T value) yield return value; foreach (var item in Descendants<T>(child)) yield return item; }
    }
    private void ShapesChanged()
    { ++_shapeVersion; InstallShapes(false); QueueRefresh(); }
    private void MutableShapeChanged()
    {
        var previous = _shapeVersion++;
        // Original CrossControl binds the same mutable object. Replacing the item
        // collection on every coordinate notification would unload its drag handle.
        if (!_installingShapes && _installedShapeVersion == previous
            && ReferenceEquals(_installedShapeCollection, _view.ShapeList)) _installedShapeVersion = _shapeVersion;
        else QueueRefresh();
    }
    private void InstallShapes(bool layoutRefresh)
    {
        if (_installingShapes) { _shapeDirty = true; return; }
        var receipt = CurrentFrame; if (receipt is null || !Current(receipt)) return;
        _installingShapes = true; _shapeDirty = false;
        try
        {
            var version = _shapeVersion;
            bool Still() => version == _shapeVersion && Current(receipt) && version == _shapeVersion;
            var active = new List<PreparedNativeShapes>(); var failures = new List<Exception>();
            foreach (var layer in _shapeLayers.OrderBy(layer => layer.Order).ToArray())
            {
                if (!Still()) return;
                try
                {
                    var prepared = layer.Read(receipt);
                    if (prepared is not null) active.Add(prepared);
                }
                catch (Exception failure) { failures.Add(failure); }
            }
            if (failures.Count != 0) throw new AggregateException("结果几何层失效检查失败。", failures);
            if (!Still()) return;
            ValidateShapeBudget(active);
            var page = Descendants<ShapeItemsPage>(_view).SingleOrDefault();
            var context = (Width: page?.ImageSourceWidth ?? 0, Height: page?.ImageSourceHeight ?? 0,
                Ratio: page?.ControlToImageRadio ?? 0, Thickness: page?.ThicknessScale ?? 0,
                ViewWidth: _view.ActualWidth, ViewHeight: _view.ActualHeight);
            if (layoutRefresh && ReferenceEquals(page, _shapePage) && context == _shapeContext && version == _installedShapeVersion) return;
            var values = active.SelectMany(shape => shape.Items).ToArray();
            _view.ShapeList = values.Length == 0 ? null : new ObservableCollection<object>(values);
            if (!Still()) return;
            _installedShapeCollection = _view.ShapeList;
            _shapePage = page; _shapeContext = context; _installedShapeVersion = version;
        }
        finally
        {
            _installingShapes = false;
            if (_shapeDirty) { _shapeDirty = false; QueueRefresh(); }
        }
    }
    private void RefreshShapes()
    {
        var failures = new List<Exception>();
        foreach (var layer in _shapeLayers.ToArray())
            try { layer.Refresh(); } catch (Exception failure) { failures.Add(failure); }
        if (failures.Count != 0) throw new AggregateException(failures);
    }
    private void RetireShapes(Func<bool> stillOwns)
    {
        ++_shapeVersion; _shapePage = null; _shapeContext = null; _installedShapeVersion = -1; _installedShapeCollection = null;
        var cleanup = _shapeLayers.ToArray().Select<ShapeLayer, Action>(layer => () => { if (stillOwns()) layer.Invalidate(); }).ToList();
        cleanup.Add(() => { if (stillOwns()) _view.ShapeList = null; }); ParameterCleanup.Run(cleanup);
    }

    public abstract class ShapeLayer : IDisposable
    {
        private NativeResultViewport? _owner;
        private NativeResultFrameReceipt? _receipt;
        private PreparedNativeShapes? _prepared;
        private Func<bool>? _proof;
        private long _revision = -1, _attempt;
        internal int Order { get; }
        private protected NativeResultViewport? Owner => _owner;
        private protected ShapeLayer(NativeResultViewport owner, int order) { _owner = owner; Order = order; }
        private protected bool Present(NativeResultFrameReceipt receipt, long revision, Func<bool> proof,
            Func<NativeResultViewport, Func<bool>, PreparedNativeShapes> prepare)
        {
            var owner = _owner ?? throw new ObjectDisposedException(GetType().Name); owner._view.Dispatcher.VerifyAccess();
            ArgumentNullException.ThrowIfNull(receipt); ArgumentNullException.ThrowIfNull(proof);
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!owner.Current(receipt) || revision <= _revision) return false;
            var attempt = ++_attempt;
            bool Current()
            {
                if (!ReferenceEquals(owner, _owner) || attempt != _attempt || !owner.Current(receipt)) return false;
                var valid = proof();
                return valid && ReferenceEquals(owner, _owner) && attempt == _attempt && owner.Current(receipt)
                    && ReferenceEquals(owner, _owner) && attempt == _attempt;
            }
            try
            {
                var prepared = prepare(owner, Current);
                if (!Current()) return false;
                var others = new List<PreparedNativeShapes>();
                foreach (var layer in owner._shapeLayers.Where(layer => !ReferenceEquals(layer, this)).ToArray())
                {
                    if (!Current()) return false;
                    var existing = layer.Read(receipt);
                    if (!Current()) return false;
                    if (existing is not null) others.Add(existing);
                }
                owner.ValidateShapeBudget(others.Append(prepared));
                _prepared = prepared; _proof = proof; _receipt = receipt; _revision = revision;
                owner.ShapesChanged();
                if (Current()) return true;
                if (ReferenceEquals(owner, _owner) && attempt == _attempt) Clear();
                return false;
            }
            catch (NativeProjectionRetiredException) { return false; }
            catch (Exception failure)
            {
                if (ReferenceEquals(owner, _owner) && attempt == _attempt)
                    try { Clear(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
                throw;
            }
        }
        internal PreparedNativeShapes? Read(NativeResultFrameReceipt receipt)
        {
            if (_prepared is null || _receipt != receipt) return null;
            var owner = _owner; var proof = _proof; var attempt = _attempt; var prepared = _prepared;
            try
            {
                var valid = owner is not null && owner.Current(receipt) && proof?.Invoke() == true;
                if (!ReferenceEquals(owner, _owner) || attempt != _attempt) return null;
                valid = valid && owner!.Current(receipt);
                if (!ReferenceEquals(owner, _owner) || attempt != _attempt || !ReferenceEquals(proof, _proof) || receipt != _receipt) return null;
                if (valid) return prepared;
                Clear(); return null;
            }
            catch (Exception failure)
            {
                if (ReferenceEquals(owner, _owner) && attempt == _attempt)
                    try { Clear(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
                throw;
            }
        }
        internal void Refresh() { if (_receipt is {} receipt) Read(receipt); }
        protected virtual void OnInvalidated() { }
        protected virtual void OnDisposed() { }
        internal void Invalidate() { ++_attempt; _revision = -1; _prepared = null; _proof = null; _receipt = null; OnInvalidated(); }
        public void Clear()
        {
            var owner = _owner; if (owner is null) return; owner._view.Dispatcher.VerifyAccess();
            var attempt = _attempt + 1;
            var installed = owner._installedShapeCollection;
            ParameterCleanup.Run(new Action[] { Invalidate,
                () =>
                {
                    if (!ReferenceEquals(owner, _owner) || attempt != _attempt) return;
                    // A callback may already have installed the complete scene without this layer.
                    // Replacing that scene again would unload another layer's new drag handle.
                    if (!ReferenceEquals(installed, owner._installedShapeCollection)
                        && ReferenceEquals(owner._installedShapeCollection, owner._view.ShapeList)
                        && owner._installedShapeVersion == owner._shapeVersion) return;
                    owner.ShapesChanged();
                } });
        }
        public void Dispose()
        {
            var owner = _owner; if (owner is null) return; owner._view.Dispatcher.VerifyAccess();
            _owner = null; ParameterCleanup.Run(new Action[] { Invalidate, () => owner._shapeLayers.Remove(this), owner.ShapesChanged, OnDisposed });
        }
    }
    public sealed class GeometryLayer : ShapeLayer
    {
        internal GeometryLayer(NativeResultViewport owner, int order) : base(owner, order) { }
        public bool Present(NativeResultFrameReceipt receipt, long revision, IReadOnlyList<NativeGeometryOverlay> values, Func<bool> isCurrent)
            => Present(receipt, revision, isCurrent, (owner, current) => NativeShapeProjection.Geometry(receipt, values,
                owner.ShapeBudget, owner._shapeLimits.MaximumPolygonVertices, current));
    }
    public sealed class TextLayer : ShapeLayer
    {
        internal TextLayer(NativeResultViewport owner, int order) : base(owner, order) { }
        public bool Present(NativeResultFrameReceipt receipt, long revision, IReadOnlyList<NativeTextOverlay> values, Func<bool> isCurrent)
            => Present(receipt, revision, isCurrent, (owner, current) =>
            { NativeResultRaster.CheckCurrent(current); var result = NativeShapeProjection.Texts(values, owner.ShapeBudget); NativeResultRaster.CheckCurrent(current); return result; });
    }
}
