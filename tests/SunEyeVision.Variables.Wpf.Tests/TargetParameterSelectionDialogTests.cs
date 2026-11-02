using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using Xunit;

namespace SunEyeVision.Variables.Wpf.Tests;

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
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var model = new Model();
                var dialog = new TargetParameterSelectionDialog { DataContext = model };
                try
                {
                    dialog.Show(); dialog.UpdateLayout();
                    var tree = Assert.IsType<TargetParameterTree>(dialog.FindName("RunParamsSelectTreeView"));
                    Assert.Same(model.TreeNodes, tree.ItemsSource);
                    var filter = Assert.IsType<ComboBox>(dialog.FindName("SearchComboBox"));
                    Assert.False(filter.IsEnabled);
                    Assert.Contains("尚未接入", Assert.IsType<string>(filter.ToolTip));
                }
                finally { dialog.Close(); }
                Assert.Null(dialog.DataContext);
                Assert.Single(model.TreeNodes);
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "目标参数窗口加载超时。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
