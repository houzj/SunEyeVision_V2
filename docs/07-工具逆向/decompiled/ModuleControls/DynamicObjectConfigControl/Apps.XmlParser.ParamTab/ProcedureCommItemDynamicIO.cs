using System.Collections.ObjectModel;
using System.Linq;

namespace Apps.XmlParser.ParamTab;

public class ProcedureCommItemDynamicIO : DynamicIO
{
	public override string FilterTypeA => "int;float;string";

	public override ButtonSelecter FilterSelecter
	{
		get
		{
			if (_FilterSelecter == null)
			{
				_FilterSelecter = new ButtonSelecter();
				_FilterSelecter.DropOpenSetter = new Setter
				{
					TargetName = "EnumEntrys",
					OperationName = "GetFrontAndSelfFilterTreeWithVarOperation"
				};
				_FilterSelecter.DropOpenSetter.SetParentNode(_FilterSelecter);
				_FilterSelecter.SetParentNode(this);
				_FilterSelecter.Triggers = new ObservableCollection<object>();
				Trigger trigger = new Trigger
				{
					Property = "CurValue"
				};
				Setter setter = new Setter
				{
					OperationName = "SetFilterProcedureCommSourceOperation",
					OperationParams = NameA
				};
				setter.CreatOperateInstance();
				trigger.Setters = new ObservableCollection<object>();
				trigger.Setters.Add(setter);
				setter.SetParentNode(trigger);
				_FilterSelecter.Triggers.Add(trigger);
				trigger.SetParentNode(_FilterSelecter);
				_FilterSelecter.Initers = new ObservableCollection<object>();
				Setter setter2 = new Setter
				{
					TargetName = "CurValue",
					OperationName = "GetSelectedFilterProcedureCommOperation",
					OperationParams = NameA
				};
				setter2.CreatOperateInstance();
				_FilterSelecter.Initers.Add(setter2);
				setter2.SetParentNode(_FilterSelecter);
			}
			if (_FilterSelecter.DropOpenSetter.OperationParams != null && _FilterSelecter.DropOpenSetter.OperationParams != FilterTypeA)
			{
				_FilterSelecter.CurValueA = "";
			}
			_FilterSelecter.Triggers.OfType<Trigger>().First().Setters.OfType<Setter>().First().OperationParams = NameA;
			_FilterSelecter.Initers.OfType<Setter>().First().OperationParams = NameA;
			_FilterSelecter.InniterBusiness();
			_FilterSelecter.DropOpenSetter.OperationParams = FilterTypeA;
			return _FilterSelecter;
		}
	}
}
