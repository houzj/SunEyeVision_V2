using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Apps.Data;
using Apps.Data.ShapeDefine;
using Apps.Interface;
using Apps.Log;
using Hik.Xml;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser;

public class ObjectBase : BaseParamNode, IGetResultValue, IDisplayObject, IIsVisible, IGetDataPath, IVmDisplayObject, IGetProcessID, IUpdateBridgeBase
{
	[XmlMark(true)]
	public string imagefilter;

	[XmlMark(true)]
	public string Type;

	[XmlMark(true)]
	public string okcolor;

	[XmlMark(true)]
	public string ngcolor;

	[XmlMark(true)]
	public string hotcolor;

	[XmlMark(true)]
	public float opacity;

	[XmlMark(true)]
	public int linetype;

	[XmlMark(true)]
	public float linewidth;

	[XmlMark(true)]
	public bool IsDisplay;

	[XmlMark(true)]
	public double arrowSize;

	[XmlMark(true)]
	public bool needLabel;

	[XmlMark(true)]
	public int highLightRowNum = 1;

	[XmlMark(true)]
	public bool highLightEnable = true;

	[XmlMark(true)]
	public bool needContentTip = false;

	[XmlMark(true)]
	public bool IsFill = false;

	[XmlMark(true)]
	public float StrokeThickness = 1f;

	[XmlMark(true)]
	public bool ROIDisable;

	[XmlMark(true)]
	public bool IsLowQuality = false;

	[XmlMark(true)]
	public bool IsShowIndex = false;

	[XmlMark(true)]
	public bool UseConvertMatrix = false;

	[XmlMark(true)]
	public int renderShapePosition = 0;

	[XmlMark(true)]
	public int profileShowByRangeRownum = 0;

	private VmModule OwnerModule = null;

	private int? _okColor_int;

	private int? _ngColor_int;

	private object _Owner;

	public ObservableCollection<FeatureBase> Features;

	private Dictionary<string, string> typeDicts = new Dictionary<string, string>();

	public int OkColor_int
	{
		get
		{
			if (!_okColor_int.HasValue)
			{
				_okColor_int = XmlColorToInt(okcolor);
			}
			return _okColor_int.Value;
		}
		set
		{
			_okColor_int = value;
		}
	}

	public int NgColor_int
	{
		get
		{
			if (!_ngColor_int.HasValue)
			{
				_ngColor_int = XmlColorToInt(ngcolor);
			}
			return _ngColor_int.Value;
		}
		set
		{
			_ngColor_int = value;
		}
	}

	public virtual string okcolorA
	{
		get
		{
			return okcolor;
		}
		set
		{
			if (okcolor != value)
			{
				_okColor_int = XmlColorToInt(value);
				okcolor = value;
				OnPropertyChanged("okcolorA");
				SaveOkcolor(okcolor, NameA);
				if (value != null)
				{
					ClearTempValue();
					OnValueChanged(new EventArgs());
				}
			}
		}
	}

	public virtual string ngcolorA
	{
		get
		{
			return ngcolor;
		}
		set
		{
			if (ngcolor != value)
			{
				_ngColor_int = XmlColorToInt(value);
				ngcolor = value;
				OnPropertyChanged("ngcolorA");
				SaveNgcolor(ngcolor, NameA);
				if (value != null)
				{
					ClearTempValue();
					OnValueChanged(new EventArgs());
				}
			}
		}
	}

	public virtual float opacityA
	{
		get
		{
			return opacity;
		}
		set
		{
			if (opacity != value)
			{
				opacity = value;
				OnPropertyChanged("opacityA");
				SaveOpacity(opacity, NameA);
				ClearTempValue();
				OnValueChanged(new EventArgs());
			}
		}
	}

	public virtual float StrokeThicknessA
	{
		get
		{
			return StrokeThickness;
		}
		set
		{
			if (StrokeThickness != value)
			{
				StrokeThickness = value;
				OnPropertyChanged("StrokeThicknessA");
				SaveStrokeThickness(StrokeThickness, NameA);
				ClearTempValue();
				OnValueChanged(new EventArgs());
			}
		}
	}

	public virtual bool IsFillA
	{
		get
		{
			return IsFill;
		}
		set
		{
			if (IsFill != value)
			{
				IsFill = value;
				OnPropertyChanged("IsFillA");
				SaveIsFill(IsFill, NameA);
				ClearTempValue();
				OnValueChanged(new EventArgs());
			}
		}
	}

	public virtual bool IsDisplayA
	{
		get
		{
			return IsDisplay;
		}
		set
		{
			if (IsDisplay != value)
			{
				IsDisplay = value;
				OnPropertyChanged("IsDisplayA");
				SaveIsDisplay(IsDisplay, NameA);
				ClearTempValue();
				OnValueChanged(new EventArgs());
			}
		}
	}

	public bool IsVisible => IsDisplay;

	public object Owner
	{
		get
		{
			if (_Owner == null && GetTopBaseNode() is IOwner owner)
			{
				_Owner = owner.Owner;
			}
			return _Owner;
		}
	}

	public int LastValueCount { get; set; }

	public int LineType
	{
		get
		{
			return linetype;
		}
		set
		{
			if (linewidth != (float)value)
			{
				linetype = value;
				OnPropertyChanged("LineType");
			}
		}
	}

	public int LineWidth
	{
		get
		{
			return (int)linewidth;
		}
		set
		{
			if (linewidth != (float)value)
			{
				linewidth = value;
				OnPropertyChanged("LineWidth");
			}
		}
	}

	public bool NeedLabel { get; set; }

	public int ArrowSize { get; set; }

	public List<IFeature> FeaturesA => Features.OfType<IFeature>().ToList();

	public ObservableCollection<FeatureBase> FeaturesB => Features;

	public string TypeA
	{
		get
		{
			return Type;
		}
		set
		{
			Type = value;
			SaveType(Type, NameA);
			OnPropertyChanged("TypeA");
		}
	}

	int IGetProcessID.ProcessID => (int)(((Owner as IVmModule)?.Root as IVmModule)?.ID ?? 0);

	int IGetProcessID.ModuleID => (int)((Owner as IVmModule)?.ID ?? 0);

	public event EventHandler ValueChanged;

	[field: NonSerialized]
	public virtual event EventHandler DisplayChanged;

	static ObjectBase()
	{
		ConvertValue(null, typeof(double));
	}

	public ObjectBase()
	{
		IsValueChangedNull();
	}

	public VmModule GetVmModule()
	{
		if (OwnerModule == null)
		{
			if (!(GetTopBaseNode() is ParamRoot paramRoot))
			{
				return null;
			}
			OwnerModule = paramRoot.Owner as VmModule;
			return OwnerModule;
		}
		return OwnerModule;
	}

	public void UpdateValue(EventArgs args)
	{
		OnValueChanged(args);
	}

	internal void OnValueChanged(EventArgs args)
	{
		try
		{
			ValueChanged?.Invoke(this, args);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message + ex.StackTrace);
		}
	}

	protected bool IsValueChangedNull()
	{
		return ValueChanged == null;
	}

	internal virtual void ClearTempValue()
	{
	}

	internal void SaveData(object data, string name, string tailName)
	{
		try
		{
			VmModule vmModule;
			if ((vmModule = GetVmModule()) != null)
			{
				vmModule.UserData["[ParamRoot]_" + name + tailName] = ((data == null) ? null : StringUtil.UTF8GetBytesPadZero(data.ToString()));
			}
		}
		catch
		{
		}
	}

	internal void SaveOkcolor(string data, string name)
	{
		SaveData(data, name, "okcolor");
	}

	internal void SaveNgcolor(string data, string name)
	{
		SaveData(data, name, "ngcolor");
	}

	internal void SaveOpacity(float? data, string name)
	{
		SaveData(data, name, "opacity");
	}

	protected void SaveStrokeThickness(float? data, string name)
	{
		SaveData(data, name, "StrokeThickness");
	}

	protected void SaveIsFill(bool? data, string name)
	{
		SaveData(data, name, "IsFill");
	}

	protected void SaveIsDisplay(bool? data, string name)
	{
		SaveData(data, name, "IsDisplay");
	}

	public static int XmlColorToInt(string xmlString)
	{
		if (xmlString != "")
		{
			return ColorTranslator.FromHtml(xmlString).ToArgb();
		}
		return 0;
	}

	protected virtual void OnNameChanged(string oldName)
	{
		SaveIsDisplay(null, oldName);
		SaveIsDisplay(IsDisplay, NameA);
		SaveIsFill(null, oldName);
		SaveIsFill(IsFill, NameA);
		SaveStrokeThickness(null, oldName);
		SaveStrokeThickness(StrokeThickness, NameA);
		SaveOkcolor(null, oldName);
		SaveOkcolor(okcolor, NameA);
		SaveNgcolor(null, oldName);
		SaveNgcolor(ngcolor, NameA);
		SaveOpacity(null, oldName);
		SaveOpacity(opacity, NameA);
		SaveType(null, oldName);
		SaveType(Type, NameA);
	}

	public void UpdateResultShowParam(List<ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO> infos)
	{
		try
		{
			byte[] array = infos?.FirstOrDefault((ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO p) => StringUtil.UTF8GetString(p.strParamName) == "[ParamRoot]_" + NameA + "IsDisplay")?.pData;
			byte[] array2 = infos?.FirstOrDefault((ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO p) => StringUtil.UTF8GetString(p.strParamName) == "[ParamRoot]_" + NameA + "okcolor")?.pData;
			byte[] array3 = infos?.FirstOrDefault((ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO p) => StringUtil.UTF8GetString(p.strParamName) == "[ParamRoot]_" + NameA + "ngcolor")?.pData;
			byte[] array4 = infos?.FirstOrDefault((ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO p) => StringUtil.UTF8GetString(p.strParamName) == "[ParamRoot]_" + NameA + "opacity")?.pData;
			byte[] array5 = infos?.FirstOrDefault((ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO p) => StringUtil.UTF8GetString(p.strParamName) == "[ParamRoot]_" + NameA + "Type")?.pData;
			byte[] array6 = infos?.FirstOrDefault((ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO p) => StringUtil.UTF8GetString(p.strParamName) == "[ParamRoot]_" + NameA + "IsFill")?.pData;
			byte[] array7 = infos?.FirstOrDefault((ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO p) => StringUtil.UTF8GetString(p.strParamName) == "[ParamRoot]_" + NameA + "StrokeThickness")?.pData;
			if (array != null)
			{
				IsDisplay = Convert.ToBoolean(StringUtil.UTF8GetString(array));
			}
			if (array2 != null)
			{
				okcolor = StringUtil.UTF8GetString(array2);
				_okColor_int = XmlColorToInt(okcolor);
			}
			if (array3 != null)
			{
				ngcolor = StringUtil.UTF8GetString(array3);
				_ngColor_int = XmlColorToInt(ngcolor);
			}
			if (array4 != null)
			{
				opacity = Convert.ToSingle(StringUtil.UTF8GetString(array4));
			}
			if (array5 != null)
			{
				Type = StringUtil.UTF8GetString(array5);
			}
			if (array6 != null)
			{
				IsFill = Convert.ToBoolean(StringUtil.UTF8GetString(array6));
			}
			if (array7 != null)
			{
				StrokeThickness = Convert.ToSingle(StringUtil.UTF8GetString(array7));
			}
			Object obj = null;
			if (this is DynamicObject)
			{
				obj = DisplayRoot.GetInstance().Items.OfType<Object>().FirstOrDefault((Object p) => p.Type == Type);
			}
			foreach (FeatureBase feature in FeaturesB)
			{
				FeatureBase featureBase = null;
				if (obj != null)
				{
					featureBase = obj.FeaturesB.FirstOrDefault((FeatureBase p) => p.Name == feature.Name);
				}
				byte[] array8 = infos?.FirstOrDefault((ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO p) => StringUtil.UTF8GetString(p.strParamName) == "[ParamRoot]_" + (feature.GetParentNode() as Object).NameA + "." + feature.NameA + ".Value")?.pData;
				if (array8 != null)
				{
					feature.Value = StringUtil.UTF8GetString(array8);
				}
				else if (featureBase != null)
				{
					feature.Value = featureBase.Value;
				}
				array8 = infos?.FirstOrDefault((ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO p) => StringUtil.UTF8GetString(p.strParamName) == "[ParamRoot]_" + (feature.GetParentNode() as Object).NameA + "." + feature.NameA + ".Mapping")?.pData;
				if (array8 != null)
				{
					feature.Mapping = StringUtil.UTF8GetString(array8);
				}
				if (featureBase != null)
				{
					feature.Type = featureBase.Type;
				}
				feature.Initializer();
			}
		}
		catch
		{
		}
	}

	public object GetValue(uint excuteCount, int index = -1, Dictionary<string, string> customConfig = null)
	{
		switch (TypeA)
		{
		case "image":
		case "sampleimage":
		case "pixelinfoimage":
			return GetImageValue(excuteCount);
		case "shepematrix":
			return GetMatrixValue(excuteCount);
		case "PointCloud3DImage":
			return GetImageValueList(excuteCount);
		default:
			return GetValueList(TypeA, excuteCount, customConfig);
		}
	}

	public virtual void SetOutputFilter()
	{
		if (Owner is IVmModule vmModule)
		{
			vmModule.SubscribeCount++;
		}
	}

	public virtual void RemoveOutputFilter()
	{
		if (Owner is IVmModule { SubscribeCount: >0 } vmModule)
		{
			vmModule.SubscribeCount--;
		}
	}

	protected virtual object GetValueList(string typeName, uint excuteCount, Dictionary<string, string> customConfig = null)
	{
		return null;
	}

	private object GetShapeValue(string shapeType, int index, uint excuteCount, Dictionary<string, string> customConfig = null)
	{
		object shape = GetShape(shapeType);
		SetBaseProperty(index, excuteCount, shape as Shape, customConfig);
		FieldInfo[] array = shape?.GetType().GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in array)
		{
			string name = fieldInfo.Name;
			Type fieldType = fieldInfo.FieldType;
			fieldInfo.SetValue(shape, GetValueByType(fieldInfo.Name, index, excuteCount, fieldInfo.FieldType));
		}
		return shape;
	}

	protected static Shape GetShape(string shapeType)
	{
		shapeType = shapeType.ToLower();
		return shapeType switch
		{
			"circle" => (Shape)new Apps.Data.ShapeDefine.Circle(), 
			"annularsector" => new AnnularSector(), 
			"text" => new Text(), 
			"datarecord" => new DataRecord(), 
			"datalist" => new DataList(), 
			"rect" => new Rect(), 
			"simplerect" => new SimpleRect(), 
			"line" => new Apps.Data.ShapeDefine.Line(), 
			"point" => new Apps.Data.ShapeDefine.Point(), 
			"outlinepoint" => new Outlinepoint(), 
			"coordinate" => new Coordinate(), 
			"micropoint" => new MicroPoint(), 
			"pointset" => new PointSet(), 
			"polygon" => new Apps.Data.ShapeDefine.Polygon(), 
			"polyline" => new Polyline(), 
			"datachart" => new Datachart(), 
			"hsilist" => new HsiList(), 
			"path" => new Path(), 
			"arc" => new Arc(), 
			"imagetitle" => new ImageTitle(), 
			"lineangle" => new LineAngle(), 
			"ellipse" => new Apps.Data.ShapeDefine.Ellipse(), 
			_ => null, 
		};
	}

	private void SetBaseProperty(int index, uint excuteCount, Shape shape, Dictionary<string, string> customConfig = null)
	{
		shape.Tips = (string)GetValueByType("Tips", index, excuteCount, typeof(string));
		shape.CenterX = (float)GetValueByType("CenterX", index, excuteCount, typeof(float));
		shape.CenterY = (float)GetValueByType("CenterY", index, excuteCount, typeof(float));
		shape.HighLightIndex = GetStringFromStruct(GetValueByType("HighLightIndex", index, excuteCount, typeof(string)));
		shape.Opacity = (float)SetCustomPropertyValueByName(customConfig, "opacityA");
		shape.FillColor = (string)GetValueByType("FillColor", index, excuteCount, typeof(string));
		shape.StrokeThickness = (float)SetCustomPropertyValueByName(customConfig, "StrokeThicknessA");
		if (shape is Outlinepoint outlinepoint)
		{
			outlinepoint.Color = SetCustomPropertyValueByName(customConfig, "okcolorA") as string;
			outlinepoint.NgColorA = SetCustomPropertyValueByName(customConfig, "ngcolorA") as string;
			return;
		}
		shape.Color = (((int)GetValueByType("Status", index, excuteCount, typeof(int)) == 0) ? (SetCustomPropertyValueByName(customConfig, "ngcolorA") as string) : (SetCustomPropertyValueByName(customConfig, "okcolorA") as string));
		if (IsFill)
		{
			shape.FillColor = shape.Color;
		}
	}

	protected string GetStringFromStruct(object stringStructArray)
	{
		string text = "";
		if (stringStructArray is string)
		{
			return (string)stringStructArray;
		}
		if (stringStructArray is Array { Length: >0 } array)
		{
			for (int i = 0; i < array.Length; i++)
			{
				object value = array.GetValue(i);
				if (value != null && value is ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX iMVS_MODULE_STRING_VALUE_EX)
				{
					text += iMVS_MODULE_STRING_VALUE_EX.strValue;
				}
			}
		}
		return text;
	}

	private object SetCustomPropertyValueByName(Dictionary<string, string> customConfig, string propertyName)
	{
		PropertyInfo propertyInfo;
		if ((object)(propertyInfo = GetType().GetProperties().FirstOrDefault((PropertyInfo p) => p.Name == propertyName)) != null)
		{
			if (customConfig != null && customConfig.Keys.Contains(propertyName))
			{
				TypeConverter converter = TypeDescriptor.GetConverter(propertyInfo.PropertyType);
				if (converter.CanConvertFrom(typeof(string)))
				{
					return converter.ConvertFromInvariantString(customConfig[propertyName]);
				}
			}
			return propertyInfo.GetValue(this);
		}
		throw new ArgumentException("SetCustomPropertyValueByName");
	}

	private object GetImageValue(uint excuteCount, int index = 0)
	{
		byte[] array = null;
		string text = null;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		if (FeaturesB != null)
		{
			FeatureBase featureBase = FeaturesB.OfType<FeatureBase>().FirstOrDefault((FeatureBase p) => p.NameA == "Image");
			if (featureBase != null)
			{
				object value = featureBase.GetValue(excuteCount, index);
				if (value is ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data)
				{
					ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data iMVS_MODULE_CHUNK_VALUE_EX_Data = (ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data)value;
					if (iMVS_MODULE_CHUNK_VALUE_EX_Data.IsMemory)
					{
						text = iMVS_MODULE_CHUNK_VALUE_EX_Data.GetRealMemoryAddress();
					}
					else
					{
						array = iMVS_MODULE_CHUNK_VALUE_EX_Data.GetData();
					}
					num = ((GetValueByType("Height", index, excuteCount, typeof(int)) is int num4) ? num4 : 0);
					num2 = ((GetValueByType("Width", index, excuteCount, typeof(int)) is int num5) ? num5 : 0);
					num3 = ((GetValueByType("PixelFormat", index, excuteCount, typeof(int)) is int num6) ? num6 : 0);
					if (num == 0 || num2 == 0 || (array == null && text == null))
					{
						return null;
					}
					ImageData imageData = new ImageData();
					try
					{
						imageData.NeedConvertColor = GetValueByType("NeedConvertColor", index, excuteCount, typeof(int)) is int num7 && num7 == 1;
						imageData.PixelInfoLabel = (string)GetValueByType("PixelInfoLabel", index, excuteCount, typeof(string));
						for (int num8 = 0; num8 < GetValueCount("PixelInfoValue"); num8++)
						{
							imageData.PixelExtraInfo.Add((string)GetValueByType("PixelInfoValue", num8, excuteCount, typeof(string)), (string)GetValueByType("PixelInfo", num8, excuteCount, typeof(string)));
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
					imageData.ExecuteCount = (Owner as IVmModule)?.ExecuteCount ?? 0;
					return imageData;
				}
				if (value is ImageData)
				{
					return value;
				}
				return null;
			}
			return null;
		}
		return null;
	}

	private object GetImageValueList(uint excuteCount)
	{
		FeatureBase featureBase;
		if (FeaturesB != null && (featureBase = FeaturesB.OfType<FeatureBase>().FirstOrDefault((FeatureBase p) => p.NameA == "Image")) != null && featureBase != null)
		{
			int valueCount = GetValueCount(featureBase.Name);
			ImageData imageData = new PointCloudImageData();
			List<ImageData> list = new List<ImageData>();
			for (int num = 0; num < valueCount; num++)
			{
				list.Add(GetImageValue(excuteCount, num) as ImageData);
			}
			if (list.Count > 0)
			{
				imageData.DetailedPixelFormat = list[0].DetailedPixelFormat;
				(imageData as PointCloudImageData).SubDatas = list;
				FeatureBase featureBase2;
				if ((featureBase2 = FeaturesB.OfType<FeatureBase>().FirstOrDefault((FeatureBase p) => p.NameA == "NormalImage")) != null && featureBase2 != null)
				{
					int valueCount2 = GetValueCount(featureBase2.Name);
					if (valueCount2 == valueCount)
					{
						List<ImageData> list2 = new List<ImageData>();
						for (int num2 = 0; num2 < valueCount2; num2++)
						{
							list2.Add(GetPcdNormalImageValue(excuteCount, num2) as ImageData);
						}
						(imageData as PointCloudImageData).NormalDatas = list2;
					}
				}
				return imageData;
			}
			return null;
		}
		return null;
	}

	private object GetPcdNormalImageValue(uint excuteCount, int index = 0)
	{
		byte[] array = null;
		string text = null;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		if (FeaturesB != null)
		{
			FeatureBase featureBase = FeaturesB.OfType<FeatureBase>().FirstOrDefault((FeatureBase o) => o.NameA == "NormalImage");
			if (featureBase != null)
			{
				object value = featureBase.GetValue(excuteCount, index);
				if (value is ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data)
				{
					ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data iMVS_MODULE_CHUNK_VALUE_EX_Data = (ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data)value;
					if (iMVS_MODULE_CHUNK_VALUE_EX_Data.IsMemory)
					{
						text = iMVS_MODULE_CHUNK_VALUE_EX_Data.GetRealMemoryAddress();
					}
					else
					{
						array = iMVS_MODULE_CHUNK_VALUE_EX_Data.GetData();
					}
					num = ((GetValueByType("NormalHeight", index, excuteCount, typeof(int)) is int num4) ? num4 : 0);
					num2 = ((GetValueByType("NormalWidth", index, excuteCount, typeof(int)) is int num5) ? num5 : 0);
					num3 = ((GetValueByType("NormalPixelFormat", index, excuteCount, typeof(int)) is int num6) ? num6 : 0);
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
				if (value is ImageData)
				{
					return value;
				}
				return null;
			}
			return null;
		}
		return null;
	}

	private object GetMatrixValue(uint excuteCount)
	{
		if (FeaturesB != null)
		{
			ConvertMatrix convertMatrix = new ConvertMatrix();
			FeatureBase featureBase = FeaturesB.OfType<FeatureBase>().FirstOrDefault((FeatureBase p) => p.NameA == "Matrix");
			if (featureBase != null)
			{
				object value = featureBase.GetValue(excuteCount, 0);
				if (value is Array { Length: >=9 } array)
				{
					convertMatrix.MatrixList = new List<double>();
					for (int num = 0; num < 9; num++)
					{
						object value2 = array.GetValue(num);
						if (value2 != null)
						{
							convertMatrix.MatrixList.Add((float)value2);
							continue;
						}
						return null;
					}
				}
				return convertMatrix;
			}
			return null;
		}
		return null;
	}

	private object GetValueByType(string featureName, int index, uint excuteCount, Type type)
	{
		FeatureBase featureBase = FeaturesB.OfType<FeatureBase>().FirstOrDefault((FeatureBase p) => p.NameA == featureName);
		if (featureBase != null)
		{
			object value = featureBase.GetValue(excuteCount, index);
			if (value != null)
			{
				if (value.GetType() != type)
				{
					if (featureBase.Type == "Array")
					{
						return value;
					}
					return ConvertValue(value, type);
				}
				return value;
			}
		}
		return GetDefaultValue(type);
	}

	protected static object GetDefaultValue(Type type)
	{
		return System.Type.GetTypeCode(type) switch
		{
			TypeCode.Int32 => (object)0, 
			TypeCode.String => "", 
			TypeCode.Single => 0f, 
			TypeCode.Boolean => false, 
			_ => null, 
		};
	}

	protected static object ConvertValue(object obj, Type type)
	{
		try
		{
			if (type == typeof(int))
			{
				return Convert.ToInt32(obj.ToString());
			}
			if (type == typeof(string))
			{
				return obj.ToString();
			}
			if (type == typeof(float))
			{
				return Convert.ToSingle(obj.ToString());
			}
			if (type == typeof(bool))
			{
				return Convert.ToBoolean(obj);
			}
		}
		catch
		{
		}
		return GetDefaultValue(type);
	}

	protected int GetValueCount(string featureName)
	{
		return FeaturesB.OfType<FeatureBase>().FirstOrDefault((FeatureBase p) => p.NameA == featureName)?.GetValueCount() ?? 0;
	}

	internal void SaveType(string data, string name)
	{
		SaveData(data, name, "Type");
	}

	public string GetDataPath()
	{
		return (this as IVmFullPath)?.FullPath;
	}

	public void UpdateAllUserData()
	{
	}

	public void UpdateSignleBase(string key, IRefreshBridgeObject brigeBase)
	{
		string name = key.Replace("[ParamRoot]_" + Name, "");
		byte[] array = GetVmModule()?.UserData[key];
		if (array != null)
		{
			SetFiledValueByName(name, BuildFunDecator(array, name));
		}
	}

	private object BuildFunDecator(byte[] data, string name)
	{
		string text = null;
		if (!typeDicts.ContainsKey(name))
		{
			text = GetType().GetField(name)?.FieldType.Name;
			typeDicts.Add(name, text);
		}
		return typeDicts[name] switch
		{
			"Signle" => (object)Convert.ToSingle(StringUtil.UTF8GetString(data)), 
			"String" => StringUtil.UTF8GetString(data), 
			"Boolean" => Convert.ToBoolean(StringUtil.UTF8GetString(data)), 
			_ => null, 
		};
	}
}
