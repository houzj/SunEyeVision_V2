using SunEyeVision.Tool.BlobAnalysis.Runtime;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

/// <summary>结果页装配的语义基线：图形组、可见性项与文本项的声明内容。</summary>
public sealed class BlobDisplayAssemblyTests
{
    [Fact]
    public void Graphic_groups_keep_the_original_order_and_count()
    {
        Assert.Equal(7, BlobDisplayDeclaration.GraphicGroups.Count);
        Assert.Equal(new[]
        {
            BlobResultDisplay.Graphic,
            BlobResultDisplay.InnerRectangle,
            BlobResultDisplay.Inspection,
            BlobResultDisplay.ReferencePoint,
            BlobResultDisplay.RunPoint,
            BlobResultDisplay.Mask,
            BlobResultDisplay.Annulus
        }, BlobDisplayDeclaration.GraphicGroups);
    }

    [Fact]
    public void Every_graphic_group_resolves_all_five_roles()
    {
        Assert.Equal(new[] { "visible", "ok", "ng", "opacity", "width" }, BlobDisplayDeclaration.GraphicRoles);
        foreach (var group in BlobDisplayDeclaration.GraphicGroups)
        foreach (var role in BlobDisplayDeclaration.GraphicRoles)
        {
            var id = BlobDisplayDeclaration.FieldId(group, role);
            Assert.StartsWith(group + ".", id);
            Assert.EndsWith("." + role, id);
        }
    }

    [Fact]
    public void Visibility_items_cover_text_and_outline_only()
    {
        Assert.Equal(new[] { BlobResultDisplay.Text, BlobResultDisplay.Outline }, BlobDisplayDeclaration.VisibilityGroups);
        Assert.Equal(2, BlobDisplayDeclaration.VisibilityGroups.Count);
    }

    [Fact]
    public void Text_display_resolves_the_seven_declared_roles()
    {
        Assert.Equal(new[] { "content", "ok", "ng", "size", "opacity", "x", "y" }, BlobDisplayDeclaration.TextRoles);
        foreach (var role in BlobDisplayDeclaration.TextRoles)
            Assert.Equal(BlobResultDisplay.Text + "." + role,
                BlobDisplayDeclaration.FieldId(BlobDisplayDeclaration.TextGroup, role));
    }
}
