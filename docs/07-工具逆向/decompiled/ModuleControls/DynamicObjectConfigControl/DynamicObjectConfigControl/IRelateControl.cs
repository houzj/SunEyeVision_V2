namespace DynamicObjectConfigControl;

public interface IRelateControl
{
	void ShowProgressDialog(string msg);

	void CloseProgressDialog();

	void SetProgressValue(int value);
}
