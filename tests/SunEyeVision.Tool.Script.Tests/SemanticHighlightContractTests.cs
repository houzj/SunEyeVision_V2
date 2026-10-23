using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

/// <summary>
/// 语义着色"代码红线"契约测试（治本防线）：
/// 守护语义着色必须走"IHighlighter 按行渲染"架构（对齐 RoslynPad RoslynSemanticHighlighter），
/// 禁止回退到"DocumentColorizingTransformer 全量遍历"或"每行 fire-and-forget 自建缓存"。
///
/// 背景：曾用 DocumentColorizingTransformer.ColorizeLine + 自建行缓存，打字/删除闪烁（重绘重算 + 无
/// offsetShift 平移）。已治本为 IHighlighter 架构（AvalonEdit 缓存 HighlightedLine，CompareAge 按行失效，
/// DoHighlightLine 旧 sections + offsetShift 平移防闪烁）。回归旧架构即失败。
/// </summary>
public class SemanticHighlightContractTests
{
    private static string GetSourcePath(string fileName)
    {
        var testAsmDir = Path.GetDirectoryName(typeof(SemanticHighlightContractTests).Assembly.Location)
            ?? throw new InvalidOperationException("无法获取测试程序集路径");
        var dir = new DirectoryInfo(testAsmDir);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CommonControl", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"未找到 {fileName} 源码（请确认工作区结构）");
    }

    /// <summary>
    /// 语义着色必须走 IHighlighter 架构：RoslynSemanticHighlighter 存在（而非旧的 DocumentColorizingTransformer 自建缓存）。
    /// </summary>
    [Fact]
    public void SemanticColoring_UsesIHighlighterArchitecture()
    {
        var source = File.ReadAllText(GetSourcePath("SemanticHighlighter.cs"));

        // IHighlighter 架构核心标志
        Assert.Contains("RoslynSemanticHighlighter : IHighlighter", source);
        Assert.Contains("HighlightLine(int lineNumber)", source);          // IHighlighter.HighlightLine
        Assert.Contains("HighlightingStateChanged", source);                // 按行局部重绘事件
        Assert.Contains("CachedLine", source);                              // 行缓存（对齐 RoslynPad CachedLine）
        Assert.Contains("CacheSize", source);                               // 512 行缓存上限
    }

    /// <summary>
    /// 语义着色必须用 Roslyn 标准分类 Classifier.GetClassifiedSpansAsync 按行 TextSpan（对齐 RoslynPad），
    /// 而非自写 GetSymbolInfo 遍历。禁止 DescendantTokens 全量遍历。
    /// </summary>
    [Fact]
    public void SemanticColoring_UsesClassifierByLineSpan()
    {
        var source = File.ReadAllText(GetSourcePath("SemanticHighlighter.cs"));

        // 用 Classifier 按行 TextSpan（Roslyn 标准语义分类）
        Assert.Contains("Classifier.GetClassifiedSpansAsync", source);
        Assert.Contains("new TextSpan(documentLine.Offset, documentLine.TotalLength)", source);

        // 禁止全量遍历 pattern：DescendantTokens() 无范围参数（遍历全树）不存在
        var fullTraversal = Regex.Matches(source, @"DescendantTokens\s*\(\s*\)");
        Assert.True(fullTraversal.Count == 0,
            $"检测到语义着色全量遍历 {fullTraversal.Count} 处：DescendantTokens() 无范围参数会遍历整棵语法树。");
    }

    /// <summary>
    /// 防闪烁机制：DoHighlightLine 必须复用旧 sections + offsetShift 平移（对齐 RoslynPad）。
    /// </summary>
    [Fact]
    public void SemanticColoring_HasFlickerProtection_OffsetShift()
    {
        var source = File.ReadAllText(GetSourcePath("SemanticHighlighter.cs"));

        Assert.Contains("offsetShift", source);            // 旧 sections 偏移平移
        Assert.Contains("previousHighlight", source);      // 复用旧 HighlightedLine sections
    }
}
