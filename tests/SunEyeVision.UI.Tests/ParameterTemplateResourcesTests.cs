using System;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Fields;
using Xunit;

namespace SunEyeVision.UI.Tests;

/// <summary>
/// 工具贡献参数行模板（原厂合并 ResourceDictionary）的登记语义：
/// 可登记、按键可查、卸载可移除，且登记按逆序查找。
/// </summary>
public sealed class ParameterTemplateResourcesTests
{
    private static void Sta(Action action) => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(action);

    [Fact]
    public void Contributed_dictionary_is_resolvable_and_removed_on_unload() => Sta(() =>
    {
        const string owner = "test.tool.contribution";
        try
        {
            Assert.Null(ParameterTemplateResources.Find("ContributedRow"));

            var dictionary = new ResourceDictionary { ["ContributedRow"] = new DataTemplate() };
            ParameterTemplateResources.Register(owner, dictionary);

            Assert.True(ParameterTemplateResources.IsRegistered("ContributedRow"));
            Assert.NotNull(ParameterTemplateResources.Find("ContributedRow"));
            Assert.Contains(owner, ParameterTemplateResources.Owners);

            Assert.True(ParameterTemplateResources.Unregister(owner));
            Assert.Null(ParameterTemplateResources.Find("ContributedRow"));
            Assert.False(ParameterTemplateResources.IsRegistered("ContributedRow"));
            // 重复注销不是错误，但必须如实返回“没有移除”。
            Assert.False(ParameterTemplateResources.Unregister(owner));
        }
        finally
        {
            ParameterTemplateResources.Unregister(owner);
        }
    });

    [Fact]
    public void Later_registration_wins_when_two_tools_contribute_the_same_key() => Sta(() =>
    {
        const string first = "test.tool.first";
        const string second = "test.tool.second";
        try
        {
            var a = new DataTemplate();
            var b = new DataTemplate();
            ParameterTemplateResources.Register(first, new ResourceDictionary { ["SharedRow"] = a });
            ParameterTemplateResources.Register(second, new ResourceDictionary { ["SharedRow"] = b });
            Assert.Same(b, ParameterTemplateResources.Find("SharedRow"));
        }
        finally
        {
            ParameterTemplateResources.Unregister(first);
            ParameterTemplateResources.Unregister(second);
        }
    });

    [Fact]
    public void Assembly_without_the_convention_resource_reports_no_contribution()
        => Assert.False(ParameterTemplateResources.RegisterFromAssembly(
            typeof(ParameterTemplateResourcesTests).Assembly));
}
