using System;
using System.ComponentModel;
using Apps.Data;
using VM.Core;

namespace GlobalCameraModuleControl;

public class CameraManagerRe : IDisposable, INotifyPropertyChanged
{
	private ImageData image = new ImageData();

	public uint ImageNum { get; set; } = 0u;

	public uint LostPacket { get; set; } = 0u;

	public ImageData Image
	{
		get
		{
			return image;
		}
		set
		{
			if (value != image)
			{
				image = value;
				OnPropertyChanged("Image");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public virtual void OnPropertyChanged(string proName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(proName));
	}

	public CameraManagerRe(string serialNumber, VmModule module)
	{
	}

	public void Dispose()
	{
	}
}
