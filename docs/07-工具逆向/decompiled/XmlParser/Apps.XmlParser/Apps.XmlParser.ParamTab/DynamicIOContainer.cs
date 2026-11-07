using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class DynamicIOContainer<T> : BaseParamNode, IDynamic, IUpdateGroupItems, IOwner where T : DynamicIO, new()
{
	private const string DEFAULT_TYPE = "int";

	private const string OUTPUT_NAME = "Output";

	private const string INPUT_NAMA = "Input";

	private const int MAX_ITEM_COUNT = 32;

	protected bool IsAddForceIO = false;

	private ObservableCollection<object> _ItemsA = null;

	private DelegateCommand addIOCommand = null;

	public VmModule DataSource { get; set; }

	public bool IsOutput { get; set; }

	public IOwner AlgorithmItems { get; set; }

	public object Owner
	{
		get
		{
			return DataSource;
		}
		set
		{
		}
	}

	public virtual VmIOCollection TargetVMIOs => (!IsOutput) ? DataSource?.Inputs : DataSource?.Outputs;

	public virtual string DefaultName
	{
		get
		{
			return IsOutput ? "out" : "in";
		}
		set
		{
		}
	}

	public virtual string DefaultTypeName
	{
		get
		{
			return "int";
		}
		set
		{
		}
	}

	public virtual int MaxItemCount => 32;

	public virtual bool IsInitAdd => false;

	public virtual ObservableCollection<object> ItemsA
	{
		get
		{
			if (_ItemsA == null && DataSource != null)
			{
				_ItemsA = new ObservableCollection<object>();
				int num = 0;
				foreach (VmIO targetVMIO in TargetVMIOs)
				{
					T val = new T
					{
						IoDataSource = targetVMIO
					};
					if (targetVMIO.CustomVisibleA)
					{
						num++;
						val.IndexA = num.ToString();
					}
					val.SetParentNode(this);
					_ItemsA.Add(val);
				}
				UpdateCanDelete();
				if (IsInitAdd && num == 0)
				{
					AddIO();
				}
			}
			return _ItemsA;
		}
		set
		{
			if (_ItemsA != value)
			{
				_ItemsA = value;
				OnPropertyChanged("ItemsA");
			}
		}
	}

	public ICommand AddCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = addIOCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)AddIOCommandExecute);
				DelegateCommand val3 = val2;
				addIOCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public string CurrentParamName => "";

	public DynamicIOContainer()
	{
	}

	public DynamicIOContainer(string name, IOwner dataSource)
	{
		IsOutput = name == "Output";
		AlgorithmItems = dataSource;
		DataSource = (VmModule)dataSource.Owner;
		SetParentNode(AlgorithmItems);
	}

	private void AddIOCommandExecute()
	{
		AddIO();
	}

	public virtual object AddIO(string name = null, string type = null, int index = -1)
	{
		if (DataSource == null)
		{
			return null;
		}
		if (ItemsA.Where((object x) => (x as T).CustomVisibleA).Count() >= MaxItemCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " :" + MaxItemCount, MessageButtons.Normal, MessageIcons.Warning);
			return null;
		}
		return AddIOMethod(name, type, index);
	}

	public virtual object AddIOWithoutVerification(string name = null, string type = null, int index = -1)
	{
		if (DataSource == null)
		{
			return null;
		}
		return AddIOMethod(name, type, index);
	}

	private object AddIOMethod(string name, string type, int index)
	{
		string typeName = ((type == null) ? DefaultTypeName : type);
		VmIO vmIO = null;
		vmIO = AddIoByType(typeName, name, (index > -1) ? (index - 1) : (-1));
		T val = new T
		{
			IoDataSource = vmIO,
			CustomVisibleA = true
		};
		if (index > -1)
		{
			val.IndexA = index.ToString();
			val.SetParentNode(this);
			ItemsA.Insert(index - 1, val);
		}
		else
		{
			val.IndexA = (ItemsA.Where((object x) => (x as T).CustomVisibleA).Count() + 1).ToString();
			val.SetParentNode(this);
			ItemsA.Add(val);
		}
		UpdateCanDelete();
		val.SetParamUnit();
		return val;
	}

	public virtual void RemoveIO(object dymaicIO)
	{
		(dymaicIO as T).IoDataSource.RemoveSelf();
		ItemsA.Remove(dymaicIO);
		UpdateCanDelete();
		UpdateItemIndex();
		try
		{
			((IVmIO)(dymaicIO as T).IoDataSource).UnBindingGlobalVariable();
		}
		catch (Exception ex)
		{
			LogHelper.Error("解除全局变量绑定关系失败：" + ex.Message);
		}
		(dymaicIO as T).SetParamUnit();
		dymaicIO = null;
	}

	public void UpdateItemIndex()
	{
		int num = 0;
		foreach (T item in ItemsA.Where((object x) => (x as T).CustomVisibleA))
		{
			num++;
			string text = num.ToString();
			if (!item.IndexA.Equals(text) && !item.ValueTypeA.Equals("IMAGE"))
			{
				item.IndexA = text;
			}
		}
	}

	public void UpdateCanDelete()
	{
		bool flag = ItemsA.Where((object x) => (x as T).CustomVisibleA).Count() <= 1;
		foreach (T item in ItemsA)
		{
			item.CanDeleteA = !flag;
		}
	}

	protected VmIO AddIoByType(string typeName, string ioName, int index = -1)
	{
		object obj = null;
		try
		{
			obj = ((!Enum.TryParse<LeafIOTypeEnum>(typeName.Equals("pointset") ? typeName : typeName.ToUpper(), out var result)) ? null : ((object)result));
		}
		catch
		{
			obj = null;
		}
		if (obj != null)
		{
			return AddLeafIO(typeName, DataSource, TargetVMIOs, ioName, index);
		}
		obj = Enum.Parse(typeof(CombinationIOTypeEnum), typeName);
		if (obj == null && CheckAdditionExist.IsExistAddition)
		{
			obj = Enum.Parse(typeof(CombinationIOTypeEnumEx), typeName);
		}
		return (obj == null) ? null : AddCombinationIO(typeName, DataSource, TargetVMIOs, ioName, index);
	}

	private VmIO AddLeafIO(string typeName, object parent, IList parentList, string ioName = null, int index = -1)
	{
		DynamicVmIO dynamicVmIO = new DynamicVmIO
		{
			Root = DataSource,
			Parent = parent,
			Name = ((ioName == null) ? GetUniqueName(DefaultName) : ioName),
			TypeName = typeName
		};
		if (IsAddForceIO)
		{
			dynamicVmIO.SetIsForce(value: true);
		}
		if (index > -1)
		{
			parentList.Insert(index, dynamicVmIO);
		}
		else
		{
			parentList.Add(dynamicVmIO);
		}
		return dynamicVmIO;
	}

	private VmIO AddCombinationIO(string typeName, object parent, IList parentList, string ioName = null, int index = -1)
	{
		CombinRoot instance = CombinRoot.GetInstance();
		Combination obj = instance.Items.OfType<Combination>().FirstOrDefault((Combination p) => p.Style == typeName);
		return AddIOFromCombination(obj, parent, parentList, ioName, index);
	}

	private VmIO AddIOFromCombination(object obj, object parent, IList parentList, string ioName = null, int index = -1)
	{
		if (obj is Combination combination)
		{
			DynamicVmIO dynamicVmIO = new DynamicVmIO();
			dynamicVmIO.Root = DataSource;
			dynamicVmIO.Parent = parent;
			dynamicVmIO.Name = ((ioName == null) ? GetUniqueName((obj as Combination).Name) : ioName);
			dynamicVmIO.TypeName = combination.Style;
			DynamicVmIO dynamicVmIO2 = dynamicVmIO;
			if (index > -1)
			{
				parentList.Insert(index, dynamicVmIO2);
			}
			else
			{
				parentList.Add(dynamicVmIO2);
			}
			foreach (object filter2 in combination.Filters)
			{
				AddIOFromCombination(filter2, dynamicVmIO2, dynamicVmIO2.Childs);
			}
			return dynamicVmIO2;
		}
		if (obj is Filter filter)
		{
			AddLeafIO(filter.ValueTypeA, parent, parentList, GetUniqueName((obj as Filter).Name));
		}
		return null;
	}

	protected virtual string GetUniqueName(string startName)
	{
		IEnumerable<string> source = from p in DataSource.Inputs.GetAllIO().Concat(DataSource.Outputs.GetAllIO())
			select p.Name;
		string text = startName;
		int num = 0;
		while (true)
		{
			text = startName + num;
			if (!source.Contains("%" + text + "%"))
			{
				break;
			}
			num++;
		}
		return text;
	}

	void IUpdateGroupItems.SetItemsNull()
	{
		_ItemsA = null;
	}
}
