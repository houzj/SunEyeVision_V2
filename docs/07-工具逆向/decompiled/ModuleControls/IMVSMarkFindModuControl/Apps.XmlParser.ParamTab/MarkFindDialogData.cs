using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Hik.Xml;
using HikExternalCall.PatCreate;
using HikPcUI.ImageView;
using VM.ModuleControl;
using VM.Utility;
using VM.Utility.SingleInstance;

namespace Apps.XmlParser.ParamTab;

public class MarkFindDialogData : CompressMaskData, INotifyPropertyChanged
{
	public string FeatureMatchName = string.Empty;

	public static string curVersion = "V3.0.0 build0927";

	public string version;

	public int PatVersion;

	public ImageData CurImageData;

	public ImageData ModelImageData;

	public byte[] otherMask;

	public byte[] ModelData;

	private byte[] MaskTemp;

	public ObservableCollection<object> _ItemsData = new ObservableCollection<object>();

	private ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> _PointsData = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();

	public double _FixPointX = 0.0;

	public double _FixPointY = 0.0;

	public SDKFeatureMatchData SDKImportData;

	private Assembly assembly = null;

	public byte[] MarkModelData;

	public int _PyramidScaleMode = 1;

	public float _RoughPyramidScale = 3f;

	public float _FinePyramidScale = 3f;

	public int _ThresholdMode = 1;

	public int _HighThreshold = 20;

	public int _FeatureMatchType = 1;

	public int _RotationAngle = 10;

	public float _RotationStep = 0.5f;

	public int _ProjectionInterval = 3;

	public int _MarkType = 2;

	public int _MaskType = 0;

	public bool _IsExpand = false;

	private FILTER_POINT_F _centerPoint = default;

	private FILTER_LINE_I[] _outputLine = new FILTER_LINE_I[4];

	private bool _BackEnable = false;

	private bool _ForwardEnable = false;

	public ObservableCollection<object> ItemsData
	{
		get
		{
			return _ItemsData;
		}
		set
		{
			if (_ItemsData != value)
			{
				_ItemsData = value;
				NotifyPropertyChanged("ItemsData");
			}
		}
	}

	public ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> PointsData
	{
		get
		{
			return _PointsData;
		}
		set
		{
			if (_PointsData != value)
			{
				_PointsData = value;
			}
			NotifyPropertyChanged("PointsData");
			NotifyPropertyChanged("EditOutlinePointsEnable");
		}
	}

	public bool EditOutlinePointsEnable => PointsData != null && PointsData.Count > 0;

	public double FixPointX
	{
		get
		{
			return _FixPointX;
		}
		set
		{
			if (_FixPointX != value)
			{
				_FixPointX = value;
				NotifyPropertyChanged("FixPointX");
			}
		}
	}

	public double FixPointY
	{
		get
		{
			return _FixPointY;
		}
		set
		{
			if (_FixPointY != value)
			{
				_FixPointY = value;
				NotifyPropertyChanged("FixPointY");
			}
		}
	}

	public Assembly Assembly
	{
		get
		{
			if (assembly == null)
			{
				string currentPath = CurrentPathHelper.Instance.GetCurrentPath();
				currentPath += "Module(sp)\\x64\\.CommonControl\\PatCreateCSharp.dll";
				try
				{
					assembly = Assembly.UnsafeLoadFrom(currentPath);
				}
				catch (Exception arg)
				{
					LogHelper.objLog.Error($"Get assembly error:{arg},assembly path={currentPath}");
				}
			}
			return assembly;
		}
	}

	public int PyramidscaleModeSel
	{
		get
		{
			return (_PyramidScaleMode != 1) ? 1 : 0;
		}
		set
		{
			int num = ((value != 1) ? 1 : 0);
			if (_PyramidScaleMode != num)
			{
				_PyramidScaleMode = num;
				NotifyPropertyChanged("PyramidscaleMode");
				NotifyPropertyChanged("PyramidscaleEnable");
			}
		}
	}

	public bool PyramidscaleEnable
	{
		get
		{
			if (_PyramidScaleMode == 1)
			{
				return false;
			}
			return true;
		}
	}

	public float RoughPyramidscale
	{
		get
		{
			return (float)Math.Round(_RoughPyramidScale, 1);
		}
		set
		{
			float num = (float)Math.Round(value, 1);
			if ((float)Math.Round(_RoughPyramidScale, 1) != num && num >= 1f && num <= 20f)
			{
				_RoughPyramidScale = num;
				float num2 = (float)Math.Floor(_RoughPyramidScale);
				if (num2 < _FinePyramidScale)
				{
					_FinePyramidScale = num2;
				}
				NotifyPropertyChanged("FinePyramidscale");
				NotifyPropertyChanged("RoughPyramidscale");
				if (_PyramidScaleMode == 0)
				{
					OnMatchCommand();
				}
			}
		}
	}

	public float FinePyramidscale
	{
		get
		{
			return (float)Math.Round(_FinePyramidScale, 1);
		}
		set
		{
			float num = (float)Math.Round(value, 1);
			if ((float)Math.Round(_FinePyramidScale, 1) != num && num >= 1f)
			{
				_FinePyramidScale = num;
				NotifyPropertyChanged("FinePyramidscale");
				if (_PyramidScaleMode == 0)
				{
					OnMatchCommand();
				}
			}
		}
	}

	public int ThresholdModeSel
	{
		get
		{
			return (_ThresholdMode != 1) ? 1 : 0;
		}
		set
		{
			int num = ((value != 1) ? 1 : 0);
			if (_ThresholdMode != num)
			{
				_ThresholdMode = num;
				NotifyPropertyChanged("ThresholdMode");
				NotifyPropertyChanged("ThresholdEnable");
				NotifyPropertyChanged("HighThreshold");
			}
		}
	}

	public bool ThresholdEnable
	{
		get
		{
			if (_ThresholdMode == 1)
			{
				return false;
			}
			return true;
		}
	}

	public int HighThreshold
	{
		get
		{
			return _HighThreshold;
		}
		set
		{
			if (_HighThreshold != value && value >= 1 && value <= 255)
			{
				_HighThreshold = value;
				NotifyPropertyChanged("HighThreshold");
				if (_ThresholdMode == 0)
				{
					OnMatchCommand();
				}
			}
		}
	}

	public int FeatureMatchType
	{
		get
		{
			return _FeatureMatchType;
		}
		set
		{
			if (value != _FeatureMatchType)
			{
				_FeatureMatchType = value;
				NotifyPropertyChanged("FeatureMatchType");
			}
		}
	}

	public int RotationAngle
	{
		get
		{
			return _RotationAngle;
		}
		set
		{
			if (_RotationAngle != value)
			{
				_RotationAngle = value;
				NotifyPropertyChanged("RotationAngle");
				OnMatchCommand();
			}
		}
	}

	public float RotationStep
	{
		get
		{
			return _RotationStep;
		}
		set
		{
			if (_RotationStep != value)
			{
				_RotationStep = value;
				NotifyPropertyChanged("RotationStep");
				OnMatchCommand();
			}
		}
	}

	public int ProjectionInterval
	{
		get
		{
			return _ProjectionInterval;
		}
		set
		{
			if (_ProjectionInterval != value)
			{
				_ProjectionInterval = value;
				NotifyPropertyChanged("ProjectionInterval");
				OnMatchCommand();
			}
		}
	}

	public int MarkType
	{
		get
		{
			return _MarkType;
		}
		set
		{
			if (_MarkType != value)
			{
				_MarkType = value;
				NotifyPropertyChanged("MarkType");
				OnMatchCommand();
			}
		}
	}

	public int MaskType
	{
		get
		{
			return _MaskType;
		}
		set
		{
			if (_MaskType != value)
			{
				_MaskType = value;
				NotifyPropertyChanged("MaskType");
			}
		}
	}

	public bool IsExpand
	{
		get
		{
			return _IsExpand;
		}
		set
		{
			if (_IsExpand != value)
			{
				_IsExpand = value;
				NotifyPropertyChanged("IsExpand");
			}
		}
	}

	public string HolderMsg { get; set; }

	public FILTER_POINT_F centerPoint
	{
		get
		{
			return _centerPoint;
		}
		set
		{
			_centerPoint = value;
			NotifyPropertyChanged("centerPoint");
		}
	}

	public FILTER_LINE_I[] outputLine
	{
		get
		{
			return _outputLine;
		}
		set
		{
			_outputLine = value;
			NotifyPropertyChanged("outputLine");
		}
	}

	[XmlIgnore]
	public bool BackEnable
	{
		get
		{
			return _BackEnable;
		}
		set
		{
			if (_BackEnable != value)
			{
				_BackEnable = value;
				NotifyPropertyChanged("BackEnable");
			}
		}
	}

	[XmlIgnore]
	public bool ForwardEnable
	{
		get
		{
			return _ForwardEnable;
		}
		set
		{
			if (_ForwardEnable != value)
			{
				_ForwardEnable = value;
				NotifyPropertyChanged("ForwardEnable");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event Action MatchCommand;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		((BaseNode)this).Init(node);
		if (base.compressMask != null)
		{
			otherMask = ((CompressMaskData)this).CompressToNormalMask(base.compressMask, 1);
		}
		return (BaseNode)(object)this;
	}

	protected override void BeforSaveBaseNode()
	{
		if (otherMask != null)
		{
			base.compressMask = ((CompressMaskData)this).NormalToCompressMask(otherMask, 1);
			MaskTemp = otherMask;
			otherMask = null;
		}
		else
		{
			base.compressMask = null;
		}
	}

	protected override void AfterSaveBaseNode()
	{
		otherMask = MaskTemp;
		MaskTemp = null;
	}

	public virtual void DoClear()
	{
		ItemsData.Clear();
		otherMask = null;
	}

	public MarkFindDialogData()
	{
		FeatureMatchName = "IMVSMarkFindModu";
	}

	protected void OnMatchCommand()
	{
		MatchCommand?.Invoke();
	}

	internal void SetMarkTypeNoTrigger(int value)
	{
		if (_MarkType != value)
		{
			_MarkType = value;
			NotifyPropertyChanged("MarkType");
		}
	}

	public static void DeepCopyModelBigData(MarkFindDialogData curModelData, MarkFindDialogData tempData)
	{
		if (curModelData == null || tempData == null)
		{
			return;
		}
		int num = curModelData.CurImageData.Height * curModelData.CurImageData.Width;
		tempData.CurImageData.ImageBuffer = new byte[num];
		Buffer.BlockCopy(curModelData.CurImageData.ImageBuffer, 0, tempData.CurImageData.ImageBuffer, 0, num);
		if (curModelData.ModelImageData != null)
		{
			tempData.ModelImageData = new ImageData();
			int num2 = curModelData.ModelImageData.Height * curModelData.ModelImageData.Width * 3;
			tempData.ModelImageData.ImageBuffer = new byte[num2];
			Buffer.BlockCopy(curModelData.ModelImageData.ImageBuffer, 0, tempData.ModelImageData.ImageBuffer, 0, num2);
			tempData.ModelImageData.Height = curModelData.ModelImageData.Height;
			tempData.ModelImageData.Width = curModelData.ModelImageData.Width;
			tempData.ModelImageData.PixelFormat = curModelData.ModelImageData.PixelFormat;
		}
		if (curModelData.otherMask != null)
		{
			tempData.otherMask = new byte[num];
			Buffer.BlockCopy(curModelData.otherMask, 0, tempData.otherMask, 0, num);
		}
		if (curModelData.ModelData != null)
		{
			tempData.ModelData = new byte[curModelData.ModelData.Length];
			Buffer.BlockCopy(curModelData.ModelData, 0, tempData.ModelData, 0, curModelData.ModelData.Length);
		}
		tempData.PointsData = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();
		foreach (MVBPATMATCH_EDGEPOINT_OUT pointsDatum in curModelData.PointsData)
		{
			tempData.PointsData.Add(pointsDatum);
		}
	}

	public MarkFindDialogData DeepCopyModelData(bool excludeBigData = false, bool isSaveImage = false)
	{
		if (this == null)
		{
			return null;
		}
		MarkFindDialogData markFindDialogData = new MarkFindDialogData();
		if (CurImageData != null)
		{
			markFindDialogData.CurImageData = new ImageData();
			markFindDialogData.CurImageData.Height = CurImageData.Height;
			markFindDialogData.CurImageData.Width = CurImageData.Width;
			markFindDialogData.CurImageData.PixelFormat = CurImageData.PixelFormat;
			if (!excludeBigData)
			{
				DeepCopyModelBigData(this, markFindDialogData);
			}
		}
		markFindDialogData.PatVersion = PatVersion;
		markFindDialogData.version = version;
		markFindDialogData.ItemsData = new ObservableCollection<object>();
		foreach (object itemsDatum in ItemsData)
		{
			object item = MemoryHelper.DeepCopyObject(itemsDatum);
			markFindDialogData.ItemsData.Add(item);
		}
		markFindDialogData.HighThreshold = HighThreshold;
		markFindDialogData.FixPointX = FixPointX;
		markFindDialogData.FixPointY = FixPointY;
		markFindDialogData.RoughPyramidscale = RoughPyramidscale;
		markFindDialogData.FinePyramidscale = FinePyramidscale;
		markFindDialogData.PyramidscaleModeSel = PyramidscaleModeSel;
		markFindDialogData.ThresholdModeSel = ThresholdModeSel;
		markFindDialogData.FeatureMatchType = FeatureMatchType;
		markFindDialogData.RotationAngle = RotationAngle;
		markFindDialogData.RotationStep = RotationStep;
		markFindDialogData.ProjectionInterval = ProjectionInterval;
		markFindDialogData.MarkType = MarkType;
		markFindDialogData.MaskType = MaskType;
		markFindDialogData.IsExpand = IsExpand;
		markFindDialogData.centerPoint = new FILTER_POINT_F
		{
			x = centerPoint.x,
			y = centerPoint.y
		};
		markFindDialogData.outputLine = new FILTER_LINE_I[outputLine.Length];
		for (int i = 0; i < outputLine.Length; i++)
		{
			markFindDialogData.outputLine[i].point_start.x = outputLine[i].point_start.x;
			markFindDialogData.outputLine[i].point_start.y = outputLine[i].point_start.y;
			markFindDialogData.outputLine[i].point_end.x = outputLine[i].point_end.x;
			markFindDialogData.outputLine[i].point_end.y = outputLine[i].point_end.y;
		}
		return markFindDialogData;
	}

	private static void GetWiderPoint(double x, double y, ref double minX, ref double minY, ref double maxX, ref double maxY)
	{
		if (x < minX)
		{
			minX = x;
		}
		if (y < minY)
		{
			minY = y;
		}
		if (x > maxX)
		{
			maxX = x;
		}
		if (y > maxY)
		{
			maxY = y;
		}
	}

	private static void SetImagePointGreen(byte[] imageBuffer, int index)
	{
		if (index + 2 < imageBuffer.Length)
		{
			imageBuffer[index] = 0;
			imageBuffer[index + 1] = byte.MaxValue;
			imageBuffer[index + 2] = 0;
		}
	}

	public static void RefreshModelImageData(MarkFindDialogData model)
	{
		if (model == null || model.CurImageData == null || model.ItemsData == null)
		{
			return;
		}
		double minX = model.CurImageData.Width;
		double minY = model.CurImageData.Height;
		double maxX = 0.0;
		double maxY = 0.0;
		if (model.ItemsData.Count > 0)
		{
			foreach (object itemsDatum in model.ItemsData)
			{
				if (itemsDatum is HikCruciformRoiParameter)
				{
					HikCruciformRoiParameter hikCruciformRoiParameter = itemsDatum as HikCruciformRoiParameter;
					RotateTransform rotateTransform = new RotateTransform(hikCruciformRoiParameter.Angle, (hikCruciformRoiParameter.PolygonPoints[0].X + hikCruciformRoiParameter.PolygonPoints[6].X) / 2f, (hikCruciformRoiParameter.PolygonPoints[0].Y + hikCruciformRoiParameter.PolygonPoints[6].Y) / 2f);
					foreach (PolygonPoint polygonPoint in hikCruciformRoiParameter.PolygonPoints)
					{
						Point point = rotateTransform.Transform(new Point(polygonPoint.X, polygonPoint.Y));
						GetWiderPoint((int)point.X, (int)point.Y, ref minX, ref minY, ref maxX, ref maxY);
					}
				}
				else
				{
					if (!(itemsDatum is HikCoverHollowRoiParameter))
					{
						continue;
					}
					HikCoverHollowRoiParameter hikCoverHollowRoiParameter = itemsDatum as HikCoverHollowRoiParameter;
					RotateTransform rotateTransform2 = new RotateTransform(hikCoverHollowRoiParameter.Angle, (hikCoverHollowRoiParameter.PolygonPoints[0].X + hikCoverHollowRoiParameter.PolygonPoints[2].X) / 2f, (hikCoverHollowRoiParameter.PolygonPoints[0].Y + hikCoverHollowRoiParameter.PolygonPoints[2].Y) / 2f);
					foreach (PolygonPoint polygonPoint2 in hikCoverHollowRoiParameter.PolygonPoints)
					{
						Point point2 = rotateTransform2.Transform(new Point(polygonPoint2.X, polygonPoint2.Y));
						GetWiderPoint((int)point2.X, (int)point2.Y, ref minX, ref minY, ref maxX, ref maxY);
					}
				}
			}
		}
		minX = Math.Max(minX, 0.0);
		minY = Math.Max(minY, 0.0);
		maxX = Math.Min(maxX, model.CurImageData.Width);
		maxY = Math.Min(maxY, model.CurImageData.Height);
		int num = (int)(maxX - minX);
		int num2 = (int)(maxY - minY);
		int num3 = 0;
		int num4 = 0;
		double num5 = (double)num2 / (double)num;
		if (num5 <= 229.0 / 322.0)
		{
			num3 = 322;
			num4 = (int)((double)num3 * num5 + 0.5);
		}
		else
		{
			num4 = 229;
			num3 = (int)((double)num4 / num5 + 0.5);
		}
		if (model.ModelImageData == null)
		{
			model.ModelImageData = new ImageData();
		}
		model.ModelImageData.ImageBuffer = new byte[num3 * num4 * 3];
		model.ModelImageData.Height = num4;
		model.ModelImageData.Width = num3;
		model.ModelImageData.PixelFormat = PixelFormats.Rgb24;
		for (int i = 0; i < num4; i++)
		{
			for (int j = 0; j < num3; j++)
			{
				int num6 = (int)(minX + (double)j / (double)num3 * (double)num);
				int num7 = (int)(minY + (double)i / (double)num4 * (double)num2);
				model.ModelImageData.ImageBuffer[3 * (i * num3 + j)] = model.CurImageData.ImageBuffer[num7 * model.CurImageData.Width + num6];
				model.ModelImageData.ImageBuffer[3 * (i * num3 + j) + 1] = model.CurImageData.ImageBuffer[num7 * model.CurImageData.Width + num6];
				model.ModelImageData.ImageBuffer[3 * (i * num3 + j) + 2] = model.CurImageData.ImageBuffer[num7 * model.CurImageData.Width + num6];
			}
		}
		foreach (MVBPATMATCH_EDGEPOINT_OUT pointsDatum in model.PointsData)
		{
			int num8 = (int)(((double)pointsDatum.x - minX) / (double)num * (double)num3 + 0.5);
			int num9 = (int)(((double)pointsDatum.y - minY) / (double)num2 * (double)num4 + 0.5);
			if (num8 < 0 || num9 < 0 || num8 > num3 || num9 > num4)
			{
				continue;
			}
			try
			{
				int index = 3 * (num9 * num3 + num8);
				SetImagePointGreen(model.ModelImageData.ImageBuffer, index);
				index = 3 * ((num9 + 1) * num3 + num8);
				SetImagePointGreen(model.ModelImageData.ImageBuffer, index);
				if (num8 < num3 - 1)
				{
					index = 3 * (num9 * num3 + num8 + 1);
					SetImagePointGreen(model.ModelImageData.ImageBuffer, index);
					index = 3 * ((num9 + 1) * num3 + num8 + 1);
					SetImagePointGreen(model.ModelImageData.ImageBuffer, index);
				}
			}
			catch
			{
			}
		}
	}

	public int GetOutLinePoints()
	{
		int num = -1;
		try
		{
			if (CurImageData == null)
			{
				throw new Exception(AppLocalizationService.GetLang("Lang_NoImageErr"));
			}
			if (ItemsData == null || ItemsData.Count == 0)
			{
				throw new Exception(AppLocalizationService.GetLang("Lang_NoROIRegion"));
			}
			Type type = Assembly?.GetType("PatCreateCSharp.PatCreateManagerCSharp");
			PropertyInfo property = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			dynamic value = property.GetValue(null, null);
			ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> pointsData = null;
			num = value.CreateMarkFindPatE(this, ref pointsData);
			if (num != 0)
			{
				throw new Exception(AppLocalizationService.GetLang("Lang_CreateModelError") + ":" + ErrorCodeInfo.GetErrorInfo(num));
			}
			PointsData = pointsData;
			RefreshModelImageData(this);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"Creat Model Err:{ex.Message}");
		}
		return num;
	}
}
