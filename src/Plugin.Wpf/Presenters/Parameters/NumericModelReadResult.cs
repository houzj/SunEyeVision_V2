namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

public enum NumericModelReadScope { Field,CurrentPage,All }
public enum NumericModelReadStatus { Updated,Unchanged,Unavailable,Failed,Retired,NotApplicable }
public sealed record NumericModelReadResult(string FieldId,NumericModelReadStatus Status,string? Error=null);
