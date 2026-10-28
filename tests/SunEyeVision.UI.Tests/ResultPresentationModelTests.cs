using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ResultPresentationModelTests:IDisposable
{
    private static ToolSchema Schema(params Port[] outputs) => new(Array.Empty<Port>(), outputs);

    [Fact]
    public void Declarations_preserve_ports_record_order_storage_and_unknown_sequence_observation()
    {
        var record = TypeRef.RecordOf("record", new[]
        {
            new Port("z", DataType.Integer, Storage: NumericStorage.UInt8),
            new Port("a", TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Text), CountConstraint.Exactly(3)))
        });
        var schema = Schema(new Port("record", record), new Port("scalar", DataType.Boolean));
        var model = Declaration(schema, pageSize: 2);
        Assert.Same(schema, model.Schema); Assert.Null(model.Identity); Assert.Equal(ResultViewState.Declaration, model.State);
        Assert.Equal(new[] { "record", "scalar" }, model.Outputs.Select(node => node.DeclaredPort!.Id));
        var fields = model.Outputs[0].GetChildrenPage(0, 2);
        Assert.Equal(new[] { "z", "a" }, fields.Select(node => node.DeclaredPort!.Id));
        Assert.Equal(NumericStorage.UInt8, fields[0].DeclaredPort!.Storage);
        Assert.Null(fields[1].ChildCount); Assert.Equal(CountConstraint.Exactly(3), fields[1].Declaration.Count);
        Assert.Empty(fields[1].GetChildrenPage(0, 2)); Assert.Null(fields[0].ScalarValue);
    }

    [Fact]
    public void Declaration_addresses_preserve_root_contract_and_all_members_without_sample_identity()
    {
        var record=TypeRef.RecordOf("record",new[]{new Port("name.with/slash",DataType.Text),new Port("count",DataType.Integer,Storage:NumericStorage.UInt8)});
        var root=new Port("output.with/slash",TypeRef.SequenceOf(record));
        var model=Declaration(Schema(root));var output=model.Outputs[0];
        var all=Assert.IsType<ResultPresentationNode>(output.CreateAllMembers());
        Assert.True(all.IsAllProjection);Assert.Equal(2,all.ChildCount);
        var member=all.GetChildrenPage(0,128)[0];
        Assert.Equal(new[]{"name.with/slash"},member.DeclarationAddress!.MemberFieldPath);
        var reference=member.DeclarationAddress.Reference("source","flow");
        Assert.Same(root.Type,reference.Type);Assert.Equal(root.Storage,reference.Storage);
        Assert.Equal(root.Id,reference.PortId);Assert.Equal("flow",reference.ScopeId);
        Assert.IsType<BindingSelection.AllItems>(reference.Selection);Assert.False(member.DeclarationAddress.IsSampleRow);
        Assert.Equal(ResultPresentationValueState.Unavailable,member.ValueState);Assert.Equal(0,member.ChildCount);
        Assert.Null(member.ScalarValue);
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
    public void Declared_zero_one_many_does_not_fabricate_runtime_elements(int declared)
    {
        var type = TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64), CountConstraint.Exactly(declared));
        var node = Assert.Single(Declaration(Schema(new Port("items", type))).Outputs);
        Assert.Null(node.ChildCount); Assert.Empty(node.GetChildrenPage(0, 128));
        Assert.Equal(ResultPresentationValueState.Unavailable, node.ValueState);
    }

    [Fact]
    public void Record_pages_reuse_nodes_and_keys_follow_field_identity_across_reordering()
    {
        var first = TypeRef.RecordOf("r", new[] { new Port("a/f1:b", DataType.Text), new Port("b", DataType.Text) });
        var second = TypeRef.RecordOf("r", first.Fields.Reverse());
        var model = Declaration(Schema(new Port("root", first)), 1);
        var root = Assert.Single(model.Outputs); var child = Assert.Single(root.GetChildrenPage(1, 1));
        child.IsExpanded = true;
        Assert.Same(child, Assert.Single(root.GetChildrenPage(1, 1))); Assert.True(root.GetChildrenPage(1, 1)[0].IsExpanded);
        var reordered = Declaration(Schema(new Port("root", second)), 1).Outputs[0].GetChildrenPage(0, 1)[0];
        Assert.Equal(child.Key, reordered.Key); Assert.False(reordered.IsExpanded);
        Assert.NotEqual(Declaration(Schema(new Port("root/f1:b", DataType.Text))).Outputs[0].Key, child.Key);
    }

    [Fact]
    public void Pages_validate_bounds_and_empty_schema_is_a_valid_declaration()
    {
        Assert.Empty(Declaration(Schema()).Outputs);
        Assert.Throws<ArgumentOutOfRangeException>(() => Declaration(Schema(), 0));
        var type = TypeRef.RecordOf("r", new[] { new Port("field", DataType.Text) });
        var node = Declaration(Schema(new Port("r", type)), 2).Outputs[0];
        Assert.Throws<ArgumentOutOfRangeException>(() => node.GetChildrenPage(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => node.GetChildrenPage(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => node.GetChildrenPage(0, 3));
        Assert.Empty(node.GetChildrenPage(int.MaxValue, 2)); Assert.Empty(node.GetChildrenPage(0, 0));
        Assert.Single(node.GetChildrenPage(0, 2));
    }
    private ResultPresentationModel Declaration(ToolSchema schema,int pageSize=128)=>Published(schema,null,ResultViewState.Declaration,null,pageSize);
    private ResultPresentationModel Published(ToolSchema schema,RunIdentity? identity,ResultViewState state,InputValues? values,int pageSize=128)
    {
        var context=Hold(schema,identity,state,values);
        return ResultPresentationModel.ForPublication(context.Publication,context.Lease,pageSize);
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(131)]
    public void Successful_sequence_values_have_observed_zero_one_many_counts_and_index_keys(int count)
    {
        var type = TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64));
        var schema = Schema(new Port("items", type));
        var values = Values(schema, ("items", new SequenceValue(type, Enumerable.Range(0, count).Select(i => (object?)(long)i))));
        var identity = Run(); var model = Published(schema, identity, ResultViewState.Success, values, 16);
        Assert.Equal(identity, model.Identity); Assert.Equal(ResultViewState.Success, model.State);
        var root = Assert.Single(model.Outputs); Assert.Equal(count, root.ChildCount);
        Assert.Equal(ResultPresentationValueState.Available, root.ValueState);
        for (var offset = 0; offset < count; offset += 16)
        {
            var page = root.GetChildrenPage(offset, 16); Assert.Equal(Math.Min(16, count - offset), page.Count);
            for (var i = 0; i < page.Count; i++)
            {
                Assert.Equal((long)(offset + i), Assert.IsType<long>(page[i].ScalarValue));
                Assert.Equal(offset + i, page[i].ElementIndex); Assert.Null(page[i].DeclaredPort);
                Assert.Equal(root.Key + "/i" + (offset + i).ToString(System.Globalization.CultureInfo.InvariantCulture), page[i].Key);
            }
        }
        Assert.Empty(root.GetChildrenPage(count, 16));
    }

    [Fact]
    public void Nested_record_sequence_values_follow_declaration_order_and_distinguish_missing_null_empty()
    {
        var record = TypeRef.RecordOf("r", new[] { new Port("z", DataType.Text, false, true), new Port("a", DataType.Text, false, true) });
        var rows = TypeRef.SequenceOf(record);
        var schema = Schema(new Port("rows", rows), new Port("missing", DataType.Text, false, true), new Port("null", DataType.Text, false, true));
        var values = Values(schema, ("rows", new[] { new Dictionary<string, object?> { { "a", "" }, { "z", null } }, new Dictionary<string, object?>() }), ("null", null));
        var model = Published(schema, Run(), ResultViewState.Success, values);
        var children = model.Outputs[0].GetChildrenPage(0, 2);
        var first = children[0].GetChildrenPage(0, 2);
        Assert.Equal(new[] { "z", "a" }, first.Select(node => node.DeclaredPort!.Id));
        Assert.Equal(ResultPresentationValueState.Null, first[0].ValueState);
        Assert.Equal(ResultPresentationValueState.Available, first[1].ValueState); Assert.Equal("", first[1].ScalarValue);
        Assert.All(children[1].GetChildrenPage(0, 2), node => Assert.Equal(ResultPresentationValueState.Missing, node.ValueState));
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
            Assert.Equal(NumericStorage.Float32, model.Outputs[1].DeclaredPort!.Storage);
            Assert.Equal(NumericStorage.UInt8, model.Outputs[2].DeclaredPort!.Storage);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void Large_legacy_double_array_is_paged_without_a_full_copy_and_input_isolation_is_kept()
    {
        const int length = 2_000_000;
        var source = new double[length]; source[length - 1] = 17.25;
        var schema = Schema(new Port("large", DataType.NumberArray)); var values = Values(schema, ("large", source));
        // Construction/freeze is outside the measurement: presentation must not clone that frozen 16 MB array.
        var context=Hold(schema,Run(),ResultViewState.Success,values);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var model = ResultPresentationModel.ForPublication(context.Publication,context.Lease,32);
        var root = model.Outputs[0]; var tail = root.GetChildrenPage(length - 32, 32);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 1024 * 1024, $"Presentation copied beyond its page: {allocated} bytes");
        Assert.Equal(length, root.ChildCount); Assert.Equal(17.25, tail[^1].ScalarValue);
        source[length - 1] = 99; values.Get<double[]>("large")[length - 1] = 88;
        Assert.Equal(17.25, tail[^1].ScalarValue);
        tail[0].IsExpanded = true;
        var overlap = root.GetChildrenPage(length - 31, 31);
        Assert.Same(tail[1], overlap[0]); Assert.True(root.GetChildrenPage(length - 32, 1)[0].IsExpanded);
        Assert.Throws<ArgumentOutOfRangeException>(() => root.GetChildrenPage(0, 33));
    }

    [Fact]
    public void Nested_legacy_double_arrays_are_paged_with_the_same_reader_and_no_whole_array_copy()
    {
        var inner = TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64)); var outer = TypeRef.SequenceOf(inner);
        var schema = Schema(new Port("nested", outer)); var large = new double[500_000]; large[^1] = 6.25;
        var values = Values(schema, ("nested", new SequenceValue(outer, new object?[] { large })));
        var context=Hold(schema,Run(),ResultViewState.Success,values);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var model = ResultPresentationModel.ForPublication(context.Publication,context.Lease,4);
        var child = model.Outputs[0].GetChildrenPage(0, 1)[0]; var last = child.GetChildrenPage(large.Length - 1, 1)[0];
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 256 * 1024, $"Nested presentation cloned an array: {allocated} bytes");
        Assert.Equal(6.25, last.ScalarValue); Assert.Equal(0, child.ElementIndex); Assert.Equal(large.Length, child.ChildCount);
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
    public void Publication_stamp_and_current_guard_retire_old_lazy_pages_without_retaining_the_owner()
    {
        var schema=Schema(new Port("items",DataType.NumberArray));var stamp=new ResultPublicationStamp(Guid.NewGuid(),Run(),3,schema.Fingerprint);
        using var publication=new ResultPublication(schema,stamp,ResultViewState.Success,Values(schema,("items",new[]{1d,2d,3d})),new EmptyOwner(),()=>true);
        using var lease=publication.Borrow();var current=true;
        var model=ResultPresentationModel.ForPublication(publication,lease,1,()=>current);
        Assert.Equal(stamp,model.Stamp);var root=Assert.Single(model.Outputs);var child=Assert.Single(root.GetChildrenPage(0,1));Assert.Equal(1d,child.ScalarValue);
        current=false;Assert.Null(root.ChildCount);Assert.Empty(root.GetChildrenPage(1,1));Assert.Null(child.ScalarValue);
    }

    [Fact]
    public void Blob_style_mixed_records_page_nested_scalars_and_geometry_without_reading_resource_leaves()
    {
        const int count=8193;
        var frame=new CoordinateFrame("image:mixed",CoordinateDomain.Image,CoordinateUnit.Pixel);
        var region=new BorrowedRegion(frame);var regionType=TypeRef.ResourceOf(ResourceKind.Region);
        var pointType=TypeRef.GeometryOf(GeometryKind.Point,frame);
        var part=TypeRef.RecordOf("part",new[]{new Port("number",DataType.Integer),new Port("region",regionType),new Port("point",pointType)});
        var parts=TypeRef.SequenceOf(part);
        var bounds=TypeRef.RecordOf("bounds",new[]{new Port("x",DataType.Number),new Port("y",DataType.Number)});
        var blob=TypeRef.RecordOf("blob",new[]{new Port("element-id",DataType.Integer),new Port("area",DataType.Number),new Port("region",regionType),new Port("bounds",bounds),new Port("parts",parts)});
        var blobs=TypeRef.SequenceOf(blob);var schema=Schema(new Port("blobs",blobs));
        var sharedParts=new SequenceValue(parts,new object?[]{new Dictionary<string,object?>{["number"]=7L,["region"]=region,["point"]=new PointGeometry(frame,new(3,4))}});
        var values=Values(schema,("blobs",new SequenceValue(blobs,Enumerable.Range(0,count).Select(index=>(object?)new Dictionary<string,object?>{
            ["element-id"]=(long)index,["area"]=(double)index,["region"]=region,
            ["bounds"]=new Dictionary<string,object?>{["x"]=1.5d,["y"]=2.5d},["parts"]=sharedParts}))));
        using var publication=new ResultPublication(schema,new(Guid.NewGuid(),Run(),1,schema.Fingerprint),ResultViewState.Success,values,new EmptyOwner(),()=>true);
        using var lease=publication.Borrow();region.BeginPresentation();
        var before=GC.GetAllocatedBytesForCurrentThread();
        var model=ResultPresentationModel.ForPublication(publication,lease);
        var root=Assert.Single(model.Outputs);Assert.Equal(ResultPresentationValueState.Available,root.ValueState);Assert.Equal(count,root.ChildCount);
        var first=root.GetChildrenPage(0,128);Assert.Equal(128,first.Count);
        var fields=first[127].GetChildrenPage(0,5);Assert.Equal(127L,fields[0].ScalarValue);Assert.Equal(127d,fields[1].ScalarValue);
        Assert.Equal(ResultPresentationValueState.ResourcePending,fields[2].ValueState);Assert.Null(fields[2].ScalarValue);Assert.Empty(fields[2].GetChildrenPage(0,1));
        Assert.Equal(1.5d,fields[3].GetChildrenPage(0,2)[0].ScalarValue);
        var nested=fields[4].GetChildrenPage(0,1)[0].GetChildrenPage(0,3);Assert.Equal(7L,nested[0].ScalarValue);
        Assert.Equal(ResultPresentationValueState.ResourcePending,nested[1].ValueState);Assert.Equal(new Point2(3,4),Assert.IsType<PointGeometry>(nested[2].GeometryValue).Point);
        var tail=Assert.Single(root.GetChildrenPage(count-1,128));Assert.Equal((double)(count-1),tail.GetChildrenPage(0,2)[1].ScalarValue);
        Assert.Same(first[127],Assert.Single(root.GetChildrenPage(127,1)));Assert.Equal(0,region.Accesses);
        Assert.True(GC.GetAllocatedBytesForCurrentThread()-before<2*1024*1024,"Mixed records copied beyond the requested pages.");
        Assert.Throws<ArgumentOutOfRangeException>(()=>root.GetChildrenPage(0,129));
        lease.Dispose();Assert.True(publication.IsCurrent());Assert.Null(root.ChildCount);Assert.Empty(root.GetChildrenPage(128,128));Assert.Null(fields[1].ScalarValue);Assert.Null(nested[2].GeometryValue);
        Assert.Equal(0,region.Accesses);Assert.Equal(0,region.Disposals);
    }

    [Fact]
    public void Closing_publication_invalidates_cached_mixed_pages_even_while_the_lease_keeps_owner_alive()
    {
        var region=new BorrowedRegion(new("image:closed",CoordinateDomain.Image,CoordinateUnit.Pixel));
        var record=TypeRef.RecordOf("mixed",new[]{new Port("area",DataType.Number),new Port("region",TypeRef.ResourceOf(ResourceKind.Region))});
        var schema=Schema(new Port("mixed",record));var released=0;
        using var publication=new ResultPublication(schema,new(Guid.NewGuid(),Run(),1,schema.Fingerprint),ResultViewState.Success,
            Values(schema,("mixed",new Dictionary<string,object?>{["area"]=16d,["region"]=region})),new CountOwner(()=>released++),()=>true);
        using var lease=publication.Borrow();region.BeginPresentation();
        var model=ResultPresentationModel.ForPublication(publication,lease);var root=Assert.Single(model.Outputs);var area=root.GetChildrenPage(0,2)[0];Assert.Equal(16d,area.ScalarValue);
        publication.Dispose();Assert.Equal(0,released);Assert.Null(root.ChildCount);Assert.Empty(root.GetChildrenPage(0,2));Assert.Null(area.ScalarValue);
        lease.Dispose();Assert.Equal(1,released);Assert.Equal(0,region.Accesses);Assert.Equal(0,region.Disposals);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (ResultPresentationModel Model,ResultPresentationNode Area,WeakReference Region,WeakReference Publication,WeakReference Lease) ClosedMixedModel()
    {
        var region=new BorrowedRegion(new("image:weak",CoordinateDomain.Image,CoordinateUnit.Pixel));
        var record=TypeRef.RecordOf("weak-mixed",new[]{new Port("area",DataType.Number),new Port("region",TypeRef.ResourceOf(ResourceKind.Region))});
        var sequence=TypeRef.SequenceOf(record);var schema=Schema(new Port("blobs",sequence));
        using var publication=new ResultPublication(schema,new(Guid.NewGuid(),Run(),1,schema.Fingerprint),ResultViewState.Success,
            Values(schema,("blobs",new SequenceValue(sequence,new object?[]{new Dictionary<string,object?>{["area"]=16d,["region"]=region}}))),new EmptyOwner(),()=>true);
        using var lease=publication.Borrow();region.BeginPresentation();var model=ResultPresentationModel.ForPublication(publication,lease);
        var fields=model.Outputs[0].GetChildrenPage(0,1)[0].GetChildrenPage(0,2);Assert.Equal(16d,fields[0].ScalarValue);
        return(model,fields[0],new(region),new(publication),new(lease));
    }
    [Fact]
    public void Cached_mixed_paths_do_not_extend_resource_publication_or_lease_lifetimes()
    {
        var result=ClosedMixedModel();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        Assert.False(result.Region.IsAlive);Assert.False(result.Publication.IsAlive);Assert.False(result.Lease.IsAlive);
        Assert.Null(result.Model.Outputs[0].ChildCount);Assert.Empty(result.Model.Outputs[0].GetChildrenPage(0,128));Assert.Null(result.Area.ScalarValue);
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
        var values = Values(schema, ("direct", image), ("mixed", new Dictionary<string, object?> { { "image", image }, { "number", 7L } }),
            ("images", new SequenceValue(sequence, new object?[] { image })), ("plain", 5L));
        using var publication=new ResultPublication(schema,new(Guid.NewGuid(),Run(),1,schema.Fingerprint),ResultViewState.Success,values,new EmptyOwner(),()=>true);
        using var lease=publication.Borrow();image.BeginPresentation();
        var model=ResultPresentationModel.ForPublication(publication,lease);
        Assert.Equal(7L,model.Outputs[1].GetChildrenPage(0,2)[1].ScalarValue);
        Assert.Equal(5L,model.Outputs[3].ScalarValue);
        return (model,new WeakReference(image));
    }

    [Fact]
    public void Resource_leaves_and_weak_mixed_containers_do_not_retain_borrowed_images_after_close()
    {
        var disposals = BorrowedImage.Disposals;
        var result = ResourceModel();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(result.Image.IsAlive);
        Assert.Equal(ResultPresentationValueState.ResourcePending,result.Model.Outputs[0].ValueState);
        Assert.All(result.Model.Outputs.Skip(1).Take(2),node=>Assert.Equal(ResultPresentationValueState.Available,node.ValueState));
        Assert.Null(result.Model.Outputs[2].ChildCount);
        Assert.All(result.Model.Outputs[1].GetChildrenPage(0, 2), node => Assert.Null(node.ScalarValue));
        Assert.Null(result.Model.Outputs[3].ScalarValue); // closed weak lease never supplies an old value.
        Assert.Equal(disposals, BorrowedImage.Disposals); Assert.False((object)result.Model is IDisposable);
        GC.KeepAlive(result.Model);
    }    [Fact]
    public void Equal_length_outputs_remain_distinct_by_port_identity_and_input_dictionary_order_is_irrelevant()
    {
        var type = TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64));
        var schema = Schema(new Port("left", type), new Port("right", type));
        var values = Values(schema, ("right", new long[] { 9, 8 }), ("left", new long[] { 1, 2 }));
        var model = Published(schema, Run(), ResultViewState.Success, values);
        Assert.Equal(1L, model.Outputs[0].GetChildrenPage(0, 1)[0].ScalarValue);
        Assert.Equal(9L, model.Outputs[1].GetChildrenPage(0, 1)[0].ScalarValue);
        Assert.NotEqual(model.Outputs[0].GetChildrenPage(1, 1)[0].Key, model.Outputs[1].GetChildrenPage(1, 1)[0].Key);
    }}
