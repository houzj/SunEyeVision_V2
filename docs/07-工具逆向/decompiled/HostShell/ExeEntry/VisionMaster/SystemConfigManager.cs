using System;
using System.Runtime.InteropServices;
using Apps.Data;
using VM.PlatformSDKCS;
using VM.Utility;

namespace VisionMaster;

public class SystemConfigManager : NotifyPropertyChangedBase
{
	private static readonly SystemConfigManager instance;

	private string _StrEncryptType;

	private bool _IsSoftEncrypt = false;

	public bool _isFirstGetTime = true;

	public DateTime _startTime;

	public EncryptDefine.AUTH_LOCALHOST_LICENSE_TIME _stLicenseTime;

	private bool? _IsSlientExecute;

	private int? _IsAllModuSlientExecute;

	private bool? _IsClearLastExecuteResult;

	private bool? _IsDirectDownSampling;

	private bool? _IsCallBackCacheEnable;

	private int? _CallBackQueueLength = null;

	public static SystemConfigManager Instance => instance;

	public string StrEncryptType
	{
		get
		{
			return _StrEncryptType;
		}
		set
		{
			if (_StrEncryptType != value)
			{
				_StrEncryptType = value;
				OnPropertyChanged("StrEncryptType");
			}
		}
	}

	public bool IsSoftEncrypt
	{
		get
		{
			return _IsSoftEncrypt;
		}
		set
		{
			if (_IsSoftEncrypt != value)
			{
				_IsSoftEncrypt = value;
				OnPropertyChanged("IsSoftEncrypt");
			}
		}
	}

	public bool ShowHardWareAlertEnable { get; set; } = true;

	public uint MaxRecordCount { get; internal set; }

	public uint NumOfDefLockModeModulesCount { get; internal set; }

	public uint PromptNumOfLockModeModulesCount { get; internal set; }

	public bool IsSlientExecute
	{
		get
		{
			if (!_IsSlientExecute.HasValue)
			{
				_IsSlientExecute = ConfigurationHelper.GetConfig("IfSlientExecute") == "1";
			}
			return _IsSlientExecute.Value;
		}
		set
		{
			if (_IsSlientExecute != value)
			{
				_IsSlientExecute = value;
			}
		}
	}

	public int IsAllModuSlientExecute
	{
		get
		{
			if (!_IsAllModuSlientExecute.HasValue)
			{
				int result = 0;
				if (int.TryParse(ConfigurationHelper.GetConfig("ModuSlientExecuteMode"), out result))
				{
					_IsAllModuSlientExecute = result;
				}
				else
				{
					_IsAllModuSlientExecute = 0;
				}
			}
			return _IsAllModuSlientExecute.Value;
		}
		set
		{
			if (_IsAllModuSlientExecute != value)
			{
				_IsAllModuSlientExecute = value;
			}
		}
	}

	public bool IsClearLastExecuteResult
	{
		get
		{
			if (!_IsClearLastExecuteResult.HasValue)
			{
				_IsClearLastExecuteResult = ConfigurationHelper.GetConfigValue("IsClearLastExecuteResult") == "1";
			}
			return _IsClearLastExecuteResult.Value;
		}
		set
		{
			if (_IsClearLastExecuteResult != value)
			{
				_IsClearLastExecuteResult = value;
			}
		}
	}

	public bool IsDirectDownSampling
	{
		get
		{
			if (!_IsDirectDownSampling.HasValue)
			{
				_IsDirectDownSampling = ConfigurationHelper.GetConfigValue("IsDirectDownSampling") == "1";
			}
			return _IsDirectDownSampling.Value;
		}
		set
		{
			if (_IsDirectDownSampling != value)
			{
				_IsDirectDownSampling = value;
			}
		}
	}

	public bool IsCallBackCacheEnable
	{
		get
		{
			if (!_IsCallBackCacheEnable.HasValue)
			{
				_IsCallBackCacheEnable = ConfigurationHelper.GetConfigValue("IsCallBackCacheEnable") == "1";
			}
			return _IsCallBackCacheEnable.Value;
		}
		set
		{
			if (_IsCallBackCacheEnable != value)
			{
				_IsCallBackCacheEnable = value;
			}
		}
	}

	public int CallBackQueueLength
	{
		get
		{
			if (!_CallBackQueueLength.HasValue)
			{
				int result = 0;
				if (int.TryParse(ConfigurationHelper.GetConfigValue("CallBackQueueLength"), out result))
				{
					_CallBackQueueLength = result;
				}
				else
				{
					_CallBackQueueLength = 10000;
				}
			}
			return _CallBackQueueLength.Value;
		}
		set
		{
			if (_CallBackQueueLength != value)
			{
				_CallBackQueueLength = value;
			}
		}
	}

	static SystemConfigManager()
	{
		instance = new SystemConfigManager();
	}

	private SystemConfigManager()
	{
	}

	public static bool IsAutoLoad()
	{
		ImvsSdkDefine.IMVS_AUTO_LOAD_SOLUTION_INFO iMVS_AUTO_LOAD_SOLUTION_INFO = default;
		if (ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetAutoLoadSolution().Value.nIfAutoLoad == 1)
		{
			return true;
		}
		return false;
	}

	public int GetAuthTime()
	{
		if (_isFirstGetTime)
		{
			_isFirstGetTime = false;
			_startTime = DateTime.Now;
		}
		else
		{
			DateTime now = DateTime.Now;
			int num = (int)now.Subtract(_startTime).TotalSeconds;
			if (num < 300)
			{
				return 0;
			}
			_startTime = now;
		}
		_stLicenseTime = default;
		IntPtr intPtr = Marshal.AllocHGlobal(Marshal.SizeOf(_stLicenseTime));
		Marshal.StructureToPtr(_stLicenseTime, intPtr, fDeleteOld: false);
		int licenseTime = EncryptAPI.GetLicenseTime(EncryptDefine.AUTH_MACHINE_TYPE.AUTH_LOCAL_MACHINE, "", intPtr);
		if (licenseTime == 0)
		{
			_stLicenseTime = (EncryptDefine.AUTH_LOCALHOST_LICENSE_TIME)Marshal.PtrToStructure(intPtr, typeof(EncryptDefine.AUTH_LOCALHOST_LICENSE_TIME));
		}
		Marshal.FreeHGlobal(intPtr);
		return licenseTime;
	}
}
