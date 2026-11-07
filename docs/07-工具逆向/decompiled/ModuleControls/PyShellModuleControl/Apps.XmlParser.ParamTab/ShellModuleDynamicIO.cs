using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Apps.Interface;
using ShellCommonControl;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class ShellModuleDynamicIO : DynamicIOContainer<ShellDynamicIO>
{
	private IList<object> targetObjects;

	public IList<object> TargetObjects
	{
		get
		{
			if (targetObjects == null)
			{
				targetObjects = ((Owner as VmModule)["[Display]"] as ParamRoot).GetDisplayList();
			}
			return targetObjects;
		}
	}

	public override bool IsInitAdd => true;

	public override int MaxItemCount => 65535;

	public ShellModuleDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	public ShellModuleDynamicIO()
	{
	}

	public override object AddIO(string name = null, string type = null, int index = -1)
	{
		object obj = base.AddIO(name, type, index);
		AddDisplayObject((obj as DynamicIO).IoDataSource);
		if (IsOutput)
		{
			ShellHelper.ChangeRecordObject(Owner as VmModule, ((obj as DynamicIO)?.GetParentNode() as ShellModuleDynamicIO)?.ItemsA.ToList());
		}
		return obj;
	}

	public override void RemoveIO(object dymaicIO)
	{
		base.RemoveIO(dymaicIO);
		RemoveDisplayObject((dymaicIO as DynamicIO).IoDataSource);
		if (IsOutput)
		{
			ShellHelper.ChangeRecordObject(Owner as VmModule, ((dymaicIO as DynamicIO)?.GetParentNode() as ShellModuleDynamicIO)?.ItemsA.ToList());
		}
	}

	public void RemoveDisplayObject(VmIO iO)
	{
		if (iO.TypeName == "IMAGE" && IsOutput)
		{
			TargetObjects.OfType<DynamicObject>().FirstOrDefault((DynamicObject x) => x.NameA == iO.Name)?.RemoveSelf();
		}
	}

	public void UpdateDisplayObjectName(VmIO iO, string oldName, string NewName)
	{
		if (iO.TypeName == "IMAGE" && IsOutput)
		{
			DynamicObject dynamicObject = TargetObjects.OfType<DynamicObject>().FirstOrDefault((DynamicObject x) => x.NameA == oldName);
			if (dynamicObject != null)
			{
				dynamicObject.NameA = ChangeName(NewName);
			}
		}
	}

	private string ChangeName(string tmpName)
	{
		if (string.IsNullOrEmpty(tmpName))
		{
			return tmpName;
		}
		if (tmpName.First() == '%' && tmpName.Last() == '%' && tmpName.Length > 1)
		{
			return tmpName.Substring(1, tmpName.Length - 2);
		}
		return tmpName;
	}

	public void AddDisplayObject(VmIO iO)
	{
		if (!(iO.TypeName == "IMAGE") || !IsOutput)
		{
			return;
		}
		ParamRoot paramRoot = (Owner as VmModule)["[Display]"] as ParamRoot;
		DynamicObject newObject = new DynamicObject();
		newObject.SetParentNode(paramRoot.CategorysA.First());
		newObject.Name = iO.RealName();
		newObject.TypeA = ObjectTypeEnum.image.ToString();
		Object obj = DisplayRoot.GetInstance().Items.OfType<Object>().FirstOrDefault((Object p) => p.TypeA == newObject.TypeA);
		newObject.Features = new ObservableCollection<FeatureBase>();
		newObject.okcolor = obj.okcolor;
		newObject.ngcolor = obj.ngcolor;
		newObject.hotcolor = obj.hotcolor;
		newObject.opacity = obj.opacity;
		newObject.linetype = obj.linetype;
		newObject.linewidth = obj.linewidth;
		newObject.IsDisplayA = true;
		foreach (FeatureBase item in obj.FeaturesB)
		{
			newObject.Features.Add(item.Clone<Feature>());
			newObject.Features.Last().SetParentNode(newObject);
		}
		int i;
		for (i = 0; i < newObject.FeaturesB.Count; i++)
		{
			newObject.FeaturesB[i].MappingA = iO.Children.FirstOrDefault((VmIO x) => x.Name.ToLower().Contains(newObject.FeaturesB[i].NameA.ToLower())).Name;
			newObject.FeaturesB[i].InitVmIOs();
		}
		paramRoot.GetDisplayList().Add(newObject);
	}

	public void SaveDisplayValueType(string type)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (object item in ItemsA)
		{
			if (item is ShellDynamicIO shellDynamicIO && shellDynamicIO.DisplayValueTypeA.Contains("[]"))
			{
				stringBuilder.Append($"{shellDynamicIO.NameA}:{shellDynamicIO.DisplayValueTypeA},");
			}
		}
		string text = stringBuilder.ToString();
		if (text.Length > 0)
		{
			text = text.Substring(0, text.LastIndexOf(","));
			DataSource.UserData[type] = StringUtil.UTF8GetBytesPadZero(text);
		}
	}

	public void LoadDisplayValueType(string type)
	{
		byte[] array = DataSource.UserData[type];
		if (array == null || array.Length == 0)
		{
			return;
		}
		string[] array2 = StringUtil.UTF8GetString(array).Split(',');
		string[] array3 = array2;
		foreach (string text in array3)
		{
			string[] nameList = text.Split(':');
			if (nameList.Length == 2 && ItemsA.FirstOrDefault((object t) => (t as ShellDynamicIO).NameA == nameList[0]) is ShellDynamicIO shellDynamicIO)
			{
				shellDynamicIO.DisplayValueTypeA = nameList[1];
			}
		}
	}
}
