using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using Apps.Data;
using Hik.Xml;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class ROISelecter_Com : ROISelecter
{
	protected int roiselectlabel;

	public bool IsShowIndex = false;

	protected ObservableCollection<RoiList_Com> roilist_comlist = new ObservableCollection<RoiList_Com>();

	public int RoiSelectLabel
	{
		get
		{
			return roiselectlabel;
		}
		set
		{
			if (roiselectlabel != value)
			{
				roiselectlabel = value;
				OnPropertyChanged("RoiSelectLabel");
			}
		}
	}

	public bool IsShowIndexA
	{
		get
		{
			return IsShowIndex;
		}
		set
		{
			if (IsShowIndex != value)
			{
				IsShowIndex = value;
				OnPropertyChanged("IsShowIndexA");
			}
		}
	}

	public ObservableCollection<RoiList_Com> RoiList_ComList
	{
		get
		{
			if (Enumerable.Count(roilist_comlist) == 0)
			{
				foreach (RoiBaseParameter rOI in ROIList)
				{
					ROIType roiType = GetRoiType(rOI.RoiType);
					RoiList_Com roiList_Com = new RoiList_Com();
					roiList_Com.RoiTypeCom = roiType;
					roiList_Com.NoticeDrawEvent += DrawROI;
					roiList_Com.NoticeSelectnumEvent += SelectRoi;
					roiList_Com.IsShowIndexA = IsShowIndexA;
					int.TryParse(rOI.Label, out roiList_Com.label);
					roilist_comlist.Add(roiList_Com);
				}
				RoiSelectLabel = Enumerable.Count(roilist_comlist);
			}
			return roilist_comlist;
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		InitMap();
		((IROIVisible)NormalizeROIList).IsVisible = CustomVisible;
		NoticeSelectEvent += NormalizeSelectRoiIcon;
		return this;
	}

	protected override void ClearOldROI(IROI newROI)
	{
	}

	protected override bool CanCopy(IROI roi)
	{
		return false;
	}

	public void SelectRoiListComNone()
	{
		foreach (RoiList_Com roiList_Com in RoiList_ComList)
		{
			roiList_Com.CircleCaliperComA = false;
			roiList_Com.LineCaliperComA = false;
		}
	}

	public void ClearComSelected()
	{
		foreach (RoiList_Com roiList_Com in RoiList_ComList)
		{
			roiList_Com.LineCaliper_ModuleEnableA = false;
			roiList_Com.CircleCaliper_ModuleEnableA = false;
		}
	}

	private void SetectComRoiIcon(int label)
	{
		if (Enumerable.Count(roilist_comlist) < label)
		{
			FullScreen_ModuleEnableA = true;
			return;
		}
		RoiList_Com roiList_Com = roilist_comlist[label - 1];
		RoiSelectLabel = roiList_Com.Label;
		switch (roiList_Com.RoiTypeCom)
		{
		case ROIType.LineCaliper:
			roiList_Com.LineCaliper_ModuleEnableA = true;
			break;
		case ROIType.CircleCaliper:
			roiList_Com.CircleCaliper_ModuleEnableA = true;
			break;
		}
	}

	private void SetectComRoiIconbyLabel(int num)
	{
		ClearComSelected();
		if (Enumerable.Count(roilist_comlist) < num)
		{
			FullScreen_ModuleEnableA = true;
			return;
		}
		foreach (RoiList_Com roiList_Com in RoiList_ComList)
		{
			if (roiList_Com.Label == num)
			{
				switch (roiList_Com.RoiTypeCom)
				{
				case ROIType.LineCaliper:
					roiList_Com.LineCaliper_ModuleEnableA = true;
					break;
				case ROIType.CircleCaliper:
					roiList_Com.CircleCaliper_ModuleEnableA = true;
					break;
				}
			}
		}
	}

	private void NormalizeSelectRoiIcon()
	{
		ClearComSelected();
		if (NormalizeROIList.Count < 1)
		{
			FullScreen_ModuleEnableA = true;
		}
		else
		{
			SetectComRoiIcon(RoiSelectLabel);
		}
	}

	protected new void DoRadioButton(ref RoiType roiType)
	{
		switch (roiType)
		{
		case RoiType.FullScreen:
			SelectNone();
			SelectRoiListComNone();
			FullScreenA = true;
			break;
		case RoiType.CircleCaliper:
			if (CircleCaliperA)
			{
				SelectNone();
				SelectRoiListComNone();
				CircleCaliperA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.LineCaliper:
			if (LineCaliperA)
			{
				SelectNone();
				SelectRoiListComNone();
				LineCaliperA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.DoubleBox:
			if (DoubleBoxA)
			{
				SelectNone();
				SelectRoiListComNone();
				DoubleBoxA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.QuadraBox:
			if (QuadraBoxA)
			{
				SelectNone();
				SelectRoiListComNone();
				QuadraBoxA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.Box:
		case RoiType.LineBox:
		case RoiType.SeqBox:
			if (BoxA)
			{
				SelectNone();
				SelectRoiListComNone();
				BoxA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.SectorRing:
			if (SectorRingA)
			{
				SelectNone();
				SelectRoiListComNone();
				SectorRingA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.Sector:
			if (SectorA)
			{
				SelectNone();
				SelectRoiListComNone();
				SectorA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.Polygon:
			if (PolygonA)
			{
				SelectNone();
				SelectRoiListComNone();
				PolygonA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.Point:
			if (PointA)
			{
				SelectNone();
				SelectRoiListComNone();
				PointA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.Line:
			if (LineA)
			{
				SelectNone();
				SelectRoiListComNone();
				LineA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.CircleMes:
			if (CircleMesA)
			{
				SelectNone();
				SelectRoiListComNone();
				CircleMesA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.SectorCaliper:
			if (SectorCaliperA)
			{
				SelectNone();
				SelectRoiListComNone();
				SectorCaliperA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.UroBox:
			if (UroBoxA)
			{
				SelectNone();
				SelectRoiListComNone();
				UroBoxA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.OverSector:
			if (OverSectorA)
			{
				SelectNone();
				SelectRoiListComNone();
				OverSectorA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.MatrixCircle:
			if (MatrixCircleA)
			{
				SelectNone();
				SelectRoiListComNone();
				MatrixCircleA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.SectorRing2:
			break;
		}
	}

	public void DeInit()
	{
		foreach (RoiList_Com roiList_Com in RoiList_ComList)
		{
			roiList_Com.NoticeDrawEvent -= DrawROI;
		}
	}

	protected override void AddROICommandExecute(object roi)
	{
		NormalizeRoiAddNumInit(roi as IROI, RoiSelectLabel.ToString());
		StopDrawRoiCom();
		SetectComRoiIconbyLabel(RoiSelectLabel);
	}

	private void StopDrawRoiCom()
	{
		SelectRoiListComNone();
		IsStateForDrawROI = false;
	}

	private void SelectRoi(int selnum)
	{
		RoiSelectLabel = selnum;
	}
}
