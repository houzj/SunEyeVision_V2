using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HikPcUI.ImageView;
using Mvvm.Commands;

namespace IMVSColorRecognitionModuControl;

public class ExternCoverRoiParameter : HikCoverRoiParameter, IFlags, IPointsRoi, IIndex
{
	public string ImagePath = "";

	public string flags = "";

	private int _index;

	private DelegateCommand _DeleteCommand = null;

	private DelegateCommand<string> _SelectedCommand = null;

	public bool IsReLoad { get; set; }

	public virtual string Flags
	{
		get
		{
			return flags;
		}
		set
		{
			if (flags != value)
			{
				flags = value;
				NotifyPropertyChanged("Flags");
			}
		}
	}

	public int Index
	{
		get
		{
			return _index;
		}
		set
		{
			if (_index != value)
			{
				_index = value;
				NotifyPropertyChanged("Index");
			}
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_DeleteCommand == null)
			{
				_DeleteCommand = new DelegateCommand((Action)DeleteCommandCmdExecute);
			}
			return (ICommand)_DeleteCommand;
		}
	}

	public ICommand SelectedCommand
	{
		get
		{
			if (_SelectedCommand == null)
			{
				_SelectedCommand = new DelegateCommand<string>((Action<string>)SelectedCommandCmdExecute);
			}
			return (ICommand)_SelectedCommand;
		}
	}

	public List<Point> GetPoints()
	{
		List<Point> list = new List<Point>();
		Point point = new Point(OriginPoint.X + Width / 2.0, OriginPoint.Y + Height / 2.0);
		RotateTransform rotateTransform = new RotateTransform
		{
			Angle = Angle,
			CenterX = point.X,
			CenterY = point.Y
		};
		Point point2 = rotateTransform.Transform(OriginPoint);
		Point point3 = rotateTransform.Transform(new Point(OriginPoint.X + Width, OriginPoint.Y));
		Point point4 = rotateTransform.Transform(new Point(OriginPoint.X + Width, OriginPoint.Y + Height));
		Point point5 = rotateTransform.Transform(new Point(OriginPoint.X, OriginPoint.Y + Height));
		int num = (int)Math.Round(point2.X);
		int num2 = (int)Math.Round(point3.X);
		int num3 = (int)Math.Round(point4.X);
		int num4 = (int)Math.Round(point5.X);
		int num5 = (int)Math.Round(point2.Y);
		int num6 = (int)Math.Round(point3.Y);
		int num7 = (int)Math.Round(point4.Y);
		int num8 = (int)Math.Round(point5.Y);
		list.Add(new Point(num, num5));
		list.Add(new Point(num2, num6));
		list.Add(new Point(num3, num7));
		list.Add(new Point(num4, num8));
		return list;
	}

	internal string GetPointString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		List<Point> points = GetPoints();
		stringBuilder.Append(points[0].X).Append(" ").Append(points[0].Y)
			.Append(" ")
			.Append(points[1].X)
			.Append(" ")
			.Append(points[1].Y)
			.Append(" ")
			.Append(points[2].X)
			.Append(" ")
			.Append(points[2].Y)
			.Append(" ")
			.Append(points[3].X)
			.Append(" ")
			.Append(points[3].Y);
		return stringBuilder.ToString();
	}

	public void ResetByPoints(List<Point> points)
	{
		if (points == null || points.Count != 4)
		{
			throw new Exception("ResetByPoints:Param error");
		}
		double x = points[0].X;
		double y = points[0].Y;
		double x2 = points[1].X;
		double y2 = points[1].Y;
		double x3 = points[2].X;
		double y3 = points[2].Y;
		double x4 = points[3].X;
		double y4 = points[3].Y;
		double angle = Math.Atan2(y2 + y3 - y - y4, x2 + x3 - x - x4) * 180.0 / Math.PI;
		double num = (Math.Sqrt((x2 - x) * (x2 - x) + (y2 - y) * (y2 - y)) + Math.Sqrt((x3 - x4) * (x3 - x4) + (y3 - y4) * (y3 - y4))) / 2.0;
		double num2 = (Math.Sqrt((x4 - x) * (x4 - x) + (y4 - y) * (y4 - y)) + Math.Sqrt((x3 - x2) * (x3 - x2) + (y3 - y2) * (y3 - y2))) / 2.0;
		double num3 = (x + x2 + x3 + x4) / 4.0;
		double num4 = (y + y2 + y3 + y4) / 4.0;
		Point originPoint = new Point(num3 - num / 2.0, num4 - num2 / 2.0);
		Width = num;
		Height = num2;
		OriginPoint = originPoint;
		Angle = angle;
	}

	private void DeleteCommandCmdExecute()
	{
		if (GetParentNode() is IItemsData itemsData)
		{
			itemsData.ItemsData.Remove(this);
		}
	}

	private void SelectedCommandCmdExecute(string name)
	{
		if (!string.IsNullOrEmpty(name))
		{
			Flags = name;
		}
	}
}
