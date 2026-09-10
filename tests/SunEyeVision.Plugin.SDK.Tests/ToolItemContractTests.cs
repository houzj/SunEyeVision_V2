using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using Xunit;

using SunEyeVision.Plugin.SDK.Execution.Parameters;
namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 通用工具项模型契约测试。
/// 验证 ToolItem 薄壳数据契约：Value 承载业务枚举、IsSelected 通知、继承类型化封装。
/// （ToolBarControl 的 UI 交互——Toggle/SelectedValue 双向同步——经端到端冒烟验证，见方案 §6 D2）
/// </summary>
public class ToolItemContractTests
{
    [Fact]
    public void ToolItem_Value_CarriesBusinessEnum()
    {
        var item = new ToolItem("矩形", "M2,2...", ShapeType.RotatedRectangle, Key.R);
        Assert.Equal(ShapeType.RotatedRectangle, item.Value);
        Assert.Equal("矩形", item.Name);
        Assert.Equal("矩形 (R)", item.ToolTip);
        Assert.Equal(Key.R, item.ShortcutKey);
    }

    [Fact]
    public void ToolItem_NoShortcut_ToolTipEqualsName()
    {
        var item = new ToolItem("圆", "M...", MeasureGeometryType.Circle);
        Assert.Equal("圆", item.ToolTip);
    }

    [Fact]
    public void ShapeToolItem_InheritsToolItem_ValueIsShapeType()
    {
        var item = new ShapeToolItem(ShapeType.Polygon, "多边形", "M11,1...", Key.P);
        Assert.IsAssignableFrom<ToolItem>(item);
        Assert.Equal(ShapeType.Polygon, item.ShapeType);
        Assert.Equal(ShapeType.Polygon, item.Value);  // Value 承载业务枚举
    }

    [Fact]
    public void MeasureToolItem_InheritsToolItem_ValueIsMeasureGeometryType()
    {
        var item = new MeasureToolItem(MeasureGeometryType.Arc, "圆弧", "M...", Key.A);
        Assert.IsAssignableFrom<ToolItem>(item);
        Assert.Equal(MeasureGeometryType.Arc, item.GeometryType);
        Assert.Equal(MeasureGeometryType.Arc, item.Value);  // Value 承载业务枚举
    }

    [Fact]
    public void ToolItem_IsSelected_NotifiesPropertyChanged()
    {
        var item = new ToolItem("直线", "M3,17 L17,3", ShapeType.Line, Key.L);
        bool notified = false;
        item.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ToolItem.IsSelected)) notified = true;
        };
        item.IsSelected = true;
        Assert.True(notified);
    }

    [Fact]
    public void ToolItem_IsVisible_DefaultTrue_AndNotifies()
    {
        var item = new ToolItem("圆", "M...", ShapeType.Circle, Key.C, isVisible: false);
        Assert.False(item.IsVisible);
    }
}
