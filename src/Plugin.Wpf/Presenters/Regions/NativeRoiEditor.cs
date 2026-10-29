using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using Numbers=SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>Original Box selector branch, expand namescope and five-field parameter stack.</summary>
public sealed class NativeRoiEditor : Decorator,IDisposable
{
    private sealed record Input(Numbers.SliderArrangeFloat View,Numbers.NumericControl Leaf,TextBox Text)
    {internal string Baseline="";internal string? Raw;}
    private readonly List<Input> _inputs=new();
    private readonly NativePolygonRoiInputs _polygon;
    private NativeRoiSession? _session;
    private NativeRoiSession? _projectedSession;
    private NativeRoiProjection? _projectedItem;
    private RoiEditStamp? _projectedStamp;
    private IDisposable? _registration;
    private bool _disposed,_projecting,_committing;
    private long _generation;
    private long _uiGeneration;
    private DispatcherOperation? _resume;
    private bool _suspended;
    private string?[]? _savedRaw;
    public RoiSelectControl NativeView {get;}=new();
    public string Error {get;private set;}="";
    public bool HasInvalidText=>Error.Length!=0;
    public NativeRoiEditor()
    {
        _polygon=new(NativeView,()=>!_disposed&&!_projecting&&!_suspended&&(IsLoaded||System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)));
        Language=XmlLanguage.GetLanguage("en-US");Child=NativeView;
        NativeView.LayoutUpdated+=OnLayout;PreviewKeyDown+=OnKey;Loaded+=OnLoaded;Unloaded+=OnUnloaded;
    }
    public void Bind(NativeRoiSession? session)
    {
        Dispatcher.VerifyAccess();if(_disposed)throw new ObjectDisposedException(nameof(NativeRoiEditor));
        _resume?.Abort();_resume=null;_savedRaw=null;_suspended=false;_registration?.Dispose();_registration=null;
        if(_session is not null){_session.Changed-=Project;_session.PropertyChanged-=OnDrawStateChanged;}
        _session=session;_projectedSession=null;_projectedItem=null;_projectedStamp=null;
        if(session is not null){_registration=session.RegisterEditorCommit(()=>TryCommitPending());session.Changed+=Project;session.PropertyChanged+=OnDrawStateChanged;}
        Project();
    }
    private void Project()=>Project(false);
    private void OnDrawStateChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs args)
    {if(!_disposed&&args.PropertyName==nameof(NativeRoiSession.ActiveDraw)&&ReferenceEquals(sender,_session)&&NativeView.DataContext is Context context)context.RefreshTools();}
    private void Project(bool force)
    {
        if(_disposed)return;
        // Same-root rejection/no-op notifications must not erase the active raw transaction.
        // Explicit Bind resets this lease; a changed version/selection still projects immediately.
        if(!force&&_committing&&_session?.IsCurrent==true&&ReferenceEquals(_projectedSession,_session)
            &&_projectedStamp==_session.Stamp&&ReferenceEquals(_projectedItem,_session.CurROIData))return;
        _generation++;_resume?.Abort();_resume=null;_savedRaw=null;_suspended=!(IsLoaded||System.ComponentModel.DesignerProperties.GetIsInDesignMode(this));_projecting=true;
        try
        {
            Error="";foreach(var input in _inputs)input.Raw=null;
            var session=_session;
            _polygon.Bind(session,session?.CurROIData as NativePolygonRoiProjection);
            NativeView.IsEnabled=session?.IsCurrent==true;
            NativeView.ImageWidth=session?.ImageWidth??0;NativeView.ImageHeight=session?.ImageHeight??0;
            NativeView.DataContext=session is null?null:new Context(this,session,_generation,session.CurROIData);
            NativeView.ApplyTemplate();NativeView.UpdateLayout();AttachInputs();
            foreach(var input in _inputs)input.Baseline=DisplayBaseline(input);
            _projectedSession=session;_projectedItem=session?.CurROIData;_projectedStamp=session?.IsCurrent==true?session.Stamp:null;
        }
        finally{_projecting=false;}
    }
    private void OnLayout(object? sender,EventArgs args){if(!_disposed&&!_projecting&&!_committing)AttachInputs();}
    private void OnLoaded(object sender,RoutedEventArgs args)
    {
        if(!ReferenceEquals(args.OriginalSource,this)||_disposed)return;
        // Replace only the command lease; keep the same raw input and parameter stack.
        var session=_session;var item=session?.CurROIData;
        var raw=_savedRaw??_inputs.Select(input=>input.Raw).ToArray();_savedRaw=raw;_suspended=true;
        _projecting=true;
        try
        {
            NativeView.DataContext=session is null?null:new Context(this,session,_generation,session.CurROIData);
            NativeView.UpdateLayout();AttachInputs();
            for(var i=0;i<Math.Min(raw.Length,_inputs.Count);i++)
                if(raw[i] is {} value){_inputs[i].Raw=value;_inputs[i].Text.Text=value;}
        }
        finally{_projecting=false;}
        var uiGeneration=_uiGeneration;var generation=_generation;
        _resume?.Abort();
        _resume=Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>
        {
            if(_disposed||!IsLoaded||uiGeneration!=_uiGeneration||generation!=_generation||!ReferenceEquals(session,_session)||!ReferenceEquals(item,session?.CurROIData))return;
            _resume=null;_projecting=true;
            try{for(var i=0;i<Math.Min(raw.Length,_inputs.Count);i++)if(raw[i] is {} value){_inputs[i].Raw=value;_inputs[i].Text.SetCurrentValue(TextBox.TextProperty,value);}}
            finally{_projecting=false;_suspended=false;_savedRaw=null;}
        }));
    }
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {
        if(!ReferenceEquals(args.OriginalSource,this)||_disposed)return;
        if(!_suspended)_savedRaw=_inputs.Select(input=>input.Raw).ToArray();_suspended=true;
        _polygon.Suspend();_uiGeneration++;_resume?.Abort();_resume=null;
        _projecting=true;
        try{foreach(var input in _inputs)((ComboBox)input.View.FindName("CBox")).SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);}
        finally{_projecting=false;}
    }
    private void AttachInputs()
    {
        _polygon.Attach();
        var views=Descendants<Numbers.SliderArrangeFloat>(NativeView).ToArray();
        if(views.SequenceEqual(_inputs.Select(input=>input.View)))return;
        foreach(var input in _inputs){input.Text.TextChanged-=OnText;input.Leaf.Loaded-=OnLeafLoaded;}
        _inputs.Clear();
        foreach(var view in views)
        {
            view.ApplyTemplate();var leaf=view.EnsureTemplateInputs().Left;
            var text=(TextBox)leaf.FindName("ValueText");
            var input=new Input(view,leaf,text);input.Baseline=DisplayBaseline(input);_inputs.Add(input);
            text.TextChanged+=OnText;leaf.Loaded+=OnLeafLoaded;
        }
    }
    private void OnLeafLoaded(object sender,RoutedEventArgs args)
    {
        var input=_inputs.SingleOrDefault(input=>ReferenceEquals(input.Leaf,sender));
        if(input?.Raw is {} raw){_projecting=true;try{input.Text.Text=raw;}finally{_projecting=false;}}
    }
    private void OnText(object sender,TextChangedEventArgs args)
    {
        if(_projecting||_committing||_suspended)return;
        var input=_inputs.SingleOrDefault(input=>ReferenceEquals(input.Text,sender));
        if(input is not null&&input.Text.IsKeyboardFocused){input.Raw=input.Text.Text==input.Baseline?null:input.Text.Text;Error="";}
    }
    private static string DisplayBaseline(Input input)=>input.View.StartValue.ToString(BindingOperations.GetBinding(input.Leaf,Numbers.NumericControl.TextProperty)?.StringFormat??"0.00",CultureInfo.InvariantCulture);
    public bool TryCommitPending()
    {
        if(_disposed||_committing||_session?.IsCurrent!=true)return false;
        var session=_session;
        if(session.CurROIData is not {} item)return _inputs.All(input=>input.Raw is null);
        if(item is NativePolygonRoiProjection)
        {
            var polygonGeneration=_generation;_committing=true;
            try
            {
                var accepted=_polygon.TryCommit();
                if(!_disposed&&polygonGeneration==_generation&&ReferenceEquals(_session,session)&&ReferenceEquals(session.CurROIData,item))
                {Error=_polygon.Error;if(accepted)Project(true);}
                return accepted;
            }
            finally{_committing=false;}
        }
        AttachInputs();if(_inputs.Count==0)return true;
        var fields=item.Fields;
        if(_inputs.Count!=fields.Count){Error="原区域参数栈缺少真实数值字段。";return false;}
        var inputs=_inputs.ToArray();var accepting=false;var generation=_generation;var uiGeneration=_uiGeneration;
        bool Current()=>!_disposed&&generation==_generation&&uiGeneration==_uiGeneration&&ReferenceEquals(session,_session)
            &&session.IsCurrent&&ReferenceEquals(item,session.CurROIData);
        var changed=new Dictionary<int,double>();var raw=(_savedRaw??inputs.Select(input=>input.Raw).ToArray()).ToArray();
        if(raw.Length!=inputs.Length){Error="区域原文与当前参数栈不匹配。";return false;}
        _committing=true;
        try
        {
            // Parse the whole dirty group before updating any of the three original binding layers.
            for(var i=0;i<fields.Count;i++)
            {
                if(raw[i] is not {} value)continue;
                if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)||!double.IsFinite(number)
                    ||number<inputs[i].View.Minimum||number>inputs[i].View.Maximum)
                    throw new ArgumentException("请输入原Box范围内的有限数值。");
                changed.Add(i,number);
            }
            // Native arrows/sliders update StartValue before their final command, rather than typing text.
            for(var i=0;i<fields.Count;i++)
                if(raw[i] is null&&DisplayBaseline(inputs[i])!=inputs[i].Baseline)
                    changed[i]=inputs[i].View.StartValue;
            if(changed.Count==0){item.Cancel();return Current();}
            item.Cancel();if(!Current())return false;
            foreach(var (i,value) in changed)
            {
                var input=inputs[i];input.Text.Text=raw[i]??value.ToString("F2",CultureInfo.InvariantCulture);
                if(!Current())return false;
                var inner=input.Text.GetBindingExpression(TextBox.TextProperty)??throw new ArgumentException("原Box内层文字绑定缺失。");
                inner.UpdateSource();if(!Current())return false;
                var middle=input.Leaf.GetBindingExpression(Numbers.NumericControl.TextProperty)??throw new ArgumentException("原Box数值绑定缺失。");
                middle.UpdateSource();if(!Current())return false;
                input.View.SetCurrentValue(Numbers.SliderArrangeBase.StartValueProperty,value);if(!Current())return false;
                var outer=BindingOperations.GetBindingExpressionBase(input.View,Numbers.SliderArrangeBase.StartValueProperty);
                if(outer is null||outer.Status!=BindingStatus.Active)
                    throw new ArgumentException("原Box外层几何绑定缺失或失效。");
                if(fields[i].MultiBinding&&outer is MultiBindingExpression multi)multi.UpdateSource();
                else if(!fields[i].MultiBinding&&outer is BindingExpression single)single.UpdateSource();
                else throw new ArgumentException("原Box外层几何绑定类型不符。");
                if(!Current())return false;
                if(Validation.GetHasError(input.Text)||Validation.GetHasError(input.Leaf)||Validation.GetHasError(input.View))
                    throw new ArgumentException("原Box数值绑定校验未通过。");
            }
            if(!Current())return false;
            // Keep unedited high precision components from the authoritative record.
            item.Cancel();if(!Current())return false;item.StageFields(changed);if(!Current())return false;
            accepting=true;
            if(!session.TryCommit(item)){if(Current()){Error=session.Error;RestoreRaw(raw,item);}return false;}
            if(!_disposed&&generation==_generation)Project(true);
            return true;
        }
        catch(Exception error)when(!accepting&&(error is ArgumentException or FormatException or OverflowException))
        {item.Cancel();if(Current()){Error=error.Message;RestoreRaw(raw,item);}return false;}
        finally{try{if(!accepting)item.Cancel();}finally{_committing=false;}}
    }
    private void RestoreRaw(string?[] raw,NativeRoiProjection item)
    {
        var session=_session;var generation=_generation;var uiGeneration=_uiGeneration;var inputs=_inputs.ToArray();
        bool Current()=>!_disposed&&generation==_generation&&uiGeneration==_uiGeneration&&ReferenceEquals(session,_session)
            &&session?.IsCurrent==true&&ReferenceEquals(session.CurROIData,item);
        if(!Current()||raw.Length!=inputs.Length)return;_projecting=true;
        try
        {
            foreach(var input in inputs)
            {
                var binding=BindingOperations.GetBindingExpressionBase(input.View,Numbers.SliderArrangeBase.StartValueProperty);
                if(binding is BindingExpression single)single.UpdateTarget();else if(binding is MultiBindingExpression multi)multi.UpdateTarget();
                if(!Current())return;
            }
            for(var i=0;i<inputs.Length;i++)if(raw[i] is {} value)
            {
                inputs[i].Raw=value;inputs[i].Text.Text=value;if(!Current())return;
            }
        }
        finally{_projecting=false;}
    }
    private void OnKey(object sender,KeyEventArgs args)
    {
        if(args.Key!=Key.Enter||(!_inputs.Any(input=>input.Text.IsKeyboardFocused)&&!_polygon.HasFocus))return;
        args.Handled=true;
        if(TryCommitPending())
        {foreach(var input in _inputs)((ComboBox)input.View.FindName("CBox")).SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);Keyboard.ClearFocus();}
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_generation++;_resume?.Abort();_resume=null;_savedRaw=null;
        _registration?.Dispose();_registration=null;if(_session is not null){_session.Changed-=Project;_session.PropertyChanged-=OnDrawStateChanged;}
        _session=null;_projectedSession=null;_projectedItem=null;_projectedStamp=null;NativeView.LayoutUpdated-=OnLayout;PreviewKeyDown-=OnKey;Loaded-=OnLoaded;Unloaded-=OnUnloaded;
        foreach(var input in _inputs){input.Text.TextChanged-=OnText;input.Leaf.Loaded-=OnLeafLoaded;}
        _inputs.Clear();_polygon.Dispose();NativeView.DataContext=null;Child=null;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private sealed class Context : System.ComponentModel.INotifyPropertyChanged
    {
        private readonly NativeRoiSession _session;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        internal void RefreshTools()
        {
            foreach(var name in new[]{nameof(FullScreenA),nameof(BoxA),nameof(SectorRingA),nameof(PolygonA),nameof(SectorA)})
                PropertyChanged?.Invoke(this,new(name));
        }
        public Context(NativeRoiEditor owner,NativeRoiSession session,long generation,NativeRoiProjection? item)
        {
            _session=session;var uiGeneration=owner._uiGeneration;
            ValueChangedCommand=new RequestCommand(()=>!owner._disposed&&!owner._projecting&&!owner._committing&&!owner._suspended&&owner.IsLoaded
                &&uiGeneration==owner._uiGeneration&&generation==owner._generation&&ReferenceEquals(owner._session,session)&&session.IsCurrent,
                value=>
                {
                    try{session.ValueChangedCommand.Execute(value);}
                    finally{RefreshTools();}
                });
            ROIValueChangedCommand=new CommitCommand(()=>
            {if(!owner._disposed&&!owner._projecting&&!owner._committing&&!owner._suspended&&owner.IsLoaded&&uiGeneration==owner._uiGeneration&&owner._generation==generation&&ReferenceEquals(owner._session,session)&&ReferenceEquals(session.CurROIData,item))owner.TryCommitPending();});
        }
        public NativeRoiProjection? CurROIData=>_session.CurROIData;
        public bool IsHaveData=>_session.IsHaveData;
        public bool BoxEnable_ModuleEnableA=>_session.BoxEnable_ModuleEnableA;
        public bool SectorEnable_ModuleEnableA=>_session.SectorEnable_ModuleEnableA;
        public bool BoxA {get=>_session.BoxA;set=>_session.BoxA=value;}
        public bool SectorA {get=>_session.SectorA;set=>_session.SectorA=value;}
        public bool SectorRingEnable_ModuleEnableA=>_session.SectorRingEnable_ModuleEnableA;
        public bool SectorRingA {get=>_session.SectorRingA;set=>_session.SectorRingA=value;}
        public bool PolygonEnable_ModuleEnableA=>_session.PolygonEnable_ModuleEnableA;
        public bool PolygonA {get=>_session.PolygonA;set=>_session.PolygonA=value;}
        public bool FullScreenEnableA=>_session.FullScreenEnableA;
        public bool FullScreen_ModuleEnableA=>_session.FullScreen_ModuleEnableA;
        public bool FullScreenA {get=>_session.FullScreenA;set=>_session.FullScreenA=value;}
        public bool IsAvoid=>_session.IsAvoid;
        public bool Is3DROI=>false;
        public string ROISelectionA=>_session.ROISelectionA;
        public string DisplayNameA=>_session.DisplayNameA;
        public string DescriptionA=>_session.DescriptionA;
        public ICommand ValueChangedCommand {get;}
        public ICommand ROIValueChangedCommand {get;}
    }
    private sealed class RequestCommand(Func<bool> current,Action<object?> execute):ICommand
    {
        public bool CanExecute(object? parameter)=>current();
        public void Execute(object? parameter){if(current())execute(parameter);}
        public event EventHandler? CanExecuteChanged{add{}remove{}}
    }
    private sealed class CommitCommand(Action execute):ICommand
    {
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter)=>execute();
        public event EventHandler? CanExecuteChanged{add{}remove{}}
    }
}
