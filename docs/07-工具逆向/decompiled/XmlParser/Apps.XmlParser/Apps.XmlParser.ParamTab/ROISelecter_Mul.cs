using HikPcUI.ImageView;

namespace Apps.XmlParser.ParamTab;

public class ROISelecter_Mul : ROISelecter
{
	public new string ROISelection;

	public new string ROISelectionA
	{
		get
		{
			return ROISelection;
		}
		set
		{
			ROISelection = value;
			OnPropertyChanged("ROISelectionA");
		}
	}

	protected override void ValueChangedCmdExecute(object type)
	{
		if (!IsUnConnect())
		{
			if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot && algorithmTabRoot.FindBaseNodeByName("ExternRoiType") is ROISelecter_Extern rOISelecter_Extern)
			{
				rOISelecter_Extern.SelectNone();
			}
			if (type is RoiType roiType)
			{
				DoRadioButton(ref roiType);
			}
		}
	}

	protected override void OnGetValueCompleted(bool success)
	{
		InvokeGetValueCompleted(success);
		if (success)
		{
			BoxA = false;
			UroBoxA = false;
			SectorRingA = false;
			SectorRing2A = false;
			PolygonA = false;
			SelectIcon();
		}
	}

	protected override void OnSetValueCompleted(bool success)
	{
		InvokeSetValueCompleted(success);
		if (success)
		{
			BoxA = false;
			UroBoxA = false;
			SectorRingA = false;
			SectorRing2A = false;
			PolygonA = false;
			SelectIcon();
		}
	}

	private void SelectIcon()
	{
	}

	private RoiParameter GetExtendROIParam(object sender, RoiUIType ROIType)
	{
		return ROIType switch
		{
			RoiUIType.HikCoverRoi => (RoiParameter)new HikCoverRoiParameter
			{
				Tag = CustomROITypeUIEnum.Mul
			}, 
			RoiUIType.HikCoverCircleRoi => new HikCoverCircleRoiParameter
			{
				Tag = CustomROITypeUIEnum.Mul
			}, 
			RoiUIType.HikPolygonRoi => new HikPolygonRoiParameter
			{
				Tag = CustomROITypeUIEnum.Mul
			}, 
			_ => null, 
		};
	}

	public override void OnShownFinish()
	{
		base.OnShownFinish();
	}

	protected override void ROIValueChangedCmdExecute()
	{
	}

	public void HiddenSelectedRoi()
	{
		BoxEnable_ModuleEnableA = false;
		SectorRingEnable_ModuleEnableA = false;
		PolygonEnable_ModuleEnableA = false;
	}

	public override void SelectNone()
	{
		base.SelectNone();
	}
}
