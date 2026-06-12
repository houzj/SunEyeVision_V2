namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 日志条目 — 支持文本和错误标记，用于统一日志面板着色显示
    /// </summary>
    public class LogEntry
    {
        public string Text { get; set; } = string.Empty;

        public bool IsError { get; set; }
    }
}
