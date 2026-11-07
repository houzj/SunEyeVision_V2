using System.Windows;

namespace Apps.XmlParser.ParamTab;

public class HPBottomCommand : Command
{
	protected virtual string ItemName => "HPMatchPat";

	public void SaveUIData()
	{
		try
		{
			FeaturepatMatchConfigPanalData featurepatMatchConfigPanalData = null;
			if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				featurepatMatchConfigPanalData = algorithmTabRoot?.FindBaseNodeByName(ItemName) as FeaturepatMatchConfigPanalData;
			}
		}
		catch
		{
		}
		finally
		{
			SystemCommands.CloseWindowCommand.Execute(null, null);
		}
	}
}
