using System;
using VM.WatchDog;

namespace Apps.XmlParser.ParamTab;

public class EnableWatchDogOperation : IOperation
{
	public string TargetName = "";

	public Setter Executor { get; set; }

	public bool Working
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public EnableWatchDogOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode != null)
		{
			bool flag = Convert.ToBoolean(parentNode.Value);
			if (flag && !WatchDogService.Instance.IsRunning)
			{
				WatchDogService.Instance.Register(MonitorOperator.UIStuckMonitor, new RecordOperator[1]);
				WatchDogService.Instance.Start();
			}
			else if (!flag && WatchDogService.Instance.IsRunning)
			{
				WatchDogService.Instance.Stop();
			}
		}
	}

	public void ReOperate()
	{
	}
}
