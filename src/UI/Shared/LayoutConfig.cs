using System;
using System.IO;
using System.Text.Json;
using SunEyeVision.Core.Services.Serialization;

namespace SunEyeVision.UI.Models
{
    /// <summary>
    /// 布局配置模型 - 用于保存和恢复主窗口列宽度和面板折叠状态
    /// </summary>
    public class LayoutConfig
    {
        // 默认值
        private const double DefaultLeftColumnWidth = 260.0;
        private const double DefaultRightColumnWidth = 500.0;
        private const double DefaultSplitterWidth = 5.0;

        // 宽度限制
        private const double MinLeftColumnWidth = 200.0;
        private const double MaxLeftColumnWidth = 600.0;
        private const double MinRightColumnWidth = 300.0;
        private const double MaxRightColumnWidth = 900.0;
        private const double MinMiddleColumnWidth = 400.0;

        /// <summary>
        /// 左侧列宽度
        /// </summary>
        public double LeftColumnWidth { get; set; } = DefaultLeftColumnWidth;

        /// <summary>
        /// 右侧列宽度
        /// </summary>
        public double RightColumnWidth { get; set; } = DefaultRightColumnWidth;

        /// <summary>
        /// 左侧面板是否折叠
        /// </summary>
        public bool IsLeftPanelCollapsed { get; set; } = false;

        /// <summary>
        /// 右侧面板是否折叠
        /// </summary>
        public bool IsRightPanelCollapsed { get; set; } = false;

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        /// <returns>是否保存成功</returns>
        public bool Save()
        {
            try
            {
                var configPath = GetConfigFilePath();
                var directory = Path.GetDirectoryName(configPath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, JsonSerializationOptions.Default);
                File.WriteAllText(configPath, json);
                return true;
            }
            catch (Exception)
            {
                // 保存失败时静默处理，使用默认值
                return false;
            }
        }

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        /// <returns>配置对象，如果加载失败返回默认配置</returns>
        public static LayoutConfig Load()
        {
            try
            {
                var configPath = GetConfigFilePath();

                if (!File.Exists(configPath))
                {
                    return new LayoutConfig();
                }

                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<LayoutConfig>(json, JsonSerializationOptions.Default);

                if (config == null)
                {
                    return new LayoutConfig();
                }

                // 验证并修正超出范围的值
                config.ValidateAndCorrect();

                return config;
            }
            catch (Exception)
            {
                // 加载失败时返回默认配置
                return new LayoutConfig();
            }
        }

        /// <summary>
        /// 验证并修正超出范围的值
        /// </summary>
        public void ValidateAndCorrect()
        {
            LeftColumnWidth = Math.Max(MinLeftColumnWidth, Math.Min(MaxLeftColumnWidth, LeftColumnWidth));
            RightColumnWidth = Math.Max(MinRightColumnWidth, Math.Min(MaxRightColumnWidth, RightColumnWidth));
        }

        /// <summary>
        /// 获取最小左侧列宽度
        /// </summary>
        public static double GetMinLeftColumnWidth() => MinLeftColumnWidth;

        /// <summary>
        /// 获取最大左侧列宽度
        /// </summary>
        public static double GetMaxLeftColumnWidth() => MaxLeftColumnWidth;

        /// <summary>
        /// 获取最小右侧列宽度
        /// </summary>
        public static double GetMinRightColumnWidth() => MinRightColumnWidth;

        /// <summary>
        /// 获取最大右侧列宽度
        /// </summary>
        public static double GetMaxRightColumnWidth() => MaxRightColumnWidth;

        /// <summary>
        /// 获取最小中间列宽度
        /// </summary>
        public static double GetMinMiddleColumnWidth() => MinMiddleColumnWidth;

        /// <summary>
        /// 获取分隔符宽度
        /// </summary>
        public static double GetSplitterWidth() => DefaultSplitterWidth;

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void Reset()
        {
            LeftColumnWidth = DefaultLeftColumnWidth;
            RightColumnWidth = DefaultRightColumnWidth;
            IsLeftPanelCollapsed = false;
            IsRightPanelCollapsed = false;
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        private static string GetConfigFilePath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "SunEyeVision", "layout.config");
        }
    }
}
