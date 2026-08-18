using System.Collections.Generic;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 图像输入源持久化配置 - 随解决方案文件保存/恢复图像源状态
    /// </summary>
    /// <remarks>
    /// 只存路径与状态（不存图片本身），对齐参考项目 VisionMaster 的 SrcFilePaths 持久化模式。
    /// 放在 Workflow 层而非 UI 层，避免跨层引用 UI 的 ImageInputSource/ImageRunMode。
    /// 保存方案时由 UI 层把 ImageInputSource 转成此配置写入节点；
    /// 打开方案时由 UI 层据此重建 ImageInputSource。
    /// </remarks>
    public class ImageSourceConfig
    {
        /// <summary>
        /// 关联节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 所有图像文件路径（按添加顺序）
        /// </summary>
        public List<string> FilePaths { get; set; } = new();

        /// <summary>
        /// 当前选中图像索引（-1 表示无选中）
        /// </summary>
        public int CurrentIndex { get; set; } = -1;

        /// <summary>
        /// 是否启用自动切换
        /// </summary>
        public bool AutoSwitchEnabled { get; set; } = true;

        /// <summary>
        /// 运行模式：0=运行全部，1=运行选择
        /// </summary>
        public int RunMode { get; set; }

        /// <summary>
        /// 运行选择模式下勾选的图像路径（保留勾选路径）
        /// </summary>
        public List<string> ForRunPaths { get; set; } = new();
    }
}
