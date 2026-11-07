using System.Collections.ObjectModel;
using System.Linq;

namespace Apps.XmlParser.ParamTab;

public class ProcedureCommDynamicIO : DynamicIO
{
	public override string FilterTypeA => "int;float;string";

	public override ButtonSelecter FilterSelecter
	{
		get
		{
			if (_FilterSelecter == null)
			{
				_FilterSelecter = new ButtonSelecter();
				if (CombineType.Contains(ValueTypeA))
				{
					_FilterSelecter.DropOpenSetter = new Setter
					{
						TargetName = "EnumEntrys",
						OperationName = "GetFrontCombinationTreeOperation"
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
						OperationName = "SetCombinationSourceOperation",
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
						OperationName = "GetSelectedCombinationOperation",
						OperationParams = NameA
					};
					setter2.CreatOperateInstance();
					_FilterSelecter.Initers.Add(setter2);
					setter2.SetParentNode(_FilterSelecter);
				}
				else
				{
					_FilterSelecter.DropOpenSetter = new Setter
					{
						TargetName = "EnumEntrys",
						OperationName = "GetFrontAndSelfFilterTreeOperation"
					};
					_FilterSelecter.DropOpenSetter.SetParentNode(_FilterSelecter);
					_FilterSelecter.SetParentNode(this);
					_FilterSelecter.Triggers = new ObservableCollection<object>();
					Trigger trigger2 = new Trigger
					{
						Property = "CurValue"
					};
					Setter setter3 = new Setter
					{
						OperationName = "SetFilterSourceOperation",
						OperationParams = NameA
					};
					setter3.CreatOperateInstance();
					trigger2.Setters = new ObservableCollection<object>();
					trigger2.Setters.Add(setter3);
					setter3.SetParentNode(trigger2);
					_FilterSelecter.Triggers.Add(trigger2);
					trigger2.SetParentNode(_FilterSelecter);
					_FilterSelecter.Initers = new ObservableCollection<object>();
					Setter setter4 = new Setter
					{
						TargetName = "CurValue",
						OperationName = "GetSelectedFilterOperation",
						OperationParams = NameA
					};
					setter4.CreatOperateInstance();
					_FilterSelecter.Initers.Add(setter4);
					setter4.SetParentNode(_FilterSelecter);
				}
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
