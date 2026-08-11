using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SunEyeVision.UI.Shell
{
    /// <summary>
    /// WPF 文件夹浏览器辅助类
    /// 使用 Windows Shell API 实现文件夹选择功能，避免依赖 WinForms
    /// </summary>
    public static class FolderBrowserHelper
    {
        #region Windows API 声明

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHGetSpecialFolderLocation(IntPtr hwndOwner, int nFolder, out IntPtr ppidl);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr pv);

        #endregion

        #region 常量定义

        private const int BIF_RETURNONLYFSDIRS = 0x0001;  // 只返回文件系统目录
        private const int BIF_DONTGOBELOWDOMAIN = 0x0002;  // 不包含域级别以下的文件夹
        private const int BIF_STATUSTEXT = 0x0004;  // 包含状态文本
        private const int BIF_RETURNFSANCESTORS = 0x0008;  // 只返回文件系统的祖先
        private const int BIF_EDITBOX = 0x0010;  // 包含编辑框
        private const int BIF_VALIDATE = 0x0020;  // 验证输入
        private const int BIF_NEWDIALOGSTYLE = 0x0040;  // 新对话框样式
        private const int BIF_USENEWUI = BIF_NEWDIALOGSTYLE | BIF_EDITBOX;
        private const int BIF_BROWSEINCLUDEURLS = 0x0080;  // 允许浏览 URL
        private const int BIF_UAHINT = 0x0100;  // 使用用户帐户提示
        private const int BIF_NONEWFOLDERBUTTON = 0x0200;  // 不显示"新建文件夹"按钮
        private const int BIF_NOTRANSLATETARGETS = 0x0400;  // 不转换目标
        private const int BIF_SHAREABLE = 0x8000;  // 显示可共享的文件夹

        private const int CSIDL_DESKTOP = 0x0000;  // 桌面
        private const int CSIDL_PERSONAL = 0x0005;  // 我的文档
        private const int MAX_PATH = 260;

        #endregion

        #region 结构体定义

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct BROWSEINFO
        {
            public IntPtr hwndOwner;
            public IntPtr pidlRoot;
            public string pszDisplayName;
            public string lpszTitle;
            public int ulFlags;
            public IntPtr lpfn;
            public int lParam;
            public IntPtr iImage;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 显示文件夹选择对话框
        /// </summary>
        /// <param name="description">对话框描述文本</param>
        /// <param name="initialPath">初始路径</param>
        /// <param name="showNewFolderButton">是否显示"新建文件夹"按钮</param>
        /// <returns>用户选择的路径，如果用户取消则返回 null</returns>
        public static string? BrowseForFolder(
            string description = "选择文件夹",
            string? initialPath = null,
            bool showNewFolderButton = true)
        {
            IntPtr hwndOwner = IntPtr.Zero;
            IntPtr pidlRoot = IntPtr.Zero;

            try
            {
                // 设置根目录
                if (!string.IsNullOrEmpty(initialPath) && System.IO.Directory.Exists(initialPath))
                {
                    // 如果指定了初始路径，不设置根目录（从桌面开始）
                    pidlRoot = IntPtr.Zero;
                }
                else
                {
                    // 否则从我的文档开始
                    SHGetSpecialFolderLocation(IntPtr.Zero, CSIDL_PERSONAL, out pidlRoot);
                }

                // 准备 BROWSEINFO 结构
                var browseInfo = new BROWSEINFO
                {
                    hwndOwner = hwndOwner,
                    pidlRoot = pidlRoot,
                    pszDisplayName = new string('\0', MAX_PATH),
                    lpszTitle = description,
                    ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE
                };

                // 如果不显示"新建文件夹"按钮，添加相应的标志
                if (!showNewFolderButton)
                {
                    browseInfo.ulFlags |= BIF_NONEWFOLDERBUTTON;
                }

                // 显示对话框
                IntPtr pidlSelected = SHBrowseForFolder(ref browseInfo);

                if (pidlSelected == IntPtr.Zero)
                {
                    // 用户取消了对话框
                    return null;
                }

                // 获取选定的路径
                var pathBuilder = new StringBuilder(MAX_PATH);
                if (SHGetPathFromIDList(pidlSelected, pathBuilder))
                {
                    string selectedPath = pathBuilder.ToString();

                    // 释放 PIDL
                    CoTaskMemFree(pidlSelected);

                    return selectedPath;
                }

                // 释放 PIDL
                CoTaskMemFree(pidlSelected);

                return null;
            }
            finally
            {
                // 释放根目录 PIDL
                if (pidlRoot != IntPtr.Zero)
                {
                    CoTaskMemFree(pidlRoot);
                }
            }
        }

        /// <summary>
        /// 显示文件夹选择对话框（简化版）
        /// </summary>
        /// <param name="description">对话框描述文本</param>
        /// <returns>用户选择的路径，如果用户取消则返回 null</returns>
        public static string? BrowseForFolder(string description)
        {
            return BrowseForFolder(description, null, true);
        }

        #endregion
    }
}
