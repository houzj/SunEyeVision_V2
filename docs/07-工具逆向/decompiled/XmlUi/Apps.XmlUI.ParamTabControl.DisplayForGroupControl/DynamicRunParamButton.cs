using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Apps.BaseData;

namespace Apps.XmlUI.ParamTabControl.DisplayForGroupControl;

public class DynamicRunParamButton : Button, INotifyPropertyChanged
{
	public DialogHandler EditMethod => Edit;

	public event PropertyChangedEventHandler PropertyChanged;

	public virtual void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	private bool? Edit(object dataSource)
	{
		DynamicRunParamDisplayWin dynamicRunParamDisplayWin = new DynamicRunParamDisplayWin
		{
			Owner = Window.GetWindow(this)
		};
		dynamicRunParamDisplayWin.DataContext = dataSource;
		bool? result = dynamicRunParamDisplayWin.ShowDialog();
		dynamicRunParamDisplayWin.DataContext = null;
		return result;
	}
}
