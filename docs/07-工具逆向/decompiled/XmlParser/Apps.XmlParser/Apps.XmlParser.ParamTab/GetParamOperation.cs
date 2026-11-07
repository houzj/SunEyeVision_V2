using System.Threading;
using HikPcUI.Commom;

namespace Apps.XmlParser.ParamTab;

public class GetParamOperation : IOperation
{
	public string TargetName = "";

	private object TargetObj = null;

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetParamOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (Params == null)
		{
			return;
		}
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		AlgorithmTabRoot algorithmTabRoot = parentNode.GetTopBaseNode() as AlgorithmTabRoot;
		if (algorithmTabRoot.IsOnInit || Params.Length <= 1)
		{
			return;
		}
		if (TargetObj == null)
		{
			GetTargetObj();
		}
		if (!(TargetObj is NameValueItem))
		{
			return;
		}
		Thread thread = new Thread(() =>
		{
			Thread.Sleep(500);
			CurrentUIHelper.Instance.Dispatcher.Invoke(() =>
			{
				(TargetObj as NameValueItem).GetParamUnit();
			});
		})
		{
			IsBackground = true
		};
		thread.Start();
	}

	private void GetTargetObj()
	{
		if (!string.IsNullOrEmpty(TargetName) && Executor.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			TargetObj = algorithmTabRoot.FindBaseNodeByName(TargetName);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
