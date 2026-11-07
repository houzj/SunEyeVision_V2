using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using Apps.Data;
using Apps.UIHelper.Extern;
using Hik.Xml;
using Microsoft.Win32;
using VM.Utility;

namespace VisionMaster.AppMainWindow;

public class SolItem : BaseNode, INotifyPropertyChanged
{
	private DelegateCommand _PathSelectCommand = null;

	private DelegateCommand _deleteCommand = null;

	public string _index;

	public string _solPath;

	public string _solPassword = "";

	public string _commString;

	public bool _commEnable;

	public bool _solcodeboxshow = true;

	public bool _solcodetextshow = false;

	public string _solPasswordBox;

	private bool _ifShowSolCode;

	public ICommand PathSelectCommand
	{
		get
		{
			if (_PathSelectCommand == null)
			{
				_PathSelectCommand = new DelegateCommand(PathSelectCmdExecute);
			}
			return _PathSelectCommand;
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			if (_deleteCommand == null)
			{
				_deleteCommand = new DelegateCommand(DeleteItemCmdExecute);
			}
			return _deleteCommand;
		}
	}

	public string Index
	{
		get
		{
			return _index;
		}
		set
		{
			_index = value;
			OnPropertyChanged("Index");
		}
	}

	public string SolPath
	{
		get
		{
			return _solPath;
		}
		set
		{
			_solPath = value;
			OnPropertyChanged("SolPath");
		}
	}

	public string SolPassword
	{
		get
		{
			return _solPassword;
		}
		set
		{
			_solPassword = value;
			OnPropertyChanged("SolPassword");
		}
	}

	public string SolPasswordBox
	{
		get
		{
			return _solPasswordBox;
		}
		set
		{
			_solPasswordBox = value;
			OnPropertyChanged("SolPasswordBox");
		}
	}

	public string CommString
	{
		get
		{
			return _commString;
		}
		set
		{
			_commString = value;
			OnPropertyChanged("CommString");
		}
	}

	public bool CommEnable
	{
		get
		{
			return _commEnable;
		}
		set
		{
			_commEnable = value;
			OnPropertyChanged("CommEnable");
		}
	}

	public bool IfShowSolCode
	{
		get
		{
			return _ifShowSolCode;
		}
		set
		{
			if (_ifShowSolCode != value)
			{
				_ifShowSolCode = value;
				if (value)
				{
					SolCodeBoxShow = false;
					SolCodeTextShow = true;
				}
				else
				{
					SolCodeBoxShow = true;
					SolCodeTextShow = false;
				}
			}
		}
	}

	public bool SolCodeBoxShow
	{
		get
		{
			if (_ifShowSolCode)
			{
				return false;
			}
			return true;
		}
		set
		{
			OnPropertyChanged("SolCodeBoxShow");
		}
	}

	public bool SolCodeTextShow
	{
		get
		{
			if (_ifShowSolCode)
			{
				return true;
			}
			return false;
		}
		set
		{
			OnPropertyChanged("SolCodeTextShow");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event Action<SolItem> DeleteEventHander = null;

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	private void PathSelectCmdExecute()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "Solution Files (*." + AppFileExtension.Solution + ")|*." + AppFileExtension.Solution
		};
		string path = ConfigurationHelper.INIGetStringValue("OpenSol");
		Apps.UIHelper.Extern.UIHelper.CheckInitDirectory(openFileDialog, path);
		if (openFileDialog.ShowDialog() == true)
		{
			SolPath = openFileDialog.FileName;
			if (File.Exists(SolPath))
			{
				ConfigurationHelper.INISetStringValue("OpenSol", Path.GetDirectoryName(SolPath));
			}
		}
	}

	private void DeleteItemCmdExecute()
	{
		DeleteEventHander?.Invoke(this);
	}
}
