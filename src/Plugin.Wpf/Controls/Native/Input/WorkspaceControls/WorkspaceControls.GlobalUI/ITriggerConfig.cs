using System.Collections.ObjectModel;

namespace SunEyeVision.Plugin.Wpf.Native.Input.WorkspaceControls.GlobalUI;

public interface ITriggerConfig
{
	ObservableCollection<object> CommEventTriggerA { get; set; }

	ObservableCollection<object> CommStrTriggerA { get; set; }

	bool IsLoading { get; }

	void RemoveItem(object item);
}
