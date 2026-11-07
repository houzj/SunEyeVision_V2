using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.UIHelper.Extern;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class FormatRow : NameValueItem
{
	private ObservableCollection<FormatTextInfo> formatInfoCollection = new ObservableCollection<FormatTextInfo>();

	private int index = 0;

	private DelegateCommand<ObjectEventArgs> _RowSelectCommand = null;

	private DelegateCommand<ObjectEventArgs> _DeleteRowCommand = null;

	private DelegateCommand<ObjectEventArgs> _ClearRowCommand = null;

	public ObservableCollection<FormatTextInfo> FormatInfoCollection
	{
		get
		{
			return formatInfoCollection;
		}
		set
		{
			if (formatInfoCollection != value)
			{
				formatInfoCollection = value;
				OnPropertyChanged("FormatInfoCollection");
			}
		}
	}

	public int Index
	{
		get
		{
			return index;
		}
		set
		{
			if (index != value)
			{
				index = value;
				OnPropertyChanged("Index");
			}
		}
	}

	public ICommand RowSelectCommand
	{
		get
		{
			if (_RowSelectCommand == null)
			{
				_RowSelectCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)RowSelectCommandExecute);
			}
			return (ICommand)_RowSelectCommand;
		}
	}

	public ICommand DeleteRowCommand
	{
		get
		{
			if (_DeleteRowCommand == null)
			{
				_DeleteRowCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)DeleteRowCommandExcute);
			}
			return (ICommand)_DeleteRowCommand;
		}
	}

	public ICommand ClearRowCommand
	{
		get
		{
			if (_ClearRowCommand == null)
			{
				_ClearRowCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)ClearRowCommandExcute);
			}
			return (ICommand)_ClearRowCommand;
		}
	}

	private void RowSelectCommandExecute(ObjectEventArgs e)
	{
		Format format = GetParentNode<Format>();
		if (format != null)
		{
			format.CurrentInfo = null;
			format.CurrentRow = this;
		}
	}

	private void DeleteRowCommandExcute(ObjectEventArgs e)
	{
		DeleteSelf();
	}

	public void DeleteSelf()
	{
		Format format = GetParentNode<Format>();
		if (format == null)
		{
			return;
		}
		if (format.FormatRows.Contains(this))
		{
			format.FormatRows.Remove(this);
			foreach (FormatRow formatRow in format.FormatRows)
			{
				formatRow.Index = format.FormatRows.IndexOf(formatRow);
			}
			format.CurrentInfo = null;
		}
		format.OnPropertyChanged("CanAdd");
	}

	private void ClearRowCommandExcute(ObjectEventArgs e)
	{
		CleaeContent();
	}

	public void CleaeContent()
	{
		FormatInfoCollection.Clear();
	}
}
