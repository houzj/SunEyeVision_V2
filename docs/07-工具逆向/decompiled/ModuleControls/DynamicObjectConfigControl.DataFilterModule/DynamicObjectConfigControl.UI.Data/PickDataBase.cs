using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Apps.Interface;
using Apps.XmlParser.ParamTab;
using VM.Core;

namespace DynamicObjectConfigControl.UI.Data;

public class PickDataBase : IPropertyChanged, INotifyPropertyChanged
{
	private bool _isNeedIncludeModuleName = true;

	public object Node = null;

	private IList EnumEntrys;

	public bool IsNeedIncludeModuleName
	{
		get
		{
			return _isNeedIncludeModuleName;
		}
		set
		{
			if (_isNeedIncludeModuleName != value)
			{
				_isNeedIncludeModuleName = value;
				OnPropertyChanged("IsNeedIncludeModuleName");
			}
		}
	}

	public IList EnumEntrysA
	{
		get
		{
			return EnumEntrys;
		}
		set
		{
			EnumEntrys = value;
			OnPropertyChanged("EnumEntrysA");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public virtual void OnPropertyChanged(string proName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(proName));
	}

	public virtual void Init()
	{
	}

	public IList GetCheckedLeafNode()
	{
		if (EnumEntrysA == null)
		{
			return null;
		}
		List<object> list = new List<object>();
		GetLeafNode(list, EnumEntrysA);
		return list;
	}

	protected virtual void GetLeafNode(List<object> leafNodes, IList ls)
	{
		foreach (Holder item in ls.OfType<Holder>())
		{
			if (item.HolderTarget is VmModule)
			{
				GetLeafNode(leafNodes, item.EnumEntrysA);
			}
			else if (IsFullConfig(item))
			{
				leafNodes.Add(item.HolderTarget);
			}
			else
			{
				GetLeafNode(leafNodes, item.EnumEntrysA);
			}
		}
	}

	private bool IsFullConfig(Holder node)
	{
		if (!node.OutputEnabled)
		{
			return false;
		}
		foreach (Holder item in node.EnumEntrysA)
		{
			if (!IsFullConfig(item))
			{
				return false;
			}
		}
		return true;
	}
}
