using System.Collections.ObjectModel;

namespace Apps.XmlParser.ParamTab;

public class ImgStitchDynamicIO : DynamicIO
{
	private EnumerationGEx _CombinationSelecter;

	public EnumerationGEx CombinationSelecter
	{
		get
		{
			if (_CombinationSelecter == null)
			{
				_CombinationSelecter = new EnumerationGEx();
				int num = GetParentNode<IMVSImgStitchCalibModuDynamicIO>().ItemsA.IndexOf(this);
				_CombinationSelecter.IndexA = num.ToString();
				_CombinationSelecter.DisplayNameA = "InImage";
				_CombinationSelecter.Triggers = new ObservableCollection<object>();
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
				_CombinationSelecter.Triggers.Add(trigger);
				trigger.SetParentNode(_CombinationSelecter);
				_CombinationSelecter.Initers = new ObservableCollection<object>();
				Setter setter2 = new Setter
				{
					TargetName = "EnumEntrys",
					OperationName = "GetFrontParamItemsOperation",
					OperationParams = "IMAGE"
				};
				setter2.CreatOperateInstance();
				_CombinationSelecter.Initers.Add(setter2);
				setter2.SetParentNode(_CombinationSelecter);
				Setter setter3 = new Setter
				{
					TargetName = "CurValue",
					OperationName = "GetSelectedCombinationOperation",
					OperationParams = NameA
				};
				setter3.CreatOperateInstance();
				_CombinationSelecter.Initers.Add(setter3);
				setter3.SetParentNode(_CombinationSelecter);
				_CombinationSelecter.SetParentNode(this);
			}
			_CombinationSelecter.InniterBusiness();
			return _CombinationSelecter;
		}
	}
}
