using System.Globalization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;

public enum ResultPresentationValueState { Unavailable, Missing, Null, Available, ResourcePending }

/// <summary>A declaration or one explicitly identified run; it never reads a latest-result cache.</summary>
/// <remarks>
/// 原件模块结果树按工具自己的显示声明组织：一行=一个输出端口或该端口内的一条成员路径。
/// 记录型序列端口按成员转置成行（每个成员一行，值为全部元素的该成员），不再展开元素索引或分页行。
/// </remarks>
public sealed class ResultPresentationModel
{
    private ResultPresentationModel(ToolSchema schema, ResultPublicationStamp stamp, ResultViewState state, InputValues? values, Func<bool>? isCurrent, ResultValueAccess access)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        if (state is ResultViewState.Success or ResultViewState.PartialSuccess) ArgumentNullException.ThrowIfNull(values);
        var hasValues=values is not null&&(state is ResultViewState.Success or ResultViewState.PartialSuccess or ResultViewState.Failed or ResultViewState.Timeout);
        Schema = schema;
        Stamp=stamp;
        Identity = stamp.Invocation;
        State = state;
        Outputs=BuildRows(schema,hasValues,access);
    }

    public ResultPublicationStamp Stamp { get; }
    public ToolSchema Schema { get; }
    public RunIdentity? Identity { get; }
    public ResultViewState State { get; }
    public IReadOnlyList<ResultPresentationNode> Outputs { get; }

    public static ResultPresentationModel ForPublication(IResultPublication publication,IResultValueLease lease,Func<bool>? isCurrent=null)
    {
        ArgumentNullException.ThrowIfNull(publication);ArgumentNullException.ThrowIfNull(lease);
        var stamp=publication.Stamp;
        if(!publication.IsCurrent()||stamp!=lease.Stamp||stamp.SchemaFingerprint!=publication.Schema.Fingerprint)
            throw new InvalidOperationException("Result publication lease is stale or has a different identity.");
        var model=new ResultPresentationModel(publication.Schema,stamp,publication.State,lease.Values,isCurrent,new ResultValueAccess(publication,lease,stamp,isCurrent));
        if(!publication.IsCurrent()||publication.Stamp!=stamp||lease.Stamp!=stamp||isCurrent?.Invoke()==false)
            throw new InvalidOperationException("Result publication changed while reading declared values.");
        return model;
    }

    internal static string PortKey(string id) => "p" + id.Length.ToString(CultureInfo.InvariantCulture) + ":" + id;

    private static IReadOnlyList<ResultPresentationNode> BuildRows(ToolSchema schema,bool hasValues,ResultValueAccess access)
    {
        var rows=new List<ResultPresentationNode>();
        var declared=new HashSet<string>(StringComparer.Ordinal);
        foreach(var display in schema.OutputDisplay)
        {
            var port=Port(schema,display.PortId??display.FieldId,display.FieldId);
            if(display.PortId is null)declared.Add(port.Id);
            if(!display.DisplayInResultPanel)continue;
            rows.Add(Build(display,port,Array.Empty<string>(),hasValues,access,root:true));
        }
        // 显示声明未列出的端口追加在已声明项之后；声明过的端口按声明顺序显示，不重复出现。
        foreach(var port in schema.Outputs)
        {
            if(declared.Remove(port.Id))continue;
            if(schema.OutputDisplay.Any(item=>item.PortId==port.Id))continue;
            rows.Add(Build(new OutputDisplayNode(port.Id,schema.FindOutputDisplay(port.Id)?.DisplayName??port.Id),port,Array.Empty<string>(),hasValues,access,root:true));
        }
        return Array.AsReadOnly(rows.ToArray());
    }

    private static ResultPresentationNode Build(OutputDisplayNode display,Port port,IReadOnlyList<string> inheritedPath,bool hasValues,ResultValueAccess access,bool root=false)
    {
        // 根节点本身就是端口行；子节点默认继承父端口并追加自己的 FieldId，显式 PortId 时使用声明的成员路径。
        var path=root?Array.Empty<string>():display.PortId is null?inheritedPath.Append(display.FieldId).ToArray():display.MemberPath.ToArray();
        var (declaration,storage)=Resolve(port,path);
        var transposed=!root&&path.Length!=0&&IsSequence(port.Type);
        var children=new List<ResultPresentationNode>();
        foreach(var child in display.Children)
        {
            var childPort=child.PortId is null?port:Port(access.Schema,child.PortId,child.FieldId);
            children.Add(Build(child,childPort,path,hasValues,access));
        }
        if(children.Count==0&&path.Length==0&&declaration.Shape==ValueShape.Record)
            foreach(var field in declaration.Fields)
                children.Add(Build(new OutputDisplayNode(field.Id,field.Id),port,path,hasValues,access));
        var reader=hasValues&&declaration.Shape!=ValueShape.Resource
            ?new ResultRowReader(access,port.Id,path,declaration,transposed)
            :null;
        return new ResultPresentationNode(Key(port.Id,path,display.FieldId),display.DisplayName,declaration,port,
            Array.AsReadOnly(path),transposed,storage,reader,Array.AsReadOnly(children.ToArray()),access);
    }

    private static string Key(string portId,IReadOnlyList<string> path,string fieldId)
        =>PortKey(portId)+(path.Count==0?"":"/m"+string.Join(".",path))+"#"+fieldId;

    private static Port Port(ToolSchema schema,string portId,string fieldId)
        =>schema.Outputs.SingleOrDefault(item=>item.Id==portId)
            ??throw new ArgumentException("输出显示声明指向未声明的端口："+portId+"（行 "+fieldId+"）");

    internal static bool IsSequence(TypeRef type)=>type.Shape==ValueShape.Sequence;

    private static (TypeRef Declaration,NumericStorage? Storage) Resolve(Port port,IReadOnlyList<string> path)
    {
        var type=port.Type;NumericStorage? storage=port.Storage;
        foreach(var member in path)
        {
            while(type.Shape==ValueShape.Sequence)type=type.Element!;
            if(type.Shape==ValueShape.Record)
            {
                var field=type.Fields.SingleOrDefault(item=>item.Id==member)
                    ??throw new ArgumentException("输出显示声明指向未声明的成员："+port.Id+" / "+member);
                storage=field.Storage;type=field.Type;
            }
            else
            {
                type=OutputDisplayNode.ResolveMember(type,member);storage=null;
            }
        }
        return(type,storage);
    }
}

/// <summary>Immutable declaration identity, independent of a value lease or a UI display key.</summary>
public sealed class ResultDeclarationAddress
{
    internal ResultDeclarationAddress(Port rootPort,IEnumerable<string> members,bool throughSequence)
    {RootPort=rootPort;MemberFieldPath=Array.AsReadOnly(members.ToArray());ThroughSequence=throughSequence;}
    public Port RootPort {get;}
    public IReadOnlyList<string> MemberFieldPath {get;}
    /// <summary>该行取值来自端口序列的逐元素成员（绑定语义为全部元素）。</summary>
    public bool ThroughSequence {get;}
    public OutputReference Reference(string nodeId,string scopeId)=>new(nodeId,RootPort.Id,RootPort.Type,MemberFieldPath,BindingSelection.All,scopeId,RootPort.Storage);
}

/// <summary>结果树一行：端口本身、端口成员，或只承载分组的声明行。</summary>
public sealed class ResultPresentationNode
{
    private readonly ResultRowReader? _reader;
    private readonly GeometryValue? _geometry;
    private readonly ResultValueAccess _access;
    private bool Current=>_access.IsCurrent;

    internal ResultPresentationNode(string key,string displayName,TypeRef declaration,Port port,IReadOnlyList<string> memberPath,
        bool throughSequence,NumericStorage? storage,ResultRowReader? reader,IReadOnlyList<ResultPresentationNode> children,ResultValueAccess access)
    {
        ArgumentNullException.ThrowIfNull(port);
        Key=key;DisplayName=displayName;Declaration=declaration;DeclaredPort=port;MemberPath=memberPath;
        ThroughSequence=throughSequence;PhysicalStorage=storage;_reader=reader;_access=access;
        ValueState = declaration.Shape==ValueShape.Resource ? ResultPresentationValueState.ResourcePending
            : reader is null ? ResultPresentationValueState.Unavailable
            : reader.State;
        _geometry=reader?.Geometry;
        DeclarationAddress=new ResultDeclarationAddress(port,memberPath,throughSequence);
        Children=Array.AsReadOnly(children.ToArray());
    }

    public string Key { get; }
    public string DisplayName { get; }
    public TypeRef Declaration { get; }
    public Port DeclaredPort { get; }
    public IReadOnlyList<string> MemberPath { get; }
    /// <summary>该行取值来自端口序列的逐元素成员（原件「按成员转置成列」）。</summary>
    public bool ThroughSequence { get; }
    public NumericStorage? PhysicalStorage { get; }
    public ResultDeclarationAddress DeclarationAddress { get; }
    public IReadOnlyList<ResultPresentationNode> Children { get; }
    public bool IsGroup=>Children.Count!=0;
    public int ChildCount=>Children.Count;
    public bool IsExpanded { get; set; }
    public ResultPresentationValueState ValueState { get; }
    public GeometryValue? GeometryValue => Current?_geometry:null;
    /// <summary>序列行（整端口标量数组或成员转置行）的观测元素数。</summary>
    public int? SequenceLength => Current&&_reader is not null?_reader.Count:null;
    /// <summary>非序列行的裸标量；序列行请用 <see cref="SequenceItem"/>。</summary>
    public object? ScalarValue => Current?_reader?.Value:null;
    /// <summary>序列行第 index 个元素的裸值；几何多值成员返回数组。</summary>
    public object? SequenceItem(int index)=>Current?_reader?.ValueAt(index):null;
}

/// <summary>裸值文本：原件模块结果树只在单行内拼接，不做枚举中文化。</summary>
public static class ResultValueText
{
    /// <summary>原件 VmIO.cs:374-385：标量数组/成员转置行以 ";" 拼接，最多 64 个值，超出以 "…" 收尾。</summary>
    public static string JoinSequence(ResultPresentationNode node)
    {
        if(node.SequenceLength is not int total)return "值不可用";
        if(total==0)return SequenceText(total);
        const int max=64;
        var shown=Math.Min(total,max);
        var parts=new List<string>();
        var overflow=total>shown;
        for(var index=0;index<shown;index++)
        {
            var value=node.SequenceItem(index);
            if(value is double[] numbers)
            {
                foreach(var number in numbers)
                {
                    if(parts.Count>=max){overflow=true;break;}
                    parts.Add(FormatScalar(number,node.PhysicalStorage));
                }
                if(parts.Count>=max)break;
                continue;
            }
            var text=FormatScalar(value,node.PhysicalStorage);
            if(text.Length==0)continue;
            if(parts.Count>=max){overflow=true;break;}
            parts.Add(text);
        }
        var joined=string.Join(";",parts);
        return overflow?joined+";…":joined;
    }
    public static string SequenceText(int? count)=>count is null?"值不可用":count==0?"空序列 (0)":"["+count.Value.ToString(CultureInfo.InvariantCulture)+" 项]";
    public static string FormatScalar(object? value,NumericStorage? storage)=>value switch
    {
        string text=>text,
        bool boolean=>boolean?"true":"false",
        long integer=>integer.ToString(CultureInfo.CurrentCulture),
        double number when storage==NumericStorage.Float32=>((float)number).ToString(CultureInfo.CurrentCulture),
        double number=>number.ToString(CultureInfo.CurrentCulture),
        _=>""
    };
}

/// <summary>Only weak access to the consumer-owned lease. Never captures values or resource readers.</summary>
internal sealed class ResultValueAccess
{
    private readonly WeakReference<IResultPublication> _publication;
    private readonly WeakReference<IResultValueLease> _lease;
    private readonly ResultPublicationStamp _stamp;
    private readonly Func<bool>? _isCurrent;
    internal ResultValueAccess(IResultPublication publication,IResultValueLease lease,ResultPublicationStamp stamp,Func<bool>? isCurrent)
    {_publication=new(publication);_lease=new(lease);_stamp=stamp;_isCurrent=isCurrent;}
    internal bool IsCurrent=>_isCurrent?.Invoke()!=false&&TryValues(out _);
    internal ToolSchema Schema=>_publication.TryGetTarget(out var publication)?publication.Schema:throw new InvalidOperationException("结果发布已退役。");
    internal bool TryValues(out InputValues? values)
    {
        values=null;
        if(!_publication.TryGetTarget(out var publication)||!_lease.TryGetTarget(out var lease))return false;
        try
        {
            if(publication.Stamp!=_stamp||lease.Stamp!=_stamp||!publication.IsCurrent())return false;
            var current=lease.Values;
            if(publication.Stamp!=_stamp||lease.Stamp!=_stamp||!publication.IsCurrent())return false;
            values=current;return true;
        }
        catch(ObjectDisposedException){return false;}
    }
}

/// <summary>按端口成员路径读取一行值；路径步骤在构造时解析成字段/几何属性，读取时沿冻结值逐级前进。</summary>
internal sealed class ResultRowReader
{
    private enum StepKind { Field, GeometryMember }
    private readonly record struct Step(StepKind Kind,string Id);
    private readonly ResultValueAccess _access;
    private readonly string _port;
    private readonly Step[] _steps;
    private readonly TypeRef _declaration;
    private readonly bool _transposed;
    internal ResultRowReader(ResultValueAccess access,string port,IReadOnlyList<string> memberPath,TypeRef declaration,bool transposed)
    {
        _access=access;_port=port;_declaration=declaration;_transposed=transposed;
        _steps=BuildSteps(access.Schema,port,memberPath);
        var (state,value,count,geometry)=Read();
        State=state;Value=value;Count=count;Geometry=geometry;
    }
    internal ResultPresentationValueState State {get;}
    internal object? Value {get;}
    internal int? Count {get;}
    internal GeometryValue? Geometry {get;}
    internal object? ValueAt(int index)
    {
        if(!Root(out var root)||root is null||root.IsMissing||root.IsNull)return null;
        if(index<0||index>=root.Count)throw new ArgumentOutOfRangeException(nameof(index));
        var element=root.GetElement(index);
        if(_transposed)return Project(element);
        // 整端口标量序列（原件"单体状态"等）：元素本身就是标量。
        return _declaration.Element!.Shape==ValueShape.Scalar?Scalar(element,_declaration.Element.Scalar!.Value):null;
    }
    private (ResultPresentationValueState,object?,int?,GeometryValue?) Read()
    {
        if(!Root(out var root))return(ResultPresentationValueState.Unavailable,null,null,null);
        if(root is null)return(ResultPresentationValueState.Unavailable,null,null,null);
        if(root.IsMissing)return(ResultPresentationValueState.Missing,null,null,null);
        if(root.IsNull)return(ResultPresentationValueState.Null,null,null,null);
        if(_transposed)return(ResultPresentationValueState.Available,null,root.Count,null);
        if(_steps.Length==0)
        {
            if(_declaration.Shape==ValueShape.Sequence)return(ResultPresentationValueState.Available,null,root.Count,null);
            if(_declaration.Shape==ValueShape.Geometry)return(ResultPresentationValueState.Available,null,null,root.GetGeometry());
            if(_declaration.Shape!=ValueShape.Scalar)return(ResultPresentationValueState.Available,null,null,null);
            return(ResultPresentationValueState.Available,Scalar(root,_declaration.Scalar!.Value),null,null);
        }
        var value=Project(root);
        return value is GeometryValue geometry
            ?(ResultPresentationValueState.Available,geometry,null,geometry)
            :(ResultPresentationValueState.Available,value,null,null);
    }
    private object? Project(DeclaredValueReader reader)
    {
        var current=reader;
        for(var index=0;index<_steps.Length;index++)
        {
            var step=_steps[index];
            if(step.Kind==StepKind.Field)
            {
                current=current.GetField(step.Id);
                if(current.IsMissing||current.IsNull)return null;
                if(index==_steps.Length-1)
                {
                    if(_declaration.Shape==ValueShape.Geometry)return current.GetGeometry();
                    return _declaration.Shape==ValueShape.Scalar?Scalar(current,_declaration.Scalar!.Value):null;
                }
                continue;
            }
            return GeometryMember(current.GetGeometry(),step.Id);
        }
        return _declaration.Shape==ValueShape.Scalar?Scalar(current,_declaration.Scalar!.Value):null;
    }
    private static object? GeometryMember(GeometryValue geometry,string member)=>geometry switch
    {
        PolygonGeometry polygon when member=="count"=>(long)polygon.Vertices.Count,
        PolygonGeometry polygon when member=="x"=>(object)polygon.Vertices.Select(point=>point.X).ToArray(),
        PolygonGeometry polygon when member=="y"=>(object)polygon.Vertices.Select(point=>point.Y).ToArray(),
        _=>throw new ArgumentException("不支持的几何成员投影："+member)
    };
    private static object? Scalar(DeclaredValueReader reader,ScalarKind kind)=>kind switch
    {
        ScalarKind.Boolean=>reader.GetScalar<bool>(),
        ScalarKind.Int64=>reader.GetScalar<long>(),
        ScalarKind.Float64=>reader.GetScalar<double>(),
        ScalarKind.Text=>reader.GetScalar<string>(),
        _=>throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private bool Root(out DeclaredValueReader? reader)
    {
        reader=null;
        if(!_access.TryValues(out var values)||values is null)return false;
        reader=values.Read(_port);return true;
    }
    private static Step[] BuildSteps(ToolSchema schema,string portId,IReadOnlyList<string> memberPath)
    {
        if(memberPath.Count==0)return Array.Empty<Step>();
        var type=schema.Outputs.Single(port=>port.Id==portId).Type;
        var steps=new Step[memberPath.Count];
        for(var index=0;index<memberPath.Count;index++)
        {
            while(type.Shape==ValueShape.Sequence)type=type.Element!;
            var member=memberPath[index];
            if(type.Shape==ValueShape.Record)
            {
                var field=type.Fields.SingleOrDefault(item=>item.Id==member)
                    ??throw new ArgumentException("输出显示声明指向未声明的成员："+portId+" / "+member);
                steps[index]=new(StepKind.Field,member);type=field.Type;
                continue;
            }
            type=OutputDisplayNode.ResolveMember(type,member);
            steps[index]=new(StepKind.GeometryMember,member);
        }
        return steps;
    }
}
