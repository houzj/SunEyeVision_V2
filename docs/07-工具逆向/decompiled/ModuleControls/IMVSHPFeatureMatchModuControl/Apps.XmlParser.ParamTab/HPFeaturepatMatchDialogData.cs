using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using Hik.Xml;
using HikPcUI.ImageView;
using Mvvm.Commands;
using VM.ModuleControl;
using VM.ModuleControl.Controls.FeatureMatch;
using VM.Utility;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class HPFeaturepatMatchDialogData : FeaturepatMatchDialogDataBase
{
	public int _PyramidScaleMode = 1;

	public float _RoughPyramidScale = 3f;

	public float _FinePyramidScale = 3f;

	public int _ThresholdMode = 1;

	public int _HighThreshold = 20;

	public int _chainFlag = 1;

	public int _minChainLen = 4;

	public byte[] otherMask2;

	public bool _IsUsePen = false;

	public byte[] maskForwardPaint;

	private byte[] MaskForwardPaintTemp;

	private List<IList> _ROILists;

	private Type _DrawROIType;

	private bool _IsStateForDrawROI;

	private IList _SelectedROIList;

	private ICommand _AddROICommand;

	private bool _MatchPointLock = false;

	protected bool IsMatching { get; set; }

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
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("PyramidscaleMode");
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("PyramidscaleEnable");
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

	public virtual float RoughPyramidscale
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
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("FinePyramidscale");
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("RoughPyramidscale");
				if (_PyramidScaleMode == 0 && !IsMatching)
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
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("FinePyramidscale");
				if (_PyramidScaleMode == 0 && !IsMatching)
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
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("ThresholdMode");
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("ThresholdEnable");
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("HighThreshold");
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("LowThreshold");
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
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("HighThreshold");
				if (_ThresholdMode == 0 && !IsMatching)
				{
					OnMatchCommand();
				}
			}
		}
	}

	public int ChainFlag
	{
		get
		{
			return _chainFlag;
		}
		set
		{
			((FeaturepatMatchDialogDataBase)this).SetProperty<int>(ref _chainFlag, value, "ChainFlag");
		}
	}

	public int MinChainLen
	{
		get
		{
			return _minChainLen;
		}
		set
		{
			if (_minChainLen != value)
			{
				_minChainLen = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("MinChainLen");
				if (_chainFlag == 0 && !IsMatching)
				{
					OnMatchCommand();
				}
			}
		}
	}

	public bool IsUsePen
	{
		get
		{
			return _IsUsePen;
		}
		set
		{
			if (_IsUsePen != value)
			{
				_IsUsePen = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("IsUsePen");
			}
		}
	}

	public object ROILists
	{
		get
		{
			if (_ROILists == null && base._ItemsData != null)
			{
				RebuildROILists();
			}
			return _ROILists;
		}
	}

	public Type DrawROIType
	{
		get
		{
			return _DrawROIType;
		}
		set
		{
			if (_DrawROIType != value)
			{
				_DrawROIType = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("DrawROIType");
			}
		}
	}

	public bool IsStateForDrawROI
	{
		get
		{
			return _IsStateForDrawROI;
		}
		set
		{
			if (_IsStateForDrawROI != value)
			{
				_IsStateForDrawROI = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("IsStateForDrawROI");
			}
		}
	}

	public IList SelectedROIList
	{
		get
		{
			return _SelectedROIList;
		}
		set
		{
			if (_SelectedROIList != value)
			{
				_SelectedROIList = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("SelectedROIList");
			}
		}
	}

	public ICommand AddROICommand => _AddROICommand ?? (_AddROICommand = (ICommand)new DelegateCommand<object>((Action<object>)AddROICommandExecute));

	public bool MatchPointLock
	{
		get
		{
			return _MatchPointLock;
		}
		set
		{
			if (_MatchPointLock != value)
			{
				_MatchPointLock = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("MatchPointLock");
			}
		}
	}

	public event Action MatchCommand;

	public HPFeaturepatMatchDialogData()
	{
		Init();
		base.version = FeaturepatMatchDialogDataBase.curVersion;
	}

	protected virtual void OnMatchCommand()
	{
		IsMatching = true;
		MatchCommand?.Invoke();
		IsMatching = false;
	}

	protected virtual void Init()
	{
		base.FeatureMatchName = "IMVSHPFeatureMatchModu";
	}

	public override FeaturepatMatchDialogDataBase DeepCopyModelData(bool excludeBigData = false, bool isSaveImage = false)
	{
		HPFeaturepatMatchDialogData tempData = GetTempData();
		if (((FeaturepatMatchDialogDataBase)this).CurImageDataA != null)
		{
			((FeaturepatMatchDialogDataBase)tempData).CurImageDataA = new ImageData();
			((FeaturepatMatchDialogDataBase)tempData).CurImageDataA.Height = ((FeaturepatMatchDialogDataBase)this).CurImageDataA.Height;
			((FeaturepatMatchDialogDataBase)tempData).CurImageDataA.Width = ((FeaturepatMatchDialogDataBase)this).CurImageDataA.Width;
			((FeaturepatMatchDialogDataBase)tempData).CurImageDataA.PixelFormat = ((FeaturepatMatchDialogDataBase)this).CurImageDataA.PixelFormat;
			if (!excludeBigData)
			{
				FeatureMatchHelper.DeepCopyModelBigData((FeaturepatMatchDialogDataBase)(object)this, (FeaturepatMatchDialogDataBase)(object)tempData);
				if (otherMask2 != null)
				{
					int num = ((FeaturepatMatchDialogDataBase)this).CurImageDataA.Height * ((FeaturepatMatchDialogDataBase)this).CurImageDataA.Width;
					tempData.otherMask2 = new byte[num];
					Buffer.BlockCopy(otherMask2, 0, tempData.otherMask2, 0, num);
				}
			}
		}
		((FeaturepatMatchDialogDataBase)tempData).PatVersion = base.PatVersion;
		((FeaturepatMatchDialogDataBase)tempData).version = base.version;
		tempData.IsUsePen = IsUsePen;
		((FeaturepatMatchDialogDataBase)tempData).ItemsData = new ObservableCollection<IROI>();
		foreach (IROI itemsDatum in ((FeaturepatMatchDialogDataBase)this).ItemsData)
		{
			IROI item = MemoryHelper.DeepCopyObject(itemsDatum) as IROI;
			((FeaturepatMatchDialogDataBase)tempData).ItemsData.Add(item);
		}
		tempData.HighThreshold = HighThreshold;
		((FeaturepatMatchDialogDataBase)tempData).FixPointX = ((FeaturepatMatchDialogDataBase)this).FixPointX;
		((FeaturepatMatchDialogDataBase)tempData).FixPointY = ((FeaturepatMatchDialogDataBase)this).FixPointY;
		tempData.RoughPyramidscale = RoughPyramidscale;
		tempData.PyramidscaleModeSel = PyramidscaleModeSel;
		tempData.ThresholdModeSel = ThresholdModeSel;
		tempData.MinChainLen = MinChainLen;
		tempData.ChainFlag = ChainFlag;
		CopyParam(tempData, this);
		if (isSaveImage)
		{
			((FeaturepatMatchDialogDataBase)tempData).CurImageDataA = null;
		}
		return (FeaturepatMatchDialogDataBase)(object)tempData;
	}

	protected virtual void CopyParam(HPFeaturepatMatchDialogData data, HPFeaturepatMatchDialogData curData)
	{
		data.FinePyramidscale = curData.FinePyramidscale;
	}

	protected virtual HPFeaturepatMatchDialogData GetTempData()
	{
		return new HPFeaturepatMatchDialogData();
	}

	public override int GetOutLinePoints()
	{
		int result = -1;
		try
		{
			if (((FeaturepatMatchDialogDataBase)this).CurImageDataA == null)
			{
				throw new Exception(AppLocalizationService.GetLang("Lang_NoImageErr"));
			}
			if (((FeaturepatMatchDialogDataBase)this).ItemsData == null || ((FeaturepatMatchDialogDataBase)this).ItemsData.Count == 0)
			{
				throw new Exception(AppLocalizationService.GetLang("Lang_NoROIRegion"));
			}
			Type type = ((FeaturepatMatchDialogDataBase)this).Assembly?.GetType("PatCreateCSharp.PatCreateManagerCSharp");
			PropertyInfo property = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			dynamic value = property.GetValue(null, null);
			result = CreatePatE(value);
			FeatureMatchHelper.RefreshModelImageData((FeaturepatMatchDialogDataBase)(object)this, false);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error($"Creat Model Err:{ex.Message}");
		}
		return result;
	}

	protected virtual int CreatePatE(dynamic dynamicObject)
	{
		return dynamicObject.CreateDipolePatE(this);
	}

	protected override void BeforSaveBaseNode()
	{
		((FeaturepatMatchDialogDataBase)this).BeforSaveBaseNode();
		if (otherMask2 != null)
		{
			maskForwardPaint = ((CompressMaskData)this).NormalToCompressMask(otherMask2, 1);
			MaskForwardPaintTemp = otherMask2;
			otherMask2 = null;
		}
		else
		{
			maskForwardPaint = null;
		}
	}

	protected override void AfterSaveBaseNode()
	{
		((FeaturepatMatchDialogDataBase)this).AfterSaveBaseNode();
		otherMask2 = MaskForwardPaintTemp;
		MaskForwardPaintTemp = null;
	}

	public override BaseNode Init(XmlNode node)
	{
		((FeaturepatMatchDialogDataBase)this).Init(node);
		if (maskForwardPaint != null)
		{
			otherMask2 = ((CompressMaskData)this).CompressToNormalMask(maskForwardPaint, 1);
		}
		return (BaseNode)(object)this;
	}

	private void RebuildROILists()
	{
		_ROILists = new List<IList>();
		_ROILists.Add(base._ItemsData);
		_ROILists.Add(((FeaturepatMatchDialogDataBase)this).SpecialRoiList);
	}

	protected override void DoItemsDataChanged()
	{
		RebuildROILists();
		((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("ROILists");
	}

	protected virtual void AddROICommandExecute(object roi)
	{
		IsStateForDrawROI = false;
		if (roi is ICrossROI crossROI)
		{
			List<IROI> list = ((FeaturepatMatchDialogDataBase)this).SpecialRoiList.Where((IROI p) => p is ICrossROI).ToList();
			foreach (IROI item in list)
			{
				if (item != roi)
				{
					((FeaturepatMatchDialogDataBase)this).SpecialRoiList.Remove(item);
				}
			}
			((FeaturepatMatchDialogDataBase)this).IsSetCrossByManual = true;
			((FeaturepatMatchDialogDataBase)this).FixPointX = crossROI.CenterPoint.X;
			((FeaturepatMatchDialogDataBase)this).FixPointY = crossROI.CenterPoint.Y;
		}
		else
		{
			((FeaturepatMatchDialogDataBase)this).AddRoiDelegate(roi as IROI);
		}
	}

	protected virtual void RoiParamChange(IROI obj)
	{
		AutoSetFixPoint();
	}

	public void AutoSetFixPoint()
	{
		if (_MatchPointLock)
		{
			return;
		}
		if (((FeaturepatMatchDialogDataBase)this).ItemsData.Count > 0)
		{
			if (((FeaturepatMatchDialogDataBase)this).IsSetCrossByManual && _MatchPointLock)
			{
				return;
			}
			PointCollection pointCollection = new PointCollection();
			foreach (IROI itemsDatum in ((FeaturepatMatchDialogDataBase)this).ItemsData)
			{
				Point value = default;
				if (itemsDatum is HikCoverRoiParameter)
				{
					HikCoverRoiParameter hikCoverRoiParameter = itemsDatum as HikCoverRoiParameter;
					value.X = hikCoverRoiParameter.OriginPoint.X + hikCoverRoiParameter.Width / 2.0;
					value.Y = hikCoverRoiParameter.OriginPoint.Y + hikCoverRoiParameter.Height / 2.0;
				}
				if (itemsDatum is HikPolygonRoiParameter)
				{
					IPolygonROI polygonROI = itemsDatum as IPolygonROI;
					double num = polygonROI.PolygonPoints.Min((Point r) => r.X);
					double num2 = polygonROI.PolygonPoints.Min((Point r) => r.Y);
					double num3 = polygonROI.PolygonPoints.Max((Point r) => r.X);
					double num4 = polygonROI.PolygonPoints.Max((Point r) => r.Y);
					value.X = (num + num3) / 2.0;
					value.Y = (num2 + num4) / 2.0;
				}
				if (itemsDatum is HikCoverCircleRoiParameter)
				{
					HikCoverCircleRoiParameter hikCoverCircleRoiParameter = itemsDatum as HikCoverCircleRoiParameter;
					value.X = hikCoverCircleRoiParameter.CenterPoint.X;
					value.Y = hikCoverCircleRoiParameter.CenterPoint.Y;
				}
				FeatureMathSectorROIModel val;
				if ((val = (FeatureMathSectorROIModel)((itemsDatum is FeatureMathSectorROIModel) ? itemsDatum : null)) != null)
				{
					value = ((SectorROIModel)(object)val).CenterPoint;
				}
				pointCollection.Add(value);
			}
			((FeaturepatMatchDialogDataBase)this).FixPointX = pointCollection.Sum((Point r) => r.X) / (double)pointCollection.Count;
			((FeaturepatMatchDialogDataBase)this).FixPointY = pointCollection.Sum((Point r) => r.Y) / (double)pointCollection.Count;
		}
		else
		{
			((FeaturepatMatchDialogDataBase)this).IsSetCrossByManual = false;
			((FeaturepatMatchDialogDataBase)this).FixPointX = 0.0;
			((FeaturepatMatchDialogDataBase)this).FixPointY = 0.0;
		}
	}

	protected override void AddRoiDelegate(IROI roi)
	{
		roi.DataChangedCommand = (ICommand)new DelegateCommand<IROI>((Action<IROI>)RoiParamChange);
		(roi as RoiBaseParameter)?.NotifyPropertyChanged("DataChangedCommand");
		(roi as RoiParameter)?.NotifyPropertyChanged("DataChangedCommand");
	}
}
