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
    private NativeRoiSession? _session;
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
        Language=XmlLanguage.GetLanguage("en-US");Child=NativeView;
        NativeView.LayoutUpdated+=OnLayout;PreviewKeyDown+=OnKey;Loaded+=OnLoaded;Unloaded+=OnUnloaded;
    }
    public void Bind(NativeRoiSession? session)
    {
        Dispatcher.VerifyAccess();if(_disposed)throw new ObjectDisposedException(nameof(NativeRoiEditor));
        _resume?.Abort();_resume=null;_savedRaw=null;_suspended=false;_registration?.Dispose();_registration=null;
        if(_session is not null)_session.Changed-=Project;
        _session=session;
        if(session is not null){_registration=session.RegisterEditorCommit(()=>TryCommitPending());session.Changed+=Project;}
        Project();
    }
    private void Project()
    {
        if(_disposed)return;_generation++;_resume?.Abort();_resume=null;_savedRaw=null;_suspended=!IsLoaded;_projecting=true;
        try
        {
            Error="";foreach(var input in _inputs)input.Raw=null;
            var session=_session;
            NativeView.IsEnabled=session?.IsCurrent==true;
            NativeView.ImageWidth=session?.ImageWidth??0;NativeView.ImageHeight=session?.ImageHeight??0;
            NativeView.DataContext=session is null?null:new Context(this,session,_generation,session.CurROIData);
            NativeView.ApplyTemplate();NativeView.UpdateLayout();AttachInputs();
            foreach(var input in _inputs)input.Baseline=DisplayBaseline(input);
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
        _uiGeneration++;_resume?.Abort();_resume=null;
        _projecting=true;
        try{foreach(var input in _inputs)((ComboBox)input.View.FindName("CBox")).SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);}
        finally{_projecting=false;}
    }
    private void AttachInputs()
    {
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
        AttachInputs();if(_inputs.Count==0)return true;
        var fields=item.Fields;
        if(_inputs.Count!=fields.Count){Error="原区域参数栈缺少真实数值字段。";return false;}
        var accepting=false;var generation=_generation;var changed=new Dictionary<int,double>();
        var raw=_savedRaw??_inputs.Select(input=>input.Raw).ToArray();
        _committing=true;
        try
        {
            // Parse the whole dirty group before updating any of the three original binding layers.
            for(var i=0;i<fields.Count;i++)
            {
                if(raw[i] is not {} value)continue;
                if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)||!double.IsFinite(number)
                    ||number<_inputs[i].View.Minimum||number>_inputs[i].View.Maximum)
                    throw new ArgumentException("请输入原Box范围内的有限数值。");
                changed.Add(i,number);
            }
            // Native arrows/sliders update StartValue before their final command, rather than typing text.
            for(var i=0;i<fields.Count;i++)
                if(raw[i] is null&&DisplayBaseline(_inputs[i])!=_inputs[i].Baseline)
                    changed[i]=_inputs[i].View.StartValue;
            if(changed.Count==0){item.Cancel();return true;}
            item.Cancel();
            foreach(var (i,value) in changed)
            {
                var input=_inputs[i];input.Text.Text=raw[i]??value.ToString("F2",CultureInfo.InvariantCulture);
                var inner=input.Text.GetBindingExpression(TextBox.TextProperty)??throw new ArgumentException("原Box内层文字绑定缺失。");
                inner.UpdateSource();
                var middle=input.Leaf.GetBindingExpression(Numbers.NumericControl.TextProperty)??throw new ArgumentException("原Box数值绑定缺失。");
                middle.UpdateSource();
                input.View.SetCurrentValue(Numbers.SliderArrangeBase.StartValueProperty,value);
                var outer=BindingOperations.GetBindingExpressionBase(input.View,Numbers.SliderArrangeBase.StartValueProperty);
                if(outer is null||outer.Status!=BindingStatus.Active)
                    throw new ArgumentException("原Box外层几何绑定缺失或失效。");
                if(fields[i].MultiBinding&&outer is MultiBindingExpression multi)multi.UpdateSource();
                else if(!fields[i].MultiBinding&&outer is BindingExpression single)single.UpdateSource();
                else throw new ArgumentException("原Box外层几何绑定类型不符。");
                if(Validation.GetHasError(input.Text)||Validation.GetHasError(input.Leaf)||Validation.GetHasError(input.View))
                    throw new ArgumentException("原Box数值绑定校验未通过。");
            }
            if(_disposed||generation!=_generation||!ReferenceEquals(session,_session)||!session.IsCurrent||!ReferenceEquals(item,session.CurROIData))return false;
            // Keep unedited high precision components from the authoritative record.
            item.Cancel();item.StageFields(changed);
            accepting=true;
            if(!session.TryCommit(item)){Error=session.Error;RestoreRaw(raw,item);return false;}
            if(!_disposed&&generation==_generation)Project();
            return true;
        }
        catch(Exception error)when(!accepting&&(error is ArgumentException or FormatException or OverflowException))
        {item.Cancel();Error=error.Message;RestoreRaw(raw,item);return false;}
        finally{try{if(!accepting)item.Cancel();}finally{_committing=false;}}
    }
    private void RestoreRaw(string?[] raw,NativeRoiProjection item)
    {
        if(_disposed||_session?.IsCurrent!=true||!ReferenceEquals(_session.CurROIData,item))return;_projecting=true;
        try
        {
            foreach(var input in _inputs)
            {
                var binding=BindingOperations.GetBindingExpressionBase(input.View,Numbers.SliderArrangeBase.StartValueProperty);
                if(binding is BindingExpression single)single.UpdateTarget();else if(binding is MultiBindingExpression multi)multi.UpdateTarget();
            }
            for(var i=0;i<Math.Min(raw.Length,_inputs.Count);i++)if(raw[i] is {} value){_inputs[i].Raw=value;_inputs[i].Text.Text=value;}
        }
        finally{_projecting=false;}
    }
    private void OnKey(object sender,KeyEventArgs args)
    {
        if(args.Key!=Key.Enter||!_inputs.Any(input=>input.Text.IsKeyboardFocused))return;
        args.Handled=true;
        if(TryCommitPending())
        {foreach(var input in _inputs)((ComboBox)input.View.FindName("CBox")).SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);Keyboard.ClearFocus();}
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_generation++;_resume?.Abort();_resume=null;_savedRaw=null;
        _registration?.Dispose();_registration=null;if(_session is not null)_session.Changed-=Project;
        _session=null;NativeView.LayoutUpdated-=OnLayout;PreviewKeyDown-=OnKey;Loaded-=OnLoaded;Unloaded-=OnUnloaded;
        foreach(var input in _inputs){input.Text.TextChanged-=OnText;input.Leaf.Loaded-=OnLeafLoaded;}
        _inputs.Clear();NativeView.DataContext=null;Child=null;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private sealed class Context
    {
        private readonly NativeRoiSession _session;
        public Context(NativeRoiEditor owner,NativeRoiSession session,long generation,NativeRoiProjection? item)
        {
            _session=session;var uiGeneration=owner._uiGeneration;
            ROIValueChangedCommand=new CommitCommand(()=>
            {if(!owner._disposed&&!owner._projecting&&!owner._committing&&!owner._suspended&&owner.IsLoaded&&uiGeneration==owner._uiGeneration&&owner._generation==generation&&ReferenceEquals(owner._session,session)&&ReferenceEquals(session.CurROIData,item))owner.TryCommitPending();});
        }
        public NativeRoiProjection? CurROIData=>_session.CurROIData;
        public bool IsHaveData=>_session.IsHaveData;
        public bool BoxEnable_ModuleEnableA=>_session.BoxEnable_ModuleEnableA;
        public bool SectorEnable_ModuleEnableA=>_session.SectorEnable_ModuleEnableA;
        public bool BoxA {get=>_session.BoxA;set=>_session.BoxA=value;}
        public bool SectorA {get=>_session.SectorA;set=>_session.SectorA=value;}
        public bool Is3DROI=>false;
        public string ROISelectionA=>_session.ROISelectionA;
        public string DisplayNameA=>_session.DisplayNameA;
        public string DescriptionA=>_session.DescriptionA;
        public ICommand ValueChangedCommand=>_session.ValueChangedCommand;
        public ICommand ROIValueChangedCommand {get;}
    }
    private sealed class CommitCommand(Action execute):ICommand
    {
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter)=>execute();
        public event EventHandler? CanExecuteChanged{add{}remove{}}
    }
}
