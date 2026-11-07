using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;
using Mvvm.Commands;

namespace FormatModuleControl;

public class ArrayInfo : NameValueItem
{
	public int realIndex;

	public bool upEnable;

	public bool downEnable;

	public ButtonSelecter_Format indexInfo;

	private DelegateCommand<ObjectEventArgs> _MoveUpCommand = null;

	private DelegateCommand<ObjectEventArgs> _MoveDownCommand = null;

	private DelegateCommand<ObjectEventArgs> _DeleteCommand = null;

	public int RealIndex
	{
		get
		{
			return realIndex;
		}
		set
		{
			if (realIndex != value)
			{
				realIndex = value;
				OnPropertyChanged("RealIndex");
			}
		}
	}

	public bool UpEnable
	{
		get
		{
			return upEnable;
		}
		set
		{
			if (upEnable != value)
			{
				upEnable = value;
				OnPropertyChanged("UpEnable");
			}
		}
	}

	public bool DownEnable
	{
		get
		{
			return downEnable;
		}
		set
		{
			if (downEnable != value)
			{
				downEnable = value;
				OnPropertyChanged("DownEnable");
			}
		}
	}

	public ButtonSelecter_Format IndexInfo
	{
		get
		{
			if (indexInfo == null)
			{
				indexInfo = new ButtonSelecter_Format
				{
					Name = "下标",
					Description = "数组下标",
					DisplayName = "数组下标",
					CustomVisible = true,
					DropOpenSetter = new Setter
					{
						TargetName = "EnumEntrys",
						OperationName = "GetFrontFilterTreeOperation",
						OperationParams = "int;float;string"
					},
					Triggers = new ObservableCollection<object>(),
					Initers = new ObservableCollection<object>()
				};
				indexInfo.SetParentNode(this);
				indexInfo.DropOpenSetter.SetParentNode(indexInfo);
				Setter setter = new Setter
				{
					TargetName = "CurValue",
					OperationName = "GetSelectedFilterOperation"
				};
				setter.CreatOperateInstance();
				setter.SetParentNode(indexInfo);
				indexInfo.Initers.Add(setter);
			}
			return indexInfo;
		}
	}

	public ICommand MoveUpCommand
	{
		get
		{
			if (_MoveUpCommand == null)
			{
				_MoveUpCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)MoveUpCommandExcute);
			}
			return (ICommand)_MoveUpCommand;
		}
	}

	public ICommand MoveDownCommand
	{
		get
		{
			if (_MoveDownCommand == null)
			{
				_MoveDownCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)MoveDownCommandExcute);
			}
			return (ICommand)_MoveDownCommand;
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			if (_DeleteCommand == null)
			{
				_DeleteCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)DeleteCommandExcute);
			}
			return (ICommand)_DeleteCommand;
		}
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(RealIndex).Append("`").Append(IndexInfo.CurValueA);
		return stringBuilder.ToString();
	}

	private void MoveUpCommandExcute(ObjectEventArgs e)
	{
		ArrayData arrayData = GetParentNode<ArrayData>();
		if (arrayData == null)
		{
			return;
		}
		int num = arrayData.ArrayList.IndexOf(this);
		if (num <= 0)
		{
			return;
		}
		arrayData.ArrayList.Remove(this);
		arrayData.ArrayList.Insert(--num, this);
		for (int i = 0; i < arrayData.ArrayList.Count; i++)
		{
			if (i == 0)
			{
				(arrayData.ArrayList[i] as ArrayInfo).UpEnable = false;
				(arrayData.ArrayList[i] as ArrayInfo).DownEnable = true;
			}
			else if (i == arrayData.ArrayList.Count - 1)
			{
				(arrayData.ArrayList[i] as ArrayInfo).UpEnable = true;
				(arrayData.ArrayList[i] as ArrayInfo).DownEnable = false;
			}
			else
			{
				(arrayData.ArrayList[i] as ArrayInfo).UpEnable = true;
				(arrayData.ArrayList[i] as ArrayInfo).DownEnable = true;
			}
			(arrayData.ArrayList[i] as ArrayInfo).RealIndex = i;
		}
	}

	private void MoveDownCommandExcute(ObjectEventArgs e)
	{
		ArrayData arrayData = GetParentNode<ArrayData>();
		if (arrayData == null)
		{
			return;
		}
		int num = arrayData.ArrayList.IndexOf(this);
		if (num < 0 || num == arrayData.ArrayList.Count - 1)
		{
			return;
		}
		arrayData.ArrayList.Remove(this);
		arrayData.ArrayList.Insert(++num, this);
		for (int i = 0; i < arrayData.ArrayList.Count; i++)
		{
			if (i == 0)
			{
				(arrayData.ArrayList[i] as ArrayInfo).UpEnable = false;
				(arrayData.ArrayList[i] as ArrayInfo).DownEnable = true;
			}
			else if (i == arrayData.ArrayList.Count - 1)
			{
				(arrayData.ArrayList[i] as ArrayInfo).UpEnable = true;
				(arrayData.ArrayList[i] as ArrayInfo).DownEnable = false;
			}
			else
			{
				(arrayData.ArrayList[i] as ArrayInfo).UpEnable = true;
				(arrayData.ArrayList[i] as ArrayInfo).DownEnable = true;
			}
			(arrayData.ArrayList[i] as ArrayInfo).RealIndex = i;
		}
	}

	private void DeleteCommandExcute(ObjectEventArgs e)
	{
		ArrayData arrayData = GetParentNode<ArrayData>();
		if (arrayData == null || !arrayData.ArrayList.Contains(this))
		{
			return;
		}
		arrayData.ArrayList.Remove(this);
		if (arrayData.ArrayList.Count == 1)
		{
			(arrayData.ArrayList[0] as ArrayInfo).UpEnable = false;
			(arrayData.ArrayList[0] as ArrayInfo).DownEnable = false;
			(arrayData.ArrayList[0] as ArrayInfo).RealIndex = 0;
			return;
		}
		for (int i = 0; i < arrayData.ArrayList.Count; i++)
		{
			if (i == 0)
			{
				(arrayData.ArrayList[i] as ArrayInfo).UpEnable = false;
				(arrayData.ArrayList[i] as ArrayInfo).DownEnable = true;
			}
			else if (i == arrayData.ArrayList.Count - 1)
			{
				(arrayData.ArrayList[i] as ArrayInfo).UpEnable = true;
				(arrayData.ArrayList[i] as ArrayInfo).DownEnable = false;
			}
			else
			{
				(arrayData.ArrayList[i] as ArrayInfo).UpEnable = true;
				(arrayData.ArrayList[i] as ArrayInfo).DownEnable = true;
			}
			(arrayData.ArrayList[i] as ArrayInfo).RealIndex = i;
		}
	}
}
