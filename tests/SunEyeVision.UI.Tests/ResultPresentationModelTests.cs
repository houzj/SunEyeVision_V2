using SunEyeVision.Plugin.Contracts;
using SunEyeVision.UI.Services.Monitoring;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ResultPresentationModelTests
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
        var model = ResultPresentationModel.ForSchema(schema, pageSize: 2);
        Assert.Same(schema, model.Schema); Assert.Null(model.Identity); Assert.Equal(ResultPresentationState.NotRun, model.State);
        Assert.Equal(new[] { "record", "scalar" }, model.Outputs.Select(node => node.DeclaredPort!.Id));
        var fields = model.Outputs[0].GetChildrenPage(0, 2);
        Assert.Equal(new[] { "z", "a" }, fields.Select(node => node.DeclaredPort!.Id));
        Assert.Equal(NumericStorage.UInt8, fields[0].DeclaredPort!.Storage);
        Assert.Null(fields[1].ChildCount); Assert.Equal(CountConstraint.Exactly(3), fields[1].Declaration.Count);
        Assert.Empty(fields[1].GetChildrenPage(0, 2)); Assert.Null(fields[0].ScalarValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(37)]
    public void Declared_zero_one_many_does_not_fabricate_runtime_elements(int declared)
    {
        var type = TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64), CountConstraint.Exactly(declared));
        var node = Assert.Single(ResultPresentationModel.ForSchema(Schema(new Port("items", type))).Outputs);
        Assert.Null(node.ChildCount); Assert.Empty(node.GetChildrenPage(0, 128));
        Assert.Equal(ResultPresentationValueState.Unavailable, node.ValueState);
    }

    [Fact]
    public void Record_pages_reuse_nodes_and_keys_follow_field_identity_across_reordering()
    {
        var first = TypeRef.RecordOf("r", new[] { new Port("a/f1:b", DataType.Text), new Port("b", DataType.Text) });
        var second = TypeRef.RecordOf("r", first.Fields.Reverse());
        var model = ResultPresentationModel.ForSchema(Schema(new Port("root", first)), 1);
        var root = Assert.Single(model.Outputs); var child = Assert.Single(root.GetChildrenPage(1, 1));
        child.IsExpanded = true;
        Assert.Same(child, Assert.Single(root.GetChildrenPage(1, 1))); Assert.True(root.GetChildrenPage(1, 1)[0].IsExpanded);
        var reordered = ResultPresentationModel.ForSchema(Schema(new Port("root", second)), 1).Outputs[0].GetChildrenPage(0, 1)[0];
        Assert.Equal(child.Key, reordered.Key); Assert.False(reordered.IsExpanded);
        Assert.NotEqual(ResultPresentationModel.ForSchema(Schema(new Port("root/f1:b", DataType.Text))).Outputs[0].Key, child.Key);
    }

    [Fact]
    public void Pages_validate_bounds_and_empty_schema_is_a_valid_declaration()
    {
        Assert.Empty(ResultPresentationModel.ForSchema(Schema()).Outputs);
        Assert.Throws<ArgumentOutOfRangeException>(() => ResultPresentationModel.ForSchema(Schema(), 0));
        var type = TypeRef.RecordOf("r", new[] { new Port("field", DataType.Text) });
        var node = ResultPresentationModel.ForSchema(Schema(new Port("r", type)), 2).Outputs[0];
        Assert.Throws<ArgumentOutOfRangeException>(() => node.GetChildrenPage(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => node.GetChildrenPage(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => node.GetChildrenPage(0, 3));
        Assert.Empty(node.GetChildrenPage(int.MaxValue, 2)); Assert.Empty(node.GetChildrenPage(0, 0));
        Assert.Single(node.GetChildrenPage(0, 2));
    }
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
        var identity = Run(); var model = ResultPresentationModel.ForRun(schema, identity, ResultPresentationState.Success, values, 16);
        Assert.Equal(identity, model.Identity); Assert.Equal(ResultPresentationState.Success, model.State);
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
        var model = ResultPresentationModel.ForRun(schema, Run(), ResultPresentationState.Success, values);
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
        var first = ResultPresentationModel.ForRun(schema, firstIdentity, ResultPresentationState.Success, Values(schema, ("number", long.MaxValue)));
        var second = ResultPresentationModel.ForRun(schema, secondIdentity, ResultPresentationState.Success, Values(schema, ("number", 3L)));
        var failed = ResultPresentationModel.ForRun(schema, Run(), ResultPresentationState.Failed, Values(schema, ("number", 99L)));
        var notRun = ResultPresentationModel.ForRun(schema, Run(), ResultPresentationState.NotRun, null);
        var declaration = ResultPresentationModel.ForSchema(schema);
        first.Outputs[0].IsExpanded = true;
        Assert.Equal(long.MaxValue, Assert.IsType<long>(first.Outputs[0].ScalarValue)); Assert.Equal(3L, second.Outputs[0].ScalarValue);
        Assert.Equal(firstIdentity, first.Identity); Assert.Equal(secondIdentity, second.Identity);
        Assert.Equal(first.Outputs[0].Key, second.Outputs[0].Key); Assert.NotSame(first.Outputs[0], second.Outputs[0]);
        Assert.False(second.Outputs[0].IsExpanded); Assert.False(declaration.Outputs[0].IsExpanded);
        Assert.Null(failed.Outputs[0].ScalarValue); Assert.Null(notRun.Outputs[0].ScalarValue); Assert.Null(declaration.Outputs[0].ScalarValue);
        Assert.Equal(ResultPresentationValueState.Unavailable, failed.Outputs[0].ValueState);
        Assert.Equal(ResultPresentationState.Failed, failed.State); Assert.Equal(ResultPresentationState.NotRun, notRun.State);
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
            var model = ResultPresentationModel.ForRun(schema, Run(), ResultPresentationState.Success,
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
        var before = GC.GetAllocatedBytesForCurrentThread();
        var model = ResultPresentationModel.ForRun(schema, Run(), ResultPresentationState.Success, values, 32);
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
        var before = GC.GetAllocatedBytesForCurrentThread();
        var model = ResultPresentationModel.ForRun(schema, Run(), ResultPresentationState.Success, values, 4);
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
        Assert.Throws<ArgumentException>(() => ResultPresentationModel.ForRun(schema, Run(), ResultPresentationState.Success, wrong));
        Assert.Throws<ArgumentNullException>(() => ResultPresentationModel.ForRun(schema, Run(), ResultPresentationState.Success, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResultPresentationModel.ForRun(schema, Run(), (ResultPresentationState)99, null));
        var failed = ResultPresentationModel.ForRun(schema, Run(), ResultPresentationState.Failed, wrong);
        Assert.Null(failed.Outputs[0].ScalarValue); // Failed runs do not inspect or reuse unrelated supplied values.
    }

    private sealed class BorrowedImage : IReadOnlyImage, IDisposable
    {
        public static int Disposals;
        public int Width => throw new Exception("Presentation accessed image metadata");
        public int Height => throw new Exception("Presentation accessed image metadata");
        public byte[] CopyPixels() => throw new Exception("Presentation accessed image pixels");
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
        return (ResultPresentationModel.ForRun(schema, Run(), ResultPresentationState.Success, values), new WeakReference(image));
    }

    [Fact]
    public void Resource_roots_and_containing_aggregates_remain_declarations_without_retaining_borrowed_images()
    {
        var disposals = BorrowedImage.Disposals;
        var result = ResourceModel();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(result.Image.IsAlive);
        Assert.All(result.Model.Outputs.Take(3), node => Assert.Equal(ResultPresentationValueState.ResourcePending, node.ValueState));
        Assert.Null(result.Model.Outputs[2].ChildCount);
        Assert.All(result.Model.Outputs[1].GetChildrenPage(0, 2), node => Assert.Null(node.ScalarValue));
        Assert.Equal(5L, result.Model.Outputs[3].ScalarValue);
        Assert.Equal(disposals, BorrowedImage.Disposals); Assert.False((object)result.Model is IDisposable);
        GC.KeepAlive(result.Model);
    }    [Fact]
    public void Equal_length_outputs_remain_distinct_by_port_identity_and_input_dictionary_order_is_irrelevant()
    {
        var type = TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64));
        var schema = Schema(new Port("left", type), new Port("right", type));
        var values = Values(schema, ("right", new long[] { 9, 8 }), ("left", new long[] { 1, 2 }));
        var model = ResultPresentationModel.ForRun(schema, Run(), ResultPresentationState.Success, values);
        Assert.Equal(1L, model.Outputs[0].GetChildrenPage(0, 1)[0].ScalarValue);
        Assert.Equal(9L, model.Outputs[1].GetChildrenPage(0, 1)[0].ScalarValue);
        Assert.NotEqual(model.Outputs[0].GetChildrenPage(1, 1)[0].Key, model.Outputs[1].GetChildrenPage(1, 1)[0].Key);
    }}
