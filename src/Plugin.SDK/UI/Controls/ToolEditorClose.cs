using System;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.UI.Controls;

/// <summary>Closing is not execution: invalid input must have an explicit escape path.</summary>
public static class ToolEditorClose
{
    public static bool TryClose(FrameworkElement editor, Window owner, Func<string, bool>? confirmDiscard = null)
    {
        if (ToolEditorCommit.GetDraftContext(editor) is not null)
        {
            ToolEditorCommit.DiscardPendingEdits(editor);
            return true;
        }
        if (ToolEditorCommit.TryCommit(editor, out var error)) return true;
        var prompt = error + "\n\n是否关闭窗口并放弃尚未提交的控件编辑？\n已实时写入参数的修改会保留，不会回滚；无效配置仍不能执行。\n选择“否”返回继续修改。";
        var discard = confirmDiscard?.Invoke(prompt) ?? MessageBox.Show(owner, prompt, "配置未提交", MessageBoxButton.YesNo,
            MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
        if (!discard) return false;
        ToolEditorCommit.DiscardPendingEdits(editor);
        return true;
    }
}
