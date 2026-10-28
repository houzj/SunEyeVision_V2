using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.UI.Services.Monitoring;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.UI.Shell.Windows;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class ResultPresentationPresenterTests:IDisposable
{
    [Fact]
    public void Sdk_snapshot_drives_tree_and_table_together_and_retired_run_cannot_replace_either() => Sta(() =>
    {
        var schema=Schema(new Port("value",DataType.Integer));
        var projection=new ResultProjectionDescriptor(ResultProjectionKind.StandardTable,ResultCardinality.One,
            new[]{new ResultColumnDescriptor("value","值",new("value"))});
        var rows=new ObservableCollection<FlatResultRow>();
        using var tree=new ResultPresentationPresenter(rows,_=>{});
        using var table=new DeclaredResultTable(schema,projection);
        var grid=Assert.IsType<DataGrid>(table.Content);
        object? Cell()=>grid.Items[0].GetType().GetProperty("Item")!.GetValue(grid.Items[0],new object[]{"value"});
        using var declaration=ToolResultViews.Declaration(schema,()=>true);
        tree.Present(declaration);table.Present(declaration);
        Assert.Equal("未运行",rows[1].DisplayValue);Assert.Empty(grid.Items);

        using var first=Success(schema,new(){["value"]=11L});
        using var second=Success(schema,new(){["value"]=22L});
        var a=new TrackedPublication(first.Publication);var b=new TrackedPublication(second.Publication);
        tree.Present(a);table.Present(a);
        Assert.Equal("11",rows[1].DisplayValue);Assert.Equal(11L,Cell());
        Assert.Equal(first.Snapshot.Identity,a.Stamp.Invocation);
        Assert.Equal(2,a.Borrows);Assert.Equal(1,a.LeaseDisposals); // 表已复制，树保留惰性借阅。
        var oldRow=rows[1];var oldTableRow=grid.Items[0];
        a.Dispose();tree.Present(b);table.Present(b);
        Assert.NotEqual(a.Stamp.Invocation,b.Stamp.Invocation);
        Assert.Equal(second.Snapshot.Identity,b.Stamp.Invocation);
        Assert.Equal("22",rows[1].DisplayValue);Assert.Equal(22L,Cell());
        Assert.NotSame(oldRow,rows[1]);Assert.NotSame(oldTableRow,grid.Items[0]);
        Assert.Equal(2,a.LeaseDisposals);
        // 晚到的旧运行不得把任一消费端回退到11；也不能再借旧reader。
        tree.Present(a);table.Present(a);
        Assert.Equal("22",rows[1].DisplayValue);Assert.Equal(22L,Cell());Assert.Equal(2,a.Borrows);
        tree.Clear();table.Clear();Assert.Empty(rows);Assert.Empty(grid.Items);
        Assert.Equal(2,b.LeaseDisposals);
    });

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
        try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");presenter.Present(run.Publication);}
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
        var before=GC.GetAllocatedBytesForCurrentThread();presenter.Present(run.Publication);
        Assert.Equal(2,rows.Count);Assert.Contains("100000 顶点",rows[1].DisplayValue);
        Assert.True(GC.GetAllocatedBytesForCurrentThread()-before<256*1024);
        var expansionBefore=GC.GetAllocatedBytesForCurrentThread();
        rows[1].IsExpanded=true;
        Assert.Equal(131,rows.Count);Assert.Equal("[0]",rows[2].DisplayName);Assert.Equal("[127]",rows[129].DisplayName);
        Assert.Contains("点 (127, 8)",rows[129].DisplayValue);
        Assert.True(GC.GetAllocatedBytesForCurrentThread()-expansionBefore<1024*1024);
        var firstVertex=rows[2];rows.Last().IsExpanded=true;
        Assert.Equal(259,rows.Count);Assert.Equal("[255]",rows[257].DisplayName);Assert.Same(firstVertex,rows[2]);
        rows[1].IsExpanded=false;rows[1].IsExpanded=true;Assert.Same(firstVertex,rows[2]);
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
        SchemaPresent(presenter,schema,displayedPorts:new HashSet<string>{"visible"});
        Assert.True(has);Assert.Equal(new[]{"结果","visible"},rows.Select(row=>row.DisplayName));
        SchemaPresent(presenter,schema,displayedPorts:new HashSet<string>{"hidden"});
        Assert.Equal(new[]{"结果","hidden"},rows.Select(row=>row.DisplayName));
        SchemaPresent(presenter,schema,displayedPorts:new HashSet<string>());
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
        var binding = new ResultBindingInfo(new OutputReference("source","record",record,scopeId:"flow"),"record");
        using var declaration=ToolResultViews.Declaration(schema,()=>true,new Dictionary<string,string>{["record"]="显示名称"});
        presenter.Present(declaration,new Dictionary<string,ResultBindingInfo>{["record"]=binding});
        Assert.Same(rows, grid.ItemsSource); Assert.True(has);
        Assert.Equal(new[] { "结果", "显示名称", "scalar" }, rows.Select(row => row.DisplayName));
        Assert.Null(rows[1].BindingInfo); // A record has no variable storage; its declared scalar members do.
        Assert.Null(rows[2].BindingInfo); Assert.Null(rows[1].SourceNode);
        rows[1].IsExpanded = true; // Same property changed by the existing control click handler.
        Assert.Equal(new[] { "z", "a" }, rows.Skip(2).Take(2).Select(row => row.DisplayName));
        Assert.Equal(new[]{"z"},rows[2].BindingInfo!.Reference.MemberFieldPath);
        Assert.Equal(new[]{"a"},rows[3].BindingInfo!.Reference.MemberFieldPath);
        Assert.Null(rows[4].BindingInfo);
        Assert.StartsWith("加载下一页", rows[4].DisplayName);
        var recordRow = rows[1]; var fieldRow = rows[2];
        presenter.Present(declaration);
        Assert.Same(recordRow, rows[1]); Assert.True(recordRow.IsExpanded); Assert.Same(fieldRow, rows[2]);
    });

    [Fact]
    public void Empty_and_declaration_record_sequences_expose_explicit_All_members_but_never_sample_indices() => Sta(() =>
    {
        var record=TypeRef.RecordOf("record",new[]{new Port("name",DataType.Text),new Port("image",TypeRef.ResourceOf(ResourceKind.Image))});
        var type=TypeRef.SequenceOf(record);var schema=Schema(new Port("records",type));
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{});
        using var publication=ToolResultViews.Declaration(schema,()=>true);
        var endpoint=new ResultBindingInfo(new OutputReference("source","records",type,scopeId:"flow"),"records");
        presenter.Present(publication,new Dictionary<string,ResultBindingInfo>{{"records",endpoint}});
        var records=rows[1];Assert.Null(records.BindingInfo);Assert.True(records.IsGroup);
        records.IsExpanded=true;var all=Assert.Single(rows.Where(row=>row.DisplayName=="All · 全部元素成员"));
        all.IsExpanded=true;var member=Assert.Single(rows.Where(row=>row.DisplayName=="name [All]"));
        Assert.NotNull(member.BindingInfo);Assert.Equal(new[]{"name"},member.BindingInfo.Reference.MemberFieldPath);
        Assert.Equal(type,member.BindingInfo.Reference.Type);Assert.IsType<BindingSelection.AllItems>(member.BindingInfo.Reference.Selection);
        Assert.Contains("All",member.DisplayValue);Assert.Null(Assert.Single(rows.Where(row=>row.DisplayName=="image [All]")).BindingInfo);
        Assert.DoesNotContain(rows,row=>row.DisplayName=="[0]");
        var retained=member;presenter.Clear();Assert.False(presenter.ContainsRow(retained));
    });

    [Fact]
    public void Observed_record_samples_and_their_fields_never_masquerade_as_single_index_bindings() => Sta(() =>
    {
        var record=TypeRef.RecordOf("record",new[]{new Port("name",DataType.Text)});var type=TypeRef.SequenceOf(record);
        var schema=Schema(new Port("records",type));using var run=Success(schema,new(){["records"]=new[]{new Dictionary<string,object?>{{"name","first"}}}});
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{});
        var endpoint=new ResultBindingInfo(new OutputReference("source","records",type,scopeId:"flow"),"records");
        presenter.Present(run.Publication,new Dictionary<string,ResultBindingInfo>{{"records",endpoint}});
        rows[1].IsExpanded=true;var sample=Assert.Single(rows.Where(row=>row.DisplayName=="[0]"));Assert.Null(sample.BindingInfo);
        sample.IsExpanded=true;var value=Assert.Single(rows.Where(row=>row.DisplayName=="name"));
        Assert.Equal("first",value.DisplayValue);Assert.Null(value.BindingInfo);Assert.False(value.CanBindVariable);
        var all=Assert.Single(rows.Where(row=>row.DisplayName=="All · 全部元素成员"));all.IsExpanded=true;
        var declared=Assert.Single(rows.Where(row=>row.DisplayName=="name [All]"));Assert.NotNull(declared.BindingInfo);
        var reference=declared.BindingInfo!.Reference;Assert.IsType<BindingSelection.AllItems>(reference.Selection);
        Assert.Equal(new[]{"name"},reference.MemberFieldPath);Assert.Equal(type,reference.Type);
    });

    [Fact]
    public void Large_sequence_creates_only_clicked_pages_and_preserves_visited_rows() => Sta(() =>
    {
        var schema = Schema(new Port("items", DataType.NumberArray));
        using var run = Success(schema, new() { ["items"] = new double[2_000_000] });
        var rows = new ObservableCollection<FlatResultRow>();
        using var presenter = new ResultPresentationPresenter(rows, _ => { }, 32);
        var before = GC.GetAllocatedBytesForCurrentThread();
        presenter.Present(run.Publication);
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
        presenter.Present(run.Publication); Assert.Same(port, rows[1]); Assert.True(port.IsExpanded); Assert.Same(first, rows[2]);
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
        presenter.Present(run.Publication); rows[1].IsExpanded = true;
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
        SchemaPresent(presenter,schema); Assert.All(rows, row => Assert.Equal("未运行", row.DisplayValue));
        using var notExecuted=ToolResultViews.FromSnapshot(DeclaredOutputSnapshot.NotExecuted(schema,2,Identity()),()=>true);presenter.Present(notExecuted);
        Assert.All(rows, row => Assert.Equal("未运行", row.DisplayValue));
        SnapshotPresent(presenter,DeclaredOutputSnapshot.WithoutValues(schema, 2, Identity(), ExecutionStatus.Failed, new[] { new Diagnostic("failure", "detail") }));
        Assert.All(rows, row => Assert.Equal("失败: detail", row.DisplayValue));
        using var run = Success(schema, new() { ["null"] = null, ["empty"] = "", ["items"] = Array.Empty<double>() });
        presenter.Present(run.Publication);
        Assert.Equal(new[] { "成功", "missing", "null", "empty", "空序列 (0)" }, rows.Select(row => row.DisplayValue));
        Assert.False(rows.Last().IsGroup);
    });

    [Theory]
    [InlineData(ExecutionStatus.Failed,"失败")]
    [InlineData(ExecutionStatus.Timeout,"超时")]
    [InlineData(ExecutionStatus.Cancelled,"已取消")]
    public void Published_non_success_outcomes_remain_distinct_without_reading_resource_values(ExecutionStatus status,string label) => Sta(() =>
    {
        var schema=Schema(new Port("image",TypeRef.ResourceOf(ResourceKind.Image)),new Port("number",DataType.Number));
        var rows=new ObservableCollection<FlatResultRow>();
        using var presenter=new ResultPresentationPresenter(rows,_=>{});
        SnapshotPresent(presenter,DeclaredOutputSnapshot.WithoutValues(schema,1,Identity(),status,new[]{new Diagnostic("test","detail")}));
        Assert.Equal(3,rows.Count);
        Assert.All(rows,row=>Assert.Equal(label+": detail",row.DisplayValue));
    });

    [Fact]
    public void Successful_empty_output_keeps_its_published_status_visible() => Sta(() =>
    {
        var rows=new ObservableCollection<FlatResultRow>();bool has=false;
        using var presenter=new ResultPresentationPresenter(rows,value=>has=value);
        SchemaPresent(presenter,Schema());Assert.Empty(rows);
        using var run=Success(Schema(),new());presenter.Present(run.Publication);
        Assert.Equal("成功",Assert.Single(rows).DisplayValue);Assert.True(has);
    });

    [Fact]
    public void A_distinct_publication_with_the_same_run_replaces_old_diagnostics() => Sta(() =>
    {
        var schema=Schema(new Port("number",DataType.Number));var identity=Identity();
        var rows=new ObservableCollection<FlatResultRow>();
        using var presenter=new ResultPresentationPresenter(rows,_=>{});
        SnapshotPresent(presenter,DeclaredOutputSnapshot.WithoutValues(schema,1,identity,ExecutionStatus.Timeout,new[]{new Diagnostic("timeout","first")}));
        Assert.All(rows,row=>Assert.Equal("超时: first",row.DisplayValue));
        SnapshotPresent(presenter,DeclaredOutputSnapshot.WithoutValues(schema,1,identity,ExecutionStatus.Timeout,new[]{new Diagnostic("timeout","second")}));
        Assert.All(rows,row=>Assert.Equal("超时: second",row.DisplayValue));
    });

    [Fact]
    public void Native_hierarchy_borrows_the_same_rows_and_loads_only_on_expansion() => Sta(() =>
    {
        var schema=Schema(new Port("items",DataType.NumberArray));
        using var run=Success(schema,new(){["items"]=new[]{1d,2d,3d}});
        var rows=new ObservableCollection<FlatResultRow>();
        using var presenter=new ResultPresentationPresenter(rows,_=>{},2);
        presenter.Present(run.Publication);run.ReleaseResult();
        var root=Assert.Single(presenter.Roots);Assert.Same(rows[0],root);
        var sequence=Assert.Single(root.Children);Assert.Same(rows[1],sequence);
        Assert.True(sequence.IsGroup);Assert.Empty(sequence.Children);
        sequence.IsExpanded=true;
        Assert.Equal(3,sequence.Children.Count);Assert.Same(rows[2],sequence.Children[0]);
        var first=sequence.Children[0];var more=sequence.Children.Last();
        Assert.StartsWith("加载下一页",more.DisplayName);more.IsExpanded=true;
        Assert.Equal(new[]{"1","2","3"},sequence.Children.Select(row=>row.DisplayValue));
        Assert.Same(first,sequence.Children[0]);Assert.NotEmpty(sequence.Key);
        SchemaPresent(presenter,Schema(new Port("other",DataType.Boolean)));
        var current=Assert.Single(presenter.Roots);more.IsExpanded=false;more.IsExpanded=true;
        Assert.False(presenter.ContainsRow(sequence));Assert.True(presenter.ContainsRow(current));
        Assert.Same(current,Assert.Single(presenter.Roots));Assert.Equal("other",Assert.Single(current.Children).DisplayName);
    });

    [Fact]
    public void Reentrant_hierarchy_retirement_cannot_republish_old_rows() => Sta(() =>
    {
        var rows=new ObservableCollection<FlatResultRow>();bool has=false;
        using var presenter=new ResultPresentationPresenter(rows,value=>has=value);
        ((System.Collections.Specialized.INotifyCollectionChanged)presenter.Roots).CollectionChanged+=(_,args)=>
        {if(args.NewItems?.Count>0)presenter.Clear();};
        SchemaPresent(presenter,Schema(new Port("number",DataType.Number)));
        Assert.Empty(presenter.Roots);Assert.Empty(rows);Assert.False(has);
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
            using var presenter = new ResultPresentationPresenter(rows, _ => { }); presenter.Present(run.Publication);
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
        presenter.Present(first.Publication); rows[1].IsExpanded = true;
        first.ReleaseResult(); Assert.Equal(0, released);
        var oldRow = rows[1]; var oldMore = rows.Last();
        presenter.Present(first.Publication); Assert.Same(oldRow, rows[1]); Assert.Equal(0, released);
        first.ReleasePublication();presenter.Present(second.Publication); Assert.Equal(1, released); Assert.True(rowsWereEmpty);
        Assert.False(rows[1].IsExpanded); Assert.NotSame(oldRow, rows[1]);
        oldMore.IsExpanded = true; oldRow.IsExpanded = false; oldRow.IsExpanded = true;
        Assert.Equal(2, rows.Count); Assert.Equal("[1 项]", rows[1].DisplayValue);
        rows[1].IsExpanded = true; Assert.Equal("99", rows.Last().DisplayValue);
        SchemaPresent(presenter,Schema(new Port("other", DataType.Text)));
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
        presenter.Present(run.Publication); Assert.Equal("42", rows.Last().DisplayValue);
    });

    [Fact]
    public void Resource_leaves_stay_pending_while_mixed_record_scalars_remain_visible_without_reading_payloads() => Sta(() =>
    {
        var image = TypeRef.ResourceOf(ResourceKind.Image);
        var aggregate = TypeRef.RecordOf("r", new[] { new Port("image", image), new Port("label", DataType.Text) });
        var schema = Schema(new Port("image", image), new Port("record", aggregate));
        var poison = new PoisonImage(); int released = 0;
        using var run = Success(schema, new() { ["image"] = poison, ["record"] = new Dictionary<string, object?> { ["image"] = poison, ["label"] = "secret" } }, () => released++);
        poison.BeginPresentation();
        var publication=run.Publication; // The publication borrows admitted values; it must not re-read resources either.
        var rows = new ObservableCollection<FlatResultRow>();
        var presenter = new ResultPresentationPresenter(rows, _ => { });
        presenter.Present(publication);run.ReleaseResult();Assert.Equal(0,released);
        Assert.Equal("资源待接入",rows[1].DisplayValue);Assert.Equal("记录",rows[2].DisplayValue);
        rows[2].IsExpanded=true;
        Assert.All(rows.Where(row=>row.DisplayName=="image"),row=>Assert.Equal("资源待接入",row.DisplayValue));
        Assert.Equal("secret",rows.Single(row=>row.DisplayName=="label").DisplayValue);
        Assert.Equal(0, poison.Accesses); Assert.Equal(0, poison.Disposals);
        run.ReleasePublication();Assert.Equal(0,released);presenter.Dispose(); presenter.Dispose(); Assert.Equal(1, released);
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
        presenter.Present(run.Publication); rows[1].IsExpanded = true; var oldMore = rows.Last();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => oldMore.IsExpanded = true));
        if (dispose) presenter.Dispose(); else presenter.Clear();
        Drain(); Assert.Empty(rows); Assert.False(has);
        if (dispose) Assert.Throws<ObjectDisposedException>(() => SchemaPresent(presenter,schema));
        else { SchemaPresent(presenter,schema); Assert.Equal(2, rows.Count); Assert.False(rows[1].IsExpanded); }
    });

    [Fact]
    public void Failed_reader_acquisition_leaves_no_old_rows_or_old_lease() => Sta(() =>
    {
        var schema = Schema(new Port("value", DataType.Number)); int released = 0;
        using var first = Success(schema, new() { ["value"] = 1d }, () => released++);
        using var expired = Success(schema, new() { ["value"] = 2d });
        var rows = new ObservableCollection<FlatResultRow>(); bool has = false;
        using var presenter = new ResultPresentationPresenter(rows, value => has = value);
        presenter.Present(first.Publication);first.ReleaseResult();first.ReleasePublication();
        var expiredPublication=expired.Publication;expired.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>ToolResultViews.FromSnapshot(expired.Snapshot,()=>true));
        Assert.Throws<ObjectDisposedException>(()=>presenter.Present(new BorrowFailure(expiredPublication)));
        Assert.Empty(rows); Assert.False(has); Assert.Equal(1, released);
    });

    [Fact]
    public void Empty_schema_and_reentrant_clear_cannot_republish_retired_rows() => Sta(() =>
    {
        var rows = new ObservableCollection<FlatResultRow>(); bool has = false;
        using var presenter = new ResultPresentationPresenter(rows, value => has = value);
        SchemaPresent(presenter,Schema()); Assert.Empty(rows); Assert.False(has);
        bool armed = true;
        rows.CollectionChanged += (_, args) =>
        {
            if (armed && args.NewItems?.Count > 0) { armed = false; presenter.Clear(); }
        };
        SchemaPresent(presenter,Schema(new Port("value", DataType.Number)));
        Assert.Empty(rows); Assert.False(has);
        SchemaPresent(presenter,Schema(new Port("value", DataType.Number))); Assert.Equal(2, rows.Count);
    });

    [Fact]
    public void Shared_owner_is_not_disposed_and_stale_current_or_lease_stamp_cannot_supply_more_pages() => Sta(() =>
    {
        var schema=Schema(new Port("items",DataType.NumberArray));
        using var actual=new ResultPublication(schema,new ResultPublicationStamp(Guid.NewGuid(),Identity(),1,schema.Fingerprint),ResultViewState.Success,
            new InputValues(schema.Outputs,new Dictionary<string,object?>{["items"]=new[]{1d,2d,3d}}),new EmptyOwner(),()=>true);
        var publication=new TrackedPublication(actual);var current=true;publication.Current=()=>current;
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{},1);
        presenter.Present(publication);var sequence=rows[1];sequence.IsExpanded=true;var oldMore=rows.Last();
        presenter.Present(publication);Assert.Same(sequence,rows[1]);Assert.Equal(1,publication.Borrows);
        current=false;presenter.RefreshRows();Assert.Empty(rows);Assert.Equal(1,publication.LeaseDisposals);Assert.Equal(0,publication.OwnerDisposals);
        oldMore.IsExpanded=true;Assert.Empty(rows);
        current=true;publication.OverrideStamp=new ResultPublicationStamp(Guid.NewGuid(),actual.Stamp.Invocation,actual.Stamp.SchemaRevision,schema.Fingerprint);
        presenter.Present(publication);Assert.Empty(rows);Assert.Equal(2,publication.Borrows);Assert.Equal(2,publication.LeaseDisposals);
        Assert.Equal(0,publication.OwnerDisposals);
    });

    [Fact]
    public void Reentrant_borrow_keeps_new_publication_and_releases_only_the_abandoned_lease() => Sta(() =>
    {
        var schema=Schema(new Port("number",DataType.Number));
        using var first=new ResultPublication(schema,new ResultPublicationStamp(Guid.NewGuid(),Identity(),1,schema.Fingerprint),ResultViewState.Success,
            new InputValues(schema.Outputs,new Dictionary<string,object?>{["number"]=1d}),new EmptyOwner(),()=>true);
        using var next=new ResultPublication(schema,new ResultPublicationStamp(Guid.NewGuid(),Identity(),1,schema.Fingerprint),ResultViewState.Success,
            new InputValues(schema.Outputs,new Dictionary<string,object?>{["number"]=22d}),new EmptyOwner(),()=>true);
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{});
        var publication=new TrackedPublication(first){AfterBorrow=()=>presenter.Present(next)};
        presenter.Present(publication);Assert.Equal(new[]{"成功","22"},rows.Select(row=>row.DisplayValue));
        Assert.Equal(1,publication.Borrows);Assert.Equal(1,publication.LeaseDisposals);Assert.Equal(0,publication.OwnerDisposals);
        presenter.Clear();Assert.Empty(rows);Assert.True(next.IsCurrent()); // borrowed reader only, not the host owner.
    });

    [Fact]
    public void Declaration_null_invocation_and_new_publication_same_invocation_keep_distinct_diagnostics() => Sta(() =>
    {
        var schema=Schema(new Port("number",DataType.Number));var invocation=Identity();
        using var declaration=ToolResultViews.Declaration(schema,()=>true);
        Assert.Null(declaration.Stamp.Invocation);Assert.Equal(ResultViewState.Declaration,declaration.State);
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{});
        presenter.Present(declaration);Assert.Equal("未运行",rows[0].DisplayValue);
        using var first=new ResultPublication(schema,new ResultPublicationStamp(Guid.NewGuid(),invocation,1,schema.Fingerprint),ResultViewState.Timeout,
            null,new EmptyOwner(),()=>true,new[]{new Diagnostic("timeout","first")});
        using var second=new ResultPublication(schema,new ResultPublicationStamp(Guid.NewGuid(),invocation,1,schema.Fingerprint),ResultViewState.Timeout,
            null,new EmptyOwner(),()=>true,new[]{new Diagnostic("timeout","second")});
        presenter.Present(first);var old=rows[0];presenter.Present(second);
        Assert.NotSame(old,rows[0]);Assert.All(rows,row=>Assert.Equal("超时: second",row.DisplayValue));
    });

    [Fact]
    public void Publication_replacement_during_lazy_page_guard_cannot_append_retired_children() => Sta(() =>
    {
        var schema=Schema(new Port("items",DataType.NumberArray));
        using var first=new ResultPublication(schema,new ResultPublicationStamp(Guid.NewGuid(),Identity(),1,schema.Fingerprint),ResultViewState.Success,
            new InputValues(schema.Outputs,new Dictionary<string,object?>{["items"]=new[]{1d,2d,3d}}),new EmptyOwner(),()=>true);
        using var next=new ResultPublication(schema,new ResultPublicationStamp(Guid.NewGuid(),Identity(),1,schema.Fingerprint),ResultViewState.Success,
            new InputValues(schema.Outputs,new Dictionary<string,object?>{["items"]=new[]{99d}}),new EmptyOwner(),()=>true);
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{},1);
        var publication=new TrackedPublication(first);presenter.Present(publication);var old=rows[1];var checks=0;
        publication.Current=()=>{if(++checks==3)presenter.Present(next);return true;};
        old.IsExpanded=true;
        Assert.Equal(2,rows.Count);Assert.NotSame(old,rows[1]);Assert.Equal("[1 项]",rows[1].DisplayValue);
        rows[1].IsExpanded=true;Assert.Equal("99",rows.Last().DisplayValue);
        Assert.Equal(1,publication.LeaseDisposals);Assert.Equal(0,publication.OwnerDisposals);
    });

    [Fact]
    public void Helper_publication_owners_remain_current_until_the_fixture_finishes_and_then_retire() => Sta(() =>
    {
        var schema=Schema(new Port("number",DataType.Number));var rows=new ObservableCollection<FlatResultRow>();
        using var presenter=new ResultPresentationPresenter(rows,_=>{});
        SchemaPresent(presenter,schema);var declaration=Assert.Single(_helperPublications);
        presenter.Clear();Assert.True(declaration.IsCurrent());
        SnapshotPresent(presenter,DeclaredOutputSnapshot.WithoutValues(schema,1,Identity(),ExecutionStatus.Failed,new[]{new Diagnostic("failure","detail")}));
        var failed=_helperPublications[1];Assert.True(declaration.IsCurrent());Assert.True(failed.IsCurrent());
        Dispose();Assert.False(declaration.IsCurrent());Assert.False(failed.IsCurrent());Assert.Empty(_helperPublications);
        presenter.Clear();Dispose();Assert.Empty(rows);
    });

    private sealed class EmptyOwner:IDisposable{public void Dispose(){}}
    private sealed class TrackedPublication(IResultPublication inner):IResultPublication
    {
        public int Borrows,LeaseDisposals,OwnerDisposals;public Func<bool>? Current;public Action? AfterBorrow;public ResultPublicationStamp? OverrideStamp;
        public ResultPublicationStamp Stamp=>OverrideStamp??inner.Stamp;public ToolSchema Schema=>inner.Schema;public ResultViewState State=>inner.State;
        public IReadOnlyList<Diagnostic> Diagnostics=>inner.Diagnostics;public IReadOnlyDictionary<string,string> DisplayNames=>inner.DisplayNames;
        public IReadOnlySet<string>? DisplayedPorts=>inner.DisplayedPorts;public bool IsCurrent()=>(Current?.Invoke()??true)&&inner.IsCurrent();
        public IResultValueLease Borrow(){Borrows++;var lease=inner.Borrow();AfterBorrow?.Invoke();return new TrackedLease(lease,()=>LeaseDisposals++);}
        public void Dispose(){OwnerDisposals++;inner.Dispose();}
        private sealed class TrackedLease(IResultValueLease inner,Action disposed):IResultValueLease
        {
            private bool _disposed;public ResultPublicationStamp Stamp=>inner.Stamp;public InputValues? Values=>inner.Values;
            public void Dispose(){if(_disposed)return;_disposed=true;inner.Dispose();disposed();}
        }
    }

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
    private sealed class RunFixture(Results result,DeclaredOutputSnapshot snapshot):IDisposable
    {
        internal Results Result{get;}=result;
        internal DeclaredOutputSnapshot Snapshot{get;}=snapshot;
        private IResultPublication? _publication;
        internal IResultPublication Publication=>_publication??=ToolResultViews.FromSnapshot(Snapshot,()=>true);
        internal void ReleaseResult()=>Result.Resources!.Dispose();
        internal void ReleasePublication()=>_publication?.Dispose();
        public void Dispose(){ReleasePublication();ReleaseResult();}
    }
    private readonly List<IResultPublication> _helperPublications=new();
    private void HoldAndPresent(ResultPresentationPresenter presenter,IResultPublication publication,IReadOnlyDictionary<string,ResultBindingInfo>? bindings=null)
    {
        _helperPublications.Add(publication); // register before Present, including its failure path.
        presenter.Present(publication,bindings);
    }
    private void SchemaPresent(ResultPresentationPresenter presenter,ToolSchema schema,IReadOnlyDictionary<string,string>? displayNames=null,
        IReadOnlyDictionary<string,ResultBindingInfo>? bindings=null,IReadOnlySet<string>? displayedPorts=null)
        =>HoldAndPresent(presenter,ToolResultViews.Declaration(schema,()=>true,displayNames,displayedPorts),bindings);
    private void SnapshotPresent(ResultPresentationPresenter presenter,DeclaredOutputSnapshot snapshot)
        =>HoldAndPresent(presenter,ToolResultViews.FromSnapshot(snapshot,()=>true));
    public void Dispose()
    {
        var failures=new List<Exception>();
        foreach(var publication in _helperPublications)try{publication.Dispose();}catch(Exception error){failures.Add(error);}
        _helperPublications.Clear();
        if(failures.Count!=0)throw new AggregateException("结果测试发布owner释放失败。",failures);
    }
    private sealed class BorrowFailure(IResultPublication inner):IResultPublication
    {
        public ResultPublicationStamp Stamp=>inner.Stamp;public ToolSchema Schema=>inner.Schema;public ResultViewState State=>inner.State;
        public IReadOnlyList<Diagnostic> Diagnostics=>inner.Diagnostics;public IReadOnlyDictionary<string,string> DisplayNames=>inner.DisplayNames;
        public IReadOnlySet<string>? DisplayedPorts=>inner.DisplayedPorts;
        public bool IsCurrent()=>true;public IResultValueLease Borrow()=>inner.Borrow();public void Dispose(){}
    }
    private sealed class PoisonImage : IReadOnlyImage, IDisposable
    {
        internal int Accesses; internal int Disposals;
        private bool _presentationPhase;
        private readonly SunEyeVision.Plugin.Runtime.FrozenImageResource _admitted = new(
            new ImageLayout(1, 1, 1, PixelLayout.Gray8), new ImageFrameAddress(Guid.NewGuid(), "presentation-test"),
            new CoordinateFrame("image:input", CoordinateDomain.Image, CoordinateUnit.Pixel), new byte[] { 0 }, new[] { new RegionRun(0, 0, 0) });
        internal void BeginPresentation() { _presentationPhase = true; Accesses = 0; }
        private T Metadata<T>(Func<T> value)
        {
            if (_presentationPhase) { Accesses++; throw new InvalidOperationException("Presentation accessed admitted image metadata"); }
            return value();
        }
        public int Width => Metadata(() => _admitted.Width);
        public int Height => Metadata(() => _admitted.Height);
        public ImageLayout Layout => Metadata(() => _admitted.Layout);
        public ImageFrameAddress Frame => Metadata(() => _admitted.Frame);
        public IReadOnlyRegion EffectiveDomain => Metadata(() => _admitted.EffectiveDomain);
        public byte[] CopyPixels() { Accesses++; throw new InvalidOperationException("No pixel payload access"); }
        public IImagePixelReadLease AcquirePixels() { Accesses++; throw new InvalidOperationException("No pixel payload access"); }
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
