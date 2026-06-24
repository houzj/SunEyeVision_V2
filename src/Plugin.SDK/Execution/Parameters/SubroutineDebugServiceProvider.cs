using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.UI.Windows;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    public static class SubroutineDebugServiceProvider
    {
        private static ISubroutineDebugService? _service;

        public static void Register(ISubroutineDebugService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public static ISubroutineDebugService GetService()
        {
            if (_service == null)
                throw new InvalidOperationException(
                    "\u5b50\u7a0b\u5e8f\u8c03\u8bd5\u670d\u52a1\u672a\u6ce8\u518c\uff0c\u8bf7\u786e\u4fdd Subroutine \u5de5\u5177\u63d2\u4ef6\u5df2\u6b63\u786e\u52a0\u8f7d\u3002");
            return _service;
        }
    }
}
