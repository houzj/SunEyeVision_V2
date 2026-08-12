using System.Collections.Generic;

namespace SunEyeVision.UI.Services
{
    /// <summary>
    /// 运行时参数服务默认实现 - 基于字典的编辑态运行时参数存储。
    /// </summary>
    public class RuntimeParameterService : IRuntimeParameterService
    {
        private readonly Dictionary<string, object> _parameters = new();

        public void Set<T>(string key, T value)
        {
            _parameters[key] = value!;
        }

        public T? Get<T>(string key)
        {
            if (_parameters.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return default;
        }

        public Dictionary<string, object> GetAll()
        {
            return new Dictionary<string, object>(_parameters);
        }

        public void Clear()
        {
            _parameters.Clear();
        }
    }
}
