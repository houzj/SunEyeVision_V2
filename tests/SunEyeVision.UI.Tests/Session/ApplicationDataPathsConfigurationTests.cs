using System.IO;
using System.Text;
using SunEyeVision.Infrastructure.Persistence.ApplicationData;
using Xunit;

namespace SunEyeVision.UI.Tests;

/// <summary>数据根配置解析：缺配置走缺省根，坏配置一律显式报错、不回退。</summary>
/// <remarks>本类不读写 AppContext，只在临时目录里建独立 App.ini，仍放独立集合避免与 UI 测试并行争用临时目录。</remarks>
[Collection("App data path configuration")]
public sealed class ApplicationDataPathsConfigurationTests : IDisposable
{
    private readonly string _caseRoot = Path.Combine(Path.GetTempPath(), "suneyevision-paths-" + Guid.NewGuid().ToString("N"));
    private readonly string _defaultRoot;

    public ApplicationDataPathsConfigurationTests()
    {
        Directory.CreateDirectory(_caseRoot);
        _defaultRoot = Path.Combine(_caseRoot, "DefaultRoot");
    }

    public void Dispose()
    {
        if (Directory.Exists(_caseRoot)) Directory.Delete(_caseRoot, true);
    }

    private string WriteConfiguration(string content, bool withBom = false)
    {
        var file = Path.Combine(_caseRoot, "App.ini");
        File.WriteAllText(file, content, new UTF8Encoding(withBom));
        return file;
    }

    private string DataRoot() => Path.Combine(_caseRoot, "DataRoot");

    [Fact]
    public void Missing_file_uses_default_root_and_reports_source()
    {
        var paths = ApplicationDataPaths.OpenConfigured(Path.Combine(_caseRoot, "absent.ini"), _defaultRoot);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(_defaultRoot)), paths.Root);
        Assert.Contains("缺省根", paths.RootSource);
        Assert.True(Directory.Exists(paths.SolutionsDirectory));
        Assert.True(Directory.Exists(paths.LogDirectory));
    }

    [Fact]
    public void Missing_data_section_uses_default_root()
    {
        var file = WriteConfiguration("[Other]\r\nRoot=" + DataRoot() + "\r\n");
        var paths = ApplicationDataPaths.OpenConfigured(file, _defaultRoot);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(_defaultRoot)), paths.Root);
        Assert.Contains("未配置", paths.RootSource);
    }

    [Fact]
    public void Valid_root_with_bom_comments_and_section_case_is_accepted()
    {
        var file = WriteConfiguration(
            "; 产品数据根\r\n# 注释同样忽略\r\n[data]\r\nRoot=" + DataRoot() + "\r\n",
            withBom: true);
        var paths = ApplicationDataPaths.OpenConfigured(file, _defaultRoot);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(DataRoot())), paths.Root);
        Assert.Contains("App.ini", paths.RootSource);
        Assert.True(Directory.Exists(Path.GetDirectoryName(paths.ConfigFile)!));
    }

    [Fact]
    public void Duplicate_root_key_is_rejected()
    {
        var file = WriteConfiguration("[Data]\r\nRoot=" + DataRoot() + "\r\nRoot=" + DataRoot() + "\r\n");
        var error = Assert.Throws<InvalidDataException>(() => ApplicationDataPaths.OpenConfigured(file, _defaultRoot));
        Assert.Contains("重复配置", error.Message);
        Assert.Contains("第 3 行", error.Message);
    }

    [Fact]
    public void Empty_root_value_is_rejected()
    {
        var file = WriteConfiguration("[Data]\r\nRoot=\r\n");
        var error = Assert.Throws<InvalidDataException>(() => ApplicationDataPaths.OpenConfigured(file, _defaultRoot));
        Assert.Contains("为空", error.Message);
    }

    [Fact]
    public void Relative_root_is_rejected()
    {
        var file = WriteConfiguration("[Data]\r\nRoot=relative\\path\r\n");
        var error = Assert.Throws<InvalidDataException>(() => ApplicationDataPaths.OpenConfigured(file, _defaultRoot));
        Assert.Contains("必须是绝对路径", error.Message);
    }

    [Fact]
    public void Missing_volume_is_rejected()
    {
        var existing = DriveInfo.GetDrives().Select(drive => drive.Name.TrimEnd('\\')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = Enumerable.Range('D', 'Z' - 'D' + 1)
            .Select(code => ((char)code).ToString())
            .First(letter => !existing.Contains(letter + ":"));
        var file = WriteConfiguration("[Data]\r\nRoot=" + missing + ":\\SunEyeVisionData\r\n");
        var error = Assert.Throws<InvalidDataException>(() => ApplicationDataPaths.OpenConfigured(file, _defaultRoot));
        Assert.Contains("所在盘符或共享不存在", error.Message);
    }

    [Fact]
    public void Unterminated_section_header_is_rejected()
    {
        var file = WriteConfiguration("[Data\r\nRoot=" + DataRoot() + "\r\n");
        var error = Assert.Throws<InvalidDataException>(() => ApplicationDataPaths.OpenConfigured(file, _defaultRoot));
        Assert.Contains("段名格式无效", error.Message);
        Assert.Contains("第 1 行", error.Message);
    }

    [Fact]
    public void Line_without_key_value_separator_is_rejected()
    {
        var file = WriteConfiguration("[Data]\r\nRoot " + DataRoot() + "\r\n");
        var error = Assert.Throws<InvalidDataException>(() => ApplicationDataPaths.OpenConfigured(file, _defaultRoot));
        Assert.Contains("不是 key=value", error.Message);
    }

    [Fact]
    public void Unreadable_configuration_throws_without_falling_back()
    {
        var file = WriteConfiguration("[Data]\r\nRoot=" + DataRoot() + "\r\n");
        using var exclusive = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
        var error = Assert.Throws<IOException>(() => ApplicationDataPaths.OpenConfigured(file, _defaultRoot));
        Assert.Contains("无法读取程序目录配置", error.Message);
        Assert.False(Directory.Exists(_defaultRoot));
    }

    [Fact]
    public void Configured_root_is_created_and_used()
    {
        var file = WriteConfiguration("[Data]\r\nRoot=" + DataRoot() + "\r\n");
        var paths = ApplicationDataPaths.OpenConfigured(file, _defaultRoot);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(DataRoot())), paths.Root);
        Assert.True(Directory.Exists(paths.Root));
    }

    [Fact]
    public void Other_section_root_key_is_ignored_and_default_root_is_used()
    {
        var file = WriteConfiguration("[Other]\r\nRoot=" + DataRoot() + "\r\n");
        var paths = ApplicationDataPaths.OpenConfigured(file, _defaultRoot);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(_defaultRoot)), paths.Root);
    }
}
