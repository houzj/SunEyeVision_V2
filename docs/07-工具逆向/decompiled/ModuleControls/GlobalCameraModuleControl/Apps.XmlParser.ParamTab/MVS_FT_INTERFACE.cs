using System;
using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public class MVS_FT_INTERFACE
{
	private const string MVSDLL = "VMFeatureTree.dll";

	[DllImport("VMFeatureTree.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int VM_FT_Init(IntPtr pDeviceXml, IntPtr pTranslateXml, IntPtr pParentHandle);

	[DllImport("VMFeatureTree.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int VM_FT_SetDeviceInfo(IntPtr pDeviceInfo);

	[DllImport("VMFeatureTree.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int VM_FT_SetCallBackFunc(delegateGetFeatureCallBack pFunc1, delegateSetFeatureCallBack pFunc2, delegateGetAccessModeCallBack pFunc3, delegateInvalidateNodesCallBack pFun4, delegateActionCallBack pFun5, IntPtr pUser);

	[DllImport("VMFeatureTree.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int VM_FT_SetWidgetSize(int nWidth, int nHeight);

	[DllImport("VMFeatureTree.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int VM_FT_SetWidgetPosition(int x, int y);

	[DllImport("VMFeatureTree.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int VM_FT_SetTitleVisible(bool bVisible);

	[DllImport("VMFeatureTree.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int VM_FT_Start(bool bModel, int nLanguage);

	[DllImport("VMFeatureTree.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int VM_FT_Close();

	[DllImport("VMFeatureTree.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int VM_FT_UpdateFeatures();

	[DllImport("VMFeatureTree.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int VM_FT_Release();
}
