using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;

/// <summary>The recovered three-column result tree. Its row objects and commands belong to the host projection.</summary>
public partial class PropertyListView : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(PropertyListView));
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(PropertyListView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SelectionRestored));
    public static readonly DependencyProperty ErrorInfoProperty = DependencyProperty.Register(nameof(ErrorInfo), typeof(string), typeof(PropertyListView));
    public static readonly DependencyProperty SelectedItemPathProperty = DependencyProperty.Register(nameof(SelectedItemPath), typeof(string), typeof(PropertyListView));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(PropertyListView), new PropertyMetadata(true));
    public static readonly DependencyProperty SelectedCommandProperty = DependencyProperty.Register(nameof(SelectedCommand), typeof(ICommand), typeof(PropertyListView));
    public static readonly DependencyProperty BindVariableCommandProperty = DependencyProperty.Register(nameof(BindVariableCommand), typeof(ICommand), typeof(PropertyListView));
    public static readonly DependencyProperty UnbindVariableCommandProperty = DependencyProperty.Register(nameof(UnbindVariableCommand), typeof(ICommand), typeof(PropertyListView));
    public static readonly DependencyProperty CopyPathCommandProperty = DependencyProperty.Register(nameof(CopyPathCommand), typeof(ICommand), typeof(PropertyListView));
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public string? ErrorInfo { get => (string?)GetValue(ErrorInfoProperty); set => SetValue(ErrorInfoProperty, value); }
    public string? SelectedItemPath { get => (string?)GetValue(SelectedItemPathProperty); set => SetValue(SelectedItemPathProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public ICommand? SelectedCommand { get => (ICommand?)GetValue(SelectedCommandProperty); set => SetValue(SelectedCommandProperty, value); }
    public ICommand? BindVariableCommand { get => (ICommand?)GetValue(BindVariableCommandProperty); set => SetValue(BindVariableCommandProperty, value); }
    public ICommand? UnbindVariableCommand { get => (ICommand?)GetValue(UnbindVariableCommandProperty); set => SetValue(UnbindVariableCommandProperty, value); }
    public ICommand? CopyPathCommand { get => (ICommand?)GetValue(CopyPathCommandProperty); set => SetValue(CopyPathCommandProperty, value); }
    public event RoutedPropertyChangedEventHandler<object>? SelectedItemChanged;
    private bool _restoringSelection;
    private bool _userSelecting;
    private void SelectionMouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) BeginUserSelection(); }
    private void SelectionKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Left or Key.Right) BeginUserSelection();
    }
    private void BeginUserSelection()
    {
        _userSelecting = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() => _userSelecting = false));
    }
    public PropertyListView()
    {
        InitializeComponent();
        resultTreeView.AddHandler(LoadedEvent, new RoutedEventHandler(ContainerLoaded), true);
    }
    private void ContainerLoaded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem container || !ReferenceEquals(container.DataContext, SelectedItem)) return;
        _restoringSelection = true;
        try { container.IsSelected = true; }
        finally { _restoringSelection = false; }
    }
    private static void SelectionRestored(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var view = (PropertyListView)sender;
        if (view.resultTreeView is null || view._restoringSelection) return;
        view._restoringSelection = true;
        try { SelectRealized(view.resultTreeView, e.NewValue); }
        finally { view._restoringSelection = false; }
    }
    private static bool SelectRealized(ItemsControl parent, object? item)
    {
        foreach (var child in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(child) is not TreeViewItem container) continue;
            if (ReferenceEquals(child, item)) { container.IsSelected = true; return true; }
            if (SelectRealized(container, item)) return true;
        }
        return false;
    }
    private void TreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_restoringSelection) return;
        _restoringSelection = true;
        try { SetCurrentValue(SelectedItemProperty, e.NewValue); }
        finally { _restoringSelection = false; }
        SelectedItemChanged?.Invoke(this, e);
        // The actual row object is the only command argument; level/index is never an identity.
        var command = SelectedCommand;
        var userSelection = _userSelecting;
        _userSelecting = false;
        if (userSelection && command?.CanExecute(e.NewValue) == true) command.Execute(e.NewValue);
    }
}
