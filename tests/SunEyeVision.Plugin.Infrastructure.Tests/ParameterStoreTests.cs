using SunEyeVision.Plugin.Contracts;
using Xunit;

namespace SunEyeVision.Plugin.Infrastructure.Tests;

public sealed class ParameterStoreTests
{
    private static ToolSchema Schema(params Port[] ports)
    {
        var fields = ports.Select((port, index) => new FieldPresentation(port.Id, port.Id, "运行参数", index, FieldEditorKind.Scalar)
        {
            Layout = new FieldLayout("运行参数", "运行参数", Minimum: 0, Maximum: 100)
        }).ToArray();
        return new ToolSchema(ports, Array.Empty<Port>(), new ToolPresentation(fields));
    }

    [Fact]
    public void Declared_node_roundtrips_written_values()
    {
        var store = new ParameterStore();
        var node = Guid.NewGuid();
        store.Declare(node, Schema(new Port("threshold", DataType.Integer)));

        store.Write(node, "threshold", new ConstantSetting(42L));

        Assert.Equal(42L, Assert.IsType<ConstantSetting>(store.Read(node, "threshold")).Value);
        Assert.Equal(42L, store.Snapshot(node).Get<long>("threshold"));
    }

    [Fact]
    public void Unwritten_field_reads_as_unset_and_stays_out_of_the_snapshot()
    {
        var store = new ParameterStore();
        var node = Guid.NewGuid();
        store.Declare(node, Schema(new Port("threshold", DataType.Integer, Required: false)));

        Assert.IsType<UnsetSetting>(store.Read(node, "threshold"));
        Assert.Empty(store.Snapshot(node).Capture());
    }

    [Fact]
    public void Undeclared_node_is_rejected()
    {
        var store = new ParameterStore();
        Assert.Throws<InvalidOperationException>(() => store.Read(Guid.NewGuid(), "threshold"));
    }

    [Fact]
    public void Undeclared_field_is_rejected()
    {
        var store = new ParameterStore();
        var node = Guid.NewGuid();
        store.Declare(node, Schema(new Port("threshold", DataType.Integer)));
        Assert.Throws<ArgumentException>(() => store.Read(node, "unknown"));
    }

    [Fact]
    public void Unset_cannot_be_stored()
    {
        var store = new ParameterStore();
        var node = Guid.NewGuid();
        store.Declare(node, Schema(new Port("threshold", DataType.Integer, Required: false)));
        Assert.Throws<ArgumentException>(() => store.Write(node, "threshold", new UnsetSetting()));
    }

    [Fact]
    public void Value_above_declared_maximum_is_rejected()
    {
        var store = new ParameterStore();
        var node = Guid.NewGuid();
        store.Declare(node, Schema(new Port("threshold", DataType.Integer)));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Write(node, "threshold", new ConstantSetting(101L)));
    }

    [Fact]
    public void Value_below_declared_minimum_is_rejected()
    {
        var store = new ParameterStore();
        var node = Guid.NewGuid();
        store.Declare(node, Schema(new Port("threshold", DataType.Integer)));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Write(node, "threshold", new ConstantSetting(-1L)));
    }

    [Fact]
    public void Restore_replaces_every_value()
    {
        var store = new ParameterStore();
        var node = Guid.NewGuid();
        var ports = new[] { new Port("threshold", DataType.Integer), new Port("gain", DataType.Integer, Required: false) };
        store.Declare(node, Schema(ports));
        store.Write(node, "threshold", new ConstantSetting(42L));
        store.Write(node, "gain", new ConstantSetting(7L));

        store.Restore(node, new FieldSettings(ports, new Dictionary<string, ParameterSetting>
        {
            ["threshold"] = new ConstantSetting(10L)
        }));

        Assert.Equal(10L, store.Snapshot(node).Get<long>("threshold"));
        Assert.IsType<UnsetSetting>(store.Read(node, "gain"));
    }

    [Fact]
    public void Changed_fires_only_when_the_value_actually_differs()
    {
        var store = new ParameterStore();
        var node = Guid.NewGuid();
        store.Declare(node, Schema(new Port("threshold", DataType.Integer)));
        var seen = new List<string>();
        store.Changed += (_, field) => seen.Add(field);

        store.Write(node, "threshold", new ConstantSetting(42L));
        store.Write(node, "threshold", new ConstantSetting(42L));

        Assert.Single(seen);
    }

    [Fact]
    public void Redeclaring_the_same_field_set_is_idempotent_and_a_different_set_is_rejected()
    {
        var store = new ParameterStore();
        var node = Guid.NewGuid();
        store.Declare(node, Schema(new Port("threshold", DataType.Integer)));
        store.Declare(node, Schema(new Port("threshold", DataType.Integer)));

        Assert.Throws<InvalidOperationException>(() =>
            store.Declare(node, Schema(new Port("other", DataType.Integer))));
    }
}
