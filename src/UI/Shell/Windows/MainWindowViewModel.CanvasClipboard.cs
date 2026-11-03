using System;
using System.Collections.Generic;
using System.Windows;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    public bool CanPasteCanvasSelection => false;
    public bool CopyCanvasSelection() { StatusText="节点复制尚未接入正式会话事务，暂不可用。"; return false; }
    public bool PasteCanvasSelection(Point? topLeft=null) { StatusText="节点粘贴尚未接入正式会话事务，暂不可用。"; return false; }
    public bool DuplicateCanvasNodes(IReadOnlyDictionary<CanvasNodeProjection,Point> positions)
    { StatusText="节点复制尚未接入正式会话事务，暂不可用。"; return false; }
}
