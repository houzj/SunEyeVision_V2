using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class ButtonSelecter_LogicItem : BridgeItemBase
{
	private DelegateCommand<object> _PreviewMouseWheelCommand;

	private bool hasRegistered;

	public ICommand PreviewMouseWheelCommand
	{
		get
		{
			if (_PreviewMouseWheelCommand == null)
			{
				_PreviewMouseWheelCommand = new DelegateCommand<object>((Action<object>)PreviewMouseWheelCommandExcute);
			}
			return (ICommand)_PreviewMouseWheelCommand;
		}
	}

	private void PreviewMouseWheelCommandExcute(object obj)
	{
		_ = hasRegistered;
		ContentControl contentcontrol = null;
		if (obj is ContentControl)
		{
			contentcontrol = obj as ContentControl;
			contentcontrol.PreviewMouseWheel += (object sender, MouseWheelEventArgs e) =>
			{
				MouseWheelEventArgs e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
				{
					RoutedEvent = UIElement.MouseWheelEvent,
					Source = sender
				};
				contentcontrol.RaiseEvent(e2);
			};
			hasRegistered = true;
		}
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		if (BridgeObjA != null)
		{
			(BridgeObjA as AndModuleDynamicIO).InitSource();
		}
	}
}
