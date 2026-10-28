using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.UI.Services.Images;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ResultImagePaneTests
{
    [Fact]
    public void Original_view_uses_actual_frame_domain_padding_and_independent_edit_borrow() => Sta(() =>
    {
        using var source = new Source();
        var view = new ImageViewBox();
        using var pane = new ResultImagePane(view);
        Assert.Throws<InvalidOperationException>(() => new ResultImagePane(view));
        Assert.True(pane.TryPresent(source.Publication, source.Reference(), "flow", () => true, out var error), error);
        Assert.Same(view, pane.Viewport.View);
        var frame = Assert.IsType<SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultFrameReceipt>(pane.CurrentFrame);
        Assert.Equal(source.Image.Frame, frame.Frame);
        Assert.Equal(source.Image.EffectiveDomain.Frame, frame.Coordinates);
        Assert.Equal(new byte[] { 2 }, pane.Viewport.ReadPixel(1, 0));
        using var borrowed = Assert.IsType<ResultViewportImageLease>(pane.BorrowCurrentImage());
        Assert.True(borrowed.IsCurrent);
        Assert.Same(source.Image, borrowed.Image);
        Assert.Equal(frame, borrowed.Receipt);
        source.Publication.Dispose();
        Assert.False(borrowed.IsCurrent);
        Assert.Null(pane.BorrowCurrentImage());
        pane.RefreshCurrentness();
        Assert.Null(view.ImageDataSource);
        Assert.Null(pane.CurrentFrame);
        Assert.Equal(0, source.Owner.Disposals);
        Assert.Equal(new byte[] { 1, 2, 3, 99, 4, 5, 6, 88 }, borrowed.Image.CopyPixels());
        borrowed.Dispose();
        Assert.Equal(1, source.Owner.Disposals);
        Assert.Throws<ObjectDisposedException>(() => source.Image.CopyPixels());
    });

    [Fact]
    public void Real_loaded_native_view_renders_and_unload_clear_keeps_publication_owner_open() => Sta(() =>
    {
        using var source = new Source();
        var view = new ImageViewBox(); using var pane = new ResultImagePane(view);
        var window = new Window { Content = view, Width = 320, Height = 240, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Assert.True(pane.TryPresent(source.Publication, source.Reference(), "flow", () => true, out var error), error);
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Assert.True(view.IsLoaded); Assert.NotNull(view.Source);
            Assert.Equal(source.Image.Frame, pane.CurrentFrame!.Frame);
            pane.Clear();
            Assert.Null(view.ImageDataSource); Assert.Null(pane.CurrentFrame);
            Assert.Equal(0, source.Publication.ActiveBorrows);
            Assert.Equal(0, source.Publication.OwnerCloses);
        }
        finally { window.Content = null; window.Close(); }
    });

    [Fact]
    public void Constant_index_one_uses_second_real_image_instead_of_first_or_latest() => Sta(() =>
    {
        using var resources = new FrozenOutputResources();
        var first = resources.CreateGray8(1, 1, new byte[] { 7 });
        var second = resources.CreateGray8(1, 1, new byte[] { 9 });
        var type = TypeRef.SequenceOf(TypeRef.ResourceOf(ResourceKind.Image), CountConstraint.Exactly(2));
        var schema = new ToolSchema(Array.Empty<Port>(), new[] { new Port("images", type) });
        var identity = new RunIdentity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "source", Guid.NewGuid());
        using var publication = new ResultPublication(schema,
            new ResultPublicationStamp(Guid.NewGuid(), identity, 7, schema.Fingerprint), ResultViewState.Success,
            new InputValues(schema.Outputs, new Dictionary<string, object?> { ["images"] = new[] { first, second } }), resources, () => true);
        using var pane = new ResultImagePane(new ImageViewBox());
        Assert.True(pane.TryPresent(publication, new OutputReference("source", "images", type,
            selection: new BindingSelection.AtConstant(1), scopeId: "flow"), "flow", () => true, out var error), error);
        Assert.Equal(second.Frame, pane.CurrentFrame!.Frame);
        Assert.NotEqual(first.Frame, pane.CurrentFrame.Frame);
        Assert.Equal(new byte[] { 9 }, pane.Viewport.ReadPixel(0, 0));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Declared_record_leaf_and_sequence_constant_select_exact_physical_image(bool sequence) => Sta(() =>
    {
        using var source = new Source(nested: true, sequence: sequence);
        using var pane = new ResultImagePane(new ImageViewBox());
        Assert.True(pane.TryPresent(source.Publication, source.Reference(), "flow", () => true, out var error), error);
        Assert.Equal(source.Image.Frame, pane.CurrentFrame!.Frame);
        Assert.Equal(1, source.Publication.ActiveBorrows);
        pane.Clear();
        Assert.Equal(0, source.Publication.ActiveBorrows);
        Assert.Equal(0, source.Publication.OwnerCloses);
        Assert.Equal(0, source.Owner.Disposals);
    });

    [Theory]
    [InlineData("scope")]
    [InlineData("node")]
    [InlineData("root-type")]
    [InlineData("port")]
    [InlineData("member")]
    [InlineData("index")]
    [InlineData("all-sequence")]
    [InlineData("dynamic")]
    [InlineData("cancel")]
    [InlineData("retired")]
    public void Invalid_source_clears_old_image_and_drains_only_its_own_borrows(string mode) => Sta(() =>
    {
        using var source = new Source(nested: true, sequence: true);
        var view = new ImageViewBox(); using var pane = new ResultImagePane(view);
        Assert.True(pane.TryPresent(source.Publication, source.Reference(), "flow", () => true, out _));
        var reference = new OutputReference(mode == "node" ? "other" : "source", mode == "port" ? "removed" : "output.image",
            mode == "root-type" ? TypeRef.ScalarOf(ScalarKind.Text) : source.Type,
            new[] { mode == "member" ? "missing" : "capture" },
            mode == "all-sequence" ? BindingSelection.All : mode == "dynamic"
                ? new BindingSelection.AtField(new OutputReference("index", "value", TypeRef.ScalarOf(ScalarKind.Int64), scopeId: "flow"))
                : new BindingSelection.AtConstant(mode == "index" ? 4 : 0), scopeId: mode == "scope" ? "foreign" : "flow");
        if (mode == "retired") source.Publication.Dispose();
        Assert.False(pane.TryPresent(source.Publication, reference, "flow", () => mode != "cancel", out var error));
        Assert.NotEmpty(error);
        Assert.Null(pane.CurrentFrame); Assert.Null(view.ImageDataSource);
        Assert.Null(pane.BorrowCurrentImage());
        Assert.Equal(0, source.Publication.ActiveBorrows);
        Assert.Equal(mode == "retired" ? 1 : 0, source.Publication.OwnerCloses);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mutated_domain_coordinates_or_physical_frame_are_rejected_without_fallback(bool wrongCoordinates) => Sta(() =>
    {
        CorruptImage? image = null;
        using var source = new Source(wrap: original => image = new CorruptImage(original));
        using var pane = new ResultImagePane(new ImageViewBox());
        image!.Corrupt = true; image.WrongCoordinates = wrongCoordinates;
        Assert.False(pane.TryPresent(source.Publication, source.Reference(), "flow", () => true, out var error));
        Assert.NotEmpty(error); Assert.Null(pane.CurrentFrame);
        Assert.Equal(0, source.Publication.ActiveBorrows);
    });

    [Fact]
    public void Reentrant_borrow_and_native_image_notification_never_clear_the_new_frame() => Sta(() =>
    {
        using var old = new Source(); using var next = new Source();
        var view = new ImageViewBox(); using var pane = new ResultImagePane(view);
        old.Publication.OnBorrow = () =>
        {
            old.Publication.OnBorrow = null;
            Assert.True(pane.TryPresent(next.Publication, next.Reference(), "flow", () => true, out var error), error);
        };
        Assert.False(pane.TryPresent(old.Publication, old.Reference(), "flow", () => true, out _));
        Assert.Equal(next.Image.Frame, pane.CurrentFrame!.Frame);
        Assert.Equal(0, old.Publication.ActiveBorrows);
        pane.Clear();
        var descriptor = DependencyPropertyDescriptor.FromProperty(ImageViewBox.ImageDataSourceProperty, typeof(ImageViewBox));
        var armed = true;
        EventHandler changed = (_, _) =>
        {
            if (!armed || view.ImageDataSource is null) return;
            armed = false;
            Assert.True(pane.TryPresent(next.Publication, next.Reference(), "flow", () => true, out var error), error);
        };
        descriptor.AddValueChanged(view, changed);
        try
        {
            Assert.False(pane.TryPresent(old.Publication, old.Reference(), "flow", () => true, out _));
            Assert.Equal(next.Image.Frame, pane.CurrentFrame!.Frame);
            Assert.Equal(0, old.Publication.ActiveBorrows);
            Assert.Equal(1, next.Publication.ActiveBorrows);
        }
        finally { descriptor.RemoveValueChanged(view, changed); }
    });

    [Fact]
    public void Old_owner_cleanup_failure_cannot_overwrite_reentrant_new_presentation() => Sta(() =>
    {
        using var old = new Source(); using var next = new Source();
        var view = new ImageViewBox(); using var pane = new ResultImagePane(view);
        Assert.True(pane.TryPresent(old.Publication, old.Reference(), "flow", () => true, out _));
        old.Publication.Dispose();
        old.Owner.AfterDispose = () =>
        {
            Assert.True(pane.TryPresent(next.Publication, next.Reference(), "flow", () => true, out var error), error);
            throw new InvalidOperationException("old owner cleanup failure");
        };
        Assert.Throws<AggregateException>(() => pane.Clear());
        Assert.Equal(1, old.Owner.Disposals);
        Assert.Equal(next.Image.Frame, pane.CurrentFrame!.Frame);
        Assert.NotNull(view.ImageDataSource);
        Assert.Equal(1, next.Publication.ActiveBorrows);
    });

    [Fact]
    public void Current_callback_reentry_and_close_never_restore_an_old_receipt() => Sta(() =>
    {
        using var old = new Source(); using var next = new Source();
        var view = new ImageViewBox(); var pane = new ResultImagePane(view);
        var armed = true;
        Assert.False(pane.TryPresent(old.Publication, old.Reference(), "flow", () =>
        {
            if (armed) { armed = false; Assert.True(pane.TryPresent(next.Publication, next.Reference(), "flow", () => true, out _)); }
            return true;
        }, out _));
        Assert.Equal(next.Image.Frame, pane.CurrentFrame!.Frame);
        using var independent = Assert.IsType<ResultViewportImageLease>(pane.BorrowCurrentImage());
        pane.Dispose();
        Assert.False(independent.IsCurrent); Assert.Null(pane.CurrentFrame);
        Assert.Null(view.ImageDataSource);
        Assert.False(pane.TryPresent(old.Publication, old.Reference(), "flow", () => true, out _));
        Assert.Equal(0, old.Publication.ActiveBorrows);
        Assert.Equal(0, next.Publication.OwnerCloses);
    });

    [Fact]
    public void Cancellation_during_native_capture_drains_candidate_without_closing_publication_owner() => Sta(() =>
    {
        using var source = new Source(); var view = new ImageViewBox(); using var pane = new ResultImagePane(view);
        var checks = 0;
        Assert.False(pane.TryPresent(source.Publication, source.Reference(), "flow", () => ++checks < 4, out var error));
        Assert.NotEmpty(error); Assert.True(checks >= 4);
        Assert.Null(view.ImageDataSource); Assert.Null(pane.CurrentFrame);
        Assert.Equal(0, source.Publication.ActiveBorrows);
        Assert.Equal(0, source.Publication.OwnerCloses);
        Assert.Equal(0, source.Owner.Disposals);
    });

    [Fact]
    public void File_preview_survives_decoder_scope_close_but_cannot_authorize_formal_roi_borrow() => Sta(() =>
    {
        var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(2, 1, new byte[] { 7, 8 });
        var frame = image.Frame; var view = new ImageViewBox(); using var pane = new ResultImagePane(view);
        var current = true;
        Assert.True(pane.TryPresentPreview(image, () => current, out var error), error);
        resources.Dispose();
        Assert.Throws<ObjectDisposedException>(() => image.CopyPixels());
        Assert.Equal(frame, pane.CurrentFrame!.Frame);
        Assert.Equal(new byte[] { 8 }, pane.Viewport.ReadPixel(1, 0));
        Assert.Null(pane.BorrowCurrentImage());
        current = false; pane.RefreshCurrentness();
        Assert.Null(pane.CurrentFrame); Assert.Null(view.ImageDataSource);
        using var formal = new Source();
        Assert.True(pane.TryPresent(formal.Publication, formal.Reference(), "flow", () => true, out _));
        using var borrowed = Assert.IsType<ResultViewportImageLease>(pane.BorrowCurrentImage());
        using var previewResources = new FrozenOutputResources();
        var replacement = previewResources.CreateGray8(1, 1, new byte[] { 3 });
        Assert.True(pane.TryPresentPreview(replacement, () => true, out _));
        Assert.False(borrowed.IsCurrent); Assert.Null(pane.BorrowCurrentImage());
    });

    private sealed class Source : IDisposable
    {
        internal readonly TrackingOwner Owner;
        internal readonly TrackingPublication Publication;
        internal readonly IReadOnlyImage Image;
        internal readonly TypeRef Type;
        private readonly bool _nested, _sequence;
        internal Source(bool nested = false, bool sequence = false, Func<IReadOnlyImage, IReadOnlyImage>? wrap = null)
        {
            _nested = nested; _sequence = sequence;
            var resources = new FrozenOutputResources(); Owner = new(resources);
            var frame = new ImageFrameAddress(Guid.NewGuid(), "captured-camera");
            var coordinates = new CoordinateFrame("image:actual-domain", CoordinateDomain.Image, CoordinateUnit.Pixel);
            var domain = resources.CreateRegion(coordinates, frame, new[] { new RegionRun(0, 1, 2), new RegionRun(1, 0, 0) });
            var actual = resources.CreateImage(new ImageLayout(3, 2, 4, PixelLayout.Gray8), frame, domain,
                new byte[] { 1, 2, 3, 99, 4, 5, 6, 88 });
            Image = wrap?.Invoke(actual) ?? actual;
            Type = TypeRef.ResourceOf(ResourceKind.Image);
            object value = Image;
            if (nested)
            {
                Type = TypeRef.RecordOf("captured", new[] { new Port("capture", Type) });
                value = new RecordValue(Type, new Dictionary<string, object?> { ["capture"] = Image });
            }
            if (sequence)
            {
                Type = TypeRef.SequenceOf(Type, CountConstraint.Exactly(1));
                value = new SequenceValue(Type, new[] { value });
            }
            var schema = new ToolSchema(Array.Empty<Port>(), new[] { new Port("output.image", Type) });
            var identity = new RunIdentity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "source", Guid.NewGuid());
            Publication = new(new ResultPublication(schema, new ResultPublicationStamp(Guid.NewGuid(), identity, 7, schema.Fingerprint),
                ResultViewState.Success, new InputValues(schema.Outputs, new Dictionary<string, object?> { ["output.image"] = value }), Owner, () => true));
        }
        internal OutputReference Reference() => new("source", "output.image", Type, _nested ? new[] { "capture" } : null,
            _sequence ? new BindingSelection.AtConstant(0) : BindingSelection.All, scopeId: "flow");
        public void Dispose() => Publication.Dispose();
    }
    private sealed class TrackingOwner(IDisposable resources) : IDisposable
    {
        internal int Disposals; internal Action? AfterDispose;
        public void Dispose() { Disposals++; resources.Dispose(); AfterDispose?.Invoke(); }
    }
    private sealed class TrackingPublication(IResultPublication inner) : IResultPublication
    {
        internal int ActiveBorrows, OwnerCloses;
        internal Action? OnBorrow;
        private bool _closed;
        public ResultPublicationStamp Stamp => inner.Stamp;
        public ToolSchema Schema => inner.Schema;
        public ResultViewState State => inner.State;
        public IReadOnlyList<Diagnostic> Diagnostics => inner.Diagnostics;
        public IReadOnlyDictionary<string, string> DisplayNames => inner.DisplayNames;
        public IReadOnlySet<string> DisplayedPorts => inner.DisplayedPorts;
        public bool IsCurrent() => inner.IsCurrent();
        public IResultValueLease Borrow()
        {
            var borrowed = inner.Borrow(); ActiveBorrows++;
            try { OnBorrow?.Invoke(); return new Lease(this, borrowed); }
            catch { borrowed.Dispose(); ActiveBorrows--; throw; }
        }
        public void Dispose() { if (_closed) return; _closed = true; OwnerCloses++; inner.Dispose(); }
        private sealed class Lease(TrackingPublication owner, IResultValueLease inner) : IResultValueLease
        {
            private bool _closed;
            public ResultPublicationStamp Stamp => inner.Stamp;
            public InputValues? Values => inner.Values;
            public void Dispose() { if (_closed) return; _closed = true; owner.ActiveBorrows--; inner.Dispose(); }
        }
    }
    private sealed class CorruptImage(IReadOnlyImage original) : IReadOnlyImage
    {
        internal bool Corrupt, WrongCoordinates;
        public int Width => original.Width; public int Height => original.Height;
        public ImageLayout Layout => original.Layout; public ImageFrameAddress Frame => original.Frame;
        public IReadOnlyRegion EffectiveDomain => Corrupt ? new CorruptRegion(original.EffectiveDomain, WrongCoordinates) : original.EffectiveDomain;
        public byte[] CopyPixels() => original.CopyPixels();
        public IImagePixelReadLease AcquirePixels() => original.AcquirePixels();
    }
    private sealed class CorruptRegion(IReadOnlyRegion original, bool wrongCoordinates) : IReadOnlyRegion
    {
        public CoordinateFrame Frame => wrongCoordinates ? new("world", CoordinateDomain.Model, CoordinateUnit.Millimeter) : original.Frame;
        public ImageFrameAddress SourceFrame => wrongCoordinates ? original.SourceFrame : new(Guid.NewGuid(), "foreign");
        public IRegionReadLease AcquireReader() => original.AcquireReader();
    }
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); } catch (Exception error) { failure = error; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "原结果图像pane测试超过20秒。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
