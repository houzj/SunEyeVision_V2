using System;
using Apps.ErrorCode;
using VM.Controls;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class ImgStitchInteger : Integer
{
	private const string MODEL_IMAGE_NUM_NAME = "ImageNum";

	private const string HARD_IMAGE_NUM_NAME = "HardImageNum";

	public override string DisplayType
	{
		get
		{
			return "Integer";
		}
		set
		{
			base.DisplayType = value;
		}
	}

	public override int SetParamUnit()
	{
		int result = base.SetParamUnit();
		if (CustomVisibleA)
		{
			UpdateImageList(isSetParam: true);
		}
		return result;
	}

	public override int GetParamUnit()
	{
		int paramUnit = base.GetParamUnit();
		if (CustomVisibleA)
		{
			UpdateImageList();
		}
		return paramUnit;
	}

	private void UpdateImageList(bool isSetParam = false)
	{
		try
		{
			if (!(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
			{
				return;
			}
			if (algorithmTabRoot.FindBaseNodeByName("ImageSourceList") is ImageSourceList imageSourceList)
			{
				if (NameA.Equals("HardImageNum"))
				{
					imageSourceList.IsHardStitch = true;
				}
				else
				{
					imageSourceList.IsHardStitch = false;
				}
				imageSourceList.ImageNumberA = CurValueA;
			}
			if (isSetParam)
			{
				string nameA = NameA;
				if (algorithmTabRoot.FindBaseNodeByName("ImageMatrix") is NumberMultiply numberMultiply && numberMultiply.Product != CurValueA && nameA.Equals("ImageNum"))
				{
					numberMultiply.needUpdateImageNumber = false;
					numberMultiply.LeftValueA = 1;
					numberMultiply.RightValueA = 1;
					numberMultiply.needUpdateImageNumber = true;
				}
			}
		}
		catch (Exception ex)
		{
			string strMsg = ex.Message;
			if (ex is VmException ex2)
			{
				strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
			}
			HikMessageBox.Show(strMsg, MessageIcons.Error);
		}
	}
}
