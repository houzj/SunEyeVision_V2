using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using Xunit;

namespace SunEyeVision.Variables.Wpf.Tests;

public sealed class VariableArrayDialogTests
{
    [Fact]
    public void Array_window_loads_original_resources_and_pages_a_frozen_snapshot()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var source = Enumerable.Range(1, 45).Select(i => i.ToString()).ToArray();
                var dialog = new VariableArrayDialog("数组", source);
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
            }
            catch (Exception failure) { error = failure; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "数组窗口加载超时。");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
