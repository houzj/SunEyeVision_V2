using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using Apps.XmlParser.Variable;
using Hik.Util;
using VM.Core;
using VM.PlatformSDKCS;

namespace ShellCommonControl;

public class ShellHelper
{
	private static ObservableCollection<string> CurVarNullValues = new ObservableCollection<string> { AppLocalizationService.GetLang("Lang_RstEpt") };

	public static ObservableCollection<string> GetVarResult(VmIO filter)
	{
		List<string> listResult = new List<string>();
		if (filter != null && filter.Root != null && !string.IsNullOrEmpty(filter.Name))
		{
			try
			{
				VariableManager variableManager = new VariableManager(filter.Root);
				switch (filter.TypeName)
				{
				case "int":
				{
					IntDataArray varIntValue = variableManager.GetVarIntValue(filter.Name);
					if (varIntValue.nValueNum > 0)
					{
						listResult.AddRange(varIntValue.pIntVal.Select((int p) => p.ToString()));
					}
					break;
				}
				case "float":
				{
					FloatDataArray varFloatValue = variableManager.GetVarFloatValue(filter.Name);
					if (varFloatValue.nValueNum > 0)
					{
						listResult.AddRange(varFloatValue.pFloatVal.Select((float p) => p.ToString()));
					}
					break;
				}
				case "string":
				{
					string[] varStringValue = variableManager.GetVarStringValue(filter.Name);
					if (varStringValue == null || varStringValue.Length == 0)
					{
						break;
					}
					string[] array = varStringValue;
					foreach (string text2 in array)
					{
						if (!string.IsNullOrWhiteSpace(text2))
						{
							listResult.Add(text2);
						}
					}
					break;
				}
				case "byte":
				{
					ByteArrayData varByteStringValue = variableManager.GetVarByteStringValue(filter.Name);
					if (varByteStringValue.pData != null && varByteStringValue.nDataLen != 0)
					{
						string text = Hik.Util.StringUtil.BytesToString(varByteStringValue.pData);
						if (!string.IsNullOrWhiteSpace(text))
						{
							listResult.Add(text);
						}
					}
					break;
				}
				case "IMAGE":
				{
					ImageBaseData varImageData = variableManager.GetVarImageData(filter.Name, isMemoryCopy: false, isGetImage: false);
					if (varImageData.Width > 0 && varImageData.Height > 0)
					{
						listResult.Add(varImageData.Width.ToString());
						listResult.Add(varImageData.Height.ToString());
						listResult.Add(varImageData.Pixelformat.ToString());
					}
					break;
				}
				case "ROIBOX":
				{
					List<RectBox> varRectBoxArray = variableManager.GetVarRectBoxArray(filter.Name);
					if (varRectBoxArray != null && varRectBoxArray.Count > 0)
					{
						varRectBoxArray.ForEach((RectBox q) =>
						{
							listResult.Add(q.CenterPoint.X.ToString());
							listResult.Add(q.CenterPoint.Y.ToString());
							listResult.Add(q.BoxWidth.ToString());
							listResult.Add(q.BoxHeight.ToString());
							listResult.Add(q.Angle.ToString());
						});
					}
					break;
				}
				case "ROIANNULUS":
				{
					List<Annulus> varAnnulusArray = variableManager.GetVarAnnulusArray(filter.Name);
					if (varAnnulusArray != null && varAnnulusArray.Count > 0)
					{
						varAnnulusArray.ForEach((Annulus q) =>
						{
							listResult.Add(q.CenterPoint.X.ToString());
							listResult.Add(q.CenterPoint.Y.ToString());
							listResult.Add(q.InnerRadius.ToString());
							listResult.Add(q.OuterRadius.ToString());
							listResult.Add(q.StartAngle.ToString());
							listResult.Add(q.AngleExtend.ToString());
						});
					}
					break;
				}
				case "ROIPOLYGON":
				{
					List<Polygon> varPolygonArray = variableManager.GetVarPolygonArray(filter.Name);
					if (varPolygonArray == null || varPolygonArray.Count <= 0)
					{
						break;
					}
					varPolygonArray.ForEach((Polygon q) =>
					{
						if (q.VertexPoints != null && q.VertexPoints.Length != 0)
						{
							VM.PlatformSDKCS.PointF[] vertexPoints = q.VertexPoints;
							foreach (VM.PlatformSDKCS.PointF pointF in vertexPoints)
							{
								listResult.Add(pointF.X.ToString());
								listResult.Add(pointF.Y.ToString());
							}
						}
					});
					break;
				}
				case "POINT":
				{
					List<VM.PlatformSDKCS.PointF> varPointArray = variableManager.GetVarPointArray(filter.Name);
					if (varPointArray != null && varPointArray.Count > 0)
					{
						varPointArray.ForEach((VM.PlatformSDKCS.PointF q) =>
						{
							listResult.Add(q.X.ToString());
							listResult.Add(q.Y.ToString());
						});
					}
					break;
				}
				case "LINE":
				{
					List<Line> varLineArray = variableManager.GetVarLineArray(filter.Name);
					if (varLineArray != null && varLineArray.Count > 0)
					{
						varLineArray.ForEach((Line q) =>
						{
							listResult.Add(q.StartPoint.X.ToString());
							listResult.Add(q.StartPoint.Y.ToString());
							listResult.Add(q.EndPoint.X.ToString());
							listResult.Add(q.EndPoint.Y.ToString());
						});
					}
					break;
				}
				case "FIXTURE":
				{
					List<Fixture> varFixtureArray = variableManager.GetVarFixtureArray(filter.Name);
					if (varFixtureArray != null && varFixtureArray.Count > 0)
					{
						varFixtureArray.ForEach((Fixture q) =>
						{
							listResult.Add(q.stInitPoint.X.ToString());
							listResult.Add(q.stInitPoint.Y.ToString());
							listResult.Add(q.fInitAngle.ToString());
							listResult.Add(q.fInitScaleX.ToString());
							listResult.Add(q.fInitScaleY.ToString());
							listResult.Add(q.stRunPoint.X.ToString());
							listResult.Add(q.stRunPoint.Y.ToString());
							listResult.Add(q.fRunAngle.ToString());
							listResult.Add(q.fRunScaleX.ToString());
							listResult.Add(q.fRunScaleY.ToString());
						});
					}
					break;
				}
				case "Rect":
				{
					List<RectF> varRectFArray = variableManager.GetVarRectFArray(filter.Name);
					if (varRectFArray != null && varRectFArray.Count > 0)
					{
						varRectFArray.ForEach((RectF q) =>
						{
							listResult.Add(q.RectPoint.X.ToString());
							listResult.Add(q.RectPoint.Y.ToString());
							listResult.Add(q.RectWidth.ToString());
							listResult.Add(q.RectHeight.ToString());
						});
					}
					break;
				}
				case "ELLIPSE":
				{
					List<Ellipse> varEllipseArray = variableManager.GetVarEllipseArray(filter.Name);
					if (varEllipseArray != null && varEllipseArray.Count > 0)
					{
						varEllipseArray.ForEach((Ellipse q) =>
						{
							listResult.Add(q.CenterPoint.X.ToString());
							listResult.Add(q.CenterPoint.Y.ToString());
							listResult.Add(q.MajorRadius.ToString());
							listResult.Add(q.MinorRadius.ToString());
							listResult.Add(q.Angle.ToString());
						});
					}
					break;
				}
				}
			}
			catch (VmException ex)
			{
				string message = string.Format("GetVarResult error, {0}{1}", AppLocalizationService.GetLang("errCode:"), ex.errorCode.ToString("X"));
				LogHelper.moduleLog.Error(message);
			}
		}
		else
		{
			LogHelper.moduleLog.Error("GetVarResult error.");
		}
		if (listResult.Count <= 0)
		{
			return CurVarNullValues;
		}
		return new ObservableCollection<string>(listResult);
	}

	public static ObservableCollection<string> GetIOResultByCallBack(ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX_Data resultInfo, VmIO filter)
	{
		if (filter == null)
		{
			LogHelper.moduleLog.Error("io is null");
			return CurVarNullValues;
		}
		List<string> list = new List<string>();
		if (filter.IsLeafNode)
		{
			list = VmIOManager.GetLeafIOResultByCallBack(resultInfo, filter);
		}
		else
		{
			foreach (VmIO item in filter.GetAllLeafIO())
			{
				List<string> leafIOResultByCallBack = VmIOManager.GetLeafIOResultByCallBack(resultInfo, item);
				if (leafIOResultByCallBack.Count > 0)
				{
					list.AddRange(leafIOResultByCallBack);
				}
			}
		}
		if (list.Count <= 0)
		{
			return CurVarNullValues;
		}
		return new ObservableCollection<string>(list);
	}

	public static ObservableCollection<string> GetIOResult(VmIO filter)
	{
		if (filter == null)
		{
			LogHelper.moduleLog.Error("io is null");
			return CurVarNullValues;
		}
		List<string> list = new List<string>();
		if (filter is VmConstantIO || filter.IsLeafNode)
		{
			list = GetLeafIOResult(filter.Value as Array, filter.TypeName);
		}
		else
		{
			foreach (VmIO item in filter.GetAllLeafIO())
			{
				List<string> leafIOResult = GetLeafIOResult(item.Value as Array, item.TypeName);
				if (leafIOResult.Count > 0)
				{
					list.AddRange(leafIOResult);
				}
			}
		}
		if (list.Count <= 0)
		{
			return CurVarNullValues;
		}
		return new ObservableCollection<string>(list);
	}

	public static void ChangeRecordObject(VmModule module, List<object> list)
	{
		if (module == null || list == null || !(module["[Display]"] is ParamRoot paramRoot))
		{
			return;
		}
		IList<object> displayList = paramRoot.GetDisplayList();
		if (displayList == null)
		{
			return;
		}
		bool flag = false;
		DynamicObject displayObject = displayList.OfType<DynamicObject>().FirstOrDefault((DynamicObject x) => x.NameA == "%Data Record%");
		if (displayObject == null)
		{
			flag = true;
			displayObject = new DynamicObject();
			displayObject.SetParentNode(paramRoot.CategorysA.First());
			displayObject.Name = AppLocalizationService.UnDoTranslate("Data Record");
			displayObject.TypeA = "datarecord";
			Apps.XmlParser.Object obj = DisplayRoot.GetInstance().Items.OfType<Apps.XmlParser.Object>().FirstOrDefault((Apps.XmlParser.Object p) => p.TypeA == displayObject.TypeA);
			displayObject.Features = new ObservableCollection<FeatureBase>();
			displayObject.okcolor = obj.okcolor;
			displayObject.ngcolor = obj.ngcolor;
			displayObject.hotcolor = obj.hotcolor;
			displayObject.opacity = obj.opacity;
			displayObject.linetype = obj.linetype;
			displayObject.linewidth = obj.linewidth;
			displayObject.IsDisplayA = true;
			Feature feature = new Feature();
			feature.Name = obj.FeaturesB[0].Name;
			feature.MappingA = obj.FeaturesB[0].MappingA;
			feature.MappingArray = obj.FeaturesB[0].MappingArray;
			feature.Value = obj.FeaturesB[0].Value;
			displayObject.Features.Add(feature);
			displayObject.Features.Last().SetParentNode(displayObject);
		}
		for (int num = 0; num < displayObject.FeaturesB.Count; num++)
		{
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = new StringBuilder();
			string text = "";
			string text2 = "";
			int num2 = 0;
			int num3 = 0;
			using (List<object>.Enumerator enumerator = list.GetEnumerator())
			{
				while (enumerator.MoveNext() && enumerator.Current is DynamicIO dynamicIO)
				{
					text = "";
					text2 = "";
					if (dynamicIO.NameA != "ModuStatus" && dynamicIO.NameA != "ResultShow" && dynamicIO.NameA != "ModuRunTime" && !VmModule.fTimeStamp.Contains(dynamicIO.NameA) && !(dynamicIO.ValueTypeA == "pointset"))
					{
						if (dynamicIO.ValueTypeA == "IMAGE")
						{
							for (int num4 = 0; num4 < dynamicIO.IoDataSource.Childs.Count; num4++)
							{
								VmIO vmIO = dynamicIO.IoDataSource.Childs[num4];
								if (vmIO.Name.ToLower().Contains("width") || vmIO.Name.ToLower().Contains("height"))
								{
									text = text + vmIO.RealName() + ":{" + num2 + "},";
									num2++;
									text2 = text2 + vmIO.Name + ",";
								}
							}
						}
						else if (dynamicIO.ValueTypeA == "int" || dynamicIO.ValueTypeA == "float" || dynamicIO.ValueTypeA == "string" || dynamicIO.ValueTypeA == "byte")
						{
							text = text + dynamicIO.RealNameA + ":{" + num2 + "},";
							num2++;
							text2 = text2 + dynamicIO.NameA + ",";
						}
						else
						{
							foreach (VmIO item in dynamicIO.IoDataSource.GetAllLeafIO())
							{
								text = text + item.RealName() + ":{" + num2 + "},";
								num2++;
								text2 = text2 + item.Name + ",";
							}
						}
					}
					num3 += Encoding.UTF8.GetByteCount(text);
					if (num3 > 512)
					{
						break;
					}
					stringBuilder.Append(text);
					stringBuilder2.Append(text2);
				}
			}
			displayObject.FeaturesB[num].ValueA = ((stringBuilder.Length == 0) ? "" : stringBuilder.ToString(0, stringBuilder.Length - 1));
			displayObject.FeaturesB[num].MappingA = ((stringBuilder2.Length == 0) ? "" : stringBuilder2.ToString(0, stringBuilder2.Length - 1));
			displayObject.FeaturesB[num].InitVmIOs();
		}
		if (flag)
		{
			displayList.Add(displayObject);
		}
	}

	private static List<string> GetLeafIOResult(Array array, string typeName)
	{
		List<string> list = new List<string>();
		if (array != null && !string.IsNullOrEmpty(typeName))
		{
			foreach (object item in array)
			{
				if (item == null)
				{
					continue;
				}
				switch (typeName)
				{
				case "int":
				case "float":
					list.Add(item.ToString());
					break;
				case "string":
				{
					string text = "";
					if (item is ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX)
					{
						text = ((ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX)item).strValue;
					}
					else if (item is string text2)
					{
						text = text2;
					}
					if (!string.IsNullOrWhiteSpace(text))
					{
						list.Add(text);
					}
					break;
				}
				case "byte":
					if (item is ImvsSdkDefine.IMVS_MODULE_BYTEDATA_VALUE_EX_Data iMVS_MODULE_BYTEDATA_VALUE_EX_Data && !string.IsNullOrWhiteSpace(iMVS_MODULE_BYTEDATA_VALUE_EX_Data.strValue))
					{
						list.Add(iMVS_MODULE_BYTEDATA_VALUE_EX_Data.strValue);
					}
					break;
				}
			}
		}
		return list;
	}
}
