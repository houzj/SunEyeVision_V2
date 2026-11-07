using System;

namespace SunEyeVision.Plugin.Editing.Wpf.Execution.Results
{
    /// <summary>
    /// 执行进度
    /// </summary>
    /// <remarks>
    /// 用于报告工具执行过程中的进度信息。
    /// </remarks>
    public sealed class ExecutionProgress
    {
        /// <summary>
        /// 当前进度（0.0 - 1.0）
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// 当前阶段
        /// </summary>
        public string? Stage { get; set; }

        /// <summary>
        /// 进度消息
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// 当前步骤索引
        /// </summary>
        public int CurrentStep { get; set; }

        /// <summary>
        /// 总步骤数
        /// </summary>
        public int TotalSteps { get; set; }

        /// <summary>
        /// 当前处理的项目名称
        /// </summary>
        public string? CurrentItem { get; set; }

        /// <summary>
        /// 已处理数量
        /// </summary>
        public int ProcessedCount { get; set; }

        /// <summary>
        /// 总数量
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 用户自定义数据
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// 创建执行进度
        /// </summary>
        public ExecutionProgress()
        {
        }

        /// <summary>
        /// 创建执行进度
        /// </summary>
        public ExecutionProgress(double progress, string? message = null)
        {
            Progress = progress;
            Message = message;
        }

        /// <summary>
        /// 创建基于步骤的进度
        /// </summary>
        public static ExecutionProgress FromSteps(int currentStep, int totalSteps, string? message = null)
        {
            return new ExecutionProgress
            {
                CurrentStep = currentStep,
                TotalSteps = totalSteps,
                Progress = totalSteps > 0 ? (double)currentStep / totalSteps : 0,
                Message = message
            };
        }

        /// <summary>
        /// 创建基于数量的进度
        /// </summary>
        public static ExecutionProgress FromCount(int processed, int total, string? message = null)
        {
            return new ExecutionProgress
            {
                ProcessedCount = processed,
                TotalCount = total,
                Progress = total > 0 ? (double)processed / total : 0,
                Message = message
            };
        }

        /// <summary>
        /// 创建阶段进度
        /// </summary>
        public static ExecutionProgress FromStage(string stage, double progress, string? message = null)
        {
            return new ExecutionProgress
            {
                Stage = stage,
                Progress = progress,
                Message = message
            };
        }

        /// <summary>
        /// 获取百分比
        /// </summary>
        public int PercentComplete => (int)Math.Round(Progress * 100);

        /// <summary>
        /// 获取格式化的进度字符串
        /// </summary>
        public string GetProgressString()
        {
            if (!string.IsNullOrEmpty(Stage))
            {
                return $"{Stage}: {PercentComplete}%";
            }
            if (TotalSteps > 0)
            {
                return $"Step {CurrentStep}/{TotalSteps} ({PercentComplete}%)";
            }
            if (TotalCount > 0)
            {
                return $"{ProcessedCount}/{TotalCount} ({PercentComplete}%)";
            }
            return $"{PercentComplete}%";
        }

        /// <summary>
        /// 克隆进度
        /// </summary>
        public ExecutionProgress Clone()
        {
            return new ExecutionProgress
            {
                Progress = Progress,
                Stage = Stage,
                Message = Message,
                CurrentStep = CurrentStep,
                TotalSteps = TotalSteps,
                CurrentItem = CurrentItem,
                ProcessedCount = ProcessedCount,
                TotalCount = TotalCount,
                Tag = Tag
            };
        }

        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(Stage)) parts.Add($"Stage={Stage}");
            parts.Add($"Progress={PercentComplete}%");
            if (!string.IsNullOrEmpty(Message)) parts.Add($"Message={Message}");
            return $"ExecutionProgress({string.Join(", ", parts)})";
        }
    }

    /// <summary>
    /// 执行进度报告器
    /// </summary>
    public sealed class ExecutionProgressReporter
    {
        private readonly IProgress<ExecutionProgress>? _progress;
        private readonly IProgress<double>? _simpleProgress;
        private int _totalSteps;
        private int _currentStep;
        private string? _currentStage;

        /// <summary>
        /// 创建进度报告器
        /// </summary>
        public ExecutionProgressReporter(IProgress<ExecutionProgress>? progress = null, IProgress<double>? simpleProgress = null)
        {
            _progress = progress;
            _simpleProgress = simpleProgress;
        }

        /// <summary>
        /// 设置总步骤数
        /// </summary>
        public void SetTotalSteps(int totalSteps)
        {
            _totalSteps = totalSteps;
        }

        /// <summary>
        /// 设置当前阶段
        /// </summary>
        public void SetStage(string stage)
        {
            _currentStage = stage;
        }

        /// <summary>
        /// 报告进度
        /// </summary>
        public void Report(double progress, string? message = null)
        {
            var progressInfo = new ExecutionProgress
            {
                Progress = progress,
                Stage = _currentStage,
                Message = message
            };
            _progress?.Report(progressInfo);
            _simpleProgress?.Report(progress);
        }

        /// <summary>
        /// 报告步骤进度
        /// </summary>
        public void ReportStep(string? message = null)
        {
            _currentStep++;
            var progressInfo = ExecutionProgress.FromSteps(_currentStep, _totalSteps, message);
            progressInfo.Stage = _currentStage;
            _progress?.Report(progressInfo);
            _simpleProgress?.Report(progressInfo.Progress);
        }

        /// <summary>
        /// 报告数量进度
        /// </summary>
        public void ReportCount(int processed, int total, string? message = null)
        {
            var progressInfo = ExecutionProgress.FromCount(processed, total, message);
            progressInfo.Stage = _currentStage;
            _progress?.Report(progressInfo);
            _simpleProgress?.Report(progressInfo.Progress);
        }

        /// <summary>
        /// 报告完成
        /// </summary>
        public void ReportComplete(string? message = "Completed")
        {
            var progressInfo = new ExecutionProgress
            {
                Progress = 1.0,
                Stage = _currentStage,
                Message = message,
                CurrentStep = _totalSteps,
                TotalSteps = _totalSteps
            };
            _progress?.Report(progressInfo);
            _simpleProgress?.Report(1.0);
        }
    }
}
