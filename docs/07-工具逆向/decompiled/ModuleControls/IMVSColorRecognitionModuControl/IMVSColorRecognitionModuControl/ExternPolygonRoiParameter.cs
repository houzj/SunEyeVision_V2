using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using HikPcUI.ImageView;
using Mvvm.Commands;

namespace IMVSColorRecognitionModuControl;

public class ExternPolygonRoiParameter : HikPolygonRoiParameter, IFlags, IPointsRoi, IIndex
{
	public string flags = "";

	private DelegateCommand<string> _SelectedCommand = null;

	private int _index;

	private DelegateCommand _DeleteCommand = null;

	public virtual string Flags
	{
		get
		{
			return flags;
		}
		set
		{
			flags = value;
			NotifyPropertyChanged("Flags");
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

	public List<Point> GetPoints()
	{
		List<Point> list = new List<Point>();
		if (PolygonPoints != null)
		{
			foreach (PolygonPoint polygonPoint in PolygonPoints)
			{
				list.Add(new Point((int)Math.Round(polygonPoint.x), (int)Math.Round(polygonPoint.y)));
			}
		}
		return list;
	}

	public void ResetByPoints(List<Point> points)
	{
		if (points == null || points.Count < 3)
		{
			throw new Exception("ResetByPoints:Param error");
		}
		if (PolygonPoints == null)
		{
			PolygonPoints = new ObservableCollection<PolygonPoint>();
		}
		foreach (Point point in points)
		{
			PolygonPoints.Add(new PolygonPoint
			{
				x = (float)point.X,
				y = (float)point.Y
			});
		}
	}

	private void SelectedCommandCmdExecute(string name)
	{
		if (!string.IsNullOrEmpty(name))
		{
			Flags = name;
		}
	}

	private void DeleteCommandCmdExecute()
	{
		if (GetParentNode() is IItemsData itemsData)
		{
			itemsData.ItemsData.Remove(this);
		}
	}
}
