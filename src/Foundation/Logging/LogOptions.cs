namespace SunEyeVision.Foundation.Logging
{
    /// <summary>
    /// 日志配置项，对齐 VisionMaster 原件把日志作为"设置页一项"的做法：
    /// 由配置源注入，不写死在代码里。
    /// </summary>
    public sealed class LogOptions
    {
        /// <summary>配置节名称。</summary>
        public const string SectionName = "Log";

        /// <summary>日志根目录。为空时抛出异常，不做默认目录兜底。</summary>
        public string Directory { get; set; } = string.Empty;

        /// <summary>最小记录级别：Debug / Info / Warn / Error / Fatal。</summary>
        public string MinimumLevel { get; set; } = "Info";

        /// <summary>单个日志文件的最大字节数。</summary>
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024;

        /// <summary>保留的历史日志文件数量。</summary>
        public int MaxRetainedFiles { get; set; } = 30;
    }
}
