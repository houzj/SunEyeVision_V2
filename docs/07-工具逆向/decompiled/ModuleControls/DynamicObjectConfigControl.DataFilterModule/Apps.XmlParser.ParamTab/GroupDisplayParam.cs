using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using DynamicObjectConfigControl;
using DynamicObjectConfigControl.UI.Data;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GroupDisplayParam : CommonParam, IUpdateGroupItems
{
	private VmModule owner;

	private IList<object> targetObjects;

	private IList _DisplayItemsA;

	private object selectedType = obj;

	private static object obj = new object();

	protected IList _DisplayTypes;

	private DelegateCommand _AddCommand = null;

	public VmModule Owner
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

	public IList<object> TargetObjects
	{
		get
		{
			if (targetObjects == null)
			{
				targetObjects = (Owner["[Display]"] as ParamRoot).GetDisplayList();
			}
			return targetObjects;
		}
	}

	public IList DisplayItemsA
	{
		get
		{
			if (_DisplayItemsA == null && TargetObjects != null)
			{
				List<DisplayObjectDecorate> list = new List<DisplayObjectDecorate>(from p in TargetObjects.OfType<DynamicObject>()
					select CreatNewDecorate(p));
				_DisplayItemsA = new ObservableCollection<DisplayObjectDecorate>(list);
			}
			return _DisplayItemsA;
		}
	}

	public object SelectedType
	{
		get
		{
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

	public virtual IList DisplayTypes
	{
		get
		{
			if (_DisplayTypes == null)
			{
				object[] first = new object[1]
				{
					new Tuple<string, object>("多选", obj)
				};
				IEnumerable<Tuple<string, object>> second = from p in Enum.GetValues(typeof(ObjectTypeEnum)).OfType<ObjectTypeEnum>()
					select new Tuple<string, object>(GetDefaultObjectName(p), p);
				_DisplayTypes = first.Concat(second).ToList();
			}
			return _DisplayTypes;
		}
	}

	public ICommand AddCommand
	{
		get
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			//IL_0024: Expected Obj, but got Unknown
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

	public string CurrentParamName => "DynamicDisplayParam";

	public override IList CommonItems => DisplayItemsA;

	public void UpdateDisplayItems()
	{
		_DisplayItemsA = null;
		OnPropertyChanged("DisplayItemsA");
	}

	protected static string GetDefaultObjectName(ObjectTypeEnum type)
	{
		Type type2 = type.GetType();
		return ((type2.GetField(type.ToString())?.GetCustomAttribute(typeof(DisplayAttribute)) is DisplayAttribute displayAttribute) ? displayAttribute.Name : null) ?? "Shape";
	}

	protected virtual void AddCommandCmdExecute()
	{
		if (SelectedType == GroupDisplayParam.obj)
		{
			DisplayPickData displayPickData = new DisplayPickData();
			displayPickData.Node = Owner;
			displayPickData.Init();
			Picker picker = new Picker
			{
				DataContext = displayPickData
			};
			picker.ShowDialog();
			if (!(picker.DialogResult ?? false))
			{
				return;
			}
			int num = Enumerable.Count(owner.Outputs.GetAllLeafIO());
			int newIoCount = 0;
			picker.CheckedLeafNodeList.OfType<Object>().ToList().ForEach((Object p) =>
			{
				newIoCount += p.GetAllTopMappingIO().Sum((VmIO m) => Enumerable.Count(m.GetAllLeafIO()));
			});
			if (num + newIoCount > 1024)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Io数量超出最大限制！"));
				LogHelper.Error(string.Format("{0} Existed output io count: {1}, add new io count: {2}", AppLocalizationService.GetLang("Io数量超出最大限制！"), num.ToString(), newIoCount.ToString()));
				return;
			}
			{
				foreach (Object checkedLeafNode in picker.CheckedLeafNodeList)
				{
					DynamicObject dynamicObject = AddDynamicObject(Owner, (ObjectTypeEnum)Enum.Parse(typeof(ObjectTypeEnum), checkedLeafNode.Type), checkedLeafNode, picker.IsNeedIncludeModuleName);
					DisplayItemsA.Add(new DisplayObjectDecorate
					{
						DecorateSource = dynamicObject,
						Owner = this
					});
					SetInternalObjectToParentObjectOperation.SetRalationObject(dynamicObject, checkedLeafNode);
				}
				return;
			}
		}
		Object obj2 = DisplayRoot.GetInstance().Items.OfType<Object>().FirstOrDefault((Object p) => p.TypeA == Convert.ToString(selectedType));
		if (obj2 == null)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("操作不支持"));
			LogHelper.Error($"Get display object failed, type: {Convert.ToString(selectedType)}");
			return;
		}
		int num2 = Enumerable.Count(owner.Outputs.GetAllLeafIO());
		int count = obj2.FeaturesB.Count;
		if (num2 + count > 1024)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Io数量超出最大限制！"));
			LogHelper.Error(string.Format("{0} Existed output io count: {1}, add new io count: {2}", AppLocalizationService.GetLang("Io数量超出最大限制！"), num2.ToString(), count.ToString()));
			return;
		}
		DynamicObject newObject = AddDynamicObject(Owner, (ObjectTypeEnum)selectedType);
		DisplayObjectDecorate displayObjectDecorate = CreatNewDecorate(newObject);
		DisplayItemsA.Add(displayObjectDecorate);
		AddExcutedAfter(displayObjectDecorate);
	}

	protected virtual void AddExcutedAfter(DisplayObjectDecorate item)
	{
	}

	protected virtual DisplayObjectDecorate CreatNewDecorate(DynamicObject newObject)
	{
		return new DisplayObjectDecorate
		{
			DecorateSource = newObject,
			Owner = this
		};
	}

	public static DynamicObject AddDynamicObject(VmModule vmModule, ObjectTypeEnum type, Object relateObj = null, bool IsNeedIncludeModuleName = false)
	{
		ParamRoot paramRoot = vmModule["[Display]"] as ParamRoot;
		DynamicObject newObject = new DynamicObject();
		newObject.SetParentNode(paramRoot.CategorysA.First());
		string name = ((relateObj == null) ? AppLocalizationService.GetLang(GetDefaultObjectName(type)) : ((IsNeedIncludeModuleName ? ((relateObj.Owner as IVmModule).Name + "_") : "") + AppLocalizationService.GetLang(relateObj.Name)));
		GroupParam.FixName(ref name);
		newObject.Name = GetUniqueName(vmModule, name);
		newObject.TypeA = type.ToString();
		newObject.IsDisplayA = true;
		Object obj = DisplayRoot.GetInstance().Items.OfType<Object>().FirstOrDefault((Object p) => p.TypeA == newObject.TypeA);
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
		return newObject;
	}

	private static string GetUniqueName(VmModule vmModule, string startName)
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

	void IUpdateGroupItems.SetItemsNull()
	{
		_DisplayItemsA = null;
		targetObjects = null;
	}
}
