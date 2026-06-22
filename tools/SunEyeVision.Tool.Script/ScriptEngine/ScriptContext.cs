using System;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 脚本上下文 — 内部实现，桥接 ScriptTool 和脚本实例。
    /// Script 通过此对象访问外部委托，用户无需直接接触。
    /// </summary>
    internal sealed class ScriptContext
    {
        #region 委托注入

        public Func<string, object?>? GetGlobalValue { get; set; }
        public Action<string, object?>? SetGlobalValue { get; set; }
        public Action<string>? LogInfoAction { get; set; }
        public Action<string>? LogWarningAction { get; set; }
        public Action<string>? LogErrorAction { get; set; }

        #endregion

        #region 公开方法（由 Script 调用）

        public T? GetGlobal<T>(string name) where T : class
        {
            var value = GetGlobalValue?.Invoke(name);
            return value as T;
        }

        public void SetGlobal(string name, object? value)
        {
            SetGlobalValue?.Invoke(name, value);
        }

        public void LogInfo(string message)
        {
            LogInfoAction?.Invoke(message);
        }

        public void LogWarning(string message)
        {
            LogWarningAction?.Invoke(message);
        }

        public void LogError(string message)
        {
            LogErrorAction?.Invoke(message);
        }

        #endregion
    }
}
