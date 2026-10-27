using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using NativeRow=SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Original radio templates over one borrowed field state. No parameter or accepted-value copy.</summary>
internal sealed class NativeIntegerRadioPresenter : Decorator,IDisposable,INotifyPropertyChanged
{
    private readonly DeclaredFieldState _state;
    private readonly NativeIntegerRadioOptions _options;
    private bool _disposed,_projecting,_hidden;
    public NativeRow NativeView {get;}
    public string DisplayNameA {get;private set;}
    public string DescriptionA=>DisplayNameA;
    public double LangRadioSelecterWidth=>LocalizationResourceDictionary.Instance.CultureName.StartsWith("zh",StringComparison.OrdinalIgnoreCase)?89:165;
    public sealed record Entry(string? DisplayNameALang,object? ValueA);
    public IReadOnlyList<Entry> EnumEntrysA {get;}
    public object? CurValueA=>_state.Accepted is ConstantSetting{Value:long value}
        &&value>=int.MinValue&&value<=int.MaxValue&&_options.Choices.Any(x=>x.Value==value)?(int)value:null;
    public ICommand ValueChangedCommand {get;}
    public event PropertyChangedEventHandler? PropertyChanged;
    public NativeIntegerRadioPresenter(DeclaredFieldState state,string label,NativeIntegerRadioOptions options)
    {
        _state=state;_options=options;DisplayNameA=label;
        options.ValidatePort(state.Port);
        EnumEntrysA=Array.AsReadOnly(Enumerable.Range(0,options.UseFiveSlotTemplate?5:2)
            .Select(i=>i<options.Choices.Count?new Entry(options.Choices[i].Label,options.Choices[i].Value):new Entry(null,null)).ToArray());
        ValueChangedCommand=new SelectionCommand(this);
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/IntegerRadioResources.xaml",UriKind.Relative)});
        NativeView=(NativeRow)((DataTemplate)FindResource(options.UseFiveSlotTemplate?"RadioSelecter3":"RadioSelecter")).LoadContent();
        NativeView.DataContext=this;Child=NativeView;
        _state.Changed+=Project;
        Loaded+=OnLoaded;Unloaded+=OnUnloaded;
    }
    public void SetLabel(string text)
    {if(_disposed)return;DisplayNameA=text;PropertyChanged?.Invoke(this,new(nameof(DisplayNameA)));PropertyChanged?.Invoke(this,new(nameof(DescriptionA)));}
    private bool CanSelect(object? value)=>!_disposed&&!_state.IsDisposed&&!_projecting&&!_hidden&&IsLoaded&&IsVisible&&IsEnabled
        &&value is int key&&_options.Choices.Any(x=>x.Value==key);
    private void Select(object? value)
    {
        if(!CanSelect(value))return;
        try{_state.TrySet(new ConstantSetting((long)(int)value!));}
        finally{Project();}
    }
    public bool TrySetConstant(object value)
    {
        if(_disposed||_state.IsDisposed)return false;
        try
        {
            ConstantSetting next;
            try{next=new ConstantSetting(ValueRules.Freeze(_state.Port,value));}
            catch(Exception error)when(error is ArgumentException or OverflowException){_state.SetEditError(error.Message);return false;}
            if(_options.Validate(next) is {} invalid){_state.SetEditError(invalid);return false;}
            return _state.TrySet(next);
        }
        finally{Project();}
    }
    private void Project()
    {
        if(_disposed||_state.IsDisposed||_projecting)return;
        _projecting=true;
        try{PropertyChanged?.Invoke(this,new(nameof(CurValueA)));}
        finally{_projecting=false;}
    }
    public void PrepareForVisibility(bool visible){_hidden=!visible;}
    private void OnLoaded(object sender,RoutedEventArgs args){LayoutUpdated-=OnLayout;LayoutUpdated+=OnLayout;Project();}
    private void OnUnloaded(object sender,RoutedEventArgs args){LayoutUpdated-=OnLayout;}
    private void OnLayout(object? sender,EventArgs args){if(!_disposed&&IsLoaded)NativeTextInheritance.Restore(NativeView);}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _state.Changed-=Project;Loaded-=OnLoaded;Unloaded-=OnUnloaded;LayoutUpdated-=OnLayout;
        PropertyChanged=null;NativeView.IsEnabled=false;NativeView.DataContext=null;Child=null;
    }
    private sealed class SelectionCommand(NativeIntegerRadioPresenter owner):ICommand
    {
        public bool CanExecute(object? parameter)=>owner.CanSelect(parameter);
        public void Execute(object? parameter)=>owner.Select(parameter);
        public event EventHandler? CanExecuteChanged {add{}remove{}}
    }
}
