using System;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class GetImageROIOffsetEnable : IOperation
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

	private bool ImageROIOffsetEnable { get; set; }

	public GetImageROIOffsetEnable(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
		if (bool.TryParse(ConfigurationHelper.GetConfig("ImageROIOffsetEnable"), out var result))
		{
			ImageROIOffsetEnable = result;
		}
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		BooleanConfirm parentNode = Executor.GetParentNode<BooleanConfirm>();
		if (parentNode != null)
		{
			parentNode.CurValueA = ImageROIOffsetEnable;
			parentNode.OnCurValueChanged -= ParentItem_OnCurValueChanged;
			parentNode.OnCurValueChanged += ParentItem_OnCurValueChanged;
		}
	}

	private void ParentItem_OnCurValueChanged(object sender, EventArgs e)
	{
		if (sender is BooleanConfirm booleanConfirm)
		{
			ImageROIOffsetEnable = booleanConfirm.CurValueA;
			ConfigurationHelper.SaveConfig("ImageROIOffsetEnable", ImageROIOffsetEnable.ToString());
		}
	}

	public void ReOperate()
	{
	}

	public bool ModifyReStart()
	{
		throw new NotImplementedException();
	}
}
