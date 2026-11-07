using System;

namespace SunEyeVision.Foundation.Logging
{
    /// <summary>
    /// 日志服务契约，形态对齐 VisionMaster 原件的 <c>H.Services.Logger.ILogService</c>：
    /// 级别即方法，不做单一入口 + 扩展别名。
    /// </summary>
    /// <remarks>
    /// 与原件一致的部分：方法集合（Debug/Trace/Info/Warn/Error/Fatal）、按级别分方法、支持批量消息与批量异常。
    /// 刻意不照抄的部分：原件用无参 <c>catch { }</c> 吞掉配置读取异常，本契约要求实现方让异常向上传播。
    /// </remarks>
    public interface ILogService
    {
        /// <summary>记录诊断级别日志。</summary>
        void Debug(params string[] messages);

        /// <summary>记录跟踪级别日志。</summary>
        void Trace(params string[] messages);

        /// <summary>记录信息级别日志。</summary>
        void Info(params string[] messages);

        /// <summary>记录警告级别日志。</summary>
        void Warn(params string[] messages);

        /// <summary>记录错误级别日志。</summary>
        void Error(params string[] messages);

        /// <summary>记录错误级别日志（异常集合）。</summary>
        void Error(params Exception[] exceptions);

        /// <summary>记录致命级别日志。</summary>
        void Fatal(params string[] messages);

        /// <summary>记录致命级别日志（异常集合）。</summary>
        void Fatal(params Exception[] exceptions);
    }
}
