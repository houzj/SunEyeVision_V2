using System.Collections.ObjectModel;

namespace IMVSColorRecognitionModuControl;

public interface IItemsData
{
	ObservableCollection<object> ItemsData { get; set; }
}
