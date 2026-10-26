using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;

public class DelegateCommand : ICommand, INotifyPropertyChanged
{
	private Action _mExecute;

	private readonly Predicate<object> _mCanExecute;

	private bool _isenabled;

	public bool Enabled
	{
		get
		{
			return _isenabled;
		}
		set
		{
			_isenabled = value;
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs("Enabled"));
			}
		}
	}

	public event EventHandler CanExecuteChanged;

	public event PropertyChangedEventHandler PropertyChanged;

	public DelegateCommand(Action execute, Predicate<object> mCanExecute)
	{
		_mExecute = execute;
		_mCanExecute = mCanExecute;
	}

	public DelegateCommand(Action execute)
	{
		_mExecute = execute;
	}

	public DelegateCommand()
	{
	}

	public void SetAction(Action execute)
	{
		_mExecute = execute;
	}

	public bool CanExecute(object parameter)
	{
		if (_mCanExecute == null)
		{
			return true;
		}
		return _mCanExecute(parameter);
	}

	public void Execute(object parameter)
	{
		if (_mExecute != null)
		{
			_mExecute();
		}
	}
}
