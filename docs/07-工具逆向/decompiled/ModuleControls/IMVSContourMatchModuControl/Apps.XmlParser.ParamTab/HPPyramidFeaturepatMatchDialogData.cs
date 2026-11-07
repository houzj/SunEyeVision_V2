using VM.ModuleControl;

namespace Apps.XmlParser.ParamTab;

public class HPPyramidFeaturepatMatchDialogData : HPFeaturepatMatchDialogData
{
	public int _IncludeImageFlag = 0;

	public int _pyramidScaleFlag = 1;

	public int _pyramidScaleLevel = 5;

	public int _pyramidScaleRLevel = 1;

	public float _edgeThresholdFlag = 1f;

	public int _edgeThreshold = 15;

	private int _highThreshold;

	private int _lowThreshold;

	public int _maskModelFlag;

	public int IncludeImageFlag
	{
		get
		{
			return _IncludeImageFlag;
		}
		set
		{
			((FeaturepatMatchDialogDataBase)this).SetProperty<int>(ref _pyramidScaleFlag, value, "IncludeImageFlag");
		}
	}

	public int PyramidScaleFlag
	{
		get
		{
			return _pyramidScaleFlag;
		}
		set
		{
			((FeaturepatMatchDialogDataBase)this).SetProperty<int>(ref _pyramidScaleFlag, value, "PyramidScaleFlag");
		}
	}

	public int PyramidScaleLevel
	{
		get
		{
			return _pyramidScaleLevel;
		}
		set
		{
			if (_pyramidScaleLevel != value)
			{
				_pyramidScaleLevel = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("PyramidScaleLevel");
				if (_pyramidScaleFlag == 0 && !((HPFeaturepatMatchDialogData)this).IsMatching)
				{
					((HPFeaturepatMatchDialogData)this).OnMatchCommand();
				}
			}
		}
	}

	public int PyramidScaleRLevel
	{
		get
		{
			return _pyramidScaleRLevel;
		}
		set
		{
			if (_pyramidScaleRLevel != value)
			{
				_pyramidScaleRLevel = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("PyramidScaleRLevel");
				if (_pyramidScaleFlag == 0 && !((HPFeaturepatMatchDialogData)this).IsMatching)
				{
					((HPFeaturepatMatchDialogData)this).OnMatchCommand();
				}
			}
		}
	}

	public float EdgeThresholdFlag
	{
		get
		{
			return _edgeThresholdFlag;
		}
		set
		{
			((FeaturepatMatchDialogDataBase)this).SetProperty<float>(ref _edgeThresholdFlag, value, "EdgeThresholdFlag");
		}
	}

	public int EdgeThreshold
	{
		get
		{
			return _edgeThreshold;
		}
		set
		{
			if (_edgeThreshold != value)
			{
				_edgeThreshold = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("EdgeThreshold");
				if (_edgeThresholdFlag == 0f && !((HPFeaturepatMatchDialogData)this).IsMatching)
				{
					((HPFeaturepatMatchDialogData)this).OnMatchCommand();
				}
			}
		}
	}

	public int HighThreshold
	{
		get
		{
			return _highThreshold;
		}
		set
		{
			((FeaturepatMatchDialogDataBase)this).SetProperty<int>(ref _highThreshold, value, "HighThreshold");
		}
	}

	public int LowThreshold
	{
		get
		{
			return _lowThreshold;
		}
		set
		{
			((FeaturepatMatchDialogDataBase)this).SetProperty<int>(ref _lowThreshold, value, "LowThreshold");
		}
	}

	public int MaskModelFlag
	{
		get
		{
			return _maskModelFlag;
		}
		set
		{
			if (_maskModelFlag != value)
			{
				_maskModelFlag = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("MaskModelFlag");
				if (_maskModelFlag == 0 && !((HPFeaturepatMatchDialogData)this).IsMatching)
				{
					((HPFeaturepatMatchDialogData)this).OnMatchCommand();
				}
			}
		}
	}

	protected override void Init()
	{
		((FeaturepatMatchDialogDataBase)this).FeatureMatchName = "IMVSContourMatchModu";
	}

	protected override void CopyParam(HPFeaturepatMatchDialogData data, HPFeaturepatMatchDialogData curData)
	{
		if (data is HPPyramidFeaturepatMatchDialogData hPPyramidFeaturepatMatchDialogData && curData is HPPyramidFeaturepatMatchDialogData hPPyramidFeaturepatMatchDialogData2)
		{
			hPPyramidFeaturepatMatchDialogData.IncludeImageFlag = hPPyramidFeaturepatMatchDialogData2.IncludeImageFlag;
			hPPyramidFeaturepatMatchDialogData.PyramidScaleFlag = hPPyramidFeaturepatMatchDialogData2.PyramidScaleFlag;
			hPPyramidFeaturepatMatchDialogData.PyramidScaleLevel = hPPyramidFeaturepatMatchDialogData2.PyramidScaleLevel;
			hPPyramidFeaturepatMatchDialogData.PyramidScaleRLevel = hPPyramidFeaturepatMatchDialogData2.PyramidScaleRLevel;
			hPPyramidFeaturepatMatchDialogData.EdgeThresholdFlag = hPPyramidFeaturepatMatchDialogData2.EdgeThresholdFlag;
			hPPyramidFeaturepatMatchDialogData.EdgeThreshold = hPPyramidFeaturepatMatchDialogData2.EdgeThreshold;
			((HPFeaturepatMatchDialogData)hPPyramidFeaturepatMatchDialogData).ChainFlag = ((HPFeaturepatMatchDialogData)hPPyramidFeaturepatMatchDialogData2).ChainFlag;
			((HPFeaturepatMatchDialogData)hPPyramidFeaturepatMatchDialogData).MinChainLen = ((HPFeaturepatMatchDialogData)hPPyramidFeaturepatMatchDialogData2).MinChainLen;
			hPPyramidFeaturepatMatchDialogData.HighThreshold = hPPyramidFeaturepatMatchDialogData2.HighThreshold;
			hPPyramidFeaturepatMatchDialogData.LowThreshold = hPPyramidFeaturepatMatchDialogData2.LowThreshold;
			hPPyramidFeaturepatMatchDialogData.MaskModelFlag = hPPyramidFeaturepatMatchDialogData2.MaskModelFlag;
		}
	}

	protected override HPFeaturepatMatchDialogData GetTempData()
	{
		return (HPFeaturepatMatchDialogData)(object)new HPPyramidFeaturepatMatchDialogData();
	}

	protected override int CreatePatE(dynamic dynamicObject)
	{
		return dynamicObject.CreateDipolePyramid(this);
	}
}
