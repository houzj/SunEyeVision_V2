using System;
using VM.ModuleControl;

namespace Apps.XmlParser.ParamTab;

public class FastFeaturepatMatchDialogData : HPFeaturepatMatchDialogData
{
	public override float RoughPyramidscale
	{
		get
		{
			return (float)Math.Round(base._RoughPyramidScale, 1);
		}
		set
		{
			float num = (float)Math.Round(value, 1);
			if ((float)Math.Round(base._RoughPyramidScale, 1) != num && num >= 1f && num <= 20f && !((HPFeaturepatMatchDialogData)this).IsMatching)
			{
				base._RoughPyramidScale = num;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("RoughPyramidscale");
				if (base._PyramidScaleMode == 0)
				{
					((HPFeaturepatMatchDialogData)this).OnMatchCommand();
				}
			}
		}
	}

	protected override void Init()
	{
		((FeaturepatMatchDialogDataBase)this).FeatureMatchName = "IMVSFastFeatureMatchModu";
	}

	protected override void CopyParam(HPFeaturepatMatchDialogData data, HPFeaturepatMatchDialogData curData)
	{
	}

	protected override HPFeaturepatMatchDialogData GetTempData()
	{
		return (HPFeaturepatMatchDialogData)(object)new FastFeaturepatMatchDialogData();
	}

	protected override int CreatePatE(dynamic dynamicObject)
	{
		return dynamicObject.CreateFastPatE(this);
	}
}
