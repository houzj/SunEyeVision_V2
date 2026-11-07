using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Hik.Xml;
using VM.Controls;
using VM.Core;
using VMControls.Interface;
using VMControls.WPF.DataForUI;

namespace Apps.XmlParser.ParamTab;

public class SetInternalObjectToParentObjectOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	private const string DefaultStartName = "out";

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetInternalObjectToParentObjectOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams == null || !(Params[0] is NameValueItem nameValueItem))
		{
			return;
		}
		string text = Params[1] as string;
		if (nameValueItem == null)
		{
			return;
		}
		BaseNode baseNode = nameValueItem.GetTopBaseNode() as BaseNode;
		if (!((baseNode as IOwner)?.Owner is VmModule { Parent: VmModule } vmModule) || ExtraParams.Length == 0)
		{
			return;
		}
		string text2 = ExtraParams[0];
		if (string.IsNullOrEmpty(text2))
		{
			return;
		}
		Object obj = vmModule.GetObject(text2);
		if (text == null)
		{
			VmModuleBaseDecorator.ClearModuleInfoFromObject(obj);
			return;
		}
		int num = Enumerable.Count(vmModule.Outputs.GetAllLeafIO());
		int count = obj.FeaturesB.Count;
		if (num + count > 1024)
		{
			if (nameValueItem is ButtonSelecter buttonSelecter)
			{
				buttonSelecter.CurValueA = null;
			}
			HikMessageBox.Show(AppLocalizationService.GetLang("Io数量超出最大限制！"));
			LogHelper.Error(AppLocalizationService.GetLang("Io数量超出最大限制！") + " Existed output io count: " + num + ", add new io count: " + count);
			return;
		}
		string[] subString = text.Split('.');
		if (subString.Count() != 2)
		{
			return;
		}
		string sub = subString[0] + ".[Display]";
		ParamRoot paramRoot = vmModule[sub] as ParamRoot;
		IEnumerable<Object> enumerable = paramRoot.GetDisplayList().OfType<Object>();
		if (enumerable != null && enumerable.Count() > 0)
		{
			Object targetObj = enumerable.Single((Object p) => AppLocalizationService.GetLang(p.NameA) == subString[1]);
			SetRalationObject(obj, targetObj);
			obj?.UpdateValue(new PropertyEventArgs
			{
				PropertyName = nameValueItem.NameA
			});
		}
	}

	public static void SetRalationObject(Object sourceObject, Object targetObj)
	{
		VmModule node = sourceObject.Owner as VmModule;
		List<VmIO> list = new List<VmIO>();
		if (sourceObject.TypeA == "image" || sourceObject.TypeA == "pixelinfoimage")
		{
			List<VmIO> allTopMappingIO = targetObj.GetAllTopMappingIO();
			foreach (VmIO item in allTopMappingIO.Where((VmIO p) => p.TypeName == "IMAGE" || p.TypeName == "PIXELIMAGE"))
			{
				list.Add(CreatOutputIOByOldIO(node, item));
			}
		}
		else if (sourceObject.TypeA == "PointCloud3DImage")
		{
			List<VmIO> allTopMappingIO2 = targetObj.GetAllTopMappingIO();
			list.Add(CreatOutputIOByOldIO(node, allTopMappingIO2[0]));
		}
		switch (sourceObject.TypeA)
		{
		case "image":
		case "PointCloud3DImage":
			SetImageObjectMapByIO(sourceObject, list.Single((VmIO p) => p.TypeName == "IMAGE"));
			break;
		case "pixelinfoimage":
			SetImageObjectMapByIO(sourceObject, list.Single((VmIO p) => p.TypeName == "PIXELIMAGE"));
			break;
		default:
		{
			Object obj = DisplayRoot.GetInstance().Items.OfType<Object>().Single((Object p) => p.Type == sourceObject.TypeA);
			obj.FeaturesB.ToList().ForEach((FeatureBase p) =>
			{
				SetNewFeatureToObject(p.Name, node, sourceObject, targetObj);
			});
			sourceObject.okcolorA = targetObj.okcolorA;
			sourceObject.ngcolorA = targetObj.ngcolorA;
			sourceObject.opacityA = targetObj.opacityA;
			sourceObject.IsFillA = targetObj.IsFillA;
			sourceObject.StrokeThicknessA = targetObj.StrokeThicknessA;
			break;
		}
		}
		sourceObject.InitVmIOs();
		if (sourceObject.Owner is IVMGroup ioParams)
		{
			VmModuleBaseDecorator.SetIoParams(ioParams);
		}
		if (sourceObject.Owner is IVmProcedure ioParams2)
		{
			VmModuleBaseDecorator.SetIoParams(ioParams2);
		}
		(sourceObject as DynamicObject).SaveExternInfo("OriSelectedObj", AppLocalizationService.GetLangSet(targetObj.FullName, '.'));
	}

	private static void SetNewFeatureToObject(string featrueName, VmModule node, Object sourceObject, Object targetObj)
	{
		if ((featrueName == "IsThroughLine" && !targetObj.FeaturesB.Any((FeatureBase a) => a.Name == "IsThroughLine")) || !(targetObj.FeaturesB.Single((FeatureBase p) => p.Name == featrueName) is Feature feature))
		{
			return;
		}
		FeatureBase featureBase = sourceObject.FeaturesB.Single((FeatureBase p) => p.Name == featrueName);
		string value = (featureBase as Feature).Value;
		string mapping = (featureBase as Feature).Mapping;
		(featureBase as Feature).ValueA = feature.Value;
		if (feature.MappingArray.Count() > 0)
		{
			List<VmIO> list = new List<VmIO>();
			string[] mappingArray = feature.MappingArray;
			foreach (string mapValue in mappingArray)
			{
				VmIO mapIO = feature.GetMapIO(mapValue);
				if (mapIO != null)
				{
					IEnumerable<VmModule> subscribleModules = node.GetSubscribleModules(isOutput: true);
					if (!subscribleModules.Contains(mapIO.Root))
					{
						(featureBase as Feature).ValueA = value;
						featureBase.MappingA = mapping;
						return;
					}
					list.Add(CreatOutputIOByOldIO(node, mapIO));
				}
				else
				{
					list.Add(null);
				}
			}
			featureBase.MappingA = string.Join(",", list.Select((VmIO p) => p?.Name ?? ""));
		}
		else
		{
			featureBase.MappingA = feature.MappingA;
		}
	}

	public static VmIO CreatOutputIOByOldIO(VmModule node, VmIO oldIO)
	{
		VmIO vmIO = null;
		vmIO = ((!oldIO.IsLeafNode) ? AddCombinationIO(node, oldIO.TypeName, node, node.Outputs, null, IsNeedIncludeModuleName: false, visible: false, isReturnRelateValue: false) : AddLeafIO(node, oldIO.TypeName, node, node.Outputs, null, IsNeedIncludeModuleName: false, oldIO.Name, visible: false, isReturnRelateValue: false));
		int num = Enumerable.Count(node.Outputs.GetAllLeafIO());
		if (num > 1024)
		{
			LogHelper.Error(AppLocalizationService.GetLang("Io数量超出最大限制！") + " Io name: " + vmIO.FullName);
			vmIO.RemoveSelf();
			return null;
		}
		vmIO.GetAllIO().ForEach((VmIO p) =>
		{
			p.Visible = false;
		});
		vmIO.RelateIO = oldIO;
		vmIO.IsReturnRelateValue = false;
		return vmIO;
	}

	public static void SetImageObjectMapByIO(Object displayObj, VmIO io)
	{
		List<VmIO> allLeafIO = io.GetAllLeafIO();
		displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Image").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("Image") && p.TypeName == "image").Name;
		displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Width").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ImageWidth")).Name;
		displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Height").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ImageHeight")).Name;
		displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PixelFormat").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ImagePixelFormat")).Name;
		if (io.TypeName == "PIXELIMAGE")
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PixelInfo").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ClassName")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PixelInfoValue").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("GrayValue")).Name;
		}
	}

	public static VmIO AddCombinationIO(VmModule Owner, string typeName, object parent, IList parentList, VmIO relateIO = null, bool IsNeedIncludeModuleName = false, bool visible = true, bool isReturnRelateValue = true)
	{
		CombinRoot instance = CombinRoot.GetInstance();
		Combination obj = instance.Items.OfType<Combination>().FirstOrDefault((Combination p) => p.Style == typeName);
		return AddIOFromCombination(Owner, obj, parent, parentList, relateIO, IsNeedIncludeModuleName, visible, isReturnRelateValue);
	}

	private static VmIO AddIOFromCombination(VmModule Owner, object obj, object parent, IList parentList, VmIO relateIO = null, bool IsNeedIncludeModuleName = false, bool visible = true, bool isReturnRelateValue = true)
	{
		if (obj is Combination combination)
		{
			string name = ((relateIO == null) ? combination.Name : ((IsNeedIncludeModuleName ? (relateIO.Root.Name + "_") : "") + AppLocalizationService.GetLang(combination.Name)));
			FixName(ref name);
			DynamicVmIO dynamicVmIO = new DynamicVmIO
			{
				Name = GetUniqueName(Owner, name),
				Root = Owner,
				Parent = parent,
				TypeName = combination.Style,
				Visible = visible,
				IsReturnRelateValue = isReturnRelateValue
			};
			parentList.Add(dynamicVmIO);
			foreach (object filter2 in combination.Filters)
			{
				AddIOFromCombination(Owner, filter2, dynamicVmIO, dynamicVmIO.Childs);
			}
			return dynamicVmIO;
		}
		if (obj is Filter filter)
		{
			AddLeafIO(Owner, filter.ValueTypeA, parent, parentList, relateIO, IsNeedIncludeModuleName, filter.NameA, visible: false, isReturnRelateValue: false);
		}
		return null;
	}

	private static VmIO AddLeafIO(VmModule Owner, string typeName, object parent, IList parentList, VmIO relateIO = null, bool IsNeedIncludeModuleName = false, string suggestName = null, bool visible = true, bool isReturnRelateValue = true)
	{
		string name = ((relateIO == null) ? (suggestName + "out") : ((IsNeedIncludeModuleName ? (relateIO.Root.Name + "_") : "") + AppLocalizationService.GetLang(relateIO.Name)));
		FixName(ref name);
		DynamicVmIO dynamicVmIO = new DynamicVmIO
		{
			Name = GetUniqueName(Owner, name),
			Root = Owner,
			Parent = parent,
			TypeName = typeName,
			Visible = visible,
			IsReturnRelateValue = isReturnRelateValue
		};
		parentList.Add(dynamicVmIO);
		return dynamicVmIO;
	}

	private static string GetUniqueName(VmModule Owner, string startName)
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

	private static void FixName(ref string name)
	{
		if (name.Length > 32)
		{
			name = name.Substring(0, 32);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
