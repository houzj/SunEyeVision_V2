using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Apps.BaseData;
using Apps.XmlParser.ParamTab;

namespace DynamicObjectConfigControl;

public partial class DynamicRunParamConfigControl : Control, INotifyPropertyChanged, IComponentConnector
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

	public DynamicRunParamConfigControl()
	{
		InitializeComponent();
	}

	private bool? EditRunParam(object dataSource)
	{
		DynamicRunParamConfigWin dynamicRunParamConfigWin = new DynamicRunParamConfigWin
		{
			Owner = Window.GetWindow(this)
		};
		dynamicRunParamConfigWin.DataContext = dataSource;
		if (dataSource is GroupRunParamConfigData groupRunParamConfigData)
		{
			groupRunParamConfigData.RelateControl = dynamicRunParamConfigWin;
			groupRunParamConfigData.AsyncLoadParams();
		}
		return dynamicRunParamConfigWin.ShowDialog();
	}
}
