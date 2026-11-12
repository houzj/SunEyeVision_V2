using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ResultPresentationModelTests:IDisposable
{
    private static ToolSchema Schema(params Port[] outputs) => new(Array.Empty<Port>(), outputs);
    private static ToolSchema Declared(IEnumerable<OutputDisplayNode> display,params Port[] outputs)
        =>new(Array.Empty<Port>(),outputs,outputDisplay:display);

    [Fact]
    public void Declarations_follow_display_order_and_auto_expand_undeclared_records()
    {
        var record = TypeRef.RecordOf("record", new[]
        {
            new Port("z", DataType.Integer, Storage: NumericStorage.UInt8),
            new Port("a", TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Text), CountConstraint.Exactly(3)))
        });
        var schema = Schema(new Port("record", record), new Port("scalar", DataType.Boolean));
        var model = Declaration(schema);
        Assert.Same(schema, model.Schema); Assert.Null(model.Identity); Assert.Equal(ResultViewState.Declaration, model.State);
        Assert.Equal(new[] { "record", "scalar" }, model.Outputs.Select(node => node.DeclaredPort.Id));
        var fields = model.Outputs[0].Children;
        Assert.Equal(new[] { new[] { "z" }, new[] { "a" } }, fields.Select(node => node.MemberPath.ToArray()));
        Assert.Equal(NumericStorage.UInt8, fields[0].PhysicalStorage);
        Assert.Equal(CountConstraint.Exactly(3), fields[1].Declaration.Count);
        Assert.True(model.Outputs[0].IsGroup); Assert.Empty(fields[0].Children);
        Assert.Equal(ResultPresentationValueState.Unavailable, fields[0].ValueState); Assert.Null(fields[0].ScalarValue);
    }

    [Fact]
    public void Result_display_declaration_controls_root_order_before_undeclared_display_items()
    {
        var schema = Declared(new[] { new OutputDisplayNode("second", "第二"), new OutputDisplayNode("first", "第一") },
            new Port("first", DataType.Integer), new Port("second", DataType.Integer), new Port("third", DataType.Integer));
        var model = Declaration(schema);
        Assert.Equal(new[] { "second", "first", "third" }, model.Outputs.Select(node => node.DeclaredPort.Id));
        Assert.Equal(new[] { "第二", "第一", "third" }, model.Outputs.Select(node => node.DisplayName));
    }

    [Fact]
    public void Member_rows_transpose_sequence_members_into_declared_columns()
    {
        var record=TypeRef.RecordOf("point",new[]{new Port("x",DataType.Number,Storage:NumericStorage.Float32),new Port("y",DataType.Number,Storage:NumericStorage.Float32)});
        var type=TypeRef.SequenceOf(record);
        var schema=Declared(new[]
        {
            new OutputDisplayNode("centroid","质心点",new[]{new OutputDisplayNode("x","质心X"),new OutputDisplayNode("y","质心Y")})
        },new Port("centroid",type));
        var values=Values(schema,("centroid",new SequenceValue(type,new object?[]
        {
            new Dictionary<string,object?>{["x"]=1.5d,["y"]=2.5d},
            new Dictionary<string,object?>{["x"]=3.5d,["y"]=4.5d}
        })));
        var model=Published(schema,Run(),ResultViewState.Success,values);
        var group=Assert.Single(model.Outputs);
        Assert.True(group.IsGroup);Assert.Equal("质心点",group.DisplayName);Assert.Equal(2,group.ChildCount);
        var x=group.Children[0];
        Assert.Equal("质心X",x.DisplayName);
        Assert.Equal(new[]{"x"},x.MemberPath.ToArray());Assert.True(x.ThroughSequence);Assert.Equal(2,x.SequenceLength);
        Assert.Equal(1.5d,x.SequenceItem(0));Assert.Equal(3.5d,x.SequenceItem(1));
        Assert.Equal(NumericStorage.Float32,x.PhysicalStorage);
        var address=x.DeclarationAddress;Assert.Same(group.DeclaredPort,address.RootPort);
        var reference=address.Reference("source","flow");
        Assert.Equal(type,reference.Type);Assert.Equal("centroid",reference.PortId);Assert.IsType<BindingSelection.AllItems>(reference.Selection);
        Assert.NotEqual(group.Key,x.Key);
    }

    [Theory]
    [InlineData(ResultViewState.Success)]
    [InlineData(ResultViewState.PartialSuccess)]
    public void Completed_value_states_preserve_their_published_scalar(ResultViewState state)
    {
        var schema=Schema(new Port("number",DataType.Integer));
        var model=Published(schema,Run(),state,Values(schema,("number",42L)));
        Assert.Equal(state,model.State);Assert.Equal(42L,Assert.Single(model.Outputs).ScalarValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(37)]
    public void Declared_sequences_never_fabricate_elements(int declared)
    {
        var type = TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64), CountConstraint.Exactly(declared));
        var node = Assert.Single(Declaration(Schema(new Port("items", type))).Outputs);
        Assert.Null(node.SequenceLength); Assert.Empty(node.Children);
        Assert.Equal(ResultPresentationValueState.Unavailable, node.ValueState);
    }

    [Fact]
    public void Row_keys_follow_port_and_member_identity_across_record_reordering()
    {
        var first = TypeRef.RecordOf("r", new[] { new Port("a/f1:b", DataType.Text), new Port("b", DataType.Text) });
        var second = TypeRef.RecordOf("r", first.Fields.Reverse());
        var model = Declaration(Schema(new Port("root", first)));
        var root = Assert.Single(model.Outputs);
        var child = root.Children.Single(node => node.MemberPath.SequenceEqual(new[] { "b" }));
        child.IsExpanded = true;
        // 行 Key 只跟端口与成员身份走，字段重排不影响已展开行的复用。
        var reordered = Declaration(Schema(new Port("root", second))).Outputs[0]
            .Children.Single(node => node.MemberPath.SequenceEqual(new[] { "b" }));
        Assert.Equal(child.Key, reordered.Key); Assert.False(reordered.IsExpanded);
        Assert.NotEqual(Declaration(Schema(new Port("root/f1:b", DataType.Text))).Outputs[0].Key, child.Key);
    }

    [Fact]
    public void Empty_schema_is_valid_and_undeclared_display_targets_fail_closed()
    {
        Assert.Empty(Declaration(Schema()).Outputs);
        Assert.Throws<ArgumentException>(() => Declared(new[] { new OutputDisplayNode("ghost", "幽灵") }, new Port("real", DataType.Integer)));
        var record = TypeRef.RecordOf("r", new[] { new Port("field", DataType.Text) });
        Assert.Throws<ArgumentException>(() => Declared(new[]
        {
            new OutputDisplayNode("r","记录",new[]{new OutputDisplayNode("ghost","幽灵成员")})
        }, new Port("r", record)));
        Assert.Throws<ArgumentException>(() => new OutputDisplayNode("row","行",memberPath:new[]{"field"}));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(131)]
    public void Successful_scalar_sequences_report_their_length_and_items_without_index_rows(int count)
    {
        var type = TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64));
        var schema = Schema(new Port("items", type));
        var values = Values(schema, ("items", new SequenceValue(type, Enumerable.Range(0, count).Select(i => (object?)(long)i))));
        var identity = Run(); var model = Published(schema, identity, ResultViewState.Success, values);
        Assert.Equal(identity, model.Identity); Assert.Equal(ResultViewState.Success, model.State);
        var root = Assert.Single(model.Outputs);
        Assert.Equal(count, root.SequenceLength); Assert.Empty(root.Children);
        Assert.Equal(ResultPresentationValueState.Available, root.ValueState);
        for (var index = 0; index < count; index++) Assert.Equal((long)index, root.SequenceItem(index));
    }

    [Fact]
    public void Record_sequences_expose_only_their_element_count()
    {
        var record = TypeRef.RecordOf("r", new[] { new Port("z", DataType.Text, false, true) });
        var rows = TypeRef.SequenceOf(record);
        var schema = Schema(new Port("rows", rows), new Port("missing", DataType.Text, false, true), new Port("null", DataType.Text, false, true));
        var values = Values(schema, ("rows", new[] { new Dictionary<string, object?> { ["z"] = null }, new Dictionary<string, object?>() }), ("null", null));
        var model = Published(schema, Run(), ResultViewState.Success, values);
        Assert.Equal(2, model.Outputs[0].SequenceLength); Assert.Empty(model.Outputs[0].Children);
        Assert.Equal(ResultPresentationValueState.Missing, model.Outputs[1].ValueState);
        Assert.Equal(ResultPresentationValueState.Null, model.Outputs[2].ValueState);
    }

    [Fact]
    public void Schema_success_and_failed_runs_never_share_values_or_expansion_state()
    {
        var schema = Schema(new Port("number", DataType.Integer, Storage: NumericStorage.Int64));
        var firstIdentity = Run(); var secondIdentity = Run();
        var first = Published(schema, firstIdentity, ResultViewState.Success, Values(schema, ("number", long.MaxValue)));
        var second = Published(schema, secondIdentity, ResultViewState.Success, Values(schema, ("number", 3L)));
        var failed = Published(schema, Run(), ResultViewState.Failed, Values(schema, ("number", 99L)));
        var notRun = Published(schema, Run(), ResultViewState.NotExecuted, null);
        var declaration = Declaration(schema);
        first.Outputs[0].IsExpanded = true;
        Assert.Equal(long.MaxValue, Assert.IsType<long>(first.Outputs[0].ScalarValue)); Assert.Equal(3L, second.Outputs[0].ScalarValue);
        Assert.Equal(firstIdentity, first.Identity); Assert.Equal(secondIdentity, second.Identity);
        Assert.Equal(first.Outputs[0].Key, second.Outputs[0].Key); Assert.NotSame(first.Outputs[0], second.Outputs[0]);
        Assert.False(second.Outputs[0].IsExpanded); Assert.False(declaration.Outputs[0].IsExpanded);
        Assert.Null(failed.Outputs[0].ScalarValue); Assert.Null(notRun.Outputs[0].ScalarValue); Assert.Null(declaration.Outputs[0].ScalarValue);
        Assert.Equal(ResultPresentationValueState.Unavailable, failed.Outputs[0].ValueState);
        Assert.Equal(ResultViewState.Failed, failed.State); Assert.Equal(ResultViewState.NotExecuted, notRun.State);
    }

    [Fact]
    public void Canonical_scalars_and_storage_are_preserved_without_culture_or_numeric_conversion()
    {
        var prior = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new("fr-FR");
            var schema = Schema(new Port("boolean", DataType.Boolean), new Port("number", DataType.Number, Storage: NumericStorage.Float32),
                new Port("integer", DataType.Integer, Storage: NumericStorage.UInt8), new Port("text", DataType.Text));
            var model = Published(schema, Run(), ResultViewState.Success,
                Values(schema, ("boolean", false), ("number", 1.25d), ("integer", 255L), ("text", "literal <null>")));
            Assert.False(Assert.IsType<bool>(model.Outputs[0].ScalarValue)); Assert.Equal(1.25d, Assert.IsType<double>(model.Outputs[1].ScalarValue));
            Assert.Equal(255L, Assert.IsType<long>(model.Outputs[2].ScalarValue)); Assert.Equal("literal <null>", model.Outputs[3].ScalarValue);
            Assert.Equal(NumericStorage.Float32, model.Outputs[1].DeclaredPort.Storage);
            Assert.Equal(NumericStorage.UInt8, model.Outputs[2].DeclaredPort.Storage);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void Large_legacy_double_array_is_never_copied_into_the_row_model()
    {
        const int length = 2_000_000;
        var source = new double[length]; source[length - 1] = 17.25;
        var schema = Schema(new Port("large", DataType.NumberArray)); var values = Values(schema, ("large", source));
        // Construction/freeze is outside the measurement: presentation must not clone that frozen 16 MB array.
        var context=Hold(schema,Run(),ResultViewState.Success,values);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var model = ResultPresentationModel.ForPublication(context.Publication,context.Lease);
        var root = model.Outputs[0];
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 1024 * 1024, $"Presentation copied the frozen array: {allocated} bytes");
        Assert.Equal(length, root.SequenceLength); Assert.Equal(17.25d, root.SequenceItem(length - 1));
        source[length - 1] = 99; values.Get<double[]>("large")[length - 1] = 88;
        Assert.Equal(17.25d, root.SequenceItem(length - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => root.SequenceItem(length));
    }

    [Fact]
    public void Nested_legacy_double_arrays_stay_single_rows_without_element_nodes()
    {
        var inner = TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64)); var outer = TypeRef.SequenceOf(inner);
        var schema = Schema(new Port("nested", outer)); var large = new double[500_000]; large[^1] = 6.25;
        var values = Values(schema, ("nested", new SequenceValue(outer, new object?[] { large })));
        var context=Hold(schema,Run(),ResultViewState.Success,values);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var model = ResultPresentationModel.ForPublication(context.Publication,context.Lease);
        var root = Assert.Single(model.Outputs);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 256 * 1024, $"Nested presentation cloned an array: {allocated} bytes");
        Assert.Equal(1, root.SequenceLength); Assert.Empty(root.Children);
    }

    [Fact]
    public void Run_values_must_match_the_declared_type_and_success_requires_an_explicit_snapshot()
    {
        var schema = Schema(new Port("value", DataType.Number));
        var wrong = Values(Schema(new Port("value", DataType.Text)), ("value", "wrong"));
        Assert.Throws<ArgumentException>(() => Published(schema, Run(), ResultViewState.Success, wrong));
        Assert.Throws<ArgumentException>(() => Published(schema, Run(), ResultViewState.Success, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => Published(schema, Run(), (ResultViewState)99, null));
        var failed = Published(schema, Run(), ResultViewState.Failed, wrong);
        Assert.Null(failed.Outputs[0].ScalarValue); // Failed runs do not inspect or reuse unrelated supplied values.
    }

    [Fact]
    public void Publication_stamp_and_current_guard_retire_old_rows_without_retaining_the_owner()
    {
        var schema=Schema(new Port("items",DataType.NumberArray));var stamp=new ResultPublicationStamp(Guid.NewGuid(),Run(),3,schema.Fingerprint);
        using var publication=new ResultPublication(schema,stamp,ResultViewState.Success,Values(schema,("items",new[]{1d,2d,3d})),new EmptyOwner(),()=>true);
        using var lease=publication.Borrow();var current=true;
        var model=ResultPresentationModel.ForPublication(publication,lease,()=>current);
        Assert.Equal(stamp,model.Stamp);var root=Assert.Single(model.Outputs);Assert.Equal(1d,root.SequenceItem(0));
        current=false;Assert.Null(root.SequenceLength);Assert.Null(root.ScalarValue);Assert.Null(root.SequenceItem(1));
    }

    [Fact]
    public void Blob_style_member_rows_read_scalars_without_touching_resource_leaves()
    {
        const int count=8193;
        var frame=new CoordinateFrame("image:mixed",CoordinateDomain.Image,CoordinateUnit.Pixel);
        var region=new BorrowedRegion(frame);var regionType=TypeRef.ResourceOf(ResourceKind.Region);
        var bounds=TypeRef.RecordOf("bounds",new[]{new Port("x",DataType.Number),new Port("y",DataType.Number)});
        var blob=TypeRef.RecordOf("blob",new[]{new Port("element-id",DataType.Integer),new Port("area",DataType.Number),new Port("region",regionType),new Port("bounds",bounds)});
        var blobs=TypeRef.SequenceOf(blob);var schema=Declared(new[]
        {
            new OutputDisplayNode("blobs","单体",new[]
            {
                new OutputDisplayNode("element-id","元素标识"),
                new OutputDisplayNode("area","面积"),
                new OutputDisplayNode("region","区域"),
                new OutputDisplayNode("bounds","边界",new[]{new OutputDisplayNode("x","左上X"),new OutputDisplayNode("y","左上Y")})
            })
        },new Port("blobs",blobs));
        var values=Values(schema,("blobs",new SequenceValue(blobs,Enumerable.Range(0,count).Select(index=>(object?)new Dictionary<string,object?>{
            ["element-id"]=(long)index,["area"]=(double)index,["region"]=region,
            ["bounds"]=new Dictionary<string,object?>{["x"]=1.5d,["y"]=2.5d}}))));
        using var publication=new ResultPublication(schema,new(Guid.NewGuid(),Run(),1,schema.Fingerprint),ResultViewState.Success,values,new EmptyOwner(),()=>true);
        using var lease=publication.Borrow();region.BeginPresentation();
        var before=GC.GetAllocatedBytesForCurrentThread();
        var model=ResultPresentationModel.ForPublication(publication,lease);
        var root=Assert.Single(model.Outputs);Assert.Equal(count,root.SequenceLength);
        var members=root.Children;Assert.Equal(4,members.Count);
        Assert.Equal((long)(count-1),members[0].SequenceItem(count-1));
        Assert.Equal((double)(count-1),members[1].SequenceItem(count-1));
        Assert.Equal(ResultPresentationValueState.ResourcePending,members[2].ValueState);Assert.Null(members[2].ScalarValue);
        Assert.Equal(1.5d,members[3].Children[0].SequenceItem(count-1));
        Assert.True(GC.GetAllocatedBytesForCurrentThread()-before<2*1024*1024,"Member rows copied beyond their declared columns.");
        Assert.Equal(0,region.Accesses);
        lease.Dispose();Assert.True(publication.IsCurrent());Assert.Null(members[1].SequenceLength);Assert.Null(members[1].ScalarValue);
        Assert.Equal(0,region.Accesses);Assert.Equal(0,region.Disposals);
    }

    [Fact]
    public void Closing_publication_invalidates_member_rows_even_while_the_lease_keeps_owner_alive()
    {
        var region=new BorrowedRegion(new("image:closed",CoordinateDomain.Image,CoordinateUnit.Pixel));
        var record=TypeRef.RecordOf("mixed",new[]{new Port("area",DataType.Number),new Port("region",TypeRef.ResourceOf(ResourceKind.Region))});
        var schema=Declared(new[]{new OutputDisplayNode("mixed","混合",new[]{new OutputDisplayNode("area","面积")})},new Port("mixed",record));var released=0;
        using var publication=new ResultPublication(schema,new(Guid.NewGuid(),Run(),1,schema.Fingerprint),ResultViewState.Success,
            Values(schema,("mixed",new Dictionary<string,object?>{["area"]=16d,["region"]=region})),new CountOwner(()=>released++),()=>true);
        using var lease=publication.Borrow();region.BeginPresentation();
        var model=ResultPresentationModel.ForPublication(publication,lease);var root=Assert.Single(model.Outputs);var area=root.Children[0];Assert.Equal(16d,area.ScalarValue);
        publication.Dispose();Assert.Equal(0,released);Assert.Null(area.ScalarValue);Assert.Null(area.SequenceLength);
        lease.Dispose();Assert.Equal(1,released);Assert.Equal(0,region.Accesses);Assert.Equal(0,region.Disposals);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (ResultPresentationModel Model,ResultPresentationNode Area,WeakReference Region,WeakReference Publication,WeakReference Lease) ClosedMixedModel()
    {
        var region=new BorrowedRegion(new("image:weak",CoordinateDomain.Image,CoordinateUnit.Pixel));
        var record=TypeRef.RecordOf("weak-mixed",new[]{new Port("area",DataType.Number),new Port("region",TypeRef.ResourceOf(ResourceKind.Region))});
        var sequence=TypeRef.SequenceOf(record);
        var schema=Declared(new[]{new OutputDisplayNode("blobs","弱混合",new[]{new OutputDisplayNode("area","面积")})},new Port("blobs",sequence));
        using var publication=new ResultPublication(schema,new(Guid.NewGuid(),Run(),1,schema.Fingerprint),ResultViewState.Success,
            Values(schema,("blobs",new SequenceValue(sequence,new object?[]{new Dictionary<string,object?>{["area"]=16d,["region"]=region}}))),new EmptyOwner(),()=>true);
        using var lease=publication.Borrow();region.BeginPresentation();var model=ResultPresentationModel.ForPublication(publication,lease);
        var area=model.Outputs[0].Children[0];Assert.Equal(16d,area.SequenceItem(0));
        return(model,area,new(region),new(publication),new(lease));
    }
    [Fact]
    public void Cached_member_rows_do_not_extend_resource_publication_or_lease_lifetimes()
    {
        var result=ClosedMixedModel();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        Assert.False(result.Region.IsAlive);Assert.False(result.Publication.IsAlive);Assert.False(result.Lease.IsAlive);
        Assert.Null(result.Model.Outputs[0].SequenceLength);Assert.Null(result.Area.ScalarValue);Assert.Null(result.Area.SequenceItem(0));
        GC.KeepAlive(result.Model);GC.KeepAlive(result.Area);
    }
    private sealed class CountOwner(Action released):IDisposable{public void Dispose()=>released();}
    private sealed class BorrowedRegion(CoordinateFrame frame):IReadOnlyRegion,IDisposable
    {
        private readonly SunEyeVision.Plugin.Runtime.FrozenRegionResource _region=new(frame,new(Guid.NewGuid(),"mixed-test"),new[]{new RegionRun(0,0,0)});
        private bool _presentation;internal int Accesses,Disposals;
        internal void BeginPresentation(){_presentation=true;Accesses=0;}
        private T Read<T>(Func<T> read){if(_presentation){Accesses++;throw new InvalidOperationException("Presentation touched region metadata or payload.");}return read();}
        public CoordinateFrame Frame=>Read(()=>_region.Frame);public ImageFrameAddress SourceFrame=>Read(()=>_region.SourceFrame);
        public IRegionReadLease AcquireReader()=>Read(()=>_region.AcquireReader());public void Dispose()=>Disposals++;
    }

    private sealed class BorrowedImage : IReadOnlyImage, IDisposable
    {
        public static int Disposals;
        private bool _presentationPhase;
        private readonly SunEyeVision.Plugin.Runtime.FrozenImageResource _admitted = new(
            new ImageLayout(1, 1, 1, PixelLayout.Gray8), new ImageFrameAddress(Guid.NewGuid(), "presentation-test"),
            new CoordinateFrame("image:input", CoordinateDomain.Image, CoordinateUnit.Pixel), new byte[] { 0 }, new[] { new RegionRun(0, 0, 0) });
        internal void BeginPresentation() { _presentationPhase = true;  }
        private T Metadata<T>(Func<T> value)
        {
            if (_presentationPhase) {  throw new InvalidOperationException("Presentation accessed admitted image metadata"); }
            return value();
        }
        public int Width => Metadata(() => _admitted.Width);
        public int Height => Metadata(() => _admitted.Height);
        public ImageLayout Layout => Metadata(() => _admitted.Layout);
        public ImageFrameAddress Frame => Metadata(() => _admitted.Frame);
        public IReadOnlyRegion EffectiveDomain => Metadata(() => _admitted.EffectiveDomain);
        public byte[] CopyPixels() {  throw new InvalidOperationException("No pixel payload access"); }
        public IImagePixelReadLease AcquirePixels() {  throw new InvalidOperationException("No pixel payload access"); }
        public void Dispose() => Interlocked.Increment(ref Disposals);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (ResultPresentationModel Model, WeakReference Image) ResourceModel()
    {
        var image = new BorrowedImage(); var imageType = TypeRef.ResourceOf(ResourceKind.Image);
        var record = TypeRef.RecordOf("mixed", new[] { new Port("image", imageType), new Port("number", DataType.Integer) });
        var sequence = TypeRef.SequenceOf(imageType);
        var schema = Schema(new Port("direct", imageType), new Port("mixed", record), new Port("images", sequence), new Port("plain", DataType.Integer));
        var values = Values(schema, ("direct", image), ("mixed", new Dictionary<string, object?> { ["image"] = image, ["number"] = 7L }),
            ("images", new SequenceValue(sequence, new object?[] { image })), ("plain", 5L));
        using var publication=new ResultPublication(schema,new(Guid.NewGuid(),Run(),1,schema.Fingerprint),ResultViewState.Success,values,new EmptyOwner(),()=>true);
        using var lease=publication.Borrow();image.BeginPresentation();
        var model=ResultPresentationModel.ForPublication(publication,lease);
        Assert.Equal(7L,model.Outputs[1].Children[1].ScalarValue);
        Assert.Equal(5L,model.Outputs[3].ScalarValue);
        return (model,new WeakReference(image));
    }

    [Fact]
    public void Resource_leaves_do_not_retain_borrowed_images_after_close()
    {
        var disposals = BorrowedImage.Disposals;
        var result = ResourceModel();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(result.Image.IsAlive);
        Assert.Equal(ResultPresentationValueState.ResourcePending,result.Model.Outputs[0].ValueState);
        Assert.Equal(ResultPresentationValueState.Available,result.Model.Outputs[1].ValueState);
        Assert.Null(result.Model.Outputs[2].SequenceLength);
        Assert.All(result.Model.Outputs[1].Children, node => Assert.Null(node.ScalarValue));
        Assert.Null(result.Model.Outputs[3].ScalarValue); // closed weak lease never supplies an old value.
        Assert.Equal(disposals, BorrowedImage.Disposals); Assert.False((object)result.Model is IDisposable);
        GC.KeepAlive(result.Model);
    }

    [Fact]
    public void Equal_length_outputs_remain_distinct_by_port_identity_and_input_dictionary_order_is_irrelevant()
    {
        var type = TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64));
        var schema = Schema(new Port("left", type), new Port("right", type));
        var values = Values(schema, ("right", new long[] { 9, 8 }), ("left", new long[] { 1, 2 }));
        var model = Published(schema, Run(), ResultViewState.Success, values);
        Assert.Equal(1L, model.Outputs[0].SequenceItem(0));
        Assert.Equal(9L, model.Outputs[1].SequenceItem(0));
        Assert.NotEqual(model.Outputs[0].Key, model.Outputs[1].Key);
    }

    private ResultPresentationModel Declaration(ToolSchema schema)=>Published(schema,null,ResultViewState.Declaration,null);
    private ResultPresentationModel Published(ToolSchema schema,RunIdentity? identity,ResultViewState state,InputValues? values)
    {
        var context=Hold(schema,identity,state,values);
        return ResultPresentationModel.ForPublication(context.Publication,context.Lease);
    }
    private readonly List<IResultPublication> _publications=new();
    private readonly List<IResultValueLease> _leases=new();
    private (IResultPublication Publication,IResultValueLease Lease) Hold(ToolSchema schema,RunIdentity? identity,ResultViewState state,InputValues? values)
    {
        var publication=new ResultPublication(schema,new ResultPublicationStamp(Guid.NewGuid(),identity,identity is null?null:1,schema.Fingerprint),
            state,state is ResultViewState.Success or ResultViewState.PartialSuccess?values:null,new EmptyOwner(),()=>true);
        var lease=publication.Borrow();_publications.Add(publication);_leases.Add(lease);return(publication,lease);
    }
    public void Dispose(){foreach(var lease in _leases)lease.Dispose();foreach(var publication in _publications)publication.Dispose();}
    private sealed class EmptyOwner:IDisposable{public void Dispose(){}}
    private static RunIdentity Run() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "node", Guid.NewGuid());
    private static InputValues Values(ToolSchema schema, params (string Id, object? Value)[] values) =>
        new(schema.Outputs, values.ToDictionary(pair => pair.Id, pair => pair.Value, StringComparer.Ordinal));
}
