using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
namespace SunEyeVision.Plugin.Wpf;
/// <summary>Minimal editor contract consumer, not the production editor replacement.</summary>
public sealed class ParameterEditorControl : UserControl
{
    public TextBox Input { get; }=new();
    public TextBlock Error { get; }=new();
    public Button Commit { get; }=new(){Content="提交"};
    public bool LastCommitSucceeded { get; private set; }
    private bool _refreshing;
    private bool _readOnly;
    private readonly ParameterEditSession _session;
    private readonly string _parameterId;
    public ParameterEditorControl(ParameterEditSession session,string parameterId)
    {
        _session=session;_parameterId=parameterId;
        var panel=new StackPanel();panel.Children.Add(Input);panel.Children.Add(Commit);panel.Children.Add(Error);Content=panel;
        RefreshFromCommitted();
        Input.TextChanged+=(_,_)=>{if(_refreshing||_readOnly)return;session.SetText(parameterId,Input.Text);Error.Text=session.DraftError(parameterId)??"";};
        Commit.Click+=(_,_)=>{
            if(_readOnly){LastCommitSucceeded=false;return;}
            LastCommitSucceeded=session.TryCommit();Error.Text=LastCommitSucceeded?"":session.DraftError(parameterId)??"Invalid draft";
        };
    }
    public void RefreshFromCommitted()
    {
        _refreshing=true;
        try
        {
            var value=_session.Committed[_parameterId];
            _readOnly=value is ReferenceSetting or ConstantSetting {Value:SequenceValue or RecordValue};
            Input.IsReadOnly=_readOnly;
            Commit.IsEnabled=!_readOnly;
            LastCommitSucceeded=false;
            Input.Text=value switch
            {
                ConstantSetting {Value:SequenceValue sequence}=>Project(sequence,sequence.Declaration),
                ConstantSetting {Value:RecordValue record}=>Project(record,record.Declaration),
                ConstantSetting {Value:IFormattable v}=>v.ToString(null,CultureInfo.InvariantCulture),
                ConstantSetting c=>c.Value?.ToString()??"",
                ReferenceSetting b=>$"{b.Reference.NodeId}.{b.Reference.PortId}",
                _=>""
            };
            Error.Text="";
        }
        finally{_refreshing=false;}
    }

    // Traverse only the frozen schema and its closed carriers. Resource and geometry
    // leaves are labels: projection never inspects arbitrary objects or payloads.
    private static string Project(object? value,TypeRef declaration)
    {
        if(value is null)return "<null>";
        if(declaration.Shape==ValueShape.Resource)return $"<resource:{declaration.Resource}>";
        if(declaration.Shape==ValueShape.Geometry)return $"<geometry:{declaration.Geometry}>";
        if(value is RecordValue record)
        {
            if(declaration.Fields.Count==0)return "{ <empty> }";
            return "{ "+string.Join(", ",declaration.Fields.Select(field=>
                JsonSerializer.Serialize(field.Id)+" = "+(record.Contains(field.Id)
                    ?Project(record.Get<object?>(field.Id),field.Type):"<missing>")))+" }";
        }
        if(value is SequenceValue sequence)
        {
            if(sequence.Count==0)return "[ <empty> ]";
            return "[ "+string.Join(", ",Enumerable.Range(0,sequence.Count).Select(index=>
                "["+index.ToString(CultureInfo.InvariantCulture)+"] = "+Project(sequence[index],declaration.Element!)))+" ]";
        }
        // The frozen Float64 sequence contract also permits its original array carrier.
        if(declaration.Shape==ValueShape.Sequence&&value is double[] numbers)
        {
            if(numbers.Length==0)return "[ <empty> ]";
            return "[ "+string.Join(", ",numbers.Select((number,index)=>
                "["+index.ToString(CultureInfo.InvariantCulture)+"] = "+number.ToString(null,CultureInfo.InvariantCulture)))+" ]";
        }
        return value switch
        {
            string text=>text.Length==0?"\"\" <empty>":JsonSerializer.Serialize(text),
            bool boolean=>boolean?"true":"false",
            long integer=>integer.ToString(CultureInfo.InvariantCulture),
            double number=>number.ToString(null,CultureInfo.InvariantCulture),
            _=>"<unsupported>"
        };
    }
}
