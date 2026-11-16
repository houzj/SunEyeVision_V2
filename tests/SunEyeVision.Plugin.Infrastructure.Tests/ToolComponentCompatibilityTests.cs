using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using Xunit;

namespace SunEyeVision.Plugin.Infrastructure.Tests;

/// <summary>兼容窗口：major 必须相等，minor 落在宿主区间；更旧的 minor 仍可准入，
/// 更新的 minor 必须在准入阶段被拒绝，而不是等到执行时才失败。</summary>
public sealed class ToolComponentCompatibilityTests
{
    private static ToolComponentMetadata Runtime(int major, int minor)
        => new("probe.runtime", "Probe", ToolComponentRole.Runtime, major, minor);

    [Fact]
    public void Current_contract_version_is_admitted()
        => Assert.NotNull(ToolComponentCatalog.Prepare(new[] { Runtime(FoundationContract.Major, FoundationContract.Minor) }));

    [Fact]
    public void Older_minor_inside_window_is_admitted()
        => Assert.NotNull(ToolComponentCatalog.Prepare(new[]
        {
            Runtime(FoundationContract.Major, FoundationContract.OldestCompatibleMinor)
        }));

    [Fact]
    public void Newer_minor_is_rejected_before_admission()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            ToolComponentCatalog.Prepare(new[] { Runtime(FoundationContract.Major, FoundationContract.Minor + 1) }));
        Assert.Contains("outside host window", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Different_major_is_rejected()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            ToolComponentCatalog.Prepare(new[] { Runtime(FoundationContract.Major + 1, FoundationContract.Minor) }));
        Assert.Contains("outside host window", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Assembly_contract_check_uses_the_same_window()
    {
        Assert.True(FoundationContract.IsCompatible(FoundationContract.Major, FoundationContract.OldestCompatibleMinor));
        Assert.False(FoundationContract.IsCompatible(FoundationContract.Major + 1, FoundationContract.Minor));
        Assert.False(FoundationContract.IsCompatible(FoundationContract.Major, FoundationContract.Minor + 1));
    }
}
