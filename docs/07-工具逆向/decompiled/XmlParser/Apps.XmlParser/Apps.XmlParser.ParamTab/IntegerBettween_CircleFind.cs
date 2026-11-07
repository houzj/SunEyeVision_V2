using System.Linq;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class IntegerBettween_CircleFind : IntegerBettween, INeedGroupRunParamSubstitute
{
	private object _ROISourceData;

	public object ROISourceData
	{
		get
		{
			if (_ROISourceData == null)
			{
				AlgorithmTabRoot algorithmTabRoot = GetParentNode<AlgorithmTabRoot>();
				if (algorithmTabRoot != null)
				{
					_ROISourceData = algorithmTabRoot.FindBaseNodeByName("RoiType");
				}
			}
			return _ROISourceData;
		}
	}

	public bool SourceTag
	{
		set
		{
			if (value)
			{
				GetParamUnit();
			}
		}
	}

	object INeedGroupRunParamSubstitute.GetGroupRunParamSubstitute(object tag)
	{
		IntegerBettween integerBettween = CloneAsBase(typeof(IntegerBettween)) as IntegerBettween;
		foreach (object item in integerBettween.SubChildren.ToList())
		{
			if (item is INeedGroupRunParamSubstitute needGroupRunParamSubstitute)
			{
				object groupRunParamSubstitute = needGroupRunParamSubstitute.GetGroupRunParamSubstitute(tag);
				int index = integerBettween.SubChildren.IndexOf(item);
				integerBettween.SubChildren.Remove(item);
				integerBettween.SubChildren.Insert(index, groupRunParamSubstitute);
				(groupRunParamSubstitute as NameValueItem).SetParentNode(integerBettween);
			}
		}
		return integerBettween;
	}
}
