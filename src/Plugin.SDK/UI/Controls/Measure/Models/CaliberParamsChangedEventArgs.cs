using System;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models
{
    /// <summary>
    /// 卡尺参数变更事件参数
    /// </summary>
    /// <remarks>
    /// 携带当前卡尺渲染参数（法线全宽 / 切线全长 / 数量），
    /// 面板/主卡尺手柄编辑后触发，宿主据此回写工具参数（MeasureLength1/2、NumMeasures）。
    /// </remarks>
    public class CaliberParamsChangedEventArgs : EventArgs
    {
        /// <summary>卡尺宽度（法线方向全宽）</summary>
        public double Width { get; }

        /// <summary>卡尺长度（切线方向全长）</summary>
        public double Length { get; }

        /// <summary>测量带数量</summary>
        public int Count { get; }

        /// <summary>
        /// 创建卡尺参数变更事件参数
        /// </summary>
        public CaliberParamsChangedEventArgs(double width, double length, int count)
        {
            Width = width;
            Length = length;
            Count = count;
        }
    }
}
