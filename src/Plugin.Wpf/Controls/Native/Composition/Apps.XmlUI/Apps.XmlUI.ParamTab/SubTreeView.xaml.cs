using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

/// <summary>Original tree visuals and leaf double-click. Object selection replaces VM business lookup.</summary>
public partial class SubTreeView : UserControl
{
    public delegate void TreeNodeDoubleClick(object sender,ObjectEventArgs e);
    public event TreeNodeDoubleClick? OnTreeNodeDoubleClick;
    private bool _restoring;
    private bool _needRestore=true;
    private TreeViewItem? _selectedContainer;

    public static readonly DependencyProperty TForegroundProperty=DependencyProperty.Register(nameof(TForeground),typeof(Brush),typeof(SubTreeView),new PropertyMetadata(new SolidColorBrush(Color.FromRgb(128,128,128))));
    public static readonly DependencyProperty TBackgroundProperty=DependencyProperty.Register(nameof(TBackground),typeof(Brush),typeof(SubTreeView),new PropertyMetadata(new SolidColorBrush(Color.FromRgb(231,229,228))));
    public static readonly DependencyProperty IsMultiCheckProperty=DependencyProperty.Register(nameof(IsMultiCheck),typeof(bool),typeof(SubTreeView),new PropertyMetadata(false));
    public static readonly DependencyProperty ItemSourceProperty=DependencyProperty.Register(nameof(ItemSource),typeof(object),typeof(SubTreeView),new PropertyMetadata(null,OnSelectionInputChanged));
    public static readonly DependencyProperty SelectedItemProperty=DependencyProperty.Register(nameof(SelectedItem),typeof(object),typeof(SubTreeView),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,OnSelectionInputChanged));
    public Brush TForeground {get=>(Brush)GetValue(TForegroundProperty);set=>SetValue(TForegroundProperty,value);}
    public Brush TBackground {get=>(Brush)GetValue(TBackgroundProperty);set=>SetValue(TBackgroundProperty,value);}
    public bool IsMultiCheck {get=>(bool)GetValue(IsMultiCheckProperty);set=>SetValue(IsMultiCheckProperty,value);}
    public object? ItemSource {get=>GetValue(ItemSourceProperty);set=>SetValue(ItemSourceProperty,value);}
    public object? SelectedItem {get=>GetValue(SelectedItemProperty);set=>SetValue(SelectedItemProperty,value);}
    public SubTreeView()=>InitializeComponent();

    private static void OnSelectionInputChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args)
    {
        var view=(SubTreeView)sender;
        if(view._restoring)return;
        view._needRestore=true;view.TryRestoreSelection();
    }
    private void TryRestoreSelection()
    {
        if(_restoring||!_needRestore||treeView is null||!treeView.IsLoaded)return;
        _restoring=true;
        try
        {
            if(SelectedItem is null)
            {
                if(_selectedContainer is not null)_selectedContainer.IsSelected=false;
                _selectedContainer=null;_needRestore=false;return;
            }
            treeView.UpdateLayout();
            var item=FindTreeViewItem(treeView,SelectedItem);
            if(item is null)return;
            _selectedContainer=item;item.IsSelected=true;item.Focus();_needRestore=false;
        }
        finally{_restoring=false;}
    }
    // Original container traversal: expand ancestors, keep the matched branch open.
    private static TreeViewItem? FindTreeViewItem(ItemsControl parent,object data)
    {
        for(var index=0;index<parent.Items.Count;index++)
        {
            if(parent.ItemContainerGenerator.ContainerFromIndex(index) is not TreeViewItem item)continue;
            if(ReferenceEquals(item.DataContext,data))return item;
            if(item.Items.Count==0)continue;
            var expanded=item.IsExpanded;
            if(!expanded){item.IsExpanded=true;item.UpdateLayout();}
            var found=FindTreeViewItem(item,data);
            if(found is not null)return found;
            if(!expanded){item.IsExpanded=false;item.UpdateLayout();}
        }
        return null;
    }
    private void TextBlock_MouseDown(object sender,MouseButtonEventArgs args)
    {
        if(sender is TreeViewItem {Items.Count:0} item)
            OnTreeNodeDoubleClick?.Invoke(sender,new ObjectEventArgs{Param=item.DataContext});
    }
    private void TreeView_Selected(object sender,RoutedEventArgs args)
    {
        if(_restoring||args.OriginalSource is not TreeViewItem item)return;
        _selectedContainer=item;
        SetCurrentValue(SelectedItemProperty,item.DataContext);
    }
    private void TreeView_Loaded(object sender,RoutedEventArgs args)
    {
        treeView.ItemContainerGenerator.StatusChanged-=ContainersChanged;
        treeView.ItemContainerGenerator.StatusChanged+=ContainersChanged;
        _needRestore=true;TryRestoreSelection();
    }
    private void ContainersChanged(object? sender,EventArgs args)
    {if(treeView.ItemContainerGenerator.Status==GeneratorStatus.ContainersGenerated)TryRestoreSelection();}
    private void TreeView_Unloaded(object sender,RoutedEventArgs args)
    {treeView.ItemContainerGenerator.StatusChanged-=ContainersChanged;_needRestore=true;}
}
