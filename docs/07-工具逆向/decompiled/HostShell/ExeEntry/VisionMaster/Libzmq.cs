using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VisionMaster;

public class Libzmq
{
	public enum Socket_Types
	{
		ZMQ_PAIR = 0,
		ZMQ_PUB = 1,
		ZMQ_SUB = 2,
		ZMQ_REQ = 3,
		ZMQ_REP = 4,
		ZMQ_DEALER = 5,
		ZMQ_ROUTER = 6,
		ZMQ_PULL = 7,
		ZMQ_PUSH = 8,
		ZMQ_XPUB = 9,
		ZMQ_XSUB = 10,
		ZMQ_LAST_ENDPOINT = 32
	}

	public enum Deprecated_Aliases
	{
		ZMQ_XREQ = 5,
		ZMQ_XREP
	}

	public enum Socket_Options
	{
		ZMQ_AFFINITY = 4,
		ZMQ_IDENTITY = 5,
		ZMQ_SUBSCRIBE = 6,
		ZMQ_UNSUBSCRIBE = 7,
		ZMQ_RATE = 8,
		ZMQ_RECOVERY_IVL = 9,
		ZMQ_SNDBUF = 11,
		ZMQ_RCVBUF = 12,
		ZMQ_RCVMORE = 13,
		ZMQ_FD = 14,
		ZMQ_EVENTS = 15,
		ZMQ_TYPE = 16,
		ZMQ_LINGER = 17,
		ZMQ_RECONNECT_IVL = 18,
		ZMQ_BACKLOG = 19,
		ZMQ_RECONNECT_IVL_MAX = 21,
		ZMQ_MAXMSGSIZE = 22,
		ZMQ_SNDHWM = 23,
		ZMQ_RCVHWM = 24,
		ZMQ_MULTICAST_HOPS = 25,
		ZMQ_RCVTIMEO = 27,
		ZMQ_SNDTIMEO = 28,
		ZMQ_IPV4ONLY = 31,
		ZMQ_LAST_ENDPOINT = 32,
		ZMQ_ROUTER_MANDATORY = 33,
		ZMQ_TCP_KEEPALIVE = 34,
		ZMQ_TCP_KEEPALIVE_CNT = 35,
		ZMQ_TCP_KEEPALIVE_IDLE = 36,
		ZMQ_TCP_KEEPALIVE_INTVL = 37,
		ZMQ_TCP_ACCEPT_FILTER = 38,
		ZMQ_DELAY_ATTACH_ON_CONNECT = 39,
		ZMQ_XPUB_VERBOSE = 40
	}

	public enum ENUM_HKR_MQ_MODE
	{
		HKR_MQ_MODE_PAIR_C,
		HKR_MQ_MODE_PAIR_S,
		HKR_MQ_MODE_PUB,
		HKR_MQ_MODE_SUB,
		HKR_MQ_MODE_REP,
		HKR_MQ_MODE_REQ,
		HKR_MQ_MODE_COUNT
	}

	public enum ENUM_HKR_OPT_TYPE
	{
		HKR_OPT_TYPE_ReadTimeOut,
		HKR_OPT_TYPE_WriteTimeOut,
		HKR_OPT_TYPE_LingerTime,
		HKR_OPT_TYPE_Subscribe,
		HKR_OPT_TYPE_Unsubscribe,
		HKR_OPT_TYPE_MAX_COUNT
	}

	public static IntPtr zmq_ctx_new()
	{
		return NativeMethods.zmq_ctx_new();
	}

	public static IntPtr zmq_socket(IntPtr pContext, int nType)
	{
		return NativeMethods.zmq_socket(pContext, nType);
	}

	public static int zmq_close(IntPtr pSocket)
	{
		return NativeMethods.zmq_close(pSocket);
	}

	public static int zmq_bind(IntPtr pSocket, string strAddress)
	{
		return NativeMethods.zmq_bind(pSocket, strAddress);
	}

	public static int zmq_connect(IntPtr pSocket, string strAddress)
	{
		return NativeMethods.zmq_connect(pSocket, strAddress);
	}

	public static int zmq_disconnect(IntPtr pSocket, string strAddress)
	{
		return NativeMethods.zmq_disconnect(pSocket, strAddress);
	}

	public static int zmq_setsockopt(IntPtr pSocket, int nOption, ref int nOptval, int nOptvallen)
	{
		return NativeMethods.zmq_setsockopt(pSocket, nOption, ref nOptval, nOptvallen);
	}

	public static int zmq_setsockopt(IntPtr pSocket, int nOption, IntPtr pOptval, int nOptvallen)
	{
		return NativeMethods.zmq_setsockopt(pSocket, nOption, pOptval, nOptvallen);
	}

	public static int zmq_getsockopt(IntPtr pSocket, int nOption, ref string strOptval)
	{
		int optvallen = 40;
		IntPtr intPtr = Marshal.AllocHGlobal(optvallen);
		int num = NativeMethods.zmq_getsockopt(pSocket, nOption, intPtr, ref optvallen);
		if (num != 0)
		{
			return num;
		}
		strOptval = Marshal.PtrToStringAnsi(intPtr);
		Marshal.FreeHGlobal(intPtr);
		return 0;
	}

	public static int zmq_send(IntPtr pSocket, [MarshalAs(UnmanagedType.LPStr)] string strMsg, uint nLenth, int nFlag)
	{
		return NativeMethods.zmq_send(pSocket, strMsg, nLenth, nFlag);
	}

	public static int zmq_recv(IntPtr pSocket, IntPtr pBuffer, uint nLength, int nFlag)
	{
		return NativeMethods.zmq_recv(pSocket, pBuffer, nLength, nFlag);
	}

	public static int zmq_ctx_term(IntPtr pSocket)
	{
		return NativeMethods.zmq_ctx_term(pSocket);
	}

	public static int zmq_ctx_shutdown(IntPtr pSocket)
	{
		return NativeMethods.zmq_ctx_shutdown(pSocket);
	}

	public static IntPtr HkrCreate(int nType, ref int nret)
	{
		IntPtr pSocketHandle = IntPtr.Zero;
		nret = NativeMethods.HKR_MQ_Create(ref pSocketHandle, nType);
		return pSocketHandle;
	}

	public static int HkrClose(IntPtr pSocket)
	{
		return NativeMethods.HKR_MQ_Close(pSocket);
	}

	public static int HkrBind(IntPtr pSocket, string strAddress)
	{
		return NativeMethods.HKR_MQ_Bind(pSocket, strAddress);
	}

	public static int HkrUnBind(IntPtr pSocket, string strAddress)
	{
		return NativeMethods.HKR_MQ_Unbind(pSocket, strAddress);
	}

	public static int HkrConnect(IntPtr pSocket, string strAddress)
	{
		return NativeMethods.HKR_MQ_Connect(pSocket, strAddress);
	}

	public static int HkrDisConnect(IntPtr pSocket, string strAddress)
	{
		return NativeMethods.HKR_MQ_Disconnect(pSocket, strAddress);
	}

	public static int HkrSetSocketOpt(IntPtr pSocket, int nOption, ref int nOptval, int nOptvallen)
	{
		return NativeMethods.HKR_MQ_SetSocketOpt(pSocket, nOption, ref nOptval, nOptvallen);
	}

	public static int HkrGetSocketOpt(IntPtr pSocket, int nOption, ref string nOptval)
	{
		int nParamLen = 40;
		IntPtr intPtr = Marshal.AllocHGlobal(nParamLen);
		int num = NativeMethods.HKR_MQ_GetSocketOpt(pSocket, nOption, intPtr, ref nParamLen);
		if (num != 0)
		{
			return num;
		}
		nOptval = Marshal.PtrToStringAnsi(intPtr);
		Marshal.FreeHGlobal(intPtr);
		return num;
	}

	public static int HkrSend(IntPtr pSocket, string msg, int nFlag)
	{
		if (string.IsNullOrEmpty(msg))
		{
			return -1;
		}
		byte[] bytes = Encoding.UTF8.GetBytes(msg);
		IntPtr intPtr = Marshal.AllocHGlobal(bytes.Length);
		Marshal.Copy(bytes, 0, intPtr, bytes.Length);
		int result = NativeMethods.HKR_MQ_Send(pSocket, intPtr, bytes.Length, nFlag);
		Marshal.FreeHGlobal(intPtr);
		return result;
	}

	public static int HkrReceive(IntPtr pSocket, ref IntPtr pBuffer, ref int nLength)
	{
		return NativeMethods.HKR_MQ_RecvEx(pSocket, ref pBuffer, ref nLength);
	}
}
