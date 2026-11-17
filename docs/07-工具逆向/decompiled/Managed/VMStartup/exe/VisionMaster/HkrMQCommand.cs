using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Apps.Log;
using Hik.Util;
using VM.Utility;

namespace VisionMaster;

public class HkrMQCommand : BaseZmqCommand
{
	private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();

	private bool _dispose;

	private IntPtr receiveBufferPtr = IntPtr.Zero;

	public HkrMQCommand()
	{
		int nret = -1;
		socket = Libzmq.HkrCreate(4, ref nret);
		int nOptval = 500;
		if (Libzmq.HkrSetSocketOpt(socket, 0, ref nOptval, 4) != 0)
		{
			LogHelper.objLog.Error("二次开发模式启动:MQ超时设置失败！");
			connectSuccess = false;
			return;
		}
		string text = "tcp://127.0.0.1:" + FindNextAvailableTCPPort(6000);
		LogHelper.Info("二次开发模式启动:获取空闲端口地址:" + text);
		if (Libzmq.HkrBind(socket, text) != 0)
		{
			LogHelper.objLog.Error("二次开发模式启动:MQ绑定失败," + text);
			connectSuccess = false;
		}
		else
		{
			localAddress = text;
			connectSuccess = true;
		}
	}

	public async Task CallReceived()
	{
		while (!_dispose)
		{
			int bytesReceived = 0;
			int nRet = Libzmq.HkrReceive(socket, ref receiveBufferPtr, ref bytesReceived);
			ZmqResult result = new ZmqResult();
			if (bytesReceived > 0 && nRet == 0)
			{
				byte[] recvData = new byte[bytesReceived];
				Marshal.Copy(receiveBufferPtr, recvData, 0, bytesReceived);
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
			await Task.Delay(TimeSpan.FromMilliseconds(30.0), _tokenSource.Token);
		}
	}

	public override int Connect(string address)
	{
		return Libzmq.HkrConnect(socket, address);
	}

	public override void SendMessage(string msg)
	{
		Libzmq.HkrSend(socket, msg, 0);
	}

	~HkrMQCommand()
	{
		Dispose(dispose: false);
	}

	private void Dispose(bool dispose)
	{
		if (!_dispose)
		{
			_dispose = true;
			if (dispose)
			{
				Libzmq.HkrClose(socket);
				this.RemoveEvents();
			}
		}
	}

	public override void Dispose()
	{
		Dispose(dispose: true);
		GC.SuppressFinalize(this);
	}

	private int FindNextAvailableTCPPort(int startPort)
	{
		int num = startPort;
		bool flag = true;
		try
		{
			IPGlobalProperties iPGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
			IPEndPoint[] activeTcpListeners = iPGlobalProperties.GetActiveTcpListeners();
			do
			{
				if (!flag)
				{
					num++;
					flag = true;
				}
				IPEndPoint[] array = activeTcpListeners;
				foreach (IPEndPoint iPEndPoint in array)
				{
					if (iPEndPoint.Port == num)
					{
						flag = false;
						break;
					}
				}
			}
			while (!flag && num < 65535);
			if (!flag)
			{
				throw new ApplicationException("Not able to find a free TCP port.");
			}
			return num;
		}
		catch
		{
			return -1;
		}
	}
}
