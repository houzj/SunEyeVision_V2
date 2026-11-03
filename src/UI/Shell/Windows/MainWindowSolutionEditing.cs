using SunEyeVision.Application;
using System;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private bool TryValidateSolutionEditors(out string error)=>TryUseDocumentEditors(false,out error);
    private bool TryCommitSolutionEditors(out string error)=>TryUseDocumentEditors(true,out error);
    private bool TryUseDocumentEditors(bool accept,out string error)
    {
        try
        {
            var session=DocumentManager.CurrentSession??throw new InvalidOperationException("当前没有方案编辑会话。");
            if(session.Editing.RegisteredScopeCount!=0)
            {
                if(accept)RefreshProductionPublication(session,session.Editing.Apply());
                else session.Editing.Validate();
            }
            error="";return true;
        }
        catch(Exception failure){error=failure.Message;return false;}
    }
}
