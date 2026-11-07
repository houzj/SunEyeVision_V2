using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using GlobalCameraModuleControl.XmlData;

namespace GlobalCameraModuleControl;

public partial class DynamicCameraParamConfigControl : Control, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	private Popup mypopup;

	private ListBox _lstSearchResult = null;

	public event PropertyChangedEventHandler PropertyChanged;

	public virtual void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public DynamicCameraParamConfigControl()
	{
		InitializeComponent();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		mypopup = Template.FindName("Pop1", this) as Popup;
		_lstSearchResult = Template.FindName("PART_ListBoxSearchResult", this) as ListBox;
		if (_lstSearchResult != null)
		{
			_lstSearchResult.PreviewMouseLeftButtonDown += _lstSearchResult_PreviewMouseLeftButtonDown;
		}
	}

	private void _lstSearchResult_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		ListBoxItem ancestor = UIElementExtension.GetAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
		if (ancestor != null)
		{
			(DataContext as DynamicCameraPanelList).ModifyGroupByName(ancestor.DataContext as DynamicCameraNameItemOfGroupCategory);
			mypopup.IsOpen = false;
		}
	}

	private void BtnMove_Click(object sender, RoutedEventArgs e)
	{
		mypopup.IsOpen = true;
	}
}
