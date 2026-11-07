using System.Collections.Generic;
using Apps.Data;
using Hik.Xml;
using HikPcUI.ImageView;
using VM.ModuleControl;

namespace IMVSMarkInspModuVAControl;

public class ModuleTrainData : BaseNode
{
	public bool _isDetectTarget = false;

	public bool _isFinePositioning = false;

	public bool _isAddMask = false;

	public bool _isStatisticsTrain = false;

	public HikCoverRoiParameter _createRoiParam = null;

	public ImageData _inheritImageData;

	public HikCoverRoiParameter _inheritRoiParam = null;

	public string _curSelectModelName = "";

	public bool _isCreateModel = false;

	public bool _isInheritModel = false;

	public FeaturepatMatchDialogData _featureMatchData = new FeaturepatMatchDialogData();

	public byte[] DetectRoiData = null;

	public byte[] CharBoxesData = null;

	public byte[] CharRoiData = null;

	public byte[] PositioningMaskData = null;

	public byte[] AddMaskData = null;

	public int _curSelectIndex = 0;

	public List<bool> _trainFlagList = new List<bool>();

	public double _curHorizontalOffset = 0.0;

	public int _modelImageCount = 0;

	public string _imagePath = null;

	public List<string> _trainImageNameList = new List<string>();

	public FeaturepatMatchDialogData FeatureMatchData
	{
		get
		{
			return _featureMatchData;
		}
		set
		{
			_featureMatchData = value;
		}
	}

	public HikCoverRoiParameter CreateRoiParam
	{
		get
		{
			return _createRoiParam;
		}
		set
		{
			if (_createRoiParam != value)
			{
				_createRoiParam = value;
			}
		}
	}

	public ImageData InheritImageData
	{
		get
		{
			return _inheritImageData;
		}
		set
		{
			if (_inheritImageData != value)
			{
				_inheritImageData = value;
			}
		}
	}

	public HikCoverRoiParameter InheritRoiParam
	{
		get
		{
			return _inheritRoiParam;
		}
		set
		{
			if (_inheritRoiParam != value)
			{
				_inheritRoiParam = value;
			}
		}
	}

	public string CurSelectModelName
	{
		get
		{
			return _curSelectModelName;
		}
		set
		{
			if (_curSelectModelName != value)
			{
				_curSelectModelName = value;
			}
		}
	}

	public bool IsCreateModel
	{
		get
		{
			return _isCreateModel;
		}
		set
		{
			if (_isCreateModel != value)
			{
				_isCreateModel = value;
			}
		}
	}

	public bool IsInheritModel
	{
		get
		{
			return _isInheritModel;
		}
		set
		{
			if (_isInheritModel != value)
			{
				_isInheritModel = value;
			}
		}
	}

	public int CurSelectIndex
	{
		get
		{
			return _curSelectIndex;
		}
		set
		{
			if (_curSelectIndex != value)
			{
				_curSelectIndex = value;
			}
		}
	}

	public double CurHorizontalOffset
	{
		get
		{
			return _curHorizontalOffset;
		}
		set
		{
			if (_curHorizontalOffset != value)
			{
				_curHorizontalOffset = value;
			}
		}
	}

	public bool IsDetectTarget
	{
		get
		{
			return _isDetectTarget;
		}
		set
		{
			if (_isDetectTarget != value)
			{
				_isDetectTarget = value;
			}
		}
	}

	public bool IsFinePositioning
	{
		get
		{
			return _isFinePositioning;
		}
		set
		{
			if (_isFinePositioning != value)
			{
				_isFinePositioning = value;
			}
		}
	}

	public bool IsAddMask
	{
		get
		{
			return _isAddMask;
		}
		set
		{
			if (_isAddMask != value)
			{
				_isAddMask = value;
			}
		}
	}

	public bool IsStatisticsTrain
	{
		get
		{
			return _isStatisticsTrain;
		}
		set
		{
			if (_isStatisticsTrain != value)
			{
				_isStatisticsTrain = value;
			}
		}
	}

	public List<bool> TrainFlagList
	{
		get
		{
			return _trainFlagList;
		}
		set
		{
			if (_trainFlagList != value)
			{
				_trainFlagList = value;
			}
		}
	}

	public List<string> TrainImageNameList => _trainImageNameList;

	public int ModelImageCount
	{
		get
		{
			return _modelImageCount;
		}
		set
		{
			if (_modelImageCount != value)
			{
				_modelImageCount = value;
			}
		}
	}

	public string ImagePath
	{
		get
		{
			return _imagePath;
		}
		set
		{
			if (_imagePath != value)
			{
				_imagePath = value;
			}
		}
	}

	public ModuleTrainData()
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected Obj, but got Unknown
	}
}
