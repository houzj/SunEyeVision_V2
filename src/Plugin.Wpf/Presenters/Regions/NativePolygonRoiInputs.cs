using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using Numbers=SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>Nonvisual leases for the original ordered Point rows, never a second geometry model.</summary>
internal sealed class NativePolygonRoiInputs : IDisposable
{
    private sealed record Slot(NativePolygonRoiProjection Owner,long Topology,int Index,bool X);
    private sealed class Input(Slot slot,Numbers.SliderArrangePoint view,Numbers.NumericControl leaf,TextBox text)
    {
        internal readonly Slot Slot=slot;
        internal readonly Numbers.SliderArrangePoint View=view;
        internal readonly Numbers.NumericControl Leaf=leaf;
        internal readonly TextBox Text=text;
        internal string Baseline="";
        internal bool Suspended;
        internal IDisposable? Lease;
    }
    private readonly FrameworkElement _root;
    private readonly Func<bool> _ready;
    private readonly List<Input> _inputs=new();
    private readonly Dictionary<Slot,string> _raw=new();
    private NativeRoiSession? _session;
    private NativePolygonRoiProjection? _item;
    private bool _projecting,_committing,_disposed;
    private long _generation;
    internal string Error {get;private set;}="";
    internal bool HasFocus=>_inputs.Any(input=>input.Text.IsKeyboardFocused);
    internal NativePolygonRoiInputs(FrameworkElement root,Func<bool> ready){_root=root;_ready=ready;}
    internal void Bind(NativeRoiSession? session,NativePolygonRoiProjection? item)
    {
        _generation++;ReleaseInputs();_raw.Clear();Error="";_session=session;_item=item;
    }
    private bool Current(Slot slot)=>!_disposed&&_ready()&&_session?.IsCurrent==true&&ReferenceEquals(_session.CurROIData,slot.Owner)
        &&ReferenceEquals(_item,slot.Owner)&&slot.Topology==slot.Owner.TopologyGeneration;
    internal void Attach()
    {
        if(_disposed||_item is null)return;
        var present=new HashSet<Input>();
        foreach(var view in Descendants<Numbers.SliderArrangePoint>(_root))
        {
            var container=Ancestor<ContentPresenter>(view);
            var list=container is null?null:ItemsControl.ItemsControlFromItemContainer(container);
            if(list is null||!ReferenceEquals(list.ItemsSource,_item.PolygonPoints))continue;
            var index=list.ItemContainerGenerator.IndexFromContainer(container!);
            if(index<0||index>=_item.PolygonPoints.Count)continue;
            var slot=new Slot(_item,_item.TopologyGeneration,index,view.IsX);
            var leaf=view.EnsurePointInput();
            var existing=_inputs.FirstOrDefault(input=>ReferenceEquals(input.View,view)&&ReferenceEquals(input.Leaf,leaf)&&input.Slot==slot&&!input.Suspended);
            if(existing is not null){present.Add(existing);continue;}
            foreach(var old in _inputs.Where(input=>ReferenceEquals(input.View,view)).ToArray()){Freeze(old);Release(old);_inputs.Remove(old);}
            var text=(TextBox)leaf.FindName("ValueText");
            var input=new Input(slot,view,leaf,text){Baseline=Baseline(view,leaf)};
            _inputs.Add(input);present.Add(input);
            text.TextChanged+=OnText;
            input.Lease=view.BindSlot(value=>
            {
                if(input.Suspended||!Current(slot))return false;
                if(_projecting||_committing)return true;
                return slot.Owner.TryStageVertex(slot.Topology,slot.Index,slot.X,value);
            },()=>Freeze(input));
            Restore(input);
        }
        foreach(var old in _inputs.Where(input=>!present.Contains(input)).ToArray())
        {Freeze(old);Release(old);_inputs.Remove(old);}
    }
    private static string Baseline(Numbers.SliderArrangePoint view,Numbers.NumericControl leaf)=>view.StartValue.ToString(
        BindingOperations.GetBinding(leaf,Numbers.NumericControl.TextProperty)?.StringFormat??"0.00",CultureInfo.InvariantCulture);
    private void OnText(object sender,TextChangedEventArgs args)
    {
        if(_projecting||_committing)return;
        var input=_inputs.FirstOrDefault(input=>ReferenceEquals(input.Text,sender));
        if(input is null||input.Suspended||!Current(input.Slot)||!input.Text.IsKeyboardFocused)return;
        if(input.Text.Text==input.Baseline)_raw.Remove(input.Slot);else _raw[input.Slot]=input.Text.Text;
        Error="";
    }
    private void Freeze(Input input)
    {
        if(input.Suspended)return;
        // Preserve existing exact raw before the original Point clears its formatted binding.
        if(!_raw.ContainsKey(input.Slot)&&input.Text.IsKeyboardFocused&&input.Text.Text!=input.Baseline)_raw[input.Slot]=input.Text.Text;
        input.Suspended=true;input.Text.TextChanged-=OnText;
    }
    internal void Suspend(){foreach(var input in _inputs)Freeze(input);}
    private void Restore(Input input)
    {
        if(!_raw.TryGetValue(input.Slot,out var raw))return;
        _projecting=true;try{input.Text.SetCurrentValue(TextBox.TextProperty,raw);}finally{_projecting=false;}
    }
    internal bool TryCommit()
    {
        if(_committing||_item is not {} item||_session is not {} session)return false;
        Attach();var topology=item.TopologyGeneration;var generation=_generation;
        bool Alive()=>generation==_generation&&Current(new(item,topology,0,true));
        if(!Alive())return false;
        var raw=_raw.ToArray();var changes=new Dictionary<int,double>();
        var accepting=false;_committing=true;
        try
        {
            foreach(var (slot,text) in raw)
            {
                if(!Current(slot))throw new ArgumentException("多边形顶点原文所属租约已失效。");
                if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||!double.IsFinite(value)||value is < -30000 or >30000)
                    throw new ArgumentException("请输入原Point范围内的有限数值。");
                changes[slot.Index*2+(slot.X?0:1)]=value;
            }
            foreach(var input in _inputs)
            {
                if(input.Suspended||!Current(input.Slot))continue;
                if(!_raw.ContainsKey(input.Slot)&&Baseline(input.View,input.Leaf)!=input.Baseline)
                    changes[input.Slot.Index*2+(input.Slot.X?0:1)]=input.View.StartValue;
                var inner=input.Text.GetBindingExpression(TextBox.TextProperty);
                var middle=input.Leaf.GetBindingExpression(Numbers.NumericControl.TextProperty);
                var outer=BindingOperations.GetMultiBindingExpression(input.View,Numbers.SliderArrangePoint.PointSourceProperty);
                if(inner is null||middle is null||outer is null||outer.Status!=BindingStatus.Active)
                    throw new ArgumentException("原Point数值或顶点投影绑定缺失。");
            }
            foreach(var input in _inputs.ToArray())
            {
                if(!changes.TryGetValue(input.Slot.Index*2+(input.Slot.X?0:1),out var value)||input.Suspended)continue;
                if(!Alive()||!Current(input.Slot))return false;
                input.Text.SetCurrentValue(TextBox.TextProperty,_raw.GetValueOrDefault(input.Slot)??value.ToString("R",CultureInfo.InvariantCulture));
                input.Text.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();if(!Alive())return false;
                input.Leaf.GetBindingExpression(Numbers.NumericControl.TextProperty)!.UpdateSource();if(!Alive())return false;
                input.View.SetCurrentValue(Numbers.SliderArrangeBase.StartValueProperty,value);if(!Alive())return false;
                if(Validation.GetHasError(input.Text)||Validation.GetHasError(input.Leaf)||Validation.GetHasError(input.View))
                    throw new ArgumentException("原Point数值绑定校验未通过。");
            }
            if(!Alive())return false;
            // Hidden/unrealized rows retain their authoritative precision. Prepare validates the entire geometry.
            if(changes.Count!=0){item.Cancel();if(!Alive())return false;item.StageFields(changes);}
            if(!Alive())return false;
            accepting=true;var accepted=session.TryCommit(item);
            if(generation!=_generation||!ReferenceEquals(_session,session)||!ReferenceEquals(_item,item)||_disposed)return accepted;
            if(!accepted){Error=session.Error;RestoreAll(raw);return false;}
            _raw.Clear();Error="";
            foreach(var input in _inputs)input.View.ClosePointPopup();
            return true;
        }
        catch(Exception error)when(!accepting&&(error is ArgumentException or FormatException or OverflowException))
        {
            item.Cancel();if(Alive()){Error=error.Message;RestoreAll(raw);}return false;
        }
        finally{_committing=false;}
    }
    private void RestoreAll(KeyValuePair<Slot,string>[] raw)
    {
        foreach(var pair in raw)if(Current(pair.Key))_raw[pair.Key]=pair.Value;
        foreach(var input in _inputs)if(!input.Suspended&&Current(input.Slot))Restore(input);
    }
    private static T? Ancestor<T>(DependencyObject start)where T:DependencyObject
    {for(var p=VisualTreeHelper.GetParent(start);p is not null;p=VisualTreeHelper.GetParent(p))if(p is T value)return value;return null;}
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T item)yield return item;foreach(var nested in Descendants<T>(child))yield return nested;}}
    private void Release(Input input){input.Text.TextChanged-=OnText;input.Lease?.Dispose();input.Lease=null;}
    private void ReleaseInputs(){foreach(var input in _inputs)Release(input);_inputs.Clear();}
    public void Dispose(){if(_disposed)return;_disposed=true;_generation++;ReleaseInputs();_raw.Clear();_item=null;_session=null;}
}
