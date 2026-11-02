using System;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Shell.Controls.Common;

namespace SunEyeVision.UI.Shell.Controls.Shell;

/// <summary>主工作区布局偏好与真实分栏控件之间的转换。</summary>
internal static class MainWorkspaceLayout
{
    internal static void Restore(LayoutConfig config, ColumnDefinition right, CollapsibleSplitterBox splitter)
    {
        if (splitter.IsCollapsed) splitter.ToggleCollapsed();
        if (double.IsFinite(config.RightColumnWidth) && config.RightColumnWidth > 0)
        {
            var width = config.RightColumnWidth;
            if (splitter.Parent is Grid grid && grid.ActualWidth > 0)
            {
                double otherMinima = 0;
                foreach (var column in grid.ColumnDefinitions)
                    if (!ReferenceEquals(column, right)) otherMinima += column.MinWidth;
                width = Math.Min(width, Math.Max(right.MinWidth, grid.ActualWidth - otherMinima));
            }
            right.Width = new GridLength(width);
        }
        if (config.IsRightPanelCollapsed) splitter.ToggleCollapsed();
    }

    internal static void Capture(LayoutConfig config, ColumnDefinition right, CollapsibleSplitterBox splitter)
    {
        config.IsRightPanelCollapsed = splitter.IsCollapsed;
        var width = splitter.IsCollapsed ? splitter.ExpandedPixelSize : right.ActualWidth;
        if (width > 0) config.RightColumnWidth = width;
    }
}
