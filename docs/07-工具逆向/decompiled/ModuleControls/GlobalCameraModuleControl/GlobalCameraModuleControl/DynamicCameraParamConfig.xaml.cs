using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Apps.BaseData;

namespace GlobalCameraModuleControl;

public partial class DynamicCameraParamConfig : Control, INotifyPropertyChanged, IComponentConnector
{
	public DialogHandler EditRunParamMethod
	{
		get
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Expected Obj, but got Unknown
			return EditRunParam;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public virtual void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	private bool? EditRunParam(object dataSource)
	{
		DynamicCameraConfigWindow dynamicCameraConfigWindow = new DynamicCameraConfigWindow
		{
			Owner = Window.GetWindow(this)
		};
		dynamicCameraConfigWindow.DataContext = dataSource;
		return dynamicCameraConfigWindow.ShowDialog();
	}

	public DynamicCameraParamConfig()
	{
		InitializeComponent();
	}
}
