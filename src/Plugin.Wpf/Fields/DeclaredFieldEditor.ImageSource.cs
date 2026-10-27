using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Input;

namespace SunEyeVision.Plugin.Wpf.Fields;

public sealed partial class DeclaredFieldEditor
{
    private Func<IReadOnlyList<DeclaredReferenceCandidate>>? _imageSources;
    private string _imageRefreshError="";
    private Action? _detachImageSource;

    private void DetachImageSource(){_detachImageSource?.Invoke();_detachImageSource=null;}

    /// <summary>VM EnumerationG uses ComboBox/ListViewComboBoxStyle, not a ButtonSelecter link popup.
    /// The host supplies declarations and owns validation, draft writes and execution.</summary>
    public void UseImageSourceSelector(Func<IReadOnlyList<DeclaredReferenceCandidate>> sources)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_port.Type!=TypeRef.ResourceOf(ResourceKind.Image)||_port.EditModes!=FieldEditModes.Reference)
            throw new ArgumentException("An image source selector requires a declared image reference input.");
        _imageSources=sources??throw new ArgumentNullException(nameof(sources));Render();
    }

    private void RenderImageSource()
    {
        var combo=NativeInputFactory.Enumeration();
        var generation=_renderGeneration;
        var refreshing=false;
        bool Current()=>!_disposed&&generation==_renderGeneration;
        void ChangeRefreshError(string value)
        {
            if(!Current()||_imageRefreshError==value)return;
            _imageRefreshError=value;UpdateErrorDisplay();Changed?.Invoke();
        }
        bool Matches(ComboBoxItem item)=>item.Tag is ReferenceSetting selected&&_setting is ReferenceSetting current
            ?SameImageReference(selected.Reference,current.Reference):item.Tag is UnsetSetting&&_setting is UnsetSetting;
        void RestoreSelection()
        {
            if(!Current())return;
            refreshing=true;
            try{combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().FirstOrDefault(Matches);}
            finally{refreshing=false;}
        }
        void Refresh()
        {
            if(!Current())return;
            try
            {
                // Build a complete candidate snapshot before replacing the accepted display.
                var items=new List<ComboBoxItem>();
                if(!_port.Required)items.Add(new ComboBoxItem{Content="",Tag=new UnsetSetting(),ToolTip="清除图像来源"});
                var candidates=_imageSources!();
                if(!Current())return;
                foreach(var candidate in candidates)
                {
                    if(!Current())return;
                    // A scalar image input cannot silently take the first image of a sequence.
                    if(candidate.Output.Type.Shape==ValueShape.Sequence||candidate.SelectedType!=_port.Type)continue;
                    var reference=new ReferenceSetting(candidate.Reference(BindingSelection.All));
                    if(items.Any(item=>item.Tag is ReferenceSetting prior&&SameImageReference(prior.Reference,reference.Reference)))continue;
                    items.Add(new ComboBoxItem{Content=candidate.Label,Tag=reference});
                }
                var selected=items.FirstOrDefault(Matches);
                if(selected is null&&_setting is ReferenceSetting retained)
                {
                    // Preserve a removed/changed source for review; refreshing never rewrites its identity or draft.
                    selected=new ComboBoxItem{Content=_formatReference?.Invoke(retained.Reference)??retained.Reference.NodeId+" · "+retained.Reference.PortId,
                        Tag=retained,IsEnabled=false,ToolTip="当前来源不在可选图像声明中，请重新选择"};
                    items.Add(selected);
                }
                if(!Current())return;
                refreshing=true;
                try
                {
                    combo.Items.Clear();
                    foreach(var item in items)combo.Items.Add(item);
                    combo.SelectedItem=selected;
                }
                finally{refreshing=false;}
                ChangeRefreshError("");
            }
            catch(Exception error)when(error is ArgumentException or InvalidOperationException)
            {ChangeRefreshError(error.Message);}
        }
        void OnOpened(object? sender,EventArgs args)=>Refresh();
        void OnSelectionChanged(object sender,SelectionChangedEventArgs args)
        {
            if(!Current()||refreshing||combo.SelectedItem is not ComboBoxItem {IsEnabled:true,Tag:ParameterSetting selected})return;
            if(!Set(selected))RestoreSelection();
        }
        void OnUnloaded(object sender,RoutedEventArgs args)=>combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);
        combo.DropDownOpened+=OnOpened;
        combo.SelectionChanged+=OnSelectionChanged;
        combo.Unloaded+=OnUnloaded;
        _detachImageSource=()=>
        {
            combo.DropDownOpened-=OnOpened;
            combo.SelectionChanged-=OnSelectionChanged;
            combo.Unloaded-=OnUnloaded;
            combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);
        };
        InputElement=combo;_body.Children.Add(combo);Refresh();
    }

    private static bool SameImageReference(OutputReference left,OutputReference right)=>left.NodeId==right.NodeId
        &&left.ScopeId==right.ScopeId&&left.PortId==right.PortId&&left.Type==right.Type&&left.Storage==right.Storage
        &&left.MemberFieldPath.SequenceEqual(right.MemberFieldPath)&&Equals(left.Selection,right.Selection);
}
