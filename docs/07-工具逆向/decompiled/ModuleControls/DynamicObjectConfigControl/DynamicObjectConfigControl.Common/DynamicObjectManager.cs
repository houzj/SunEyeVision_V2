using System;
using System.Collections.Generic;
using System.Linq;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace DynamicObjectConfigControl.Common;

public class DynamicObjectManager
{
	private static Dictionary<string, ObjectTypeEnum> _outputDisplayDic = new Dictionary<string, ObjectTypeEnum>
	{
		{
			"IMAGE",
			ObjectTypeEnum.image
		},
		{
			"PIXELIMAGE",
			ObjectTypeEnum.pixelinfoimage
		},
		{
			"CIRCLE",
			ObjectTypeEnum.circle
		},
		{
			"POINT",
			ObjectTypeEnum.point
		},
		{
			"ROIBOX",
			ObjectTypeEnum.rect
		},
		{
			"Rect",
			ObjectTypeEnum.rect
		},
		{
			"LINE",
			ObjectTypeEnum.line
		},
		{
			"CONTOURPOINTS",
			ObjectTypeEnum.outlinepoint
		},
		{
			"ROIPOLYGON",
			ObjectTypeEnum.polygon
		},
		{
			"ROIANNULUS",
			ObjectTypeEnum.annularsector
		},
		{
			"ELLIPSE",
			ObjectTypeEnum.ellipse
		},
		{
			"pointset",
			ObjectTypeEnum.outlinepoint
		},
		{
			"STRING".ToLower(),
			ObjectTypeEnum.text
		}
	};

	public static Dictionary<string, ObjectTypeEnum> OutputDisplayDic => _outputDisplayDic;

	public static void AddDisplayObject(VmIO io)
	{
		if (!OutputDisplayDic.Keys.Contains(io.TypeName))
		{
			return;
		}
		DynamicObject dynamicObject = GroupDisplayParam.AddDynamicObject(io, OutputDisplayDic[io.TypeName]);
		if (dynamicObject == null)
		{
			return;
		}
		try
		{
			if (dynamicObject.Owner is IVMGroup iVMGroup)
			{
				iVMGroup.SetIoParams();
			}
			if (dynamicObject.Owner is IVmProcedure vmProcedure)
			{
				vmProcedure.SetIoParams();
			}
		}
		catch (VmException vmException)
		{
			ShowMessage.ShowVmExceptionMessage(vmException);
		}
		dynamicObject.RelateIO = io;
	}

	public static void RemoveDisplayObject(VmIO io)
	{
		if (io == null || io.Root == null)
		{
			return;
		}
		ParamRoot paramRoot = io.Root["[Display]"] as ParamRoot;
		DynamicObject dynamicObject = paramRoot.GetDisplayList().OfType<DynamicObject>().Single((DynamicObject p) => p.RelateIO == io);
		if (dynamicObject.TypeA.Equals("text"))
		{
			List<VmIO> list = new List<VmIO>();
			List<VmIO> list2 = dynamicObject.FeaturesB.FirstOrDefault((FeatureBase p) => p.NameA == "PositionX")?.vmIOs;
			if (list2 != null)
			{
				list.AddRange(list2);
			}
			List<VmIO> list3 = dynamicObject.FeaturesB.FirstOrDefault((FeatureBase p) => p.NameA == "PositionY")?.vmIOs;
			if (list3 != null)
			{
				list.AddRange(list3);
			}
			foreach (VmIO item in list)
			{
				item.RemoveSelf();
				if (io.Root.StrModuleName.Equals("ShellModule"))
				{
					try
					{
						((IUserStringData)io.Root.Params)["DeletePositionIO"] = item.Name;
					}
					catch (VmException ex)
					{
						string text = "DeletePositionIO error, " + AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + Environment.NewLine;
						text = text + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
						LogHelper.moduleLog.Error(text);
					}
				}
			}
		}
		List<VmIO> list4 = dynamicObject.FeaturesB.FirstOrDefault((FeatureBase p) => p.NameA == "Status")?.vmIOs;
		if (list4 != null && list4.Count > 0)
		{
			foreach (VmIO item2 in list4)
			{
				if (item2.Name != "ModuStatus")
				{
					item2.RemoveSelf();
				}
			}
		}
		dynamicObject.RemoveSelf();
	}

	public static void SetObjectMapByIO(Apps.XmlParser.Object displayObj, VmIO io)
	{
		List<VmIO> allLeafIO = io.GetAllLeafIO();
		switch (io.TypeName)
		{
		case "IMAGE":
		case "PIXELIMAGE":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Image").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("Image") && p.TypeName == "image").Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Width").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ImageWidth")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Height").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ImageHeight")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PixelFormat").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ImagePixelFormat")).Name;
			if (io.TypeName == "PIXELIMAGE")
			{
				displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PixelInfo").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ClassName")).Name;
				displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PixelInfoValue").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("GrayValue")).Name;
			}
			break;
		case "CIRCLE":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("centerx")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("centery")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MajorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("radius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MinorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("radius")).Name;
			break;
		case "POINT":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("pointx")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("pointy")).Name;
			break;
		case "ROIBOX":
			displayObj.okcolorA = "#0000ff";
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roicenterx")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roicentery")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Width").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roiwidth")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Height").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roiheight")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Angle").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roiangle")).Name;
			break;
		case "Rect":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Width").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectWidth")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Height").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectHeight")).Name;
			break;
		case "LINE":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "StartLineX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineStartPointX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "StartLineY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineStartPointY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "EndLineX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineEndPointX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "EndLineY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineEndPointY")).Name;
			break;
		case "CONTOURPOINTS":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "OutLineInfo").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ContourInfo")).Name;
			break;
		case "ROIPOLYGON":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PointNum").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlindPolygonPointNum")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PointXArray").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlindPolygonPointsX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PointYArray").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlindPolygonPointsY")).Name;
			break;
		case "ROIANNULUS":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusCenterX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusCenterY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "OuterRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusOuterRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "InnerRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusInnerRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "StartAngle").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusStartAngle")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "AngleRange").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusAngleExtend")).Name;
			break;
		case "ELLIPSE":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("CenterX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("CenterY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MajorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("MajorRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MinorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("MinorRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Angle").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("Angle")).Name;
			break;
		case "pointset":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "OutLineInfo").MappingA = io.Name;
			break;
		default:
			if (io.TypeName == "STRING".ToLower())
			{
				displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Content").MappingA = io.Name;
			}
			break;
		}
		if (!(io.TypeName != "CONTOURPOINTS") || !(io.TypeName != "pointset") || !(io.TypeName != "IMAGE") || !(io.TypeName != "PIXELIMAGE"))
		{
			return;
		}
		VmIO vmIO = io.RelateIO?.Root.Outputs.FirstOrDefault((VmIO x) => x.Name == "ModuStatus");
		if (vmIO != null)
		{
			VmIO vmIO2 = SetInternalObjectToParentObjectOperation.CreatOutputIOByOldIO(io.Root, vmIO);
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Status").MappingA = vmIO2.Name;
			return;
		}
		VmIO vmIO3 = io.Root.Outputs.FirstOrDefault((VmIO x) => x.Name == "ModuStatus");
		displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Status").MappingA = vmIO3?.Name;
	}
}
