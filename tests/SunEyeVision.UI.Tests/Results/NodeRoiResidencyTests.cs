using System.Collections;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;
using Xunit;

namespace SunEyeVision.UI.Tests.Results;

/// <summary>节点级常驻 ROI 的契约测试：常驻投影无会话、字段独占、形状合法。</summary>
public sealed class NodeRoiResidencyTests
{
    private static RoiEditItem RectItem(string name = "搜索区")
        => new(RoiDocumentValue.Inspection, Guid.NewGuid(), name,
            new RecordValue(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
            {
                ["kind"] = "rectangle",
                ["center"] = RoiDocumentValue.Point(10, 10),
                ["width"] = 20d, ["height"] = 20d, ["angle-degrees"] = 0d
            }), true, true);

    [Fact]
    public void Resident_projection_exists_without_session()
    {
        var projection = NativeRoiProjection.CreateResident(RectItem(), 64, 64);
        Assert.Equal(RoiDocumentValue.Inspection, projection.Group);
        Assert.True(projection.Enabled);
        Assert.True(projection.Visible);
        // 无会话时命令不可执行；常驻对象只负责显示，编辑需临时挂载会话。
        Assert.False(projection.DataChangedCommand.CanExecute(null));
    }

    [Fact]
    public void Resident_projection_rejects_invalid_dimensions()
    {
        Assert.Throws<ArgumentException>(() => NativeRoiProjection.CreateResident(RectItem(), 0, 64));
        Assert.Throws<ArgumentException>(() => NativeRoiProjection.CreateResident(RectItem(), 64, -1));
    }

    [Fact]
    public void Resident_projection_keeps_identity_across_two_instances()
    {
        var item = RectItem();
        var first = NativeRoiProjection.CreateResident(item, 64, 64);
        var second = NativeRoiProjection.CreateResident(item, 64, 64);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Group, second.Group);
        // 节点级服务按组与身份复用投影；形状内容不变时身份稳定。
        Assert.Equal(first.Kind, second.Kind);
    }
}
