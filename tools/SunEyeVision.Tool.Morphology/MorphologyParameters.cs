using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.Morphology.Models;

namespace SunEyeVision.Tool.Morphology
{
    /// <summary>
    /// 形态学操作类型枚举
    /// </summary>
    public enum MorphOperation
    {
        Erode = 0,
        Dilate = 1,
        Open = 2,
        Close = 3,
        Gradient = 4,
        TopHat = 5,
        BlackHat = 6
    }

    /// <summary>
    /// 形态学处理工具参数
    /// </summary>
    /// <remarks>
    /// 参数类继承 RegionParameters，自带区域管理功能。
    /// 支持检测区域 + 屏蔽区域，以及位置修正功能。
    ///
    /// 多态序列化（rule-010）：
    /// 使用 [JsonDerivedType] 特性标识参数类型，类型标识符为 "Morphology"。
    /// </remarks>
    [JsonDerivedType(typeof(MorphologyParameters), "Morphology")]
    public class MorphologyParameters : RegionParameters
    {
        #region 常量定义

        /// <summary>
        /// 核宽度最小值
        /// </summary>
        public const int KernelMin = 1;

        /// <summary>
        /// 核宽度最大值
        /// </summary>
        public const int KernelMax = 51;

        /// <summary>
        /// 迭代次数最小值
        /// </summary>
        public const int IterationsMin = 1;

        /// <summary>
        /// 迭代次数最大值
        /// </summary>
        public const int IterationsMax = 20;

        #endregion

        #region 基本参数

        /// <summary>
        /// 形态学操作类型
        /// </summary>
        [DisplayName("形态学操作")]
        public ParamValue<MorphOperation> Operation { get; set; } =
            ParamValue<MorphOperation>.CreateConstant(MorphOperation.Open, "Operation");

        /// <summary>
        /// 结构元素形状
        /// </summary>
        [DisplayName("结构元素形状")]
        public ParamValue<MorphShapes> KernelShape { get; set; } =
            ParamValue<MorphShapes>.CreateConstant(MorphShapes.Rect, "KernelShape");

        /// <summary>
        /// 结构元素宽度（像素）
        /// </summary>
        [DisplayName("核宽度")]
        public ParamValue<int> KernelWidth { get; set; } =
            ParamValue<int>.CreateConstant(3, "KernelWidth");

        /// <summary>
        /// 结构元素高度（像素）
        /// </summary>
        [DisplayName("核高度")]
        public ParamValue<int> KernelHeight { get; set; } =
            ParamValue<int>.CreateConstant(3, "KernelHeight");

        /// <summary>
        /// 迭代次数
        /// </summary>
        [DisplayName("迭代次数")]
        public ParamValue<int> Iterations { get; set; } =
            ParamValue<int>.CreateConstant(1, "Iterations");

        /// <summary>
        /// 边框类型
        /// </summary>
        [DisplayName("边框类型")]
        public ParamValue<BorderTypes> BorderType { get; set; } =
            ParamValue<BorderTypes>.CreateConstant(BorderTypes.Constant, "BorderType");

        #endregion

        #region 位置修正参数

        /// <summary>
        /// 是否启用位置修正
        /// </summary>
        [DisplayName("启用位置修正")]
        public bool IsPositionCorrectionEnabled { get; set; } = false;

        /// <summary>
        /// 位置修正绑定源（序列化引用）
        /// </summary>
        [JsonPropertyOrder(30)]
        [JsonPropertyName("positionCorrectionSource")]
        public ImageSourceReference? PositionCorrectionSource
        {
            get => _positionCorrectionSource;
            set => _positionCorrectionSource = value;
        }

        /// <summary>
        /// 位置修正绑定源显示文本
        /// </summary>
        [JsonPropertyOrder(31)]
        public string? PositionCorrectionBindingDisplayText { get; set; }

        /// <summary>
        /// 位置修正绑定源（运行时，不序列化）
        /// </summary>
        [JsonIgnore]
        public AvailableDataSource? PositionCorrectionBindingSource
        {
            get => _positionCorrectionBindingSource;
            set
            {
                if (SetProperty(ref _positionCorrectionBindingSource, value, "位置修正绑定源"))
                {
                    _positionCorrectionSource = ImageSourceReference.FromDataSource(value);
                }
            }
        }

        private AvailableDataSource? _positionCorrectionBindingSource;
        private ImageSourceReference? _positionCorrectionSource;

        /// <summary>
        /// 从 AvailableDataSources 列表恢复 PositionCorrectionBindingSource
        /// </summary>
        public bool RestorePositionCorrectionSource(IEnumerable<AvailableDataSource>? dataSources)
        {
            if (_positionCorrectionSource == null || dataSources == null)
                return false;

            var matched = _positionCorrectionSource.MatchDataSource(dataSources);
            if (matched != null)
            {
                _positionCorrectionBindingSource = matched;
                OnPropertyChanged(nameof(PositionCorrectionBindingSource));
                return true;
            }
            return false;
        }

        #endregion

        #region 结果显示配置

        /// <summary>
        /// 结果判断配置
        /// </summary>
        public MorphologyResultConfig ResultConfig { get; set; } = new();

        #endregion

        /// <summary>
        /// 验证参数
        /// </summary>
        public override ValidationResult Validate()
        {
            var result = base.Validate();

            // 核宽度范围检查
            if (KernelWidth.Value < KernelMin || KernelWidth.Value > KernelMax)
            {
                result.AddError($"核宽度必须在 {KernelMin}-{KernelMax} 范围内，当前值: {KernelWidth.Value}");
            }

            // 核高度范围检查
            if (KernelHeight.Value < KernelMin || KernelHeight.Value > KernelMax)
            {
                result.AddError($"核高度必须在 {KernelMin}-{KernelMax} 范围内，当前值: {KernelHeight.Value}");
            }

            // 核尺寸必须为奇数
            if (KernelWidth.Value % 2 == 0)
            {
                result.AddError($"核宽度必须为奇数，当前值: {KernelWidth.Value}");
            }
            if (KernelHeight.Value % 2 == 0)
            {
                result.AddError($"核高度必须为奇数，当前值: {KernelHeight.Value}");
            }

            // 迭代次数范围检查
            if (Iterations.Value < IterationsMin || Iterations.Value > IterationsMax)
            {
                result.AddError($"迭代次数必须在 {IterationsMin}-{IterationsMax} 范围内，当前值: {Iterations.Value}");
            }

            // 结果配置验证
            ValidateResultConfig(result);

            return result;
        }

        /// <summary>
        /// 验证结果判断配置
        /// </summary>
        private void ValidateResultConfig(ValidationResult result)
        {
            if (ResultConfig == null)
            {
                result.AddError("结果判断配置不能为空");
                return;
            }

            if (ResultConfig.IsAreaCheckEnabled)
            {
                if (ResultConfig.AreaMin >= ResultConfig.AreaMax)
                {
                    result.AddError($"总面积最小值({ResultConfig.AreaMin})必须小于最大值({ResultConfig.AreaMax})");
                }
            }

            if (ResultConfig.IsBlobCountCheckEnabled)
            {
                if (ResultConfig.BlobCountMin >= ResultConfig.BlobCountMax)
                {
                    result.AddError($"Blob数量最小值({ResultConfig.BlobCountMin})必须小于最大值({ResultConfig.BlobCountMax})");
                }
            }

            if (ResultConfig.IsMaxBlobAreaCheckEnabled)
            {
                if (ResultConfig.MaxBlobAreaMin >= ResultConfig.MaxBlobAreaMax)
                {
                    result.AddError($"最大Blob面积最小值({ResultConfig.MaxBlobAreaMin})必须小于最大值({ResultConfig.MaxBlobAreaMax})");
                }
            }
        }

        /// <summary>
        /// 获取形态学操作选项（中文）
        /// </summary>
        public static IReadOnlyList<string> GetOperationOptions()
        {
            return new List<string> { "腐蚀", "膨胀", "开运算", "闭运算", "梯度", "顶帽", "黑帽" };
        }

        /// <summary>
        /// 获取结构元素形状选项（中文）
        /// </summary>
        public static IReadOnlyList<string> GetShapeOptions()
        {
            return new List<string> { "矩形", "十字形", "椭圆形" };
        }
    }
}
