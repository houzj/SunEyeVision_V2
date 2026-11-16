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
        PartialSuccess = 6,

        /// <summary>
        /// 未被选中而跳过。与 NotExecuted 不同：NotExecuted 是上游失败导致无法执行，
        /// 这里是分支控制流没有选它，属于正常运行结果，不参与合格判定。
        /// </summary>
        Skipped = 7
    }
