using System.Linq;
using VM.ModuleControl;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class GrayMatchDialogData : HPFeaturepatMatchDialogData
{
	public int numLevel = 5;

	public int NumLevel
	{
		get
		{
			return numLevel;
		}
		set
		{
			if (numLevel != value)
			{
				numLevel = value;
				((FeaturepatMatchDialogDataBase)this).NotifyPropertyChanged("NumLevel");
			}
		}
	}

	protected override void Init()
	{
		((FeaturepatMatchDialogDataBase)this).FeatureMatchName = "IMVSGrayMatchModu";
	}

	protected override void CopyParam(HPFeaturepatMatchDialogData data, HPFeaturepatMatchDialogData curData)
	{
		(data as GrayMatchDialogData).NumLevel = (data as GrayMatchDialogData).NumLevel;
	}

	protected override HPFeaturepatMatchDialogData GetTempData()
	{
		return (HPFeaturepatMatchDialogData)(object)new GrayMatchDialogData();
	}

	protected override int CreatePatE(dynamic dynamicObject)
	{
		return dynamicObject.CreateGrayPatE(this);
	}

	protected override void AddROICommandExecute(object roi)
	{
		((HPFeaturepatMatchDialogData)this).AddROICommandExecute(roi);
		if (roi is ICrossROI)
		{
			return;
		}
		foreach (IROI item in ((FeaturepatMatchDialogDataBase)this)._ItemsData.OfType<IROI>().ToList())
		{
			if (item != roi && !(item is ICrossROI))
			{
				((FeaturepatMatchDialogDataBase)this)._ItemsData.Remove(item);
			}
		}
		if (roi is IAngleEnable angleEnable)
		{
			angleEnable.IsAngleEnable = false;
		}
	}

	public override FeaturepatMatchDialogDataBase DeepCopyModelData(bool excludeBigData = false, bool isSaveImage = false)
	{
		FeaturepatMatchDialogDataBase val = ((HPFeaturepatMatchDialogData)this).DeepCopyModelData(excludeBigData, isSaveImage);
		if (val is GrayMatchDialogData grayMatchDialogData)
		{
			grayMatchDialogData.NumLevel = NumLevel;
			return (FeaturepatMatchDialogDataBase)(object)grayMatchDialogData;
		}
		return val;
	}
}
