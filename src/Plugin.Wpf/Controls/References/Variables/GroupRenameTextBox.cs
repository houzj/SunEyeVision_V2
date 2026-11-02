using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Controls.References.Variables;

/// <summary>Group-header rename editor adapted from the original HeadReNameTextBox.</summary>
public sealed class GroupRenameTextBox : TextBox
{
    public static readonly DependencyProperty IsRenamingProperty = DependencyProperty.Register(
        nameof(IsRenaming), typeof(bool), typeof(GroupRenameTextBox), new PropertyMetadata(false, OnIsRenamingChanged));

    public bool IsRenaming { get => (bool)GetValue(IsRenamingProperty); set => SetValue(IsRenamingProperty, value); }

    static GroupRenameTextBox() => DefaultStyleKeyProperty.OverrideMetadata(typeof(GroupRenameTextBox),
        new FrameworkPropertyMetadata(typeof(GroupRenameTextBox)));

    public GroupRenameTextBox()
    {
        KeyDown += OnKeyDown;
        LostFocus += OnLostFocus;
        ContextMenu = null;
    }

    private static void OnIsRenamingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var box = (GroupRenameTextBox)sender;
        if ((bool)args.NewValue) { box.Focus(); box.SelectAll(); }
        if ((bool)args.OldValue) box.GetBindingExpression(TextProperty)?.UpdateSource();
    }

    private void OnLostFocus(object sender, RoutedEventArgs args) => IsRenaming = false;
    private void OnKeyDown(object sender, KeyEventArgs args) { if (args.Key == Key.Return) IsRenaming = false; }
}
