using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.UI.Services.Workflow;
namespace SunEyeVision.UI.Shell.Controls.Shell;
public partial class WorkflowLocationControl : UserControl
{
    public static readonly DependencyProperty DefinitionIdentityProperty = DependencyProperty.Register(
        "DefinitionIdentity", typeof(object), typeof(WorkflowLocationControl),
        new PropertyMetadata(null, (d, _) => ((WorkflowLocationControl)d).CloseLocations()));
    private Window? _owner;
    internal Func<IReadOnlyList<WorkflowLocationEntry>>? GetLocations { get; set; }
    internal Func<WorkflowLocationEntry, bool>? Navigate { get; set; }
    public WorkflowLocationControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _owner = Window.GetWindow(this);
            if (_owner != null) _owner.Deactivated += OwnerDeactivated;
        };
        Unloaded += (_, _) =>
        {
            CloseLocations();
            if (_owner != null) _owner.Deactivated -= OwnerDeactivated;
            _owner = null;
        };
    }
    private void OwnerDeactivated(object? sender, EventArgs e) => CloseLocations();
    internal void CloseLocations()
    {
        if (LocationsPopup == null) return;
        LocationsPopup.IsOpen = false;
        Locations.ItemsSource = null;
        Expand.IsChecked = false;
    }
    private void ExpandChanged(object sender, RoutedEventArgs e)
    {
        if (LocationsPopup == null) return;
        if (Expand.IsChecked == true)
        {
            Locations.ItemsSource = GetLocations?.Invoke() ?? Array.Empty<WorkflowLocationEntry>();
            LocationsPopup.IsOpen = true;
        }
        else CloseLocations();
    }
    private void PopupClosed(object sender, EventArgs e) => CloseLocations();
    private void Commit(WorkflowLocationEntry entry)
    {
        if (Navigate?.Invoke(entry) == true) CloseLocations();
        else Locations.ItemsSource = GetLocations?.Invoke() ?? Array.Empty<WorkflowLocationEntry>();
    }
    private void LocationClick(object sender, MouseButtonEventArgs e)
    {
        // Only a header activates navigation; clicking an expander keeps the tree open.
        if (e.OriginalSource is TextBlock { DataContext: WorkflowLocationEntry entry })
        {
            Commit(entry);
            e.Handled = true;
        }
    }
    private void LocationKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { CloseLocations(); e.Handled = true; }
        else if (e.Key == Key.Enter && Locations.SelectedItem is WorkflowLocationEntry entry)
        { Commit(entry); e.Handled = true; }
    }
}
