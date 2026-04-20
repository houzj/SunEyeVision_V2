using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SunEyeVision.Tools
{
    /// <summary>
    /// Dispatcher调用分析工具 - 检查参数顺序是否正确
    /// </summary>
    public class DispatcherCallAnalyzer
    {
        /// <summary>
        /// 分析指定目录中的所有C#文件，检查Dispatcher调用是否正确
        /// </summary>
        public static void AnalyzeDispatcherCalls(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            var csFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);
            int issuesFound = 0;
            foreach (var file in csFiles)
            {
                var content = File.ReadAllText(file);
                var issues = AnalyzeFile(file, content);

                if (issues.Count > 0)
                {
                    issuesFound += issues.Count;
                    foreach (var issue in issues)
                    {
                    }
                }
            }
        }

        /// <summary>
        /// 分析单个文件
        /// </summary>
        private static List<DispatcherIssue> AnalyzeFile(string filePath, string content)
        {
            var issues = new List<DispatcherIssue>();
            var lines = content.Split('\n');

            // 模式1: 错误的Dispatcher.BeginInvoke参数顺序
            // 错误: _dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => { ... });
            // 正确: _dispatcher.BeginInvoke(new Action(() => { ... }), DispatcherPriority.ContextIdle);
            var wrongPattern1 = new Regex(@"\.BeginInvoke\s*\(\s*DispatcherPriority\.[A-Za-z]+\s*,\s*\(\)", RegexOptions.Multiline);
            var wrongPattern2 = new Regex(@"\.BeginInvoke\s*\(\s*DispatcherPriority\.[A-Za-z]+\s*,\s*new\s+Action\s*\(", RegexOptions.Multiline);

            // 模式2: 错误的Dispatcher.Invoke参数顺序
            // 错误: _dispatcher.Invoke(DispatcherPriority.ContextIdle, () => { ... });
            // 正确: _dispatcher.Invoke(new Action(() => { ... }), DispatcherPriority.ContextIdle);
            var wrongPattern3 = new Regex(@"\.Invoke\s*\(\s*DispatcherPriority\.[A-Za-z]+\s*,\s*\(\)", RegexOptions.Multiline);
            var wrongPattern4 = new Regex(@"\.Invoke\s*\(\s*DispatcherPriority\.[A-Za-z]+\s*,\s*new\s+Action\s*\(", RegexOptions.Multiline);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNumber = i + 1;

                // 跳过注释
                if (line.Trim().StartsWith("//") || line.Trim().StartsWith("*"))
                    continue;

                if (wrongPattern1.IsMatch(line) || wrongPattern2.IsMatch(line))
                {
                    issues.Add(new DispatcherIssue
                    {
                        LineNumber = lineNumber,
                        Message = "错误的BeginInvoke参数顺序：应该是 BeginInvoke(Action, DispatcherPriority)，而不是 BeginInvoke(DispatcherPriority, Action)",
                        LineContent = line,
                        FilePath = filePath
                    });
                }

                if (wrongPattern3.IsMatch(line) || wrongPattern4.IsMatch(line))
                {
                    issues.Add(new DispatcherIssue
                    {
                        LineNumber = lineNumber,
                        Message = "错误的Invoke参数顺序：应该是 Invoke(Action, DispatcherPriority)，而不是 Invoke(DispatcherPriority, Action)",
                        LineContent = line,
                        FilePath = filePath
                    });
                }
            }

            return issues;
        }
    }

    /// <summary>
    /// Dispatcher问题信息
    /// </summary>
    public class DispatcherIssue
    {
        public int LineNumber { get; set; }
        public string Message { get; set; } = string.Empty;
        public string LineContent { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }
}
