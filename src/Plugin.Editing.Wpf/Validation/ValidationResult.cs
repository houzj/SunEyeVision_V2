using System.Collections.Generic;

namespace SunEyeVision.Plugin.Editing.Wpf.Validation
{
    /// <summary>
    /// 参数验证结果
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 是否验证通过
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// 创建验证成功的结果
        /// </summary>
        public static ValidationResult Success()
        {
            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// 创建验证失败的结果
        /// </summary>
        /// <param name="error">错误信息</param>
        public static ValidationResult Failure(string error)
        {
            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { error }
            };
        }

        /// <summary>
        /// 创建验证失败的结果(多个错误)
        /// </summary>
        /// <param name="errors">错误信息列表</param>
        public static ValidationResult Failure(IEnumerable<string> errors)
        {
            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<string>(errors)
            };
        }

        /// <summary>
        /// 添加错误信息
        /// </summary>
        /// <param name="error">错误信息</param>
        public void AddError(string error)
        {
            IsValid = false;
            Errors.Add(error);
        }

        /// <summary>
        /// 添加警告信息
        /// </summary>
        /// <param name="warning">警告信息</param>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        /// <summary>
        /// 合并另一个验证结果
        /// </summary>
        /// <param name="other">其他验证结果</param>
        public void Merge(ValidationResult? other)
        {
            if (other == null) return;

            if (!other.IsValid)
            {
                IsValid = false;
                Errors.AddRange(other.Errors);
            }
            Warnings.AddRange(other.Warnings);
        }
    }
}
