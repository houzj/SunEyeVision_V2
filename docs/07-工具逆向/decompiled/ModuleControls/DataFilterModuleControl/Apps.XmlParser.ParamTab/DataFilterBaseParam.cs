using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using DynamicObjectConfigControl;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class DataFilterBaseParam : NameValueItem, IChildren, IUpdateGroupItems
{
	private VmModule owner;

	public VmModule _SelectedModule;

	private object selectedType;

	private static object obj = new object();

	protected IList _InputOutputTypes;

	private IList _ItemsA;

	private DelegateCommand _AddCommand = null;

	public Setter ChangedCommandSetter;

	internal VmModule Owner
	{
		get
		{
			if (owner == null)
			{
				owner = (GetTopBaseNode() as AlgorithmTabRoot).Owner as VmModule;
			}
			return owner;
		}
	}

	public VmModule SelectedModule
	{
		get
		{
			if (_SelectedModule == null)
			{
				byte[] array = owner.UserData["SelectedModuleID"];
				if (array != null)
				{
					string text = Encoding.ASCII.GetString(array);
					foreach (object frontModule in Owner.FrontModules)
					{
						if (frontModule is VmModule { ID: var iD } vmModule && iD.ToString() == text)
						{
							_SelectedModule = vmModule;
						}
					}
				}
			}
			return _SelectedModule;
		}
		set
		{
			if (_SelectedModule != value)
			{
				if (value != null)
				{
					owner.SetPropertyValueByName("SelectedModule", value);
					owner.UserData["SelectedModuleID"] = Encoding.ASCII.GetBytes(value.ID.ToString());
				}
				_SelectedModule = value;
				OnPropertyChanged("SelectedModule");
			}
		}
	}

	public virtual VmIOCollection TargetVmIOs => null;

	public virtual string DefaultStartName => null;

	public virtual object SelectedType
	{
		get
		{
			if (selectedType == null)
			{
				selectedType = (InputOutputTypes[0] as Tuple<string, object>).Item2;
			}
			return selectedType;
		}
		set
		{
			if (selectedType != value)
			{
				selectedType = value;
				OnPropertyChanged("SelectedType");
			}
		}
	}

	public virtual IList InputOutputTypes
	{
		get
		{
			if (_InputOutputTypes == null)
			{
				object[] first = new object[1]
				{
					new Tuple<string, object>("多选", obj)
				};
				IEnumerable<Tuple<string, object>> first2 = from p in Enum.GetValues(typeof(LeafIOTypeEnum)).OfType<LeafIOTypeEnum>()
					where p.ToString() == "INT" || p.ToString() == "FLOAT"
					select new Tuple<string, object>("%" + p.ToString().ToLower() + "%", p);
				IEnumerable<Tuple<string, object>> second = from p in Enum.GetValues(typeof(CombinationIOTypeEnum)).OfType<CombinationIOTypeEnum>()
					where p.ToString() == "ROIBOX" || p.ToString() == "POINT" || p.ToString() == "LINE" || p.ToString() == "Rect" || p.ToString() == "ROIPOLYGON" || p.ToString() == "ELLIPSE" || p.ToString() == "ROIANNULUS"
					select new Tuple<string, object>("%" + DynamicIO.GetDefaultObjectName(p) + "%", p);
				_InputOutputTypes = first.Concat(first2.Concat(second)).ToList();
			}
			return _InputOutputTypes;
		}
	}

	public IList ItemsA
	{
		get
		{
			if (_ItemsA == null)
			{
				if (TargetVmIOs == null)
				{
					return null;
				}
				_ItemsA = new ObservableCollection<VmIODecorate>(from p in TargetVmIOs.OfType<DynamicVmIO>()
					where p.Visible
					select new VmIODecorate
					{
						DecorateSource = p,
						Owner = this
					});
			}
			return _ItemsA;
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
			DelegateCommand val = _AddCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)AddCommandCmdExecute);
				DelegateCommand val3 = val2;
				_AddCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public Setter ChangedCommandSetterA
	{
		get
		{
			return ChangedCommandSetter;
		}
		set
		{
			ChangedCommandSetter = value;
			OnPropertyChanged("ChangedCommandSetterA");
		}
	}

	IList IChildren.Children => ItemsA;

	public IList CommonItems => ItemsA;

	public virtual string CurrentParamName => null;

	public string GetDefaultStartName(bool isOutput)
	{
		if (isOutput)
		{
			return "out";
		}
		return "in";
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		foreach (IButtonSelecter item in CommonItems.OfType<IButtonSelecter>())
		{
			if (item?.Selecter != null)
			{
				item.Selecter.InniterBusiness();
			}
		}
	}

	private void AddCommandCmdExecute()
	{
		if (Owner == null || TargetVmIOs == null)
		{
			return;
		}
		if (ItemsA.Count >= 10)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " :" + 10, MessageButtons.Normal, MessageIcons.Warning);
			return;
		}
		if (SelectedType == obj)
		{
			IOFilterPickData iOFilterPickData = new IOFilterPickData();
			iOFilterPickData.TargetVmIOs = TargetVmIOs;
			iOFilterPickData.SelectedModule = SelectedModule;
			iOFilterPickData.Node = Owner;
			iOFilterPickData.Init();
			Picker picker = new Picker
			{
				DataContext = iOFilterPickData
			};
			picker.ShowDialog();
			if (picker.DialogResult ?? false)
			{
				int num = 0;
				num = picker.CheckedLeafNodeList.Count;
				if (CurrentParamName.Equals("DynamicInputParam"))
				{
					int count = ItemsA.Count;
					if (count + num > 10)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " :" + 10, MessageButtons.Normal, MessageIcons.Warning);
						LogHelper.Error(string.Format("{0} Existed input io count: {1}, add new io count: {2}", AppLocalizationService.GetLang("Io数量超出最大限制！"), count.ToString(), num.ToString()));
						return;
					}
				}
				else if (CurrentParamName.Equals("DynamicOutputParam"))
				{
					int count2 = ItemsA.Count;
					if (count2 + num > 10)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " :" + 10, MessageButtons.Normal, MessageIcons.Warning);
						LogHelper.Error(string.Format("{0} Existed output io count: {1}, add new io count: {2}", AppLocalizationService.GetLang("Io数量超出最大限制！"), count2.ToString(), num.ToString()));
						return;
					}
				}
				List<string> list = new List<string> { "int", "float" };
				foreach (VmIO checkedLeafNode in picker.CheckedLeafNodeList)
				{
					bool flag = false;
					foreach (object inputOutputType in _InputOutputTypes)
					{
						string text = (inputOutputType as Tuple<string, object>).Item2.ToString();
						if (text.ToLower() == checkedLeafNode.TypeName.ToLower())
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						HikMessageBox.Show(string.Format("{0}:{1}", AppLocalizationService.GetLang("不支持当前类型"), checkedLeafNode.TypeName));
						continue;
					}
					VmIO source = null;
					if (checkedLeafNode.IsLeafNode)
					{
						source = AddLeafIO(checkedLeafNode.TypeName, Owner, TargetVmIOs, checkedLeafNode, picker.IsNeedIncludeModuleName);
					}
					else
					{
						source = AddCombinationIO(checkedLeafNode.TypeName, Owner, TargetVmIOs, checkedLeafNode, picker.IsNeedIncludeModuleName);
					}
					source.RelateIO = checkedLeafNode;
					ItemsA.OfType<VmIODecorate>().FirstOrDefault((VmIODecorate p) => p.DecorateSource == source)?.FilterSelecter.InniterBusiness();
				}
			}
		}
		else if (SelectedType is LeafIOTypeEnum leafIOTypeEnum)
		{
			AddLeafIO(leafIOTypeEnum.ToString().ToLower(), Owner, TargetVmIOs);
			if (Enumerable.Count(owner.Inputs.GetAllLeafIO()) > 1024 || Enumerable.Count(owner.Outputs.GetAllLeafIO()) > 1024)
			{
				TargetVmIOs.Last().RemoveSelf();
				ItemsA.RemoveAt(ItemsA.Count - 1);
				HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " :" + 10, MessageButtons.Normal, MessageIcons.Warning);
				return;
			}
		}
		else if (SelectedType is CombinationIOTypeEnum combinationIOTypeEnum)
		{
			AddCombinationIO(combinationIOTypeEnum.ToString(), Owner, TargetVmIOs);
			if (Enumerable.Count(owner.Inputs.GetAllLeafIO()) > 1024 || Enumerable.Count(owner.Outputs.GetAllLeafIO()) > 1024)
			{
				TargetVmIOs.Last().RemoveSelf();
				ItemsA.RemoveAt(ItemsA.Count - 1);
				HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " :" + 10, MessageButtons.Normal, MessageIcons.Warning);
				return;
			}
		}
		DataFilterConfig dataFilterConfig = ((Owner.Params.Data as AlgorithmTabRoot).Tabs[1] as Tab).Collection[0] as DataFilterConfig;
		dataFilterConfig.SetParamUnitInput();
	}

	public void SetIoParams()
	{
		try
		{
			(Owner as IVMGroup)?.SetIoParams();
			(Owner as IVmProcedure)?.SetIoParams();
		}
		catch (VmException vmException)
		{
			ShowMessage.ShowVmExceptionMessage(vmException);
		}
	}

	public static void FixName(ref string name)
	{
		if (name.Length > 32)
		{
			name = name.Substring(0, 32);
		}
	}

	protected VmIO AddLeafIO(string typeName, object parent, IList parentList, VmIO relateIO = null, bool IsNeedIncludeModuleName = false, string suggestName = null)
	{
		string name = ((relateIO == null) ? (suggestName + DefaultStartName) : ((IsNeedIncludeModuleName ? (relateIO.Root.Name + "_") : "") + AppLocalizationService.GetLang(relateIO.Name)));
		FixName(ref name);
		DataFilterDynamicVmIO dataFilterDynamicVmIO = new DataFilterDynamicVmIO
		{
			Name = GetUniqueName(name),
			SelectedModule = SelectedModule,
			Root = Owner,
			Parent = parent,
			TypeName = typeName,
			IsResultShow = true
		};
		parentList.Add(dataFilterDynamicVmIO);
		if (parent is IVmModule)
		{
			ItemsA.Add(new VmIODecorate
			{
				DecorateSource = dataFilterDynamicVmIO,
				Owner = this
			});
		}
		return dataFilterDynamicVmIO;
	}

	protected VmIO AddCombinationIO(string typeName, object parent, IList parentList, VmIO relateIO = null, bool IsNeedIncludeModuleName = false)
	{
		CombinRoot instance = CombinRoot.GetInstance();
		Combination combination = instance.Items.OfType<Combination>().FirstOrDefault((Combination p) => p.Style == typeName);
		VmIO res = AddIOFromCombination(combination, parent, parentList, relateIO, IsNeedIncludeModuleName);
		if (res == null)
		{
			return null;
		}
		VmIODecorate vmIODecorate = ItemsA.OfType<VmIODecorate>().Single((VmIODecorate p) => p.DecorateSource == res);
		if (res.TypeName == "IMAGE" || res.TypeName == "PIXELIMAGE")
		{
			vmIODecorate.IsDisplayForImage = true;
		}
		return res;
	}

	private VmIO AddIOFromCombination(object obj, object parent, IList parentList, VmIO relateIO = null, bool IsNeedIncludeModuleName = false)
	{
		if (obj is Combination combination)
		{
			string name = ((relateIO == null) ? combination.Name : ((IsNeedIncludeModuleName ? (relateIO.Root.Name + "_") : "") + AppLocalizationService.GetLang(combination.Name)));
			FixName(ref name);
			DynamicVmIO dynamicVmIO = new DynamicVmIO
			{
				Name = GetUniqueName(name),
				Root = Owner,
				Parent = parent,
				TypeName = combination.Style
			};
			parentList.Add(dynamicVmIO);
			if (parent is VmModule)
			{
				ItemsA.Add(new VmIODecorate
				{
					DecorateSource = dynamicVmIO,
					Owner = this
				});
			}
			foreach (object filter2 in combination.Filters)
			{
				AddIOFromCombination(filter2, dynamicVmIO, dynamicVmIO.Childs);
			}
			return dynamicVmIO;
		}
		if (obj is Filter filter)
		{
			AddLeafIO(filter.ValueTypeA, parent, parentList, relateIO, IsNeedIncludeModuleName, filter.NameA);
		}
		return null;
	}

	private string GetUniqueName(string startName)
	{
		IEnumerable<string> source = from p in Owner.Inputs.GetAllIO().Concat(Owner.Outputs.GetAllIO())
			select p.Name;
		string text = startName;
		if (!source.Contains("%" + text + "%"))
		{
			return text;
		}
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

	internal void ChangedCommandExecute()
	{
		if (ChangedCommandSetter != null)
		{
			if (ChangedCommandSetter.Operation == null)
			{
				ChangedCommandSetter.CreatOperateInstance();
			}
			if (ChangedCommandSetter.Operation != null)
			{
				ChangedCommandSetter.Operate(null);
			}
		}
	}

	public override void XmlLoadedTask()
	{
		base.XmlLoadedTask();
		ChangedCommandSetter?.Init();
	}

	void IUpdateGroupItems.SetItemsNull()
	{
		_ItemsA = null;
	}
}
