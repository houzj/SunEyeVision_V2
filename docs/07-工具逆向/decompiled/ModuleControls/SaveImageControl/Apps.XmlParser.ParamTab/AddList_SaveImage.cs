using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Apps.Localization;
using Apps.Log;
using VM.Core;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class AddList_SaveImage : AddList
{
	public override string DisplayType => "AddList";

	public List<int> ModuleIds { get; set; } = new List<int>();

	public override void DoAdd(string value = null, string hashCode = null, int moduleId = -1)
	{
		if (value != null)
		{
			foreach (SubData item in EnumEntrysA)
			{
				IEnumerable<ButtonSelecter_SaveImageDisplayObject> source = item.EnumEntrysA.OfType<ButtonSelecter_SaveImageDisplayObject>();
				ButtonSelecter_SaveImageDisplayObject buttonSelecter_SaveImageDisplayObject = source.FirstOrDefault((ButtonSelecter_SaveImageDisplayObject p) => p.CurValueA == value);
				if (buttonSelecter_SaveImageDisplayObject != null)
				{
					return;
				}
			}
		}
		ButtonSelecter_SaveImageDisplayObject buttonSelecter_SaveImageDisplayObject2 = new ButtonSelecter_SaveImageDisplayObject();
		buttonSelecter_SaveImageDisplayObject2.SetPropertyValueByNameNotrigger("CurValueA", value);
		buttonSelecter_SaveImageDisplayObject2.DropOpenSetter = new Setter
		{
			TargetName = "EnumEntrys",
			OperationName = "GetFrontDisplayModuleOperation",
			OperationParams = "circle;point;line;rect;text;outlinepoint;micropoint;coordinate;group;polygon;annularsector;path;LineAngle;ellipse"
		};
		buttonSelecter_SaveImageDisplayObject2.DropOpenSetter.SetParentNode(buttonSelecter_SaveImageDisplayObject2);
		buttonSelecter_SaveImageDisplayObject2.Triggers = new ObservableCollection<object>();
		Trigger trigger = new Trigger
		{
			Property = "CurValue"
		};
		Setter setter = new Setter
		{
			OperationName = "SetFrontDisplayOperation",
			OperationParams = "circle;point;line;rect;text;outlinepoint;micropoint;coordinate;group;polygon;annularsector;path;LineAngle;ellipse"
		};
		setter.CreatOperateInstance();
		trigger.Setters = new ObservableCollection<object>();
		trigger.Setters.Add(setter);
		setter.SetParentNode(trigger);
		buttonSelecter_SaveImageDisplayObject2.Triggers.Add(trigger);
		trigger.SetParentNode(buttonSelecter_SaveImageDisplayObject2);
		if (moduleId > -1)
		{
			buttonSelecter_SaveImageDisplayObject2.ModuleId = moduleId;
		}
		ButtonSelecter_Edit_DisplayObject buttonSelecter_Edit_DisplayObject = new ButtonSelecter_Edit_DisplayObject();
		buttonSelecter_Edit_DisplayObject.DropOpenSetter = new Setter
		{
			TargetName = "EnumEntrys",
			OperationName = "GetModuleDisplayList_FrontDisplayOperation",
			OperationParams = "circle;point;line;rect;text;outlinepoint;micropoint;coordinate;group;polygon;annularsector;path;LineAngle;ellipse"
		};
		buttonSelecter_Edit_DisplayObject.DropOpenSetter.SetParentNode(buttonSelecter_Edit_DisplayObject);
		SubData subData = new SubData
		{
			EnumEntrysA = new ObservableCollection<object> { buttonSelecter_SaveImageDisplayObject2, buttonSelecter_Edit_DisplayObject }
		};
		buttonSelecter_SaveImageDisplayObject2.SetParentNode(subData);
		buttonSelecter_Edit_DisplayObject.SetParentNode(subData);
		subData.SetParentNode(this);
		EnumEntrysA.Add(subData);
	}

	public override int GetParamUnit()
	{
		try
		{
			IUserStringData moduleTagInfo = GetModuleTagInfo();
			Value = moduleTagInfo[ComName];
			LogHelper.objLog.Info("ComName：" + ComName + " Value--" + Value);
			ParseValue();
			return 0;
		}
		catch (Exception ex)
		{
			LogHelper.Error("获取参数：" + ComName + "失败：" + ex.Message);
			return -1;
		}
	}

	private void ParseValue()
	{
		ModuleIds.Clear();
		EnumEntrysA.Clear();
		if (string.IsNullOrEmpty(Value) || !(GetTopBaseNode() is AlgorithmTabRoot { Owner: VmModule owner }))
		{
			return;
		}
		string[] array = Value.Split(' ');
		StringBuilder stringBuilder = new StringBuilder();
		string[] array2 = array;
		foreach (string s in array2)
		{
			int moduleId = -1;
			if (!int.TryParse(s, out moduleId))
			{
				continue;
			}
			VmModule moduleByID = owner.Root.GetModuleByID((uint)moduleId);
			if (moduleByID != null)
			{
				List<VmIO> source = owner.Inputs.Where((VmIO p) => p.Name.StartsWith("%" + moduleId + "_")).ToList();
				if (source.All((VmIO p) => p.RelateIO != null))
				{
					DoAdd(moduleByID.GetIdName());
					continue;
				}
				if (source.All((VmIO p) => p.RelateIO == null))
				{
					ModuleIds.Add(moduleId);
					continue;
				}
				stringBuilder.Append(moduleByID.ID + moduleByID.StrModuleName + ";");
				DoAdd(moduleByID.GetIdName());
			}
		}
		string text = stringBuilder.ToString();
		if (!string.IsNullOrEmpty(text))
		{
			text = AppLocalizationService.GetLang("Module ID") + ":" + text + "数据订阅不完整，需重新订阅";
			LogHelper.Info(text);
		}
	}
}
