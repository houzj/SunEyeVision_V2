using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using Xunit;

namespace SunEyeVision.Variables.Wpf.Tests;

[Collection("正式WPF owner")]
public sealed class VariableArrayDialogTests
{
    [Fact]
    public void Array_window_loads_original_resources_and_pages_a_frozen_snapshot()
    {
        SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
        {
                var source = Enumerable.Range(1, 45).Select(i => i.ToString()).ToArray();
                var dialog = new VariableArrayDialog("数组", source) { Left=-32000,Top=-32000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual };
                try
                {
                    dialog.Show();
                    dialog.UpdateLayout();
                    source[0] = "modified after opening";
                    Assert.Equal(45, dialog.ItemsTotalCount);
                    Assert.Equal(40, dialog.BindItems.Count);
                    Assert.Equal("1", dialog.BindItems[0].Value);
                    dialog.CurrentPageNum = 2;
                    Assert.Equal(5, dialog.BindItems.Count);
                    Assert.Equal("41:", dialog.BindItems[0].Index);
                    dialog.UpdateArrayValue(new[] { "new" });
                    Assert.Equal(1, dialog.CurrentPageNum);
                    Assert.Equal("new", Assert.Single(dialog.BindItems).Value);
                }
                finally { dialog.Close(); }
                Assert.Empty(dialog.BindItems);
        }, loadShellResources:false);
    }
}
