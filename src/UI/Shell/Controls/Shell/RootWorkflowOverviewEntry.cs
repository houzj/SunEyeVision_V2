using System;
using System.Windows;
using System.Windows.Input;
namespace SunEyeVision.UI.Shell.Controls.Shell;
/// <summary>Immutable presentation only. Null statistics mean no trustworthy completed-run value.</summary>
public sealed record RootWorkflowOverviewEntry(string RootId, string Name, bool IsReady, bool IsRunning,
 bool IsContinuousRunning, bool CanRun, bool CanConfigure, bool CanMove, long? RunCount,
 double? ElapsedMilliseconds, double? X, double? Y);
public enum RootOverviewRunKind { Single, ToggleContinuous }
public sealed class RootOverviewRequestEventArgs : EventArgs
{
 public string DocumentIdentity {get;}
 public long Revision {get;}
 public string RootId { get; }
 public RootOverviewRunKind RunKind { get; }
 public RootOverviewRequestEventArgs(string rootId, RootOverviewRunKind runKind = RootOverviewRunKind.Single,string documentIdentity="",long revision=0)
 { RootId = rootId; RunKind = runKind; DocumentIdentity=documentIdentity;Revision=revision; }
}
public sealed class RootOverviewPositionEventArgs : EventArgs
{
 public string RootId { get; }
 public double X { get; }
 public double Y { get; }
 public string DocumentIdentity { get; }
 public long Revision { get; }
 public RootOverviewPositionEventArgs(string rootId,double x,double y,string documentIdentity,long revision)
 { RootId=rootId; X=x; Y=y; DocumentIdentity=documentIdentity; Revision=revision; }
}
/// <summary>Pan values are nonnegative ScrollViewer offsets in scaled device independent pixels.</summary>
public readonly record struct RootOverviewViewport(double Scale,double PanX,double PanY);
internal static class RootOverviewGeometry
{
 internal const double PageSize=5000, CardWidth=140, CardHeight=120;
 internal static Point Position(double x,double y) => new(
  Math.Clamp(Math.Round(x/5)*5,0,PageSize-CardWidth), Math.Clamp(Math.Round(y/5)*5,0,PageSize-CardHeight));
 internal static bool AcceptDrop(bool copy,DragDropKeyStates keys)
 {
  var modifiers=keys & (DragDropKeyStates.ControlKey|DragDropKeyStates.ShiftKey|DragDropKeyStates.AltKey);
  return copy ? modifiers==DragDropKeyStates.ControlKey : (modifiers & DragDropKeyStates.ControlKey)==0;
 }
}
