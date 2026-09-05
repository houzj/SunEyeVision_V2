using System.Linq;
using System.Windows;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// HalconRegionPipeline 契约测试（v1.4 纯 ROI 管线）。
/// 契约（聚焦 v1.4 新增的管线边界，几何细节由 ToolRegionHelper 既有契约 + 真实工具 Run 覆盖）：
///   ① Resolve 只做纯几何（订阅解析 + 位置修正 + 检测−屏蔽差集），不接触图像对象 —— 签名级保证（无 HImage 参数）；
///   ② 空区域 → RoiRegion=null（全图语义，由算法层 Restrict(gray, null) 落地）；
///   ③ RoiScope.Dispose 幂等（防重入）；
///   ④ HalImageDomain.Restrict：无 ROI = 全图 Clone；有 ROI = ReduceDomain（宽高不变、Domain=ROI），图所有权归调用方。
/// </summary>
public class HalconRegionPipelineContractTests
{
    private static RoiData MakeRegion(string name, object geometry, bool enabled = true) => new()
    {
        Name = name,
        Parameters = new ShapeParameters { Geometry = geometry },
        IsEnabled = enabled,
    };

    private static HRegion RegionFromRect(Rect rect)
    {
        HOperatorSet.GenRectangle1(out var region,
            rect.Y, rect.X, rect.Y + rect.Height - 1, rect.X + rect.Width - 1);
        return new HRegion(region);
    }

    [Fact]
    public void NoRegions_ResolveReturnsNullRoi()
    {
        using var scope = HalconRegionPipeline.Resolve(1000, 800, [], [], null, null);

        Assert.Null(scope.RoiRegion);
        Assert.Null(scope.InspectionRegion);
        Assert.Null(scope.MaskRegion);
        Assert.Contains(scope.Timings, t => t.Step == "ROI构建");
    }

    [Fact]
    public void EnabledRegion_YieldsNonEmptyRoi()
    {
        using var scope = HalconRegionPipeline.Resolve(1000, 800,
            new[] { MakeRegion("insp", new Rect(100, 100, 200, 150)) },
            [], null, null);

        Assert.NotNull(scope.RoiRegion);
        Assert.NotNull(scope.InspectionRegion);
    }

    [Fact]
    public void RoiScope_DisposeIsIdempotent()
    {
        var scope = HalconRegionPipeline.Resolve(200, 200,
            new[] { MakeRegion("insp", new Rect(10, 10, 50, 50)) },
            [], null, null);

        scope.Dispose();
        scope.Dispose(); // 第二次释放不抛异常
    }

    [Fact]
    public void Restrict_NoRoi_ClonesFullImage()
    {
        using var gray = new HImage("byte", 200, 150);
        using var restricted = HalImageDomain.Restrict(gray, null);

        HOperatorSet.GetImageSize(restricted, out var w, out var h);
        Assert.Equal(200, w.I);
        Assert.Equal(150, h.I);
    }

    [Fact]
    public void Restrict_WithRoi_ReturnsSameSizeReducedImage()
    {
        using var gray = new HImage("byte", 200, 150);
        using var roi = RegionFromRect(new Rect(50, 50, 60, 40));
        using var restricted = HalImageDomain.Restrict(gray, roi);

        HOperatorSet.GetImageSize(restricted, out var w, out var h);
        Assert.Equal(200, w.I); // ReduceDomain 不改变图像宽高（仅裁剪 Domain）

        HOperatorSet.GetDomain(restricted, out var dom);
        using (dom)
        {
            HOperatorSet.AreaCenter(dom, out var area, out _, out _);
            Assert.InRange(area.D, 60 * 40 - 10, 60 * 40 + 10); // Domain ≈ ROI
        }
    }
}
