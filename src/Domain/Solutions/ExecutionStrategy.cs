using System;
using System.Collections.Generic;


namespace SunEyeVision.Domain.Solutions;

/// <summary>
/// 执行模式枚举
/// </summary>
public enum WorkflowExecutionMode
{
    /// <summary>
    /// 顺序执行
    /// </summary>
    Sequential,

    /// <summary>
    /// 并行执行
    /// </summary>
    Parallel,

    /// <summary>
    /// 混合模式
    /// </summary>
    Hybrid
}

/// <summary>
/// 错误处理策略枚举
/// </summary>
public enum ErrorHandlingStrategy
{
    /// <summary>
    /// 继续执行
    /// </summary>
    Continue,

    /// <summary>
    /// 停止执行
    /// </summary>
    Stop,

    /// <summary>
    /// 重试
    /// </summary>
    Retry,

    /// <summary>
    /// 跳过
    /// </summary>
    Skip
}

/// <summary>
/// 执行策略模型
/// </summary>
/// <remarks>
/// 用于配置工作流的执行策略，包括并发控制、错误处理等。
/// </remarks>
public class ExecutionStrategy
{
    private WorkflowExecutionMode _mode = WorkflowExecutionMode.Sequential;
    private int _maxConcurrency = 1;
    private bool _enableAsyncExecution = false;
    private bool _enableProgressReport = true;
    private bool _enableLogging = true;
    private ErrorHandlingStrategy _errorHandling = ErrorHandlingStrategy.Stop;
    private int _timeout = 30000; // 30秒

    /// <summary>
    /// 执行模式
    /// </summary>
    public WorkflowExecutionMode Mode
    {
        get => _mode;
        internal set => _mode = value;
    }

    /// <summary>
    /// 最大并发数
    /// </summary>
    public int MaxConcurrency
    {
        get => _maxConcurrency;
        internal set => _maxConcurrency = value;
    }

    /// <summary>
    /// 是否启用异步执行
    /// </summary>
    public bool EnableAsyncExecution
    {
        get => _enableAsyncExecution;
        internal set => _enableAsyncExecution = value;
    }

    /// <summary>
    /// 是否启用进度报告
    /// </summary>
    public bool EnableProgressReport
    {
        get => _enableProgressReport;
        internal set => _enableProgressReport = value;
    }

    /// <summary>
    /// 是否启用日志记录
    /// </summary>
    public bool EnableLogging
    {
        get => _enableLogging;
        internal set => _enableLogging = value;
    }

    /// <summary>
    /// 错误处理策略
    /// </summary>
    public ErrorHandlingStrategy ErrorHandling
    {
        get => _errorHandling;
        internal set => _errorHandling = value;
    }

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int Timeout
    {
        get => _timeout;
        internal set => _timeout = value;
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime ModifiedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 无参构造函数
    /// </summary>
    public ExecutionStrategy()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public ExecutionStrategy(WorkflowExecutionMode mode)
    {
        Mode = mode;
        if (mode == WorkflowExecutionMode.Parallel || mode == WorkflowExecutionMode.Hybrid)
        {
            MaxConcurrency = 4;
        }
    }

    /// <summary>
    /// 克隆执行策略
    /// </summary>
    public ExecutionStrategy Clone()
    {
        return new ExecutionStrategy
        {
            Mode = Mode,
            MaxConcurrency = MaxConcurrency,
            EnableAsyncExecution = EnableAsyncExecution,
            EnableProgressReport = EnableProgressReport,
            EnableLogging = EnableLogging,
            ErrorHandling = ErrorHandling,
            Timeout = Timeout,
            CreatedTime = CreatedTime,
            ModifiedTime = DateTime.Now
        };
    }

    /// <summary>
    /// 验证执行策略
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        if (MaxConcurrency <= 0)
        {
            errors.Add("最大并发数必须大于0");
        }

        if (MaxConcurrency > 16)
        {
            errors.Add("最大并发数建议不超过16");
        }

        if (Timeout <= 0)
        {
            errors.Add("超时时间必须大于0");
        }

        if ((Mode == WorkflowExecutionMode.Parallel || Mode == WorkflowExecutionMode.Hybrid) && MaxConcurrency <= 1)
        {
            errors.Add("并行或混合模式下，最大并发数应大于1");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 获取执行模式名称
    /// </summary>
    public string GetModeName()
    {
        switch (Mode)
        {
            case WorkflowExecutionMode.Sequential:
                return "顺序执行";
            case WorkflowExecutionMode.Parallel:
                return "并行执行";
            case WorkflowExecutionMode.Hybrid:
                return "混合模式";
            default:
                return "Unknown";
        }
    }

    /// <summary>
    /// 获取错误处理策略名称
    /// </summary>
    public string GetErrorHandlingName()
    {
        switch (ErrorHandling)
        {
            case ErrorHandlingStrategy.Continue:
                return "继续执行";
            case ErrorHandlingStrategy.Stop:
                return "停止执行";
            case ErrorHandlingStrategy.Retry:
                return "重试";
            case ErrorHandlingStrategy.Skip:
                return "跳过";
            default:
                return "Unknown";
        }
    }
}

