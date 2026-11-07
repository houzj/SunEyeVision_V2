using System;
using System.Collections.ObjectModel;
using Apps.Interface;
using VM.Core;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class SpecRefOutputTypeHiddenOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public static ObservableCollection<object> diffRefOutputTypeEnumEntrys = new ObservableCollection<object>();

	public static ObservableCollection<object> specRefOutputTypeEnumEntrys = new ObservableCollection<object>();

	public static int diffRefOutputTypeOldCurvalue = -1;

	public static int specRefOutputTypeOldCurvalue = -1;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SpecRefOutputTypeHiddenOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
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
		try
		{
			UniformGridCheck uniformGridCheck = Executor.TargetObj as UniformGridCheck;
			if ((parentNode as Enumeration).CurValueA == 1)
			{
				if (diffRefOutputTypeEnumEntrys.Count == 0)
				{
					for (int i = 2; i < uniformGridCheck.EnumEntrysA.Count; i++)
					{
						diffRefOutputTypeEnumEntrys.Add(uniformGridCheck.EnumEntrysA[i]);
					}
				}
				if (uniformGridCheck.EnumEntrysA.Count == 8)
				{
					for (int num = uniformGridCheck.EnumEntrysA.Count - 1; num >= 2; num--)
					{
						uniformGridCheck.EnumEntrysA.RemoveAt(num);
					}
				}
				else
				{
					uniformGridCheck.CurValueA = false;
					uniformGridCheck.EnumEntrysA.Clear();
				}
				if (uniformGridCheck.EnumEntrysA.Count == 0)
				{
					for (int j = 0; j < specRefOutputTypeEnumEntrys.Count; j++)
					{
						uniformGridCheck.EnumEntrysA.Insert(j, specRefOutputTypeEnumEntrys[j]);
					}
				}
				uniformGridCheck.CurValueA = true;
				if (owner != null && owner.Owner != null)
				{
					VmModule vmModule = owner.Owner as VmModule;
					((IUserStringData)vmModule.Params)[uniformGridCheck.ComName] = "true";
				}
			}
			else
			{
				if ((parentNode as Enumeration).CurValueA != 2)
				{
					return;
				}
				if (specRefOutputTypeEnumEntrys.Count == 0)
				{
					for (int k = 0; k < 2; k++)
					{
						specRefOutputTypeEnumEntrys.Add(uniformGridCheck.EnumEntrysA[k]);
					}
				}
				if (uniformGridCheck.EnumEntrysA.Count == 2)
				{
					uniformGridCheck.CurValueA = false;
					uniformGridCheck.EnumEntrysA.Clear();
				}
				if (uniformGridCheck.EnumEntrysA.Count == 0)
				{
					for (int l = 0; l < diffRefOutputTypeEnumEntrys.Count; l++)
					{
						uniformGridCheck.EnumEntrysA.Add(diffRefOutputTypeEnumEntrys[l]);
					}
				}
				uniformGridCheck.CurValueA = true;
				if (owner != null && owner.Owner != null)
				{
					VmModule vmModule2 = owner.Owner as VmModule;
					((IUserStringData)vmModule2.Params)[uniformGridCheck.ComName] = "true";
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
