namespace Apps.XmlParser.ParamTab;

public interface IConfirm
{
	string MessageText { get; set; }

	void ConfirmData();

	bool ModifyReStart();
}
