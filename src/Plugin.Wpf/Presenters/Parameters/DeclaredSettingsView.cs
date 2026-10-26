using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Common settings view over Runtime metadata and an independent private model.
/// It does not execute, commit the document, resolve references or keep resources.</summary>
public sealed class DeclaredSettingsView : ContentControl,IToolSettingsView
{
    private readonly IToolSettingsDraft _draft;
    private readonly Dictionary<string,ReferenceSetting> _references;
    private readonly DeclaredFieldsView _fields;
    private bool _disposed;
    public FrameworkElement View=>this;
    public bool HasInvalidInput=>!_disposed&&_fields.HasInvalidActiveFields;
    public event Action? Changed;
    public DeclaredSettingsView(IToolSettingsDraft draft,ToolSettingsViewOptions? options=null)
    {
        _draft=draft??throw new ArgumentNullException(nameof(draft));options??=new();
        var schema=draft.Schema;var values=draft.ReadFields();
        if(schema.Presentation is null)throw new ArgumentException("Runtime settings require explicit presentation metadata.");
        _references=options.References?.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal)??new(StringComparer.Ordinal);
        foreach(var reference in _references)
            if(!schema.Inputs.Any(p=>p.Id==reference.Key&&(p.EditModes&FieldEditModes.Reference)!=0))
                throw new ArgumentException("Unknown or non-reference settings field: "+reference.Key);
        _fields=new(schema.Presentation.Fields.Select(presentation=>
        {
            var port=schema.Inputs.Single(p=>p.Id==presentation.FieldId);
            ParameterSetting initial=_references.TryGetValue(port.Id,out var reference)?reference:new ConstantSetting(values.Get<object?>(port.Id));
            return(port,presentation,initial);
        }),Write,options.SelectReference,id=>new ConstantSetting(_draft.ReadFields().Get<object?>(id)),options.FormatReference);
        foreach(var field in _fields.Fields)field.Changed+=OnChanged;
        Content=_fields;
    }
    private void Write(string id,ParameterSetting value)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(value is ConstantSetting constant)
        {_draft.ApplyFields(new Dictionary<string,object?>{{id,constant.Value}});_references.Remove(id);}
        else if(value is ReferenceSetting reference)
        {
            var port=_draft.Schema.Inputs.Single(p=>p.Id==id);
            if((port.EditModes&FieldEditModes.Reference)==0)throw new ArgumentException("Settings field does not allow references.");
            _references[id]=reference;
        }
        else throw new ArgumentException("Settings require a current value or an explicit reference.");
    }
    private void OnChanged()=>Changed?.Invoke();
    public bool TryCapture(out object settings,out IReadOnlyDictionary<string,ReferenceSetting> references,out string error)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        settings=_draft.CaptureSettings();references=new ReadOnlyDictionary<string,ReferenceSetting>(new Dictionary<string,ReferenceSetting>(_references,StringComparer.Ordinal));
        return _fields.TryValidate(out error);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        foreach(var field in _fields.Fields)field.Changed-=OnChanged;
        Changed=null;Content=null;
    }
}
