using System.Collections.Generic;
using System.Linq;
using Apps.Interface;
using Hik.Xml;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class SetCombinationToObjectOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetCombinationToObjectOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams == null)
		{
			return;
		}
		NameValueItem nameValueItem = Params[0] as NameValueItem;
		int num = 0;
		if (nameValueItem is ButtonSelecter)
		{
			num = (nameValueItem as ButtonSelecter).Type;
		}
		if (nameValueItem == null)
		{
			return;
		}
		string filterString = Params[1] as string;
		if (nameValueItem == null)
		{
			return;
		}
		BaseNode baseNode = nameValueItem.GetTopBaseNode() as BaseNode;
		if (!((baseNode as IOwner)?.Owner is VmModule { Parent: VmModule } vmModule) || ExtraParams.Length == 0)
		{
			return;
		}
		string text = ExtraParams[0];
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		Object obj = vmModule.GetObject(text);
		VmIO outputFilter = null;
		string BindingIndex = null;
		bool isBindGlobalVar = false;
		if (!SetFilterSourceOperation.GetOutputInfo(vmModule, filterString, ref outputFilter, ref BindingIndex, ref isBindGlobalVar))
		{
			return;
		}
		switch (obj.TypeA)
		{
		case "image":
		{
			for (int num2 = 0; num2 < obj.FeaturesB.Count; num2++)
			{
				obj.FeaturesB[num2].MappingA = outputFilter?.Children[num2].Name;
				obj.FeaturesB[num2].InitVmIOs();
			}
			break;
		}
		case "circle":
		{
			List<VmIO> list6 = outputFilter?.GetAllLeafIO();
			if (list6 == null)
			{
				break;
			}
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("CenterX")).MappingA = list6.Single((VmIO p) => p.Name.Contains("centerx")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("CenterY")).MappingA = list6.Single((VmIO p) => p.Name.Contains("centery")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("MajorRadius")).MappingA = list6.Single((VmIO p) => p.Name.Contains("radius")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("MinorRadius")).MappingA = list6.Single((VmIO p) => p.Name.Contains("radius")).Name;
			{
				foreach (FeatureBase item in obj.FeaturesB)
				{
					item.InitVmIOs();
				}
				break;
			}
		}
		case "point":
		{
			List<VmIO> list4 = outputFilter?.GetAllLeafIO();
			if (list4 == null)
			{
				break;
			}
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("CenterX")).MappingA = list4.Single((VmIO p) => p.Name.Contains("pointx")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("CenterY")).MappingA = list4.Single((VmIO p) => p.Name.Contains("pointy")).Name;
			{
				foreach (FeatureBase item2 in obj.FeaturesB)
				{
					item2.InitVmIOs();
				}
				break;
			}
		}
		case "micropoint":
		{
			List<VmIO> list3 = outputFilter?.GetAllLeafIO();
			if (list3 == null)
			{
				break;
			}
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("CenterX")).MappingA = list3.Single((VmIO p) => p.Name.Contains("pointx")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("CenterY")).MappingA = list3.Single((VmIO p) => p.Name.Contains("pointy")).Name;
			{
				foreach (FeatureBase item3 in obj.FeaturesB)
				{
					item3.InitVmIOs();
				}
				break;
			}
		}
		case "rect":
		{
			List<VmIO> list5 = outputFilter?.GetAllLeafIO();
			if (list5 == null)
			{
				break;
			}
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("CenterX")).MappingA = list5.Single((VmIO p) => p.Name.Contains("roicenterx")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("CenterY")).MappingA = list5.Single((VmIO p) => p.Name.Contains("roicentery")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("Width")).MappingA = list5.Single((VmIO p) => p.Name.Contains("roiwidth")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("Height")).MappingA = list5.Single((VmIO p) => p.Name.Contains("roiheight")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("Angle")).MappingA = list5.Single((VmIO p) => p.Name.Contains("roiangle")).Name;
			{
				foreach (FeatureBase item4 in obj.FeaturesB)
				{
					item4.InitVmIOs();
				}
				break;
			}
		}
		case "line":
		{
			List<VmIO> list2 = outputFilter?.GetAllLeafIO();
			if (list2 == null)
			{
				break;
			}
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("StartLineX")).MappingA = list2.Single((VmIO p) => p.Name.Contains("LineStartPointX")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("StartLineY")).MappingA = list2.Single((VmIO p) => p.Name.Contains("LineStartPointY")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("EndLineX")).MappingA = list2.Single((VmIO p) => p.Name.Contains("LineEndPointX")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("EndLineY")).MappingA = list2.Single((VmIO p) => p.Name.Contains("LineEndPointY")).Name;
			{
				foreach (FeatureBase item5 in obj.FeaturesB)
				{
					item5.InitVmIOs();
				}
				break;
			}
		}
		case "outlinepoint":
		{
			List<VmIO> list = outputFilter?.GetAllLeafIO();
			if (list == null)
			{
				break;
			}
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("OutLineInfo")).MappingA = list.Single((VmIO p) => p.Name.Contains("ContourInfo")).Name;
			obj.FeaturesB.Single((FeatureBase p) => p.Name.Contains("Num")).MappingA = list.Single((VmIO p) => p.Name.Contains("ContourNum")).Name;
			{
				foreach (FeatureBase item6 in obj.FeaturesB)
				{
					item6.InitVmIOs();
				}
				break;
			}
		}
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
