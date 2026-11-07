using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Localization;
using VM.Controls;
using VM.PlatformSDKCS;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class ImageConfigItem : NameValueItem
{
	private EnumerationLightForAcq enumerationLight;

	private Float floatExposure;

	private Float floatGain;

	private Integer intTriggerTime;

	private Integer intChannel;

	private Integer intBright;

	private Float floatDistribution;

	private Float floatIrradiation;

	private int _serial;

	private DelegateCommand _DeleteCommand;

	public new string NameA
	{
		get
		{
			Name = AppLocalizationService.GetLang("Image Config") + Serial;
			return Name;
		}
	}

	public EnumerationLightForAcq EnumerationLightA
	{
		get
		{
			if (enumerationLight == null)
			{
				enumerationLight = new EnumerationLightForAcq
				{
					DisplayNameA = "Tab_Light Device",
					DescriptionA = "Tab_Light Device",
					AccessMode = "O",
					CurValue = 0,
					DefaultValue = 0,
					DeviceType = "Light"
				};
				enumerationLight.SetParentNode(this);
				enumerationLight.OnCurValueChanged += OnValuePropertyChanged;
			}
			return enumerationLight;
		}
		set
		{
			enumerationLight = value;
			OnPropertyChanged("EnumerationLightA");
		}
	}

	public Float FloatExposure
	{
		get
		{
			if (floatExposure == null)
			{
				floatExposure = new Float
				{
					DisplayNameA = "RunParam_Camera Exposure Time",
					DescriptionA = "Exposure Time of Current Camera(unit:ms)",
					AccessMode = "O",
					CurValue = 0.0,
					DefaultValue = 0.0,
					MinValue = 0.0,
					MaxValue = 0.0,
					IncValue = 0.1,
					SubscribeVisibleA = false
				};
				floatExposure.OnCurValueChanged += OnValuePropertyChanged;
			}
			return floatExposure;
		}
		set
		{
			floatExposure = value;
			OnPropertyChanged("FloatExposure");
		}
	}

	public Float FloatGain
	{
		get
		{
			if (floatGain == null)
			{
				floatGain = new Float
				{
					DisplayNameA = "CameraGain",
					DescriptionA = "Gain of Current Camera",
					AccessMode = "O",
					CurValue = 0.0,
					DefaultValue = 0.0,
					MinValue = 0.0,
					MaxValue = 0.0,
					IncValue = 0.1,
					SubscribeVisibleA = false
				};
				floatGain.OnCurValueChanged += OnValuePropertyChanged;
			}
			return floatGain;
		}
		set
		{
			floatGain = value;
			OnPropertyChanged("FloatGain");
		}
	}

	public Integer IntTriggerTime
	{
		get
		{
			if (intTriggerTime == null)
			{
				intTriggerTime = new Integer
				{
					DisplayNameA = "RunParam_Trigger Time",
					DescriptionA = "RunParam_Trigger Time",
					AccessMode = "O",
					CurValue = 500,
					DefaultValue = 500,
					MinValue = 0,
					MaxValue = 65535,
					IncValue = 1,
					SubscribeVisibleA = false
				};
				intTriggerTime.OnCurValueChanged += OnValuePropertyChanged;
			}
			return intTriggerTime;
		}
		set
		{
			intTriggerTime = value;
			OnPropertyChanged("IntTriggerTime");
		}
	}

	public Integer IntChannel
	{
		get
		{
			if (intChannel == null)
			{
				intChannel = new Integer
				{
					DisplayNameA = "RunParam_Light Channel",
					DescriptionA = "RunParam_Light Channel",
					AccessMode = "O",
					CurValue = 1,
					DefaultValue = 1,
					MinValue = 1,
					MaxValue = 6,
					IncValue = 1,
					SubscribeVisibleA = false
				};
				intChannel.OnCurValueChanged += OnValuePropertyChanged;
			}
			return intChannel;
		}
		set
		{
			intChannel = value;
			OnPropertyChanged("IntChannel");
		}
	}

	public Integer IntBright
	{
		get
		{
			if (intBright == null)
			{
				intBright = new Integer
				{
					DisplayNameA = "RunParam_Light Brightness",
					DescriptionA = "RunParam_Light Brightness",
					AccessMode = "O",
					CurValue = 0,
					DefaultValue = 0,
					MinValue = 0,
					MaxValue = 255,
					IncValue = 1,
					SubscribeVisibleA = false
				};
				intBright.OnCurValueChanged += OnValuePropertyChanged;
			}
			return intBright;
		}
		set
		{
			intBright = value;
			OnPropertyChanged("IntBright");
		}
	}

	public Float FloatDistribution
	{
		get
		{
			if (floatDistribution == null)
			{
				floatDistribution = new Float
				{
					DisplayNameA = "RunParam_Distribution Angle",
					DescriptionA = "RunParam_Distribution Angle",
					AccessMode = "O",
					CurValue = 0.0,
					DefaultValue = 0.0,
					MinValue = -180.0,
					MaxValue = 180.0,
					IncValue = 0.1,
					SubscribeVisibleA = false
				};
				floatDistribution.OnCurValueChanged += OnValuePropertyChanged;
			}
			return floatDistribution;
		}
		set
		{
			floatDistribution = value;
			OnPropertyChanged("FloatDistribution");
		}
	}

	public Float FloatIrradiation
	{
		get
		{
			if (floatIrradiation == null)
			{
				floatIrradiation = new Float
				{
					DisplayNameA = "RunParam_Irradiation Angle",
					DescriptionA = "RunParam_Irradiation Angle",
					AccessMode = "O",
					CurValue = 0.0,
					DefaultValue = 0.0,
					MinValue = 0.0,
					MaxValue = 90.0,
					IncValue = 0.1,
					SubscribeVisibleA = false
				};
				floatIrradiation.OnCurValueChanged += OnValuePropertyChanged;
			}
			return floatIrradiation;
		}
		set
		{
			floatIrradiation = value;
			OnPropertyChanged("FloatIrradiation");
		}
	}

	public int Serial
	{
		get
		{
			return _serial;
		}
		set
		{
			if (_serial != value)
			{
				_serial = value;
				OnPropertyChanged("Serial");
				OnPropertyChanged("NameA");
			}
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			if (_DeleteCommand == null)
			{
				_DeleteCommand = new DelegateCommand(DeleteCommandCmdExecute);
			}
			return _DeleteCommand;
		}
	}

	public event EventHandler ValuePropertyChanged;

	private void DeleteCommandCmdExecute()
	{
		_ = Serial;
		if (parentNode == null)
		{
			return;
		}
		if ((parentNode as ImageConfig).VMRun != null && (parentNode as ImageConfig).VMRun.IsRunning)
		{
			HikMessageBox.Show("Lang_OpeRun", MessageIcons.Warning, isTranslate: true);
			return;
		}
		ObservableCollection<ImageConfigItem> itemsA = (parentNode as ImageConfig).ItemsA;
		itemsA.Remove(this);
		OnCurValueChanged -= OnValuePropertyChanged;
		for (int i = 0; i < itemsA.Count; i++)
		{
			if (itemsA[i].Serial > Serial)
			{
				itemsA[i].Serial--;
			}
		}
		(parentNode as ImageConfig).SetData();
	}

	public void OnValuePropertyChanged(object sender, EventArgs e)
	{
		if (ValuePropertyChanged != null)
		{
			ValuePropertyChanged(this, new EventArgs());
		}
	}

	public IMVS_IMAGE_CONFIG ConvertToIMVS_IMAGE_CONFIG()
	{
		try
		{
			_ = ServerSDKManager.serverSDKManager.mGlobalModuleManager.GetModuleListByType(7u).Value;
		}
		catch (VmException)
		{
		}
		string[] array = EnumerationLightA.Value?.Split('#');
		IMVS_IMAGE_CONFIG result = new IMVS_IMAGE_CONFIG
		{
			nSerial = Serial,
			nFloatExposure = FloatExposure.CurValue,
			nFloatGain = FloatGain.CurValue,
			nIntTriggerTime = IntTriggerTime.CurValueA,
			nIntChannel = IntChannel.CurValue,
			nIntBright = IntBright.CurValue,
			nFloatIrradiation = FloatIrradiation.CurValue,
			nFloatDistribution = FloatDistribution.CurValue,
			nIndex = -1,
			nId = -1,
			nDeviceType = -1,
			nchDeviceGuid = null
		};
		if (array != null && array[0] != "-1")
		{
			result.nIndex = int.Parse(array[0]);
			result.nId = int.Parse(array[1]);
			result.nDeviceType = int.Parse(array[2]);
			result.nchDeviceGuid = array[3];
		}
		return result;
	}
}
