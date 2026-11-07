using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class RoiStruct
{
	public interface IGetReqString
	{
		string Name { get; }

		string GetReqString();
	}

	public class Box : IGetReqString
	{
		public string _Name = "Box";

		public double fCenterX { get; set; }

		public double fCenterY { get; set; }

		public double fWidth { get; set; }

		public double fHeight { get; set; }

		public double fAngle { get; set; }

		public string Name => _Name;

		public string GetReqString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Name).Append(" ");
			stringBuilder.Append(GetParamsString());
			return stringBuilder.ToString();
		}

		public string GetParamsString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(fCenterX.ToString("F7")).Append(" ");
			stringBuilder.Append(fCenterY.ToString("F7")).Append(" ");
			stringBuilder.Append(fWidth.ToString("F7")).Append(" ");
			stringBuilder.Append(fHeight.ToString("F7")).Append(" ");
			stringBuilder.Append(fAngle.ToString("F7"));
			return stringBuilder.ToString();
		}

		public void Reset()
		{
			fCenterX = 0.0;
			fCenterY = 0.0;
			fWidth = 0.0;
			fHeight = 0.0;
			fAngle = 0.0;
		}
	}

	public class DoubleBox : IGetReqString
	{
		public string _Name = "DoubleBox";

		public Box Box1 = new Box();

		public Box Box2 = new Box();

		public string Name => _Name;

		public string GetReqString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Name).Append(" ");
			stringBuilder.Append(Box1.GetParamsString()).Append(" ");
			stringBuilder.Append(Box2.GetParamsString());
			return stringBuilder.ToString();
		}

		public void Reset()
		{
			Box1.Reset();
			Box2.Reset();
		}
	}

	public class QuadraBox : IGetReqString
	{
		public string _Name = "QuadraBox";

		public Box Box1 = new Box();

		public Box Box2 = new Box();

		public Box Box3 = new Box();

		public Box Box4 = new Box();

		public string Name => _Name;

		public string GetReqString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Name).Append(" ");
			stringBuilder.Append(Box1.GetParamsString()).Append(" ");
			stringBuilder.Append(Box2.GetParamsString()).Append(" ");
			stringBuilder.Append(Box3.GetParamsString()).Append(" ");
			stringBuilder.Append(Box4.GetParamsString());
			return stringBuilder.ToString();
		}

		public void Reset()
		{
			Box1.Reset();
			Box2.Reset();
			Box3.Reset();
			Box4.Reset();
		}
	}

	public class SectorRing2 : SectorRing
	{
		public SweepDirection fClockWise { get; set; }

		public SectorRing2()
		{
			_Name = "SectorRing2";
		}

		public override string GetReqString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Name).Append(" ");
			stringBuilder.Append(((int)fClockWise).ToString()).Append(" ");
			stringBuilder.Append(fCenterX.ToString("F7")).Append(" ");
			stringBuilder.Append(fCenterY.ToString("F7")).Append(" ");
			stringBuilder.Append(fMinRadius.ToString("F7")).Append(" ");
			stringBuilder.Append(fMaxRadius.ToString("F7")).Append(" ");
			stringBuilder.Append(fStartAngle.ToString("F7")).Append(" ");
			stringBuilder.Append(fEndAngle.ToString("F7")).Append(" ");
			return stringBuilder.ToString();
		}
	}

	public class SectorRing : IGetReqString
	{
		public string _Name = "SectorRing";

		public double fCenterX { get; set; }

		public double fCenterY { get; set; }

		public double fMinRadius { get; set; }

		public double fMaxRadius { get; set; }

		public double fStartAngle { get; set; }

		public double fEndAngle { get; set; }

		public string Name => _Name;

		public virtual string GetReqString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Name).Append(" ");
			stringBuilder.Append(fCenterX.ToString("F7")).Append(" ");
			stringBuilder.Append(fCenterY.ToString("F7")).Append(" ");
			stringBuilder.Append(fMinRadius.ToString("F7")).Append(" ");
			stringBuilder.Append(fMaxRadius.ToString("F7")).Append(" ");
			stringBuilder.Append(fStartAngle.ToString("F7")).Append(" ");
			stringBuilder.Append(fEndAngle.ToString("F7")).Append(" ");
			return stringBuilder.ToString();
		}

		public void Reset()
		{
			fCenterX = 0.0;
			fCenterY = 0.0;
			fMinRadius = 0.0;
			fMaxRadius = 0.0;
			fStartAngle = 0.0;
			fEndAngle = 0.0;
		}
	}

	public class Polygon : IGetReqString
	{
		public string _Name = "Polygon";

		private List<Point> points = new List<Point>();

		public List<Point> Points => points;

		public string Name => _Name;

		public string GetReqString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Name).Append(" ");
			stringBuilder.Append(Points.Count.ToString());
			stringBuilder.Append(" ");
			foreach (Point point in Points)
			{
				stringBuilder.Append(point.X.ToString("F5") + " " + point.Y.ToString("F5")).Append(" ");
			}
			return stringBuilder.ToString();
		}

		public void Reset()
		{
			points.Clear();
		}
	}

	public class PointGetReq : IGetReqString
	{
		public string _Name = "Point";

		public double fCenterX { get; set; }

		public double fCenterY { get; set; }

		public string Name => _Name;

		public string GetReqString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Name).Append(" ");
			stringBuilder.Append(GetParamsString());
			return stringBuilder.ToString();
		}

		public string GetParamsString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(fCenterX.ToString("F3")).Append(" ");
			stringBuilder.Append(fCenterY.ToString("F3")).Append(" ");
			return stringBuilder.ToString();
		}

		public void Reset()
		{
			fCenterX = 0.0;
			fCenterY = 0.0;
		}
	}

	public class LineGetReq : IGetReqString
	{
		public string _Name = "Line";

		public double fStartX { get; set; }

		public double fStartY { get; set; }

		public double fEndX { get; set; }

		public double fEndY { get; set; }

		public string Name => _Name;

		public virtual string GetReqString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Name).Append(" ");
			stringBuilder.Append(GetParamsString());
			return stringBuilder.ToString();
		}

		public string GetParamsString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(fStartX.ToString("F7")).Append(" ");
			stringBuilder.Append(fStartY.ToString("F7")).Append(" ");
			stringBuilder.Append(fEndX.ToString("F7")).Append(" ");
			stringBuilder.Append(fEndY.ToString("F7")).Append(" ");
			return stringBuilder.ToString();
		}

		public void Reset()
		{
			fStartX = 0.0;
			fStartY = 0.0;
			fEndX = 0.0;
			fEndY = 0.0;
		}
	}

	public class CircleMesGetReq : IGetReqString
	{
		public string _Name = "Circle";

		public double fCenterX { get; set; }

		public double fCenterY { get; set; }

		public double fRadius { get; set; }

		public string Name => _Name;

		public string GetReqString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Name).Append(" ");
			stringBuilder.Append(fCenterX.ToString("F7")).Append(" ");
			stringBuilder.Append(fCenterY.ToString("F7")).Append(" ");
			stringBuilder.Append(fRadius.ToString("F7")).Append(" ");
			return stringBuilder.ToString();
		}

		public void Reset()
		{
			fCenterX = 0.0;
			fCenterY = 0.0;
			fRadius = 0.0;
		}
	}

	public class LineCaliperGetReq : LineGetReq
	{
		public double CaliperWidth { get; set; }

		public double CaliperHeight { get; set; }

		public uint CaliperNum { get; set; }

		public double CaliperRotateAngle { get; set; }

		public LineCaliperGetReq()
		{
			_Name = "LineCaliper";
		}

		public override string GetReqString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Name).Append(" ");
			stringBuilder.Append(fStartX.ToString("F7")).Append(" ");
			stringBuilder.Append(fStartY.ToString("F7")).Append(" ");
			stringBuilder.Append(fEndX.ToString("F7")).Append(" ");
			stringBuilder.Append(fEndY.ToString("F7")).Append(" ");
			stringBuilder.Append(CaliperWidth.ToString("F7")).Append(" ");
			stringBuilder.Append(CaliperHeight.ToString("F7")).Append(" ");
			stringBuilder.Append(CaliperNum.ToString()).Append(" ");
			stringBuilder.Append(CaliperRotateAngle.ToString("F7")).Append(" ");
			return stringBuilder.ToString();
		}
	}

	public class CircleCaliperGetReq : SectorRing
	{
		public double CaliperWidth { get; set; }

		public double CaliperHeight { get; set; }

		public uint CaliperNum { get; set; }

		public CircleCaliperGetReq()
		{
			_Name = "CircleCaliper";
		}

		public override string GetReqString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Name).Append(" ");
			stringBuilder.Append(fCenterX.ToString("F7")).Append(" ");
			stringBuilder.Append(fCenterY.ToString("F7")).Append(" ");
			stringBuilder.Append(fMaxRadius.ToString("F7")).Append(" ");
			stringBuilder.Append(fStartAngle.ToString("F7")).Append(" ");
			stringBuilder.Append(fEndAngle.ToString("F7")).Append(" ");
			stringBuilder.Append(CaliperWidth.ToString("F7")).Append(" ");
			stringBuilder.Append(CaliperHeight.ToString("F7")).Append(" ");
			stringBuilder.Append(CaliperNum.ToString()).Append(" ");
			return stringBuilder.ToString();
		}
	}

	public RoiType SelectedType = RoiType.Default;

	public Box box = new Box();

	public DoubleBox doubleBox = new DoubleBox();

	public QuadraBox quadraBox = new QuadraBox();

	public SectorRing sectorRing = new SectorRing();

	public SectorRing2 sectorRing2 = new SectorRing2();

	public Polygon polygon = new Polygon();

	public PointGetReq point = new PointGetReq();

	public LineGetReq line = new LineGetReq();

	public CircleMesGetReq circle = new CircleMesGetReq();

	public LineCaliperGetReq lineCaliper = new LineCaliperGetReq();

	public CircleCaliperGetReq circleCaliper = new CircleCaliperGetReq();

	public List<object> RoiList = new List<object>();

	public int SelectedIndex { get; set; }

	public void Init()
	{
		RoiList.Clear();
	}

	public void Init(RoiType roiType, string strInfo)
	{
		RoiList.Clear();
		SetGemInfo(roiType, strInfo);
	}

	public void SetGemInfo(RoiType roitype, string strInfo)
	{
		string strMsg = "";
		try
		{
			string[] array = strInfo.Split(' ');
			switch (roitype)
			{
			case RoiType.Point:
				if (2 == array.Length)
				{
					point.fCenterX = Convert.ToSingle(array[0].ToString());
					point.fCenterY = Convert.ToSingle(array[1].ToString());
				}
				break;
			case RoiType.Line:
				if (4 == array.Length)
				{
					line.fStartX = Convert.ToSingle(array[0].ToString());
					line.fStartY = Convert.ToSingle(array[1].ToString());
					line.fEndX = Convert.ToSingle(array[2].ToString());
					line.fEndY = Convert.ToSingle(array[3].ToString());
				}
				break;
			case RoiType.CircleMes:
				if (3 == array.Length)
				{
					circle.fCenterX = Convert.ToSingle(array[0].ToString());
					circle.fCenterY = Convert.ToSingle(array[1].ToString());
					circle.fRadius = Convert.ToSingle(array[2].ToString());
				}
				break;
			}
		}
		catch
		{
			HikMessageBox.Show(Application.Current.MainWindow, "Lang_GeoCreate", strMsg, MessageIcons.Error);
		}
	}

	public void SetSelectedType(int index)
	{
		if (index > 0 && index < RoiList.Count)
		{
			RoiType result = RoiType.Default;
			if (Enum.TryParse<RoiType>((RoiList[index] as IGetReqString).Name, out result))
			{
				SelectedType = result;
				SelectedIndex = index;
			}
		}
	}

	public void SetSelectedType(RoiType roiType)
	{
		if (roiType == RoiType.FullScreen)
		{
			SelectedIndex = -1;
			SelectedType = roiType;
			return;
		}
		foreach (IGetReqString roi in RoiList)
		{
			if (roi.Name == roiType.ToString())
			{
				SelectedIndex = RoiList.IndexOf(roi);
				SelectedType = roiType;
				break;
			}
		}
	}

	public void SetRoiInfo(string info)
	{
		if (info == null)
		{
			return;
		}
		string[] array = info.Split(' ');
		int result = 0;
		SelectedType = RoiType.Default;
		if (!int.TryParse(array[0], out result))
		{
			return;
		}
		try
		{
			for (int i = 1; i < array.Length; i++)
			{
				RoiType result2 = RoiType.Default;
				if (!Enum.TryParse<RoiType>(array[i], out result2))
				{
					continue;
				}
				int num = i;
				switch (result2)
				{
				case RoiType.Box:
					box.fCenterX = Convert.ToSingle(array[++num]);
					box.fCenterY = Convert.ToSingle(array[++num]);
					box.fWidth = Convert.ToSingle(array[++num]);
					box.fHeight = Convert.ToSingle(array[++num]);
					box.fAngle = Convert.ToSingle(array[++num]);
					if (!RoiList.Contains(box))
					{
						RoiList.Add(box);
					}
					if (RoiList.IndexOf(box) == result)
					{
						SelectedType = result2;
					}
					break;
				case RoiType.DoubleBox:
					doubleBox.Box1.fCenterX = Convert.ToSingle(array[++num]);
					doubleBox.Box1.fCenterY = Convert.ToSingle(array[++num]);
					doubleBox.Box1.fWidth = Convert.ToSingle(array[++num]);
					doubleBox.Box1.fHeight = Convert.ToSingle(array[++num]);
					doubleBox.Box1.fAngle = Convert.ToSingle(array[++num]);
					doubleBox.Box2.fCenterX = Convert.ToSingle(array[++num]);
					doubleBox.Box2.fCenterY = Convert.ToSingle(array[++num]);
					doubleBox.Box2.fWidth = Convert.ToSingle(array[++num]);
					doubleBox.Box2.fHeight = Convert.ToSingle(array[++num]);
					doubleBox.Box2.fAngle = Convert.ToSingle(array[++num]);
					if (!RoiList.Contains(doubleBox))
					{
						RoiList.Add(doubleBox);
					}
					if (RoiList.IndexOf(doubleBox) == result)
					{
						SelectedType = result2;
					}
					break;
				case RoiType.QuadraBox:
					quadraBox.Box1.fCenterX = Convert.ToSingle(array[++num]);
					quadraBox.Box1.fCenterY = Convert.ToSingle(array[++num]);
					quadraBox.Box1.fWidth = Convert.ToSingle(array[++num]);
					quadraBox.Box1.fHeight = Convert.ToSingle(array[++num]);
					quadraBox.Box1.fAngle = Convert.ToSingle(array[++num]);
					quadraBox.Box2.fCenterX = Convert.ToSingle(array[++num]);
					quadraBox.Box2.fCenterY = Convert.ToSingle(array[++num]);
					quadraBox.Box2.fWidth = Convert.ToSingle(array[++num]);
					quadraBox.Box2.fHeight = Convert.ToSingle(array[++num]);
					quadraBox.Box2.fAngle = Convert.ToSingle(array[++num]);
					quadraBox.Box3.fCenterX = Convert.ToSingle(array[++num]);
					quadraBox.Box3.fCenterY = Convert.ToSingle(array[++num]);
					quadraBox.Box3.fWidth = Convert.ToSingle(array[++num]);
					quadraBox.Box3.fHeight = Convert.ToSingle(array[++num]);
					quadraBox.Box3.fAngle = Convert.ToSingle(array[++num]);
					quadraBox.Box4.fCenterX = Convert.ToSingle(array[++num]);
					quadraBox.Box4.fCenterY = Convert.ToSingle(array[++num]);
					quadraBox.Box4.fWidth = Convert.ToSingle(array[++num]);
					quadraBox.Box4.fHeight = Convert.ToSingle(array[++num]);
					quadraBox.Box4.fAngle = Convert.ToSingle(array[++num]);
					if (!RoiList.Contains(quadraBox))
					{
						RoiList.Add(quadraBox);
					}
					if (RoiList.IndexOf(quadraBox) == result)
					{
						SelectedType = result2;
					}
					break;
				case RoiType.SectorRing:
					sectorRing.fCenterX = Convert.ToSingle(array[++num]);
					sectorRing.fCenterY = Convert.ToSingle(array[++num]);
					sectorRing.fMinRadius = Convert.ToSingle(array[++num]);
					sectorRing.fMaxRadius = Convert.ToSingle(array[++num]);
					sectorRing.fStartAngle = Convert.ToSingle(array[++num]);
					sectorRing.fEndAngle = Convert.ToSingle(array[++num]);
					if (!RoiList.Contains(sectorRing))
					{
						RoiList.Add(sectorRing);
					}
					if (RoiList.IndexOf(sectorRing) == result)
					{
						SelectedType = result2;
					}
					break;
				case RoiType.SectorRing2:
					sectorRing2.fClockWise = (SweepDirection)Convert.ToInt32(array[++num]);
					sectorRing2.fCenterX = Convert.ToSingle(array[++num]);
					sectorRing2.fCenterY = Convert.ToSingle(array[++num]);
					sectorRing2.fMinRadius = Convert.ToSingle(array[++num]);
					sectorRing2.fMaxRadius = Convert.ToSingle(array[++num]);
					sectorRing2.fStartAngle = Convert.ToSingle(array[++num]);
					sectorRing2.fEndAngle = Convert.ToSingle(array[++num]);
					if (!RoiList.Contains(sectorRing2))
					{
						RoiList.Add(sectorRing2);
					}
					if (RoiList.IndexOf(sectorRing2) == result)
					{
						SelectedType = result2;
					}
					break;
				case RoiType.Polygon:
					polygon.Points.Clear();
					num++;
					while (num + 2 < array.Length && MathHelper.IsNumber(array[num + 1]) && MathHelper.IsNumber(array[num + 2]))
					{
						double x = Convert.ToSingle(array[++num]);
						double y = Convert.ToSingle(array[++num]);
						polygon.Points.Add(new Point(x, y));
					}
					if (!RoiList.Contains(polygon))
					{
						RoiList.Add(polygon);
					}
					if (RoiList.IndexOf(polygon) == result)
					{
						SelectedType = result2;
					}
					break;
				case RoiType.LineCaliper:
					lineCaliper.fStartX = Convert.ToSingle(array[++num]);
					lineCaliper.fStartY = Convert.ToSingle(array[++num]);
					lineCaliper.fEndX = Convert.ToSingle(array[++num]);
					lineCaliper.fEndY = Convert.ToSingle(array[++num]);
					lineCaliper.CaliperWidth = Convert.ToSingle(array[++num]);
					lineCaliper.CaliperHeight = Convert.ToSingle(array[++num]);
					lineCaliper.CaliperNum = Convert.ToUInt16(array[++num]);
					lineCaliper.CaliperRotateAngle = Convert.ToSingle(array[++num]);
					if (!RoiList.Contains(lineCaliper))
					{
						RoiList.Add(lineCaliper);
					}
					if (RoiList.IndexOf(lineCaliper) == result)
					{
						SelectedType = result2;
					}
					break;
				case RoiType.CircleCaliper:
					circleCaliper.fCenterX = Convert.ToSingle(array[++num]);
					circleCaliper.fCenterY = Convert.ToSingle(array[++num]);
					circleCaliper.fMaxRadius = Convert.ToSingle(array[++num]);
					circleCaliper.fStartAngle = Convert.ToSingle(array[++num]);
					circleCaliper.fEndAngle = Convert.ToSingle(array[++num]);
					circleCaliper.CaliperWidth = Convert.ToSingle(array[++num]);
					circleCaliper.CaliperHeight = Convert.ToSingle(array[++num]);
					circleCaliper.CaliperNum = Convert.ToUInt16(array[++num]);
					if (!RoiList.Contains(circleCaliper))
					{
						RoiList.Add(circleCaliper);
					}
					if (RoiList.IndexOf(circleCaliper) == result)
					{
						SelectedType = result2;
					}
					break;
				}
				i = num;
			}
		}
		catch
		{
			HikMessageBox.Show(Application.Current.MainWindow, "Lang_ROISet", "ROI Err：" + info, MessageIcons.Error);
		}
		SelectedIndex = result;
		if (SelectedIndex == -1)
		{
			SelectedType = RoiType.FullScreen;
		}
	}

	public void Init(string msg)
	{
		RoiList.Clear();
		SetRoiInfo(msg);
	}

	public string GetReqString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (RoiList.Count > 0)
		{
			stringBuilder.Append(SelectedIndex.ToString()).Append(" ");
			foreach (IGetReqString roi in RoiList)
			{
				stringBuilder.Append(roi.GetReqString()).Append(" ");
			}
			return stringBuilder.ToString().Substring(0, stringBuilder.Length - 1);
		}
		return "";
	}
}
