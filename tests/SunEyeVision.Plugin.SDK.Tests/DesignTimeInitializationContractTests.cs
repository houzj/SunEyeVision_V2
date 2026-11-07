using System.IO;
using Xunit;
namespace SunEyeVision.Plugin.Editing.Wpf.Tests;
public sealed class DesignTimeInitializationContractTests
{
    [Fact]
    public void Blob_uses_declared_runtime_layout_and_presentation_factory()
    {
        var root = FindRepositoryRoot();
        var tool = Path.Combine(root, "tools", "SunEyeVision.BlobAnalysis");
        var declaration = File.ReadAllText(Path.Combine(tool, "Runtime", "BlobPresentation.cs"));
        Assert.Contains("FieldLayout Layout", declaration);
        Assert.Contains("presentation.graphics.", declaration);
        Assert.Contains("presentation.texts.", declaration);
        Assert.Contains("presentation.judgments.", declaration);
        var factory = File.ReadAllText(Path.Combine(tool, "Wpf", "BlobEditorFactory.cs"));
        Assert.Contains("IToolPresentationFactory", factory);
        Assert.Contains("CreateSettingsView", factory);
        Assert.Contains("CreateResultView", factory);
    }
    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "src", "Plugin.SDK"))) return directory.FullName;
        throw new DirectoryNotFoundException("无法定位 SunEyeVision 仓库根目录。");
    }
}