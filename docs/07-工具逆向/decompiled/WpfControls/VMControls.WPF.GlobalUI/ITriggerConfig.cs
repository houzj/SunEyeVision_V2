using System.Collections.ObjectModel;

namespace VMControls.WPF.GlobalUI;

public interface ITriggerConfig
{
	ObservableCollection<object> CommEventTriggerA { get; set; }

	ObservableCollection<object> CommStrTriggerA { get; set; }

	bool IsLoading { get; }

	void RemoveItem(object item);
}
