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
    private readonly IBindingPlanCompiler? _bindingCompiler;
    private readonly IBindingCompilationScope? _bindingScope;
    public ParameterEditSession(IEnumerable<Port> ports,IReadOnlyDictionary<string,ParameterSetting> initial,
        IBindingPlanCompiler? bindingCompiler=null,IBindingCompilationScope? bindingScope=null)
    {
        if((bindingCompiler is null)!=(bindingScope is null))throw new ArgumentException("Binding compiler and scope must be supplied together.");
        _bindingCompiler=bindingCompiler;_bindingScope=bindingScope;
        _ports=ports.ToDictionary(p=>p.Id,StringComparer.Ordinal); _committed=new(StringComparer.Ordinal);
        foreach(var (id,s) in initial){_committed[id]=Freeze(id,s);if(s is ConstantSetting c)_constantBackup[id]=ValueRules.Freeze(_ports[id],c.Value);}
    }
    private ParameterSetting Freeze(string id,ParameterSetting s)
    {
        var p=_ports[id];
        if(s is ConstantSetting c)
        {
            if (ContainsResource(p.Type))
                throw new ArgumentException("Resources cannot be stored as configuration constants; bind an invocation source instead.");
            var value=ValueRules.Freeze(p,c.Value);
            if(ValueRules.Images(value).Any())
                throw new ArgumentException("Borrowed images cannot be stored as configuration constants; bind an invocation source instead.");
            return new ConstantSetting(value);
        }
        if(s is ReferenceSetting b)
        {
            if(_bindingCompiler is not null)
            {
                var result=_bindingCompiler.CompileBinding(b.Reference,p,_bindingScope!);
                if(result.Plan is not null && result.Compatibility is BindingCompatibility.Compatible or BindingCompatibility.Conditional)return b;
                throw new ArgumentException(result.Diagnostic?.Message??"Binding could not be compiled.");
            }
            // Standalone foundation editing has no graph owner. It only supports the original exact direct reference.
            // Production editing supplies the existing parameter service and authoritative scope, including quantity adaptation.
            if(b.Reference.Type==p.Type && b.Reference.Selection is BindingSelection.AllItems && b.Reference.MemberFieldPath.Count==0)return b;
        }
        throw new ArgumentException("Invalid binding");
    }
    private static bool ContainsResource(TypeRef type) => type.Shape == ValueShape.Resource
        || (type.Element is not null && ContainsResource(type.Element))
        || type.Fields.Any(field => ContainsResource(field.Type));
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
