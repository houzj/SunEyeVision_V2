using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.BaseData;
/// <summary>Original normalized/pixel point-list projection. Collection identity and subscriptions are instance-owned.</summary>
public sealed class PointListRightFixdMultiplicationConverter : IMultiValueConverter,IDisposable
{
    private IList<Point>? _left;
    private ProjectedPoints? _right;
    private double _width,_height;
    private bool _projecting,_writing,_disposed;
    private long _generation,_revision,_topology;
    private bool _pending,_pendingTopology;
    private sealed class ProjectedPoints:ObservableCollection<Point>
    {
        private int _notifications;
        internal bool IsNotifying=>_notifications!=0;
        internal Action? NotificationCompleted;
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            _notifications++;
            Exception? failure=null;
            try{base.OnCollectionChanged(args);}
            catch(Exception error){failure=error;}
            finally{_notifications--;}
            try{if(_notifications==0)NotificationCompleted?.Invoke();}
            catch(Exception cleanup){if(failure is not null)throw new AggregateException(failure,cleanup);throw;}
            if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
        internal void ReplaceAll(IReadOnlyList<Point> points)
        {
            Items.Clear();foreach(var point in points)Items.Add(point);
            OnPropertyChanged(new("Count"));OnPropertyChanged(new("Item[]"));
            OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
        }
    }
    public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(values.Length<3||values[0] is not IList<Point> source||values[1]==DependencyProperty.UnsetValue||values[2]==DependencyProperty.UnsetValue)
        {Disconnect();return DependencyProperty.UnsetValue;}
        var width=System.Convert.ToDouble(values[1],CultureInfo.InvariantCulture);
        var height=System.Convert.ToDouble(values[2],CultureInfo.InvariantCulture);
        if(!double.IsFinite(width)||!double.IsFinite(height)||width<=0||height<=0)
        {Disconnect();return DependencyProperty.UnsetValue;}
        var changed=!ReferenceEquals(source,_left);
        if(changed)
        {
            Disconnect();_left=source;_right=new();_right.CollectionChanged+=RightChanged;
            _right.NotificationCompleted=()=>{if(_pending)Synchronize(false);};
            if(source is INotifyCollectionChanged notifier)CollectionChangedEventManager.AddHandler(notifier,LeftChanged);
        }
        if(_width!=width||_height!=height){_generation++;_revision++;}
        _width=width;_height=height;Synchronize(changed);return _right!;
    }
    private Point[] Snapshot()=>_left!.Select(point=>
    {
        var value=new Point(point.X*_width,point.Y*_height);
        if(!double.IsFinite(value.X)||!double.IsFinite(value.Y))throw new ArgumentException("多边形坐标必须有限。");
        return value;
    }).ToArray();
    private void Synchronize(bool topology)
    {
        _pending=true;_pendingTopology|=topology;
        if(_projecting||_writing||_right?.IsNotifying==true)return;
        _projecting=true;
        try
        {
            var passes=0;
            while(_pending&&_left is not null&&_right is not null&&!_disposed)
            {
                if(++passes>128)throw new InvalidOperationException("顶点投影观察者持续修改输入，无法取得稳定快照。");
                var reset=_pendingTopology;_pending=false;_pendingTopology=false;
                var right=_right;var revision=_revision;var generation=_generation;
                var points=Snapshot();
                if(revision!=_revision||generation!=_generation||!ReferenceEquals(right,_right)){_pending=true;continue;}
                if(reset||right.Count!=points.Length)right.ReplaceAll(points);
                else for(var i=0;i<points.Length;i++)
                {
                    if(revision!=_revision||generation!=_generation||!ReferenceEquals(right,_right))break;
                    if(right[i]!=points[i])right[i]=points[i];
                }
            }
        }
        finally{_projecting=false;}
    }
    private void LeftChanged(object? sender,NotifyCollectionChangedEventArgs args)
    {
        if(_disposed||!ReferenceEquals(sender,_left))return;
        _revision++;
        var topology=args.Action!=NotifyCollectionChangedAction.Replace;
        if(topology)_topology++;
        Synchronize(topology);
    }
    private void RightChanged(object? sender,NotifyCollectionChangedEventArgs args)
    {
        if(_disposed||_projecting||_writing||!ReferenceEquals(sender,_right)||_left is null)return;
        if(args.Action!=NotifyCollectionChangedAction.Replace||args.NewStartingIndex<0||args.NewItems is null)
        {Synchronize(true);throw new InvalidOperationException("顶点拓扑由区域编辑宿主一次接纳，显示集合不可直接增删或重排。");}
        var source=_left;var right=_right;var generation=_generation;var topology=_topology;
        var width=_width;var height=_height;
        _writing=true;
        try
        {
            for(var offset=0;offset<args.NewItems.Count;offset++)
            {
                if(generation!=_generation||topology!=_topology||!ReferenceEquals(source,_left)||!ReferenceEquals(right,_right))return;
                var index=args.NewStartingIndex+offset;
                if(index>=source.Count)throw new InvalidOperationException("顶点槽位已失效。");
                var point=(Point)args.NewItems[offset]!;
                if(!double.IsFinite(point.X)||!double.IsFinite(point.Y))throw new ArgumentException("多边形坐标必须有限。");
                source[index]=new(point.X/width,point.Y/height);
            }
        }
        finally{_writing=false;Synchronize(false);}
    }
    public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture)=>throw new NotSupportedException("点集合通过有序Replace事件投影，不更换正式来源绑定。");
    public void Disconnect()
    {
        _generation++;_revision++;_topology++;
        _pending=false;_pendingTopology=false;
        if(_left is INotifyCollectionChanged notifier)CollectionChangedEventManager.RemoveHandler(notifier,LeftChanged);
        if(_right is not null){_right.CollectionChanged-=RightChanged;_right.NotificationCompleted=null;}
        _left=null;_right=null;
    }
    public void Dispose(){if(_disposed)return;_disposed=true;Disconnect();}
}
