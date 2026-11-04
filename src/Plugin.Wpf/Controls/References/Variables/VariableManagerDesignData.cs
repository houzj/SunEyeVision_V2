using System.Collections.ObjectModel;
namespace SunEyeVision.Plugin.Wpf.Controls.References.Variables;
/// <summary>Design-only values for the same manager composition, without a document or runtime.</summary>
public sealed class VariableManagerDesignData
{
 public sealed record Group(string Name)
 {public string EditingText{get;set;}=Name;public bool IsEditing{get;set;}public bool IsSelected{get;set;}=true;}
 public sealed record Row(int Index,string Name,string Description,string Type,object Value)
 {public bool IsInitialized{get;set;}public string InputSourceDisplayText=>"未绑定（使用初值）";}
 public sealed record Choice(string DisplayName,string Value)
 {public bool CanChoose=>true;public string AvailabilityReason=>"";}
 public ObservableCollection<Group> Groups{get;}=new(){new("变量组1")};
 public Group? SelectedGroup{get;set;}
 public Row? SelectedVariable{get;set;}
 public VariableManagerDesignData(){SelectedGroup=Groups[0];}
 public ObservableCollection<Row> CurrentVariables{get;}=new(){new(1,"产品编号","独立设计数据","String","A001"),new(2,"检测数量","","Integer",12L),new(3,"测量结果","数组详情","Double[]",new double[]{12.4,18.6,20.1})};
 public Choice[] TypeOptions{get;}=new[]{new Choice("字符串","String"),new Choice("整数","Integer"),new Choice("浮点数组","Double[]")};
 public bool CanEdit=>true;
 public string SearchText{get;set;}="";
 public int PageCount{get;set;}=12;
 public int CurrentPageNum{get;set;}=1;
 public int ItemsTotalCount=>CurrentVariables.Count;
 public string EmptyStatus=>"Collapsed";
 public string PageNumShowStatus=>"Visible";
}
