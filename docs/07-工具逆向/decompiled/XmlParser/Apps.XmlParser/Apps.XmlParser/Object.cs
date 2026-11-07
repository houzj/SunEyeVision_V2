using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using Apps.Data;
using Apps.Data.ShapeDefine;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;
using VMControls.RenderInterface;

namespace Apps.XmlParser;

public class Object : ObjectBase, IVmFullPath
{
	private static int ImageStrCode;

	private static int PixelInfoValueStrCode;

	private static Thread refreshParamthread;

	private static int dataNumForReceiveStore;

	private static int dataNumForSendStore;

	private static int maxStoreLen;

	private static object[] forReceiveStore;

	private static object[] forSendStore;

	private static object[] dataStore;

	private static object[] dataStore2;

	private static int[] numStoreForReceived;

	private static int[] numStoreForSent;

	private static ConcurrentDictionary<uint, uint> procedureSendExecuteCount;

	internal uint? ExecuteCount;

	private object[] ValueStore;

	private string[] StrokeThicknessConfigEnableTypes = new string[10] { "circle", "point", "rect", "line", "LineAngle", "coordinate", "polygon", "annularsector", "ellipse", "polyline" };

	private string[] StrokeThicknessConfigEnableTypesEx = new string[3] { "point3d", "line3d", "PointXYZ" };

	private int valueStoreSumNum = 0;

	private int fixedNum;

	private int maxValueLenth = 0;

	private bool OutputEnable = false;

	public object value = new List<object>();

	private uint valueExecuteCount = 0u;

	private Dictionary<int, Feature> FeaturesDic = new Dictionary<int, Feature>();

	private Dictionary<int, int> FeaturesStoreIndexDic = new Dictionary<int, int>();

	private uint ownerProID = 0u;

	public string FullName => "%" + Owner?.ToString() + "%." + NameA;

	string IVmFullPath.FullPath => (Owner as IVmFullPath)?.FullPath + ".[Display]." + NameA;

	public bool StrokeThicknessConfigEnable
	{
		get
		{
			if (IsFill)
			{
				return false;
			}
			return StrokeThicknessConfigEnableTypes.Contains(Type) || StrokeThicknessConfigEnableTypesEx.Contains(Type);
		}
	}

	static Object()
	{
		refreshParamthread = null;
		dataNumForReceiveStore = 0;
		dataNumForSendStore = 0;
		maxStoreLen = 1000;
		dataStore = new object[maxStoreLen];
		dataStore2 = new object[maxStoreLen];
		numStoreForReceived = new int[maxStoreLen];
		numStoreForSent = new int[maxStoreLen];
		procedureSendExecuteCount = new ConcurrentDictionary<uint, uint>();
		ImageStrCode = "Image".GetHashCode();
		PixelInfoValueStrCode = "PixelInfoValue".GetHashCode();
		new ImageData();
		forReceiveStore = dataStore;
		forSendStore = dataStore2;
		refreshParamthread = new Thread(() =>
		{
			while (true)
			{
				if (dataNumForReceiveStore > 0)
				{
					object[] array = forSendStore;
					lock (refreshParamthread)
					{
						forSendStore = forReceiveStore;
						dataNumForSendStore = dataNumForReceiveStore;
						dataNumForReceiveStore = 0;
						forReceiveStore = array;
					}
					for (int i = 0; i < dataNumForSendStore; i++)
					{
						Tuple<Object, RenderDataEventArgs> tuple = forSendStore[i] as Tuple<Object, RenderDataEventArgs>;
						tuple.Item1.OnValueChanged(tuple.Item2);
						int result = 0;
						int executeCount = (int)tuple.Item2.ExecuteCount;
						Math.DivRem(executeCount, maxStoreLen, out result);
						numStoreForSent[result]++;
						forSendStore[i] = null;
						if (procedureSendExecuteCount.TryGetValue(tuple.Item1.ownerProID, out var num) && num != tuple.Item2.ExecuteCount)
						{
							procedureSendExecuteCount[tuple.Item1.ownerProID] = tuple.Item2.ExecuteCount;
						}
					}
					dataNumForSendStore = 0;
				}
				Thread.Sleep(3);
			}
		})
		{
			IsBackground = true,
			Priority = ThreadPriority.BelowNormal
		};
		refreshParamthread.Start();
	}

	public static bool GetDataSendFinishState(uint proID, uint executeCount)
	{
		int result = 0;
		Math.DivRem((int)executeCount, maxStoreLen, out result);
		uint num = executeCount;
		procedureSendExecuteCount.TryGetValue(proID, out num);
		bool flag = numStoreForSent[result] == numStoreForReceived[result] || executeCount < num;
		if (flag)
		{
			numStoreForReceived[result] = 0;
			numStoreForSent[result] = 0;
		}
		return !flag;
	}

	public Object()
	{
		FeatureNotifyValueChanged(null, null, null);
		NotifyValueChanged(0);
		FillImageValue();
		GetValueByType(null, 1, null);
		ImageData.GetPixelFormatByProtocol(1u);
		ImageData.GetDetailedPixelFormatByProtocol(1u);
		GetShapeValue("circle", 0);
		SetShapeProperty(0, new DataRecord(), "datarecord", GetValueByType);
	}

	public void SubscribeVmIOValueChangedEvent()
	{
		foreach (Feature item in FeaturesB)
		{
			item.SetOutputFilter();
		}
	}

	public void UnSubscribeVmIOValueChangedEvent()
	{
		foreach (Feature item in FeaturesB)
		{
			item.RemoveOutputFilter();
		}
	}

	internal void FeatureNotifyValueChanged(FeatureBase vmFeature, uint? executeCount, object value, int maxLenth = 0)
	{
		if (vmFeature == null || (IsValueChangedNull() && !OutputEnable))
		{
			return;
		}
		if (ExecuteCount != executeCount || !ExecuteCount.HasValue || valueStoreSumNum == 0)
		{
			ExecuteCount = executeCount;
			int num = 0;
			foreach (Feature feature in Features)
			{
				if (!feature.isFixedValue)
				{
					ValueStore[num] = null;
				}
				num++;
			}
			valueStoreSumNum = fixedNum;
			maxValueLenth = 0;
		}
		int num2 = FeaturesStoreIndexDic[vmFeature.GetHashCode()];
		if (ValueStore[num2] == null)
		{
			valueStoreSumNum++;
		}
		ValueStore[num2] = value;
		maxValueLenth = Math.Max(maxValueLenth, maxLenth);
		if (valueStoreSumNum == ValueStore.Length)
		{
			NotifyValueChanged(maxValueLenth);
		}
	}

	public override void SetOutputFilter()
	{
		base.SetOutputFilter();
		if (NameA == "Show Text")
		{
			List<IVmModule> mappingModules = GetMappingModules();
			if (mappingModules != null)
			{
				foreach (IVmModule item in mappingModules)
				{
					if (item != Owner)
					{
						item.SubscribeCount++;
					}
				}
			}
		}
		else if (GetMappingIO() is VmIO { Root: var root })
		{
			IVmModule vmModule = root;
			if (root != null && vmModule != Owner)
			{
				vmModule.SubscribeCount++;
			}
		}
		InitVmIOs();
		OutputEnable = true;
	}

	public override void RemoveOutputFilter()
	{
		base.RemoveOutputFilter();
		if (NameA == "Show Text")
		{
			List<IVmModule> mappingModules = GetMappingModules();
			if (mappingModules != null)
			{
				foreach (IVmModule item in mappingModules)
				{
					if (item != Owner && item.SubscribeCount > 0)
					{
						item.SubscribeCount--;
					}
				}
			}
		}
		else if (GetMappingIO() is VmIO { Root: var root })
		{
			IVmModule vmModule = root;
			if (root != null && vmModule != Owner && vmModule.SubscribeCount > 0)
			{
				vmModule.SubscribeCount--;
			}
		}
		OutputEnable = false;
	}

	internal void NotifyValueChanged(int maxlenth)
	{
		value = FillValue(maxlenth);
		valueExecuteCount = ExecuteCount ?? 0;
		if (IsValueChangedNull())
		{
			return;
		}
		Tuple<Object, RenderDataEventArgs> tuple = new Tuple<Object, RenderDataEventArgs>(this, new RenderDataEventArgs
		{
			Value = value,
			ValueType = TypeA,
			ExecuteCount = (ExecuteCount ?? 0),
			IsLowQuality = IsLowQuality
		});
		if (dataNumForReceiveStore < maxStoreLen)
		{
			lock (refreshParamthread)
			{
				forReceiveStore[dataNumForReceiveStore] = tuple;
				dataNumForReceiveStore++;
				int result = 0;
				int a = (int)(ExecuteCount ?? 0);
				Math.DivRem(a, maxStoreLen, out result);
				numStoreForReceived[result]++;
			}
		}
		valueStoreSumNum = 0;
		maxValueLenth = 0;
	}

	internal override void ClearTempValue()
	{
		value = new List<object>();
		valueExecuteCount = 0u;
	}

	public void ClearValueStore()
	{
		ClearTempValue();
		valueStoreSumNum = 0;
		maxValueLenth = 0;
		if (ValueStore == null)
		{
			return;
		}
		int num = 0;
		foreach (Feature feature in Features)
		{
			if (!feature.isFixedValue)
			{
				ValueStore[num] = null;
				feature.ClearValueStore();
			}
			num++;
		}
	}

	public object FillValue(int maxLenth, Dictionary<string, string> customConfig = null)
	{
		switch (TypeA)
		{
		case "image":
		case "sampleimage":
		case "pixelinfoimage":
			return FillImageValue();
		case "PointCloud3DImage":
			return FillImageValueList(maxLenth);
		case "shepematrix":
			return FillMatrixValue();
		default:
			return FillValueList(TypeA, maxLenth, customConfig);
		}
	}

	private object FillImageValueList(int maxLenth)
	{
		ImageData imageData = new PointCloudImageData();
		List<ImageData> list = new List<ImageData>();
		List<ImageData> list2 = new List<ImageData>();
		for (int i = 0; i < maxLenth; i++)
		{
			if (FillImageValue(i) is ImageData item)
			{
				list.Add(item);
			}
		}
		for (int j = 0; j < maxLenth; j++)
		{
			if (FillPcdNormalImageValue(j) is ImageData item2)
			{
				list2.Add(item2);
			}
		}
		if (list.Count > 0)
		{
			imageData.DetailedPixelFormat = list[0].DetailedPixelFormat;
			(imageData as PointCloudImageData).SubDatas = list;
			(imageData as PointCloudImageData).NormalDatas = list2;
			return imageData;
		}
		return null;
	}

	private object FillImageValue(int index = 0)
	{
		byte[] array = null;
		string text = null;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		if (FeaturesDic.Count == 0)
		{
			return null;
		}
		Feature feature = FeaturesDic[ImageStrCode];
		object obj = null;
		if (ValueStore[FeaturesStoreIndexDic[feature.GetHashCode()]] is Array { Length: >0 } array2)
		{
			obj = array2.GetValue(index);
		}
		if (obj is ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data iMVS_MODULE_CHUNK_VALUE_EX_Data)
		{
			if (iMVS_MODULE_CHUNK_VALUE_EX_Data.IsMemory)
			{
				text = iMVS_MODULE_CHUNK_VALUE_EX_Data.GetRealMemoryAddress();
			}
			else
			{
				array = iMVS_MODULE_CHUNK_VALUE_EX_Data.GetData();
			}
			num = ((GetValueByType("Height", index, typeof(int)) is int num4) ? num4 : 0);
			num2 = ((GetValueByType("Width", index, typeof(int)) is int num5) ? num5 : 0);
			num3 = ((GetValueByType("PixelFormat", index, typeof(int)) is int num6) ? num6 : 0);
			if (num == 0 || num2 == 0 || (array == null && text == null))
			{
				return null;
			}
			ImageData imageData = new ImageData();
			try
			{
				imageData.NeedConvertColor = (int)GetValueByType("NeedConvertColor", index, typeof(int)) == 1;
				imageData.PixelInfoLabel = (string)GetValueByType("PixelInfoLabel", index, typeof(string));
				FeaturesDic.TryGetValue(PixelInfoValueStrCode, out feature);
				if (feature != null)
				{
					int num7 = FeaturesStoreIndexDic[feature.GetHashCode()];
					int num8 = (ValueStore[num7] as Array)?.Length ?? 0;
					for (int i = 0; i < num8; i++)
					{
						imageData.PixelExtraInfo.Add((string)GetValueByType("PixelInfoValue", i, typeof(string)), (string)GetValueByType("PixelInfo", i, typeof(string)));
					}
				}
			}
			catch
			{
			}
			imageData.ImageBuffer = array;
			imageData.MemoryAddress = text;
			imageData.Width = num2;
			imageData.Height = num;
			imageData.PixelFormat = ImageData.GetPixelFormatByProtocol((uint)num3);
			imageData.DetailedPixelFormat = (uint)num3;
			imageData.NeedCheckExecuteCount = true;
			imageData.ExecuteCount = ExecuteCount ?? 0;
			return imageData;
		}
		if (obj is ImageData)
		{
			return obj;
		}
		return null;
	}

	private object FillPcdNormalImageValue(int index = 0)
	{
		byte[] array = null;
		string text = null;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		if (FeaturesDic.Count == 0 || !FeaturesDic.Keys.Contains("NormalImage".GetHashCode()))
		{
			return null;
		}
		Feature feature = FeaturesDic["NormalImage".GetHashCode()];
		object obj = null;
		if (ValueStore[FeaturesStoreIndexDic[feature.GetHashCode()]] is Array { Length: >0 } array2)
		{
			obj = array2.GetValue(index);
		}
		if (obj is ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data iMVS_MODULE_CHUNK_VALUE_EX_Data)
		{
			if (iMVS_MODULE_CHUNK_VALUE_EX_Data.IsMemory)
			{
				text = iMVS_MODULE_CHUNK_VALUE_EX_Data.GetRealMemoryAddress();
			}
			else
			{
				array = iMVS_MODULE_CHUNK_VALUE_EX_Data.GetData();
			}
			num = ((GetValueByType("NormalHeight", index, typeof(int)) is int num4) ? num4 : 0);
			num2 = ((GetValueByType("NormalWidth", index, typeof(int)) is int num5) ? num5 : 0);
			num3 = ((GetValueByType("NormalPixelFormat", index, typeof(int)) is int num6) ? num6 : 0);
			if (num == 0 || num2 == 0 || (array == null && text == null))
			{
				return null;
			}
			ImageData imageData = new ImageData();
			imageData.ImageBuffer = array;
			imageData.MemoryAddress = text;
			imageData.Width = num2;
			imageData.Height = num;
			imageData.PixelFormat = ImageData.GetPixelFormatByProtocol((uint)num3);
			imageData.DetailedPixelFormat = (uint)num3;
			return imageData;
		}
		if (obj is ImageData)
		{
			return obj;
		}
		return null;
	}

	private object FillMatrixValue()
	{
		ConvertMatrix convertMatrix = new ConvertMatrix();
		if (GetValueByType("Matrix", 0, typeof(float)) is Array { Length: >=9 } array)
		{
			convertMatrix.MatrixList = new List<double>();
			for (int i = 0; i < 9; i++)
			{
				object obj = array.GetValue(i);
				if (obj != null)
				{
					convertMatrix.MatrixList.Add((float)obj);
					continue;
				}
				return null;
			}
		}
		return convertMatrix;
	}

	public void InitVmIOs()
	{
		DeinitVmIOs();
		Features.ToList().ForEach((FeatureBase p) =>
		{
			p.InitVmIOs();
		});
		FeaturesDic.Clear();
		ValueStore = new object[Features.Count];
		fixedNum = 0;
		int num = 0;
		fixedNum = 0;
		foreach (Feature feature in Features)
		{
			FeaturesDic.Add(feature.Name.GetHashCode(), feature);
			FeaturesStoreIndexDic.Add(feature.GetHashCode(), FeaturesStoreIndexDic.Count);
			if (feature.isFixedValue)
			{
				ValueStore[num] = new string[1] { feature.ValueA };
				fixedNum++;
			}
			num++;
		}
		if (Owner is VmModule vmModule && ((Owner is VmProcedure) ? vmModule : vmModule.Root) is VmProcedure vmProcedure)
		{
			ownerProID = vmProcedure.ID;
			if (!procedureSendExecuteCount.ContainsKey(vmProcedure.ID))
			{
				procedureSendExecuteCount.TryAdd(vmProcedure.ID, 0u);
			}
			procedureSendExecuteCount[vmProcedure.ID] = vmProcedure.ExecuteCount;
		}
		OnValueChanged(null);
	}

	private void DeinitVmIOs()
	{
		FeaturesDic.Clear();
		FeaturesStoreIndexDic.Clear();
	}

	private object FillValueList(string typeName, int maxLenth, Dictionary<string, string> customConfig = null, bool isGetValueFromCallBack = true)
	{
		if (typeName == null)
		{
			return new List<object>();
		}
		if (typeName == "text" && ValueStore != null)
		{
			if (isGetValueFromCallBack)
			{
				int num = Features.IndexOf(Features.FirstOrDefault((FeatureBase p) => p.Name == "Content"));
				if (num >= 0 && num < ValueStore.Length)
				{
					maxLenth = (ValueStore.GetValue(num) as Array)?.Length ?? 0;
				}
			}
			else
			{
				FeatureBase featureBase = Features.FirstOrDefault((FeatureBase p) => p.Name == "Content");
				maxLenth = featureBase.GetValueCount();
			}
		}
		maxLenth = Math.Min(maxLenth, GlobalRenderVariable.ShapeRenderingMaxNumber);
		List<object> list = new List<object>(maxLenth);
		for (int num2 = 0; num2 < maxLenth; num2++)
		{
			list.Add(GetShapeValue(typeName, num2, customConfig, isGetValueFromCallBack));
		}
		if ((Name == "Unfixtured Point" || IsShowIndex) && list.Count > 1)
		{
			for (int num3 = 0; num3 < list.Count; num3++)
			{
				if (list[num3] is IIndex index)
				{
					index.Index = num3 + 1;
				}
			}
		}
		return list;
	}

	protected override object GetValueList(string typeName, uint excuteCount, Dictionary<string, string> customConfig = null)
	{
		if (typeName == null || Owner == null)
		{
			return new List<object>();
		}
		bool isEmpty = (Owner as VmModule).IsEmpty;
		VmProcedure vmProcedure = ((Owner is VmProcedure vmProcedure2) ? vmProcedure2 : ((Owner as IVmModule).Root as VmProcedure));
		uint num = (((Owner as IVmModule).ExecuteCount > vmProcedure.ExecuteCount) ? (Owner as IVmModule).ExecuteCount : vmProcedure.ExecuteCount);
		if (!isEmpty && valueExecuteCount != 0 && num == valueExecuteCount)
		{
			return value;
		}
		string featureName = FeaturesB?.FirstOrDefault((FeatureBase p) => !p.isFixedValue)?.Name;
		int valueCount = GetValueCount(featureName);
		value = FillValueList(typeName, valueCount, customConfig, isGetValueFromCallBack: false);
		valueExecuteCount = GetVmModule()?.ExecuteCount ?? 0;
		return value;
	}

	private object GetShapeValue(string shapeType, int index, Dictionary<string, string> customConfig = null, bool isGetValueFromCallBack = true)
	{
		Shape shapeEx = GetShapeEx(shapeType);
		Func<string, int, Type, object> func = null;
		func = ((!isGetValueFromCallBack) ? new Func<string, int, Type, object>(GetValueByTypeDir) : new Func<string, int, Type, object>(GetValueByType));
		SetBaseProperty(index, shapeEx, func, customConfig);
		SetShapeProperty(index, shapeEx, shapeType, func);
		return shapeEx;
	}

	private void SetShapeProperty(int index, Shape shape, string shapeType, Func<string, int, Type, object> getValueByType)
	{
		if (shape == null)
		{
			return;
		}
		shapeType = shapeType.ToLower();
		switch (shapeType)
		{
		case "circle":
		{
			Apps.Data.ShapeDefine.Circle circle = shape as Apps.Data.ShapeDefine.Circle;
			circle.MajorRadius = (float)getValueByType("MajorRadius", index, typeof(float));
			circle.MinorRadius = (float)getValueByType("MinorRadius", index, typeof(float));
			break;
		}
		case "annularsector":
		{
			AnnularSector annularSector = shape as AnnularSector;
			annularSector.OuterRadius = (float)getValueByType("OuterRadius", index, typeof(float));
			annularSector.InnerRadius = (float)getValueByType("InnerRadius", index, typeof(float));
			annularSector.StartAngle = (float)getValueByType("StartAngle", index, typeof(float));
			annularSector.AngleRange = (float)getValueByType("AngleRange", index, typeof(float));
			break;
		}
		case "text":
		{
			Text text = shape as Text;
			text.Content = (string)getValueByType("Content", index, typeof(string));
			text.PositionX = (float)getValueByType("PositionX", index, typeof(float));
			text.PositionY = (float)getValueByType("PositionY", index, typeof(float));
			text.FontSize = (int)getValueByType("FontSize", index, typeof(int));
			text.Width = (float)getValueByType("Width", index, typeof(float));
			text.Height = (float)getValueByType("Height", index, typeof(float));
			break;
		}
		case "datarecord":
		{
			DataRecord dataRecord = shape as DataRecord;
			dataRecord.Content = (string)getValueByType("Content", index, typeof(string));
			break;
		}
		case "datalist":
		{
			DataList dataList = shape as DataList;
			dataList.Colume = (object[])getValueByType("Colume", index, typeof(object[]));
			break;
		}
		case "rect":
		{
			Apps.Data.ShapeDefine.Rect rect = shape as Apps.Data.ShapeDefine.Rect;
			rect.Width = (float)getValueByType("Width", index, typeof(float));
			rect.Height = (float)getValueByType("Height", index, typeof(float));
			rect.Angle = (float)getValueByType("Angle", index, typeof(float));
			rect.SkewAngleX = (float)getValueByType("SkewAngleX", index, typeof(float));
			break;
		}
		case "simplerect":
		{
			SimpleRect simpleRect = shape as SimpleRect;
			simpleRect.Width = (float)getValueByType("Width", index, typeof(float));
			simpleRect.Height = (float)getValueByType("Height", index, typeof(float));
			simpleRect.Angle = (float)getValueByType("Angle", index, typeof(float));
			break;
		}
		case "line":
		{
			Apps.Data.ShapeDefine.Line line = shape as Apps.Data.ShapeDefine.Line;
			line.StartLineX = (float)getValueByType("StartLineX", index, typeof(float));
			line.StartLineY = (float)getValueByType("StartLineY", index, typeof(float));
			line.EndLineX = (float)getValueByType("EndLineX", index, typeof(float));
			line.EndLineY = (float)getValueByType("EndLineY", index, typeof(float));
			line.IsThroughLine = (bool)getValueByType("IsThroughLine", index, typeof(bool));
			break;
		}
		case "point":
			break;
		case "outlinepoint":
		{
			Outlinepoint outlinepoint = shape as Outlinepoint;
			outlinepoint.OutLineInfo = (ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data)getValueByType("OutLineInfo", index, typeof(ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data));
			break;
		}
		case "coordinate":
		{
			Coordinate coordinate = shape as Coordinate;
			coordinate.CoorDXVecX = (float)getValueByType("CoorDXVecX", index, typeof(float));
			coordinate.CoorDXVecY = (float)getValueByType("CoorDXVecY", index, typeof(float));
			coordinate.CoorDYVecX = (float)getValueByType("CoorDYVecX", index, typeof(float));
			coordinate.CoorDYVecY = (float)getValueByType("CoorDYVecY", index, typeof(float));
			break;
		}
		case "micropoint":
			break;
		case "pointset":
			break;
		case "polygon":
		{
			Apps.Data.ShapeDefine.Polygon polygon = shape as Apps.Data.ShapeDefine.Polygon;
			polygon.PointNum = (int)getValueByType("PointNum", index, typeof(int));
			polygon.PointXArray = getValueByType("PointXArray", index, typeof(Array)) as Array;
			polygon.PointYArray = getValueByType("PointYArray", index, typeof(Array)) as Array;
			int num33 = 0;
			for (int i = 0; i < index; i++)
			{
				num33 += (int)getValueByType("PointNum", i, typeof(int));
			}
			if (polygon.PointNum <= 2 || polygon.PointXArray == null || polygon.PointYArray == null)
			{
				break;
			}
			for (int j = 0; j < polygon.PointNum; j++)
			{
				if (num33 < polygon.PointXArray.Length && num33 < polygon.PointYArray.Length)
				{
					double result = 0.0;
					double result2 = 0.0;
					if (double.TryParse(polygon.PointXArray.GetValue(num33)?.ToString(), out result) && double.TryParse(polygon.PointYArray.GetValue(num33)?.ToString(), out result2))
					{
						polygon.PolygonPoints.Add(new System.Windows.Point(result, result2));
					}
					num33++;
				}
			}
			break;
		}
		case "polyline":
			break;
		case "datachart":
		{
			Datachart datachart = shape as Datachart;
			datachart.VerticalAxis = (float)getValueByType("VerticalAxis", index, typeof(float));
			break;
		}
		case "hsilist":
		{
			HsiList hsiList = shape as HsiList;
			hsiList.ModelHData = (float)getValueByType("ModelHData", index, typeof(float));
			hsiList.ModelSData = (float)getValueByType("ModelSData", index, typeof(float));
			hsiList.ModelIData = (float)getValueByType("ModelIData", index, typeof(float));
			hsiList.SampleHData = (float)getValueByType("SampleHData", index, typeof(float));
			hsiList.SampleSData = (float)getValueByType("SampleSData", index, typeof(float));
			hsiList.SampleIData = (float)getValueByType("SampleIData", index, typeof(float));
			break;
		}
		case "path":
			break;
		case "arc":
		{
			Arc arc = shape as Arc;
			arc.MajorRadius = (float)getValueByType("MajorRadius", index, typeof(float));
			arc.MinorRadius = (float)getValueByType("MinorRadius", index, typeof(float));
			arc.Angle = (float)getValueByType("Angle", index, typeof(float));
			arc.StartAngle = (float)getValueByType("StartAngle", index, typeof(float));
			arc.EndAngle = (float)getValueByType("EndAngle", index, typeof(float));
			break;
		}
		case "imagetitle":
		{
			ImageTitle imageTitle = shape as ImageTitle;
			imageTitle.Content = (string)getValueByType("Content", index, typeof(string));
			break;
		}
		case "lineangle":
		{
			LineAngle lineAngle = shape as LineAngle;
			lineAngle.StartLine1X = (float)getValueByType("StartLine1X", index, typeof(float));
			lineAngle.StartLine1Y = (float)getValueByType("StartLine1Y", index, typeof(float));
			lineAngle.EndLine1X = (float)getValueByType("EndLine1X", index, typeof(float));
			lineAngle.EndLine1Y = (float)getValueByType("EndLine1Y", index, typeof(float));
			lineAngle.IntersectionX = (float)getValueByType("IntersectionX", index, typeof(float));
			lineAngle.IntersectionY = (float)getValueByType("IntersectionY", index, typeof(float));
			lineAngle.StartLine2X = (float)getValueByType("StartLine2X", index, typeof(float));
			lineAngle.StartLine2Y = (float)getValueByType("StartLine2Y", index, typeof(float));
			lineAngle.EndLine2X = (float)getValueByType("EndLine2X", index, typeof(float));
			lineAngle.EndLine2Y = (float)getValueByType("EndLine2Y", index, typeof(float));
			lineAngle.Angle = (float)getValueByType("Angle", index, typeof(float));
			break;
		}
		case "ellipse":
		{
			Apps.Data.ShapeDefine.Ellipse ellipse = shape as Apps.Data.ShapeDefine.Ellipse;
			ellipse.MajorRadius = (float)getValueByType("MajorRadius", index, typeof(float));
			ellipse.MinorRadius = (float)getValueByType("MinorRadius", index, typeof(float));
			ellipse.Angle = (float)getValueByType("Angle", index, typeof(float));
			break;
		}
		case "linechart":
		{
			LineChart lineChart = shape as LineChart;
			lineChart.X = (Array)GetValueByType("X", index, typeof(Array));
			lineChart.Y = (Array)GetValueByType("Y", index, typeof(Array));
			break;
		}
		case "pointxyz":
		{
			PointXYZ pointXYZ = shape as PointXYZ;
			pointXYZ.CenterX = ((GetValueByType("CenterX", index, typeof(float)) is float num34) ? num34 : 0f);
			pointXYZ.CenterY = ((GetValueByType("CenterY", index, typeof(float)) is float num35) ? num35 : 0f);
			pointXYZ.CenterZ = ((GetValueByType("CenterZ", index, typeof(float)) is float num36) ? num36 : 0f);
			break;
		}
		case "line3d":
		{
			Line3D line3D = shape as Line3D;
			line3D.StartLineX = (float)getValueByType("StartLineX", index, typeof(float));
			line3D.StartLineY = (float)getValueByType("StartLineY", index, typeof(float));
			line3D.EndLineX = (float)getValueByType("EndLineX", index, typeof(float));
			line3D.EndLineY = (float)getValueByType("EndLineY", index, typeof(float));
			float num23 = (float)getValueByType("Xscale", index, typeof(float));
			float num24 = (float)getValueByType("Yscale", index, typeof(float));
			int num25 = (int)getValueByType("Xoffset", index, typeof(int));
			int num26 = (int)getValueByType("Yoffset", index, typeof(int));
			int num27 = (int)getValueByType("ImageWidth", index, typeof(int));
			int num28 = (int)getValueByType("ImageHeight", index, typeof(int));
			float num29 = Math.Abs((line3D.StartLineX - (float)num25) / num23);
			float num30 = Math.Abs((line3D.StartLineY - (float)num26) / num24);
			float num31 = Math.Abs((line3D.EndLineX - (float)num25) / num23);
			float num32 = Math.Abs((line3D.EndLineY - (float)num26) / num24);
			if (num29 >= 0f && num29 < (float)num27 && num30 >= 0f && num30 < (float)num28 && num31 >= 0f && num31 < (float)num27 && num32 >= 0f && num32 < (float)num28)
			{
				line3D.StartLineX = num29;
				line3D.StartLineY = num30;
				line3D.EndLineX = num31;
				line3D.EndLineY = num32;
			}
			line3D.IsThroughLine = (bool)getValueByType("IsThroughLine", index, typeof(bool));
			break;
		}
		case "point3d":
		{
			Point3D point3D = shape as Point3D;
			float num17 = (float)getValueByType("Xscale", index, typeof(float));
			float num18 = (float)getValueByType("Yscale", index, typeof(float));
			int num19 = (int)getValueByType("Xoffset", index, typeof(int));
			int num20 = (int)getValueByType("Yoffset", index, typeof(int));
			int num21 = (int)getValueByType("ImageWidth", index, typeof(int));
			int num22 = (int)getValueByType("ImageHeight", index, typeof(int));
			float centerX = (point3D.CenterX - (float)num19) / num17;
			float centerY = (point3D.CenterY - (float)num20) / num18;
			point3D.CenterX = centerX;
			point3D.CenterY = centerY;
			break;
		}
		case "grasppoint":
			try
			{
				GraspPoint graspPoint = shape as GraspPoint;
				double num = (double)getValueByType("PoseMatOut0", index, typeof(double));
				double num2 = (double)getValueByType("PoseMatOut1", index, typeof(double));
				double num3 = (double)getValueByType("PoseMatOut2", index, typeof(double));
				double num4 = (double)getValueByType("PoseMatOut3", index, typeof(double));
				double num5 = (double)getValueByType("PoseMatOut4", index, typeof(double));
				double num6 = (double)getValueByType("PoseMatOut5", index, typeof(double));
				double num7 = (double)getValueByType("PoseMatOut6", index, typeof(double));
				double num8 = (double)getValueByType("PoseMatOut7", index, typeof(double));
				double num9 = (double)getValueByType("PoseMatOut8", index, typeof(double));
				double num10 = (double)getValueByType("PoseMatOut9", index, typeof(double));
				double num11 = (double)getValueByType("PoseMatOut10", index, typeof(double));
				double num12 = (double)getValueByType("PoseMatOut11", index, typeof(double));
				double num13 = (double)getValueByType("PoseMatOut12", index, typeof(double));
				double num14 = (double)getValueByType("PoseMatOut13", index, typeof(double));
				double num15 = (double)getValueByType("PoseMatOut14", index, typeof(double));
				double num16 = (double)getValueByType("PoseMatOut15", index, typeof(double));
				graspPoint.Position = new double[16]
				{
					num, num2, num3, num4, num5, num6, num7, num8, num9, num10,
					num11, num12, num13, num14, num15, num16
				};
				graspPoint.Name = index.ToString();
				break;
			}
			catch (Exception)
			{
				break;
			}
		}
	}

	protected static Shape GetShapeEx(string shapeType)
	{
		shapeType = shapeType.ToLower();
		return shapeType switch
		{
			"line3d" => new Line3D(), 
			"point3d" => new Point3D(), 
			"linechart" => new LineChart(), 
			"pointxyz" => new PointXYZ(), 
			"grasppoint" => new GraspPoint(), 
			_ => ObjectBase.GetShape(shapeType), 
		};
	}

	private void SetBaseProperty(int index, Shape shape, Func<string, int, Type, object> getValueByType, Dictionary<string, string> customConfig = null)
	{
		if (shape == null)
		{
			return;
		}
		shape.Tips = (string)getValueByType("Tips", index, typeof(string));
		shape.CenterX = (float)getValueByType("CenterX", index, typeof(float));
		shape.CenterY = (float)getValueByType("CenterY", index, typeof(float));
		shape.HighLightIndex = GetStringFromStruct(getValueByType("HighLightIndex", index, typeof(string)));
		shape.Opacity = opacityA;
		shape.FillColor = (string)getValueByType("FillColor", index, typeof(string));
		shape.StrokeThickness = StrokeThickness;
		shape.MatrixConvertEnable = UseConvertMatrix;
		if (shape is Outlinepoint outlinepoint)
		{
			outlinepoint.Color = okcolor;
			outlinepoint.NgColorA = ngcolor;
			return;
		}
		shape.Color = (((int)getValueByType("Status", index, typeof(int)) == 0) ? ngcolor : okcolor);
		if (IsFill)
		{
			shape.FillColor = shape.Color;
		}
	}

	private object SetCustomPropertyValueByName(Dictionary<string, string> customConfig, string propertyName)
	{
		PropertyInfo propertyInfo;
		if ((object)(propertyInfo = GetType().GetProperties().FirstOrDefault((PropertyInfo p) => p.Name == propertyName)) != null)
		{
			if (customConfig != null && customConfig.Keys.Contains(propertyName))
			{
				TypeConverter converter = TypeDescriptor.GetConverter(propertyInfo.PropertyType);
				if (converter != null && converter.CanConvertFrom(typeof(string)))
				{
					return converter.ConvertFromInvariantString(customConfig[propertyName]);
				}
			}
			return propertyInfo.GetValue(this);
		}
		throw new ArgumentException("SetCustomPropertyValueByName");
	}

	private object GetValueByTypeDir(string featureName, int index, Type type)
	{
		if (featureName == null)
		{
			return null;
		}
		try
		{
			if (FeaturesDic.TryGetValue(featureName.GetHashCode(), out var feature))
			{
				object obj = feature.GetValue(uint.MaxValue, index);
				if (obj != null)
				{
					if (obj.GetType() != type)
					{
						if (feature.Type == "Array")
						{
							return obj;
						}
						return ObjectBase.ConvertValue(obj, type);
					}
					return obj;
				}
			}
			return ObjectBase.GetDefaultValue(type);
		}
		catch (Exception ex)
		{
			LogHelper.Error("异常信息：" + ex.Message);
			LogHelper.Error("堆栈信息：" + ex.StackTrace);
			return ObjectBase.GetDefaultValue(type);
		}
	}

	private object GetValueByType(string featureName, int index, Type type)
	{
		if (featureName == null)
		{
			return null;
		}
		try
		{
			if (FeaturesDic.TryGetValue(featureName.GetHashCode(), out var feature))
			{
				int num = FeaturesStoreIndexDic[feature.GetHashCode()];
				Array array = ValueStore[num] as Array;
				if (feature.Type == "Array")
				{
					return array;
				}
				int num2 = array?.Length ?? 0;
				index = ((index < num2) ? index : 0);
				object obj = array?.GetValue(index);
				if (obj != null)
				{
					if (obj is ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX iMVS_MODULE_STRING_VALUE_EX)
					{
						obj = iMVS_MODULE_STRING_VALUE_EX.strValue;
					}
					if (obj.GetType() != type)
					{
						return ObjectBase.ConvertValue(obj, type);
					}
					return obj;
				}
			}
			return ObjectBase.GetDefaultValue(type);
		}
		catch (Exception ex)
		{
			LogHelper.Error("异常信息：" + ex.Message);
			LogHelper.Error("堆栈信息：" + ex.StackTrace);
			return ObjectBase.GetDefaultValue(type);
		}
	}

	public object GetRecordContent(ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX_Data result)
	{
		if (result.nStatus != 1 && result.nStatus != 5)
		{
			return AppLocalizationService.GetLang("Status") + ":0 " + string.Format("{0}:{1:X000} ", AppLocalizationService.GetLang("ErrorCode"), result.nErrorCode) + AppLocalizationService.GetLang("ErrorInfo") + ":" + ErrorCodeInfo.GetErrorInfo((int)result.nErrorCode);
		}
		if (valueExecuteCount == result.nExecuteCount)
		{
			if (value is List<object> source)
			{
				return source.OfType<DataRecord>()?.Select((DataRecord p) => p.Content)?.ToArray();
			}
		}
		else
		{
			FeatureBase featureBase = FeaturesB.FirstOrDefault((FeatureBase p) => p.NameA == "Content");
			Feature feature;
			if ((feature = featureBase as Feature) != null && feature.MappingArray != null)
			{
				int num = feature.MappingArray.Length;
				object[] array = new object[num];
				int i;
				for (i = 0; i < num; i++)
				{
					ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_EX_Data struResultInfo = result.pInfo.FirstOrDefault((ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_EX_Data p) => p.strParamName == feature.MappingArray[i]);
					if (struResultInfo.strParamName == feature.MappingArray[i])
					{
						array[i] = ModuleManager.GetValueFromServer(struResultInfo);
					}
				}
				if (array.Count((object p) => p != null) > 0)
				{
					return feature.GetValue(array);
				}
			}
		}
		return new string[1] { "" };
	}

	public List<VmIO> GetAllTopMappingIO()
	{
		List<VmIO> ios = new List<VmIO>();
		foreach (FeatureBase item in FeaturesB)
		{
			ios.AddRange(item.vmIOs);
		}
		ios = ios.Distinct().ToList();
		bool flag = true;
		while (true)
		{
			int i = 0;
			while (true)
			{
				if (i < Enumerable.Count(ios))
				{
					if (ios.Any((VmIO p) => ios[i].Parent == p))
					{
						break;
					}
					i++;
					continue;
				}
				ios = ios.Distinct().ToList();
				while (true)
				{
					bool isDone = false;
					ios = ios.Distinct().ToList();
					IEnumerable<IGrouping<object, VmIO>> source = from p in ios
						group p by p.Parent;
					source.ToList().ForEach((IGrouping<object, VmIO> p) =>
					{
						if (p.Key is VmIO vmIO && vmIO.Children.Count == p.Count())
						{
							foreach (VmIO item2 in p)
							{
								ios[ios.IndexOf(item2)] = vmIO;
							}
							isDone = true;
						}
					});
					if (!isDone)
					{
						break;
					}
				}
				ios = ios.Distinct().ToList();
				return ios;
			}
			ios[i] = ios[i].Parent as VmIO;
		}
	}

	public IVmIO GetDirectMappingIO()
	{
		List<IVmIO> list = new List<IVmIO>();
		foreach (FeatureBase item in FeaturesB)
		{
			if (item.MappingArray != null && item.MappingArray.Length == 1)
			{
				VmIO outputFilter = item.GetOutputFilter(Owner, item.MappingArray[0]);
				string type = null;
				switch (Type)
				{
				case "pixelinfoimage":
					type = "PIXELIMAGE";
					break;
				case "image":
					type = "IMAGE";
					break;
				case "PointCloud3DImage":
					type = "POINTCLOUD";
					break;
				}
				IVmIO parentIOByType;
				if (outputFilter != null && (parentIOByType = GetParentIOByType(outputFilter, type)) != null && !list.Contains(parentIOByType))
				{
					list.Add(parentIOByType);
				}
			}
		}
		return (list.Count > 0) ? GetTopItem(list) : null;
	}

	private IVmIO GetParentIOByType(IVmIO io, string type = null)
	{
		if (io == null)
		{
			return null;
		}
		while (io.Parent is IVmIO)
		{
			io = io.Parent as IVmIO;
			if (type != null && io.TypeName == type)
			{
				break;
			}
		}
		return io;
	}

	private IVmIO GetTopItem(List<IVmIO> ios)
	{
		IVmIO vmIO = ios[0];
		if (ios.Count > 1)
		{
			for (int i = 1; i < ios.Count; i++)
			{
				if (vmIO.Parent == ios[i])
				{
					vmIO = ios[i];
				}
			}
		}
		return vmIO;
	}

	public IVmIO GetMappingIO()
	{
		List<IVmIO> list = new List<IVmIO>();
		foreach (FeatureBase item2 in FeaturesB)
		{
			if (item2.MappingArray != null && item2.MappingArray.Length == 1)
			{
				VmIO vmIO = item2.GetOutputFilter(Owner, item2.MappingArray[0]);
				if (vmIO != null && !vmIO.IsOutput && vmIO?.RelateIO != null && !(vmIO.Root is IVmModuleSet))
				{
					vmIO = vmIO.RelateIO;
				}
				if (vmIO != null && vmIO.Parent is IVmIO item && !list.Contains(item))
				{
					list.Add(item);
				}
			}
		}
		return (list.Count > 0) ? GetTopItem(list) : null;
	}

	private List<IVmModule> GetMappingModules()
	{
		List<VmIO> list = new List<VmIO>();
		foreach (FeatureBase item in FeaturesB)
		{
			list.AddRange(item.vmIOs);
		}
		list = list.Distinct().ToList();
		return ((IEnumerable<VmIO>)list).Select((Func<VmIO, IVmModule>)((VmIO p) => p.Root)).Distinct().ToList();
	}

	public Feature GetFeatureByName(string name)
	{
		if (Features != null && Features.Count > 0)
		{
			try
			{
				return Features.FirstOrDefault((FeatureBase P) => P.Name.Equals(name)) as Feature;
			}
			catch
			{
			}
		}
		return null;
	}

	public override string ToString()
	{
		return FullName;
	}
}
