using System.Collections.ObjectModel;
using System.Globalization;
namespace SunEyeVision.Plugin.Contracts;
public abstract record ParameterSetting;
public sealed record ConstantSetting(object? Value) : ParameterSetting;
public sealed record ReferenceSetting(OutputReference Reference) : ParameterSetting;
/// <summary>UI-neutral transactional edit session. Invalid drafts never replace committed values.</summary>
public sealed class ParameterEditSession
{
    private readonly Dictionary<string,Port> _ports;
    private readonly Dictionary<string,ParameterSetting> _committed;
    private readonly Dictionary<string,ParameterSetting> _pending=new(StringComparer.Ordinal);
    private readonly Dictionary<string,string> _invalid=new(StringComparer.Ordinal);
    private readonly Dictionary<string,object?> _constantBackup=new(StringComparer.Ordinal);
    public ParameterEditSession(IEnumerable<Port> ports,IReadOnlyDictionary<string,ParameterSetting> initial)
    {
        _ports=ports.ToDictionary(p=>p.Id,StringComparer.Ordinal); _committed=new(StringComparer.Ordinal);
        foreach(var (id,s) in initial){_committed[id]=Freeze(id,s);if(s is ConstantSetting c)_constantBackup[id]=ValueRules.Freeze(_ports[id],c.Value);}
    }
    private ParameterSetting Freeze(string id,ParameterSetting s)
    {
        var p=_ports[id];
        if(s is ConstantSetting c)return new ConstantSetting(ValueRules.Freeze(p,c.Value));
        if(s is ReferenceSetting b && b.Reference.Type==p.Type && !string.IsNullOrWhiteSpace(b.Reference.NodeId) && !string.IsNullOrWhiteSpace(b.Reference.PortId))return b;
        throw new ArgumentException("Invalid binding");
    }
    public bool HasInvalidDraft=>_invalid.Count>0;
    public string? DraftError(string id)=>_invalid.GetValueOrDefault(id);
    public bool Set(string id,ParameterSetting setting)
    {
        try{var frozen=Freeze(id,setting);_pending[id]=frozen;_invalid.Remove(id);if(frozen is ConstantSetting c)_constantBackup[id]=c.Value;return true;}
        catch(ArgumentException ex){_invalid[id]=ex.Message;return false;}
    }
    public bool SetText(string id,string text)
    {
        var type=_ports[id].Type;
        object? value;
        try { value=type.Scalar switch {
            ScalarKind.Text=>text, ScalarKind.Float64=>double.Parse(text,CultureInfo.InvariantCulture),
            ScalarKind.Int64=>long.Parse(text,CultureInfo.InvariantCulture),ScalarKind.Boolean=>bool.Parse(text),
            _=>throw new FormatException("Use typed editor for this parameter")}; }
        catch(Exception ex) when(ex is FormatException or OverflowException){_invalid[id]=text;return false;}
        return Set(id,new ConstantSetting(value));
    }
    public bool RestoreConstant(string id)=>_constantBackup.TryGetValue(id,out var v)&&Set(id,new ConstantSetting(v));
    public bool TryCommit()
    {
        if(HasInvalidDraft)return false;
        foreach(var (id,s) in _pending)_committed[id]=Freeze(id,s);
        _pending.Clear();return true;
    }
    public IReadOnlyDictionary<string,ParameterSetting> Committed=>new ReadOnlyDictionary<string,ParameterSetting>(_committed.ToDictionary(p=>p.Key,p=>Freeze(p.Key,p.Value),StringComparer.Ordinal));
    public void Cancel(){_pending.Clear();_invalid.Clear();_constantBackup.Clear();foreach(var (id,s) in _committed)if(s is ConstantSetting c)_constantBackup[id]=c.Value;}
}
