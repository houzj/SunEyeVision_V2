namespace SunEyeVision.Plugin.Runtime;

public enum ExecutionStatus
    {
        /// <summary>
        /// 未执行
        /// </summary>
        NotExecuted = 0,

        /// <summary>
        /// 执行中
        /// </summary>
        Running = 1,

        /// <summary>
        /// 执行成功
        /// </summary>
        Success = 2,

        /// <summary>
        /// 执行失败
        /// </summary>
        Failed = 3,

        /// <summary>
        /// 执行超时
        /// </summary>
        Timeout = 4,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled = 5,

        /// <summary>
        /// 部分成功
        /// </summary>
        PartialSuccess = 6
    }
