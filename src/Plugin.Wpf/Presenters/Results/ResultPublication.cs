using System.Collections.Frozen;
using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Presenters.Results;

public enum ResultViewState
{
    Declaration, NotExecuted, Running, Success, PartialSuccess, Failed, Timeout, Cancelled
}

public sealed record ResultPublicationStamp(Guid PublicationId, RunIdentity? Invocation,
    long? SchemaRevision, string SchemaFingerprint);

/// <summary>Values are borrowed until this lease closes. Disposing the owner retires
/// the publication but does not release resources still borrowed by a view.</summary>
public interface IResultValueLease : IDisposable
{
    ResultPublicationStamp Stamp { get; }
    InputValues? Values { get; }
}

public interface IResultPublication : IDisposable
{
    ResultPublicationStamp Stamp { get; }
    ToolSchema Schema { get; }
    ResultViewState State { get; }
    IReadOnlyList<Diagnostic> Diagnostics { get; }
    IReadOnlyDictionary<string,string> DisplayNames { get; }
    IReadOnlySet<string> DisplayedPorts { get; }
    bool IsCurrent();
    IResultValueLease Borrow();
}

/// <summary>Owns exactly one transferred reader. Each view borrows that reader's
/// frozen values; neither a second result lookup nor a second SDK reader is needed.</summary>
public sealed class ResultPublication : IResultPublication
{
    private readonly object _gate=new();
    private readonly Func<bool> _isCurrent;
    private InputValues? _values;
    private IDisposable? _owner;
    private int _references=1;
    private bool _retired;

    // Ownership transfers only when this constructor returns successfully.
    public ResultPublication(ToolSchema schema,ResultPublicationStamp stamp,ResultViewState state,
        InputValues? values,IDisposable? owner,Func<bool> isCurrent,
        IReadOnlyList<Diagnostic>? diagnostics=null,IReadOnlyDictionary<string,string>? displayNames=null,
        IReadOnlySet<string>? displayedPorts=null)
    {
        ArgumentNullException.ThrowIfNull(schema);ArgumentNullException.ThrowIfNull(stamp);
        ArgumentNullException.ThrowIfNull(isCurrent);
        if(schema.Inputs.Count!=0)throw new ArgumentException("A result publication declares outputs only.",nameof(schema));
        if(stamp.PublicationId==Guid.Empty||stamp.SchemaFingerprint!=schema.Fingerprint||stamp.SchemaRevision<0)
            throw new ArgumentException("Publication stamp must identify this frozen output declaration.",nameof(stamp));
        if(!Enum.IsDefined(state))throw new ArgumentOutOfRangeException(nameof(state));
        if(state==ResultViewState.Declaration&&(stamp.Invocation is not null||stamp.SchemaRevision is not null||values is not null))
            throw new ArgumentException("A declaration does not invent a run, revision or result values.");
        if(state!=ResultViewState.Declaration&&(stamp.Invocation is null||stamp.SchemaRevision is null))
            throw new ArgumentException("An invocation publication requires its actual run and schema revision.");
        if((state is ResultViewState.Success or ResultViewState.PartialSuccess)!=(values is not null))
            throw new ArgumentException("Only completed successful publications have result values.");
        var ids=schema.Outputs.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
        if(displayNames?.Any(item=>!ids.Contains(item.Key)||item.Value is null)==true
            ||displayedPorts?.Any(id=>!ids.Contains(id))==true)
            throw new ArgumentException("Display metadata must reference declared output identities.");
        var issues=(diagnostics??Array.Empty<Diagnostic>()).ToArray();
        if(issues.Any(issue=>issue is null))throw new ArgumentException("Diagnostics cannot contain null.");
        if(values is not null)
        {
            if(values.Declarations.Count!=schema.Outputs.Count)throw new ArgumentException("Result values use another output declaration.");
            var declared=new ToolSchema(Array.Empty<Port>(),schema.Outputs.Select(port=>
                values.Declarations.TryGetValue(port.Id,out var actual)?actual:
                    throw new ArgumentException("Result values omit a declared output.")));
            if(declared.Fingerprint!=schema.Fingerprint)throw new ArgumentException("Result values use another output declaration.");
        }
        Schema=schema;Stamp=stamp;State=state;
        Diagnostics=Array.AsReadOnly(issues);
        DisplayNames=new ReadOnlyDictionary<string,string>((displayNames??new Dictionary<string,string>())
            .ToDictionary(item=>item.Key,item=>item.Value,StringComparer.Ordinal));
        DisplayedPorts=(displayedPorts??ids).ToFrozenSet(StringComparer.Ordinal);
        _isCurrent=isCurrent;_values=values;_owner=owner;
    }

    public ResultPublicationStamp Stamp {get;}
    public ToolSchema Schema {get;}
    public ResultViewState State {get;}
    public IReadOnlyList<Diagnostic> Diagnostics {get;}
    public IReadOnlyDictionary<string,string> DisplayNames {get;}
    public IReadOnlySet<string> DisplayedPorts {get;}
    public bool IsCurrent()
    {
        lock(_gate){if(_retired)return false;}
        var current=_isCurrent();
        lock(_gate){return current&&!_retired;}
    }
    public IResultValueLease Borrow()
    {
        lock(_gate)
        {
            ObjectDisposedException.ThrowIf(_retired,this);
            checked{_references++;}
            return new Lease(this);
        }
    }
    public void Dispose()
    {
        IDisposable? release;
        lock(_gate)
        {
            if(_retired)return;
            _retired=true;release=ReleaseLocked();
        }
        release?.Dispose();
    }
    private IDisposable? ReleaseLocked()
    {
        if(--_references!=0)return null;
        var owner=_owner;_owner=null;_values=null;
        return owner;
    }
    private sealed class Lease(ResultPublication publication) : IResultValueLease
    {
        private bool _closed;
        public ResultPublicationStamp Stamp=>publication.Stamp;
        public InputValues? Values
        {
            get{lock(publication._gate){ObjectDisposedException.ThrowIf(_closed,this);return publication._values;}}
        }
        public void Dispose()
        {
            IDisposable? release;
            lock(publication._gate)
            {
                if(_closed)return;
                _closed=true;release=publication.ReleaseLocked();
            }
            release?.Dispose();
        }
    }
}

public sealed class ResultHighlightTarget
{
    public ResultHighlightTarget(ImageFrameAddress imageFrame,CoordinateFrame coordinates,IEnumerable<GeometryValue> geometry)
    {
        ArgumentNullException.ThrowIfNull(imageFrame);ArgumentNullException.ThrowIfNull(coordinates);
        var items=geometry.ToArray();
        if(items.Any(item=>item is null||item.Frame!=coordinates))throw new ArgumentException("Mixed highlight coordinate frames.");
        ImageFrame=imageFrame;Coordinates=coordinates;Geometry=Array.AsReadOnly(items);
    }
    public ImageFrameAddress ImageFrame {get;}
    public CoordinateFrame Coordinates {get;}
    public IReadOnlyList<GeometryValue> Geometry {get;}
}

/// <summary>An empty Targets list clears only this publication's selection.</summary>
public sealed class ResultSelection
{
    public ResultSelection(ResultPublicationStamp publication,object? elementIdentity,IEnumerable<ResultHighlightTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(publication);
        if(elementIdentity is not null and not string and not long)throw new ArgumentException("Selection identity must be a declared immutable text or integer.");
        var items=targets.ToArray();
        if(items.Any(item=>item is null)||elementIdentity is null&&items.Length!=0)throw new ArgumentException("A highlight requires an element identity.");
        Publication=publication;ElementIdentity=elementIdentity;Targets=Array.AsReadOnly(items);
    }
    public ResultPublicationStamp Publication {get;}
    public object? ElementIdentity {get;}
    public IReadOnlyList<ResultHighlightTarget> Targets {get;}
}
