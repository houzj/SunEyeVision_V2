using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using GlobalCameraModuleControl;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class CameraPreview : NameValueItem
{
	public bool isRealTimeMode = false;

	public int _imageHeight = 0;

	public int _imageWidth = 0;

	public uint _imageNum = 0u;

	public uint _lostPackgeNum = 0u;

	private Task refreshTask = null;

	public string SerialNumber { get; set; } = "";

	public bool IsRealTimeMode
	{
		get
		{
			return isRealTimeMode;
		}
		set
		{
			isRealTimeMode = value;
			Value = value.ToString();
			SetParamUnit();
		}
	}

	public CameraManager Camera { get; set; } = null;

	public int ImageHeight
	{
		get
		{
			return _imageHeight;
		}
		set
		{
			_imageHeight = value;
			OnPropertyChanged("ImageHeight");
		}
	}

	public int ImageWidth
	{
		get
		{
			return _imageWidth;
		}
		set
		{
			_imageWidth = value;
			OnPropertyChanged("ImageWidth");
		}
	}

	public uint ImageNum
	{
		get
		{
			return _imageNum;
		}
		set
		{
			_imageNum = value;
			OnPropertyChanged("ImageNum");
		}
	}

	public uint LostPackgeNum
	{
		get
		{
			return _lostPackgeNum;
		}
		set
		{
			_lostPackgeNum = value;
			OnPropertyChanged("LostPackgeNum");
		}
	}

	public override bool NeedInit => false;

	private void StartRefreshCamera()
	{
		Reset();
		RefreshCameraInfo();
		refreshTask = new Task(() =>
		{
			do
			{
				Thread.Sleep(300);
				RefreshCameraInfo();
			}
			while (IsRealTimeMode);
		});
		refreshTask.Start();
	}

	public void DoCameraPreview()
	{
		try
		{
			if (!IsRealTimeMode)
			{
				IsRealTimeMode = true;
				StartRefreshCamera();
				CreatePreviewWindow();
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
	}

	private void CreatePreviewWindow()
	{
		PreviewWindow previewWindow = new PreviewWindow();
		previewWindow.Closed += ExitRealTimeMode;
		previewWindow.SetBinding(PreviewWindow.LostPackgeNumProperty, new Binding("LostPackgeNum"));
		previewWindow.DataContext = this;
		try
		{
			previewWindow.Owner = Application.Current?.MainWindow ?? null;
		}
		catch
		{
		}
		previewWindow.WindowStartupLocation = WindowStartupLocation.Manual;
		if (Application.Current?.MainWindow != null)
		{
			previewWindow.Left = Application.Current.MainWindow.Left;
			previewWindow.Top = Application.Current.MainWindow.Top + 120.0;
		}
		previewWindow.Show();
		VmModule vmModule = GetVmModule();
		Camera = new CameraManager(SerialNumber, vmModule);
		previewWindow.SetBinding(PreviewWindow.ImageSourceProperty, new Binding("Camera.Image"));
	}

	private void ExitRealTimeMode(object sender, EventArgs e)
	{
		IsRealTimeMode = false;
		Camera?.Dispose();
	}

	private void GetParamValue(string Name, ref string Value)
	{
		VmModule vmModule = GetVmModule();
		if (vmModule != null)
		{
			ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(vmModule.ID, Name, ref Value);
		}
	}

	private void SetParamValue(string Name, string Value)
	{
		VmModule vmModule = GetVmModule();
		if (vmModule != null)
		{
			ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(vmModule.ID, Name, Value);
		}
	}

	public int RefreshCameraInfo()
	{
		string Value = string.Empty;
		try
		{
			GetParamValue("CameraPreviewInfo", ref Value);
			string[] array = Value.Split('$');
			if (array.Length == 7)
			{
				ImageWidth = Convert.ToInt32(array[4]);
				ImageHeight = Convert.ToInt32(array[5]);
				SerialNumber = array[6];
				if (SerialNumber != "")
				{
					ImageNum = Camera?.ImageNum ?? 0;
					LostPackgeNum = Camera?.LostPacket ?? 0;
				}
				else
				{
					ImageNum = 0u;
					LostPackgeNum = 0u;
				}
			}
		}
		catch
		{
			Reset();
		}
		return 0;
	}

	private void Reset()
	{
		SerialNumber = "";
		ImageNum = 0u;
		LostPackgeNum = 0u;
		ImageHeight = 0;
		ImageWidth = 0;
	}
}
