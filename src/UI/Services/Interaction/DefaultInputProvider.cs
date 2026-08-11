using System;
using System.Threading.Tasks;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shell;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Interaction
{
    /// <summary>
    /// 默认输入图像提供器
    /// </summary>
    public class DefaultInputProvider : IInputProvider
    {
        /// <summary>
        /// 异步获取输入图像
        /// </summary>
        /// <returns>图像数据,如果没有图像则返回null</returns>
        public async Task<object?> GetInputImageAsync()
        {
            await Task.Delay(10);

            // 返回null，让执行引擎使用默认的测试图表
            return null;
        }
    }
}
