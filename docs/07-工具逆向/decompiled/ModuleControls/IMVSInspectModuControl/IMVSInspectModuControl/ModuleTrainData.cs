using System;
using System.Globalization;
using System.Text;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Hik.Util.Model;
using Hik.Xml;
using HikPcUI.ImageView;
using VM.ModuleControl;
using VMControls.BaseInterface;

namespace IMVSInspectModuControl;

public class ModuleTrainData : BaseNode
{
	public FeaturepatMatchDialogData _featureMatchData = new FeaturepatMatchDialogData();

	public HikCoverRoiParameter _trainRoiParam = null;

	public HikCoverRoiParameter _matchBox = null;

	public bool _isSetTrainRegion = false;

	public bool _isBatchTrain = false;

	public byte[] _pointsData;

	public byte[] _inspModelData;

	public byte[] _matchModelData;

	public bool _isTrainFinish = false;

	public string _imagePath = null;

	public byte[] _matchBoxData;

	public bool _isExportToModel = false;

	public byte[] _imagesData;

	public int _modelType = 0;

	public SDKInspectData SDKImportData;

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

	public HikCoverRoiParameter MatchBox
	{
		get
		{
			return _matchBox;
		}
		set
		{
			if (_matchBox != value)
			{
				_matchBox = value;
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

	public bool IsBatchTrain
	{
		get
		{
			return _isBatchTrain;
		}
		set
		{
			if (_isBatchTrain != value)
			{
				_isBatchTrain = value;
			}
		}
	}

	public bool IsTrainFinish
	{
		get
		{
			return _isTrainFinish;
		}
		set
		{
			if (_isTrainFinish != value)
			{
				_isTrainFinish = value;
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

	public bool IsExportToModel
	{
		get
		{
			return _isExportToModel;
		}
		set
		{
			if (value != _isExportToModel)
			{
				_isExportToModel = value;
			}
		}
	}

	public int ModelType
	{
		get
		{
			return _modelType;
		}
		set
		{
			if (value != _modelType)
			{
				_modelType = value;
			}
		}
	}

	public void ClearModelData()
	{
		_inspModelData = null;
		_matchModelData = null;
		_pointsData = null;
		SDKImportData = null;
	}

	public void PrepareSDKImportData(IParamsConfig paramsConfig, bool isCreate)
	{
		SDKImportData = new SDKInspectData();
		SDKImportData.UserInterfaceData = new SDKInspectUIData();
		SDKImportData.ModuleModelData = new SDKInspectModelData();
		byte[] modelData = ((FeaturepatMatchDataBase)FeatureMatchData).ModelData;
		if (isCreate)
		{
			if (modelData != null)
			{
				SDKImportData.UserInterfaceData.IMVSInspectModu_ModelData_ByteArray = modelData;
			}
			_pointsData = SerializationUnit.SerializeObject(((FeaturepatMatchDataBase)FeatureMatchData).PointsData);
			SDKImportData.UserInterfaceData.IMVSInspectModu_PointsData_ByteArray = _pointsData;
			try
			{
				_matchModelData = ((IUserBytesData)paramsConfig)["GetMatchModelData"];
				SDKImportData.ModuleModelData.AddMatchModelData_ByteArray = _matchModelData;
			}
			catch (Exception ex)
			{
				int result = 0;
				string text = "GetMatchModelData error, ";
				text += AppLocalizationService.GetLang("errInfo:");
				string text2 = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				text += text2;
				LogHelper.moduleLog.Error(text);
			}
			SDKImportData.ModuleModelData.SetFixPoint_ByteArray = TrainDataMgr.Instance.GetPointBuffer((float)((FeaturepatMatchDataBase)FeatureMatchData).FixPointX, (float)((FeaturepatMatchDataBase)FeatureMatchData).FixPointY);
			if (((FeaturepatMatchDataBase)FeatureMatchData).CurImageData != null)
			{
				SDKImportData.ModuleModelData.SetROIData_ByteArray = ModuleCommonMethodMgr.Instance.GetRoiBuffer(TrainRoiParam, (float)((FeaturepatMatchDataBase)FeatureMatchData).CurImageData.Width, (float)((FeaturepatMatchDataBase)FeatureMatchData).CurImageData.Height);
			}
			ModelType = 0;
		}
		else
		{
			ModelType = 1;
		}
		((FeaturepatMatchDataBase)FeatureMatchData).ModelData = null;
		byte[] bytes = Encoding.UTF8.GetBytes(ToXmlStringWithHead(IsShort: true));
		if (bytes != null)
		{
			SDKImportData.UserInterfaceData.IMVSInspectModu_TrainData_ByteArray = bytes;
		}
		((FeaturepatMatchDataBase)FeatureMatchData).ModelData = modelData;
		_inspModelData = ((IUserBytesData)paramsConfig)["GetInspModelData"];
		SDKImportData.ModuleModelData.AddInspModelData_ByteArray = _inspModelData;
	}

	public ModuleTrainData()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
	}
}
