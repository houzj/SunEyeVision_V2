using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Apps.Localization;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetLinkedModulesOperation : IOperation
{
	public static string strChoice = AppLocalizationService.GetLang("Lang_PlzChoose");

	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetLinkedModulesOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		try
		{
			ObservableCollection<object> observableCollection = new ObservableCollection<object>();
			string text = "";
			if (ExtraParams.Length != 0)
			{
				text = ExtraParams[0];
			}
			NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
			if (parentNode == null || !(parentNode.GetTopBaseNode() is AlgorithmTabRoot { Owner: not null } algorithmTabRoot))
			{
				return;
			}
			if (parentNode is Enumeration_TimeStatistic && (parentNode as Enumeration_TimeStatistic).CurValueA > 0)
			{
				(parentNode as Enumeration_TimeStatistic).GetIndex();
			}
			VmModule vmModule = algorithmTabRoot.Owner as VmModule;
			List<VmModule> frontParamItems = vmModule.GetFrontParamItems();
			foreach (VmModule item2 in frontParamItems)
			{
				EnumEntry enumEntry = new EnumEntry();
				if (!(item2.Parent is VmSolution) && item2.Parent == vmModule.Parent && item2.StrModuleName != vmModule.StrModuleName)
				{
					enumEntry.DisplayNameA = "%" + item2.ID + item2.Name + "%";
					enumEntry.DescriptionA = "%" + item2.ID + item2.Name + "%";
					enumEntry.ValueA = (int)(item2.ID + 1);
					observableCollection.Add(enumEntry);
				}
			}
			bool flag = true;
			int num = observableCollection.Count - 1;
			while (num > 0 && flag)
			{
				flag = false;
				for (int i = 0; i < num; i++)
				{
					if ((observableCollection[i] as EnumEntry).ValueA > (observableCollection[i + 1] as EnumEntry).ValueA)
					{
						EnumEntry enumEntry2 = new EnumEntry();
						enumEntry2 = observableCollection[i + 1] as EnumEntry;
						observableCollection[i + 1] = observableCollection[i];
						observableCollection[i] = enumEntry2;
						flag = true;
					}
				}
				num--;
			}
			if (observableCollection.Count > 0)
			{
				EnumEntry item = new EnumEntry
				{
					DisplayNameA = strChoice,
					DescriptionA = strChoice,
					ValueA = 0
				};
				observableCollection.Insert(0, item);
			}
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", observableCollection);
			Enumeration_TimeStatistic enumeration_TimeStatistic = parentNode as Enumeration_TimeStatistic;
			enumeration_TimeStatistic.GetSelectParam();
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
