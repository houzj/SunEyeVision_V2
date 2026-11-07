using System;
using System.Linq;
using Apps.Interface;

namespace Apps.XmlParser.ParamTab;

public class AndModuleDynamicIO : DynamicIOContainer<AndDynamicIO>
{
	private bool _isAllowAdd = true;

	public bool IsAllowAdd
	{
		get
		{
			return _isAllowAdd;
		}
		set
		{
			if (_isAllowAdd != value)
			{
				_isAllowAdd = value;
				OnPropertyChanged("IsAllowAdd");
			}
		}
	}

	public override bool IsInitAdd => true;

	public override string DefaultTypeName => "int";

	public override string DefaultName => "int";

	public AndModuleDynamicIO(string name, IOwner dataSorce)
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
		(obj as AndDynamicIO).DisplayIndexA = (Convert.ToInt32(ItemsA.Where((object x) => (x as AndDynamicIO).CustomVisibleA).ToList().Max((object p) => Convert.ToInt32((p as AndDynamicIO).DisplayIndexA))) + 1).ToString();
		return obj;
	}

	public override void RemoveIO(object dymaicIO)
	{
		base.RemoveIO(dymaicIO);
		ModifyDisplayIndex();
	}

	public void InitSource()
	{
		GetParam();
		ModifyDisplayIndex();
	}

	public void GetParam()
	{
		if (Owner == null)
		{
			return;
		}
		AndDynamicIO[] array = (from AndDynamicIO p in ItemsA.Where((object p) => p is AndDynamicIO)
			where p.CustomVisibleA
			select p).ToArray();
		object obj = (GetParentNode() as AlgorithmTabRoot)?.FindBaseNodeByName("LogicalMode");
		if (obj != null)
		{
			object logicType = (obj as EnumerationLogicType).GetLogicType("NOT");
			if (array.Length > 1)
			{
				(logicType as EnumEntry_LogicType).IsEnableA = false;
			}
			else
			{
				(logicType as EnumEntry_LogicType).IsEnableA = true;
			}
			string text = DataSource.Params["LogicalMode"];
			if ("5" == text)
			{
				IsAllowAdd = false;
			}
			else
			{
				IsAllowAdd = true;
			}
		}
	}

	private void ModifyDisplayIndex()
	{
		AndDynamicIO[] array = (from AndDynamicIO p in ItemsA.Where((object p) => p is AndDynamicIO)
			where p.CustomVisibleA
			select p).ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			array[num].DisplayIndexA = (num + 1).ToString();
		}
	}
}
