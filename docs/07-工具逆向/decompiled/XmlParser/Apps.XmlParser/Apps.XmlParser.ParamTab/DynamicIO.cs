using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class DynamicIO : BaseNode, INotifyPropertyChanged, IOutputTree, IPropertyChanged
{
	private const int MAX_OUTPUT_COUNT = 600;

	private const int MAX_NAME_LEN = 32;

	protected string[] CombineType = new string[17]
	{
		"IMAGE", "ROIBOX", "ROIANNULUS", "ROIPOLYGON", "POINT", "LINE", "FIXTURE", "CIRCLE", "ANNULUS", "Rect",
		"ELLIPSE", "COORDINATE", "STEREOIMAGE", "POINT3D", "POINTCLOUD", "Matrix4", "POSEINFO"
	};

	private string index;

	protected ButtonSelecter _FilterSelecter;

	private bool canDelete = false;

	private DelegateCommand deeteIOCommand = null;

	private DelegateCommand<string> _TextChangedCommand = null;

	public static int outputNumber = 0;

	public static bool overRun = false;

	public bool outputEnabled;

	private bool IsAdded = true;

	public static bool getName = true;

	public static List<DynamicIO> Filters;

	public readonly string[] allType = new string[6] { "int", "float", "string", "double", "image", "pointset" };

	private IOutputTree _parentTree;

	private bool? checkEnabled = null;

	public VmIO IoDataSource { get; set; }

	public virtual List<string> IoSelectValueTypes => new List<string> { "int", "float", "string", "IMAGE", "byte", "double" };

	public virtual int LengthA => 32;

	public virtual string NameA
	{
		get
		{
			return IoDataSource.Name;
		}
		set
		{
			if (string.IsNullOrEmpty(value))
			{
				OnPropertyChanged("NameA");
				return;
			}
			string tmpValue = AppLocalizationService.UnDoTranslate(value);
			if (!(tmpValue != IoDataSource.Name))
			{
				return;
			}
			if (!string.IsNullOrEmpty(tmpValue) && !IoDataSource.Root.Inputs.GetAllIO().Exists((VmIO p) => p.Name == tmpValue) && !IoDataSource.Root.Outputs.GetAllIO().Exists((VmIO p) => p.Name == tmpValue))
			{
				string name = IoDataSource.Name;
				try
				{
					string text = ((IVmIO)IoDataSource).UnBindingGlobalVariable();
					IoDataSource.Name = tmpValue;
					IoDataSource.OnPropertyChanged("Name");
					SetParamUnit();
					if (!string.IsNullOrEmpty(text))
					{
						IoDataSource.BindingGlobalVariable(text);
					}
				}
				catch (Exception ex)
				{
					LogHelper.Error("更新全局变量绑定关系失败：" + ex.Message);
				}
			}
			OnPropertyChanged("NameA");
			OnPropertyChanged("FilterSelecter");
		}
	}

	public string RealNameA => IoDataSource.RealName();

	public string IndexA
	{
		get
		{
			return index;
		}
		set
		{
			if (value != index)
			{
				index = value;
				OnPropertyChanged("IndexA");
			}
		}
	}

	public bool CustomVisibleA
	{
		get
		{
			return IoDataSource.CustomVisibleA;
		}
		set
		{
			if (IoDataSource.CustomVisibleA != value)
			{
				IoDataSource.CustomVisibleA = value;
				OnPropertyChanged("CustomVisibleA");
			}
		}
	}

	public virtual string ValueTypeA
	{
		get
		{
			return IoDataSource.TypeName;
		}
		set
		{
			if (value != IoDataSource.TypeName)
			{
				IoDataSource.TypeName = value;
				if (value.Equals("IMAGE"))
				{
					AddImageObject();
				}
				else
				{
					IoDataSource.Childs = new VmIOCollection();
					RemoveImageObject(IoDataSource);
				}
				try
				{
					((IVmIO)IoDataSource).UnBindingGlobalVariable();
				}
				catch (Exception ex)
				{
					LogHelper.Error("解除全局变量绑定关系失败：" + ex.Message);
				}
				SetParamUnit();
				OnPropertyChanged("ValueTypeA");
				OnPropertyChanged("FilterSelecter");
			}
		}
	}

	public virtual string FilterTypeA => ValueTypeA;

	public virtual ButtonSelecter FilterSelecter
	{
		get
		{
			if (_FilterSelecter == null)
			{
				_FilterSelecter = new ButtonSelecter();
				if (CombineType.Contains(ValueTypeA))
				{
					_FilterSelecter.DropOpenSetter = new Setter
					{
						TargetName = "EnumEntrys",
						OperationName = "GetFrontCombinationTreeOperation"
					};
					_FilterSelecter.DropOpenSetter.SetParentNode(_FilterSelecter);
					_FilterSelecter.SetParentNode(this);
					_FilterSelecter.Triggers = new ObservableCollection<object>();
					Trigger trigger = new Trigger
					{
						Property = "CurValue"
					};
					Setter setter = new Setter
					{
						OperationName = "SetCombinationSourceOperation",
						OperationParams = NameA
					};
					setter.CreatOperateInstance();
					trigger.Setters = new ObservableCollection<object>();
					trigger.Setters.Add(setter);
					setter.SetParentNode(trigger);
					_FilterSelecter.Triggers.Add(trigger);
					trigger.SetParentNode(_FilterSelecter);
					_FilterSelecter.Initers = new ObservableCollection<object>();
					Setter setter2 = new Setter
					{
						TargetName = "CurValue",
						OperationName = "GetSelectedCombinationOperation",
						OperationParams = NameA
					};
					setter2.CreatOperateInstance();
					_FilterSelecter.Initers.Add(setter2);
					setter2.SetParentNode(_FilterSelecter);
				}
				else
				{
					_FilterSelecter.DropOpenSetter = new Setter
					{
						TargetName = "EnumEntrys",
						OperationName = "GetFrontFilterTreeOperation"
					};
					_FilterSelecter.DropOpenSetter.SetParentNode(_FilterSelecter);
					_FilterSelecter.SetParentNode(this);
					_FilterSelecter.Triggers = new ObservableCollection<object>();
					Trigger trigger2 = new Trigger
					{
						Property = "CurValue"
					};
					Setter setter3 = new Setter
					{
						OperationName = "SetFilterSourceOperation",
						OperationParams = NameA
					};
					setter3.CreatOperateInstance();
					trigger2.Setters = new ObservableCollection<object>();
					trigger2.Setters.Add(setter3);
					setter3.SetParentNode(trigger2);
					_FilterSelecter.Triggers.Add(trigger2);
					trigger2.SetParentNode(_FilterSelecter);
					_FilterSelecter.Initers = new ObservableCollection<object>();
					Setter setter4 = new Setter
					{
						TargetName = "CurValue",
						OperationName = "GetSelectedFilterOperation",
						OperationParams = NameA
					};
					setter4.CreatOperateInstance();
					_FilterSelecter.Initers.Add(setter4);
					setter4.SetParentNode(_FilterSelecter);
				}
			}
			if (_FilterSelecter.DropOpenSetter.OperationParams != null && _FilterSelecter.DropOpenSetter.OperationParams != FilterTypeA)
			{
				_FilterSelecter.CurValueA = "";
			}
			_FilterSelecter.Triggers.OfType<Trigger>().First().Setters.OfType<Setter>().First().OperationParams = NameA;
			_FilterSelecter.Initers.OfType<Setter>().First().OperationParams = NameA;
			_FilterSelecter.InniterBusiness();
			_FilterSelecter.DropOpenSetter.OperationParams = FilterTypeA;
			return _FilterSelecter;
		}
	}

	public bool CanDeleteA
	{
		get
		{
			return canDelete;
		}
		set
		{
			if (canDelete != value)
			{
				canDelete = value;
				OnPropertyChanged("CanDeleteA");
			}
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = deeteIOCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)DeleteIOCommandExecute);
				DelegateCommand val3 = val2;
				deeteIOCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand TextChangedCommand => (ICommand)(_TextChangedCommand ?? (_TextChangedCommand = new DelegateCommand<string>((Action<string>)TextChangedExecute)));

	public string FullNameA
	{
		get
		{
			string[] array = IoDataSource.FullName.Split('.');
			string text = "";
			for (int i = 0; i < array.Length - 1; i++)
			{
				text += array[i];
				text += ".";
			}
			string text2 = array[array.Length - 1];
			if (!string.IsNullOrEmpty(text2) && text2.First() == '(' && text2.Last() == ')')
			{
				text2 = text2.Substring(1, text2.Length - 2);
			}
			return text + AppLocalizationService.GetLang(text2);
		}
	}

	public bool OutputEnabled
	{
		get
		{
			return outputEnabled;
		}
		set
		{
			if (value == outputEnabled)
			{
				return;
			}
			if (!value)
			{
				outputNumber--;
				overRun = false;
			}
			if (overRun)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SetCnfg"), AppLocalizationService.GetLang("超限"), MessageIcons.Error);
				return;
			}
			if (value)
			{
				if (outputNumber >= 600)
				{
					overRun = true;
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SetCnfg"), AppLocalizationService.GetLang("超限"), MessageIcons.Error);
					return;
				}
				outputNumber++;
				IoDataSource.CustomName = (getName ? RulerName() : CustomName);
			}
			outputEnabled = value;
			ServerSetOutput(CustomName, OutputEnabled);
			OnPropertyChanged("OutputEnabled");
			OnPropertyChangedParentTree("OutputEnabled");
			OnPropertyChanged("CustomName");
		}
	}

	public string DisplayNameA
	{
		get
		{
			return AppLocalizationService.GetLang(IoDataSource.Name);
		}
		set
		{
			if (IoDataSource.Name != value)
			{
				IoDataSource.Name = value;
				OnPropertyChanged("DisplayNameA");
			}
		}
	}

	public bool IsAddedA
	{
		get
		{
			return IsAdded;
		}
		set
		{
			if (IsAdded != value)
			{
				IsAdded = value;
			}
		}
	}

	public string CustomName
	{
		get
		{
			return IoDataSource.CustomName;
		}
		set
		{
			if (getName)
			{
				if (value != IoDataSource.CustomName && JudgeCustomName(value, isModify: true))
				{
					ServerSetOutput(CustomName, outputEnabled: false);
					IoDataSource.CustomName = value;
					OnPropertyChanged("CustomName");
					if (OutputEnabled)
					{
						ServerSetOutput(value, OutputEnabled);
					}
				}
			}
			else
			{
				IoDataSource.CustomName = value;
			}
		}
	}

	public int ValueTypeIndex
	{
		get
		{
			switch (ValueTypeA)
			{
			case "int":
			case "pixelformat":
				return 1;
			case "float":
				return 2;
			case "string":
				return 3;
			case "image":
				return 4;
			case "pointset":
			case "chunk":
				return 5;
			case "bytes":
				return 6;
			case "double":
				return 7;
			default:
				return 0;
			}
		}
	}

	public IOutputTree ParentTree
	{
		get
		{
			return _parentTree;
		}
		set
		{
			_parentTree = value;
		}
	}

	public bool? CheckEnabled
	{
		get
		{
			return checkEnabled;
		}
		set
		{
			if (value != checkEnabled)
			{
				checkEnabled = value;
				OnPropertyChanged("CheckEnabled");
				OnPropertyChangedParentTree("CheckEnabled");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public void AddImageObject()
	{
		if (!(IoDataSource.TypeName == "IMAGE"))
		{
			return;
		}
		DynamicObject dynamicObject = AddDynamicObject(IoDataSource.Root, ObjectTypeEnum.image);
		dynamicObject.RelateIO = IoDataSource;
		SetImageObjectMapByIO(dynamicObject, IoDataSource);
		foreach (FeatureBase item in dynamicObject.FeaturesB)
		{
			item.InitVmIOs();
		}
	}

	public void RemoveImageObject(VmIO vmIO)
	{
		try
		{
			if (vmIO != null && vmIO.TypeName.Equals("IMAGE"))
			{
				ParamRoot paramRoot = vmIO.Root["[Display]"] as ParamRoot;
				paramRoot.GetDisplayList().OfType<DynamicObject>().FirstOrDefault((DynamicObject p) => p.RelateIO == vmIO)?.RemoveSelf();
			}
		}
		catch
		{
		}
	}

	private DynamicObject AddDynamicObject(VmModule vmModule, ObjectTypeEnum type)
	{
		ParamRoot paramRoot = vmModule["[Display]"] as ParamRoot;
		DynamicObject newObject = new DynamicObject();
		newObject.SetParentNode(paramRoot.CategorysA.First());
		newObject.Name = GetUniqueName(vmModule, AppLocalizationService.GetLang(GetDefaultObjectName(type)));
		newObject.TypeA = type.ToString();
		newObject.IsDisplayA = true;
		Object obj = DisplayRoot.GetInstance().Items.OfType<Object>().FirstOrDefault((Object p) => p.TypeA == newObject.TypeA);
		if (obj != null)
		{
			newObject.Features = new ObservableCollection<FeatureBase>();
			newObject.okcolorA = obj.okcolor;
			newObject.ngcolorA = obj.ngcolor;
			newObject.hotcolor = obj.hotcolor;
			newObject.opacityA = obj.opacity;
			newObject.linetype = obj.linetype;
			newObject.linewidth = obj.linewidth;
			foreach (FeatureBase item in obj.FeaturesB)
			{
				newObject.Features.Add(item.Clone<Feature>());
				newObject.Features.Last().SetParentNode(newObject);
			}
			paramRoot.GetDisplayList().Add(newObject);
		}
		return newObject;
	}

	private string GetUniqueName(VmModule vmModule, string startName)
	{
		IEnumerable<string> source = from p in (vmModule["[Display]"] as ParamRoot).GetDisplayList().OfType<Object>()
			select p.NameA;
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

	public static string GetDefaultObjectName(Enum type)
	{
		Type type2 = type.GetType();
		FieldInfo field = type2.GetField(type.ToString());
		return ((DisplayAttribute)field.GetCustomAttribute(typeof(DisplayAttribute)))?.Name ?? type.ToString();
	}

	private void SetImageObjectMapByIO(Object displayObj, VmIO io)
	{
		List<VmIO> allLeafIO = io.GetAllLeafIO();
		displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Image").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("Image") && p.TypeName == "image").Name;
		displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Width").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ImageWidth")).Name;
		displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Height").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ImageHeight")).Name;
		displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PixelFormat").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ImagePixelFormat")).Name;
	}

	private void DeleteIOCommandExecute()
	{
		(GetParentNode() as IDynamic)?.RemoveIO(this);
	}

	public virtual void TextChangedExecute(string newName)
	{
		NameA = newName;
	}

	private void ServerSetOutput(string customName, bool outputEnabled)
	{
		VmProcedure root = IoDataSource.Root.Root;
		if (root != null)
		{
			ImvsSdkDefine.IMVS_CUSTOM_SUBSCRIBE_INFO customOutputSubscribeInfo = new ImvsSdkDefine.IMVS_CUSTOM_SUBSCRIBE_INFO
			{
				strInputParamName = StringUtil.UTF8GetFixLenBytes(customName, 256),
				strOutputParamName = StringUtil.UTF8GetFixLenBytes(NameA, 256),
				nIfDelete = ((!outputEnabled) ? 1 : 0),
				nOutputType = ValueTypeIndex,
				nSubProcessID = (int)root.ID,
				nPubModuleID = (int)IoDataSource.Root.ID
			};
			if (!outputEnabled)
			{
				customOutputSubscribeInfo.nIfDelete = 0;
				ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetCustomOutputSubscribeInfo(customOutputSubscribeInfo);
				customOutputSubscribeInfo.nIfDelete = ((!outputEnabled) ? 1 : 0);
				ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetCustomOutputSubscribeInfo(customOutputSubscribeInfo);
			}
			else
			{
				ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetCustomOutputSubscribeInfo(customOutputSubscribeInfo);
			}
		}
	}

	private bool JudgeCustomName(string value, bool isModify)
	{
		if (value.Length > 32)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LengthLimit32"), MessageIcons.Error);
			return false;
		}
		if (string.IsNullOrEmpty(value) || Filters?.Find((DynamicIO p) => p.OutputEnabled && p.CustomName == value) != null)
		{
			if (isModify)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NullOrRepeat"), MessageIcons.Error);
			}
			return false;
		}
		string text = "~!@$%^&*(){}[]\\|/?<>,，！";
		foreach (char value2 in text)
		{
			if (value.Contains(value2))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ContainsIllegal"), MessageIcons.Error);
				return false;
			}
		}
		return true;
	}

	public string RulerName(int i = 0)
	{
		string name = IoDataSource.Name;
		name += i;
		if (!JudgeCustomName(name, isModify: false))
		{
			name = RulerName(++i);
		}
		return name;
	}

	public void OnPropertyChangedParentTree(string name)
	{
		ParentTree?.OnPropertyChanged(name);
		ParentTree?.OnPropertyChangedParentTree(name);
	}

	public virtual int SetParamUnit()
	{
		return 0;
	}

	public void UpdateRunParamSubscribe(string oldName, string newName)
	{
		VmModule module = IoDataSource.Root;
		if (module == null)
		{
			return;
		}
		VmProcedure root = module.Root;
		if (root == null)
		{
			return;
		}
		foreach (VmModule module2 in root.Modules)
		{
			if (module2 == module || !(module2.Params.Data is AlgorithmTabRoot { Tabs: var tabs }))
			{
				continue;
			}
			(from q in tabs.SelectMany((object p) => (p as Tab).Collection)
				where q is ISubChild || q is ISubInfo
				select q).ToList().ForEach((object x) =>
			{
				if (x is ISubInfo { IsSubA: not false } subInfo)
				{
					UpdateBinding(subInfo, module, oldName, newName);
				}
				else if (x is ISubChild subChild)
				{
					foreach (object item in subChild.SubChildren.ToList())
					{
						if (item is ISubInfo { IsSubA: not false } subInfo2)
						{
							UpdateBinding(subInfo2, module, oldName, newName);
						}
					}
				}
			});
		}
	}

	private void UpdateBinding(ISubInfo subInfo, VmModule module, string oldName, string newName)
	{
		ButtonSelecter subData = subInfo.SubData;
		string curValueA = subData.CurValueA;
		if (!string.IsNullOrEmpty(curValueA))
		{
			string[] array = curValueA.Split(' ');
			if (array.Length != 0 && module.ID.ToString() == array[0] && curValueA.Contains(oldName))
			{
				string curValueA2 = curValueA.Replace(oldName, newName);
				subData.CurValueA = curValueA2;
			}
		}
	}
}
