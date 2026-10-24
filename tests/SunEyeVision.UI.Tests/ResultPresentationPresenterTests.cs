using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.UI.Services.Monitoring;
using SunEyeVision.UI.Shell.Windows;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class ResultPresentationPresenterTests
{
    [Fact]
    public void Immutable_geometry_values_show_coordinates_frame_and_explicit_angle_units() => Sta(() =>
    {
        var frame=new CoordinateFrame("image:test",CoordinateDomain.Image,CoordinateUnit.Pixel);
        GeometryValue[] shapes={new PointGeometry(frame,new(1.25,2.5)),new LineGeometry(frame,new(0,0),new(3,4)),
            new CircleGeometry(frame,new(2,3),4),new ArcGeometry(frame,new(2,3),4,.25,1.5),
            new OrientedRectangleGeometry(frame,new(2,3),4,5,.75),new EllipseGeometry(frame,new(2,3),4,5,.25),
            new PolygonGeometry(frame,new[]{new Point2(0,0),new Point2(4,0),new Point2(0,3)})};
        var schema=Schema(shapes.Select((shape,i)=>new Port("shape"+i,TypeRef.GeometryOf(shape.Kind,frame))).ToArray());
        using var run=Success(schema,shapes.Select((shape,i)=>(shape,i)).ToDictionary(item=>"shape"+item.i,item=>(object?)item.shape));
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{});
        var previous=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");presenter.ShowRun(run.Snapshot);}
        finally{CultureInfo.CurrentCulture=previous;}
        Assert.Equal(8,rows.Count);Assert.Contains("点 (1.25, 2.5)",rows[1].DisplayValue);
        Assert.All(rows.Skip(1),row=>{Assert.Contains("image:test",row.DisplayValue);Assert.Contains("Pixel",row.DisplayValue);});
        Assert.All(rows.Skip(4).Take(3),row=>Assert.Contains(" rad",row.DisplayValue));
        Assert.Contains("3 顶点",rows[7].DisplayValue);
    });

    [Fact]
    public void Large_immutable_polygon_summary_does_not_enumerate_or_clone_vertices() => Sta(() =>
    {
        var frame=new CoordinateFrame("image:test",CoordinateDomain.Image,CoordinateUnit.Pixel);
        var geometry=new PolygonGeometry(frame,Enumerable.Range(0,100_000).Select(i=>new Point2(i,i%17)));
        var schema=Schema(new Port("polygon",TypeRef.GeometryOf(GeometryKind.Polygon,frame)));
        using var run=Success(schema,new(){["polygon"]=geometry});
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{});
        var before=GC.GetAllocatedBytesForCurrentThread();presenter.ShowRun(run.Snapshot);
        Assert.Equal(2,rows.Count);Assert.Contains("100000 顶点",rows[1].DisplayValue);
        Assert.True(GC.GetAllocatedBytesForCurrentThread()-before<256*1024);
        using var reader=run.Snapshot.AcquireReader();
        Assert.Same(geometry,reader.Values!.Read("polygon").GetGeometry());
        Assert.Throws<InvalidOperationException>(()=>new InputValues(new[]{new Port("n",DataType.Number)},new Dictionary<string,object?>{["n"]=1d}).Read("n").GetGeometry());
    });
    [Fact]
    public void Display_flags_change_without_changing_the_binding_schema()
    {
        var schema=Schema(new Port("visible",DataType.Number),new Port("hidden",DataType.Number));
        var rows=new ObservableCollection<FlatResultRow>();bool has=false;
        using var presenter=new ResultPresentationPresenter(rows,value=>has=value);
        presenter.ShowSchema(schema,displayedPorts:new HashSet<string>{"visible"});
        Assert.True(has);Assert.Equal(new[]{"结果","visible"},rows.Select(row=>row.DisplayName));
        presenter.ShowSchema(schema,displayedPorts:new HashSet<string>{"hidden"});
        Assert.Equal(new[]{"结果","hidden"},rows.Select(row=>row.DisplayName));
        presenter.ShowSchema(schema,displayedPorts:new HashSet<string>());
        Assert.Empty(rows);Assert.False(has);Assert.Equal(2,schema.Outputs.Count);
    }
    private static ToolSchema Schema(params Port[] ports) => new(Array.Empty<Port>(), ports);
    private static RunIdentity Identity() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "source", Guid.NewGuid());

    [Fact]
    public void Existing_grid_receives_declared_order_explicit_root_binding_and_lazy_record_fields() => Sta(() =>
    {
        var record = TypeRef.RecordOf("r", new[] { new Port("z", DataType.Text), new Port("a", DataType.Integer), new Port("last", DataType.Boolean) });
        var schema = Schema(new Port("record", record), new Port("scalar", DataType.Text));
        var rows = new ObservableCollection<FlatResultRow>(); bool has = false;
        using var presenter = new ResultPresentationPresenter(rows, value => has = value, 2);
        var grid = new DataGrid { ItemsSource = rows, IsReadOnly = true, AutoGenerateColumns = false };
        var binding = new ResultBindingInfo("source", "explicit.address", "String", typeof(string));
        presenter.ShowSchema(schema, new Dictionary<string, string> { ["record"] = "显示名称" },
            new Dictionary<string, ResultBindingInfo> { ["record"] = binding });
        Assert.Same(rows, grid.ItemsSource); Assert.True(has);
        Assert.Equal(new[] { "结果", "显示名称", "scalar" }, rows.Select(row => row.DisplayName));
        Assert.Same(binding, rows[1].BindingInfo); Assert.Equal("explicit.address", rows[1].SourcePropertyName);
        Assert.Null(rows[2].BindingInfo); Assert.Null(rows[1].SourceNode);
        rows[1].IsExpanded = true; // Same property changed by the existing control click handler.
        Assert.Equal(new[] { "z", "a" }, rows.Skip(2).Take(2).Select(row => row.DisplayName));
        Assert.All(rows.Skip(2), row => Assert.Null(row.BindingInfo));
        Assert.StartsWith("加载下一页", rows[4].DisplayName);
        var recordRow = rows[1]; var fieldRow = rows[2];
        presenter.ShowSchema(new ToolSchema(schema.Inputs, schema.Outputs));
        Assert.Same(recordRow, rows[1]); Assert.True(recordRow.IsExpanded); Assert.Same(fieldRow, rows[2]);
    });

    [Fact]
    public void Large_sequence_creates_only_clicked_pages_and_preserves_visited_rows() => Sta(() =>
    {
        var schema = Schema(new Port("items", DataType.NumberArray));
        using var run = Success(schema, new() { ["items"] = new double[2_000_000] });
        var rows = new ObservableCollection<FlatResultRow>();
        using var presenter = new ResultPresentationPresenter(rows, _ => { }, 32);
        var before = GC.GetAllocatedBytesForCurrentThread();
        presenter.ShowRun(run.Snapshot);
        Assert.Equal(2, rows.Count); Assert.Equal("[2000000 项]", rows[1].DisplayValue);
        rows[1].IsExpanded = true;
        Assert.Equal(35, rows.Count); Assert.Equal("[0]", rows[2].DisplayName); Assert.Equal("[31]", rows[33].DisplayName);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1024 * 1024, "Presenting 32 elements must not clone the 16 MB frozen array.");
        var first = rows[2]; var port = rows[1]; var oldMore = rows.Last();
        presenter.RefreshRows(); Assert.Equal(35, rows.Count);
        oldMore.IsExpanded = true;
        Assert.Equal(67, rows.Count); Assert.Equal("[63]", rows[65].DisplayName);
        oldMore.IsExpanded = false; oldMore.IsExpanded = true; Assert.Equal(67, rows.Count);
        port.IsExpanded = false; Assert.Equal(2, rows.Count);
        port.IsExpanded = true; Assert.Equal(67, rows.Count); Assert.Same(first, rows[2]);
        presenter.ShowRun(run.Snapshot); Assert.Same(port, rows[1]); Assert.True(port.IsExpanded); Assert.Same(first, rows[2]);
    });

    [Fact]
    public void Nested_sequences_wait_for_each_parent_expansion_and_keep_expanded_state() => Sta(() =>
    {
        var inner = TypeRef.SequenceOf(DataType.Text);
        var record = TypeRef.RecordOf("r", new[] { new Port("values", inner) });
        var outer = TypeRef.SequenceOf(record);
        var schema = Schema(new Port("records", outer));
        using var run = Success(schema, new() { ["records"] = Enumerable.Range(0, 3).Select(_ =>
            new Dictionary<string, object?> { ["values"] = new[] { "a", "b", "c" } }).ToArray() });
        var rows = new ObservableCollection<FlatResultRow>();
        using var presenter = new ResultPresentationPresenter(rows, _ => { }, 2);
        presenter.ShowRun(run.Snapshot); rows[1].IsExpanded = true;
        Assert.Equal(5, rows.Count); Assert.DoesNotContain(rows, row => row.DisplayName == "values");
        var element = rows[2]; element.IsExpanded = true;
        var values = rows[3]; Assert.Equal("values", values.DisplayName); Assert.False(values.IsExpanded);
        values.IsExpanded = true; Assert.Equal("a", rows[4].DisplayValue); Assert.Equal("b", rows[5].DisplayValue);
        element.IsExpanded = false; element.IsExpanded = true;
        Assert.Same(values, rows[3]); Assert.True(values.IsExpanded); Assert.Equal("b", rows[5].DisplayValue);
    });

    [Fact]
    public void Not_run_failed_missing_null_empty_and_successful_empty_sequence_remain_distinct() => Sta(() =>
    {
        var schema = Schema(new Port("missing", DataType.Text, false, true), new Port("null", DataType.Text, false, true),
            new Port("empty", DataType.Text), new Port("items", DataType.NumberArray));
        var rows = new ObservableCollection<FlatResultRow>();
        using var presenter = new ResultPresentationPresenter(rows, _ => { });
        presenter.ShowSchema(schema); Assert.All(rows, row => Assert.Equal("未运行", row.DisplayValue));
        presenter.ShowRun(DeclaredOutputSnapshot.NotExecuted(schema, 2, Identity()));
        Assert.All(rows, row => Assert.Equal("未运行", row.DisplayValue));
        presenter.ShowRun(DeclaredOutputSnapshot.WithoutValues(schema, 2, Identity(), ExecutionStatus.Failed, new[] { new Diagnostic("failure", "detail") }));
        Assert.All(rows, row => Assert.Equal("失败: detail", row.DisplayValue));
        using var run = Success(schema, new() { ["null"] = null, ["empty"] = "", ["items"] = Array.Empty<double>() });
        presenter.ShowRun(run.Snapshot);
        Assert.Equal(new[] { "成功", "missing", "null", "empty", "空序列 (0)" }, rows.Select(row => row.DisplayValue));
        Assert.False(rows.Last().IsGroup);
    });

    [Fact]
    public void Immutable_scalar_formatting_uses_invariant_culture_without_unwrapping_sequences() => Sta(() =>
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var schema = Schema(new Port("number", DataType.Number), new Port("integer", DataType.Integer), new Port("flag", DataType.Boolean),
                new Port("single", DataType.NumberArray));
            using var run = Success(schema, new() { ["number"] = 1.25d, ["integer"] = long.MaxValue, ["flag"] = true, ["single"] = new[] { 3.5d } });
            var rows = new ObservableCollection<FlatResultRow>();
            using var presenter = new ResultPresentationPresenter(rows, _ => { }); presenter.ShowRun(run.Snapshot);
            Assert.Equal(new[] { "成功", "1.25", "9223372036854775807", "true", "[1 项]" }, rows.Select(row => row.DisplayValue));
            rows.Last().IsExpanded = true; Assert.Equal("3.5", rows.Last().DisplayValue);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    });

    [Fact]
    public void Switching_request_or_schema_retires_old_rows_and_old_reader_before_new_view() => Sta(() =>
    {
        var schema = Schema(new Port("items", DataType.NumberArray));
        var rows = new ObservableCollection<FlatResultRow>(); int released = 0; bool rowsWereEmpty = false;
        using var first = Success(schema, new() { ["items"] = new[] { 1d, 2d, 3d } }, () => { released++; rowsWereEmpty = rows.Count == 0; });
        using var second = Success(schema, new() { ["items"] = new[] { 99d } });
        using var presenter = new ResultPresentationPresenter(rows, _ => { }, 1);
        presenter.ShowRun(first.Snapshot); rows[1].IsExpanded = true;
        first.Dispose(); Assert.Equal(0, released);
        var oldRow = rows[1]; var oldMore = rows.Last();
        presenter.ShowRun(first.Snapshot); Assert.Same(oldRow, rows[1]); Assert.Equal(0, released);
        presenter.ShowRun(second.Snapshot); Assert.Equal(1, released); Assert.True(rowsWereEmpty);
        Assert.False(rows[1].IsExpanded); Assert.NotSame(oldRow, rows[1]);
        oldMore.IsExpanded = true; oldRow.IsExpanded = false; oldRow.IsExpanded = true;
        Assert.Equal(2, rows.Count); Assert.Equal("[1 项]", rows[1].DisplayValue);
        rows[1].IsExpanded = true; Assert.Equal("99", rows.Last().DisplayValue);
        presenter.ShowSchema(Schema(new Port("other", DataType.Text)));
        Assert.Equal(new[] { "结果", "other" }, rows.Select(row => row.DisplayName));
    });

    [Fact]
    public void Historical_snapshot_is_presentable_after_its_admission_is_retired() => Sta(() =>
    {
        var schema = Schema(new Port("value", DataType.Number));
        using var run = Success(schema, new() { ["value"] = 42d });
        run.Result.ExecutionIdentity!.Owner.Retire(); Assert.False(run.Result.ExecutionIdentity.IsCurrent);
        var rows = new ObservableCollection<FlatResultRow>();
        using var presenter = new ResultPresentationPresenter(rows, _ => { });
        presenter.ShowRun(run.Snapshot); Assert.Equal("42", rows.Last().DisplayValue);
    });

    [Fact]
    public void Resource_roots_and_resource_aggregates_stay_pending_without_reading_borrowed_payloads() => Sta(() =>
    {
        var image = TypeRef.ResourceOf(ResourceKind.Image);
        var aggregate = TypeRef.RecordOf("r", new[] { new Port("image", image), new Port("label", DataType.Text) });
        var schema = Schema(new Port("image", image), new Port("record", aggregate));
        var poison = new PoisonImage(); int released = 0;
        using var run = Success(schema, new() { ["image"] = poison, ["record"] = new Dictionary<string, object?> { ["image"] = poison, ["label"] = "secret" } }, () => released++);
        var rows = new ObservableCollection<FlatResultRow>();
        var presenter = new ResultPresentationPresenter(rows, _ => { });
        presenter.ShowRun(run.Snapshot); run.Dispose(); Assert.Equal(0, released);
        Assert.All(rows.Skip(1), row => Assert.Equal("资源待接入", row.DisplayValue));
        rows[2].IsExpanded = true;
        Assert.DoesNotContain(rows, row => row.DisplayValue == "secret");
        Assert.Equal("值不可用", rows.Single(row => row.DisplayName == "label").DisplayValue);
        Assert.Equal(0, poison.Accesses); Assert.Equal(0, poison.Disposals);
        presenter.Dispose(); presenter.Dispose(); Assert.Equal(1, released);
        Assert.Equal(0, poison.Accesses); Assert.Equal(0, poison.Disposals);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Queued_old_expansion_is_harmless_after_clear_or_disposal(bool dispose) => Sta(() =>
    {
        var schema = Schema(new Port("items", DataType.NumberArray));
        using var run = Success(schema, new() { ["items"] = new[] { 1d, 2d, 3d } });
        var rows = new ObservableCollection<FlatResultRow>(); bool has = false;
        using var presenter = new ResultPresentationPresenter(rows, value => has = value, 1);
        presenter.ShowRun(run.Snapshot); rows[1].IsExpanded = true; var oldMore = rows.Last();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => oldMore.IsExpanded = true));
        if (dispose) presenter.Dispose(); else presenter.Clear();
        Drain(); Assert.Empty(rows); Assert.False(has);
        if (dispose) Assert.Throws<ObjectDisposedException>(() => presenter.ShowSchema(schema));
        else { presenter.ShowSchema(schema); Assert.Equal(2, rows.Count); Assert.False(rows[1].IsExpanded); }
    });

    [Fact]
    public void Failed_reader_acquisition_leaves_no_old_rows_or_old_lease() => Sta(() =>
    {
        var schema = Schema(new Port("value", DataType.Number)); int released = 0;
        using var first = Success(schema, new() { ["value"] = 1d }, () => released++);
        using var expired = Success(schema, new() { ["value"] = 2d });
        var rows = new ObservableCollection<FlatResultRow>(); bool has = false;
        using var presenter = new ResultPresentationPresenter(rows, value => has = value);
        presenter.ShowRun(first.Snapshot); first.Dispose(); expired.Dispose();
        Assert.Throws<ObjectDisposedException>(() => presenter.ShowRun(expired.Snapshot));
        Assert.Empty(rows); Assert.False(has); Assert.Equal(1, released);
    });

    [Fact]
    public void Empty_schema_and_reentrant_clear_cannot_republish_retired_rows() => Sta(() =>
    {
        var rows = new ObservableCollection<FlatResultRow>(); bool has = false;
        using var presenter = new ResultPresentationPresenter(rows, value => has = value);
        presenter.ShowSchema(Schema()); Assert.Empty(rows); Assert.False(has);
        bool armed = true;
        rows.CollectionChanged += (_, args) =>
        {
            if (armed && args.NewItems?.Count > 0) { armed = false; presenter.Clear(); }
        };
        presenter.ShowSchema(Schema(new Port("value", DataType.Number)));
        Assert.Empty(rows); Assert.False(has);
        presenter.ShowSchema(Schema(new Port("value", DataType.Number))); Assert.Equal(2, rows.Count);
    });

    private static RunFixture Success(ToolSchema schema, Dictionary<string, object?> values, Action? release = null)
    {
        var request = new ToolExecutionRequest(new Parameters()) { NodeId = "source", BindingOutputSchema = schema };
        var result = new ToolExecutor(new ParameterResolver()).Run(new Probe(release ?? (() => { })), request);
        return new(result, DeclaredOutputSnapshot.Publish(result, request.GetInvocationIdentity(), values));
    }

    public sealed class Parameters : ToolParameters { }
    private sealed class Results : ToolResults
    {
        public Results() { }
        public Results(Action release) { Resources = new(release); SetSuccess(); }
    }
    private sealed class Probe(Action release) : IToolPlugin<Parameters, Results>
    { public Results Run(Parameters parameters) => new(release); }
    private sealed class RunFixture(Results result, DeclaredOutputSnapshot snapshot) : IDisposable
    {
        internal Results Result { get; } = result;
        internal DeclaredOutputSnapshot Snapshot { get; } = snapshot;
        public void Dispose() => Result.Resources!.Dispose();
    }
    private sealed class PoisonImage : IReadOnlyImage, IDisposable
    {
        internal int Accesses; internal int Disposals;
        public int Width { get { Accesses++; throw new InvalidOperationException("No payload access"); } }
        public int Height { get { Accesses++; throw new InvalidOperationException("No payload access"); } }
        public byte[] CopyPixels() { Accesses++; throw new InvalidOperationException("No payload access"); }
        public void Dispose() { Disposals++; throw new InvalidOperationException("The presenter cannot dispose borrowed images"); }
    }
    private static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); } catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Presenter Dispatcher test exceeded 20 seconds.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
