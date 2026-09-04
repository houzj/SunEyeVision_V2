using System.Collections.Generic;
using OpenCvSharp;
using SunEyeVision.CvAdapter;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;
using CvImage = SunEyeVision.CvEngine.CvImage;

namespace SunEyeVision.Tool.Subroutine.Tests;

/// <summary>
/// 契约测试：SubroutineResults.GetDisplayImages 必须从 ResolvedOutputValues 收集用户声明的 Image 类型输出，
/// 而非遍历 NodeOutputs（其存内部节点结果对象，永远不可能是 IVisionImage）。
/// 对应验收项 AC-01（图像断链修复）。
/// </summary>
public class SubroutineResultsDisplayImagesContractTests
{
    private static IVisionImage MakeImage()
        => new CvVisionImageAdapter(new CvImage(new Mat(64, 64, MatType.CV_8UC1, Scalar.All(128))));

    [Fact]
    public void GetDisplayImages_FromResolvedOutputValues_ReturnsDeclaredImageOutput()
    {
        var results = new SubroutineResults();
        var image = MakeImage();
        // 用户声明的 Image 输出值在 ResolvedOutputValues
        results.ResolvedOutputValues["out_img"] = image;
        // NodeOutputs 存的是内部节点结果对象（非图像），不应干扰
        results.NodeOutputs["internalNode"] = new GenericToolResults();

        var images = results.GetDisplayImages();

        Assert.True(images.ContainsKey("out_img"));
        Assert.Same(image, images["out_img"]);
        Assert.False(images.ContainsKey("internalNode"));
    }

    [Fact]
    public void GetDisplayImages_OnlyImageType_IgnoresNonImageResolvedValues()
    {
        var results = new SubroutineResults();
        results.ResolvedOutputValues["out_value"] = 42.0;      // 非图像类型，不进入图像字典
        results.ResolvedOutputValues["out_text"] = "hello";

        var images = results.GetDisplayImages();

        Assert.Empty(images);
    }

    [Fact]
    public void GetDisplayImages_EmptyResolved_ReturnsEmpty()
    {
        var results = new SubroutineResults();
        Assert.Empty(results.GetDisplayImages());
    }
}
