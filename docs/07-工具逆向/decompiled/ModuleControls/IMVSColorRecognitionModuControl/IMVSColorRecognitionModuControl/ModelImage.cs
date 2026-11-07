using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Log;
using Hik.Util;
using HikExternalCall.Common;

namespace IMVSColorRecognitionModuControl;

public class ModelImage : INotifyPropertyChanged
{
	private string _name;

	private WriteableBitmap _source;

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			_name = value;
			OnPropertyChanged("Name");
		}
	}

	public WriteableBitmap Source
	{
		get
		{
			return _source;
		}
		set
		{
			_source = value;
			OnPropertyChanged("Source");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public unsafe static ObservableCollection<ModelImage> GetModuleImages(byte[] data)
	{
		ObservableCollection<ModelImage> observableCollection = new ObservableCollection<ModelImage>();
		try
		{
			IntPtr intPtr = ExternalCallHelper.Bytes2IntPtr(data, out var hObject);
			byte* ptr = (byte*)intPtr.ToPointer();
			int num = 0;
			int num2 = *(int*)(ptr + num);
			num += 4;
			for (int i = 0; i < num2; i++)
			{
				ModelImage modelImage = new ModelImage();
				byte[] array = new byte[256];
				Marshal.Copy((IntPtr)(ptr + num), array, 0, 256);
				num += 256;
				string name = StringUtil.UTF8GetString(array);
				modelImage.Name = name;
				int num3 = *(int*)(ptr + num);
				num += 4;
				for (int j = 0; j < num3; j++)
				{
					int num4 = *(int*)(ptr + num);
					num += 4;
					int num5 = *(int*)(ptr + num);
					num += 4;
					int num6 = *(int*)(ptr + num);
					num += 4;
					num += 4;
					int num7 = *(int*)(ptr + num);
					num += 4;
					if (num7 != 0)
					{
						num += 4;
						num += 4;
						num += 4;
					}
					if (j == 0)
					{
						WriteableBitmap writeableBitmap = new WriteableBitmap(num5, num6, 96.0, 96.0, PixelFormats.Rgb24, null);
						writeableBitmap.WritePixels(new Int32Rect(0, 0, num5, num6), intPtr + num, num4, writeableBitmap.PixelWidth * writeableBitmap.Format.BitsPerPixel / 8);
						modelImage.Source = writeableBitmap;
					}
					num += num4;
					if (num7 != 0)
					{
						num += num7;
					}
				}
				observableCollection.Add(modelImage);
			}
			ExternalCallHelper.FreeGCHandle(hObject);
		}
		catch (Exception ex)
		{
			LogHelper.Info("Exception in color GetModuleImages" + ex.Message);
		}
		return observableCollection;
	}

	public static ObservableCollection<ModelImage> GetModuleImages(List<ThbalObj> pathList)
	{
		ObservableCollection<ModelImage> observableCollection = new ObservableCollection<ModelImage>();
		foreach (ThbalObj path in pathList)
		{
			if (File.Exists(path.path))
			{
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.UriSource = new Uri(path.path);
				bitmapImage.EndInit();
				WriteableBitmap source = new WriteableBitmap(bitmapImage);
				observableCollection.Add(new ModelImage
				{
					Source = source,
					Name = path.name
				});
			}
		}
		return observableCollection;
	}
}
