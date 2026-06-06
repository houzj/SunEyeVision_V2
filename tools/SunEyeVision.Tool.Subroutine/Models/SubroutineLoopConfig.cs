using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Workflow;

namespace SunEyeVision.Tool.Subroutine.Models
{
    /// <summary>
    /// 子程序循环类型（原定义在 SubroutineNode.cs，迁移至此）
    /// </summary>
    public enum LoopType
    {
        /// <summary>固定次数</summary>
        FixedCount,
        /// <summary>条件循环</summary>
        ConditionBased,
        /// <summary>数据驱动</summary>
        DataDriven
    }

    /// <summary>
    /// 子程序循环配置
    /// </summary>
    public class SubroutineLoopConfig : ObservableObject
    {
        private bool _enableLoop;
        private LoopType _loopType = LoopType.FixedCount;
        private int _maxIterations = 10;
        private string _loopCondition = string.Empty;

        /// <summary>启用循环</summary>
        public bool EnableLoop
        {
            get => _enableLoop;
            set => SetProperty(ref _enableLoop, value);
        }

        /// <summary>循环类型</summary>
        public LoopType LoopType
        {
            get => _loopType;
            set => SetProperty(ref _loopType, value);
        }

        /// <summary>最大迭代次数</summary>
        public int MaxIterations
        {
            get => _maxIterations;
            set => SetProperty(ref _maxIterations, value);
        }

        /// <summary>循环条件表达式</summary>
        public string LoopCondition
        {
            get => _loopCondition;
            set => SetProperty(ref _loopCondition, value);
        }

        /// <summary>循环类型显示文本</summary>
        public string LoopTypeDisplay => LoopType switch
        {
            LoopType.FixedCount => "固定次数",
            LoopType.ConditionBased => "条件循环",
            LoopType.DataDriven => "数据驱动",
            _ => "未知"
        };
    }
}
