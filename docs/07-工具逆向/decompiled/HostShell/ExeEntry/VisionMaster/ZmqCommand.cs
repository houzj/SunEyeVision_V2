using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Apps.Log;
using Hik.Util;
using VM.Utility;

namespace VisionMaster;

public class ZmqCommand : BaseZmqCommand
{
	private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();

	private bool _dispose;

	private readonly IntPtr context;

	internal const int CMD_STARTPLATFORM = 180092928;

	internal const int CMD_CLOSEPLATFORM = 180092929;

	internal const int CMD_SHOWPLATFORM = 180092930;

	internal const int CMD_SHOWONEMODULE = 180092931;

	internal const int CMD_STOPRUN = 180092932;

	internal const int CMD_RUNONCE = 180092933;

	internal const int CMD_CONTINOUSRUNSTART = 180092934;

	internal const int CMD_CONTINOUSRUNSTOP = 180092935;

	internal const int CMD_SETCONTINOUSRUNINTERVAL = 180092936;

	internal const int CMD_SAVEPROJECT = 180092937;

	internal const int CMD_LOADPROJECT = 180092938;

	internal const int CMD_GETLOADPROGRESS = 180092939;

	internal const int CMD_STARTRUN = 180092940;

	internal const int CMD_GETPARAMVALUE = 180092941;

	internal const int CMD_SETPARAMVALUE = 180092942;

	internal const int CMD_SETFRONTENDEMBED = 180092935;

	internal const int CMD_CLOSEFRONTED = 180092936;

	internal const int CMD_SHOWSAVESOLUTIONNAME = 180092932;

	internal const int CMD_SHOWLOADSOLUTIONNAME = 180092933;

	internal const int CMD_CLOSESOLUTION = 180092934;

	internal const int CMD_GETPLATFORMSHOWSTATUS = 180092937;

	internal const int CMD_ATTACHMULTIFRONTWND = 180092944;

	internal const int CMD_SCREENFRONTWNDPIC = 180092945;

	public ZmqCommand()
	{
		context = Libzmq.zmq_ctx_new();
		socket = Libzmq.zmq_socket(context, 4);
		int nOptval = 500;
		if (Libzmq.zmq_setsockopt(socket, 27, ref nOptval, 4) != 0)
		{
			LogHelper.Error("二次开发模式启动:MQ超时设置失败！");
			connectSuccess = false;
			return;
		}
		int nOptval2 = 0;
		if (Libzmq.zmq_setsockopt(socket, 17, ref nOptval2, 4) != 0)
		{
			LogHelper.Error("二次开发模式启动:Linger设置失败！");
			connectSuccess = false;
			return;
		}
		if (Libzmq.zmq_bind(socket, "tcp://127.0.0.1:*") != 0)
		{
			LogHelper.Error("二次开发模式启动:MQ绑定失败");
			connectSuccess = false;
			return;
		}
		string strOptval = "";
		if (Libzmq.zmq_getsockopt(socket, 32, ref strOptval) != 0)
		{
			LogHelper.Error("二次开发模式启动:MQ获取socket信息失败:");
			connectSuccess = false;
			return;
		}
		localAddress = strOptval;
		zmqTask = Task.Run(async () =>
		{
			await CallReceived();
		});
		connectSuccess = true;
	}

	public override int Connect(string address)
	{
		return Libzmq.zmq_connect(socket, address);
	}

	public async Task CallReceived()
	{
		while (!_dispose)
		{
			IntPtr bufferPtr = Marshal.AllocHGlobal(1024);
			int size = Libzmq.zmq_recv(socket, bufferPtr, 1024u, 0);
			ZmqResult result = new ZmqResult();
			if (size > 0)
			{
				byte[] recvData = new byte[size];
				Marshal.Copy(bufferPtr, recvData, 0, size);
				result.Ok = true;
				string recvStrData = StringUtil.UTF8GetString(recvData);
				if (recvStrData != null)
				{
					result.Command = recvStrData.Split('*');
				}
			}
			if (OnZmqReceived != null)
			{
				OnZmqReceived(result);
			}
			Marshal.FreeHGlobal(bufferPtr);
			await Task.Delay(TimeSpan.FromMilliseconds(30.0), _tokenSource.Token);
		}
	}

	~ZmqCommand()
	{
		Dispose(dispose: false);
	}

	public override void SendMessage(string msg)
	{
		Libzmq.zmq_send(socket, msg, (uint)(Encoding.Default.GetBytes(msg).Length + 1), 0);
	}

	private void Dispose(bool dispose)
	{
		if (!_dispose)
		{
			_dispose = true;
			if (dispose)
			{
				Libzmq.zmq_close(socket);
				Libzmq.zmq_ctx_shutdown(context);
				this.RemoveEvents();
			}
		}
	}

	public override void Dispose()
	{
		Dispose(dispose: true);
		GC.SuppressFinalize(this);
	}
}
