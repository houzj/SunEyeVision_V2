using System.Collections.Generic;
using Hik.Xml;
using HikPcUI.ImageView;
using VM.ModuleControl;

namespace IMVSMarkInspModuControl;

public class ModuleTrainData : BaseNode
{
	public string _moduleId;

	public HikCoverRoiParameter _trainRoiParam = null;

	public HikCoverRoiParameter _roughRoiParam = null;

	public int _curSelectIndex = 0;

	public List<bool> _trainFlagList = new List<bool>();

	public double _curHorizontalOffset = 0.0;

	public FeaturepatMatchDialogData _featureMatchData = new FeaturepatMatchDialogData();

	public bool _isSetTrainRegion = false;

	public bool _isSetTrainTarget = false;

	public bool _isStatisticsTrain = false;

	public int _modelImageCount = 0;

	public string _imagePath = null;

	public string ModuleId
	{
		get
		{
			return _moduleId;
		}
		set
		{
			if (_moduleId != value)
			{
				_moduleId = value;
			}
		}
	}

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

	public HikCoverRoiParameter TrainRoiParam
	{
		get
		{
			return _trainRoiParam;
		}
		set
		{
			if (_trainRoiParam != value)
			{
				_trainRoiParam = value;
			}
		}
	}

	public HikCoverRoiParameter RoughRoiParam
	{
		get
		{
			return _roughRoiParam;
		}
		set
		{
			if (_roughRoiParam != value)
			{
				_roughRoiParam = value;
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

	public bool IsSetTrainRegion
	{
		get
		{
			return _isSetTrainRegion;
		}
		set
		{
			if (_isSetTrainRegion != value)
			{
				_isSetTrainRegion = value;
			}
		}
	}

	public bool IsSetTrainTarget
	{
		get
		{
			return _isSetTrainTarget;
		}
		set
		{
			if (_isSetTrainTarget != value)
			{
				_isSetTrainTarget = value;
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
			_trainFlagList = value;
		}
	}

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
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Expected Obj, but got Unknown
	}
}
