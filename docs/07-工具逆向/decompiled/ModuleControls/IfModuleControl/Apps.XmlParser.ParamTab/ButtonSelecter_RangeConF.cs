using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class ButtonSelecter_RangeConF : BridgeItemBase, ISubInfoCollection
{
	private DelegateCommand<object> _PreviewMouseWheelCommand = null;

	private bool hasRegistered;

	public int DecimalDigits;

	public IList<object> SubChildren => GetSubChildren();

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

	public int DecimalDigitsA
	{
		get
		{
			return DecimalDigits;
		}
		set
		{
			DecimalDigits = value;
			OnPropertyChanged("DecimalDigitsA");
		}
	}

	public IList<object> GetSubChildren()
	{
		ObservableCollection<object> observableCollection = (BridgeObjA as IfModuleDynamicIO)?.ItemsA;
		List<object> list = new List<object>();
		if (observableCollection == null)
		{
			return list;
		}
		foreach (object item in observableCollection)
		{
			if (string.Compare((item as IfDynamicIO).FilterTypeA, "float", StringComparison.CurrentCultureIgnoreCase) == 0)
			{
				IList<object> list2 = ((ISubInfoCollection)((item as IfDynamicIO)?.Floatbetween))?.SubChildren;
				if (list2 != null)
				{
					list.AddRange(list2);
				}
			}
			if (string.Compare((item as IfDynamicIO).FilterTypeA, "int", StringComparison.CurrentCultureIgnoreCase) == 0)
			{
				IList<object> list3 = ((ISubInfoCollection)((item as IfDynamicIO)?.IntegerBettween))?.SubChildren;
				if (list3 != null)
				{
					list.AddRange(list3);
				}
			}
		}
		return list;
	}

	private void PreviewMouseWheelCommandExcute(object obj)
	{
		if (hasRegistered)
		{
		}
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
			(BridgeObjA as IfModuleDynamicIO).InitSource(DecimalDigitsA);
		}
	}
}
