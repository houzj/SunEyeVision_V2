using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;
using Xunit;

namespace SunEyeVision.Execution.Tests;

public sealed class VariableTransportTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Scalar_declaration_accepts_only_single_item_transport(int count, bool valid)
    {
        var type = TypeRef.ScalarOf(ScalarKind.Int64);
        var source = new BindingVariableSource(new VariableReference("solution", "value", type, storage: NumericStorage.Int64));
        using var values = new ExecutionVariableValues("solution", new[] { source },
            new Dictionary<string, string> { ["value"] = "number" },
            new Dictionary<string, object?> { ["value"] = Enumerable.Repeat(7L, count).ToArray() });
        var captured = Assert.Single(values.Capture(new RunIdentity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "node", Guid.NewGuid())));
        if (valid) { Assert.Null(captured.Issue); Assert.Equal(7L, captured.Value); }
        else { Assert.NotNull(captured.Issue); Assert.Null(captured.Value); }
    }

    [Fact]
    public void Explicit_sequence_is_not_collapsed_to_scalar()
    {
        var type = TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64));
        var source = new BindingVariableSource(new VariableReference("solution", "value", type, storage: NumericStorage.Int64));
        using var values = new ExecutionVariableValues("solution", new[] { source },
            new Dictionary<string, string> { ["value"] = "numbers" },
            new Dictionary<string, object?> { ["value"] = new long[] { 7, 8 } });
        var captured = Assert.Single(values.Capture(new RunIdentity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "node", Guid.NewGuid())));
        Assert.Null(captured.Issue);
        Assert.Equal(new object?[] { 7L, 8L }, Assert.IsType<SequenceValue>(captured.Value).ToArray());
    }
}
