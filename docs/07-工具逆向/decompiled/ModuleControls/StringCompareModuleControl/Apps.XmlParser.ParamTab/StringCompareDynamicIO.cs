using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Mvvm.Commands;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class StringCompareDynamicIO : DynamicIO
{
	private string _realIndex = "0";

	private DelegateCommand _realIndexCommand;

	private bool _upenable;

	private bool _downenable = false;

	private DelegateCommand _upCommand;

	private DelegateCommand _downCommand;

	public List<string> SelectValueBasicTypes => new List<string> { "int", "float" };

	public string RealIndex
	{
		get
		{
			return _realIndex;
		}
		set
		{
			if (!(_realIndex == value))
			{
				int result = 0;
				if (string.IsNullOrEmpty(value))
				{
					value = "0";
				}
				if (int.TryParse(value, out result))
				{
					_realIndex = value;
					OnPropertyChanged("RealIndex");
				}
			}
		}
	}

	public ICommand RealIndexCommand
	{
		get
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			//IL_0024: Expected Obj, but got Unknown
			DelegateCommand val = _realIndexCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)RealIndexCommandExecute);
				DelegateCommand val3 = val2;
				_realIndexCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public bool UpEnable
	{
		get
		{
			return _upenable;
		}
		set
		{
			if (_upenable != value)
			{
				_upenable = value;
				OnPropertyChanged("UpEnable");
			}
		}
	}

	public bool DownEnable
	{
		get
		{
			return _downenable;
		}
		set
		{
			if (_downenable != value)
			{
				_downenable = value;
				OnPropertyChanged("DownEnable");
			}
		}
	}

	public int VisiableCount => Enumerable.Count((GetParentNode() as StringCompareModuleDynamicIO).ItemsA, (object x) => (x as StringCompareDynamicIO).CustomVisibleA);

	public ICommand MoveUpCommand
	{
		get
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			//IL_0024: Expected Obj, but got Unknown
			DelegateCommand val = _upCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)MoveUpCommandExecute);
				DelegateCommand val3 = val2;
				_upCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand MoveDownCommand
	{
		get
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			//IL_0024: Expected Obj, but got Unknown
			DelegateCommand val = _downCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)MoveDownCommandExecute);
				DelegateCommand val3 = val2;
				_downCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	private void RealIndexCommandExecute()
	{
		if (GetParentNode() is StringCompareModuleDynamicIO stringCompareModuleDynamicIO)
		{
			stringCompareModuleDynamicIO.SetDisplayIndexSetParamUnit();
		}
	}

	private void MoveUpCommandExecute()
	{
		if (GetParentNode() is StringCompareModuleDynamicIO { ItemsA: var itemsA })
		{
			List<StringCompareDynamicIO> list = (from StringCompareDynamicIO p in itemsA.Where((object p) => p is StringCompareDynamicIO)
				where p.CustomVisibleA
				select p).ToList();
			int num = list.IndexOf(this);
			if (num != 0)
			{
				Swap(list[num - 1]);
			}
		}
	}

	private void Swap(object swapItem)
	{
		if (swapItem != null)
		{
			StringCompareDynamicIO stringCompareDynamicIO = (StringCompareDynamicIO)swapItem;
			string curValueA = FilterSelecter.CurValueA;
			string realIndex = RealIndex;
			FilterSelecter.CurValueA = stringCompareDynamicIO.FilterSelecter.CurValueA;
			RealIndex = stringCompareDynamicIO.RealIndex;
			stringCompareDynamicIO.FilterSelecter.CurValueA = curValueA;
			stringCompareDynamicIO.RealIndex = realIndex;
			(GetParentNode() as StringCompareModuleDynamicIO).SetDisplayIndexSetParamUnit();
		}
	}

	private void MoveDownCommandExecute()
	{
		if (GetParentNode() is StringCompareModuleDynamicIO { ItemsA: var itemsA })
		{
			List<StringCompareDynamicIO> list = (from StringCompareDynamicIO p in itemsA.Where((object p) => p is StringCompareDynamicIO)
				where p.CustomVisibleA
				select p).ToList();
			int num = list.IndexOf(this);
			if (num != list.Count - 1)
			{
				Swap(list[num + 1]);
			}
		}
	}

	public override int SetParamUnit()
	{
		(GetParentNode() as StringCompareModuleDynamicIO).UpdateEnable();
		StringBuilder stringBuilder = new StringBuilder();
		foreach (StringCompareDynamicIO item in (GetParentNode() as DynamicIOContainer<StringCompareDynamicIO>).ItemsA)
		{
			if (item.CustomVisibleA)
			{
				stringBuilder.Append(item.NameA);
				stringBuilder.Append("\n");
				stringBuilder.Append(Convert.ToInt32(item.IndexA) - 1);
				stringBuilder.Append("-");
				stringBuilder.Append(item.CustomVisibleA.ToString());
				stringBuilder.Append("-");
				stringBuilder.Append("");
				stringBuilder.Append("-");
				stringBuilder.Append(item.RealIndex);
				stringBuilder.Append("\r");
			}
		}
		if (string.IsNullOrEmpty(stringBuilder.ToString()))
		{
			stringBuilder.Append("-1");
		}
		try
		{
			stringBuilder.Append("\0");
			byte[] bytes = Encoding.UTF8.GetBytes(stringBuilder.ToString());
			((IUserBytesData)IoDataSource.Root.Params)["StrCompareText"] = bytes;
		}
		catch
		{
		}
		return 0;
	}
}
