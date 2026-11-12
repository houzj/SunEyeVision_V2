using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
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
    public void Hidden_declared_output_is_hidden_in_declaration_and_failed_publication() => Sta(() =>
    {
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("status",DataType.Integer)},
            outputDisplay:new[]{new OutputDisplayNode("status","模块状态",displayInResultPanel:false)});
        using var declaration=ToolResultViews.Declaration(schema,()=>true,displayedPorts:new HashSet<string>{"status"});
        Assert.DoesNotContain("status",declaration.DisplayedPorts);
        using var snapshot=DeclaredOutputSnapshot.WithoutValues(schema,0,Identity(),ExecutionStatus.Failed,
            new[]{new Diagnostic("IMAGE_SOURCE_STITCH","拼接失败"){NativeErrorCode=0xE0000502}});
        using var publication=ToolResultViews.FromSnapshot(snapshot,()=>true);
        Assert.DoesNotContain("status",publication.DisplayedPorts);Assert.Single(publication.Schema.Outputs);
        Assert.Equal(0xE0000502u,Assert.Single(publication.Diagnostics).NativeErrorCode);
    });

    [Fact]
    public void Failed_publication_displays_produced_scalars_and_keeps_missing_images_and_failure_status() => Sta(() =>
    {
        var schema=Schema(new Port("sn",DataType.Integer),new Port("image",TypeRef.ResourceOf(ResourceKind.Image)));
        var values=InputValues.CaptureAvailableOutputs(schema.Outputs,new Dictionary<string,object?>{{"sn",25L}});
        var stamp=new ResultPublicationStamp(Guid.NewGuid(),Identity(),1,schema.Fingerprint);
        using var publication=new ResultPublication(schema,stamp,ResultViewState.Failed,values,new EmptyOwner(),()=>true,
            new[]{new Diagnostic("IMAGE_SOURCE_READ","拼接失败")});
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{});
        presenter.Present(publication);
        Assert.Equal("25",rows.Single(row=>row.DisplayName=="sn").DisplayValue);
        Assert.Contains("失败",rows[0].DisplayValue);
        Assert.NotEqual("25",rows.Single(row=>row.DisplayName=="image").DisplayValue);
        using var lease=publication.Borrow();Assert.True(lease.Values!.Read("image").IsMissing);
        Assert.Throws<ArgumentException>(()=>new ResultPublication(schema,stamp with{PublicationId=Guid.NewGuid()},ResultViewState.Success,
            values,new EmptyOwner(),()=>true));
    });

    [Fact]
    public void Result_tree_formats_float32_scalars_and_sequence_items_in_the_current_culture() => Sta(() =>
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var schema = Schema(new Port("module-time", DataType.Number, Storage: NumericStorage.Float32),
                new Port("measurements", DataType.NumberArray, Storage: NumericStorage.Float32));
            const double value = (double)1.2f;
            using var result = Success(schema, new() { ["module-time"] = value, ["measurements"] = new[] { value } });
            var rows = new ObservableCollection<FlatResultRow>();
            using var tree = new ResultPresentationPresenter(rows, _ => { });
            tree.Present(result.Publication);
            Assert.Equal("1,2", rows.Single(row => row.DisplayName == "module-time").DisplayValue);
            // 原件：标量数组在一行内以 ";" 拼接，不展开元素索引行。
            Assert.Equal("1,2", rows.Single(row => row.DisplayName == "measurements").DisplayValue);
            Assert.DoesNotContain(rows, row => row.Level == 2);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    });

    [Fact]
    public void Blob_status_enum_and_polarity_outputs_project_original_texts_in_the_result_tree() => Sta(() =>
    {
        var schema = new ToolSchema(Array.Empty<Port>(), new[]
        {
            new Port("module-status", DataType.Integer),
            new Port("blob-statuses", TypeRef.SequenceOf(DataType.Integer)),
            new Port("threshold-mode", DataType.Text),
            new Port("polarity", DataType.Text)
        }, outputDisplay: new[]
        {
            new OutputDisplayNode("module-status", "模块状态"),
            new OutputDisplayNode("blob-statuses", "单体状态"),
            new OutputDisplayNode("threshold-mode", "阈值方式"),
            new OutputDisplayNode("polarity", "极性")
        });
        using var run = Success(schema, new()
        {
            ["module-status"] = 1L,
            ["blob-statuses"] = new long[] { 0, 1 },
            ["threshold-mode"] = "DoubleThreshold",
            ["polarity"] = "BinaryInv"
        });
        var rows = new ObservableCollection<FlatResultRow>();
        using var tree = new ResultPresentationPresenter(rows, _ => { });
        tree.Present(run.Publication);
        // 原件模块结果树为裸值（用户原件截图：模块状态=1、单体状态=1;1;1…），不做枚举中文化。
        Assert.Equal("1", rows.Single(row => row.DisplayName == "模块状态").DisplayValue);
        // 原件 VmIO.cs:374-385：标量数组一行以 ";" 拼接，不展开 [i] 子节点。
        var statuses = rows.Single(row => row.DisplayName == "单体状态");
        Assert.Equal("0;1", statuses.DisplayValue);
        Assert.DoesNotContain(rows, row => row.Level == 2);
        Assert.Equal("DoubleThreshold", rows.Single(row => row.DisplayName == "阈值方式").DisplayValue);
        Assert.Equal("BinaryInv", rows.Single(row => row.DisplayName == "极性").DisplayValue);
    });

    [Fact]
    public void Semantic_projection_maps_only_declared_blob_status_enum_and_polarity_values()
    {
        Assert.Equal("成功", ResultSemanticText.TryProject("模块状态", 1L));
        Assert.Equal("失败", ResultSemanticText.TryProject("单体状态", 0L));
        Assert.Equal("2", ResultSemanticText.TryProject("模块状态", 2L));
        Assert.Equal("单阈值", ResultSemanticText.TryProject("阈值方式", "SingleThreshold"));
        Assert.Equal("软阈值（相对）", ResultSemanticText.TryProject("阈值方式", "RelativeSoftThreshold"));
        Assert.Equal("亮于背景", ResultSemanticText.TryProject("极性", "Binary"));
        Assert.Equal("暗于背景", ResultSemanticText.TryProject("极性", "BinaryInv"));
        Assert.Null(ResultSemanticText.TryProject("极性", "Unknown"));
        Assert.Null(ResultSemanticText.TryProject("面积", 1L));
        Assert.Equal("1.2", ResultSemanticText.TryProjectFloat32(NumericStorage.Float32, (double)1.2f));
        Assert.Equal("0.4195392", ResultSemanticText.TryProjectFloat32(NumericStorage.Float32, 0.41953921d));
        Assert.Null(ResultSemanticText.TryProjectFloat32(NumericStorage.Float64, 1.2d));
        Assert.Null(ResultSemanticText.TryProjectFloat32(null, 1.2d));
    }

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
    public void Paging_appends_the_same_publication_and_keeps_the_selected_row() => Sta(() =>
    {
        var rowType=TypeRef.RecordOf("test.result-row",new[]{new Port("id",DataType.Integer)});
        var rowsType=TypeRef.SequenceOf(rowType);
        var schema=Schema(new Port("rows",rowsType));
        var projection=new ResultProjectionDescriptor(ResultProjectionKind.StandardTable,ResultCardinality.Many,
            new[]{new ResultColumnDescriptor("id","编号",new("rows",new[]{"id"}))},new("rows"),"id");
        SequenceValue Rows(int count)=>new(rowsType,Enumerable.Range(0,count)
            .Select(index=>new RecordValue(rowType,new Dictionary<string,object?>{["id"]=(long)index})));
        using var first=Success(schema,new(){["rows"]=Rows(501)});
        using var next=Success(schema,new(){["rows"]=Rows(1)});
        var tracked=new TrackedPublication(first.Publication);
        using var table=new DeclaredResultTable(schema,projection);
        var grid=Assert.IsType<DataGrid>(table.Content);
        table.Present(tracked);
        Assert.Equal(500,grid.Items.Count);
        grid.SelectedIndex=1;
        var selected=grid.SelectedItem;
        Assert.NotNull(selected);
        var page=table.CurResultScrollCommand;
        page.Execute(401d);
        Assert.Equal(501,grid.Items.Count);
        Assert.Same(selected,grid.SelectedItem);
        Assert.Equal(1,tracked.Borrows);
        Assert.Equal(1,tracked.LeaseDisposals);
        table.Present(next.Publication);
        page.Execute(401d);
        Assert.Single(grid.Items.Cast<object>());
        Assert.Null(grid.SelectedItem);
    });

    [Fact]
    public void Current_result_column_resets_width_when_the_longest_value_shrinks_by_more_than_fifty() => Sta(() =>
    {
        var schema=Schema(new Port("text",DataType.Text));
        var projection=new ResultProjectionDescriptor(ResultProjectionKind.StandardTable,ResultCardinality.One,
            new[]{new ResultColumnDescriptor("text","文本",new("text"))});
        using var first=Success(schema,new(){["text"]=new string('a',102)});
        using var second=Success(schema,new(){["text"]=new string('b',52)});
        using var third=Success(schema,new(){["text"]="c"});
        using var table=new DeclaredResultTable(schema,projection);
        var column=Assert.Single(Assert.IsType<DataGrid>(table.Content).Columns);
        table.Present(first.Publication);
        column.Width=new DataGridLength(300);
        table.Present(second.Publication);
        Assert.Equal(new DataGridLength(300),column.Width);
        table.Present(third.Publication);
        Assert.Equal(DataGridLengthUnitType.Auto,column.Width.UnitType);
    });

    [Fact]
    public void Highlight_borrows_only_the_selected_row_and_rejects_replaced_publications() => Sta(() =>
    {
        var coordinates=new CoordinateFrame("image:selection",CoordinateDomain.Image,CoordinateUnit.Pixel);
        var frame=new ImageFrameAddress(Guid.NewGuid(),"selection-source");
        using var resources=new FrozenOutputResources();
        var region=resources.CreateRegion(coordinates,frame,new[]{new RegionRun(0,0,0)});
        var rowType=TypeRef.RecordOf("test.highlight-row",new[]
        {
            new Port("id",DataType.Integer),
            new Port("point",TypeRef.GeometryOf(GeometryKind.Point,coordinates)),
            new Port("origin",TypeRef.ResourceOf(ResourceKind.Region))
        });
        var rowsType=TypeRef.SequenceOf(rowType);
        var schema=Schema(new Port("rows",rowsType));
        var projection=new ResultProjectionDescriptor(ResultProjectionKind.StandardTable,ResultCardinality.Many,
            new[]{new ResultColumnDescriptor("id","编号",new("rows",new[]{"id"}),Geometry:new("rows",new[]{"point"}),
                GeometrySource:new("rows",new[]{"origin"}))},new("rows"),"id");
        Dictionary<string,object?> Rows(params long[] ids)=>new()
        {
            ["rows"]=new SequenceValue(rowsType,ids.Select(id=>new RecordValue(rowType,new Dictionary<string,object?>
            {
                ["id"]=id,["point"]=new PointGeometry(coordinates,new(id,0)),["origin"]=region
            })))
        };
        using var first=Success(schema,Rows(10,20));
        using var next=Success(schema,Rows(30));
        var tracked=new TrackedPublication(first.Publication);
        using var table=new DeclaredResultTable(schema,projection);
        var grid=Assert.IsType<DataGrid>(table.Content);
        var changes=new List<ResultSelection>();
        table.SelectionHighlightChanged+=(_,selection)=>changes.Add(selection);
        table.Present(tracked);
        Assert.Equal(1,tracked.Borrows);
        Assert.Equal(1,tracked.LeaseDisposals);
        Assert.Empty(Assert.Single(changes).Targets);
        grid.SelectedIndex=1;
        Assert.Equal(2,tracked.Borrows);
        Assert.Equal(2,tracked.LeaseDisposals);
        Assert.Equal(20L,changes[^1].ElementIdentity);
        Assert.Equal(frame,Assert.Single(changes[^1].Targets).ImageFrame);

        changes.Clear();
        tracked.AfterBorrow=()=>table.Present(next.Publication);
        grid.SelectedIndex=0;
        Assert.Equal(3,tracked.LeaseDisposals);
        Assert.Null(grid.SelectedItem);
        var clear=Assert.Single(changes);
        Assert.Equal(next.Publication.Stamp,clear.Publication);
        Assert.Empty(clear.Targets);
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
        // 原件几何值是单行摘要，不生成顶点索引子节点，也不允许整批枚举。
        Assert.False(rows[1].IsGroup);rows[1].IsExpanded=true;
        Assert.Equal(2,rows.Count);
        using var reader=run.Snapshot.AcquireReader();
        Assert.Same(geometry,reader.Values!.Read("polygon").GetGeometry());
        Assert.Throws<InvalidOperationException>(()=>new InputValues(new[]{new Port("n",DataType.Number)},new Dictionary<string,object?>{["n"]=1d}).Read("n").GetGeometry());
    });
    [Fact]
    public void Display_flags_come_from_the_tool_declaration_not_the_publication()
    {
        var schema=new ToolSchema(Array.Empty<Port>(),
            new[]{new Port("visible",DataType.Number),new Port("hidden",DataType.Number)},
            outputDisplay:new[]{new OutputDisplayNode("visible","可见"),new OutputDisplayNode("hidden","隐藏",displayInResultPanel:false)});
        var rows=new ObservableCollection<FlatResultRow>();bool has=false;
        using var presenter=new ResultPresentationPresenter(rows,value=>has=value);
        SchemaPresent(presenter,schema);
        Assert.True(has);Assert.Equal(new[]{"结果","可见"},rows.Select(row=>row.DisplayName));
        // 发布端的显示端口集合只服务表格/属性视图，不能让声明为隐藏的项回到结果树。
        SchemaPresent(presenter,schema,displayedPorts:new HashSet<string>{"hidden"});
        Assert.Equal(new[]{"结果","可见"},rows.Select(row=>row.DisplayName));
        Assert.Equal(2,schema.Outputs.Count);
    }
    private static ToolSchema Schema(params Port[] ports) => new(Array.Empty<Port>(), ports);
    private static RunIdentity Identity() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "source", Guid.NewGuid());

    [Fact]
    public void Existing_grid_receives_declared_order_explicit_root_binding_and_lazy_record_fields() => Sta(() =>
    {
        var record = TypeRef.RecordOf("r", new[] { new Port("z", DataType.Text), new Port("a", DataType.Integer), new Port("last", DataType.Boolean) });
        var schema = Schema(new Port("record", record), new Port("scalar", DataType.Text));
        var rows = new ObservableCollection<FlatResultRow>(); bool has = false;
        using var presenter = new ResultPresentationPresenter(rows, value => has = value);
        var grid = new DataGrid { ItemsSource = rows, IsReadOnly = true, AutoGenerateColumns = false };
        var binding = new ResultBindingInfo(new OutputReference("source","record",record,scopeId:"flow"),"record");
        using var declaration=ToolResultViews.Declaration(schema,()=>true,new Dictionary<string,string>{["record"]="显示名称"});
        presenter.Present(declaration,new Dictionary<string,ResultBindingInfo>{["record"]=binding});
        Assert.Same(rows, grid.ItemsSource); Assert.True(has);
        // 记录端口没有显示声明时按声明字段展开：没有分页行，也没有元素索引行。
        Assert.Equal(new[] { "结果", "显示名称", "z", "a", "last", "scalar" }, rows.Select(row => row.DisplayName));
        Assert.Null(rows[1].BindingInfo); // A record has no variable storage; its declared scalar members do.
        Assert.Null(rows.Last().BindingInfo); Assert.Null(rows[1].SourceNode);
        Assert.True(rows[1].IsExpanded); // VM TreeViewItem style initially expands declared groups.
        Assert.Equal(new[]{"z"},rows[2].BindingInfo!.Reference.MemberFieldPath);
        Assert.Equal(new[]{"a"},rows[3].BindingInfo!.Reference.MemberFieldPath);
        Assert.Equal(new[]{"last"},rows[4].BindingInfo!.Reference.MemberFieldPath);
        var recordRow = rows[1]; var fieldRow = rows[2];
        presenter.Present(declaration);
        Assert.Same(recordRow, rows[1]); Assert.True(recordRow.IsExpanded); Assert.Same(fieldRow, rows[2]);
    });

    [Fact]
    public void Record_sequences_stay_one_row_and_never_expose_sample_or_all_members() => Sta(() =>
    {
        var record=TypeRef.RecordOf("record",new[]{new Port("name",DataType.Text),new Port("image",TypeRef.ResourceOf(ResourceKind.Image))});
        var type=TypeRef.SequenceOf(record);var schema=Schema(new Port("records",type));
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{});
        using var publication=ToolResultViews.Declaration(schema,()=>true);
        var endpoint=new ResultBindingInfo(new OutputReference("source","records",type,scopeId:"flow"),"records");
        presenter.Present(publication,new Dictionary<string,ResultBindingInfo>{{"records",endpoint}});
        Assert.Equal(2,rows.Count);
        var records=rows[1];Assert.False(records.IsGroup);Assert.Equal("未运行",records.DisplayValue);
        Assert.Null(records.BindingInfo);Assert.Empty(records.Children);
        Assert.DoesNotContain(rows,row=>row.DisplayName is "[0]" or "All · 全部元素成员");
        Assert.DoesNotContain(rows,row=>row.DisplayName.EndsWith(" [All]",StringComparison.Ordinal));
        var retained=records;presenter.Clear();Assert.False(presenter.ContainsRow(retained));
    });

    [Fact]
    public void Observed_record_sequences_report_their_count_and_bind_all_items() => Sta(() =>
    {
        var record=TypeRef.RecordOf("record",new[]{new Port("name",DataType.Text)});var type=TypeRef.SequenceOf(record);
        var schema=Schema(new Port("records",type));using var run=Success(schema,new(){["records"]=new[]{new Dictionary<string,object?>{{"name","first"}}}});
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{});
        var endpoint=new ResultBindingInfo(new OutputReference("source","records",type,scopeId:"flow"),"records");
        presenter.Present(run.Publication,new Dictionary<string,ResultBindingInfo>{{"records",endpoint}});
        Assert.Equal(2,rows.Count);Assert.Equal("[1 项]",rows[1].DisplayValue);Assert.False(rows[1].IsGroup);
        // 记录序列本身没有可承载的正式变量类型；绑定终点是它的标量成员行（本方记录端口不声明成员时无绑定行）。
        Assert.Null(rows[1].BindingInfo);
    });

    [Fact]
    public void Large_scalar_sequence_stays_a_single_joined_row_without_copying_the_frozen_array() => Sta(() =>
    {
        var schema = Schema(new Port("items", DataType.NumberArray));
        using var run = Success(schema, new() { ["items"] = new double[2_000_000] });
        var rows = new ObservableCollection<FlatResultRow>();
        using var presenter = new ResultPresentationPresenter(rows, _ => { });
        var before = GC.GetAllocatedBytesForCurrentThread();
        presenter.Present(run.Publication);
        Assert.Equal(2, rows.Count);
        Assert.StartsWith("0;0;0;", rows[1].DisplayValue, StringComparison.Ordinal);
        Assert.EndsWith(";…", rows[1].DisplayValue, StringComparison.Ordinal);
        Assert.Equal(64, rows[1].DisplayValue.Split(';').Length - 1);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1024 * 1024, "Presenting 64 joined values must not clone the 16 MB frozen array.");
        Assert.False(rows[1].IsGroup);
        var port = rows[1]; presenter.RefreshRows(); Assert.Equal(2, rows.Count); Assert.Same(port, rows[1]);
        presenter.Present(run.Publication); Assert.Same(port, rows[1]);
    });

    [Fact]
    public void Nested_record_sequences_stay_single_rows() => Sta(() =>
    {
        var inner = TypeRef.SequenceOf(DataType.Text);
        var record = TypeRef.RecordOf("r", new[] { new Port("values", inner) });
        var outer = TypeRef.SequenceOf(record);
        var schema = Schema(new Port("records", outer));
        using var run = Success(schema, new() { ["records"] = Enumerable.Range(0, 3).Select(_ =>
            new Dictionary<string, object?> { ["values"] = new[] { "a", "b", "c" } }).ToArray() });
        var rows = new ObservableCollection<FlatResultRow>();
        using var presenter = new ResultPresentationPresenter(rows, _ => { });
        presenter.Present(run.Publication);
        Assert.Equal(2, rows.Count); Assert.Equal("[3 项]", rows[1].DisplayValue); Assert.False(rows[1].IsGroup);
        rows[1].IsExpanded = true; Assert.Equal(2, rows.Count);
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
        Assert.Equal(new[] { "成功", "missing", "null", "", "空序列 (0)" }, rows.Select(row => row.DisplayValue));
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
        using var presenter=new ResultPresentationPresenter(rows,_=>{});
        presenter.Present(run.Publication);run.ReleaseResult();
        var root=Assert.Single(presenter.Roots);Assert.Same(rows[0],root);
        var sequence=Assert.Single(root.Children);Assert.Same(rows[1],sequence);
        Assert.False(sequence.IsGroup);Assert.Equal("1;2;3",sequence.DisplayValue);Assert.Empty(sequence.Children);
        sequence.IsExpanded=true;Assert.Equal(2,rows.Count);
        Assert.NotEmpty(sequence.Key);
        SchemaPresent(presenter,Schema(new Port("other",DataType.Boolean)));
        var current=Assert.Single(presenter.Roots);
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
    public void Scalar_formatting_follows_the_current_culture_and_sequences_stay_joined() => Sta(() =>
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
            Assert.Equal(new[] { "成功", "1,25", "9223372036854775807", "true", "3,5" }, rows.Select(row => row.DisplayValue));
            Assert.False(rows.Last().IsGroup);
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
        using var presenter = new ResultPresentationPresenter(rows, _ => { });
        presenter.Present(first.Publication); rows[1].IsExpanded = true;
        first.ReleaseResult(); Assert.Equal(0, released);
        var oldRow = rows[1];
        presenter.Present(first.Publication); Assert.Same(oldRow, rows[1]); Assert.Equal(0, released);
        first.ReleasePublication();presenter.Present(second.Publication); Assert.Equal(1, released); Assert.True(rowsWereEmpty);
        Assert.False(rows[1].IsExpanded); Assert.NotSame(oldRow, rows[1]);
        oldRow.IsExpanded = false; oldRow.IsExpanded = true;
        Assert.Equal(2, rows.Count); Assert.Equal("99", rows[1].DisplayValue);
        SchemaPresent(presenter,Schema(new Port("other", DataType.Text)));
        Assert.Equal(new[] { "结果", "other" }, rows.Select(row => row.DisplayName));
    });

    [Fact]
    public void Historical_snapshot_is_presentable_after_its_admission_is_retired() => Sta(() =>
    {
        var schema = Schema(new Port("value", DataType.Number));
        using var run = Success(schema, new() { ["value"] = 42d });
        run.Snapshot.ExecutionIdentity!.Owner.Retire(); Assert.False(run.Snapshot.ExecutionIdentity.IsCurrent);
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
        Assert.Equal("",rows[1].DisplayValue);Assert.Equal("",rows[2].DisplayValue);
        Assert.All(rows.Where(row=>row.DisplayName=="image"),row=>Assert.Equal("",row.DisplayValue));
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
        using var presenter = new ResultPresentationPresenter(rows, value => has = value);
        presenter.Present(run.Publication); rows[1].IsExpanded = true; var oldRow = rows[1];
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => oldRow.IsExpanded = false));
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
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{});
        presenter.Present(publication);var sequence=rows[1];
        presenter.Present(publication);Assert.Same(sequence,rows[1]);Assert.Equal(1,publication.Borrows);
        current=false;presenter.RefreshRows();Assert.Empty(rows);Assert.Equal(1,publication.LeaseDisposals);Assert.Equal(0,publication.OwnerDisposals);
        sequence.IsExpanded=true;Assert.Empty(rows);
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
    public void Publication_replacement_during_refresh_cannot_append_retired_rows() => Sta(() =>
    {
        var schema=Schema(new Port("items",DataType.NumberArray));
        using var first=new ResultPublication(schema,new ResultPublicationStamp(Guid.NewGuid(),Identity(),1,schema.Fingerprint),ResultViewState.Success,
            new InputValues(schema.Outputs,new Dictionary<string,object?>{["items"]=new[]{1d,2d,3d}}),new EmptyOwner(),()=>true);
        using var next=new ResultPublication(schema,new ResultPublicationStamp(Guid.NewGuid(),Identity(),1,schema.Fingerprint),ResultViewState.Success,
            new InputValues(schema.Outputs,new Dictionary<string,object?>{["items"]=new[]{99d}}),new EmptyOwner(),()=>true);
        var rows=new ObservableCollection<FlatResultRow>();using var presenter=new ResultPresentationPresenter(rows,_=>{});
        var publication=new TrackedPublication(first);presenter.Present(publication);var old=rows[1];var checks=0;
        publication.Current=()=>{if(++checks==3)presenter.Present(next);return true;};
        presenter.RefreshRows();
        Assert.Equal(2,rows.Count);Assert.NotSame(old,rows[1]);Assert.Equal("99",rows[1].DisplayValue);
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
        var identity=new ToolExecutionOwner().Begin("source",Guid.NewGuid(),"result-presenter-tests");
        var output=new FrozenTestOutput(values);
        var snapshot=DeclaredOutputSnapshot.Capture(schema,1,identity,"source",
            new(true,output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),new CallbackOwner(release),0);
        return new(snapshot);
    }

    private sealed class FrozenTestOutput(IReadOnlyDictionary<string,object?> values):IToolOutput
    { public IReadOnlyDictionary<string,object?> ToValues()=>values; }
    private sealed class CallbackOwner(Action? release):IDisposable
    {
        private Action? _release=release;
        public void Dispose()=>Interlocked.Exchange(ref _release,null)?.Invoke();
    }
    private sealed class RunFixture(DeclaredOutputSnapshot snapshot):IDisposable
    {
        internal DeclaredOutputSnapshot Snapshot{get;}=snapshot;
        private IResultPublication? _publication;
        internal IResultPublication Publication=>_publication??=ToolResultViews.FromSnapshot(Snapshot,()=>true);
        internal void ReleaseResult()=>Snapshot.Dispose();
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
    private static void Sta(Action action)=>SunEyeVision.TestShared.WpfTestDispatcher.Invoke(action);
}
