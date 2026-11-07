using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Apps.Log;
using HikExternalCall.Common;

namespace IMVSColorRecognitionModuControl;

public class ColorData : INotifyPropertyChanged
{
	private PointCollection _HDataPoints;

	private PointCollection _SDataPoints;

	private PointCollection _IDataPoints;

	public int H_num { get; set; }

	public int S_num { get; set; }

	public int I_num { get; set; }

	public PointCollection HDataPoints
	{
		get
		{
			if (_HDataPoints == null)
			{
				_HDataPoints = new PointCollection();
			}
			return _HDataPoints;
		}
		set
		{
			if (_HDataPoints != value)
			{
				_HDataPoints = value;
				NotifyPropertyChanged("HDataPoints");
			}
		}
	}

	public PointCollection SDataPoints
	{
		get
		{
			if (_SDataPoints == null)
			{
				_SDataPoints = new PointCollection();
			}
			return _SDataPoints;
		}
		set
		{
			if (_SDataPoints != value)
			{
				_SDataPoints = value;
				NotifyPropertyChanged("SDataPoints");
			}
		}
	}

	public PointCollection IDataPoints
	{
		get
		{
			if (_IDataPoints == null)
			{
				_IDataPoints = new PointCollection();
			}
			return _IDataPoints;
		}
		set
		{
			if (_IDataPoints != value)
			{
				_IDataPoints = value;
				NotifyPropertyChanged("IDataPoints");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public unsafe void GetColorData(byte[] data)
	{
		try
		{
			byte* ptr = (byte*)ExternalCallHelper.Bytes2IntPtr(data, out var hObject).ToPointer();
			int num = 0;
			H_num = *(int*)(ptr + num);
			num += 4;
			S_num = *(int*)(ptr + num);
			num += 4;
			I_num = *(int*)(ptr + num);
			num += 4;
			int num2 = 4;
			PointCollection pointCollection = new PointCollection();
			PointCollection pointCollection2 = new PointCollection();
			PointCollection pointCollection3 = new PointCollection();
			for (int i = 0; i < H_num; i++)
			{
				pointCollection.Add(new Point(i, Convert.ToDouble(*(float*)(ptr + num))));
				num += num2;
			}
			HDataPoints = pointCollection;
			for (int j = 0; j < S_num; j++)
			{
				pointCollection2.Add(new Point(j, Convert.ToDouble(*(float*)(ptr + num))));
				num += num2;
			}
			SDataPoints = pointCollection2;
			for (int k = 0; k < I_num; k++)
			{
				pointCollection3.Add(new Point(k, Convert.ToDouble(*(float*)(ptr + num))));
				num += num2;
			}
			IDataPoints = pointCollection3;
			ExternalCallHelper.FreeGCHandle(hObject);
		}
		catch (Exception ex)
		{
			LogHelper.Info("Exception in color GetColorData" + ex.Message);
		}
	}

	public void Clear()
	{
		PointCollection hDataPoints = new PointCollection();
		HDataPoints.Clear();
		HDataPoints = hDataPoints;
		PointCollection sDataPoints = new PointCollection();
		SDataPoints.Clear();
		SDataPoints = sDataPoints;
		PointCollection iDataPoints = new PointCollection();
		IDataPoints.Clear();
		IDataPoints = iDataPoints;
	}
}
