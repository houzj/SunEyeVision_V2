using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using MvCamCtrl.NET;
using VM.Controls;
using VM.Core;

namespace GlobalCameraModuleControl;

public class CameraManager : IDisposable, INotifyPropertyChanged
{
	private MyCamera device = null;

	private ImageData image = new ImageData();

	private MyCamera.cbOutputExdelegate cbImage = null;

	private const uint MV_GVSP_PIX_ID_MASK = 65535u;

	private const uint MV_GVSP_PIX_COLOR_MASK = 4278190080u;

	private const uint MV_GVSP_PIX_COLOR = 33554432u;

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

	private void ImageCallBack(IntPtr pData, ref MyCamera.MV_FRAME_OUT_INFO_EX pFrameInfo, IntPtr pUser)
	{
		try
		{
			ImageData dstImage = new ImageData();
			ImageNum = pFrameInfo.nFrameNum;
			LostPacket = pFrameInfo.nLostPacket;
			uint num = (uint)(pFrameInfo.enPixelType & (MyCamera.MvGvspPixelType)0xFFFF);
			bool flag = (pFrameInfo.enPixelType & (MyCamera.MvGvspPixelType)(-16777216)) == (MyCamera.MvGvspPixelType)33554432 || (num >= 8 && num <= 19) || (num >= 38 && num <= 49);
			dstImage.PixelFormat = (flag ? PixelFormats.Rgb24 : PixelFormats.Gray8);
			dstImage.Width = pFrameInfo.nWidth;
			dstImage.Height = pFrameInfo.nHeight;
			int num2 = pFrameInfo.nWidth * pFrameInfo.nHeight * ((!flag) ? 1 : 3);
			if ((dstImage.ImageBuffer?.Length ?? 0) < num2)
			{
				dstImage.ImageBuffer = new byte[num2];
			}
			if (pFrameInfo.enPixelType != (MyCamera.MvGvspPixelType)ImageData.GetPixelFormatProtocolCode(dstImage.PixelFormat))
			{
				ConvertPixelFormat(ref pFrameInfo, pData, ref dstImage);
			}
			else
			{
				Marshal.Copy(pData, dstImage.ImageBuffer, 0, (int)pFrameInfo.nFrameLen);
			}
			Image = dstImage;
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message + ex.StackTrace);
		}
	}

	private void ConvertPixelFormat(ref MyCamera.MV_FRAME_OUT_INFO_EX pFrameInfo, IntPtr pData, ref ImageData dstImage)
	{
		MyCamera.MV_PIXEL_CONVERT_PARAM pstCvtParam = new MyCamera.MV_PIXEL_CONVERT_PARAM
		{
			enDstPixelType = (MyCamera.MvGvspPixelType)ImageData.GetPixelFormatProtocolCode(dstImage.PixelFormat),
			enSrcPixelType = pFrameInfo.enPixelType,
			nHeight = pFrameInfo.nHeight,
			nWidth = pFrameInfo.nWidth,
			nSrcDataLen = pFrameInfo.nFrameLen,
			pSrcData = pData,
			nDstBufferSize = (uint)dstImage.ImageBuffer.Length
		};
		GCHandle gCHandle = GCHandle.Alloc(dstImage.ImageBuffer, GCHandleType.Pinned);
		IntPtr pDstBuffer = gCHandle.AddrOfPinnedObject();
		pstCvtParam.pDstBuffer = pDstBuffer;
		int? num = device?.MV_CC_ConvertPixelType_NET(ref pstCvtParam);
		if (num != 0)
		{
			LogHelper.Error("MV_CC_ConvertPixelType_NET:" + num);
		}
		if (gCHandle.IsAllocated)
		{
			gCHandle.Free();
		}
	}

	public CameraManager(string serialNumber, VmModule module)
	{
		int num = 0;
		bool flag = false;
		device = new MyCamera();
		cbImage = ImageCallBack;
		MyCamera.MV_CC_DEVICE_INFO_LIST stDevList = default;
		MyCamera.MV_CC_DEVICE_INFO stDevInfo = default;
		string text = module.Params["Type"];
		if (text == "0")
		{
			bool flag2 = false;
			num = MyCamera.MV_CC_EnumDevices_NET(5u, ref stDevList);
			if (num != 0)
			{
				LogHelper.Error("MV_CC_EnumDevices_NET" + num);
				throw new Exception($"{ErrorCodeInfo.GetErrorInfo(num)}({num:x8})");
			}
			for (int i = 0; i < stDevList.nDeviceNum; i++)
			{
				MyCamera.MV_CC_DEVICE_INFO mV_CC_DEVICE_INFO = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(stDevList.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO));
				if (1 == mV_CC_DEVICE_INFO.nTLayerType)
				{
					if (((MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(mV_CC_DEVICE_INFO.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO))).chSerialNumber == serialNumber)
					{
						stDevInfo = mV_CC_DEVICE_INFO;
						flag2 = true;
					}
				}
				else if (4 == mV_CC_DEVICE_INFO.nTLayerType && ((MyCamera.MV_USB3_DEVICE_INFO)MyCamera.ByteToStruct(mV_CC_DEVICE_INFO.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO))).chSerialNumber == serialNumber)
				{
					stDevInfo = mV_CC_DEVICE_INFO;
					flag2 = true;
					flag = true;
				}
			}
			if (!flag2)
			{
				LogHelper.Error("findDev" + flag2);
				throw new Exception($"{ErrorCodeInfo.GetErrorInfo(-536869872)}({3758097424u:x8})");
			}
			if (flag)
			{
				LogHelper.Error("isUsb" + flag);
				HikMessageBox.Show(AppLocalizationService.GetLang("U口相机不支持实时取流"), MessageIcons.Error);
				return;
			}
			num = device.MV_CC_CreateDevice_NET(ref stDevInfo);
			if (num != 0)
			{
				LogHelper.Error("MV_CC_CreateDevice_NET" + num);
				throw new Exception($"{ErrorCodeInfo.GetErrorInfo(num)}({num:x8})");
			}
			num = device.MV_CC_OpenDevice_NET(7u, 0);
			if (num != 0)
			{
				LogHelper.Error("MV_CC_OpenDevice_NET" + num);
				throw new Exception($"{ErrorCodeInfo.GetErrorInfo(num)}({num:x8})");
			}
		}
		MyCamera.MV_CC_TRANSMISSION_TYPE pstTransmissionType = new MyCamera.MV_CC_TRANSMISSION_TYPE
		{
			enTransmissionType = MyCamera.MV_GIGE_TRANSMISSION_TYPE.MV_GIGE_TRANSTYPE_MULTICAST,
			nDestIp = GetIp("239.192.1.1"),
			nDestPort = 1042
		};
		num = device.MV_GIGE_SetTransmissionType_NET(ref pstTransmissionType);
		if (num != 0)
		{
			LogHelper.Error("MV_GIGE_SetTransmissionType_NET" + num);
			throw new Exception($"{ErrorCodeInfo.GetErrorInfo(num)}({num:x8})");
		}
		num = device.MV_CC_RegisterImageCallBackEx_NET(cbImage, IntPtr.Zero);
		if (num != 0)
		{
			LogHelper.Error("MV_CC_RegisterImageCallBackEx_NET" + num);
			throw new Exception($"{ErrorCodeInfo.GetErrorInfo(num)}({num:x8})");
		}
		num = device.MV_CC_StartGrabbing_NET();
		if (num != 0)
		{
			LogHelper.Error("MV_CC_StartGrabbing_NET" + num);
			throw new Exception($"{ErrorCodeInfo.GetErrorInfo(num)}({num:x8})");
		}
		module.Params["GrabStatus"] = "True";
	}

	private uint GetIp(string strIp)
	{
		string[] array = strIp.Split('.');
		int num = Convert.ToInt32(array[0]);
		int num2 = Convert.ToInt32(array[1]);
		int num3 = Convert.ToInt32(array[2]);
		int num4 = Convert.ToInt32(array[3]);
		return (uint)((num << 24) | (num2 << 16) | (num3 << 8) | num4);
	}

	public void Dispose()
	{
		if (device != null)
		{
			device.MV_CC_StopGrabbing_NET();
			device.MV_CC_CloseDevice_NET();
			device.MV_CC_DestroyDevice_NET();
			device = null;
		}
	}
}
