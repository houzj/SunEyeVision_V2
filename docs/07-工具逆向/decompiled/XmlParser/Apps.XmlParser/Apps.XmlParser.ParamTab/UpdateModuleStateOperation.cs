using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class UpdateModuleStateOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public UpdateModuleStateOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		try
		{
			NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
			AlgorithmTabRoot root = parentNode.GetTopBaseNode() as AlgorithmTabRoot;
			VmModule module = root.Owner as VmModule;
			IVmModule vmModule;
			if ((vmModule = module) != null)
			{
				vmModule.InitDynamicIO();
			}
			Task.Run(() =>
			{
				Thread.Sleep(500);
				if (module != null)
				{
					string text = "";
					try
					{
						text = module.Params["ImageSourceType"];
					}
					catch (Exception)
					{
					}
					byte[] array = module.UserData["CheckItemParamReady"];
					string text2 = null;
					if (array != null)
					{
						text2 = StringUtil.UTF8GetString(array);
					}
					IEnumerable<NameValueItem> enumerable = root?.GetModuleSelector();
					if (enumerable != null)
					{
						foreach (NameValueItem item in enumerable)
						{
							item.GetParamUnit();
							if (item.Value == "-1" && text2 != "false" && item.CustomVisibleA && (string.IsNullOrEmpty(text) || text == "2"))
							{
								module.UserData["CheckItemParamReady"] = StringUtil.UTF8GetBytes("false");
							}
							else if ((item.Value != "-1" && text2 != "true") || (!item.CustomVisibleA && text2 != "true"))
							{
								module.UserData["CheckItemParamReady"] = StringUtil.UTF8GetBytes("true");
							}
						}
					}
					module.ReFlashReadyState();
				}
			});
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message);
		}
	}

	private void ChangeLink()
	{
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
