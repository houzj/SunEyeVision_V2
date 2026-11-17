using System;
using System.Runtime.InteropServices;

namespace VisionMaster;

internal static class NativeMethods
{
	private const string libzmq_x86 = ".\\PublicFile\\x86\\libzmq-v120-x86-4_3_2.dll";

	private const string libzmq_x64 = ".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll";

	private const string libzmq_lib = ".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll";

	private const string hkrzmq = ".\\SMQComm.dll";

	[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode, IntPtr lpSecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

	[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpAttributes, int flProtect, int dwMaximumSizeLow, int dwMaximumSizeHigh, string lpName);

	[DllImport("kernel32", SetLastError = true)]
	public static extern bool FlushViewOfFile(IntPtr lpBaseAddress, IntPtr dwNumBytesToFlush);

	[DllImport("kernel32", SetLastError = true)]
	public static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, int dwDesiredAccess, int dwFileOffsetHigh, int dwFileOffsetLow, IntPtr dwNumBytesToMap);

	[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr OpenFileMapping(int dwDesiredAccess, bool bInheritHandle, string lpName);

	[DllImport("kernel32", SetLastError = true)]
	public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

	[DllImport("kernel32", SetLastError = true)]
	public static extern bool CloseHandle(IntPtr handle);

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern bool DeleteObject(IntPtr hObject);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr zmq_ctx_new();

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr zmq_socket(IntPtr context, int type);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int zmq_close(IntPtr socket);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int zmq_bind(IntPtr socket, string addr);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int zmq_connect(IntPtr socket, string addr);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int zmq_disconnect(IntPtr socket, string addr);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int zmq_setsockopt(IntPtr socket, int option, ref int optval, int optvallen);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int zmq_setsockopt(IntPtr socket, int option, IntPtr optval, int optvallen);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int zmq_getsockopt(IntPtr socket, int option, IntPtr optval, ref int optvallen);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int zmq_send(IntPtr socket, string msg, uint lenth, int flag);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int zmq_recv(IntPtr socket, IntPtr buffer, uint len, int flags);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int zmq_ctx_term(IntPtr context);

	[DllImport(".\\PublicFile\\x64\\libzmq-v120-x64-4_3_2.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int zmq_ctx_shutdown(IntPtr context);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int HKR_MQ_Create(ref IntPtr pSocketHandle, int emWorkMode);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int HKR_MQ_Close(IntPtr pSocketHandle);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int HKR_MQ_Bind(IntPtr hSocketHandle, string pAddr);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int HKR_MQ_Unbind(IntPtr hSocketHandle, string pAddr);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int HKR_MQ_SetSocketOpt(IntPtr hSocketHandle, int emOptType, ref int pInParam, int nParamLen);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int HKR_MQ_GetSocketOpt(IntPtr hSocketHandle, int emOptType, IntPtr pInParam, ref int nParamLen);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int HKR_MQ_Connect(IntPtr hSocketHandle, string pAddr);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int HKR_MQ_Disconnect(IntPtr hSocketHandle, string pAddr);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int HKR_MQ_Send(IntPtr hSocketHandle, IntPtr pSendBuf, int nSendBufLen, int nSendTimeOut = 0);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int HKR_MQ_Recv(IntPtr hSocketHandle, IntPtr pRecvBuf, ref int nRecvBufLen, int nRecvTimeOut = 0);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern int HKR_MQ_RecvEx(IntPtr hSocketHandle, ref IntPtr pRecvBuf, ref int nRecvBufLen, int nRecvTimeOut = 0);

	[DllImport(".\\SMQComm.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern string HKR_MQ_GetStrError(int nErrorCode);
}
