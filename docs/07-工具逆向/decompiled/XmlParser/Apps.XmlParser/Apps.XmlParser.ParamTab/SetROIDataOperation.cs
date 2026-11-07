using System;
using System.Windows;
using Apps.Data;
using Apps.Interface;
using Hik.Xml;
using VM.Core;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class SetROIDataOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public EnumEntry firstItem = new EnumEntry();

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetROIDataOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		try
		{
			NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
			IOwner owner = null;
			if (parentNode != null)
			{
				owner = parentNode.GetTopBaseNode() as AlgorithmTabRoot;
			}
			if (Executor == null || Executor.TargetObj == null)
			{
				return;
			}
			Integer integer = Params[0] as Integer;
			int curValueA = integer.CurValueA;
			if (owner == null || owner.Owner == null)
			{
				return;
			}
			VmModule vmModule = owner.Owner as VmModule;
			IData data;
			if ((data = vmModule.Params) == null || !(data is ModuleParam param))
			{
				return;
			}
			AlgorithmTabRoot paramRoot = param.GetParamRoot();
			foreach (object item in paramRoot.TabsA)
			{
				BaseNode baseNode = (item as Tab).FindBaseNodeByName("RoiType");
				if (baseNode != null)
				{
					for (int i = 0; i < curValueA; i++)
					{
						NormalizedRectROIModel normalizedRectROIModel = new NormalizedRectROIModel();
						normalizedRectROIModel.Width = 0.2;
						normalizedRectROIModel.Height = 0.5;
						normalizedRectROIModel.CenterPoint = new Point(0.2, 0.2);
						normalizedRectROIModel.Angle = 0.0;
						(baseNode as ROISelecter_Fix).InitROI(normalizedRectROIModel);
						(baseNode as ROISelecter).NormalizeROIList.Add(normalizedRectROIModel);
					}
				}
			}
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
