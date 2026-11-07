namespace Apps.XmlParser.ParamTab;

public class EditButton_Calib : Integer
{
	public OpenDialog EditDialog;

	public OpenDialog EditDialogA
	{
		get
		{
			return EditDialog;
		}
		set
		{
			EditDialog = value;
			OnPropertyChanged("EditDialogA");
		}
	}
}
