using Apps.Data;
using VMControls.Interface;

namespace FeatureMatchCommonControl;

internal class FeaturepatMatchForGroupDecorate : NotifyPropertyChangedBase, IDecorate
{
	private object _DecorateSource;

	public string[] SupportDisplayForGroupData { get; } = new string[1] { "FeaturepatMatch_GroupControl" };

	public string DisplayType => SupportDisplayForGroupData[0];

	public object DecorateSource
	{
		get
		{
			return _DecorateSource;
		}
		set
		{
			if (value != _DecorateSource)
			{
				_DecorateSource = value;
			}
		}
	}

	object IDecorate.DecorateSource
	{
		get
		{
			return DecorateSource;
		}
		set
		{
			DecorateSource = value;
		}
	}
}
