using Apps.BaseData;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ItemInterface;

public interface IROISelecter
{
	int CurImageWidth { get; set; }

	int CurImageHeight { get; set; }

	bool IsStateForDrawROI { get; set; }

	ObservableRangeCollection<IROI> ROIList { get; }

	IROI CurROIData { get; set; }

	bool CanUpdateROI { get; set; }

	void SelectNone();

	void DoRoiSelect(IROI roi);

	void DoRoiDeleted(IROI roi);

	void DoRoiPaste(IROI roi);

	void UpdateROI();

	bool CanDeleted(IROI roi);

	bool CanCopy(IROI roi);
}
