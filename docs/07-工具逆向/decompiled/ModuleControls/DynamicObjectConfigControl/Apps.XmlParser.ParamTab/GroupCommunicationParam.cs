using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class GroupCommunicationParam : BridgeItemBase
{
	private DelegateCommand<object> _PreviewMouseWheelCommand = null;

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

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		if (BridgeObjA != null)
		{
			(BridgeObjA as ProcedureCommDynamicIO).InitSource();
		}
	}

	private void PreviewMouseWheelCommandExcute(object obj)
	{
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
		}
	}
}
