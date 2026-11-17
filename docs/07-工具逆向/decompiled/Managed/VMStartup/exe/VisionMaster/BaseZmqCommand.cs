using System;
using System.Threading.Tasks;

namespace VisionMaster;

public class BaseZmqCommand : IDisposable
{
	public OnZmqReceived OnZmqReceived = null;

	public IntPtr socket;

	public Task zmqTask;

	public bool connectSuccess { get; set; }

	public string localAddress { get; set; }

	public virtual void SendMessage(string msg)
	{
		throw new NotImplementedException();
	}

	public virtual int Connect(string address)
	{
		return 0;
	}

	public virtual void Dispose()
	{
		throw new NotImplementedException();
	}

	public void SendExcute(int cmd, int flag, int errorCode, string strArg = null)
	{
		string text = "";
		text = ((strArg != null) ? (cmd + "*" + flag + "*" + errorCode + "*" + strArg) : (cmd + "*" + flag + "*" + errorCode));
		SendMessage(text);
	}

	public void SendExcute(int cmd, int flag, string msg)
	{
		string msg2 = cmd + "*" + flag + "*" + msg;
		SendMessage(msg2);
	}
}
