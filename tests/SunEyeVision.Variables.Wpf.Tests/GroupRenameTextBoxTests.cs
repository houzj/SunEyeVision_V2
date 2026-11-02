using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using Xunit;

namespace SunEyeVision.Variables.Wpf.Tests;

public sealed class GroupRenameTextBoxTests
{
    private sealed class Group { public string Name { get; set; } = "原名称"; }

    [Fact]
    public void Original_manager_styles_load_in_a_real_window()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window { Width = 400, Height = 200 };
                var resources = new ResourceDictionary
                {
                    Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/References/VariableManagerStyles.xaml", UriKind.Relative)
                };
                var panel = new StackPanel();
                panel.Resources.MergedDictionaries.Add(resources);
                panel.Children.Add(new Button { Style = (Style)resources["Add_ButtonStyle"], Content = "添加" });
                panel.Children.Add(new Button { Style = (Style)resources["deleteButton"] });
                panel.Children.Add(new DataGrid { Style = (Style)resources["varDataGridStyle"] });
                window.Content = panel;
                try { window.Show(); window.UpdateLayout(); }
                finally { window.Content = null; window.Close(); }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "全局变量原件样式加载超时。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void Leaving_rename_mode_updates_the_explicit_binding()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var group = new Group();
                var box = new GroupRenameTextBox();
                box.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/References/GroupRenameTextBox.xaml", UriKind.Relative)
                });
                box.SetResourceReference(FrameworkElement.StyleProperty, typeof(GroupRenameTextBox));
                box.SetBinding(GroupRenameTextBox.TextProperty, new Binding(nameof(Group.Name))
                {
                    Source = group, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit
                });
                var owner = new Window { Content = box, Width = 200, Height = 100 };
                try
                {
                    owner.Show(); owner.UpdateLayout();
                    box.IsRenaming = true;
                    box.Text = "新名称";
                    Assert.Equal("原名称", group.Name);
                    box.IsRenaming = false;
                    Assert.Equal("新名称", group.Name);
                }
                finally { owner.Content = null; owner.Close(); }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "分组重命名控件加载超时。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
