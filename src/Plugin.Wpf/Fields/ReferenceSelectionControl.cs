using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Fields;

public sealed record DeclaredReferenceCandidate(string Label,string NodeId,string? ScopeId,Port Output,
    IReadOnlyList<string> MemberPath)
{
    public override string ToString()=>Label;
    public TypeRef SelectedType
    {
        get
        {
            var type=Output.Type;
            foreach(var member in MemberPath)
            {while(type.Shape==ValueShape.Sequence)type=type.Element!;type=type.Fields.Single(field=>field.Id==member).Type;}
            return type;
        }
    }
    public OutputReference Reference(BindingSelection selection)=>new(NodeId,Output.Id,Output.Type,MemberPath,selection,ScopeId,Output.Storage);
}

/// <summary>UI projection of a supplied declaration catalog. The host owns the window and final graph validation.</summary>
public sealed class ReferenceSelectionControl : UserControl
{
    private readonly ListBox _sources=new(){MinHeight=200,DisplayMemberPath=nameof(DeclaredReferenceCandidate.Label)};
    private readonly ComboBox _quantity=new(){ItemsSource=new[]{"全部元素","指定序号","绑定序号"},SelectedIndex=0};
    private readonly TextBox _index=new(){Text="0"};
    private readonly ComboBox _indexSource=new(){DisplayMemberPath=nameof(DeclaredReferenceCandidate.Label)};
    private readonly TextBlock _error=new(){Foreground=Brushes.OrangeRed,TextWrapping=TextWrapping.Wrap};
    private readonly Func<OutputReference,string?> _validate;
    public DeclaredReferenceCandidate? SelectedCandidate {get=>_sources.SelectedItem as DeclaredReferenceCandidate;set=>_sources.SelectedItem=value;}
    public int SelectionMode {get=>_quantity.SelectedIndex;set=>_quantity.SelectedIndex=value;}
    public string IndexText {get=>_index.Text;set=>_index.Text=value;}
    public DeclaredReferenceCandidate? IndexCandidate {get=>_indexSource.SelectedItem as DeclaredReferenceCandidate;set=>_indexSource.SelectedItem=value;}
    public ReferenceSelectionControl(IEnumerable<DeclaredReferenceCandidate> candidates,Func<OutputReference,string?> validate)
    {
        _validate=validate;var all=candidates.ToArray();_sources.ItemsSource=all;
        _indexSource.ItemsSource=all.Where(candidate=>candidate.Output.Type.Shape!=ValueShape.Sequence&&candidate.SelectedType==TypeRef.ScalarOf(ScalarKind.Int64)).ToArray();
        var panel=new DockPanel{Margin=new Thickness(12)};
        var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);
        bottom.Children.Add(new TextBlock{Text="集合取值",Margin=new Thickness(0,8,0,3)});bottom.Children.Add(_quantity);
        bottom.Children.Add(_index);bottom.Children.Add(_indexSource);bottom.Children.Add(_error);
        _quantity.SelectionChanged+=(_,_)=>RefreshQuantity();panel.Children.Add(bottom);panel.Children.Add(_sources);Content=panel;RefreshQuantity();
    }
    public bool TryGetReference(out OutputReference? reference)
    {
        reference=null;
        try
        {
            var source=SelectedCandidate??throw new ArgumentException("请选择声明的输出字段。");
            BindingSelection selection=SelectionMode switch {
                0=>BindingSelection.All,
                1=>new BindingSelection.AtConstant(int.Parse(IndexText,CultureInfo.InvariantCulture)),
                2=>new BindingSelection.AtField((IndexCandidate??throw new ArgumentException("请选择序号来源。")).Reference(BindingSelection.All)),
                _=>throw new ArgumentException("请选择集合取值方式。")};
            var proposed=source.Reference(selection);var error=_validate(proposed);
            if(!string.IsNullOrEmpty(error))throw new ArgumentException(error);
            reference=proposed;_error.Text="";return true;
        }
        catch(Exception exception) when(exception is ArgumentException or FormatException or OverflowException or InvalidOperationException)
        {_error.Text=exception.Message;return false;}
    }
    private void RefreshQuantity()
    {_index.Visibility=SelectionMode==1?Visibility.Visible:Visibility.Collapsed;_indexSource.Visibility=SelectionMode==2?Visibility.Visible:Visibility.Collapsed;}
}
