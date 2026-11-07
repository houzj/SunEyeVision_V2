using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using DynamicObjectConfigControl.UI.Data;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace DynamicObjectConfigControl;

public class GroupParam : CommonParam, IChildren, IUpdateGroupItems
{
	private VmModuleSet owner;

	private object selectedType;

	private static object obj = new object();

	protected IList _InputOutputTypes;

	private IList _ItemsA;

	private DelegateCommand _AddCommand = null;

	public Setter ChangedCommandSetter;

	internal VmModuleSet Owner
	{
		get
		{
			if (owner == null)
			{
				owner = (GetTopBaseNode() as AlgorithmTabRoot).Owner as VmModuleSet;
			}
			return owner;
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
					where p.ToString() != "image" && p.ToString() != "DOUBLE"
					select new Tuple<string, object>("%" + p.ToString().ToLower() + "%", p);
				IEnumerable<Tuple<string, object>> enumerable = from p in (from x in Enum.GetValues(typeof(CombinationIOTypeEnum)).OfType<CombinationIOTypeEnum>()
						where x.ToString() != "CIRCLE"
						select x).Except(new List<CombinationIOTypeEnum> { CombinationIOTypeEnum.ANNULUS })
					select new Tuple<string, object>("%" + DynamicIO.GetDefaultObjectName(p) + "%", p);
				if (CheckAdditionExist.IsExistAddition)
				{
					IEnumerable<Tuple<string, object>> second = from p in Enum.GetValues(typeof(CombinationIOTypeEnumEx)).OfType<CombinationIOTypeEnumEx>()
						where p.ToString() != "CIRCLE"
						select new Tuple<string, object>("%" + DynamicIO.GetDefaultObjectName(p) + "%", p);
					_InputOutputTypes = first.Concat(first2.Concat(enumerable.Concat(second))).ToList();
				}
				else
				{
					_InputOutputTypes = first.Concat(first2.Concat(enumerable)).ToList();
				}
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

	public bool IsShowAccumulatedOutput => (Owner is IMVSGroup && CurrentParamName.Equals("DynamicOutputParam")) ? true : false;

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

	public override IList CommonItems => ItemsA;

	public virtual string CurrentParamName => null;

	private void AddCommandCmdExecute()
	{
		if (Owner == null)
		{
			return;
		}
		if (ItemsA.Count >= 1024)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " :" + 1024, MessageButtons.Normal, MessageIcons.Warning);
			return;
		}
		if (SelectedType == obj)
		{
			IOPickData iOPickData = new IOPickData();
			iOPickData.TargetVmIOs = TargetVmIOs;
			iOPickData.Node = Owner;
			iOPickData.Init();
			Picker picker = new Picker
			{
				DataContext = iOPickData
			};
			picker.ShowDialog();
			if (picker.DialogResult == true)
			{
				int newIoCount = 0;
				picker.CheckedLeafNodeList.OfType<VmIO>().ToList().ForEach((VmIO p) =>
				{
					newIoCount += p.GetAllLeafIO().Count;
				});
				if (CurrentParamName.Equals("DynamicInputParam"))
				{
					int num = Enumerable.Count(owner.Inputs.GetAllLeafIO());
					if (num + newIoCount > 1024)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Io数量超出最大限制！"));
						LogHelper.Error(AppLocalizationService.GetLang("Io数量超出最大限制！") + " Existed input io count: " + num + ", add new io count: " + newIoCount);
						return;
					}
				}
				else if (CurrentParamName.Equals("DynamicOutputParam"))
				{
					int num2 = Enumerable.Count(owner.Outputs.GetAllLeafIO());
					if (num2 + newIoCount > 1024)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Io数量超出最大限制！"));
						LogHelper.Error(AppLocalizationService.GetLang("Io数量超出最大限制！") + " Existed output io count: " + num2 + ", add new io count: " + newIoCount);
						return;
					}
				}
				foreach (VmIO checkedLeafNode in picker.CheckedLeafNodeList)
				{
					VmIO source = null;
					if (checkedLeafNode.IsLeafNode)
					{
						source = AddLeafIO(checkedLeafNode.TypeName, Owner, TargetVmIOs, checkedLeafNode, picker.IsNeedIncludeModuleName);
					}
					else
					{
						source = AddCombinationIO(checkedLeafNode.TypeName, Owner, TargetVmIOs, checkedLeafNode, picker.IsNeedIncludeModuleName);
					}
					if (source != null)
					{
						source.RelateIO = checkedLeafNode;
						ItemsA.OfType<VmIODecorate>().FirstOrDefault((VmIODecorate p) => p.DecorateSource == source)?.FilterSelecter.InniterBusiness();
					}
					else
					{
						if (checkedLeafNode.IsLeafNode)
						{
							continue;
						}
						foreach (VmIO item in checkedLeafNode.GetAllLeafIO())
						{
							VmIO subSource = AddLeafIO(item.TypeName, Owner, TargetVmIOs, item, picker.IsNeedIncludeModuleName);
							subSource.RelateIO = item;
							ItemsA.OfType<VmIODecorate>().FirstOrDefault((VmIODecorate p) => p.DecorateSource == subSource)?.FilterSelecter.InniterBusiness();
						}
					}
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
				HikMessageBox.Show(AppLocalizationService.GetLang("Io数量超出最大限制！"));
				return;
			}
		}
		else if (SelectedType is CombinationIOTypeEnum || SelectedType is CombinationIOTypeEnumEx)
		{
			AddCombinationIO(SelectedType.ToString(), Owner, TargetVmIOs);
			if (Enumerable.Count(owner.Inputs.GetAllLeafIO()) > 1024 || Enumerable.Count(owner.Outputs.GetAllLeafIO()) > 1024)
			{
				TargetVmIOs.Last().RemoveSelf();
				ItemsA.RemoveAt(ItemsA.Count - 1);
				HikMessageBox.Show(AppLocalizationService.GetLang("Io数量超出最大限制！"));
				return;
			}
		}
		SetIoParams();
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

	private VmIO AddLeafIO(string typeName, object parent, IList parentList, VmIO relateIO = null, bool IsNeedIncludeModuleName = false, string suggestName = null)
	{
		string name = ((relateIO == null) ? (suggestName + DefaultStartName) : ((IsNeedIncludeModuleName ? (relateIO.Root.Name + "_") : "") + AppLocalizationService.GetLang(relateIO.Name)));
		FixName(ref name);
		DynamicVmIO dynamicVmIO = new DynamicVmIO
		{
			Name = GetUniqueName(name),
			Root = Owner,
			Parent = parent,
			TypeName = typeName
		};
		parentList.Add(dynamicVmIO);
		if (parent is IVmModuleSet)
		{
			ItemsA.Add(new VmIODecorate
			{
				DecorateSource = dynamicVmIO,
				Owner = this
			});
		}
		return dynamicVmIO;
	}

	private VmIO AddCombinationIO(string typeName, object parent, IList parentList, VmIO relateIO = null, bool IsNeedIncludeModuleName = false)
	{
		CombinRoot instance = CombinRoot.GetInstance();
		Combination combination = instance.Items.OfType<Combination>().FirstOrDefault((Combination p) => p.Style == typeName);
		if (combination == null)
		{
			return null;
		}
		VmIO res = AddIOFromCombination(combination, parent, parentList, relateIO, IsNeedIncludeModuleName);
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
