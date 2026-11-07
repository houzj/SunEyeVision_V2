using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Apps.Interface;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class StringCompareModuleDynamicIO : DynamicIOContainer<StringCompareDynamicIO>
{
	public override ObservableCollection<object> ItemsA
	{
		get
		{
			ObservableCollection<object> itemsA = base.ItemsA;
			if (itemsA != null && itemsA.FirstOrDefault((object p) => (p as StringCompareDynamicIO).IoDataSource.Name == "StringA") is StringCompareDynamicIO { CustomVisibleA: not false } stringCompareDynamicIO)
			{
				stringCompareDynamicIO.CustomVisibleA = false;
				UpdateItemIndex();
				UpdateCanDelete();
			}
			return itemsA;
		}
		set
		{
			base.ItemsA = value;
		}
	}

	public override string DefaultTypeName => "string";

	public override string DefaultName => "Text";

	public StringCompareModuleDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	public override object AddIO(string name = null, string type = null, int index = -1)
	{
		object obj = base.AddIO(name, type, index);
		if (obj == null)
		{
			return obj;
		}
		(obj as StringCompareDynamicIO).RealIndex = (Convert.ToInt32(ItemsA.Where((object x) => (x as StringCompareDynamicIO).CustomVisibleA).ToList().Max((object p) => Convert.ToInt32((p as StringCompareDynamicIO).RealIndex))) + 1).ToString();
		SetDisplayIndexSetParamUnit();
		return obj;
	}

	public override void RemoveIO(object dymaicIO)
	{
		base.RemoveIO(dymaicIO);
		SetDisplayIndexSetParamUnit();
	}

	public void SetDisplayIndexSetParamUnit()
	{
		List<string> list = new List<string>();
		for (int i = 0; i < ItemsA.Count; i++)
		{
			StringCompareDynamicIO stringCompareDynamicIO = ItemsA[i] as StringCompareDynamicIO;
			if (stringCompareDynamicIO.CustomVisibleA)
			{
				list.Add(stringCompareDynamicIO.RealIndex);
			}
		}
		if (list.Count < 1)
		{
			return;
		}
		(Owner as VmModule).UserData["RealIndex"] = StringUtil.UTF8GetBytesPadZero(string.Join(",", list.ToArray()));
		UpdateEnable();
		StringBuilder stringBuilder = new StringBuilder();
		foreach (StringCompareDynamicIO item in ItemsA)
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
			return;
		}
		try
		{
			stringBuilder.Append("\0");
			byte[] bytes = Encoding.UTF8.GetBytes(stringBuilder.ToString());
			((IUserBytesData)(Owner as VmModule).Params)["StrCompareText"] = bytes;
		}
		catch
		{
		}
	}

	public void InitSource()
	{
		GetDisplayIndex();
		UpdateEnable();
	}

	public void GetDisplayIndex()
	{
		if (Owner == null)
		{
			return;
		}
		byte[] array = (Owner as VmModule).UserData["RealIndex"];
		if (array == null || array.Length == 0)
		{
			return;
		}
		string text = StringUtil.UTF8GetString(array);
		string[] array2 = text.Split(',');
		List<object> list = ItemsA.Where((object x) => (x as StringCompareDynamicIO).CustomVisibleA).ToList();
		for (int num = 0; num < list.Count; num++)
		{
			StringCompareDynamicIO stringCompareDynamicIO = list[num] as StringCompareDynamicIO;
			if (stringCompareDynamicIO.CustomVisibleA)
			{
				stringCompareDynamicIO.RealIndex = array2[num];
			}
		}
	}

	public void UpdateEnable()
	{
		int num = Enumerable.Count(ItemsA, (object x) => (x as StringCompareDynamicIO).CustomVisibleA);
		for (int num2 = 0; num2 < ItemsA.Count; num2++)
		{
			StringCompareDynamicIO stringCompareDynamicIO = ItemsA[num2] as StringCompareDynamicIO;
			if (!string.IsNullOrEmpty(stringCompareDynamicIO.IndexA))
			{
				int num3 = int.Parse(stringCompareDynamicIO.IndexA);
				stringCompareDynamicIO.UpEnable = num3 > 1;
				stringCompareDynamicIO.DownEnable = num3 < num;
			}
		}
	}
}
