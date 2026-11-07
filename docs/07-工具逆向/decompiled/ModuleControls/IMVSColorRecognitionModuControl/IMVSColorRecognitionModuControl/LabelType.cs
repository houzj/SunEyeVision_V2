using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Apps.Data;
using Mvvm.Commands;

namespace IMVSColorRecognitionModuControl;

public class LabelType : INotifyPropertyChanged
{
	private string _name;

	private ItemState _MyItemState = ItemState.Normal;

	private ImageEntity _coverImage;

	protected ObservableCollection<LabelMask> _MaskList = new ObservableCollection<LabelMask>();

	private bool _TextBoxIsReadOnly = true;

	private DelegateCommand _EditeCommand = null;

	private DelegateCommand _DeleteCommand = null;

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			if (_name != value)
			{
				if ((Owner as ColorDstgshDialog).LabelList.FirstOrDefault((LabelType x) => x.Name == value) != null)
				{
					MyItemState = ItemState.SetParamErro;
					return;
				}
				string text = value;
				if (text.Length > 64 || string.IsNullOrWhiteSpace(text))
				{
					MyItemState = ItemState.SetParamErro;
					return;
				}
				_name = value;
				MyItemState = ItemState.Normal;
				TextBoxIsReadOnly = true;
				NotifyPropertyChanged("Name");
			}
			else
			{
				MyItemState = ItemState.Normal;
				TextBoxIsReadOnly = true;
			}
		}
	}

	public virtual ItemState MyItemState
	{
		get
		{
			return _MyItemState;
		}
		set
		{
			if (_MyItemState != value)
			{
				_MyItemState = value;
				NotifyPropertyChanged("MyItemState");
			}
		}
	}

	public ImageEntity CoverImage
	{
		get
		{
			return _coverImage;
		}
		set
		{
			if (_coverImage != value)
			{
				_coverImage = value;
				NotifyPropertyChanged("LabelCoverImage");
			}
		}
	}

	public ObservableCollection<LabelMask> MaskList => _MaskList;

	public bool TextBoxIsReadOnly
	{
		get
		{
			return _TextBoxIsReadOnly;
		}
		set
		{
			if (_TextBoxIsReadOnly != value)
			{
				_TextBoxIsReadOnly = value;
				NotifyPropertyChanged("TextBoxIsReadOnly");
			}
		}
	}

	public object Owner { get; set; }

	public ICommand EditeCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_EditeCommand == null)
			{
				_EditeCommand = new DelegateCommand((Action)EditeCommandCmdExecute);
			}
			return (ICommand)_EditeCommand;
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_DeleteCommand == null)
			{
				_DeleteCommand = new DelegateCommand((Action)DeleteCommandCmdExecute);
			}
			return (ICommand)_DeleteCommand;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	private void EditeCommandCmdExecute()
	{
		TextBoxIsReadOnly = false;
	}

	private void DeleteCommandCmdExecute()
	{
		if (Owner == null || (Owner as ColorDstgshDialog).LabelList == null)
		{
			return;
		}
		foreach (LabelMask mask in MaskList)
		{
			mask.DeleteMaskImg();
		}
		(Owner as ColorDstgshDialog).LabelList.Remove(this);
	}
}
