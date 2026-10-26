using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Visibility inline, declared style fields in an anchored popup on the caller's draft.</summary>
public sealed class DisplayObjectField : Grid
{
    private readonly IReadOnlyList<(DeclaredFieldEditor Editor, string Label)> _style;
    private readonly Button _edit;
    private readonly Popup _popup;
    private readonly Border _surface;
    private Window? _owner;
    private bool _listening;

    public bool IsStyleEditorOpen => _popup.IsOpen;
    public FrameworkElement StyleEditorContent => _surface;

    public DisplayObjectField(string title, DeclaredFieldEditor visibility,
        IReadOnlyList<(DeclaredFieldEditor Editor, string Label)> style)
    {
        _style = style;
        Margin = new Thickness(0, 1, 0, 1);
        ColumnDefinitions.Add(new() { Width = new GridLength(138) });
        ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        ColumnDefinitions.Add(new());
        visibility.SetLabel(title);
        Children.Add(visibility);
        _edit = new Button
        {
            Content = EditorIcons.Pencil(), ToolTip = "编辑" + title + "样式",
            Width = 29, Height = 29, BorderThickness = new Thickness(0),
            Background = Brushes.Transparent, Margin = new Thickness(8, 0, 0, 0)
        };
        SetColumn(_edit, 1);
        Children.Add(_edit);

        var content = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
        var additional = new StackPanel();
        foreach (var (item, index) in style.Select((item, index) => (item, index)))
        {
            // Reuse the same editors validated by DeclaredFieldsView, not a second setting store.
            item.Editor.SetLabel(item.Label);
            item.Editor.SetLabelWidth(92);
            item.Editor.Margin = new Thickness(10, 0, 6, 0);
            (index < 4 ? content : additional).Children.Add(item.Editor);
        }
        if (additional.Children.Count > 0)
            content.Children.Add(new Expander { Header = "更多", Content = additional, Margin = new Thickness(6, 4, 0, 0) });
        _surface = new Border
        {
            Width = 260, BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 205)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Background = new LinearGradientBrush(new GradientStopCollection
            {
                new(Color.FromRgb(252, 252, 252), 0), new(Color.FromRgb(252, 252, 252), .15),
                new(Color.FromRgb(247, 247, 247), 1)
            }), Child = content
        };
        _surface.Resources.MergedDictionaries.Add(EditorResources.Create());
        _surface.KeyDown += OnStyleKeyDown;
        _popup = new Popup
        {
            PlacementTarget = _edit, Placement = PlacementMode.Bottom, HorizontalOffset = -90,
            VerticalOffset = 4, AllowsTransparency = true,
            // Child popup placement targets form the same ownership chain.
            StaysOpen = true, Child = _surface
        };
        _edit.Click += (_, _) => { if (_popup.IsOpen) CloseStyleEditor(); else OpenStyleEditor(); };
        _popup.Opened += OnOpened;
        _popup.Closed += OnClosed;
        Unloaded += (_, _) => CloseStyleEditor();
        IsVisibleChanged += (_, _) => { if (!IsVisible) CloseStyleEditor(); };
    }

    public void OpenStyleEditor() { if (IsLoaded && IsVisible) _popup.IsOpen = true; }

    public void CloseStyleEditor()
    {
        foreach (var item in _style)
            if (item.Editor.InputElement is ColorField color) color.CancelEdit();
        _popup.IsOpen = false;
        StopListening();
    }

    private void OnOpened(object? sender, EventArgs args)
    {
        if (_listening) return;
        _listening = true;
        InputManager.Current.PreProcessInput += OnInput;
        _owner = Window.GetWindow(this);
        if (_owner == null) return;
        _owner.Closed += OnOwnerClosed;
        _owner.Deactivated += OnOwnerDeactivated;
    }

    private void OnClosed(object? sender, EventArgs args) => StopListening();
    private void OnOwnerClosed(object? sender, EventArgs args) => CloseStyleEditor();
    private void OnOwnerDeactivated(object? sender, EventArgs args) => CloseStyleEditor();

    private void StopListening()
    {
        if (_listening) InputManager.Current.PreProcessInput -= OnInput;
        _listening = false;
        if (_owner != null)
        {
            _owner.Closed -= OnOwnerClosed;
            _owner.Deactivated -= OnOwnerDeactivated;
            _owner = null;
        }
    }

    private void OnInput(object sender, PreProcessInputEventArgs args)
    {
        if (args.StagingItem.Input is not MouseButtonEventArgs { ButtonState: MouseButtonState.Pressed } mouse) return;
        if (!OwnsInput(Mouse.DirectlyOver as DependencyObject ?? mouse.OriginalSource as DependencyObject)) CloseStyleEditor();
    }

    private bool OwnsInput(DependencyObject? node)
    {
        var visited = new HashSet<DependencyObject>();
        while (node != null && visited.Add(node))
        {
            if (ReferenceEquals(node, _surface) || ReferenceEquals(node, _edit)) return true;
            if (node is Popup popup) node = popup.PlacementTarget ?? popup.TemplatedParent ?? LogicalTreeHelper.GetParent(popup);
            else node = LogicalTreeHelper.GetParent(node) ?? (node is Visual ? VisualTreeHelper.GetParent(node) : null);
        }
        return false;
    }

    private void OnStyleKeyDown(object sender, KeyEventArgs args)
    {
        // Inner controls handle their own Escape first; unhandled Escape closes this level only.
        if (args.Key != Key.Escape) return;
        CloseStyleEditor();
        _edit.Focus();
        args.Handled = true;
    }
}
