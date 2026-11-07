namespace Apps.XmlParser.ParamTab;

public class OpenFileDialog4ReadFile : OpenFileDialog
{
	protected override void ValueChangedCmdExecute()
	{
		base.ValueChangedCmdExecute();
		ReadData();
	}

	private void ReadData()
	{
		if ((GetTopBaseNode() as AlgorithmTabRoot).FindBaseNodeByName("CalibInputParam") is ReadFileConfigData readFileConfigData)
		{
			readFileConfigData.ReadData();
		}
	}
}
