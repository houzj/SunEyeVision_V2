using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SunEyeVision.UI.Themes.Base;
using Path = System.Windows.Shapes.Path;

namespace SunEyeVision.UI.Shell.Controls.Shell;

public partial class ShellTopBarControl
{
    private void BuildMenuAndToolbar()
    {
        var roots = ShellMenuCatalog.Create();
        foreach (var root in roots)
            if (root.ShowInMenu) MainMenu.Items.Add(CreateMenuItem(root, true));

        string? group = null;
        foreach (var entry in ShellMenuCatalog.Toolbar(roots))
        {
            if (group is not null && group != entry.ToolGroup)
            {
                var separator = new Border { Width = 1, Height = 18, Margin = new Thickness(4, 0, 4, 0) };
                separator.SetResourceReference(Border.BackgroundProperty, ThemeKeys.TitleBarDividerBrush);
                ToolbarItems.Children.Add(separator);
            }
            group = entry.ToolGroup;
            ToolbarItems.Children.Add(CreateToolbarItem(entry));
        }
    }

    private MenuItem CreateMenuItem(ShellMenuEntry entry, bool topLevel = false)
    {
        var item = new MenuItem { Header = entry.Label, Tag = entry, InputGestureText = entry.Gesture ?? "" };
        if (topLevel) item.Style = (Style)FindResource("TitleBarMenuButton");
        if (entry.Id == "welcome") RegisterName("WelcomeMenuItem", item);
        if (entry.DisabledReason is { } reason)
        { item.IsEnabled = false; item.ToolTip = reason; ToolTipService.SetShowOnDisabled(item, true); }
        if (entry.Command is { } command) item.SetBinding(MenuItem.CommandProperty, new Binding(command));
        if (entry.Action == "exit") item.Click += ExitMenu_Click;
        if (entry.Action == "fullscreen") item.Click += ToggleFullscreen_Click;
        if (entry.Children is not null)
            foreach (var child in entry.Children)
            {
                if (!child.ShowInMenu) continue;
                if (child.SeparatorBefore) item.Items.Add(new Separator());
                item.Items.Add(CreateMenuItem(child));
            }
        return item;
    }

    private UIElement CreateToolbarItem(ShellMenuEntry entry)
    {
        var button = new Button { Style = (Style)FindResource("TitleBarButton"), Tag = entry,
            ToolTip = entry.ToolTip ?? entry.DisabledReason ?? entry.Label };
        ToolTipService.SetShowOnDisabled(button, true);
        if (entry.ToolName is { } name) RegisterName(name, button);
        if (entry.DisabledReason is not null) button.IsEnabled = false;
        if (entry.Command is { } command) button.SetBinding(Button.CommandProperty, new Binding(command));
        if (entry.Icon is { } normal)
        {
            var image = new Image { Width = 20, Height = 20 };
            var style = new Style(typeof(Image));
            style.Setters.Add(new Setter(Image.SourceProperty, ImageSource(normal)));
            if (entry.DisabledIcon is { } disabled)
            {
                var trigger = new DataTrigger { Binding = new Binding("IsEnabled") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1) }, Value = false };
                trigger.Setters.Add(new Setter(Image.SourceProperty, ImageSource(disabled)));
                style.Triggers.Add(trigger);
            }
            image.Style = style;
            button.Content = image;
        }
        else button.Content = entry.Label;
        if (entry.Special == "theme") ConfigureTheme(button);
        if (entry.Special != "undo") return button;

        var wrapper = new Grid(); wrapper.Children.Add(button);
        var hotspot = new Grid { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom, Background = Brushes.Transparent,
            ToolTip = "操作记录：原件记录窗口尚未接入" };
        RegisterName("UndoHistoryHotspot", hotspot);
        hotspot.Children.Add(new Image { Width = 12, Height = 12,
            Source = ImageSource("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/combo_ic_triangle_normal.png") });
        var popup = new Popup { Placement = PlacementMode.Bottom, PlacementTarget = hotspot, AllowsTransparency = true, HorizontalOffset = -24 };
        popup.SetBinding(Popup.IsOpenProperty, new Binding("IsMouseOver") { Source = hotspot, Mode = BindingMode.OneWay });
        var border = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(12, 6, 12, 6) };
        border.SetResourceReference(Border.BackgroundProperty, ThemeKeys.PanelBackgroundBrush);
        border.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.BorderBrush);
        var text = new TextBlock { Text = "操作记录（尚未接入）" };
        text.SetResourceReference(TextBlock.ForegroundProperty, ThemeKeys.TextDisabledBrush);
        border.Child = text; popup.Child = border; hotspot.Children.Add(popup); wrapper.Children.Add(hotspot);
        return wrapper;
    }

    private static ImageSource ImageSource(string path) => new BitmapImage(new Uri("pack://application:,,," + path, UriKind.Absolute));

    private void ConfigureTheme(Button button)
    {
        button.ClearValue(ToolTipProperty);
        var style = new Style(typeof(Button), (Style)FindResource("TitleBarButton"));
        style.Setters.Add(new Setter(ToolTipProperty, "切换到深色主题"));
        var dark = new DataTrigger { Binding = new Binding("IsDarkTheme"), Value = true };
        dark.Setters.Add(new Setter(ToolTipProperty, "切换到浅色主题")); style.Triggers.Add(dark); button.Style = style;
        var shape = new Path { StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        shape.SetBinding(Shape.StrokeProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1) });
        var shapeStyle = new Style(typeof(Path));
        shapeStyle.Setters.Add(new Setter(Path.DataProperty, Geometry.Parse("M 13,2 A 7,7 0 1 0 18,15 A 7,7 0 0 1 13,2 Z")));
        var trigger = new DataTrigger { Binding = new Binding("IsDarkTheme"), Value = true };
        trigger.Setters.Add(new Setter(Path.DataProperty, Geometry.Parse("M 10,2 L 10,4 M 10,16 L 10,18 M 2,10 L 4,10 M 16,10 L 18,10 M 4.3,4.3 L 5.7,5.7 M 14.3,14.3 L 15.7,15.7 M 15.7,4.3 L 14.3,5.7 M 5.7,14.3 L 4.3,15.7 M 14,10 A 4,4 0 1 1 6,10 A 4,4 0 1 1 14,10")));
        shapeStyle.Triggers.Add(trigger); shape.Style = shapeStyle;
        button.Content = new Viewbox { Width = 18, Height = 18, Child = shape };
    }
}
