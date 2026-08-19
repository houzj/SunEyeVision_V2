using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit.Document;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

/// <summary>
/// AvalonEditTextContainer 增量桥接契约测试（UT-BRIDGE-01）。
/// 守护：编辑器文本增量变化 → 容器 CurrentText 正确更新（对齐 RoslynPad AvalonEditTextContainer 增量模型）。
/// 消除"每次补全/语义 WithText 全量重建"的卡顿来源。
/// </summary>
public class AvalonEditBridgeTests
{
    private static Type LoadContainerType()
    {
        var testAsmDir = Path.GetDirectoryName(typeof(AvalonEditBridgeTests).Assembly.Location)
            ?? throw new InvalidOperationException("无法获取测试程序集路径");
        var debugDir = testAsmDir;
        while (debugDir != null && !File.Exists(Path.Combine(debugDir, "SunEyeVision.Plugin.SDK.dll")))
        {
            debugDir = Path.GetDirectoryName(debugDir);
        }
        if (debugDir == null)
            throw new InvalidOperationException("未找到宿主构建目录 dev/Debug");

        var toolDll = Path.Combine(debugDir, "plugins", "SunEyeVision.Tool.Script", "SunEyeVision.Tool.Script.dll");
        if (!File.Exists(toolDll))
            throw new InvalidOperationException($"工具 DLL 未找到: {toolDll}");
        try { AssemblyLoadContext.Default.LoadFromAssemblyPath(toolDll); }
        catch (FileLoadException) { /* 已加载则忽略 */ }

        var asm = AssemblyLoadContext.Default.Assemblies
            .First(a => a.GetName().Name == "SunEyeVision.Tool.Script");
        return asm.GetType("SunEyeVision.Tool.Script.Views.AvalonEditTextContainer")
            ?? throw new InvalidOperationException("AvalonEditTextContainer 类型未找到");
    }

    /// <summary>
    /// UT-BRIDGE-01a: 初始文本同步 — 构造容器时 CurrentText = 文档当前文本。
    /// </summary>
    [Fact]
    public void Container_Init_CurrentTextEqualsDocumentText()
    {
        var type = LoadContainerType();
        var doc = new TextDocument("var x = 1;");
        var container = Activator.CreateInstance(type, doc)!;
        try
        {
            var currentText = (Microsoft.CodeAnalysis.Text.SourceText)type.GetProperty("CurrentText")!.GetValue(container)!;
            Assert.Equal("var x = 1;", currentText.ToString());
        }
        finally
        {
            (container as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// UT-BRIDGE-01b: 增量同步 — 编辑文档插入文本后，CurrentText 正确反映（含跨行/插入）。
    /// </summary>
    [Fact]
    public void Container_TextChanged_CurrentTextTracksIncrementally()
    {
        var type = LoadContainerType();
        var doc = new TextDocument("var x = 1;");
        var container = Activator.CreateInstance(type, doc)!;
        try
        {
            // 编辑：在偏移 8 处插入 "y = "（"var x = 1;" 中 '1' 之前）
            doc.Insert(8, "y = ");
            var currentText = (Microsoft.CodeAnalysis.Text.SourceText)type.GetProperty("CurrentText")!.GetValue(container)!;
            Assert.Equal("var x = y = 1;", currentText.ToString());
        }
        finally
        {
            (container as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// UT-BRIDGE-01c: 增量同步 — 删除文本后 CurrentText 正确反映。
    /// </summary>
    [Fact]
    public void Container_TextChanged_CurrentTextTracksDeletion()
    {
        var type = LoadContainerType();
        var doc = new TextDocument("var abc = 1;");
        var container = Activator.CreateInstance(type, doc)!;
        try
        {
            // 删除 "abc "（偏移 4，长度 4）
            doc.Remove(4, 4);
            var currentText = (Microsoft.CodeAnalysis.Text.SourceText)type.GetProperty("CurrentText")!.GetValue(container)!;
            Assert.Equal("var = 1;", currentText.ToString());
        }
        finally
        {
            (container as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// UT-BRIDGE-01d: 跨行换行同步 — 插入换行后 CurrentText 正确。
    /// </summary>
    [Fact]
    public void Container_TextChanged_CurrentTextTracksNewline()
    {
        var type = LoadContainerType();
        var doc = new TextDocument("int a;\nint b;");
        var container = Activator.CreateInstance(type, doc)!;
        try
        {
            doc.Insert(6, "\n");
            var currentText = (Microsoft.CodeAnalysis.Text.SourceText)type.GetProperty("CurrentText")!.GetValue(container)!;
            Assert.Equal("int a;\n\nint b;", currentText.ToString());
        }
        finally
        {
            (container as IDisposable)?.Dispose();
        }
    }
}
