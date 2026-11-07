using System;
using Apps.Localization;

namespace Apps.XmlParser.ParamTab;

public class UpdateImageSourceListOperation : IOperation
{
	private const string MODEL_IMAGE_NUM_NAME = "ImageNum";

	private const string HARD_IMAGE_NUM_NAME = "HardImageNum";

	public string TargetName = "";

	private bool _Working = false;

	public Setter Executor { get; set; }

	public bool Working => _Working;

	public UpdateImageSourceListOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode == null)
		{
			return;
		}
		if (parentNode.NameA != TargetName)
		{
			if (Params.Length <= 1)
			{
				throw new Exception(AppLocalizationService.GetLang(parentNode.Name) + AppLocalizationService.GetLang("缺少参数") + " :" + AppLocalizationService.GetLang(Executor.OperationName));
			}
			if (!parentNode.CustomVisibleA)
			{
				return;
			}
			int num = 0;
			if (Params[1] is bool)
			{
				if (parentNode is Integer integer)
				{
					num = integer.CurValueA;
				}
			}
			else
			{
				num = (int)Params[1];
			}
			if (!(parentNode.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
			{
				return;
			}
			if (algorithmTabRoot.FindBaseNodeByName("ImageSourceList") is ImageSourceList imageSourceList)
			{
				try
				{
					if (parentNode.NameA.Equals("HardImageNum"))
					{
						imageSourceList.IsHardStitch = true;
					}
					else
					{
						imageSourceList.IsHardStitch = false;
					}
					imageSourceList.ImageNumberA = num;
				}
				catch (Exception ex)
				{
					throw new Exception(ex.Message);
				}
			}
			string nameA = parentNode.NameA;
			if (algorithmTabRoot.FindBaseNodeByName("ImageMatrix") is NumberMultiply numberMultiply && numberMultiply.Product != num && nameA.Equals("ImageNum"))
			{
				try
				{
					numberMultiply.LeftValueA = 1;
					numberMultiply.RightValueA = 1;
				}
				catch (Exception ex2)
				{
					throw new Exception(ex2.Message);
				}
			}
		}
		else
		{
			(parentNode as ImageSourceList)?.UpdateImageList();
		}
	}

	public void ReOperate()
	{
	}
}
