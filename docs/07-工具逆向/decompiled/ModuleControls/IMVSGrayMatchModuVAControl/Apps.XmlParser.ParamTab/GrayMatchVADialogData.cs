using VM.ModuleControl;

namespace Apps.XmlParser.ParamTab;

public class GrayMatchVADialogData : HPFeaturepatMatchDialogData
{
	public int pyramidNumLevelFlag = 1;

	public int pyramidNumLevel = 5;

	public int modelAngleStep = 2;

	public int modelAngleStart = -45;

	public int modelAngleEnd = 45;

	public int PyramidNumLevelFlag
	{
		get
		{
			return pyramidNumLevelFlag;
		}
		set
		{
			if (pyramidNumLevelFlag != value && !((HPFeaturepatMatchDialogData)this).IsMatching)
			{
				pyramidNumLevelFlag = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("PyramidNumLevelFlag");
				((HPFeaturepatMatchDialogData)this).OnMatchCommand();
			}
		}
	}

	public int PyramidNumLevel
	{
		get
		{
			return pyramidNumLevel;
		}
		set
		{
			if (pyramidNumLevel != value && !((HPFeaturepatMatchDialogData)this).IsMatching)
			{
				pyramidNumLevel = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("PyramidNumLevel");
				((HPFeaturepatMatchDialogData)this).OnMatchCommand();
			}
		}
	}

	public int ModelAngleStep
	{
		get
		{
			return modelAngleStep;
		}
		set
		{
			if (modelAngleStep != value && !((HPFeaturepatMatchDialogData)this).IsMatching)
			{
				modelAngleStep = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("ModelAngleStep");
				((HPFeaturepatMatchDialogData)this).OnMatchCommand();
			}
		}
	}

	public int ModelAngleStart
	{
		get
		{
			return modelAngleStart;
		}
		set
		{
			if (modelAngleStart != value && !((HPFeaturepatMatchDialogData)this).IsMatching)
			{
				modelAngleStart = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("ModelAngleStart");
				((HPFeaturepatMatchDialogData)this).OnMatchCommand();
			}
		}
	}

	public int ModelAngleEnd
	{
		get
		{
			return modelAngleEnd;
		}
		set
		{
			if (modelAngleEnd != value && !((HPFeaturepatMatchDialogData)this).IsMatching)
			{
				modelAngleEnd = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("ModelAngleEnd");
				((HPFeaturepatMatchDialogData)this).OnMatchCommand();
			}
		}
	}

	protected override void Init()
	{
		((FeaturepatMatchDialogDataBase)this).FeatureMatchName = "IMVSGrayMatchModu";
	}

	protected override HPFeaturepatMatchDialogData GetTempData()
	{
		return (HPFeaturepatMatchDialogData)(object)new GrayMatchVADialogData();
	}

	protected override void CopyParam(HPFeaturepatMatchDialogData data, HPFeaturepatMatchDialogData curData)
	{
		(data as GrayMatchVADialogData).PyramidNumLevelFlag = (data as GrayMatchVADialogData).PyramidNumLevelFlag;
		(data as GrayMatchVADialogData).PyramidNumLevel = (data as GrayMatchVADialogData).PyramidNumLevel;
		(data as GrayMatchVADialogData).ModelAngleStep = (data as GrayMatchVADialogData).ModelAngleStep;
		(data as GrayMatchVADialogData).ModelAngleStart = (data as GrayMatchVADialogData).ModelAngleStart;
		(data as GrayMatchVADialogData).ModelAngleEnd = (data as GrayMatchVADialogData).ModelAngleEnd;
	}

	protected override int CreatePatE(dynamic dynamicObject)
	{
		return dynamicObject.CreateGrayPatEVA(this);
	}

	public override FeaturepatMatchDialogDataBase DeepCopyModelData(bool excludeBigData = false, bool isSaveImage = false)
	{
		FeaturepatMatchDialogDataBase val = ((HPFeaturepatMatchDialogData)this).DeepCopyModelData(excludeBigData, isSaveImage);
		if (val is GrayMatchVADialogData grayMatchVADialogData)
		{
			grayMatchVADialogData.PyramidNumLevelFlag = PyramidNumLevelFlag;
			grayMatchVADialogData.PyramidNumLevel = PyramidNumLevel;
			grayMatchVADialogData.ModelAngleStart = ModelAngleStart;
			grayMatchVADialogData.ModelAngleEnd = ModelAngleEnd;
			grayMatchVADialogData.ModelAngleStep = ModelAngleStep;
			return (FeaturepatMatchDialogDataBase)(object)grayMatchVADialogData;
		}
		return val;
	}
}
