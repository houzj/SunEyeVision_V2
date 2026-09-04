using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 契约测试：GenericToolResults 必须把动态 AddResultItem 结果项暴露为输出变量契约（SSOT）。
/// 覆盖：子程序输入端口图像（in_1）解析、全局变量注入解析、未映射类型直接报错（rule-015）。
/// 补齐 SubroutineResults 已有重写、GenericToolResults 缺失导致"子程序内部工具拿不到父图"的对称缺口。
/// </summary>
public class GenericToolResultsContractTests
{
    private static IDictionary<string, ToolResults> MakeResults(GenericToolResults sample)
        => new Dictionary<string, ToolResults> { ["N"] = sample };

    private static IVisionImage MakeImage()
        => new FakeImage();

    /// <summary>契约测试用的最小非空图像实现（不依赖任何引擎 dll，保持 D1 纯净）。</summary>
    private sealed class FakeImage : IVisionImage
    {
        public int Width => 64;
        public int Height => 64;
        public int Channels => 1;
        public bool IsEmpty => false;
        public byte[] GetPixelData() => new byte[64 * 64];
        public IVisionImage Clone() => new FakeImage();
        public void Dispose() { }
    }

    /// <summary>
    /// UT-GTR-01 复现本次 bug：子程序输入端口 in_1 注入图像后，解析器必须能按 in_1 取回图像。
    /// </summary>
    [Fact]
    public void Resolve_SubroutineInputImage_ReturnsImage()
    {
        var image = MakeImage();
        var sample = new GenericToolResults();
        sample.AddResultItem("in_1", image);

        var resolved = OutputVariableResolver.Resolve(MakeResults(sample), "N", "in_1");

        Assert.NotNull(resolved);
        Assert.IsAssignableFrom<IVisionImage>(resolved);
        Assert.False(((IVisionImage)resolved!).IsEmpty);
    }

    /// <summary>
    /// UT-GTR-02 同类潜伏回归：全局变量注入（__GlobalVars__）合成结果按变量名解析取值。
    /// </summary>
    [Fact]
    public void Resolve_GlobalVariableInjection_ReturnsValue()
    {
        var globalSample = new GenericToolResults();
        globalSample.AddResultItem("g_inspectThreshold", 0.85d);

        var resolved = OutputVariableResolver.Resolve(MakeResults(globalSample), "N", "g_inspectThreshold");

        var value = Assert.IsType<double>(resolved);
        Assert.Equal(0.85d, value);
    }

    /// <summary>
    /// UT-GTR-03 rule-015：未映射到 SdkTypeVocabulary 的值类型必须直接报错，禁止静默跳过。
    /// </summary>
    [Fact]
    public void GetOutputVariables_UnmappedType_Throws()
    {
        var sample = new GenericToolResults();
        sample.AddResultItem("unknown", new UnmappedPayload());

        var ex = Assert.Throws<InvalidOperationException>(() => sample.GetOutputVariables());
        Assert.Contains("unknown", ex.Message);
    }

    private sealed class UnmappedPayload
    {
    }
}
