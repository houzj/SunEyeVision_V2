using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.UI.Services.Workflow;
namespace SunEyeVision.UI.Shell.Controls.Shell;
public partial class WorkflowSearchControl:UserControl
{
 public static readonly DependencyProperty ScopeIdentityProperty=DependencyProperty.Register("ScopeIdentity",typeof(string),typeof(WorkflowSearchControl),new PropertyMetadata(null,(d,_)=>((WorkflowSearchControl)d).CloseSearch()));
 public static readonly DependencyProperty DefinitionIdentityProperty=DependencyProperty.Register("DefinitionIdentity",typeof(object),typeof(WorkflowSearchControl),new PropertyMetadata(null,(d,_)=>((WorkflowSearchControl)d).CloseSearch()));
 internal Func<string,bool,IReadOnlyList<WorkflowSearchEntry>>? QuerySources {get;set;}
 internal Func<WorkflowSearchEntry,bool>? Navigate {get;set;}
 private bool _setting;private Window? _owner;
 private readonly System.Windows.Threading.DispatcherTimer _searchTimer=new(){Interval=TimeSpan.FromMilliseconds(100)};
 public WorkflowSearchControl(){_searchTimer.Tick+=(_,_)=>{_searchTimer.Stop();Refresh();};InitializeComponent();Loaded+=(_,_)=>{_owner=Window.GetWindow(this);if(_owner!=null){_owner.Deactivated+=OwnerDeactivated;_owner.LocationChanged+=OwnerMoved;}};Unloaded+=(_,_)=>{CloseSearch();if(_owner!=null){_owner.Deactivated-=OwnerDeactivated;_owner.LocationChanged-=OwnerMoved;}_owner=null;};}
 private void OwnerMoved(object? sender,EventArgs e){if(ResultsPopup.IsOpen){var offset=ResultsPopup.HorizontalOffset;ResultsPopup.HorizontalOffset=offset+0.1;ResultsPopup.HorizontalOffset=offset;}}
 private void OwnerDeactivated(object? sender,EventArgs e){_searchTimer.Stop();ResultsPopup.IsOpen=false;}
 internal void CloseSearch(){_searchTimer.Stop();if(ResultsPopup==null)return;ResultsPopup.IsOpen=false;Results.ItemsSource=null;Expand.IsChecked=false;}
 private void ExpandChanged(object sender,RoutedEventArgs e){if(SearchFields==null)return;SearchFields.Visibility=Expand.IsChecked==true?Visibility.Visible:Visibility.Collapsed;if(Expand.IsChecked==true){Query.Focus();Refresh();}else{_searchTimer.Stop();ResultsPopup.IsOpen=false;}}
 private void QueryLostFocus(object sender,RoutedEventArgs e){_searchTimer.Stop();ResultsPopup.IsOpen=false;}
 private void QueryChanged(object sender,TextChangedEventArgs e){if(_setting||ResultsPopup==null)return;_searchTimer.Stop();if(string.IsNullOrEmpty(Query.Text)){ResultsPopup.IsOpen=false;return;}if(Expand.IsChecked==true)_searchTimer.Start();}
 private void ScopeChanged(object sender,SelectionChangedEventArgs e){if(ResultsPopup!=null)Refresh();}
 private void ClearClick(object sender,RoutedEventArgs e){Query.Clear();Query.Focus();}
 private void Refresh()
 {
  if(Expand.IsChecked!=true||string.IsNullOrEmpty(Query.Text)){ResultsPopup.IsOpen=false;return;}
  var values=QuerySources?.Invoke(Query.Text,Scope.SelectedIndex==0)??Array.Empty<WorkflowSearchEntry>();Results.ItemsSource=values;Results.SelectedIndex=values.Count==0?-1:0;Results.Visibility=values.Count==0?Visibility.Collapsed:Visibility.Visible;NoResults.Visibility=values.Count==0?Visibility.Visible:Visibility.Collapsed;ResultsPopup.IsOpen=true;
 }
 private void Commit()
 {
  if(Results.SelectedItem is not WorkflowSearchEntry entry)return;
  if(Navigate?.Invoke(entry)!=true){Refresh();return;}
  _setting=true;try{Query.Text=entry.Name;}finally{_setting=false;}ResultsPopup.IsOpen=false;
 }
 private void ResultClick(object sender,MouseButtonEventArgs e){if(ItemsControl.ContainerFromElement(Results,e.OriginalSource as DependencyObject) is ListBoxItem item){Results.SelectedItem=item.DataContext;Commit();e.Handled=true;}}
 private void QueryKeyDown(object sender,KeyEventArgs e)
 {
  if(e.Key==Key.Escape){_searchTimer.Stop();ResultsPopup.IsOpen=false;e.Handled=true;return;}
  if(e.Key==Key.Enter){if(_searchTimer.IsEnabled){_searchTimer.Stop();Refresh();}if(ResultsPopup.IsOpen)Commit();e.Handled=true;return;}
  if(ResultsPopup.IsOpen&&e.Key is Key.Down or Key.Up&&Results.Items.Count>0){Results.SelectedIndex=Math.Clamp(Results.SelectedIndex+(e.Key==Key.Down?1:-1),0,Results.Items.Count-1);Results.ScrollIntoView(Results.SelectedItem);e.Handled=true;}
 }
}
