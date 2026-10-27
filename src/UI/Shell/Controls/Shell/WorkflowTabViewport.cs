namespace SunEyeVision.UI.Shell.Controls.Shell;
/// <summary>恢复件30px边缘规则的宿主ScrollViewer适配；不改变画布视口。</summary>
internal static class WorkflowTabViewport
{
 internal static double SelectedOffset(double offset,double left,double width,double viewport,double maximum,bool first)
 {
  if(first)return 0;
  double target=offset;
  if(width>=viewport||left<0)target+=left-30;
  else if(left+width>viewport)target+=left+width-viewport+30;
  else
  {
   if(System.Math.Abs(left)<30)target-=30;
   if(System.Math.Abs(left+width-viewport)<30)target+=30;
  }
  return System.Math.Clamp(target,0,System.Math.Max(0,maximum));
 }
}
