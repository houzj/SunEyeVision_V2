using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Commom;

public class ParamsEventCommand : TriggerAction<DependencyObject>
{
	public static readonly DependencyProperty CommandProperty = DependencyProperty.Register("Command", typeof(ICommand), typeof(ParamsEventCommand));

	public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register("CommandParameter", typeof(object), typeof(ParamsEventCommand), new PropertyMetadata(null));

	private Action<object> action;

	public ICommand Command
	{
		get
		{
			return (ICommand)((DependencyObject)(object)this).GetValue(CommandProperty);
		}
		set
		{
			((DependencyObject)(object)this).SetValue(CommandProperty, (object)value);
		}
	}

	public object CommandParameter
	{
		get
		{
			return ((DependencyObject)(object)this).GetValue(CommandParameterProperty);
		}
		set
		{
			((DependencyObject)(object)this).SetValue(CommandParameterProperty, value);
		}
	}

	public ParamsEventCommand()
	{
	}

	public ParamsEventCommand(Action<object> action)
	{
		this.action = action;
	}

	protected override void Invoke(object parameter)
	{
		if (CommandParameter != null)
		{
			parameter = CommandParameter;
		}
		Command?.Execute(parameter);
	}
}
