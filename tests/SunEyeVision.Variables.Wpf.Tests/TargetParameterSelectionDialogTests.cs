using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using Xunit;

namespace SunEyeVision.Variables.Wpf.Tests;

[Collection("正式WPF owner")]
public sealed class TargetParameterSelectionDialogTests
{
    private sealed class Row
    {
        public string Name { get; init; } = "流程";
        public bool IsSelected { get; set; }
        public ObservableCollection<Row> Children { get; } = new();
    }
    private sealed class Model
    {
        public ObservableCollection<Row> TreeNodes { get; } = new() { new Row() };
        public ObservableCollection<object> SelectedConsumers { get; } = new();
    }

    [Fact]
    public void Original_target_window_loads_its_tree_and_closes_without_owning_host_data()
    {
        SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
        {
                var model = new Model();
                var dialog = new TargetParameterSelectionDialog { DataContext = model, Left=-32000,Top=-32000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual };
                try
                {
                    dialog.Show(); dialog.UpdateLayout();
                    var tree = Assert.IsType<TargetParameterTree>(dialog.FindName("RunParamsSelectTreeView"));
                    Assert.Same(model.TreeNodes, tree.ItemsSource);
                    var filter = Assert.IsType<ComboBox>(dialog.FindName("SearchComboBox"));
                    Assert.False(filter.IsEnabled);
                    Assert.Contains("尚未接入", Assert.IsType<string>(filter.ToolTip));
                    object? removed=null;bool confirmed=false;dialog.RemoveRequested+=item=>removed=item;dialog.ConfirmRequested+=(_,_)=>confirmed=true;
                    var actual=model.TreeNodes.Single();
                    typeof(TargetParameterSelectionDialog).GetMethod("Delete_OnHandler",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(dialog,new object[]{new Button{DataContext=actual},new RoutedEventArgs()});
                    Assert.Same(actual,removed);Assert.Single(model.TreeNodes);
                    typeof(TargetParameterSelectionDialog).GetMethod("Close_OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(dialog,new object[]{dialog,new RoutedEventArgs()});
                    Assert.True(confirmed);Assert.True(dialog.IsVisible);
                }
                finally { dialog.Close(); }
                Assert.Null(dialog.DataContext);
                Assert.Single(model.TreeNodes);
        }, loadShellResources:false);
    }
}
