using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Editing.Wpf.Core;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

/// <summary>Desktop-only projections of an explicitly admitted runtime generation.</summary>
public static class DesktopToolPresentation
{
    public static IToolSettingsView CreateSettingsView(ToolPresentationAdmission admission,FieldSettings fields,ToolSettingsViewOptions? options=null)
    {
        ArgumentNullException.ThrowIfNull(admission);
        var generation=admission.AcquireInvocation("参数视图");
        IToolSettingsView? inner=null;
        SettingsLease? lease=null;
        try
        {
            admission.EnsureCurrent();
            var draft=new ToolSettingsEditSession(admission.Definition,fields);
            var editor=admission.EditorEntryType;
            options??=new();
            inner=editor is null ? new DeclaredSettingsView(draft,options) :
                (CreateFactory(editor)).CreateSettingsView(draft,options)
                ??throw new InvalidOperationException("Settings view factory returned null.");
            admission.EnsureCurrent();
            lease=new SettingsLease(inner,generation,admission);
            admission.EnsureCurrent();
            return lease;
        }
        catch(Exception failure){var errors=new List<Exception>{failure};if(lease is not null){try{lease.Dispose();}catch(Exception cleanup){errors.Add(cleanup);}}else{try{inner?.Dispose();}catch(Exception cleanup){errors.Add(cleanup);}try{generation.Dispose();}catch(Exception cleanup){errors.Add(cleanup);}}if(errors.Count>1)throw new AggregateException(errors);throw;}
    }
    public static IToolResultView? CreateCurrentResultView(ToolPresentationAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        var generation=admission.AcquireInvocation("结果视图");
        IToolResultView? inner=null;
        ResultLease? lease=null;
        try
        {
            admission.EnsureCurrent();
            if(admission.Definition is not IToolResultPresentationProvider provider){admission.EnsureCurrent();generation.Dispose();return null;}
            var schema=provider.ResultSchema;var projection=provider.ResultProjection;
            var errors=projection.Validate(schema);
            if(errors.Count!=0)throw new InvalidOperationException("Invalid runtime result presentation: "+string.Join("; ",errors.Select(e=>e.Message)));
            if(projection.Kind is ResultProjectionKind.None or ResultProjectionKind.PropertyList){admission.EnsureCurrent();generation.Dispose();return null;}
            var editor=admission.EditorEntryType;
            inner=editor is not null ?
                CreateFactory(editor).CreateResultView(schema,projection)
                    ??throw new InvalidOperationException("Result view factory returned null.") :
                projection.Kind switch {ResultProjectionKind.StandardTable=>new DeclaredResultTable(schema,projection),_=>throw new NotSupportedException("No result renderer is registered for: "+projection.RendererKey)};
            admission.EnsureCurrent();
            lease=new ResultLease(inner,generation,admission);
            admission.EnsureCurrent();
            return lease;
        }
        catch(Exception failure){var errors=new List<Exception>{failure};if(lease is not null){try{lease.Dispose();}catch(Exception cleanup){errors.Add(cleanup);}}else{try{inner?.Dispose();}catch(Exception cleanup){errors.Add(cleanup);}try{generation.Dispose();}catch(Exception cleanup){errors.Add(cleanup);}}if(errors.Count>1)throw new AggregateException(errors);throw;}
    }
    private static IToolPresentationFactory CreateFactory(Type editor)
    {
        if(editor.IsAbstract||editor.GetConstructor(Type.EmptyTypes) is null)
            throw new InvalidOperationException("桌面编辑组件必须提供公开无参构造且不能为抽象类型。 "+editor.FullName);
        if(editor.Assembly.GetReferencedAssemblies().Any(a=>a.Name=="SunEyeVision.UI")||
            typeof(IToolPlugin).IsAssignableFrom(editor)||!typeof(IToolPresentationFactory).IsAssignableFrom(editor))
            throw new InvalidOperationException("桌面编辑组件必须是独立的正式呈现工厂。 "+editor.FullName);
        return Activator.CreateInstance(editor) as IToolPresentationFactory
            ??throw new InvalidOperationException("桌面编辑组件不是正式呈现工厂。 "+editor.FullName);
    }
    private sealed class SettingsLease:IToolSettingsView,IToolSettingsViewReady
    {
        private readonly IToolSettingsView _view;
        private readonly RuntimeGenerationLease _generation;
        private readonly ToolPresentationAdmission _admission;
        private CancellationTokenRegistration _retirement;
        private CancellationTokenRegistration _nodeRetirement;
        private readonly List<(Action Original,Action Guarded)> _changed = new();
        private int _disposed;
        internal SettingsLease(IToolSettingsView view,RuntimeGenerationLease generation,ToolPresentationAdmission admission)
        {
            _view=view;_generation=generation;_admission=admission;
            _retirement=generation.RetirementToken.Register(()=>RetireOnDispatcher(view.View,Dispose));
            _nodeRetirement=admission.RetirementToken.Register(()=>RetireOnDispatcher(view.View,Dispose));
        }
        public FrameworkElement View=>_view.View;
        public System.Threading.Tasks.Task ReadyTask=>_view is IToolSettingsViewReady ready
            ?ready.ReadyTask:System.Threading.Tasks.Task.CompletedTask;
        public bool HasInvalidInput=>_view.HasInvalidInput;
        public bool HasPendingEdits=>_view.HasPendingEdits;
        public bool HasPendingEditsOutside(IReadOnlySet<string> excludedFields)=>_view.HasPendingEditsOutside(excludedFields);
        public long AcceptedRevision=>_view.AcceptedRevision;
        public IReadOnlyList<string> AcceptedFieldOrder=>_view.AcceptedFieldOrder;
        public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId=null)
        { _admission.EnsureCurrent();ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);var result=_view.ReadNumericModels(scope,fieldId);_admission.EnsureCurrent();return result; }
        public FieldSettings CaptureAcceptedFields(){_admission.EnsureCurrent();var fields=_view.CaptureAcceptedFields();_admission.EnsureCurrent();return fields;}
        public event Action? Changed
        {
            add { if(value is null)return;_admission.EnsureCurrent();Action guarded=()=>{if(Volatile.Read(ref _disposed)==0&&_admission.IsCurrent)value();};_changed.Add((value,guarded));_view.Changed+=guarded; }
            remove { if(value is null)return;var index=_changed.FindLastIndex(item=>item.Original==value);if(index<0)return;var handler=_changed[index].Guarded;_changed.RemoveAt(index);_view.Changed-=handler; }
        }
        public bool TryCapture(out FieldSettings fields,out string error){_admission.EnsureCurrent();var result=_view.TryCapture(out fields,out error);_admission.EnsureCurrent();return result;}
        public void Dispose(){if(Interlocked.Exchange(ref _disposed,1)!=0)return;var errors=new List<Exception>();foreach(var item in _changed.ToArray())try{_view.Changed-=item.Guarded;}catch(Exception e){errors.Add(e);}_changed.Clear();try{_nodeRetirement.Dispose();}catch(Exception e){errors.Add(e);}try{_retirement.Dispose();}catch(Exception e){errors.Add(e);}try{_view.Dispose();}catch(Exception e){errors.Add(e);}try{_generation.Dispose();}catch(Exception e){errors.Add(e);}if(errors.Count!=0)throw new AggregateException(errors);}
    }
    private sealed class ResultLease:IToolResultView
    {
        private readonly IToolResultView _view;
        private readonly RuntimeGenerationLease _generation;
        private readonly ToolPresentationAdmission _admission;
        private CancellationTokenRegistration _retirement;
        private CancellationTokenRegistration _nodeRetirement;
        private readonly List<(EventHandler<ResultSelection> Original,EventHandler<ResultSelection> Guarded)> _selectionChanged = new();
        private int _disposed;
        internal ResultLease(IToolResultView view,RuntimeGenerationLease generation,ToolPresentationAdmission admission)
        {
            _view=view;_generation=generation;_admission=admission;
            _retirement=generation.RetirementToken.Register(()=>RetireOnDispatcher(view.View,Dispose));
            _nodeRetirement=admission.RetirementToken.Register(()=>RetireOnDispatcher(view.View,Dispose));
        }
        public FrameworkElement View=>_view.View;
        public IReadOnlyList<Port> Sources=>_view.Sources;
        public void Present(IResultPublication publication){_admission.EnsureCurrent();_view.Present(publication);}
        public void Clear()=>_view.Clear();
        public event EventHandler<ResultSelection>? SelectionHighlightChanged
        {
            add { if(value is null)return;_admission.EnsureCurrent();EventHandler<ResultSelection> guarded=(sender,selection)=>{if(Volatile.Read(ref _disposed)==0&&_admission.IsCurrent)value(sender,selection);};_selectionChanged.Add((value,guarded));_view.SelectionHighlightChanged+=guarded; }
            remove { if(value is null)return;var index=_selectionChanged.FindLastIndex(item=>item.Original==value);if(index<0)return;var handler=_selectionChanged[index].Guarded;_selectionChanged.RemoveAt(index);_view.SelectionHighlightChanged-=handler; }
        }
        public void Dispose(){if(Interlocked.Exchange(ref _disposed,1)!=0)return;var errors=new List<Exception>();foreach(var item in _selectionChanged.ToArray())try{_view.SelectionHighlightChanged-=item.Guarded;}catch(Exception e){errors.Add(e);}_selectionChanged.Clear();try{_nodeRetirement.Dispose();}catch(Exception e){errors.Add(e);}try{_retirement.Dispose();}catch(Exception e){errors.Add(e);}try{_view.Dispose();}catch(Exception e){errors.Add(e);}try{_generation.Dispose();}catch(Exception e){errors.Add(e);}if(errors.Count!=0)throw new AggregateException(errors);}
    }
    private static void RetireOnDispatcher(FrameworkElement element,Action dispose)
    {
        var dispatcher=element.Dispatcher;
        if(dispatcher.HasShutdownStarted)return; // the owning host must still dispose the view
        dispatcher.BeginInvoke(new Action(()=>{element.IsEnabled=false;dispose();}));
    }
}
