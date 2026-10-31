using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Adapters.Resources;

    // Default DisplayMember templates generate TextBlocks. Restore the native inherited
    // foreground/font if a host implicit TextBlock style shadows them, including in the Popup.
internal sealed class NativeEnumerationTextScope : IDisposable
    {
        private readonly ComboBox _combo;
        private DispatcherOperation? _pending;
        private long _generation;
        private bool _disposed;
        public NativeEnumerationTextScope(ComboBox combo)
        {
            _combo=combo;
            combo.Loaded+=OnLoaded;combo.Unloaded+=OnUnloaded;
            combo.SelectionChanged+=OnSelectionChanged;combo.DropDownOpened+=OnDropDownOpened;
        }
        private void OnLoaded(object sender,RoutedEventArgs args)=>Queue();
        private void OnSelectionChanged(object sender,SelectionChangedEventArgs args)=>Queue();
        private void OnDropDownOpened(object? sender,EventArgs args)=>Queue();
        private void OnUnloaded(object sender,RoutedEventArgs args)=>Cancel();
        private void Cancel(){_generation++;_pending?.Abort();_pending=null;}
        private void Queue()
        {
            Cancel();if(_disposed||!_combo.IsLoaded)return;
            var generation=_generation;
            _pending=_combo.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
            {
                if(_disposed||generation!=_generation||!_combo.IsLoaded)return;
                _pending=null;NativeTextInheritance.Restore(_combo);
                if(_combo.IsDropDownOpen&&_combo.Template?.FindName("Popup",_combo) is System.Windows.Controls.Primitives.Popup {Child:{} child})
                    NativeTextInheritance.Restore(child);
            }));
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;Cancel();
            _combo.Loaded-=OnLoaded;_combo.Unloaded-=OnUnloaded;
            _combo.SelectionChanged-=OnSelectionChanged;_combo.DropDownOpened-=OnDropDownOpened;
        }
    }
