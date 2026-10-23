using System;

namespace SunEyeVision.Plugin.SDK.Scripting
{
    /// <summary>
    /// 脚本基类 — 用户编写的脚本必须继承此类。
    /// 提供日志、全局变量等 protected 方法，用户脚本中可直接调用。
    /// </summary>
    public abstract class Script
    {
        public ScriptContext Context { get; set; } = null!;

        /// <summary>
        /// 初始化阶段：加载资源、初始化变量
        /// </summary>
        public abstract void Init();

        /// <summary>
        /// 处理阶段：核心算法逻辑
        /// </summary>
        public abstract void Process();

        /// <summary>
        /// 释放阶段：清理资源（可选重写）
        /// </summary>
        public virtual void Release() { }

        #region 封装 API 方法（protected，用户可直接调用）

        protected System.Threading.CancellationToken CancellationToken => Context.CancellationToken;
        protected void ThrowIfCancellationRequested() => Context.ThrowIfCancellationRequested();

        protected void LogInfo(string message) => Context.LogInfo(message);
        protected void LogWarning(string message) => Context.LogWarning(message);
        protected void LogError(string message) => Context.LogError(message);

        protected T? GetGlobal<T>(string name) where T : class
            => Context.GetGlobal<T>(name);

        protected void SetGlobal(string name, object? value)
            => Context.SetGlobal(name, value);

        #endregion
    }
}
