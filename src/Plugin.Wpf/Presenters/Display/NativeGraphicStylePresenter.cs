using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using NativePopup=SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton;
using NativePicker=SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView;
using NativeFloat=SunEyeVision.Plugin.Wpf.Native.Numeric.Arranged.SliderArrangeFloat;
using NativeArrange=SunEyeVision.Plugin.Wpf.Native.Numeric.Arranged.SliderArrangeBase;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>One original ImageViewConfig template. All leaves borrow the owner's formal states.</summary>
internal sealed class NativeGraphicStylePresenter : Decorator,IDisposable,IParameterVisibility
{
    private readonly NativeGraphicDisplayOptions _roles;
    private readonly bool _visibilityOnly;
    private readonly IReadOnlyDictionary<string,DeclaredFieldState> _states;
    private readonly IReadOnlyDictionary<string,FieldPresentation> _presentations;
    private readonly Func<bool> _current;
    private readonly NativeGraphicBridge _bridge;
    private readonly ContentControl _host;
    private readonly Dictionary<string,NativeNumberCoordinator> _numbers=new(StringComparer.Ordinal);
    private readonly Dictionary<string,NativePicker> _colors=new(StringComparer.Ordinal);
    private readonly List<NativeColorPaletteScope> _palettes=[];
    private readonly Dictionary<string,(long Version,NumericPendingEdit? Raw)> _pendingRaw=new(StringComparer.Ordinal);
    private NativePopup? _popup;
    private ToggleButton? _eye;
    private FrameworkElement? _surface;
    private Window? _owner;
    private bool _disposed,_syncing,_attaching,_hidden,_closing,_committing,_listening;
    private long _generation;
    private long _attachEpoch;
    private static readonly DependencyPropertyDescriptor ColorProperty=DependencyPropertyDescriptor.FromProperty(NativePicker.CurrentColorProperty,typeof(NativePicker));
    public object BridgeObjA=>_bridge;
    public string DisplayNameA {get;}
    public string DescriptionA=>DisplayNameA;
    public bool PanVisibleA=>_roles.ShowStyleButton;
    internal event Action? PendingChanged;
    private void OnPendingText(object sender,TextChangedEventArgs args){if(!_disposed&&!_syncing&&!_closing&&Editable&&IsOpen)PendingChanged?.Invoke();}
    internal bool IsOpen=>_popup?.IsDropDownOpen==true;
    internal FrameworkElement? Surface=>_surface;
    internal NativeGraphicStylePresenter(NativeGraphicDisplayOptions roles,IReadOnlyDictionary<string,DeclaredFieldState> states,
        IReadOnlyDictionary<string,FieldPresentation> presentations,Func<bool> current,bool visibilityOnly=false)
    {
        _roles=roles;_visibilityOnly=visibilityOnly;_states=states;_presentations=presentations;_current=current;
        DisplayNameA=presentations[roles.VisibleFieldId].Layout?.DialogTitle??presentations[roles.VisibleFieldId].DisplayName;
        _bridge=new(roles.ThicknessFieldId is not null){opacityA=visibilityOnly?1:Number(roles.OpacityFieldId)};
        if(roles.ThicknessFieldId is {} thickness)_bridge.StrokeThicknessA=Number(thickness);
        _host=new ContentControl{Content=new NativeGraphicTemplateData(DisplayNameA,_bridge,roles.ShowStyleButton),HorizontalContentAlignment=HorizontalAlignment.Stretch};
        try
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/Display/GraphicStyle.xaml",UriKind.Relative)});
            Project();_host.ContentTemplate=(DataTemplate)FindResource("ImageViewConfig");Child=_host;
            foreach(var state in _states.Values)state.Changed+=OnState;
            Loaded+=OnLoaded;Unloaded+=OnUnloaded;IsVisibleChanged+=OnVisibility;IsEnabledChanged+=OnEnabled;KeyDown+=OnKey;
            Attach();
        }
        catch(Exception failure){try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    private bool Current()
    {
        if(_disposed||_states.Values.Any(state=>state.IsDisposed))return false;
        var result=_current();return result&&!_disposed&&_states.Values.All(state=>!state.IsDisposed);
    }
    private bool Editable=>Current()&&!_hidden&&IsLoaded&&IsVisible&&IsEnabled;
    private bool HasCurrentParts=>_popup is not null&&_eye is not null
        &&Visuals<NativePopup>(_host).Any(item=>ReferenceEquals(item,_popup))
        &&Visuals<ToggleButton>(_host).Any(item=>ReferenceEquals(item,_eye));
    private double Number(string id)=>Convert.ToDouble(((ConstantSetting)_states[id].Accepted).Value,CultureInfo.InvariantCulture);
    private SolidColorBrush Brush(string id)
    {
        var value=(RecordValue)((ConstantSetting)_states[id].Accepted).Value!;
        var brush=new SolidColorBrush(Color.FromArgb(checked((byte)value.Get<long>("a")),checked((byte)value.Get<long>("r")),checked((byte)value.Get<long>("g")),checked((byte)value.Get<long>("b"))));
        brush.Freeze();return brush;
    }
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T value)yield return value;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Visuals<T>(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    private void Attach()
    {
        if(_disposed||_attaching)return;_attaching=true;
        var epoch=++_attachEpoch;var template=_host.ContentTemplate;var abandoned=false;
        bool Alive()
        {
            var alive=epoch==_attachEpoch&&Current()&&ReferenceEquals(template,_host.ContentTemplate);
            if(!alive)abandoned=true;return alive;
        }
        try
        {
            _host.ApplyTemplate();if(!Alive())return;
            _host.Measure(new Size(600,32));if(!Alive())return;
            _host.UpdateLayout();if(!Alive())return;
            var eye=Visuals<ToggleButton>(_host).FirstOrDefault(item=>Equals(item.Tag,"NativeGraphicVisibility"));
            var popup=Visuals<NativePopup>(_host).FirstOrDefault(item=>Equals(item.Tag,"NativeGraphicStylePopup"));
            if(eye is null||popup is null){RetireTemplate();return;}
            if(!ReferenceEquals(eye,_eye)||!ReferenceEquals(popup,_popup))RetireTemplate();
            if(!Alive())return;
            if(!ReferenceEquals(eye,_eye))
            {
                if(_eye is not null)_eye.Click-=OnEye;_eye=eye;
                // A Popup template can retire while its outer original eye is retained.
                if(eye.GetBindingExpression(ToggleButton.IsCheckedProperty) is null)
                    eye.SetBinding(ToggleButton.IsCheckedProperty,new Binding("BridgeObjA.IsDisplayA"){Mode=BindingMode.TwoWay});
                if(!Alive())return;eye.Click+=OnEye;
            }
            if(!ReferenceEquals(popup,_popup))
            {
                if(_popup is not null){_popup.DropDownOpened-=OnOpened;_popup.DropDownClosed-=OnClosed;}
                _popup=popup;popup.DropDownOpened+=OnOpened;popup.DropDownClosed+=OnClosed;
            }
            if(_visibilityOnly)return;
            popup.ApplyTemplate();
            if(!Alive())return;
            if(popup.Template.FindName("PART_Popup",popup) is not Popup popupPart||popupPart.Child is not FrameworkElement child)
            {RetireTemplate();return;}
            child.ApplyTemplate();if(!Alive())return;
            child.Measure(new Size(260,154));if(!Alive())return;
            child.UpdateLayout();if(!Alive())return;
            var surface=Visuals<FrameworkElement>(child).FirstOrDefault(item=>item.Name=="StyleSurface");
            if(surface is null){RetireTemplate();return;}
            if(ReferenceEquals(surface,_surface))return;
            ReleaseSurface();if(!Alive())return;
            _surface=surface;surface.AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnPendingText));var generation=++_generation;
            bool Same()=>Current()&&generation==_generation&&ReferenceEquals(surface,_surface)
                &&ReferenceEquals(template,_host.ContentTemplate)&&ReferenceEquals(_popup,popup)
                &&ReferenceEquals(popup.Template.FindName("PART_Popup",popup),popupPart)&&ReferenceEquals(popupPart.Child,child)
                &&Visuals<NativePopup>(_host).Any(item=>ReferenceEquals(item,popup))
                &&Visuals<FrameworkElement>(child).Any(item=>ReferenceEquals(item,surface));
            var syncing=_syncing;_syncing=true;
            try
            {
                var ok=Part<NativePicker>("NativeGraphicOkPicker");var ng=Part<NativePicker>("NativeGraphicNgPicker");
                var opacity=Part<NativeFloat>("NativeGraphicOpacity");var thickness=Part<NativeFloat>("NativeGraphicThickness");
                T Part<T>(string role)where T:FrameworkElement
                {
                    var parts=Visuals<T>(surface).Where(item=>Equals(item.Tag,role)).ToArray();
                    if(parts.Length!=1)throw new InvalidOperationException($"原图形样式角色{role}需要唯一{typeof(T).Name}，实际{parts.Length}项。");
                    return parts[0];
                }
                foreach(var (id,picker) in new[]{(_roles.OkColorFieldId,ok),(_roles.NgColorFieldId,ng)})
                {
                    _colors.Add(id,picker);_palettes.Add(new(picker,this));if(!Alive())return;
                    ColorProperty.AddValueChanged(picker,OnColor);if(!Alive())return;
                }
                AddNumber(_roles.OpacityFieldId,opacity,value=>_bridge.opacityA=value);
                if(!Alive())return;
                if(_roles.ThicknessFieldId is {} thicknessId)AddNumber(thicknessId,thickness,value=>_bridge.StrokeThicknessA=value);
                if(!Alive())return;
                Project();
                foreach(var number in _numbers.Values)number.PrepareForVisibility(Editable&&IsOpen);
                void AddNumber(string id,NativeFloat original,Action<double> project)
                {
                    var number=new NativeNumberCoordinator(_states[id],_presentations[id],original,original,requireStartBinding:true,
                        projectBoundValue:project,isCurrent:Same,getOwner:()=>Window.GetWindow(this));
                    if(!Alive()){number.Dispose();return;}
                    _numbers.Add(id,number);
                    if(_pendingRaw.Remove(id,out var pending)&&pending.Version==_states[id].AcceptanceVersion)number.RestorePending(pending.Raw);
                }
            }
            finally{_syncing=syncing;}
        }
        catch(Exception failure)
        {try{ReleaseSurface();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
        finally{_attaching=false;if(abandoned)ReleaseSurface();}
    }
    private void Project()
    {
        if(!Current())return;var syncing=_syncing;_syncing=true;
        try
        {
            _bridge.IsDisplayA=(bool)((ConstantSetting)_states[_roles.VisibleFieldId].Accepted).Value!;
            if(_visibilityOnly){if(Current())_eye?.SetCurrentValue(ToggleButton.IsCheckedProperty,_bridge.IsDisplayA);return;}
            if(!Current())return;_bridge.okcolorA=Brush(_roles.OkColorFieldId);
            if(!Current())return;_bridge.ngcolorA=Brush(_roles.NgColorFieldId);
            if(!Current())return;_eye?.SetCurrentValue(ToggleButton.IsCheckedProperty,_bridge.IsDisplayA);
            foreach(var (id,picker) in _colors.ToArray())
            {if(!Current())return;picker.SetCurrentValue(NativePicker.CurrentColorProperty,id==_roles.OkColorFieldId?_bridge.okcolorA:_bridge.ngcolorA);}
        }
        finally{_syncing=syncing;}
    }
    private void OnState(){if(!_committing)Project();}
    private bool Set(string id,object value)
    {
        if(!Current()||_committing)return false;_committing=true;
        try{return _states[id].TrySet(new ConstantSetting(value));}
        finally{_committing=false;if(Current())Project();}
    }
    private void OnEye(object sender,RoutedEventArgs args)
    {if(!_syncing&&Editable&&HasCurrentParts&&ReferenceEquals(sender,_eye))Set(_roles.VisibleFieldId,_eye!.IsChecked==true);}
    private void OnColor(object? sender,EventArgs args)
    {
        if(_syncing||_closing||sender is not NativePicker picker)return;
        if(!Editable||!HasCurrentParts||!IsOpen){Project();return;}
        var entry=_colors.FirstOrDefault(item=>ReferenceEquals(item.Value,picker));if(entry.Key is null)return;
        var color=picker.CurrentColor.Color;Set(entry.Key,PresentationDeclarations.Color(color.R,color.G,color.B,color.A));
    }
    internal FrameworkElement? Input(string id)=>!HasCurrentParts?null:id==_roles.VisibleFieldId?_eye:_colors.TryGetValue(id,out var color)?color:_numbers.GetValueOrDefault(id)?.TextEditor;
    internal bool HasPendingEdits(string id)=>!_disposed&&(_numbers.TryGetValue(id,out var number)&&number.HasPendingEdits
        ||_pendingRaw.TryGetValue(id,out var pending)&&pending.Version==_states[id].AcceptanceVersion
            &&pending.Raw is {} raw&&raw.Acceptance==_states[id].AcceptanceVersion);
    internal bool TrySetConstant(string id,object value)
    {Attach();return _numbers.TryGetValue(id,out var number)?number.TrySetConstant(value):Set(id,value);}
    internal bool TryCommitPending(string id)
    {
        if(!Current())return false;Attach();if(!Current())return false;
        if(_numbers.TryGetValue(id,out var number))return number.TryCommitPending();
        if(_pendingRaw.TryGetValue(id,out var pending))
        {
            if(pending.Version!=_states[id].AcceptanceVersion)_pendingRaw.Remove(id);
            else if(pending.Raw is not null){_states[id].SetEditError("原数值模板未就绪，待提交原文已保留。");return false;}
        }
        return !_states[id].HasInvalidInput;
    }
    internal void Open(){if(!Editable)return;Attach();if(_popup is not null)_popup.IsDropDownOpen=true;}
    internal void Close()
    {
        _closing=true;
        try{ParameterCleanup.Run(_numbers.Values.Select(number=>(Action)(()=>number.PrepareForVisibility(false)))
            .Concat(_palettes.Select(palette=>(Action)palette.ClosePalette))
            .Append(()=>{if(_popup is not null)_popup.IsDropDownOpen=false;}).Append(StopListening));}
        finally{_closing=false;}
    }
    void IParameterVisibility.PrepareForVisibility(bool visible){_hidden=!visible;if(!visible)Close();}
    private void OnLoaded(object sender,RoutedEventArgs args)=>Attach();
    private void OnUnloaded(object sender,RoutedEventArgs args)=>Close();
    private void OnVisibility(object sender,DependencyPropertyChangedEventArgs args)=>((IParameterVisibility)this).PrepareForVisibility(IsVisible);
    private void OnEnabled(object sender,DependencyPropertyChangedEventArgs args){if(!IsEnabled)Close();}
    private void OnOpened(object? sender,EventArgs args)
    {
        if(!Editable||!_roles.ShowStyleButton){Close();return;}Attach();Project();if(!Editable)return;
        foreach(var number in _numbers.Values.ToArray())number.PrepareForVisibility(true);
        if(_listening)return;_listening=true;InputManager.Current.PreProcessInput+=OnInput;
        _owner=Window.GetWindow(this);if(_owner is not null){_owner.Deactivated+=OnOwnerClosed;_owner.Closed+=OnOwnerClosed;}
    }
    private void OnClosed(object? sender,EventArgs args){if(!_closing)Close();}
    private void OnOwnerClosed(object? sender,EventArgs args)=>Close();
    private void StopListening()
    {
        if(_listening)InputManager.Current.PreProcessInput-=OnInput;_listening=false;
        if(_owner is not null){_owner.Deactivated-=OnOwnerClosed;_owner.Closed-=OnOwnerClosed;_owner=null;}
    }
    private void OnInput(object sender,PreProcessInputEventArgs args)
    {
        if(args.StagingItem.Input is not MouseButtonEventArgs {ButtonState:MouseButtonState.Pressed} mouse)return;
        // A WPF Popup has its own visual root, so its child cannot walk back to
        // the graphic-style surface through LogicalTreeHelper or VisualTreeHelper.
        var paletteRoots=_palettes.Select(scope=>scope.Palette)
            .Where(palette=>palette.IsDropDownOpen)
            .Select(palette=>(palette.Template?.FindName("PART_Popup",palette) as Popup)?.Child)
            .OfType<DependencyObject>().ToHashSet();
        var node=Mouse.DirectlyOver as DependencyObject??mouse.OriginalSource as DependencyObject;var visited=new HashSet<DependencyObject>();
        while(node is not null&&visited.Add(node))
        {
            if(ReferenceEquals(node,this)||ReferenceEquals(node,_surface)||paletteRoots.Contains(node))return;
            node=node is Popup popup?popup.PlacementTarget:LogicalTreeHelper.GetParent(node)??(node is Visual?VisualTreeHelper.GetParent(node):null);
        }
        Close();
    }
    private void OnKey(object sender,KeyEventArgs args)
    {if(args.Key==Key.Escape&&!args.Handled){Close();_popup?.Focus();args.Handled=true;}}
    private void ReleaseSurface()
    {
        _generation++;
        foreach(var (id,number) in _numbers.ToArray())_pendingRaw[id]=(_states[id].AcceptanceVersion,number.TakePendingRaw());
        var numbers=_numbers.Values.ToArray();var colors=_colors.Values.ToArray();var palettes=_palettes.ToArray();
        var originals=_surface is null?Array.Empty<NativeFloat>():Visuals<NativeFloat>(_surface).ToArray();
        _numbers.Clear();_colors.Clear();_palettes.Clear();_surface=null;
        ParameterCleanup.Run(numbers.Select(number=>(Action)number.Dispose).Concat(palettes.Select(palette=>(Action)palette.Dispose))
            .Concat(colors.Select(color=>(Action)(()=>ColorProperty.RemoveValueChanged(color,OnColor))))
            .Concat(colors.Select(color=>(Action)(()=>BindingOperations.ClearBinding(color,NativePicker.CurrentColorProperty))))
            .Concat(originals.Select(number=>(Action)(()=>BindingOperations.ClearBinding(number,NativeArrange.StartValueProperty)))));
    }
    private void RetireTemplate()
    {
        var popup=_popup;var eye=_eye;_popup=null;_eye=null;
        ParameterCleanup.Run(new Action[]{()=>{if(eye is not null){eye.Click-=OnEye;BindingOperations.ClearBinding(eye,ToggleButton.IsCheckedProperty);}},
            ()=>{if(popup is not null){popup.DropDownOpened-=OnOpened;popup.DropDownClosed-=OnClosed;}},
            Close,()=>{if(popup is not null)popup.IsDropDownOpen=false;},ReleaseSurface});
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;PendingChanged=null;_attachEpoch++;
        ParameterCleanup.Run(new Action[]{Close,ReleaseSurface,
            ()=>{foreach(var state in _states.Values)state.Changed-=OnState;},
            ()=>{if(_eye is not null)_eye.Click-=OnEye;if(_popup is not null){_popup.DropDownOpened-=OnOpened;_popup.DropDownClosed-=OnClosed;}},
            ()=>{Loaded-=OnLoaded;Unloaded-=OnUnloaded;IsVisibleChanged-=OnVisibility;IsEnabledChanged-=OnEnabled;KeyDown-=OnKey;},
            ()=>{_host.Content=null;Child=null;_popup=null;_eye=null;_pendingRaw.Clear();}});
    }
}
