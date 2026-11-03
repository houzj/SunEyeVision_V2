using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using Xunit;

namespace SunEyeVision.Variables.Wpf.Tests;

[Collection("正式WPF owner")]
public sealed class VariableSelectionDialogTests
{
    [Fact]
    public void Original_selector_loads_pages_and_returns_selected_identity()
    {
        SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
        {
                var items = Enumerable.Range(1, 15).Select(i => new VariableSelectionItem(
                    "变量" + i, "分组", i, "", i.ToString(),
                    new VariableReference("solution", "id-" + i, TypeRef.ScalarOf(ScalarKind.Int64)))).ToArray();
                var original=items[11];
                var dialog = new VariableSelectionDialog(items) { Left=-32000,Top=-32000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual };
                try
                {
                    dialog.Show(); dialog.UpdateLayout();
                    items[11]=original with{Name="外部数组已替换",Reference=new VariableReference("other","forged",TypeRef.ScalarOf(ScalarKind.Int64))};
                    Assert.Equal(10, dialog.BindItems.Count);
                    dialog.CurrentPageNum = 2;
                    Assert.Equal(5, dialog.BindItems.Count);
                    var row = Assert.Single(dialog.BindItems.Where(item => item.Reference.VariableId == "id-12"));
                    Assert.Same(original,row);
                    var list = Assert.IsType<ListView>(dialog.FindName("globalVarList"));
                    list.SelectedItem = row;
                    typeof(VariableSelectionDialog).GetMethod("Confirm_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(dialog, new object[] { dialog, new RoutedEventArgs() });
                    Assert.True(dialog.IsConfirm);Assert.Same(original,dialog.SelectedVarItem);
                    Assert.Equal("solution", dialog.SelectedVarItem?.Reference.SolutionId);
                    Assert.Equal("id-12", dialog.SelectedVarItem?.Reference.VariableId);
                    Assert.Empty(dialog.BindItems);
                }
                finally { if (dialog.IsVisible) dialog.Close(); }
        }, loadShellResources:false);
    }
}
