using System.Collections.ObjectModel;
using System.Globalization;
namespace SunEyeVision.Plugin.Contracts;
public abstract record ParameterSetting;
public sealed record ConstantSetting(object? Value) : ParameterSetting;
public sealed record ReferenceSetting(OutputReference Reference) : ParameterSetting;
/// <summary>Remove an optional setting. Distinct from a present null or a present empty collection.</summary>
public sealed record UnsetSetting : ParameterSetting;
/// <summary>UI-neutral transactional edit session. Invalid drafts never replace committed values.</summary>
public sealed class ParameterEditSession
{
    private readonly Dictionary<string,Port> _ports;
    private readonly Dictionary<string,ParameterSetting> _committed;
    private readonly Dictionary<string,ParameterSetting> _pending=new(StringComparer.Ordinal);
    private readonly Dictionary<string,string> _invalid=new(StringComparer.Ordinal);
    private readonly Dictionary<string,object?> _constantBackup=new(StringComparer.Ordinal);
    private readonly IBindingPlanCompiler? _bindingCompiler;
    private IBindingCompilationScope? _bindingScope;
    private readonly object _prepareOwner = new();
    private readonly Dictionary<string,long> _fieldRevisions=new(StringComparer.Ordinal);
    private long _acceptanceGeneration;
    public EditScopeIdentity? Identity { get; private set; }
    public long DraftRevision { get; private set; }
    public ParameterEditSession(IEnumerable<Port> ports,IReadOnlyDictionary<string,ParameterSetting> initial,
        IBindingPlanCompiler? bindingCompiler=null,IBindingCompilationScope? bindingScope=null,EditScopeIdentity? identity=null)
    {
        if((bindingCompiler is null)!=(bindingScope is null))throw new ArgumentException("Binding compiler and scope must be supplied together.");
        if(identity is { } owned && (bindingScope is null || bindingScope.DocumentSessionId!=owned.DocumentSessionId
            || bindingScope.Consumer!=owned.Node || bindingScope.DocumentRevision!=owned.BaseConfigurationRevision))
            throw new ArgumentException("A document-owned draft requires its authoritative compilation scope.");
        _bindingCompiler=bindingCompiler;_bindingScope=bindingScope;
        Identity=identity;
        _ports=ports.ToDictionary(p=>p.Id,StringComparer.Ordinal); _committed=new(StringComparer.Ordinal);
        foreach(var (id,s) in initial){var frozen=Freeze(id,s);if(frozen is not UnsetSetting)_committed[id]=frozen;if(s is ConstantSetting c)_constantBackup[id]=ValueRules.Freeze(_ports[id],c.Value);}
    }
    private ParameterSetting Freeze(string id,ParameterSetting s)
        => Freeze(id,s,_bindingScope);
    private ParameterSetting Freeze(string id,ParameterSetting s,IBindingCompilationScope? scope)
    {
        var p=_ports[id];
        if(s is UnsetSetting)return !p.Required?s:throw new ArgumentException("A required field cannot be Unset.");
        if(p.EditModes!=FieldEditModes.None&&((s is ConstantSetting&&(p.EditModes&FieldEditModes.Constant)==0)
            ||(s is ReferenceSetting&&(p.EditModes&FieldEditModes.Reference)==0)))
            throw new ArgumentException("This setting mode is not allowed by the declared field capabilities.");
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
                var result=_bindingCompiler.CompileBinding(b.Reference,p,scope!);
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
        DraftRevision=checked(DraftRevision+1);
        _fieldRevisions[id]=DraftRevision;
        try{var frozen=Freeze(id,setting);_pending[id]=frozen;_invalid.Remove(id);if(frozen is ConstantSetting c)_constantBackup[id]=c.Value;return true;}
        catch(Exception ex) when(ex is ArgumentException or OverflowException){_invalid[id]=ex.Message;return false;}
    }
    public bool SetText(string id,string text)
    {
        var type=_ports[id].Type;
        object? value;
        try { value=type.Scalar switch {
            ScalarKind.Text=>text, ScalarKind.Float64=>double.Parse(text,CultureInfo.InvariantCulture),
            ScalarKind.Int64=>long.Parse(text,CultureInfo.InvariantCulture),ScalarKind.Boolean=>bool.Parse(text),
            _=>throw new FormatException("Use typed editor for this parameter")}; }
        catch(Exception ex) when(ex is FormatException or OverflowException){DraftRevision=checked(DraftRevision+1);_fieldRevisions[id]=DraftRevision;_invalid[id]=text;return false;}
        return Set(id,new ConstantSetting(value));
    }
    public bool RestoreConstant(string id)=>_constantBackup.TryGetValue(id,out var v)&&Set(id,new ConstantSetting(v));
    public bool Clear(string id)=>Set(id,new UnsetSetting());
    public bool TryCommit()
    {
        if(Identity is not null)throw new InvalidOperationException("A document-owned scope must prepare and acknowledge explicit publication.");
        if(HasInvalidDraft)return false;
        foreach(var (id,s) in _pending)if(s is UnsetSetting)_committed.Remove(id);else _committed[id]=Freeze(id,s);
        _pending.Clear();return true;
    }
    public IReadOnlyDictionary<string,ParameterSetting> Committed=>new ReadOnlyDictionary<string,ParameterSetting>(_committed.ToDictionary(p=>p.Key,p=>Freeze(p.Key,p.Value),StringComparer.Ordinal));
    public bool TryPrepare(EditScopeIdentity expectedScope,IBindingCompilationScope currentScope,
        out PreparedParameterEdit? prepared,out IReadOnlyList<Diagnostic> issues)
    {
        prepared=null;
        var errors=new List<Diagnostic>();
        if(Identity is null || Identity!=expectedScope || currentScope.DocumentSessionId!=expectedScope.DocumentSessionId
            || currentScope.Consumer!=expectedScope.Node || currentScope.DocumentRevision!=expectedScope.BaseConfigurationRevision)
            errors.Add(new("edit-scope","Draft scope or published configuration changed; explicit revalidation is required."));
        errors.AddRange(_invalid.Select(pair=>new Diagnostic("edit-draft",pair.Value,pair.Key)));
        var changes=new Dictionary<string,ParameterSetting>(StringComparer.Ordinal);
        if(errors.Count==0)
        foreach(var (id,setting) in _pending)
        {
            try{changes.Add(id,Freeze(id,setting,currentScope));}
            catch(Exception exception) when(exception is ArgumentException or OverflowException){errors.Add(new("edit-binding",exception.Message,id));}
        }
        issues=errors.AsReadOnly();
        if(errors.Count!=0)return false;
        prepared=new(_prepareOwner,_acceptanceGeneration,expectedScope,DraftRevision,new ReadOnlyDictionary<string,Port>(_ports),
            new ReadOnlyDictionary<string,ParameterSetting>(changes));
        return true;
    }
    /// <summary>Acknowledges an already published document transaction; this method itself never publishes.</summary>
    public ParameterEditAcknowledgement AcceptPublished(PreparedParameterEdit prepared,IReadOnlyDictionary<string,ParameterSetting> published,
        IBindingCompilationScope publishedScope,bool configurationChanged)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if(!prepared.OwnedBy(_prepareOwner)||prepared.AcceptanceGeneration!=_acceptanceGeneration||Identity!=prepared.Identity
            ||publishedScope.DocumentSessionId!=prepared.Identity.DocumentSessionId||publishedScope.Consumer!=prepared.Identity.Node
            ||publishedScope.DocumentRevision<prepared.Identity.BaseConfigurationRevision
            ||configurationChanged&&publishedScope.DocumentRevision==prepared.Identity.BaseConfigurationRevision)
            throw new InvalidOperationException("Published receipt does not match this prepared draft.");
        // Validate/copy the complete accepted projection before changing any session state.
        var next=published.ToDictionary(pair=>pair.Key,pair=>Freeze(pair.Key,pair.Value,publishedScope),StringComparer.Ordinal);
        if(next.Values.Any(value=>value is UnsetSetting))throw new ArgumentException("Published Unset fields must be absent from the accepted projection.");
        var late=DraftRevision!=prepared.DraftRevision;
        _committed.Clear();foreach(var (id,value) in next)_committed.Add(id,value);
        _bindingScope=publishedScope;
        Identity=new(prepared.Identity.DocumentSessionId,prepared.Identity.EditScopeId,prepared.Identity.Node,publishedScope.DocumentRevision);
        // Publication has already happened. A late UI acknowledgement refreshes the accepted baseline,
        // removes only unchanged prepared fields, and preserves newer edits/invalid raw text or a later Cancel.
        foreach(var id in _pending.Keys.Where(id=>_fieldRevisions.GetValueOrDefault(id)<=prepared.DraftRevision).ToArray())_pending.Remove(id);
        _constantBackup.Clear();foreach(var (id,value) in _committed)if(value is ConstantSetting constant)_constantBackup[id]=constant.Value;
        foreach(var (id,value) in _pending)if(value is ConstantSetting constant)_constantBackup[id]=constant.Value;
        _acceptanceGeneration=checked(_acceptanceGeneration+1);DraftRevision=checked(DraftRevision+1);
        return new(Identity.Value,DraftRevision,late);
    }
    public void Cancel(){DraftRevision=checked(DraftRevision+1);_pending.Clear();_invalid.Clear();_fieldRevisions.Clear();_constantBackup.Clear();foreach(var (id,s) in _committed)if(s is ConstantSetting c)_constantBackup[id]=c.Value;}
}
