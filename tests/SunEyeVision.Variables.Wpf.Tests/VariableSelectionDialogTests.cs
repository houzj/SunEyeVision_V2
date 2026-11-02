using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using Xunit;

namespace SunEyeVision.Variables.Wpf.Tests;

public sealed class VariableSelectionDialogTests
{
    [Fact]
    public void Original_selector_loads_pages_and_returns_selected_identity()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var items = Enumerable.Range(1, 15).Select(i => new VariableSelectionItem(
                    "变量" + i, "分组", i, "", i.ToString(),
                    new VariableReference("solution", "id-" + i, TypeRef.ScalarOf(ScalarKind.Int64)))).ToArray();
                var dialog = new VariableSelectionDialog(items);
                try
                {
                    dialog.Show(); dialog.UpdateLayout();
                    Assert.Equal(10, dialog.BindItems.Count);
                    dialog.CurrentPageNum = 2;
                    Assert.Equal(5, dialog.BindItems.Count);
                    var row = Assert.Single(dialog.BindItems.Where(item => item.Reference.VariableId == "id-12"));
                    var list = Assert.IsType<ListView>(dialog.FindName("globalVarList"));
                    list.SelectedItem = row;
                    typeof(VariableSelectionDialog).GetMethod("Confirm_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(dialog, new object[] { dialog, new RoutedEventArgs() });
                    Assert.True(dialog.IsConfirm);
                    Assert.Equal("solution", dialog.SelectedVarItem?.Reference.SolutionId);
                    Assert.Equal("id-12", dialog.SelectedVarItem?.Reference.VariableId);
                    Assert.Empty(dialog.BindItems);
                }
                finally { if (dialog.IsVisible) dialog.Close(); }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "变量选择窗口加载超时。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
