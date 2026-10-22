using System.Windows; using System.Windows.Controls;
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
    private readonly ParameterEditSession _session;
    private readonly string _parameterId;
    public ParameterEditorControl(ParameterEditSession session,string parameterId)
    {
        _session=session;_parameterId=parameterId;
        var panel=new StackPanel();panel.Children.Add(Input);panel.Children.Add(Commit);panel.Children.Add(Error);Content=panel;
        RefreshFromCommitted();
        Input.TextChanged+=(_,_)=>{if(_refreshing)return;session.SetText(parameterId,Input.Text);Error.Text=session.DraftError(parameterId)??"";};
        Commit.Click+=(_,_)=>{LastCommitSucceeded=session.TryCommit();Error.Text=LastCommitSucceeded?"":session.DraftError(parameterId)??"Invalid draft";};
    }
    public void RefreshFromCommitted()
    {
        _refreshing=true;
        try
        {
            var value=_session.Committed[_parameterId];
            Input.IsReadOnly=value is ReferenceSetting;
            Input.Text=value switch
            {
                ConstantSetting {Value:IFormattable v}=>v.ToString(null,System.Globalization.CultureInfo.InvariantCulture),
                ConstantSetting c=>c.Value?.ToString()??"",
                ReferenceSetting b=>$"{b.Reference.NodeId}.{b.Reference.PortId}",
                _=>""
            };
            Error.Text="";
        }
        finally{_refreshing=false;}
    }
}
