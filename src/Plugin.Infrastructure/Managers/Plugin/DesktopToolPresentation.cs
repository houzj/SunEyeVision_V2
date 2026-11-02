using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

/// <summary>Desktop-only projections of an explicitly admitted runtime generation.</summary>
public static class DesktopToolPresentation
{
    public static IToolSettingsView CreateSettingsView(RuntimeDirectorySnapshot directory,string toolId,FieldSettings fields,ToolSettingsViewOptions? options=null)
    {
        var generation=directory.AcquireInvocation();
        IToolSettingsView? inner=null;
        try
        {
            var draft=new ToolSettingsEditSession(directory.CreateDefinition(toolId),fields);
            var editor=directory.Components.SingleOrDefault(c=>c.Metadata.Role==ToolComponentRole.Editor&&c.Metadata.ToolId==toolId);
            options??=new();
            inner=editor is null ? new DeclaredSettingsView(draft,options) :
                (Activator.CreateInstance(editor.EntryType) as IToolPresentationFactory??throw new InvalidOperationException("Runtime settings require a presentation factory.")).CreateSettingsView(draft,options)
                ??throw new InvalidOperationException("Settings view factory returned null.");
            return new SettingsLease(inner,generation);
        }
        catch(Exception failure){var errors=new List<Exception>{failure};try{inner?.Dispose();}catch(Exception cleanup){errors.Add(cleanup);}try{generation.Dispose();}catch(Exception cleanup){errors.Add(cleanup);}if(errors.Count>1)throw new AggregateException(errors);throw;}
    }
    public static IToolResultView? CreateCurrentResultView(RuntimeDirectorySnapshot directory,string toolId)
    {
        var generation=directory.AcquireInvocation();
        IToolResultView? inner=null;
        try
        {
            if(directory.CreateDefinition(toolId) is not IToolResultPresentationProvider provider){generation.Dispose();return null;}
            var schema=provider.ResultSchema;var projection=provider.ResultProjection;
            var errors=projection.Validate(schema);
            if(errors.Count!=0)throw new InvalidOperationException("Invalid runtime result presentation: "+string.Join("; ",errors.Select(e=>e.Message)));
            if(projection.Kind is ResultProjectionKind.None or ResultProjectionKind.PropertyList){generation.Dispose();return null;}
            var editor=directory.Components.SingleOrDefault(c=>c.Metadata.Role==ToolComponentRole.Editor&&c.Metadata.ToolId==toolId);
            inner=editor is not null ?
                (Activator.CreateInstance(editor.EntryType) as IToolPresentationFactory??throw new InvalidOperationException("Result declaration requires a presentation factory.")).CreateResultView(schema,projection)
                    ??throw new InvalidOperationException("Result view factory returned null.") :
                projection.Kind switch {ResultProjectionKind.StandardTable=>new DeclaredResultTable(schema,projection),_=>throw new NotSupportedException("No result renderer is registered for: "+projection.RendererKey)};
            return new ResultLease(inner,generation);
        }
        catch(Exception failure){var errors=new List<Exception>{failure};try{inner?.Dispose();}catch(Exception cleanup){errors.Add(cleanup);}try{generation.Dispose();}catch(Exception cleanup){errors.Add(cleanup);}if(errors.Count>1)throw new AggregateException(errors);throw;}
    }
    private sealed class SettingsLease:IToolSettingsView
    {
        private readonly IToolSettingsView _view;
        private readonly RuntimeGenerationLease _generation;
        private CancellationTokenRegistration _retirement;
        private int _disposed;
        internal SettingsLease(IToolSettingsView view,RuntimeGenerationLease generation)
        {
            _view=view;_generation=generation;
            _retirement=generation.RetirementToken.Register(()=>RetireOnDispatcher(view.View,Dispose));
        }
        public FrameworkElement View=>_view.View;
        public bool HasInvalidInput=>_view.HasInvalidInput;
        public bool HasPendingEdits=>_view.HasPendingEdits;
        public long AcceptedRevision=>_view.AcceptedRevision;
        public IReadOnlyList<string> AcceptedFieldOrder=>_view.AcceptedFieldOrder;
        public FieldSettings CaptureAcceptedFields()=>_view.CaptureAcceptedFields();
        public event Action? Changed {add=>_view.Changed+=value;remove=>_view.Changed-=value;}
        public bool TryCapture(out FieldSettings fields,out string error)=>_view.TryCapture(out fields,out error);
        public void Dispose(){if(Interlocked.Exchange(ref _disposed,1)!=0)return;var errors=new List<Exception>();try{_retirement.Dispose();}catch(Exception e){errors.Add(e);}try{_view.Dispose();}catch(Exception e){errors.Add(e);}try{_generation.Dispose();}catch(Exception e){errors.Add(e);}if(errors.Count!=0)throw new AggregateException(errors);}
    }
    private sealed class ResultLease:IToolResultView
    {
        private readonly IToolResultView _view;
        private readonly RuntimeGenerationLease _generation;
        private CancellationTokenRegistration _retirement;
        private int _disposed;
        internal ResultLease(IToolResultView view,RuntimeGenerationLease generation)
        {
            _view=view;_generation=generation;
            _retirement=generation.RetirementToken.Register(()=>RetireOnDispatcher(view.View,Dispose));
        }
        public FrameworkElement View=>_view.View;
        public IReadOnlyList<Port> Sources=>_view.Sources;
        public void Present(IResultPublication publication)=>_view.Present(publication);
        public void Clear()=>_view.Clear();
        public event EventHandler<ResultSelection>? SelectionHighlightChanged {add=>_view.SelectionHighlightChanged+=value;remove=>_view.SelectionHighlightChanged-=value;}
        public void Dispose(){if(Interlocked.Exchange(ref _disposed,1)!=0)return;var errors=new List<Exception>();try{_retirement.Dispose();}catch(Exception e){errors.Add(e);}try{_view.Dispose();}catch(Exception e){errors.Add(e);}try{_generation.Dispose();}catch(Exception e){errors.Add(e);}if(errors.Count!=0)throw new AggregateException(errors);}
    }
    private static void RetireOnDispatcher(FrameworkElement element,Action dispose)
    {
        var dispatcher=element.Dispatcher;
        if(dispatcher.HasShutdownStarted)return; // the owning host must still dispose the view
        dispatcher.BeginInvoke(new Action(()=>{element.IsEnabled=false;dispose();}));
    }
}