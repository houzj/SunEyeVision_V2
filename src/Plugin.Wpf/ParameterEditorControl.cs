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
    public ParameterEditorControl(ParameterEditSession session,string parameterId)
    {
        var panel=new StackPanel();panel.Children.Add(Input);panel.Children.Add(Commit);panel.Children.Add(Error);Content=panel;
        Input.TextChanged+=(_,_)=>{session.SetText(parameterId,Input.Text);Error.Text=session.DraftError(parameterId)??"";};
        Commit.Click+=(_,_)=>{LastCommitSucceeded=session.TryCommit();Error.Text=LastCommitSucceeded?"":session.DraftError(parameterId)??"Invalid draft";};
    }
}
