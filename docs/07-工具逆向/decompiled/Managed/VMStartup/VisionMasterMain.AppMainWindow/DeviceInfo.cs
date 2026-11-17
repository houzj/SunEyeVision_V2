using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MvCamCtrl.NET;

namespace VisionMasterMain.AppMainWindow;

public class DeviceInfo : INotifyPropertyChanged, ICloneable
{
	private string _lowIpAddress;

	private string _highIpAddress;

	public string LowIpAddress
	{
		get
		{
			return _lowIpAddress;
		}
		set
		{
			SetProperty(ref _lowIpAddress, value, "LowIpAddress");
		}
	}

	public string HighIpAddress
	{
		get
		{
			return _highIpAddress;
		}
		set
		{
			SetProperty(ref _highIpAddress, value, "HighIpAddress");
		}
	}

	public string CurrentIP { get; set; }

	public string SubNetMask { get; set; }

	public string DefaultGateway { get; set; }

	public string SerialNumber { get; set; }

	public uint Resverved { get; set; }

	public bool IsController { get; set; }

	public IntPtr DevicePtr { get; set; }

	public MyCamera.MV_CC_DEVICE_INFO DEVICE_INFO { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	protected void SetProperty<T>(ref T prop, T value, [CallerMemberName] string propertyName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(prop, value))
		{
			prop = value;
			NotifyPropertyChanged(propertyName);
		}
	}

	public object Clone()
	{
		return new DeviceInfo
		{
			LowIpAddress = LowIpAddress,
			HighIpAddress = HighIpAddress,
			CurrentIP = CurrentIP,
			SubNetMask = SubNetMask,
			DefaultGateway = DefaultGateway,
			SerialNumber = SerialNumber,
			Resverved = Resverved,
			DevicePtr = DevicePtr,
			IsController = IsController,
			DEVICE_INFO = DEVICE_INFO
		};
	}
}
