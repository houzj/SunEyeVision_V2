using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Apps.ErrorCode;
using Apps.Log;
using MvCamCtrl.NET;

namespace VisionMasterMain.AppMainWindow;

public static class DeviceUtil
{
	public static uint GetIpUintFromString(this string strIp)
	{
		string[] array = strIp.Split('.');
		int num = Convert.ToInt32(array[0]);
		int num2 = Convert.ToInt32(array[1]);
		int num3 = Convert.ToInt32(array[2]);
		int num4 = Convert.ToInt32(array[3]);
		return (uint)((num << 24) | (num2 << 16) | (num3 << 8) | num4);
	}

	public static string GetLowIPStrFromUint(this uint ip)
	{
		byte b = (byte)(ip >> 24);
		byte b2 = (byte)(ip - (b << 24) >> 16);
		byte b3 = (byte)(ip - (b2 << 16) - (b << 24) >> 8);
		byte b4 = (byte)(ip - (b2 << 16) - (b << 24) - (b3 << 8));
		return $"{b}.{b2}.{b3}.{1}";
	}

	public static uint GetMaxIPStrFromUint(this string localIp, string subNetMask)
	{
		string[] array = subNetMask.Split('.');
		int num = Convert.ToInt32(array[0]);
		int num2 = Convert.ToInt32(array[1]);
		int num3 = Convert.ToInt32(array[2]);
		int num4 = Convert.ToInt32(array[3]);
		string[] array2 = localIp.Split('.');
		int num5 = Convert.ToInt32(array2[0]);
		int num6 = Convert.ToInt32(array2[1]);
		int num7 = Convert.ToInt32(array2[2]);
		int num8 = Convert.ToInt32(array2[3]);
		int num9 = num & num5;
		if (num9 == 0)
		{
			num9 = 255 - num;
		}
		int num10 = num2 & num6;
		if (num10 == 0)
		{
			num10 = 255 - num2;
		}
		int num11 = num3 & num7;
		if (num11 == 0)
		{
			num11 = 255 - num3;
		}
		int num12 = num4 & num8;
		if (num12 == 0)
		{
			num12 = ((num4 != 0) ? (255 - num4) : 254);
		}
		return (uint)((num9 << 24) | (num10 << 16) | (num11 << 8) | num12);
	}

	public static string GetIPStrFromUint(this uint ip)
	{
		byte b = (byte)(ip >> 24);
		byte b2 = (byte)(ip - (b << 24) >> 16);
		byte b3 = (byte)(ip - (b2 << 16) - (b << 24) >> 8);
		byte b4 = (byte)(ip - (b2 << 16) - (b << 24) - (b3 << 8));
		return $"{b}.{b2}.{b3}.{b4}";
	}

	private static MyCamera GetMyCameraInstance(MyCamera.MV_CC_DEVICE_INFO mV_CC_DEVICE_INFO)
	{
		MyCamera myCamera = new MyCamera();
		int num = myCamera.MV_CC_CreateDevice_NET(ref mV_CC_DEVICE_INFO);
		if (num != 0)
		{
			LogHelper.Info(string.Format("MV_CC_CreateDevice_NET Failed,Error Code:{0}", num.ToString("x8")));
			throw new Exception(num.ToString());
		}
		num = myCamera.MV_CC_OpenDevice_NET();
		if (num != 0)
		{
			LogHelper.Info(string.Format("MV_CC_OpenDevice_NET Failed,Error Code:{0}", num.ToString("x8")));
			throw new Exception(num.ToString());
		}
		return myCamera;
	}

	private static void CloseDevice(this MyCamera device)
	{
		if (device.MV_CC_IsDeviceConnected_NET())
		{
			int num = device.MV_CC_CloseDevice_NET();
			if (num != 0)
			{
				LogHelper.Info(string.Format("MV_CC_CloseDevice_NET Failed,Error Code:{0}", num.ToString("x8")));
				throw new Exception(num.ToString());
			}
			num = device.MV_CC_DestroyDevice_NET();
			if (num != 0)
			{
				LogHelper.Info(string.Format("MV_CC_DestroyDevice_NET Failed,Error Code:{0}", num.ToString("x8")));
				throw new Exception(num.ToString());
			}
		}
	}

	private static void RestartDevice(this MyCamera device, string serialNumber)
	{
		int num = device.MV_CC_SetCommandValue_NET("DeviceReset");
		if (num != 0)
		{
			LogHelper.Info(string.Format("MV_CC_SetCommandValue_NET Failed,Error Code:{0}", num.ToString("x8")));
			throw new Exception(num.ToString());
		}
		Thread.Sleep(3000);
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		while (stopwatch.ElapsedMilliseconds <= 60000)
		{
			int num2 = (from d in GetDeviceList()
				where d.SerialNumber == serialNumber
				select d).Count();
			if (num2 > 0)
			{
				return;
			}
			Thread.Sleep(3000);
		}
		throw new Exception("Device Restart Timeout!");
	}

	private static DeviceInfo GetDeviceInfo(IntPtr intPtr, MyCamera.MV_CC_DEVICE_INFO tempDevInfo, MyCamera.MV_GIGE_DEVICE_INFO stGigEDeviceInfo)
	{
		uint num = stGigEDeviceInfo.nReserved[0];
		return new DeviceInfo
		{
			DevicePtr = intPtr,
			DEVICE_INFO = tempDevInfo,
			CurrentIP = stGigEDeviceInfo.nCurrentIp.GetIPStrFromUint(),
			SubNetMask = stGigEDeviceInfo.nCurrentSubNetMask.GetIPStrFromUint(),
			DefaultGateway = stGigEDeviceInfo.nDefultGateWay.GetIPStrFromUint(),
			HighIpAddress = stGigEDeviceInfo.nDefultGateWay.GetIPStrFromUint(),
			LowIpAddress = stGigEDeviceInfo.nDefultGateWay.GetLowIPStrFromUint(),
			SerialNumber = stGigEDeviceInfo.chSerialNumber,
			Resverved = num,
			IsController = (num == 1297503062)
		};
	}

	private static void SetCameraWithStaticIP(this DeviceInfo deviceInfo, ref MyCamera device)
	{
		uint ipUintFromString = deviceInfo.CurrentIP.GetIpUintFromString();
		uint ipUintFromString2 = deviceInfo.SubNetMask.GetIpUintFromString();
		uint ipUintFromString3 = deviceInfo.DefaultGateway.GetIpUintFromString();
		device = GetMyCameraInstance(deviceInfo.DEVICE_INFO);
		MyCamera.MV_CC_DEVICE_INFO pstDevInfo = default;
		int num = device.MV_CC_GetDeviceInfo_NET(ref pstDevInfo);
		MyCamera.MV_GIGE_DEVICE_INFO stGigEDeviceInfo = (MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(pstDevInfo.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO));
		DeviceInfo deviceInfo2 = GetDeviceInfo(deviceInfo.DevicePtr, pstDevInfo, stGigEDeviceInfo);
		LogHelper.Info($"[静态IP修改前（获取结果执行是否成功：{num}）] 原IP：{deviceInfo2.CurrentIP}，修改为：{deviceInfo.CurrentIP}");
		int num2 = device.MV_GIGE_SetIpConfig_NET(83886080u);
		if (num2 != 0)
		{
			LogHelper.Info(string.Format("MV_GIGE_SetIpConfig_NET Failed,Error Code:{0}", num2.ToString("x8")));
			throw new Exception(num2.ToString());
		}
		num2 = device.MV_CC_CloseDevice_NET();
		if (num2 != 0)
		{
			LogHelper.Info(string.Format("MV_CC_CloseDevice_NET Failed,Error Code:{0}", num2.ToString("x8")));
			throw new Exception(num2.ToString());
		}
		int num3 = device.MV_GIGE_ForceIpEx_NET(ipUintFromString, ipUintFromString2, ipUintFromString3);
		if (num3 != 0)
		{
			LogHelper.Info(string.Format("MV_GIGE_ForceIpEx_NET Failed,Error Code:{0}", num3.ToString("x8")));
			throw new Exception(num3.ToString());
		}
	}

	private static void SetCameraWithDHCP(this DeviceInfo deviceInfo, ref MyCamera device)
	{
		device = GetMyCameraInstance(deviceInfo.DEVICE_INFO);
		int num = device.MV_GIGE_SetIpConfig_NET(100663296u);
		if (num != 0)
		{
			LogHelper.Info(string.Format("MV_GIGE_SetNetTransMode_NET Failed,Error Code:{0}", num.ToString("x8")));
			throw new Exception(num.ToString());
		}
		device.RestartDevice(deviceInfo.SerialNumber);
		MyCamera.MV_CC_DEVICE_INFO pstDevInfo = default;
		device.MV_CC_GetDeviceInfo_NET(ref pstDevInfo);
	}

	private static void SetCameraWithLLA(this DeviceInfo deviceInfo, ref MyCamera device)
	{
		device = GetMyCameraInstance(deviceInfo.DEVICE_INFO);
		int num = device.MV_GIGE_SetIpConfig_NET(67108864u);
		if (num != 0)
		{
			LogHelper.Info(string.Format("MV_GIGE_SetNetTransMode_NET Failed,Error Code:{0}", num.ToString("x8")));
			throw new Exception(num.ToString());
		}
		device.RestartDevice(deviceInfo.SerialNumber);
		MyCamera.MV_CC_DEVICE_INFO pstDevInfo = default;
		device.MV_CC_GetDeviceInfo_NET(ref pstDevInfo);
	}

	public static List<DeviceInfo> GetDeviceList()
	{
		List<DeviceInfo> list = new List<DeviceInfo>();
		MyCamera.MV_CC_DEVICE_INFO_LIST stDevList = default;
		if (MyCamera.MV_CC_EnumDevices_NET(1u, ref stDevList) == 0)
		{
			for (int i = 0; i < stDevList.nDeviceNum; i++)
			{
				MyCamera.MV_CC_DEVICE_INFO tempDevInfo = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(stDevList.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO));
				MyCamera.MV_GIGE_DEVICE_INFO stGigEDeviceInfo = (MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(tempDevInfo.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO));
				list.Add(GetDeviceInfo(stDevList.pDeviceInfo[i], tempDevInfo, stGigEDeviceInfo));
			}
			list = list.Where((DeviceInfo t) => t.IsController).ToList();
		}
		return list;
	}

	public static void SetDeviceMode(this DeviceInfo deviceInfo, int mode)
	{
		MyCamera device = null;
		try
		{
			switch (mode)
			{
			case 83886080:
				deviceInfo.SetCameraWithStaticIP(ref device);
				break;
			case 100663296:
				deviceInfo.SetCameraWithDHCP(ref device);
				break;
			case 67108864:
				deviceInfo.SetCameraWithLLA(ref device);
				break;
			}
		}
		catch (Exception ex)
		{
			if (device != null)
			{
				device.CloseDevice();
			}
			if (int.TryParse(ex.Message, out var result))
			{
				string message = string.Format("{0}({1})", ErrorCodeInfo.GetErrorInfo(result), result.ToString("x8"));
				throw new Exception(message);
			}
			throw new Exception(ex.Message);
		}
		finally
		{
			if (device != null)
			{
				device.CloseDevice();
			}
		}
	}
}
